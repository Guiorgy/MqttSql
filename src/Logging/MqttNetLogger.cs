using MQTTnet.Diagnostics.Logger;
using System;

namespace MqttSql.Logging;

public sealed class MqttNetLogger : IMqttNetLogger
{
    private readonly string clientId;
    private readonly Logger logger;
    private readonly Logger.LogLevel logLevel;
    private readonly MqttNetLogLevel minMqttLogLevel;

    public MqttNetLogger(string clientId, Logger logger, Logger.LogLevel logLevel, MqttNetLogLevel minMqttLogLevel)
    {
        this.clientId = clientId;
        this.logger = logger;
        this.logLevel = logLevel;
        this.minMqttLogLevel = minMqttLogLevel;
    }

    public bool IsEnabled => logger.EnabledFor(logLevel);

    public void Publish(MqttNetLogLevel logLevel, string source, string message, object[] parameters, Exception exception)
    {
        if (logLevel < minMqttLogLevel) return;

        message = $"[{clientId}] [{Enum.GetName(logLevel)} - {source}]\n{message}\n{string.Join('\n', parameters)}";

        if (exception == null)
        {
            logger.Log(this.logLevel, message);
        }
        else
        {
            logger.Log(this.logLevel, message, exception);
        }
    }
}
