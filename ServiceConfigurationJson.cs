using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;

namespace MqttSql.ConfigurationsJson
{
    public sealed class ServiceConfiguration
    {
        public BaseConfiguration[] Databases { get; }
        public BrokerConfiguration[] Brokers { get; }

        [JsonConstructor]
        public ServiceConfiguration(
            BaseConfiguration[] databases = default,
            BrokerConfiguration[] brokers = default) =>
            (Databases, Brokers) = (databases ?? Array.Empty<BaseConfiguration>(), brokers ?? Array.Empty<BrokerConfiguration>());

        public override string ToString()
        {
            /*return
                $"Databases:{Environment.NewLine}" +
                string.Join(Environment.NewLine,
                    Databases.Select(database => database.ToString().AppendBeforeLines("\t"))) +
                $"{Environment.NewLine}Brokers:{Environment.NewLine}" +
                string.Join(Environment.NewLine,
                    Brokers.Select(broker => broker.ToString().AppendBeforeLines("\t")));*/
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

    public sealed class BaseConfiguration : IEquatable<BaseConfiguration>
    {
        public string Name { get; }
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public DatabaseType Type { get; }
        public string ConnectionString { get; }

        [JsonConstructor]
        public BaseConfiguration(
            string name = "sqlite",
            DatabaseType type = DatabaseType.SQLite,
            string connectionString = default) =>
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
            return new StringBuilder(10)
                .Append("\tName: ").AppendLine(Name)
                .Append("\tType: ").AppendLine(Type.ToString())
                .Append("\tConnectionString: ").AppendLine(ConnectionString);
        }

        public bool Equals([AllowNull] BaseConfiguration other)
        {
            return
                other != null
                && other.Type == this.Type
                && other.ConnectionString.Equals(this.ConnectionString);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as BaseConfiguration);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Type, ConnectionString);
        }

        public enum DatabaseType
        {
            SQLite = 0,
            GeneralSql = 1
        }
    }

    public sealed class BrokerConfiguration : IEquatable<BrokerConfiguration>
    {
        public string Host { get; }
        public int Port { get; }
        public string User { get; }
        public string Password { get; }
        public SubscriptionConfiguration[] Subscriptions { get; }

        [JsonConstructor]
        public BrokerConfiguration(
            string host = "localhost",
            int port = 1883,
            string user = default,
            string password = default,
            SubscriptionConfiguration[] subscriptions = default) =>
            (Host, Port, User, Password, Subscriptions) = (host, port, user, password, subscriptions ?? Array.Empty<SubscriptionConfiguration>());

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

        public bool Equals([AllowNull] BrokerConfiguration other)
        {
            return
                other?.Host.Equals(this.Host) == true
                && other.Port.Equals(this.Port)
                && other.User.Equals(this.User)
                && other.Password.Equals(this.Password);
        }

        public override bool Equals(object obj)
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
        public string Topic { get; }
        public int QOS { get; }
        [JsonPropertyName("base")]
        public string Database { get; }
        public string Table { get; }

        [JsonConstructor]
        public SubscriptionConfiguration(
            string topic = "sql",
            int qos = 2,
            string database = "sqlite",
            string table = "mqtt") =>
            (Topic, QOS, Database, Table) = (topic, qos, database, table);

        public override string ToString()
        {
            return
                $"Topic: {Topic}{Environment.NewLine}" +
                $"QOS: {QOS}{Environment.NewLine}" +
                $"Database: {Database}{Environment.NewLine}" +
                $"Table: {Table}{Environment.NewLine}";
        }

        internal StringBuilder ToStringBuilder()
        {
            return new StringBuilder(10)
                .Append("\t\tTopic: ").AppendLine(Topic)
                .Append("\t\tQOS: ").AppendLine(QOS.ToString())
                .Append("\t\tDatabase: ").AppendLine(Database)
                .Append("\t\tTable: ").AppendLine(Table);
        }
    }
}
