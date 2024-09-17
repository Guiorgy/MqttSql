/*
    This file is part of MqttSql (Copyright © 2024 Guiorgy).
    MqttSql is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
    MqttSql is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Affero General Public License for more details.
    You should have received a copy of the GNU Affero General Public License along with Foobar. If not, see <https://www.gnu.org/licenses/>.
*/

using Guiorgy.JsonExtensions;
using MqttSql.Database;
using MqttSql.Utility;
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
    public DatabaseConfiguration[] Databases { get; init; } = [];

    [JsonConverter(typeof(SingleOrArrayJsonConverter))]
    [JsonPropertyNames("Brokers", "Broker")]
    public BrokerConfiguration[] Brokers { get; init; } = [];

    public override string ToString() => AppendStringBuilder(new StringBuilder()).ToString();

    public StringBuilder AppendStringBuilder(StringBuilder builder)
    {
        return builder
            .Append(nameof(Databases)).AppendLine(":").AppendLine(Databases, true)
            .Append(nameof(Brokers)).AppendLine(":").AppendLine(Brokers);
    }
}

public sealed class DatabaseConfiguration : IAppendStringBuilder
{
    public string Name { get; init; } = "sqlite";
    public string Type { get; init; } = nameof(DatabaseType.SQLite);
    public string ConnectionString { get; init; } = "";

    public override string ToString() => AppendStringBuilder(new StringBuilder()).ToString();

    public StringBuilder AppendStringBuilder(StringBuilder builder)
    {
        const char tabs = '\t';

        return builder
            .Append(tabs).Append(nameof(Name)).Append(": ").AppendLine(Name)
            .Append(tabs).Append(nameof(Type)).Append(": ").AppendLine(Type)
            .Append(tabs).Append(nameof(ConnectionString)).Append(": ").AppendLine(ConnectionString);
    }
}

public sealed class BrokerConfiguration : IAppendStringBuilder
{
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 1883;

    [JsonPropertyNames("User", "UserName")]
    public string User { get; init; } = "";

    public string Password { get; init; } = "";

    [JsonConverter(typeof(SingleOrArrayJsonConverter<SubscriptionConfiguration.SubscriptionConfigurationJsonConverter>))]
    [JsonPropertyNames("Subscriptions", "Subscription")]
    public SubscriptionConfiguration[] Subscriptions { get; } = [];

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
    public string Topic { get; init; } = "sql";
    public int QOS { get; init; } = 2;

    [JsonConverter(typeof(SingleOrArrayJsonConverter))]
    [JsonPropertyNames("Databases", "Database", "Bases", "Base", "Dbs", "Db")]
    public TableConfiguration[] Databases { get; init; } = [];

    public StringBuilder AppendStringBuilder(StringBuilder builder)
    {
        const string tabs = "\t\t";

        _ = builder
            .Append(tabs).Append(nameof(Topic)).Append(": ").AppendLine(Topic)
            .Append(tabs).Append(nameof(QOS)).Append(": ").AppendLine(QOS);

        if (Databases.Length == 1)
        {
            var database = Databases[0];

            _ = builder.Append(tabs).Append("Database").Append(": ").AppendLine(database.DatabaseName)
                .Append(tabs).Append(nameof(database.Table)).Append(": ").AppendLine(database.Table)
                .Append(tabs).Append(nameof(database.TimestampFormat)).Append(": ").AppendLine(database.TimestampFormat);
        }
        else
        {
            _ = builder.Append(tabs).Append(nameof(Databases)).AppendLine(":").AppendLine(Databases, true, true);
        }

        return builder;
    }

    internal sealed class SubscriptionConfigurationJsonConverter : JsonConverter<SubscriptionConfiguration>
    {
        private static readonly SubscriptionConfiguration DefaultSubscriptionConfiguration = new();
        private static readonly TableConfiguration DefaultTableConfiguration = new();

        public override SubscriptionConfiguration? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException();

            var propertyNames =
                typeToConvert.GetProperty(nameof(DefaultSubscriptionConfiguration.Databases))
                ?.GetCustomAttribute<JsonPropertyNamesAttribute>()
                ?.Names
                ?.Select(name => name.ToLower())
                ?? [];
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
                        _ = probeReader.Read();

                        if (probeReader.TokenType == JsonTokenType.StartArray)
                        {
                            isFlat = false;
                            break;
                        }

                        if (probeReader.TokenType == JsonTokenType.String) isFlat = true;
                    }

                    if (propertyName?.ToLowerInvariant() is "table" or "timestampformat") isFlat = true;
                }
            }

            if (isFlat)
            {
                var flat = JsonSerializer.Deserialize<FlatConfiguration>(ref reader, options);
                return flat != null && (flat.Database != null || flat.Table != null)
                    ? new SubscriptionConfiguration()
                    {
                        Topic = flat.Topic ?? DefaultSubscriptionConfiguration.Topic,
                        QOS = flat.QOS ?? DefaultSubscriptionConfiguration.QOS,
                        Databases =
                        [
                            new()
                            {
                                DatabaseName = flat.Database ?? DefaultTableConfiguration.DatabaseName,
                                Table = flat.Table ?? DefaultTableConfiguration.Table,
                                TimestampFormat = flat.TimestampFormat ?? DefaultTableConfiguration.TimestampFormat
                            }
                        ]
                    }
                    : null;
            }
            else
            {
                return JsonSerializer.Deserialize<SubscriptionConfiguration>(ref reader, options);
            }
        }

        public override void Write(Utf8JsonWriter writer, SubscriptionConfiguration values, JsonSerializerOptions options) => JsonSerializer.Serialize(writer, values, options);

        public sealed class FlatConfiguration
        {
            public string? Topic { get; init; }
            public int? QOS { get; init; }

            [JsonPropertyNames("Database", "Base", "Db")]
            public string? Database { get; init; }

            public string? Table { get; init; }
            public string? TimestampFormat { get; init; }
        }
    }
}

public sealed class TableConfiguration : IAppendStringBuilder
{
    [JsonPropertyNames("DatabaseName", "Database", "BaseName", "Base", "DbName", "Db", "Name")]
    public string DatabaseName { get; init; } = "sqlite";

    public string Table { get; init; } = "mqtt";
    public string TimestampFormat { get; init; } = "yyyy-MM-dd HH:mm:ss";

    public StringBuilder AppendStringBuilder(StringBuilder builder)
    {
        const string tabs = "\t\t\t";

        return builder
            .Append(tabs).Append(nameof(DatabaseName)).Append(": ").AppendLine(DatabaseName)
            .Append(tabs).Append(nameof(Table)).Append(": ").AppendLine(Table)
            .Append(tabs).Append(nameof(TimestampFormat)).Append(": ").AppendLine(TimestampFormat);
    }
}
