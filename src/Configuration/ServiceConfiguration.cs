using MqttSql.Database;
using System;

namespace MqttSql.Configuration;

public sealed class BrokerConfiguration : IEquatable<BrokerConfiguration>
{
    public string Host { get; }
    public int Port { get; }
    public ClientConfiguration[] Clients { get; }

    public BrokerConfiguration(string host, int port, params ClientConfiguration[] clients)
    {
        Host = host;
        Port = port;
        Clients = clients;
    }

    public override string ToString()
    {
        return
            $"{nameof(Host)}: {Host}{Environment.NewLine}" +
            $"{nameof(Port)}: {Port}{Environment.NewLine}" +
            $"{nameof(Clients)}:{Environment.NewLine}" +
                Clients.ToString(prefix: "\t", separator: Environment.NewLine, prefixPostfixLines: true);
    }

    public bool Equals(BrokerConfiguration? other)
    {
        return
            other?.Host == this.Host
            && other.Port == this.Port;
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

public sealed class ClientConfiguration : IEquatable<ClientConfiguration>
{
    private const string salt = "8c57bbe4-147e-4a7b-b1be-8037de032bf0";

    public string User { get; }
    public string Password { get; }
    public SubscriptionConfiguration[] Subscriptions { get; }

    public ClientConfiguration(string user, string password, params SubscriptionConfiguration[] subscriptions)
    {
        User = user;
        Password = password;
        Subscriptions = subscriptions;
    }

    public override string ToString()
    {
        return
            $"{nameof(User)}: {User}{Environment.NewLine}" +
#if DEBUG
            $"{nameof(Password)}: {Password}{Environment.NewLine}" +
#else
            $"{nameof(Password)}: {new string('*', Password.Length)}{Environment.NewLine}" +
#endif
            $"{nameof(Subscriptions)}:{Environment.NewLine}" +
                Subscriptions.ToString(prefix: "\t", separator: Environment.NewLine, prefixPostfixLines: true);
    }

    public bool Equals(ClientConfiguration? other)
    {
        return
            other?.User == this.User
            && other.Password == this.Password;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as ClientConfiguration);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(User, Password + salt);
    }
}

public sealed class SubscriptionConfiguration : IEquatable<SubscriptionConfiguration>
{
    public string Topic { get; }
    public MqttQualityOfService QOS { get; }
    public DatabaseConfiguration[] Databases { get; }

    public SubscriptionConfiguration(string topic, MqttQualityOfService qos, params DatabaseConfiguration[] bases)
    {
        Topic = topic;
        QOS = qos;
        Databases = bases;
    }

    public override string ToString()
    {
        return
            $"{nameof(Topic)}: {Topic}{Environment.NewLine}" +
            $"{nameof(QOS)}: {QOS.ToFriendlyString()}{Environment.NewLine}" +
            $"{nameof(Databases)}:{Environment.NewLine}" +
                Databases.ToString(prefix: "\t", separator: Environment.NewLine, prefixPostfixLines: true);
    }

    public bool Equals(SubscriptionConfiguration? other)
    {
        return other?.Topic == this.Topic;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as SubscriptionConfiguration);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Topic);
    }

    public enum MqttQualityOfService
    {
        AtMostOnce = 0,
        AtLeastOnce = 1,
        ExactlyOnce = 2
    }
}

public sealed class DatabaseConfiguration : IEquatable<DatabaseConfiguration>
{
    public DatabaseType Type { get; }
    public string ConnectionString { get; }
    public TableConfiguration[] Tables { get; }

    public DatabaseConfiguration(DatabaseType type, string connectionString, params TableConfiguration[] tables)
    {
        Type = type;
        ConnectionString = connectionString;
        Tables = tables;
    }

    public override string ToString()
    {
        return
            $"{nameof(Type)}: {Type.ToFriendlyString()}{Environment.NewLine}" +
            $"{nameof(ConnectionString)}: {ConnectionString}{Environment.NewLine}" +
            (
                Tables.Length == 1
                ? $"Table: {Tables[0]}{Environment.NewLine}"
                : $"{nameof(Tables)}: {Tables.ToString(open: "[", separator: ", ", close: "]")}{Environment.NewLine}"
            );
    }

    public bool Equals(DatabaseConfiguration? other)
    {
        return other?.ConnectionString == this.ConnectionString;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as DatabaseConfiguration);
    }

    public override int GetHashCode()
    {
        return ConnectionString.GetHashCode();
    }
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

    public override string ToString()
    {
        return $"({nameof(Name)}: {Name}, {nameof(TimestampFormat)}: {TimestampFormat})";
    }

    public bool Equals(TableConfiguration? other)
    {
        return other?.Name == this.Name;
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
