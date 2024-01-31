using System;
using static MqttSql.Configuration.SubscriptionConfiguration;
using static MqttSql.Configuration.TlsConfiguration;

namespace MqttSql.Configuration;

public static class Extensions
{
    public static string ToFriendlyString(this MqttQualityOfService mqttQualityOfService)
    {
        return mqttQualityOfService switch
        {
            MqttQualityOfService.AtMostOnce => "At Most Once",
            MqttQualityOfService.AtLeastOnce => "At Least Once",
            MqttQualityOfService.ExactlyOnce => "Exactly Once",
            _ => throw new NotImplementedException($"You forgot to update a switch statement after modifying the {nameof(MqttQualityOfService)} enum.")
        };
    }

    public static string ToFriendlyName(this SslProtocols mqttQualityOfService)
    {
        return mqttQualityOfService switch
        {
            SslProtocols.Auto => "Auto",
            SslProtocols.TlsV1point1 => "TLS v1.1",
            SslProtocols.TlsV1point2 => "TLS v1.2",
            SslProtocols.TlsV1point3 => "TLS v1.3",
            _ => throw new NotImplementedException($"You forgot to update a switch statement after modifying the {nameof(SslProtocols)} enum.")
        };
    }
}
