using Microsoft.VisualStudio.TestTools.UnitTesting;
using MqttSql;
using System.IO;
using System.Linq;
using System.Reflection;

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
            Assert.AreEqual(expectedLoadedConfig, loadedConfig);

            var brokers = GetBrokersFromConfig(configuration);
            foreach (var (broker, index) in brokers.Select((broker, index) => (broker, index)))
            {
                string str = broker.ToString();
                string expected = File.ReadAllText(configResultsDirPath + $"config{number}broker{index + 1}.txt");
                Assert.AreEqual(expected, str);
            }
        }

        [TestMethod]
        public void TestConfig1()
        {
            TestSampleConfigNumber(1);
        }

        [TestMethod]
        public void TestConfig2()
        {
            TestSampleConfigNumber(2);
        }

        [TestMethod]
        public void TestConfig3()
        {
            TestSampleConfigNumber(3);
        }
    }
}