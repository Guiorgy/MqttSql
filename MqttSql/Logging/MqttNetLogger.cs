/*
    This file is part of MqttSql (Copyright © 2024 Guiorgy).
    MqttSql is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
    MqttSql is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Affero General Public License for more details.
    You should have received a copy of the GNU Affero General Public License along with Foobar. If not, see <https://www.gnu.org/licenses/>.
*/

using MQTTnet.Diagnostics.Logger;
using System;

namespace MqttSql.Logging;

public sealed class MqttNetLogger(string clientId, Logger logger, Logger.LogLevel logLevel, MqttNetLogLevel minMqttLogLevel) : IMqttNetLogger
{
    private readonly string clientId = clientId;
    private readonly Logger logger = logger;
    private readonly Logger.LogLevel logLevel = logLevel;
    private readonly MqttNetLogLevel minMqttLogLevel = minMqttLogLevel;

    public bool IsEnabled => logger.EnabledFor(logLevel);

    public void Publish(MqttNetLogLevel logLevel, string source, string message, object[]? parameters, Exception exception)
    {
        if (logLevel < minMqttLogLevel) return;

        message = $"[{clientId}] [{Enum.GetName(logLevel)} - {source}]\n{message}\n{(parameters != null ? string.Join('\n', parameters) : "")}";

        if (exception == null)
            logger.Log(this.logLevel, message);
        else
            logger.Log(this.logLevel, message, exception);
    }
}
