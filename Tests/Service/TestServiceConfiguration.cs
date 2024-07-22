using Microsoft.VisualStudio.TestTools.UnitTesting;
using MqttSql.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using MqttSql.Logging;
using ServiceConfigurationJson = MqttSql.Configuration.Json.ServiceConfiguration;
using Microsoft.CodeAnalysis;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Tests;

[TestClass]
public sealed class TestServiceConfiguration : VerifyBaseWithDefaultSettings
{
    private static readonly string sampleConfigDirPath = Path.GetFullPath(@"..\..\..\Service\Configuration Samples\", Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!);
    private const string dummyDirPath = @"Some\Path\";
    private static readonly MethodInfo LoadJsonConfigMethodInfo = typeof(ConfigurationLoader).GetMethod("LoadJsonConfig", BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly Logger logger = new Logger(null, false, Logger.LogLevel.None);

    private static ServiceConfigurationJson LoadJsonConfig(string configPath) => (LoadJsonConfigMethodInfo.Invoke(null, [logger, configPath]) as ServiceConfigurationJson)!;

    private static BrokerConfiguration[] GetBrokersFromConfig(ServiceConfigurationJson config) => config.ToServiceConfiguration(logger, dummyDirPath);

    private Task TestSampleConfigNumber(int number)
    {
        var configuration = LoadJsonConfig($"{sampleConfigDirPath}config{number}.json");

        var loadedConfig = configuration.ToString().PrependToLines("    ");
        var brokers = GetBrokersFromConfig(configuration).Select((broker, i) => new KeyValuePair<string, string>($"broker {i + 1}", broker.ToString().PrependToLines("        ")));

        return Verify(
            new
            {
                loadedConfig,
                brokers
            }
        );
    }

    // Test the minimal configuration.
    [TestMethod]
    public Task TestConfig1() => TestSampleConfigNumber(1);

    // Test the merging and discarding of clients.
    [TestMethod]
    public Task TestConfig2() => TestSampleConfigNumber(2);

    // Test the merging of subscriptions.
    [TestMethod]
    public Task TestConfig3() => TestSampleConfigNumber(3);

    // Test when the referenced database isn't defined.
    [TestMethod]
    public Task TestConfig4() => TestSampleConfigNumber(4);

    // Test the merging of databases of same type with the same connection string.
    [TestMethod]
    public Task TestConfig5() => TestSampleConfigNumber(5);

    // Test configuration with no brokers.
    [TestMethod]
    public Task TestConfig6() => TestSampleConfigNumber(6);

    // Test timestamp formats.
    [TestMethod]
    public Task TestConfig7() => TestSampleConfigNumber(7);

    // Test empty database name, and the definition of databases with the same name.
    [TestMethod]
    public Task TestConfig8() => TestSampleConfigNumber(8);

    // Test defining Subscriptions and Databases both as a single object, and as an array of objects.
    [TestMethod]
    public Task TestConfig9() => TestSampleConfigNumber(9);

    // Test the minimal configuration without arrays.
    [TestMethod]
    public Task TestConfig10() => TestSampleConfigNumber(10);
}
