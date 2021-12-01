using System;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;

namespace MqttSql.Configurations
{
    public class ServiceConfiguration
    {
        public BaseConfiguration[] Databases { get; set; } = new BaseConfiguration[0];
        public BrokerConfiguration[] Brokers { get; set; } = new BrokerConfiguration[0];

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

    public class BaseConfiguration
    {
        public string Name { get; set; } = "sqlite";
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public DatabaseType Type { get; set; } = DatabaseType.SqlLite;
        public string ConnectionString { get; set; } = string.Empty;

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

        public enum DatabaseType
        {
            SqlLite = 0,
            GeneralSql = 1
        }
    }

    public class BrokerConfiguration
    {
        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 1883;
        public string User { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public SubscriptionConfiguration[] Subscriptions { get; set; } = new SubscriptionConfiguration[0];

        public override string ToString()
        {
            return
                $"Host: {Host}{Environment.NewLine}" +
                $"Port: {Port}{Environment.NewLine}" +
                $"User: {User}{Environment.NewLine}" +
#if DEBUG
                    $"Password:{Password}{Environment.NewLine}" +
#else
                    $"Password:{new string('*', Password.Length)}{Environment.NewLine}" +
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
    }

    public class SubscriptionConfiguration
    {
        public string Topic { get; set; } = "sql";
        public int QOS { get; set; } = 0;
        public string Base { get; set; } = "sqlite";
        public string Table { get; set; } = "mqtt";

        public override string ToString()
        {
            return
                $"Topic: {Topic}{Environment.NewLine}" +
                $"QOS: {QOS}{Environment.NewLine}" +
                $"Table: {Table}{Environment.NewLine}";
        }

        internal StringBuilder ToStringBuilder()
        {
            return new StringBuilder(10)
                .Append("\t\tTopic: ").AppendLine(Topic)
                .Append("\t\tQOS: ").AppendLine(QOS.ToString())
                .Append("\t\tTable: ").AppendLine(Table);
        }
    }
}
