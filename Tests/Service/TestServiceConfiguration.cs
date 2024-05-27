using Microsoft.VisualStudio.TestTools.UnitTesting;
using MqttSql.Configuration;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MqttSql.Logging;
using ServiceConfigurationJson = MqttSql.Configuration.Json.ServiceConfiguration;

#if !DEBUG
using System.Text.RegularExpressions;
#endif

namespace Tests;

[TestClass]
public sealed partial class TestServiceConfiguration
{
#if !DEBUG
    [GeneratedRegex("(Password: )(.*?)($|\n|\r)", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline, "en-US")]
    private static partial Regex PasswordRegex();
#endif

    private static readonly string sampleConfigDirPath;
    private static readonly string configResultsDirPath;
    private const string dummyDirPath = @"Some\Path\";
    private static readonly MethodInfo LoadJsonConfigMethodInfo;
    private static readonly Logger logger;

    static TestServiceConfiguration()
    {
        var directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        sampleConfigDirPath = Path.GetFullPath(@"..\..\..\Service\Configuration Samples\", directory);
        configResultsDirPath = Path.Combine(sampleConfigDirPath, @"Results\");
        LoadJsonConfigMethodInfo = typeof(ConfigurationLoader).GetMethod("LoadJsonConfig", BindingFlags.Static | BindingFlags.NonPublic)!;
        logger = new Logger(null, false, Logger.LogLevel.None);
    }

    private static ServiceConfigurationJson LoadJsonConfig(string configPath) => (LoadJsonConfigMethodInfo.Invoke(null, [logger, configPath]) as ServiceConfigurationJson)!;

    private static BrokerConfiguration[] GetBrokersFromConfig(ServiceConfigurationJson config) => config.ToServiceConfiguration(logger, dummyDirPath);

    private static void TestSampleConfigNumber(int number)
    {
        var configuration = LoadJsonConfig($"{sampleConfigDirPath}config{number}.json");

        string loadedConfig = configuration.ToString();
        string expectedLoadedConfig = File.ReadAllText(configResultsDirPath + $"config{number}loaded.txt");
#if !DEBUG
        expectedLoadedConfig = PasswordRegex().Replace(
            expectedLoadedConfig,
            m => m.Groups[1].Value + new string('*', m.Groups[2].Length) + m.Groups[3].Value
        );
#endif
        Assert.AreEqual(
            expectedLoadedConfig,
            loadedConfig,
            $"{Environment.NewLine}{Environment.NewLine}Differences:{Environment.NewLine}{expectedLoadedConfig.DifferenceString(loadedConfig)}");

        var brokers = GetBrokersFromConfig(configuration);

        DirectoryInfo resultsDir = new(configResultsDirPath);
        var brokerResults = resultsDir.GetFiles($"config{number}broker*.txt");
        Assert.AreEqual(brokerResults.Length, brokers.Length, $"The number of parsed Brokers, {brokers.Length} didn't match the expected {brokerResults.Length}!");

        foreach (var (broker, index) in brokers.Select((broker, index) => (broker, index)))
        {
            string str = broker.ToString();
            string expected = File.ReadAllText(configResultsDirPath + $"config{number}broker{index + 1}.txt");
#if !DEBUG
            expected = PasswordRegex().Replace(
                expected,
                m => m.Groups[1].Value + new string('*', m.Groups[2].Length) + m.Groups[3].Value
            );
#endif
            Assert.AreEqual(
                expected,
                str,
                $"{Environment.NewLine}{Environment.NewLine}Differences:{Environment.NewLine}{expected.DifferenceString(str)}");
        }
    }

    // Test the minimal configuration.
    [TestMethod]
    public void TestConfig1() => TestSampleConfigNumber(1);

    // Test the merging and discarding of clients.
    [TestMethod]
    public void TestConfig2() => TestSampleConfigNumber(2);

    // Test the merging of subscriptions.
    [TestMethod]
    public void TestConfig3() => TestSampleConfigNumber(3);

    // Test when the referenced database isn't defined.
    [TestMethod]
    public void TestConfig4() => TestSampleConfigNumber(4);

    // Test the merging of databases of same type with the same connection string.
    [TestMethod]
    public void TestConfig5() => TestSampleConfigNumber(5);

    // Test configuration with no brokers.
    [TestMethod]
    public void TestConfig6() => TestSampleConfigNumber(6);

    // Test timestamp formats.
    [TestMethod]
    public void TestConfig7() => TestSampleConfigNumber(7);

    // Test empty database name, and the definition of databases with the same name.
    [TestMethod]
    public void TestConfig8() => TestSampleConfigNumber(8);

    // Test defining Subscriptions and Databases both as a single object, and as an array of objects.
    [TestMethod]
    public void TestConfig9() => TestSampleConfigNumber(9);

    // Test the minimal configuration without arrays.
    [TestMethod]
    public void TestConfig10() => TestSampleConfigNumber(10);
}
