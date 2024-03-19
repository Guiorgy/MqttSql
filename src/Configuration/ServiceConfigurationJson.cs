/*
    This file is part of MqttSql (Copyright © 2024  Guiorgy).
    MqttSql is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
    MqttSql is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
    You should have received a copy of the GNU General Public License along with Foobar. If not, see <https://www.gnu.org/licenses/>.
*/

using Guiorgy.JsonExtensions;
using MqttSql.Database;
using MqttSql.src.Utility;
using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MqttSql.Configuration.Json;

public sealed class ServiceConfiguration : IAppendStringBuilder
{
    [JsonConverter(typeof(SingleOrArrayJsonConverter))]
    [JsonPropertyNames("Databases", "Database")]
    public DatabaseConfiguration[] Databases { get; }

    [JsonConverter(typeof(SingleOrArrayJsonConverter))]
    [JsonPropertyNames("Brokers", "Broker")]
    public BrokerConfiguration[] Brokers { get; }

    [JsonConstructor]
    public ServiceConfiguration(DatabaseConfiguration[]? databases = default, BrokerConfiguration[]? brokers = default) =>
        (Databases, Brokers) = (databases ?? [], brokers ?? []);

    public override string ToString()
    {
        return AppendStringBuilder(new StringBuilder()).ToString();
    }

    public StringBuilder AppendStringBuilder(StringBuilder builder)
    {
        builder
            .Append(nameof(Databases)).AppendLine(":").AppendLine(Databases, true)
            .Append(nameof(Brokers)).AppendLine(":").AppendLine(Brokers);

        return builder;
    }
}

public sealed class DatabaseConfiguration : IAppendStringBuilder
{
    private const string NameDefault = "sqlite";
    private const string TypeDefault = nameof(DatabaseType.SQLite);
    private const string ConnectionStringDefault = "";

    public string Name { get; }
    public string Type { get; }
    public string ConnectionString { get; }

    [JsonConstructor]
    public DatabaseConfiguration(
        string name = NameDefault,
        string type = TypeDefault,
        string connectionString = ConnectionStringDefault) =>
        (Name, Type, ConnectionString) = (name, type, connectionString);

    public override string ToString()
    {
        return AppendStringBuilder(new StringBuilder()).ToString();
    }

    public StringBuilder AppendStringBuilder(StringBuilder builder)
    {
        const char tabs = '\t';

        builder
            .Append(tabs).Append(nameof(Name)).Append(": ").AppendLine(Name)
            .Append(tabs).Append(nameof(Type)).Append(": ").AppendLine(Type)
            .Append(tabs).Append(nameof(ConnectionString)).Append(": ").AppendLine(ConnectionString);

        return builder;
    }
}

public sealed class BrokerConfiguration : IAppendStringBuilder
{
    private const string HostDefault = "localhost";
    private const int PortDefault = 1883;
    private const string UserDefault = "";
    private const string PasswordDefault = "";

    public string Host { get; }
    public int Port { get; }

    [JsonPropertyNames("User", "UserName")]
    public string User { get; }

    public string Password { get; }

    [JsonConverter(typeof(SingleOrArrayJsonConverter<SubscriptionConfiguration.SubscriptionConfigurationJsonConverter>))]
    [JsonPropertyNames("Subscriptions", "Subscription")]
    public SubscriptionConfiguration[] Subscriptions { get; }

    [JsonConstructor]
    public BrokerConfiguration(
        string host = HostDefault,
        int port = PortDefault,
        string? user = default,
        string password = PasswordDefault,
        SubscriptionConfiguration[]? subscriptions = default) =>
        (Host, Port, User, Password, Subscriptions) = (host, port, user ?? UserDefault, password, subscriptions ?? []);

    public StringBuilder AppendStringBuilder(StringBuilder builder)
    {
        const char tabs = '\t';

        builder
            .Append(tabs).Append(nameof(Host)).Append(": ").AppendLine(Host)
            .Append(tabs).Append(nameof(Port)).Append(": ").AppendLine(Port)
            .Append(tabs).Append(nameof(User)).Append(": ").AppendLine(User)
#if DEBUG
            .Append(tabs).Append(nameof(Password)).Append(": ").AppendLine(Password);
#else
            .Append(tabs).Append(nameof(Password)).Append(": ").AppendLine(new string('*', Password.Length));
#endif

        builder.Append(tabs).Append(nameof(Subscriptions)).AppendLine(":").AppendLine(Subscriptions, true, true);

        return builder;
    }
}

public sealed class SubscriptionConfiguration : IAppendStringBuilder
{
    private const string NameOfDatabasesPropery = nameof(Databases);

    private const string TopicDefault = "sql";
    private const int QOSDefault = 2;
    private const TableConfiguration[]? DatabasesDefault = default;

    public string Topic { get; }
    public int QOS { get; }

    [JsonConverter(typeof(SingleOrArrayJsonConverter))]
    [JsonPropertyNames("Databases", "Database", "Bases", "Base", "Dbs", "Db")]
    public TableConfiguration[] Databases { get; }

    [JsonConstructor]
    public SubscriptionConfiguration(
        string topic = TopicDefault,
        int qos = QOSDefault,
        TableConfiguration[]? databases = DatabasesDefault) =>
        (Topic, QOS, Databases) = (topic, qos, databases ?? []);

    public StringBuilder AppendStringBuilder(StringBuilder builder)
    {
        const string tabs = "\t\t";

        builder
            .Append(tabs).Append(nameof(Topic)).Append(": ").AppendLine(Topic)
            .Append(tabs).Append(nameof(QOS)).Append(": ").AppendLine(QOS);

        if (Databases.Length == 1)
        {
            var database = Databases[0];

            builder.Append(tabs).Append("Database").Append(": ").AppendLine(database.DatabaseName)
                .Append(tabs).Append(nameof(database.Table)).Append(": ").AppendLine(database.Table)
                .Append(tabs).Append(nameof(database.TimestampFormat)).Append(": ").AppendLine(database.TimestampFormat);
        }
        else
        {
            builder.Append(tabs).Append(nameof(Databases)).AppendLine(":").AppendLine(Databases, true, true);
        }

        return builder;
    }

    internal sealed class SubscriptionConfigurationJsonConverter : JsonConverter<SubscriptionConfiguration>
    {
        public override SubscriptionConfiguration? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException();

            var propertyNames =
                typeToConvert.GetProperty(NameOfDatabasesPropery)
                ?.GetCustomAttribute<JsonPropertyNamesAttribute>()
                ?.Names
                ?.Select(name => name.ToLower())
                ?? Enumerable.Empty<string>();
            var pluralPropertyNames = propertyNames.Where(name => name.EndsWith('s')).ToArray();
            var singlePropertyNames = propertyNames.Where(name => !name.EndsWith('s')).ToArray();

            bool isFlat = false;
            Utf8JsonReader probeReader = reader;
            while (probeReader.Read())
            {
                if (probeReader.TokenType == JsonTokenType.EndObject) break;

                if (probeReader.TokenType == JsonTokenType.PropertyName)
                {
                    string? propertyName = probeReader.GetString();

                    if (pluralPropertyNames.Contains(propertyName?.ToLower()))
                    {
                        isFlat = false;
                        break;
                    }

                    if (singlePropertyNames.Contains(propertyName?.ToLower()))
                    {
                        probeReader.Read();

                        if (probeReader.TokenType == JsonTokenType.StartArray)
                        {
                            isFlat = false;
                            break;
                        }

                        if (probeReader.TokenType == JsonTokenType.String) isFlat = true;
                    }

                    if (propertyName?.ToLower() == "table" || propertyName?.ToLower() == "timestampformat") isFlat = true;
                }
            }

            if (isFlat)
            {
                var flat = JsonSerializer.Deserialize<FlatConfiguration>(ref reader, options);
                if (flat != null && (flat.Database != null || flat.Table != null))
                {
                    return new SubscriptionConfiguration(
                        topic: flat.Topic ?? TopicDefault,
                        qos: flat.QOS ?? QOSDefault,
                        databases:
                        [
                            new(
                                databaseName: flat.Database ?? TableConfiguration.DatabaseNameDefault,
                                table: flat.Table ?? TableConfiguration.TableDefault,
                                timestampFormat: flat.TimestampFormat ?? TableConfiguration.TimestampFormatDefault
                            )
                        ]
                    );
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return JsonSerializer.Deserialize<SubscriptionConfiguration>(ref reader, options);
            }
        }

        public override void Write(Utf8JsonWriter writer, SubscriptionConfiguration values, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, values, options);
        }

        public sealed class FlatConfiguration
        {
            public string? Topic { get; }
            public int? QOS { get; }

            [JsonPropertyNames("Database", "Base", "Db")]
            public string? Database { get; }

            public string? Table { get; }
            public string? TimestampFormat { get; }

            [JsonConstructor]
            public FlatConfiguration(
                string? topic = null,
                int? qos = null,
                string? database = null,
                string? table = null,
                string? timestampFormat = null) =>
                (Topic, QOS, Database, Table, TimestampFormat) = (topic, qos, database, table, timestampFormat);
        }
    }
}

public sealed class TableConfiguration : IAppendStringBuilder
{
    internal const string DatabaseNameDefault = "sqlite";
    internal const string TableDefault = "mqtt";
    internal const string TimestampFormatDefault = "yyyy-MM-dd HH:mm:ss";

    [JsonPropertyNames("DatabaseName", "Database", "BaseName", "Base", "DbName", "Db", "Name")]
    public string DatabaseName { get; }

    public string Table { get; }
    public string TimestampFormat { get; }

    [JsonConstructor]
    public TableConfiguration(
        string? databaseName = default,
        string table = TableDefault,
        string timestampFormat = TimestampFormatDefault) =>
        (DatabaseName, Table, TimestampFormat) = (databaseName ?? DatabaseNameDefault, table, timestampFormat);

    public StringBuilder AppendStringBuilder(StringBuilder builder)
    {
        const string tabs = "\t\t\t";

        builder
            .Append(tabs).Append(nameof(DatabaseName)).Append(": ").AppendLine(DatabaseName)
            .Append(tabs).Append(nameof(Table)).Append(": ").AppendLine(Table)
            .Append(tabs).Append(nameof(TimestampFormat)).Append(": ").AppendLine(TimestampFormat);

        return builder;
    }
}
