using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using static MqttSql.ConfigurationsJson.BaseConfiguration;
using BaseConfigurationJson = MqttSql.ConfigurationsJson.BaseConfiguration;
using BrokerConfigurationJson = MqttSql.ConfigurationsJson.BrokerConfiguration;
using SubscriptionConfigurationJson = MqttSql.ConfigurationsJson.SubscriptionConfiguration;

namespace MqttSql.Configurations
{
    public sealed class BrokerConfiguration : IEquatable<BrokerConfiguration>, IEquatable<BrokerConfigurationJson>
    {
        public string Host { get; }
        public int Port { get; }
        public List<ClientConfiguration> Clients { get; }

        private List<SubscriptionConfiguration> MakeSubscriptions(
            IEnumerable<IGrouping<string, SubscriptionConfigurationJson>> topicGroups,
            Dictionary<string, BaseConfigurationJson> databases)
        {
            var subscriptions = new List<SubscriptionConfiguration>(topicGroups.Count());
            foreach (var topicGroup in topicGroups)
            {
                var bases =
                    topicGroup
                    .Select(sub => (dbcfg: databases.GetValueOrNull(sub.Database), table: sub.Table))
                    .Where(tuple => tuple.dbcfg != null);
                if (!bases.Any()) continue;
                int maxQOS = topicGroup.Max(sub => sub.QOS);
                var sub = new SubscriptionConfiguration(topicGroup.Key, maxQOS, bases);
                if (sub.Databases.Count != 0 && !string.IsNullOrWhiteSpace(sub.Topic)) subscriptions.Add(sub);
            }
            return subscriptions;
        }

        public BrokerConfiguration(Dictionary<string, BaseConfigurationJson> databases, BrokerConfigurationJson jsonConfig)
        {
            Host = jsonConfig.Host;
            Port = jsonConfig.Port;
            Clients = new List<ClientConfiguration>()
            {
                new ClientConfiguration(jsonConfig.User, jsonConfig.Password, MakeSubscriptions(jsonConfig.Subscriptions.GroupBy(sub => sub.Topic), databases))
            };
        }

        public void Merge(Dictionary<string, BaseConfigurationJson> databases, BrokerConfigurationJson jsonConfig)
        {
            var topicGroups = jsonConfig.Subscriptions.GroupBy(sub => sub.Topic);
            var newSubscriptions = MakeSubscriptions(topicGroups, databases);
            var client = Clients.FirstOrDefault(cl => cl.User == jsonConfig.User && cl.Password == jsonConfig.Password);
            if (client == null)
                Clients.Add(new ClientConfiguration(jsonConfig.User, jsonConfig.Password, newSubscriptions));
            else
                client.Merge(newSubscriptions);
        }

        public override string ToString()
        {
            return
                $"Host: {Host}{Environment.NewLine}" +
                $"Port: {Port}{Environment.NewLine}" +
                $"Clients:{Environment.NewLine}" +
                string.Join(Environment.NewLine,
                    Clients.Select(cl => cl.ToString().AppendBeforeLines("\t")));
        }

        public bool Equals([AllowNull] BrokerConfiguration other)
        {
            return
                other != null
                && other.Host.Equals(this.Host)
                && other.Port.Equals(this.Port);
        }

        public bool Equals([AllowNull] BrokerConfigurationJson other)
        {
            return
                other != null
                && other.Host.Equals(this.Host)
                && other.Port.Equals(this.Port);
        }
    }

    public sealed class ClientConfiguration
    {
        public string User { get; }
        public string Password { get; }
        public List<SubscriptionConfiguration> Subscriptions { get; }

        public ClientConfiguration(string user, string password, List<SubscriptionConfiguration> subscriptions)
        {
            User = user;
            Password = password;
            Subscriptions = subscriptions;
        }

        public void Merge(List<SubscriptionConfiguration> newSubscriptions)
        {
            foreach (var newSub in newSubscriptions)
            {
                var sub = Subscriptions.FirstOrDefault(s => s.Topic.Equals(newSub.Topic));
                if (sub == null)
                    Subscriptions.Add(newSub);
                else
                    sub.Merge(newSub);
            }
        }

        public override string ToString()
        {
            return
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
    }

    public sealed class SubscriptionConfiguration
    {
        public string Topic { get; }
        public int QOS { get; private set; }
        public List<BaseConfiguration> Databases { get; }

        public SubscriptionConfiguration(string topic, int qos, IEnumerable<(BaseConfigurationJson dbcfg, string table)> bases)
        {
            Topic = topic;
            QOS = qos;
            var baseGroups = bases.GroupBy(tuple => tuple.dbcfg);
            Databases = new List<BaseConfiguration>(baseGroups.Count());
            foreach (var baseGroup in baseGroups)
            {
                var db = new BaseConfiguration(baseGroup.Key, baseGroup.Select(tuple => tuple.table).ToArray());
                if (string.IsNullOrWhiteSpace(db.ConnectionString) || db.Tables.Count == 0) continue;
                Databases.Add(db);
            }
        }

        public void Merge(SubscriptionConfiguration other)
        {
            QOS = Math.Max(QOS, other.QOS);
            foreach (var newDb in other.Databases)
            {
                if (string.IsNullOrWhiteSpace(newDb.ConnectionString) || newDb.Tables.Count == 0) continue;
                var db = Databases.FirstOrDefault(db => db.Type == newDb.Type && db.ConnectionString.Equals(newDb.ConnectionString));
                if (db == null)
                    Databases.Add(newDb);
                else
                    db.Merge(newDb);
            }
        }

        public override string ToString()
        {
            return
                $"Topic: {Topic}{Environment.NewLine}" +
                $"QOS: {QOS}{Environment.NewLine}" +
                $"Databases:{Environment.NewLine}" +
                string.Join(Environment.NewLine,
                    Databases.Select(db => db.ToString().AppendBeforeLines("\t")));
        }

        public sealed class BaseConfiguration : IEquatable<BaseConfiguration>
        {
            public DatabaseType Type { get; }
            public string ConnectionString { get; }
            public List<string> Tables { get; }

            public BaseConfiguration(BaseConfigurationJson jsonConfig, params string[] tables)
            {
                Type = jsonConfig.Type;
                ConnectionString = jsonConfig.ConnectionString;
                Tables = new List<string>(tables.Where(table => !string.IsNullOrWhiteSpace(table)));
            }

            public void Merge(BaseConfiguration other)
            {
                Tables.AddRange(other.Tables.Where(table => !string.IsNullOrWhiteSpace(table) && !Tables.Contains(table)));
            }

            public override string ToString()
            {
                return
                    $"Type: {Type}{Environment.NewLine}" +
                    $"ConnectionString: {ConnectionString}{Environment.NewLine}" +
                    (
                        Tables.Count == 1
                        ? $"Table: {Tables[0]}{Environment.NewLine}"
                        : $"Tables: [{string.Join(", ", Tables)}]{Environment.NewLine}"
                    );
            }

            public bool Equals([AllowNull] BaseConfiguration other)
            {
                return
                    other != null
                    && other.ConnectionString.Equals(this.ConnectionString);
            }
        }
    }
}
