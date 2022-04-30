using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using BrokerConfigurationJson = MqttSql.ConfigurationsJson.BrokerConfiguration;
using SubscriptionConfigurationJson = MqttSql.ConfigurationsJson.SubscriptionConfiguration;

namespace MqttSql.Configurations
{
    public sealed class BrokerConfiguration : IEquatable<BrokerConfiguration>, IEquatable<BrokerConfigurationJson>, IMergeable<List<SubscriptionConfiguration>, string, string>
    {
        public string Host { get; }
        public int Port { get; }
        public List<ClientConfiguration> Clients { get; }

        private static List<SubscriptionConfiguration> MakeSubscriptions(
            IEnumerable<IGrouping<string, SubscriptionConfigurationJson>> topicGroups,
            Dictionary<string, DatabaseConfiguration> databases)
        {
            var subscriptions = new List<SubscriptionConfiguration>(topicGroups.Count());
            foreach (var topicGroup in topicGroups)
            {
                if (string.IsNullOrWhiteSpace(topicGroup.Key)) continue;
                IEnumerable<DatabaseConfiguration> allbases =
                    topicGroup
                    .SelectMany(sub => sub.Databases.Select(db => databases.GetValueOrNull(db.DatabaseName)?.EmptyClone()?.WithTable(db.Table, db.TimestampFormat)))
                    .Where(db => db != null && db.Tables.Count != 0)!;
                List<DatabaseConfiguration> bases = new(allbases.Count());
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

        public BrokerConfiguration(Dictionary<string, DatabaseConfiguration> databases, BrokerConfigurationJson jsonConfig)
        {
            Host = jsonConfig.Host;
            Port = jsonConfig.Port;
            Clients = new List<ClientConfiguration>()
            {
                new ClientConfiguration(jsonConfig.User, jsonConfig.Password, MakeSubscriptions(jsonConfig.Subscriptions.GroupBy(sub => sub.Topic), databases))
            };
        }

        public void Merge(Dictionary<string, DatabaseConfiguration> databases, BrokerConfigurationJson jsonConfig)
        {
            if (jsonConfig.Subscriptions.Length == 0) return;
            var topicGroups = jsonConfig.Subscriptions.GroupBy(sub => sub.Topic);
            var newSubscriptions = MakeSubscriptions(topicGroups, databases);
            Merge(newSubscriptions, jsonConfig.User, jsonConfig.Password);
        }

        public void Merge(List<SubscriptionConfiguration> subscriptions, string user, string password)
        {
            var client = Clients.Find(cl => cl.User == user && cl.Password == password);
            if (client == null)
                Clients.Add(new ClientConfiguration(user, password, subscriptions));
            else
                client.Merge(subscriptions);
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

        public override bool Equals(object? obj)
        {
            return Equals(obj as BrokerConfiguration);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Host, Port);
        }
    }

    public sealed class ClientConfiguration : IMergeable<List<SubscriptionConfiguration>>
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

    public sealed class SubscriptionConfiguration : IMergeable<SubscriptionConfiguration>
    {
        public string Topic { get; }
        public int QOS { get; private set; }
        public List<DatabaseConfiguration> Databases { get; }

        public SubscriptionConfiguration(string topic, int qos, IEnumerable<DatabaseConfiguration> bases)
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

    public sealed class DatabaseConfiguration : IEquatable<DatabaseConfiguration>, ICloneable, IMergeable<DatabaseConfiguration>, IMergeable<List<DatabaseConfiguration.TableConfiguration>>
    {
        public DatabaseType Type { get; }
        public string ConnectionString { get; }
        public List<TableConfiguration> Tables { get; }

        public DatabaseConfiguration(DatabaseType type, string connectionString, params TableConfiguration[] tables)
        {
            Type = type;
            ConnectionString = connectionString;
            Tables = new(tables.Where(table => table.IsValid));
        }

        public void Merge(DatabaseConfiguration other)
        {
            Merge(other.Tables);
        }

        public void Merge(List<TableConfiguration> tables)
        {
            Tables.AddRange(tables.Where(table => table.IsValid && !Tables.Contains(table)));
        }

        internal DatabaseConfiguration EmptyClone()
        {
            return new DatabaseConfiguration(Type, ConnectionString);
        }

        object ICloneable.Clone()
        {
            return EmptyClone().WithTables(Tables);
        }

        internal DatabaseConfiguration WithTable(string Name, string TimestampFormat)
        {
            var table = new TableConfiguration(Name, TimestampFormat);
            if (table.IsValid && !Tables.Contains(table)) Tables.Add(table);
            return this;
        }

        internal DatabaseConfiguration WithTables(params TableConfiguration[] tables)
        {
            return WithTables(tables.ToList());
        }

        internal DatabaseConfiguration WithTables(List<TableConfiguration> tables)
        {
            Tables.AddRange(tables.Where(table => table.IsValid && !Tables.Contains(table)));
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

        public bool Equals([AllowNull] DatabaseConfiguration other)
        {
            return other?.ConnectionString.Equals(this.ConnectionString) == true;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as DatabaseConfiguration);
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

        public sealed class TableConfiguration : IEquatable<TableConfiguration>
        {
            public string Name { get; }
            public string TimestampFormat { get; }

            public TableConfiguration(string name, string timestampFormat)
            {
                Name = name;
                TimestampFormat = timestampFormat;
            }

            public void Deconstruct(out string Name, out string TimestampFormat)
            {
                Name = this.Name;
                TimestampFormat = this.TimestampFormat;
            }

            private static readonly DateTime SampleDateTime = new(2022, 04, 26, 16, 10, 30, 500);
            /*
             * A DateTime format will be considered valid if:
             * - The format string is not empty/white space.
             * - Converting DateTime.Now to string and parsing it back doesn't throw an exception.
             * - Converting DateTime.Now to string and parsing it back retains at least some information,
             *   i.e. the round-trip parsed DateTime shouldn't be equal to the default.
             *   ((DateTime)default).ToString("yyyy/MM/dd-HH:mm:ss.fff", CultureInfo.InvariantCulture) = "0001/01/01-00:00:00.000"
             */
            public bool IsValid
            {
                get
                {
                    if (string.IsNullOrWhiteSpace(Name)) return false;
                    try
                    {
                        var dt = DateTime.ParseExact(
                            SampleDateTime.ToString(TimestampFormat, CultureInfo.InvariantCulture),
                            TimestampFormat,
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.NoCurrentDateDefault);
                        return dt != default;
                    }
                    catch
                    {
                        return false;
                    }
                }
            }

            public override string ToString()
            {
                return $"(Name: {Name}, TimestampFormat: {TimestampFormat})";
            }

            public bool Equals([AllowNull] TableConfiguration other)
            {
                return other?.Name.Equals(this.Name) == true;
            }

            public override bool Equals(object? obj)
            {
                return Equals(obj as TableConfiguration);
            }

            public override int GetHashCode()
            {
                return Name.GetHashCode();
            }
        }
    }
}
