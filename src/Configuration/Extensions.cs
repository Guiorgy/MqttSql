/*
    This file is part of MqttSql (Copyright © 2024  Guiorgy).
    MqttSql is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
    MqttSql is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
    You should have received a copy of the GNU General Public License along with Foobar. If not, see <https://www.gnu.org/licenses/>.
*/

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
