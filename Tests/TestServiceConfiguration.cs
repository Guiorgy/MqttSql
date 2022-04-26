using Microsoft.VisualStudio.TestTools.UnitTesting;
using MqttSql;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

#if !DEBUG
using System.Text.RegularExpressions;
#endif

namespace Tests
{
    [TestClass]
    public class TestServiceConfiguration
    {
        private readonly string sampleConfigDirPath;
        private readonly string configResultsDirPath;
        private const string dummyDirPath = @"Some\Path\";
        private readonly MethodInfo LoadJsonConfigMethodInfo;
        private readonly MethodInfo GetBrokersFromConfigMethodInfo;

#if !DEBUG
        private static readonly Regex PasswordRegex = new("(Password: )(.*?)($|\n|\r)", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
#endif

        public TestServiceConfiguration()
        {
            var directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            sampleConfigDirPath = Path.GetFullPath(@"..\..\..\Configuration Samples\", directory);
            configResultsDirPath = Path.Combine(sampleConfigDirPath, @"Results\");
            LoadJsonConfigMethodInfo = typeof(ConfigurationLoader).GetMethod("LoadJsonConfig", BindingFlags.Static | BindingFlags.NonPublic)!;
            GetBrokersFromConfigMethodInfo = typeof(ConfigurationLoader).GetMethod("GetBrokersFromConfig", BindingFlags.Static | BindingFlags.NonPublic)!;
        }

        private MqttSql.ConfigurationsJson.ServiceConfiguration LoadJsonConfig(string configPath)
        {
            return (LoadJsonConfigMethodInfo.Invoke(null, new object?[]{ configPath, null }) as MqttSql.ConfigurationsJson.ServiceConfiguration)!;
        }

        private MqttSql.Configurations.BrokerConfiguration[] GetBrokersFromConfig(MqttSql.ConfigurationsJson.ServiceConfiguration config)
        {
            static string GetSQLiteDbPath(string path)
            {
                if (string.IsNullOrWhiteSpace(path))
                    path = dummyDirPath + "database.sqlite";
                else if (path.StartsWith('.'))
                    path = Path.GetFullPath(dummyDirPath + path);
                return path;
            }

#pragma warning disable CS8974 // Converting method group to non-delegate type
            return (GetBrokersFromConfigMethodInfo.Invoke(null, new object?[] { config, GetSQLiteDbPath, null }) as MqttSql.Configurations.BrokerConfiguration[])!;
#pragma warning restore CS8974 // Converting method group to non-delegate type
        }

        private void TestSampleConfigNumber(int number)
        {
            var configuration = LoadJsonConfig($"{sampleConfigDirPath}config{number}.json");

            string loadedConfig = configuration.ToString();
            string expectedLoadedConfig = File.ReadAllText(configResultsDirPath + $"config{number}loaded.txt");
#if !DEBUG
            expectedLoadedConfig = PasswordRegex.Replace(
                expectedLoadedConfig,
                m => m.Groups[1].Value + new string('*', m.Groups[2].Length) + m.Groups[3].Value
            );
#endif
            Assert.AreEqual(
                expectedLoadedConfig,
                loadedConfig,
                $"{Environment.NewLine}{Environment.NewLine}Differences:{Environment.NewLine}{expectedLoadedConfig.DifferenceString(loadedConfig)}");

            var brokers = GetBrokersFromConfig(configuration);

            DirectoryInfo resultsDir = new DirectoryInfo(configResultsDirPath);
            var brokerResults = resultsDir.GetFiles($"config{number}broker*.txt");
            Assert.AreEqual(brokerResults.Length, brokers.Length, $"The number of parsed Brokers, {brokers.Length} didn't match the expected {brokerResults.Length}!");

            foreach (var (broker, index) in brokers.Select((broker, index) => (broker, index)))
            {
                string str = broker.ToString();
                string expected = File.ReadAllText(configResultsDirPath + $"config{number}broker{index + 1}.txt");
#if !DEBUG
                expected = PasswordRegex.Replace(
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

        // Test the minimal configuration
        [TestMethod]
        public void TestConfig1()
        {
            TestSampleConfigNumber(1);
        }

        // Test the merging and discarding of clients
        [TestMethod]
        public void TestConfig2()
        {
            TestSampleConfigNumber(2);
        }

        // Test the merging of subscriptions.
        [TestMethod]
        public void TestConfig3()
        {
            TestSampleConfigNumber(3);
        }

        // Test when the referenced database isn't defined.
        [TestMethod]
        public void TestConfig4()
        {
            TestSampleConfigNumber(4);
        }

        // Test the merging of databases of same type with the same connection string.
        [TestMethod]
        public void TestConfig5()
        {
            TestSampleConfigNumber(5);
        }

        // Test configuration with no brokers.
        [TestMethod]
        public void TestConfig6()
        {
            TestSampleConfigNumber(6);
        }

        // Test timestamp formats.
        [TestMethod]
        public void TestConfig7()
        {
            TestSampleConfigNumber(7);
        }
    }
}