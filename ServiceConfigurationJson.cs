using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using static MqttSql.Configurations.DatabaseConfiguration;

namespace MqttSql.ConfigurationsJson
{
    public sealed class ServiceConfiguration
    {
        [JsonConverter(typeof(SingleOrArrayJsonConverter))]
        public DatabaseConfiguration[] Databases { get; }
        [JsonConverter(typeof(SingleOrArrayJsonConverter))]
        public BrokerConfiguration[] Brokers { get; }

        [JsonConstructor]
        public ServiceConfiguration(
            DatabaseConfiguration[]? databases = default,
            BrokerConfiguration[]? brokers = default) =>
            (Databases, Brokers) = (databases ?? Array.Empty<DatabaseConfiguration>(), brokers ?? Array.Empty<BrokerConfiguration>());

        public override string ToString()
        {
            var builder = new StringBuilder()
                .AppendLine("Databases:");
            foreach (var database in Databases)
                builder.Append(database.ToStringBuilder()).AppendLine();
            builder.AppendLine("Brokers:");
            foreach (var broker in Brokers)
                builder.Append(broker.ToStringBuilder());
            return builder.ToString();
        }
    }

    public sealed class DatabaseConfiguration : IEquatable<DatabaseConfiguration>
    {
        public string Name { get; }
        public string Type { get; }
        public string ConnectionString { get; }

        [JsonConstructor]
        public DatabaseConfiguration(
            string name = "sqlite",
            string type = nameof(DatabaseType.SQLite),
            string connectionString = "") =>
            (Name, Type, ConnectionString) = (name, type, connectionString);

        public override string ToString()
        {
            return
                $"Name: {Name}{Environment.NewLine}" +
                $"Type: {Type}{Environment.NewLine}" +
                $"ConnectionString: {ConnectionString}{Environment.NewLine}";
        }

        internal StringBuilder ToStringBuilder()
        {
            return new StringBuilder(6)
                .Append("\tName: ").AppendLine(Name)
                .Append("\tType: ").AppendLine(Type)
                .Append("\tConnectionString: ").AppendLine(ConnectionString);
        }

        public bool Equals([AllowNull] DatabaseConfiguration other)
        {
            return
                other != null
                && other.Type == this.Type
                && other.ConnectionString.Equals(this.ConnectionString);
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as DatabaseConfiguration);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Type, ConnectionString);
        }
    }

    public sealed class BrokerConfiguration : IEquatable<BrokerConfiguration>
    {
        private const string DeprecationMessage = "This is defined as a workaround to map 2 different names onto the same property when deserializing, while only serializing one. Use the `Subscriptions` property instead.";

        public string Host { get; }
        public int Port { get; }
        public string User { get; }
        public string Password { get; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [Obsolete(DeprecationMessage, true)]
        [JsonConverter(typeof(SingleOrArrayJsonConverter<SubscriptionConfiguration, SubscriptionConfiguration.SubscriptionConfigurationJsonConverter>))]
        public SubscriptionConfiguration[]? Subscription { get => default; }
        [JsonConverter(typeof(SingleOrArrayJsonConverter<SubscriptionConfiguration, SubscriptionConfiguration.SubscriptionConfigurationJsonConverter>))]
        public SubscriptionConfiguration[] Subscriptions { get; }

        [JsonConstructor]
        public BrokerConfiguration(
            string host = "localhost",
            int port = 1883,
            string user = "",
            string password = "",
            SubscriptionConfiguration[]? subscription = default,
            SubscriptionConfiguration[]? subscriptions = default) =>
            (Host, Port, User, Password, Subscriptions) = (host, port, user, password, subscriptions ?? subscription ?? Array.Empty<SubscriptionConfiguration>());

        public override string ToString()
        {
            return
                $"Host: {Host}{Environment.NewLine}" +
                $"Port: {Port}{Environment.NewLine}" +
                $"User: {User}{Environment.NewLine}" +
#if DEBUG
                $"Password: {Password}{Environment.NewLine}" +
#else
                $"Password: {new string('*', Password.Length)}{Environment.NewLine}" +
#endif
                $"Subscriptions:{Environment.NewLine}" +
                string.Join(Environment.NewLine,
                    Subscriptions.Select(sub => sub.ToString().AppendBeforeLines("\t")));
        }

        internal StringBuilder ToStringBuilder()
        {
            var builder = new StringBuilder(20 + 3 * Subscriptions.Length)
                .Append("\tHost: ").AppendLine(Host)
                .Append("\tPort: ").AppendLine(Port.ToString())
                .Append("\tUser: ").AppendLine(User)
#if DEBUG
                .Append("\tPassword: ").AppendLine(Password)
#else
                .Append("\tPassword: ").AppendLine(new string('*', Password.Length))
#endif
                .AppendLine("\tSubscriptions:");
            if (Subscriptions.Length == 0)
                builder.AppendLine();
            else foreach (var subscription in Subscriptions)
                    builder.Append(subscription.ToStringBuilder()).AppendLine();
            return builder;
        }

        public bool Equals([AllowNull] BrokerConfiguration? other)
        {
            return
                other?.Host.Equals(this.Host) == true
                && other.Port.Equals(this.Port)
                && other.User.Equals(this.User)
                && other.Password.Equals(this.Password);
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as BrokerConfiguration);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Host, Port, User, Password);
        }
    }

    public sealed class SubscriptionConfiguration
    {
        private const string DeprecationMessage = "This is defined as a workaround to map 2 different names onto the same property when deserializing, while only serializing one. Use the `Databases` property instead.";

        private const string TopicDefault = "sql";
        private const int QOSDefault = 2;
        private const TableConfiguration[]? DbDefault = default;
        private const TableConfiguration[]? DatabaseDefault = default;
        private const TableConfiguration[]? DbsDefault = default;
        private const TableConfiguration[] DatabasesDefault = default;

        public string Topic { get; }
        public int QOS { get; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [Obsolete(DeprecationMessage, true)]
        [JsonConverter(typeof(SingleOrArrayJsonConverter))]
        [JsonPropertyName("base")]
        public TableConfiguration[]? Db { get => default; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [Obsolete(DeprecationMessage, true)]
        [JsonConverter(typeof(SingleOrArrayJsonConverter))]
        public TableConfiguration[]? Database { get => default; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [Obsolete(DeprecationMessage, true)]
        [JsonConverter(typeof(SingleOrArrayJsonConverter))]
        [JsonPropertyName("bases")]
        public TableConfiguration[]? Dbs { get => default; }
        [JsonConverter(typeof(SingleOrArrayJsonConverter))]
        public TableConfiguration[] Databases { get; }

        [JsonConstructor]
        public SubscriptionConfiguration(
            string topic = TopicDefault,
            int qos = QOSDefault,
            TableConfiguration[]? db = DbDefault,
            TableConfiguration[]? database = DatabaseDefault,
            TableConfiguration[]? dbs = DbsDefault,
            TableConfiguration[]? databases = DatabasesDefault) =>
            (Topic, QOS, Databases) = (topic, qos, databases ?? dbs ?? database ?? db ?? Array.Empty<TableConfiguration>());

        public override string ToString()
        {
            return
                $"Topic: {Topic}{Environment.NewLine}" +
                $"QOS: {QOS}{Environment.NewLine}" +
                (
                    Databases.Length == 1 ? (
                        $"Database: {Databases[0].DatabaseName}{Environment.NewLine}" +
                        $"Table: {Databases[0].Table}{Environment.NewLine}" +
                        $"TimestampFormat: {Databases[0].TimestampFormat}{Environment.NewLine}"
                    ) : (
                        $"Databases:{Environment.NewLine}" +
                            string.Join(Environment.NewLine,
                                Databases.Select(db => db.ToString().AppendBeforeLines("\t")))
                    )
                );
        }

        internal StringBuilder ToStringBuilder()
        {
            var builder = new StringBuilder(10)
                .Append("\t\tTopic: ").AppendLine(Topic)
                .Append("\t\tQOS: ").AppendLine(QOS.ToString());
            if (Databases.Length == 1)
            {
                builder.Append("\t\tDatabase: ").AppendLine(Databases[0].DatabaseName)
                    .Append("\t\tTable: ").AppendLine(Databases[0].Table)
                    .Append("\t\tTimestampFormat: ").AppendLine(Databases[0].TimestampFormat);
            }
            else
            {
                builder.AppendLine("\t\tDatabases: ");
                if (Databases.Length == 0)
                    builder.AppendLine();
                else foreach (var db in Databases)
                        builder.Append(db.ToStringBuilder()).AppendLine();
            }
            return builder;
        }

        public sealed class SubscriptionConfigurationJsonConverter : JsonConverter<SubscriptionConfiguration>
        {
            public override SubscriptionConfiguration? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException();

                bool isFlat = false;
                Utf8JsonReader probeReader = reader;
                while (probeReader.Read())
                {
                    if (probeReader.TokenType == JsonTokenType.EndObject) break;

                    if (probeReader.TokenType == JsonTokenType.PropertyName)
                    {
                        string? propertyName = probeReader.GetString();

                        if (propertyName?.ToLower() == "bases" || propertyName?.ToLower() == "databases")
                        {
                            isFlat = false;
                            break;
                        }

                        if (propertyName?.ToLower() == "base" || propertyName?.ToLower() == "database")
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
                            databases: new TableConfiguration[]
                            {
                                new TableConfiguration(
                                    databaseName: flat.Database ?? TableConfiguration.DatabaseNameDefault,
                                    table: flat.Table ?? TableConfiguration.TableDefault,
                                    timestampFormat: flat.TimestampFormat ?? TableConfiguration.TimestampFormatDefault
                                )
                            }
                        );
                    }
                    else return null;
                }
                else return JsonSerializer.Deserialize<SubscriptionConfiguration>(ref reader, options);
            }

            public override void Write(Utf8JsonWriter writer, SubscriptionConfiguration values, JsonSerializerOptions options)
            {
                JsonSerializer.Serialize(writer, values, options);
            }

            public sealed class FlatConfiguration
            {
                public string? Topic { get; }
                public int? QOS { get; }
                [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
                [Obsolete(DeprecationMessage, true)]
                [JsonPropertyName("base")]
                public string? Db { get => default; }
                public string? Database { get; }
                public string? Table { get; }
                public string? TimestampFormat { get; }

                [JsonConstructor]
                public FlatConfiguration(
                    string? topic = null,
                    int? qos = null,
                    string? db = null,
                    string? database = null,
                    string? table = null,
                    string? timestampFormat = null) =>
                    (Topic, QOS, Database, Table, TimestampFormat) = (topic, qos, database ?? db, table, timestampFormat);
            }
        }
    }

    public sealed class TableConfiguration
    {
        internal const string DatabaseNameDefault = "sqlite";
        internal const string TableDefault = "mqtt";
        internal const string TimestampFormatDefault = "yyyy-MM-dd-HH:mm:ss";

        [JsonPropertyName("name")]
        public string DatabaseName { get; }
        public string Table { get; }
        public string TimestampFormat { get; }

        [JsonConstructor]
        public TableConfiguration(
            string databaseName = DatabaseNameDefault,
            string table = TableDefault,
            string timestampFormat = TimestampFormatDefault) =>
            (DatabaseName, Table, TimestampFormat) = (databaseName, table, timestampFormat);

        public override string ToString()
        {
            return
                $"DatabaseName: {DatabaseName}{Environment.NewLine}" +
                $"Table: {Table}{Environment.NewLine}" +
                $"TimestampFormat: {TimestampFormat}{Environment.NewLine}";
        }

        internal StringBuilder ToStringBuilder()
        {
            return new StringBuilder(6)
                .Append("\t\t\tDatabaseName: ").AppendLine(DatabaseName)
                .Append("\t\t\tTable: ").AppendLine(Table)
                .Append("\t\t\tTimestampFormat: ").AppendLine(TimestampFormat);
        }
    }
}
