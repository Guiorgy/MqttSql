using Microsoft.VisualStudio.TestTools.UnitTesting;
using MqttSql.Configurations;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using static MqttSql.Configurations.SubscriptionConfiguration;
using static MqttSql.ConfigurationsJson.BaseConfiguration;

using BaseConfigurationJson = MqttSql.ConfigurationsJson.BaseConfiguration;

using ServiceConfigurationJson = MqttSql.ConfigurationsJson.ServiceConfiguration;

namespace Tests
{
    [TestClass]
    public class TestServiceConfiguration
    {
        private void TestSampleConfigNumber(int number)
        {
            var configuration = LoadConfiguration($"config{number}.json");

            string loadedConfig = configuration.ToString();
            //System.Console.WriteLine(loadedConfig);
            string expectedLoadedConfig = File.ReadAllText(configResultsDirPath + $"config{number}loaded.txt");
            Assert.AreEqual(expectedLoadedConfig, loadedConfig);

            var brokers = ParseConfiguration(configuration);
            foreach (var (broker, index) in brokers.Select((broker, index) => (broker, index)))
            {
                string str = broker.ToString();
                System.Console.WriteLine(str);
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

        private readonly string sampleConfigDirPath;
        private readonly string configResultsDirPath;
        private const string dummyDirPath = @"Some\Path\";

        public TestServiceConfiguration()
        {
            var directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            sampleConfigDirPath = Path.GetFullPath(@"..\..\..\Configuration Samples\", directory);
            configResultsDirPath = Path.Combine(sampleConfigDirPath, @"Results\");
        }

        private ServiceConfigurationJson LoadConfiguration(string filename)
        {
            var jsonOptions = new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                MaxDepth = 5
            };

            string json = File.ReadAllText(sampleConfigDirPath + filename);
            return JsonSerializer.Deserialize<ServiceConfigurationJson>(json, jsonOptions)!;
        }

        private BrokerConfiguration[] ParseConfiguration(ServiceConfigurationJson configuration)
        {
            var databases = new Dictionary<string, BaseConfigurationJson>(configuration.Databases.Length);
            foreach (var db in configuration.Databases)
            {
                if (!databases.ContainsKey(db.Name))
                {
                    if (db.Type != DatabaseType.SQLite)
                    {
                        databases.Add(db.Name, db);
                    }
                    else
                    {
                        string connectionString = db.ConnectionString ?? "Version3;";
                        string path =
                            Regex.Match(connectionString,
                            "(Data Source\\s*=\\s*)(.*?)(;|$)").Groups[2].Value;
                        if (string.IsNullOrWhiteSpace(path))
                            path = dummyDirPath + "database.sqlite";
                        else if (path.StartsWith('.'))
                            path = Path.GetFullPath(dummyDirPath + path);
                        connectionString =
                            Regex.IsMatch(connectionString, "(Data Source\\s*=\\s*)(.*?)(;|$)") ?
                                Regex.Replace(connectionString,
                                "(Data Source\\s*=\\s*)(.*?)(;|$)",
                                $"$1{path}$3") :
                                $"Data Source={path};{connectionString}";
                        databases.Add(db.Name, new BaseConfigurationJson(db.Name, DatabaseType.SQLite, connectionString));
                    }
                }
            }

            var brokers = new List<BrokerConfiguration>(configuration.Brokers.Length);
            foreach (var broker in configuration.Brokers)
            {
                var similar = brokers.FirstOrDefault(b => b.Equals(broker));
                if (similar == null)
                {
                    var newBroker = new BrokerConfiguration(databases, broker);
                    if (newBroker.Clients[0].Subscriptions.Count != 0) brokers.Add(newBroker);
                }
                else
                    similar.Merge(databases, broker);
            }

            return brokers.ToArray();
        }
    }
}