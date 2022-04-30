using MqttSql.Configurations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using static MqttSql.Configurations.DatabaseConfiguration;

using ServiceConfigurationJson = MqttSql.ConfigurationsJson.ServiceConfiguration;

namespace MqttSql
{
    public static class ConfigurationLoader
    {
        public static (Settings Settings, BrokerConfiguration[] Brokers) LoadJsonConfig(
            string configPath,
            Func<string?, string>? GetSQLiteDbPath = null,
            Action<string>? logger = null)
        {
            var serviceConfig = LoadServiceConfigurationJson(configPath, logger);
            return (new Settings(serviceConfig.Settings), GetBrokersFromConfig(serviceConfig, GetSQLiteDbPath, logger));
        }

        private static ServiceConfigurationJson LoadServiceConfigurationJson(string configPath, Action<string>? logger = null)
        {
            logger?.Invoke($"Loading configuration \"{configPath}\":");
            string json = File.ReadAllText(configPath);

            foreach (var regex in new Regex[3] { ConnectionStringRegex, PythonPathRegex, PythonParserScriptPathRegex })
            {
                json = regex.Replace(
                    json,
                    m => m.Groups[2].Value.Contains(@"\\") ? m.Value : (m.Groups[1].Value + m.Groups[2].Value.Replace(@"\", @"\\") + m.Groups[3].Value)
                );
            }
            json = PythonParserScriptPathsArrayRegex.Replace(
                json,
                m => m.Groups[1].Value
                    + string.Concat(m.Groups[2].Captures.Zip(m.Groups[3].Captures,
                        (path, delimiter) => (path.Value.Contains(@"\\") ? path.Value : path.Value.Replace(@"\", @"\\")) + delimiter.Value))
                    + m.Groups[4].Value
            );

#if DEBUG
            logger?.Invoke(json);
#else
            logger?.Invoke(PasswordRegex.Replace(
                json,
                m => m.Groups[1].Value + new string('*', m.Groups[2].Length) + m.Groups[3].Value)
            );
#endif

            var jsonOptions = new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                MaxDepth = 7
            };
            ServiceConfigurationJson? configuration = JsonSerializer.Deserialize<ServiceConfigurationJson>(json, jsonOptions);
            if (configuration == null) throw new JsonException($"Failed to Deserialize the service configuration file \"{configPath}\"");

            logger?.Invoke("Configuration loaded:");
            logger?.Invoke(configuration.ToString());

            return configuration;
        }

        private static BrokerConfiguration[] GetBrokersFromConfig(
            ServiceConfigurationJson configuration,
            Func<string?, string>? GetSQLiteDbPath = null,
            Action<string>? logger = null)
        {
            var databases = new Dictionary<string, DatabaseConfiguration>(configuration.Databases.Length + 1);
            if (configuration.Databases.Length == 0)
            {
                databases.Add("sqlite", new DatabaseConfiguration(DatabaseType.SQLite, $"Data Source={GetSQLiteDbPath?.Invoke(null) ?? "./database.sqlite"};Version=3;"));
            }
            else
            {
                HashSet<string>? connStrings = logger != null ? new(databases.Keys.Count) : null;
                foreach (var db in configuration.Databases)
                {
                    if (string.IsNullOrEmpty(db.Name))
                    {
                        logger?.Invoke($"A database with the ConnectionString: \"{db.ConnectionString}\" was defined with an empty name, thus it's been discarded. This may lead to unwanted behaviour!");
                        continue;
                    }
                    else if (!databases.ContainsKey(db.Name))
                    {
                        if (db.Type != nameof(DatabaseType.SQLite))
                        {
                            if (Enum.TryParse(db.Type, true, out DatabaseType type) && type != DatabaseType.None)
                                databases.Add(db.Name, new DatabaseConfiguration(type, db.ConnectionString ?? ""));

                            if (db.ConnectionString != null)
                            {
                                if (connStrings?.Contains(db.ConnectionString) ?? false)
                                    logger?.Invoke($"Multiple databases have the same ConnectionString: \"{db.ConnectionString}\". This may lead to unwanted behaviour!");
                                else
                                    connStrings?.Add(db.ConnectionString);
                            }
                            else
                            {
                                logger?.Invoke($"The \"{db.Name}\" database is missing a ConnectionString! Expect undefined behaviour!");
                            }
                        }
                        else
                        {
                            string connectionString = string.IsNullOrWhiteSpace(db.ConnectionString) ? "Version=3;" : db.ConnectionString;
                            string path = DataSourceRegex.Match(connectionString).Groups[2].Value;
                            path = GetSQLiteDbPath?.Invoke(path) ?? path;
                            connectionString =
                                DataSourceRegex.IsMatch(connectionString) ?
                                    DataSourceRegex.Replace(
                                        connectionString,
                                        $"$1{path}$3"
                                    ) :
                                    $"Data Source={path};{connectionString}";
                            databases.Add(db.Name, new DatabaseConfiguration(DatabaseType.SQLite, connectionString));

                            if (connStrings?.Contains(connectionString) ?? false)
                                logger?.Invoke($"Multiple databases have the same ConnectionString: \"{db.ConnectionString}\". This may lead to unwanted behaviour!");
                            else
                                connStrings?.Add(connectionString);
                        }
                    }
                    else logger?.Invoke($"Duplicate database names ({db.Name}) in the service configuration file will be discarded!");
                }
            }

            var brokers = new List<BrokerConfiguration>(configuration.Brokers.Length);
            foreach (var broker in configuration.Brokers)
            {
                var similar = brokers.FirstOrDefault(b => b.Equals(broker));
                if (similar == null)
                {
                    var newBroker = new BrokerConfiguration(databases, broker);
                    if (newBroker.Clients[0].Subscriptions.Count != 0) brokers.Add(newBroker);
                }
                else
                    similar.Merge(databases, broker);
            }

            logger?.Invoke("Final brokers configuration:");
            logger?.Invoke(string.Join(Environment.NewLine, brokers.Select(broker => broker.ToString())));

            return brokers.ToArray();
        }

        #region Regex Patterns
#if !DEBUG
        private static readonly Regex PasswordRegex = new("(\"password\"\\s*:\\s*\")(.*?)((?:\"\\s*)(?:,|$|}|\n|\r))", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
#endif
        private static readonly Regex ConnectionStringRegex = new("(\"connectionString\"\\s*:\\s*\")(.*?)((?:\"\\s*)(?:,|$|}|\n|\r))", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private static readonly Regex PythonPathRegex = new("(\"pythonPath\"\\s*:\\s*\")(.*?)((?:\"\\s*)(?:,|$|}|\n|\r))", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex PythonParserScriptPathRegex = new("(\"pythonParserScriptPath(?:s?)\"\\s*:\\s*\")(.*?)((?:\"\\s*)(?:,|$|}|\n|\r))", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex PythonParserScriptPathsArrayRegex = new("(\"pythonParserScriptPath(?:s?)\"\\s*:\\s*\\[)(?:(\\s*\".*?\"\\s*)(,|$|\n|\r|))*((?:\\s*\\]\\s*)(?:,|$|}|\n|\r))", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);
        private static readonly Regex DataSourceRegex = new("(Data Source\\s*=\\s*)(.*?)(;|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
        #endregion
    }
}
