using MqttSql.Logging;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using static Guiorgy.JsonExtensions.Modifiers;
using ServiceConfigurationJson = MqttSql.Configuration.Json.ServiceConfiguration;

namespace MqttSql.Configuration;

public static class ConfigurationLoader
{
    public static BrokerConfiguration[] LoadBrokersFromJson(
        string configPath,
        Logger logger,
        string baseDirectory)
    {
        return LoadJsonConfig(logger, configPath).ToServiceConfiguration(logger, baseDirectory);
    }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        {
            Modifiers = { JsonMultiNameModifier }
        },
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        MaxDepth = 7
    };

    private static ServiceConfigurationJson LoadJsonConfig(Logger logger, string configPath)
    {
        logger.Information("Loading configuration \"", configPath, '"');

        string json = File.ReadAllText(configPath);

        if (Path.DirectorySeparatorChar == '\\')
        {
            json = ConnectionStringRegex.Replace(
                json,
                m => m.Groups[2].Value.Contains(@"\\") ? m.Value : (m.Groups[1].Value + m.Groups[2].Value.Replace(@"\", @"\\") + m.Groups[3].Value)
            );
        }

        string jsonToLog;
#if DEBUG
        jsonToLog = json;
#else
        jsonToLog = PasswordRegex.Replace(
            json,
            m => m.Groups[1].Value + new string('*', m.Groups[2].Length) + m.Groups[3].Value
        );
#endif
        logger.Debug("Configuration Parsed:\n", jsonToLog);

        var configuration = JsonSerializer.Deserialize<ServiceConfigurationJson>(json, _jsonOptions)
            ?? throw new JsonException($"Failed to Deserialize the service configuration file \"{configPath}\"");

        logger.Debug("Configuration loaded:\n", configuration);

        return configuration;
    }

    #region Regex Patterns

#if !DEBUG
    private static readonly Regex PasswordRegex = new("(\\s*\"password\"\\s*:\\s*\")(.*?)((?:\"\\s*)(?:,|$|}|\n|\r))", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
#endif
    private static readonly Regex ConnectionStringRegex = new("(\\s*\"connectionString\"\\s*:\\s*\")(.*?)((?:\"\\s*)(?:,|$|}|\n|\r))", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    #endregion Regex Patterns
}
