/*
    This file is part of MqttSql (Copyright © 2024 Guiorgy).
    MqttSql is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
    MqttSql is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Affero General Public License for more details.
    You should have received a copy of the GNU Affero General Public License along with MqttSql. If not, see <https://www.gnu.org/licenses/>.
*/

using MqttSql.Database;
using MqttSql.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using static MqttSql.Configuration.SubscriptionConfiguration;
using ServiceConfigurationJson = MqttSql.Configuration.Json.ServiceConfiguration;

namespace MqttSql.Configuration;

public static partial class ServiceConfigurationMapper
{
    private const string SqliteDatabaseFileNameDefault = "database.sqlite";

    private static string GetAbsolutePath(string baseDirectory, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("Invalid path", nameof(relativePath));

        if (Path.IsPathFullyQualified(relativePath))
        {
            return relativePath;
        }
        else if (Path.IsPathFullyQualified(baseDirectory))
        {
            return Path.GetFullPath(relativePath, baseDirectory);
        }
        else
        {
            return Path.Combine(baseDirectory, relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar));
        }
    }

    public static BrokerConfiguration[] ToServiceConfiguration(
        this ServiceConfigurationJson configuration,
        Logger logger,
        string baseDirectory)
    {
        var databaseConfigurations = new Dictionary<string, DatabaseConfiguration>(configuration.Databases.Length);
        var sameConnectionNameMapping = new Dictionary<string, string>(configuration.Databases.Length);

        if (configuration.Databases.Length == 0)
        {
            databaseConfigurations.Add("sqlite", new DatabaseConfiguration(DatabaseType.SQLite, $"Data Source={GetAbsolutePath(baseDirectory, SqliteDatabaseFileNameDefault)};Version=3;"));
        }
        else
        {
            var connectionStringToNameMapping = new Dictionary<string, string>(configuration.Databases.Length);

            foreach (var db in configuration.Databases)
            {
                if (string.IsNullOrEmpty(db.Name))
                {
                    logger.Warning("A database with the ConnectionString: \"", db.ConnectionString, "\" was defined with an empty name, thus it's been discarded. This may lead to unwanted behaviour!");
                }
                else if (!databaseConfigurations.ContainsKey(db.Name))
                {
                    string? connectionString = null;

                    if (db.Type is (nameof(DatabaseType.GenericSql)) or (nameof(DatabaseType.PostgreSql)))
                    {
                        connectionString = db.ConnectionString;
                    }
                    else if (db.Type == nameof(DatabaseType.SQLite))
                    {
                        connectionString = string.IsNullOrWhiteSpace(db.ConnectionString) ? "Version=3;" : db.ConnectionString;

                        string path = DataSourceRegex().Match(connectionString).Groups[2].Value;
                        path = GetAbsolutePath(baseDirectory, !string.IsNullOrEmpty(path) ? path : SqliteDatabaseFileNameDefault);

                        connectionString =
                            DataSourceRegex().IsMatch(connectionString) ?
                                DataSourceRegex().Replace(
                                    connectionString,
                                    $"$1{path}$3"
                                ) :
                                $"Data Source={path};{connectionString}";
                    }
                    else
                    {
                        logger.Warning("The \"", db.Name, "\" database type is invalid!");
                        continue;
                    }

                    if (connectionString == null)
                    {
                        logger.Warning("The \"", db.Name, "\" database is missing a ConnectionString!");
                        continue;
                    }

                    var name = db.Name;
                    var type = Enum.Parse<DatabaseType>(db.Type, true);

                    if (connectionStringToNameMapping.TryGetValue(connectionString, out string? value))
                    {
                        name = value;
                        sameConnectionNameMapping.Add(db.Name, name);
                        logger.Warning("Databases \"", db.Name, "\" and \"", name, "\" have the same ConnectionString: \"", connectionString, "\". This may lead to unexpected behaviour!");
                    }
                    else
                    {
                        connectionStringToNameMapping.Add(connectionString, db.Name);
                        databaseConfigurations.Add(name, new DatabaseConfiguration(type, connectionString));
                    }
                }
                else
                {
                    logger.Warning("Duplicate database names (\"", db.Name, "\") in the service configuration file will be discarded!");
                }
            }
        }

        var brokers = new List<BrokerConfiguration>(configuration.Brokers.Length);

        foreach (var jsonBrokersWithSameEndpoint in configuration.Brokers.GroupBy(jsonBroker => (jsonBroker.Host, jsonBroker.Port)))
        {
            var host = jsonBrokersWithSameEndpoint.Key.Host;
            var port = jsonBrokersWithSameEndpoint.Key.Port;
            var clients = new List<ClientConfiguration>();

            foreach (var jsonBrokersWithSameCredentials in jsonBrokersWithSameEndpoint.GroupBy(jsonBroker => (jsonBroker.User, jsonBroker.Password)))
            {
                var user = jsonBrokersWithSameCredentials.Key.User;
                var password = jsonBrokersWithSameCredentials.Key.Password;
                var subscriptions = new List<SubscriptionConfiguration>();

                var jsonSubscriptions = jsonBrokersWithSameCredentials.SelectMany(jsonBroker => jsonBroker.Subscriptions);

                foreach (var jsonSubscriptionsWithSameTopic in jsonSubscriptions.GroupBy(jsonSubscription => jsonSubscription.Topic))
                {
                    var topic = jsonSubscriptionsWithSameTopic.Key;
                    var qos = jsonSubscriptionsWithSameTopic.Max(jsonSubscription => jsonSubscription.QOS);
                    var databases = new List<DatabaseConfiguration>();

                    var jsonDatabases = jsonSubscriptionsWithSameTopic.SelectMany(jsonSubscription => jsonSubscription.Databases);

                    foreach (var jsonDatabasesWithSameName in jsonDatabases.GroupBy(jsonDatabase => sameConnectionNameMapping.GetValueOrDefault(jsonDatabase.DatabaseName, jsonDatabase.DatabaseName)))
                    {
                        var jsonDatabaseName = jsonDatabasesWithSameName.Key;
                        var databaseType = DatabaseType.None;
                        string? connectionString = null;
                        var tables = new List<TableConfiguration>();

                        foreach (var jsonDatabasesWithSameTable in jsonDatabasesWithSameName.GroupBy(jsonDatabase => jsonDatabase.Table))
                        {
                            var tableName = jsonDatabasesWithSameTable.Key;
                            string? timestampFormat = null;

                            foreach (var jsonDatabasesWithSameTimestampFormat in jsonDatabasesWithSameTable.GroupBy(jsonDatabase => jsonDatabase.TimestampFormat))
                            {
                                var first = jsonDatabasesWithSameTimestampFormat.First();

                                if (databaseType == DatabaseType.None || connectionString == null)
                                {
                                    var database = databaseConfigurations.GetValueOrNull(first.DatabaseName);

                                    databaseType = database?.Type ?? DatabaseType.None;
                                    connectionString = database?.ConnectionString;
                                }

                                if (first.TimestampFormat.IsValidDateTimeFormat())
                                {
                                    if (timestampFormat != null)
                                    {
                                        logger.Warning("More than one valid TimestampFormat given for the \"", tableName, "\" table in \"", jsonDatabaseName, "\" database");
                                        break;
                                    }

                                    timestampFormat = first.TimestampFormat;
                                }
                            }

                            if (timestampFormat == null)
                            {
                                logger.Warning("No valid TimestampFormat given for the \"", tableName, "\" table in \"", jsonDatabaseName, "\" database");
                                continue;
                            }

                            tables.Add(new TableConfiguration(tableName, timestampFormat));
                        }

                        if (databaseType == DatabaseType.None)
                        {
                            logger.Warning("Database type for the \"", jsonDatabaseName, "\" database is invalid");
                            continue;
                        }

                        if (connectionString == null)
                        {
                            logger.Warning("No valid ConnectionString given for the \"", jsonDatabaseName, "\" database");
                            continue;
                        }

                        if (tables.Count == 0)
                        {
                            logger.Warning("No tables given for the \"", jsonDatabaseName, "\" database");
                            continue;
                        }

                        databases.Add(new DatabaseConfiguration(databaseType, connectionString, [..tables]));
                    }

                    if (databases.Count == 0)
                    {
                        logger.Warning("No valid databases given for the \"", topic, "\" topic subscription");
                        continue;
                    }

                    subscriptions.Add(new SubscriptionConfiguration(topic, (MqttQualityOfService)qos, [..databases]));
                }

                if (subscriptions.Count == 0)
                {
                    string passwordToLog;
#if DEBUG
                    passwordToLog = password;
#else
                    passwordToLog = new string('*', password.Length);
#endif
                    logger.Warning("No valid subscriptions given for the \"", user, '|', passwordToLog, "\" client");
                    continue;
                }

                clients.Add(new ClientConfiguration(user, password, [..subscriptions]));
            }

            if (clients.Count == 0)
            {
                logger.Warning("No valid clients given for the \"", host, ':', port, "\" broker");
                continue;
            }

            brokers.Add(new BrokerConfiguration(host, port, [..clients]));
        }

        return [..brokers];
    }

    #region Regex Patterns

    [GeneratedRegex("(\\s*Data Source\\s*=\\s*)(.*?)(;|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline, "en-US")]
    private static partial Regex DataSourceRegex();

    #endregion Regex Patterns
}
