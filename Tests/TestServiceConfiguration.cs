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

        public TestServiceConfiguration()
        {
            var directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            sampleConfigDirPath = Path.GetFullPath(@"..\..\..\Configuration Samples\", directory);
            configResultsDirPath = Path.Combine(sampleConfigDirPath, @"Results\");
        }

        private void TestSampleConfigNumber(int number)
        {
            var configuration = ConfigurationLoader.LoadJsonConfig($"{sampleConfigDirPath}config{number}.json");

            string loadedConfig = configuration.ToString();
            string expectedLoadedConfig = File.ReadAllText(configResultsDirPath + $"config{number}loaded.txt");
            Assert.AreEqual(expectedLoadedConfig, loadedConfig);

            static string GetSQLiteDbPath(string path)
            {
                if (string.IsNullOrWhiteSpace(path))
                    path = dummyDirPath + "database.sqlite";
                else if (path.StartsWith('.'))
                    path = Path.GetFullPath(dummyDirPath + path);
                return path;
            }

            var brokers = ConfigurationLoader.GetBrokersFromConfig(configuration, GetSQLiteDbPath);
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