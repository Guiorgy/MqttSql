using System;
using static MqttSql.Configuration.SubscriptionConfiguration;

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
}
