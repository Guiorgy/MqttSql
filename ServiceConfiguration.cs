using System;
using System.Collections.Generic;
using System.Linq;
using static MqttSql.ConfigurationsJson.BaseConfiguration;
using BaseConfigurationJson = MqttSql.ConfigurationsJson.BaseConfiguration;
using BrokerConfigurationJson =  MqttSql.ConfigurationsJson.BrokerConfiguration;

namespace MqttSql.Configurations
{
    public class BrokerConfiguration
    {
        public string Host { get; }
        public int Port { get; }
        public string User { get; }
        public string Password { get; }
        public List<SubscriptionConfiguration> Subscriptions { get; }

        public BrokerConfiguration(Dictionary<string, BaseConfigurationJson> databases, BrokerConfigurationJson jsonConfig)
        {
            Host = jsonConfig.Host;
            Port = jsonConfig.Port;
            User = jsonConfig.User;
            Password = jsonConfig.Password;
            var topicGroups = jsonConfig.Subscriptions.GroupBy(sub => sub.Topic);
            Subscriptions = new List<SubscriptionConfiguration>(topicGroups.Count());
            foreach (var topicGroup in topicGroups)
            {
                int maxQOS = topicGroup.Max(sub => sub.QOS);
                Subscriptions.Add(
                    new SubscriptionConfiguration(
                        topicGroup.Key,
                        maxQOS,
                        topicGroup
                            .Select(sub =>(databases.GetValueOrNull(sub.Database), sub.Table))
                            .Where(tuple => tuple.Item1 != null)));
            }
        }

        public void Merge(Dictionary<string, BaseConfigurationJson> databases, BrokerConfigurationJson jsonConfig)
        {
            var topicGroups = jsonConfig.Subscriptions.GroupBy(sub => sub.Topic);
            var newSubscriptions = new List<SubscriptionConfiguration>(topicGroups.Count());
            foreach (var topicGroup in topicGroups)
            {
                int maxQOS = topicGroup.Max(sub => sub.QOS);
                newSubscriptions.Add(
                    new SubscriptionConfiguration(
                        topicGroup.Key,
                        maxQOS,
                        topicGroup
                            .Select(sub => (databases.GetValueOrNull(sub.Database), sub.Table))
                            .Where(tuple => tuple.Item1 != null)));
            }
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
    }

    public class SubscriptionConfiguration
    {
        public string Topic { get; }
        public int QOS { get; private set; }
        public List<BaseConfiguration> Databases { get; }

        public SubscriptionConfiguration(string topic, int qos, IEnumerable<(BaseConfigurationJson, string)> bases)
        {
            Topic = topic;
            QOS = qos;
            var baseGroups = bases.GroupBy(tuple => tuple.Item1);
            Databases = new List<BaseConfiguration>(baseGroups.Count());
            foreach (var baseGroup in baseGroups)
            {
                Databases.Add(
                    new BaseConfiguration(
                        baseGroup.Key,
                        baseGroup.Select(tuple => tuple.Item2).ToArray()));
            }
        }

        public void Merge(SubscriptionConfiguration other)
        {
            QOS = Math.Max(QOS, other.QOS);
            foreach (var newDb in other.Databases)
            {
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
                string.Join(Environment.NewLine,
                    Databases.Select(db => db.ToString().AppendBeforeLines("\t")));
        }

        public class BaseConfiguration
        {
            public DatabaseType Type { get; }
            public string ConnectionString { get; }
            public List<string> Tables { get; }

            public BaseConfiguration(BaseConfigurationJson jsonConfig, params string[] tables)
            {
                Type = jsonConfig.Type;
                ConnectionString = jsonConfig.ConnectionString;
                Tables = new List<string>(tables);
            }

            public void Merge(BaseConfiguration other)
            {
                Tables.AddRange(other.Tables.Where(table => !Tables.Contains(table)));
            }

            public override string ToString()
            {
                return
                    $"Type: {Type}{Environment.NewLine}" +
                    $"ConnectionString: {ConnectionString}{Environment.NewLine}" +
                    $"[{string.Join(", ", Tables)}]{Environment.NewLine}";
            }
        }
    }
}
