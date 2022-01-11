using MqttSql.Configurations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using static MqttSql.Configurations.BaseConfiguration;

using ServiceConfigurationJson = MqttSql.ConfigurationsJson.ServiceConfiguration;

namespace MqttSql
{
    public static class ConfigurationLoader
    {
        public static BrokerConfiguration[] LoadBrokersFromJson(
            string configPath,
            Func<string, string>? GetSQLiteDbPath = null,
            Action<string>? logger = null)
        {
            return GetBrokersFromConfig(LoadJsonConfig(configPath, logger), GetSQLiteDbPath, logger);
        }

        private static ServiceConfigurationJson LoadJsonConfig(string configPath, Action<string>? logger = null)
        {
            logger?.Invoke($"Loading configuration \"{configPath}\":");
            string json = File.ReadAllText(configPath);
            json = Regex.Replace(
                json,
                "(\"connectionString\"\\s*:\\s*\")(.*?)(\")(,|\n|\r)",
                m => m.Groups[2].Value.Contains(@"\\") ? m.Value : (m.Groups[1].Value + m.Groups[2].Value.Replace(@"\", @"\\") + '"' + m.Groups[4].Value),
                RegexOptions.IgnoreCase
            );
#if DEBUG
            logger?.Invoke(json);
#else
            logger?.Invoke(Regex.Replace(json,
                "(\"password\"\\s*:\\s*\")(.*?)(\")(,|\n|\r)",
                m => m.Groups[1].Value + new string('*', m.Groups[2].Length) + '"' + m.Groups[4].Value));
#endif
            var jsonOptions = new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                MaxDepth = 5
            };
            ServiceConfigurationJson? configuration = JsonSerializer.Deserialize<ServiceConfigurationJson>(json, jsonOptions);
            if (configuration == null) throw new JsonException($"Failed to Deserialize the service configuration file \"{configPath}\"");

            logger?.Invoke("Configuration loaded:");
            logger?.Invoke(configuration.ToString());

            return configuration;
        }

        private static BrokerConfiguration[] GetBrokersFromConfig(
            ServiceConfigurationJson configuration,
            Func<string, string>? GetSQLiteDbPath = null,
            Action<string>? logger = null)
        {
            var databases = new Dictionary<string, BaseConfiguration>(configuration.Databases.Length);
            HashSet<string>? connStrings = logger != null ? new(databases.Keys.Count) : null;
            foreach (var db in configuration.Databases)
            {
                if (!databases.ContainsKey(db.Name))
                {
                    if (db.Type != nameof(DatabaseType.SQLite))
                    {
                        if (Enum.TryParse(db.Type, true, out DatabaseType type) && type != DatabaseType.None)
                            databases.Add(db.Name, new BaseConfiguration(type, db.ConnectionString ?? ""));

                        if (db.ConnectionString != null)
                        {
                            if (connStrings?.Contains(db.ConnectionString) ?? false)
                                logger?.Invoke($"Multiple databases have the same ConnectionString: \"{db.ConnectionString}\". This may lead to undefined behaviour!");
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
                        string path =
                            Regex.Match(connectionString,
                            "(Data Source\\s*=\\s*)(.*?)(;|$)").Groups[2].Value;
                        path = GetSQLiteDbPath?.Invoke(path) ?? path;
                        connectionString =
                            Regex.IsMatch(connectionString, "(Data Source\\s*=\\s*)(.*?)(;|$)") ?
                                Regex.Replace(connectionString,
                                "(Data Source\\s*=\\s*)(.*?)(;|$)",
                                $"$1{path}$3") :
                                $"Data Source={path};{connectionString}";
                        databases.Add(db.Name, new BaseConfiguration(DatabaseType.SQLite, connectionString));

                        if (connStrings?.Contains(connectionString) ?? false)
                            logger?.Invoke($"Multiple databases have the same ConnectionString: \"{db.ConnectionString}\". This may lead to undefined behaviour!");
                        else
                            connStrings?.Add(connectionString);
                    }
                }
                else logger?.Invoke($"Duplicate database names ({db.Name}) in the service configuration file will be discarded!");
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
    }
}
