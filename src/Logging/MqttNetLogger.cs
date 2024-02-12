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
