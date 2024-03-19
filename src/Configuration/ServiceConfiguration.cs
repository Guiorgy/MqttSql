/*
    This file is part of MqttSql (Copyright © 2024  Guiorgy).
    MqttSql is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
    MqttSql is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
    You should have received a copy of the GNU General Public License along with Foobar. If not, see <https://www.gnu.org/licenses/>.
*/

using MqttSql.Database;
using System;

namespace MqttSql.Configuration;

public sealed class BrokerConfiguration(string host, int port, params ClientConfiguration[] clients) : IEquatable<BrokerConfiguration>
{
    public string Host { get; } = host;
    public int Port { get; } = port;
    public ClientConfiguration[] Clients { get; } = clients;

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

public sealed class ClientConfiguration(string user, string password, params SubscriptionConfiguration[] subscriptions) : IEquatable<ClientConfiguration>
{
    private const string salt = "8c57bbe4-147e-4a7b-b1be-8037de032bf0";

    public string User { get; } = user;
    public string Password { get; } = password;
    public SubscriptionConfiguration[] Subscriptions { get; } = subscriptions;

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

public sealed class SubscriptionConfiguration(string topic, SubscriptionConfiguration.MqttQualityOfService qos, params DatabaseConfiguration[] bases) : IEquatable<SubscriptionConfiguration>
{
    public string Topic { get; } = topic;
    public MqttQualityOfService QOS { get; } = qos;
    public DatabaseConfiguration[] Databases { get; } = bases;

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

public sealed class DatabaseConfiguration(DatabaseType type, string connectionString, params TableConfiguration[] tables) : IEquatable<DatabaseConfiguration>
{
    public DatabaseType Type { get; } = type;
    public string ConnectionString { get; } = connectionString;
    public TableConfiguration[] Tables { get; } = tables;

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

public sealed class TableConfiguration(string name, string timestampFormat) : IEquatable<TableConfiguration>
{
    public string Name { get; } = name;
    public string TimestampFormat { get; } = timestampFormat;

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
