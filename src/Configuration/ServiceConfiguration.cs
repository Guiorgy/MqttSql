using MqttSql.Database;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography.X509Certificates;
using SystemSslProtocols = System.Security.Authentication.SslProtocols;

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

public sealed class ClientConfiguration(string user, string password, TlsConfiguration tlsConfiguration, SubscriptionConfiguration[] subscriptions) : IEquatable<ClientConfiguration>
{
    private const string salt = "8c57bbe4-147e-4a7b-b1be-8037de032bf0";

    public string User { get; } = user;
    public string Password { get; } = password;
    public TlsConfiguration TlsConfiguration { get; } = tlsConfiguration;
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
            $"{nameof(TlsConfiguration)}:{Environment.NewLine}" +
                TlsConfiguration.ToString().AppendBeforeLines("\t")[..^1] +
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

public sealed class TlsConfiguration(
    bool enabled,
    TlsConfiguration.SslProtocols sslProtocol,
    X509Certificate2? certificateAuthorityCertificate,
    bool selfSignedCertificateAuthority,
    X509Certificate2? clientCertificate,
    string? clientCertificatePassword,
    bool allowUntrustedCertificates,
    bool ignoreCertificateChainErrors,
    bool ignoreCertificateRevocationErrors) : IEquatable<TlsConfiguration>
{
    private const string salt = "4fa4e62e-5f9f-4ff4-9b8f-542365c533ad";

    public bool Enabled { get; } = enabled;
    public SslProtocols SslProtocol { get; } = sslProtocol;
    public X509Certificate2? CertificateAuthorityCertificate { get; } = certificateAuthorityCertificate;
    public bool SelfSignedCertificateAuthority { get; } = selfSignedCertificateAuthority;
    public X509Certificate2? ClientCertificate { get; } = clientCertificate;
    public string? ClientCertificatePassword { get; } = clientCertificatePassword;
    public bool AllowUntrustedCertificates { get; } = allowUntrustedCertificates;
    public bool IgnoreCertificateChainErrors { get; } = ignoreCertificateChainErrors;
    public bool IgnoreCertificateRevocationErrors { get; } = ignoreCertificateRevocationErrors;

    public override string ToString()
    {
        return
            $"Enabled: {Enabled}{Environment.NewLine}" +
            $"SslProtocol: {SslProtocol.ToFriendlyName()}{Environment.NewLine}" +
            $"CertificateAuthorityCertificate:{Environment.NewLine}" +
                CertificateToString(CertificateAuthorityCertificate).AppendBeforeLines("\t")[..^1] +
            $"SelfSignedCertificateAuthority: {SelfSignedCertificateAuthority}{Environment.NewLine}" +
            $"ClientCertificate:{Environment.NewLine}" +
                CertificateToString(ClientCertificate).AppendBeforeLines("\t")[..^1] +
#if DEBUG
            $"ClientCertificatePassword: {ClientCertificatePassword}{Environment.NewLine}" +
#else
            $"ClientCertificatePassword: {ClientCertificatePassword == null ? "" : new string('*', ClientCertificatePassword.Length)}{Environment.NewLine}" +
#endif
            $"AllowUntrustedCertificates: {AllowUntrustedCertificates}{Environment.NewLine}" +
            $"IgnoreCertificateChainErrors: {IgnoreCertificateChainErrors}{Environment.NewLine}" +
            $"IgnoreCertificateRevocationErrors: {IgnoreCertificateRevocationErrors}{Environment.NewLine}";
    }

    private static string CertificateToString(X509Certificate2? certificate)
    {
        if (certificate == null) return "";

        return
            $"Subject: {certificate.Subject}{Environment.NewLine}" +
            $"Issuer: {certificate.Issuer}{Environment.NewLine}" +
            $"Serial Number: {certificate.SerialNumber}{Environment.NewLine}" +
            $"Not Before: {certificate.NotBefore.ToIsoString(milliseconds: false)}{Environment.NewLine}" +
            $"Not After: {certificate.NotAfter.ToIsoString(milliseconds: false)}{Environment.NewLine}" +
            $"Thumbprint: {certificate.Thumbprint}{Environment.NewLine}";
    }

    public bool Equals(TlsConfiguration? other)
    {
        return
            other?.Enabled == this.Enabled
            && other.SslProtocol == this.SslProtocol
            && other.CertificateAuthorityCertificate?.Equals(this.CertificateAuthorityCertificate) == true
            && other.SelfSignedCertificateAuthority == this.SelfSignedCertificateAuthority
            && other.ClientCertificate?.Equals(this.ClientCertificate) == true
            && other.ClientCertificatePassword == ClientCertificatePassword
            && other.AllowUntrustedCertificates == this.AllowUntrustedCertificates
            && other.IgnoreCertificateChainErrors == this.IgnoreCertificateChainErrors
            && other.IgnoreCertificateRevocationErrors == this.IgnoreCertificateRevocationErrors;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as TlsConfiguration);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(
            Enabled,
            SslProtocol,
            CertificateAuthorityCertificate,
            ClientCertificate,
            ClientCertificatePassword + salt,
            AllowUntrustedCertificates,
            IgnoreCertificateChainErrors,
            IgnoreCertificateRevocationErrors
        );
    }

    public enum SslProtocols
    {
        Auto = SystemSslProtocols.None,
        // Mosquitto still supports TLS 1.1
        [Obsolete("Older versions of SSL / TLS protocol like \"SSLv3\" have been proven to be insecure. Provided for backward compatibility only")]
        TlsV1point1 = SystemSslProtocols.Tls11,
        TlsV1point2 = SystemSslProtocols.Tls12,
        TlsV1point3 = SystemSslProtocols.Tls13
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
