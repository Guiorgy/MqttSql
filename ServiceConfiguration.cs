using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using BrokerConfigurationJson = MqttSql.ConfigurationsJson.BrokerConfiguration;
using SubscriptionConfigurationJson = MqttSql.ConfigurationsJson.SubscriptionConfiguration;

namespace MqttSql.Configurations
{
    public sealed class BrokerConfiguration : IEquatable<BrokerConfiguration>, IEquatable<BrokerConfigurationJson>
    {
        public string Host { get; }
        public int Port { get; }
        public List<ClientConfiguration> Clients { get; }

        private static List<SubscriptionConfiguration> MakeSubscriptions(
            IEnumerable<IGrouping<string, SubscriptionConfigurationJson>> topicGroups,
            Dictionary<string, BaseConfiguration> databases)
        {
            var subscriptions = new List<SubscriptionConfiguration>(topicGroups.Count());
            foreach (var topicGroup in topicGroups)
            {
                if (string.IsNullOrWhiteSpace(topicGroup.Key)) continue;
                var allbases =
                    topicGroup
                    .Select(sub => databases.GetValueOrNull(sub.Database)?.Clone()?.WithTables(sub.Table))
                    .Where(db => db != null && db.Tables.Count != 0);
                List<BaseConfiguration> bases = new(allbases.Count());
                foreach (var adb in allbases)
                {
                    var db = bases.Find(db => db.ConnectionString.Equals(adb.ConnectionString));
                    if (db == null)
                        bases.Add(adb);
                    else
                        db.Merge(adb.Tables);
                }
                if (!bases.Any()) continue;
                int maxQOS = topicGroup.Max(sub => sub.QOS);
                var sub = new SubscriptionConfiguration(topicGroup.Key, maxQOS, bases);
                subscriptions.Add(sub);
            }
            return subscriptions;
        }

        public BrokerConfiguration(Dictionary<string, BaseConfiguration> databases, BrokerConfigurationJson jsonConfig)
        {
            Host = jsonConfig.Host;
            Port = jsonConfig.Port;
            Clients = new List<ClientConfiguration>()
            {
                new ClientConfiguration(jsonConfig.User, jsonConfig.Password, MakeSubscriptions(jsonConfig.Subscriptions.GroupBy(sub => sub.Topic), databases))
            };
        }

        public void Merge(Dictionary<string, BaseConfiguration> databases, BrokerConfigurationJson jsonConfig)
        {
            if (jsonConfig.Subscriptions.Length == 0) return;
            var topicGroups = jsonConfig.Subscriptions.GroupBy(sub => sub.Topic);
            var newSubscriptions = MakeSubscriptions(topicGroups, databases);
            var client = Clients.Find(cl => cl.User == jsonConfig.User && cl.Password == jsonConfig.Password);
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
                other?.Host.Equals(this.Host) == true
                && other.Port.Equals(this.Port);
        }

        public bool Equals([AllowNull] BrokerConfigurationJson other)
        {
            return
                other?.Host.Equals(this.Host) == true
                && other.Port.Equals(this.Port);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as BrokerConfiguration);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Host, Port);
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
                var sub = Subscriptions.Find(s => s.Topic.Equals(newSub.Topic));
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

        public SubscriptionConfiguration(string topic, int qos, IEnumerable<BaseConfiguration> bases)
        {
            Topic = topic;
            QOS = qos;
            Databases = bases.ToList();
        }

        public void Merge(SubscriptionConfiguration other)
        {
            QOS = Math.Max(QOS, other.QOS);
            foreach (var newDb in other.Databases)
            {
                if (string.IsNullOrWhiteSpace(newDb.ConnectionString) || newDb.Tables.Count == 0) continue;
                var db = Databases.Find(db => db.Type == newDb.Type && db.ConnectionString.Equals(newDb.ConnectionString));
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
    }

    public sealed class BaseConfiguration : IEquatable<BaseConfiguration>, ICloneable
    {
        public DatabaseType Type { get; }
        public string ConnectionString { get; }
        public List<string> Tables { get; }

        public BaseConfiguration(DatabaseType type, string connectionString, params string[] tables)
        {
            Type = type;
            ConnectionString = connectionString;
            Tables = new List<string>(tables.Where(table => !string.IsNullOrWhiteSpace(table)));
        }

        public void Merge(BaseConfiguration other)
        {
            Tables.AddRange(other.Tables.Where(table => !string.IsNullOrWhiteSpace(table) && !Tables.Contains(table)));
        }

        public void Merge(List<string> tables)
        {
            Tables.AddRange(tables.Where(table => !string.IsNullOrWhiteSpace(table) && !Tables.Contains(table)));
        }

        internal BaseConfiguration Clone()
        {
            return new BaseConfiguration(Type, ConnectionString);
        }

        object ICloneable.Clone()
        {
            return Clone();
        }

        internal BaseConfiguration WithTables(params string[] tables)
        {
            Tables.AddRange(tables.Where(table => !string.IsNullOrWhiteSpace(table) && !Tables.Contains(table)));
            return this;
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
                other?.ConnectionString.Equals(this.ConnectionString) == true;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as BaseConfiguration);
        }

        public override int GetHashCode()
        {
            return ConnectionString.GetHashCode();
        }

        public enum DatabaseType
        {
            None = -1,
            SQLite = 0,
            GeneralSql = 1
        }
    }
}
