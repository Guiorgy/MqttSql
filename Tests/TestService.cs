using Microsoft.VisualStudio.TestTools.UnitTesting;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using MQTTnet.Diagnostics.Logger;
using MQTTnet.Protocol;
using MQTTnet.Server;
using MqttSql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.OSPlatform;
using static System.Runtime.InteropServices.RuntimeInformation;
using ServiceConfigurationJson = MqttSql.ConfigurationsJson.ServiceConfiguration;

namespace Tests
{
	[TestClass]
	public class TestService
	{
		private readonly CancellationTokenSource cancellationToken = new();
		private readonly Random rand = new();
		private readonly int port = 20000;
		private readonly string? homeDir;
		private const int maxReplaceCount = 10;
		private readonly string[]? sqlitePaths;
		private readonly IMqttClientOptions? mqttClientOptions;
		private readonly IMqttClient? mqttClient;
		private readonly Service? service;
		private readonly string[]? topics;
		private readonly Dictionary<string, List<string>[]>? topicDestinations;
		private readonly Dictionary<string, List<string>>[]? inMemoryDatabases;
		private IMqttServer? mqttServer = null;

		[TestMethod]
		public async Task TestSimulation()
        {
			if (cancellationToken.IsCancellationRequested)
                Assert.Inconclusive(mqttServer == null ? "Test setup failed!" : "Test cancelled!");

            try
            {
                async Task PublishTask(string topic, string message)
                {
                    var tables = topicDestinations![topic];
                    for (int i = 0; i < tables.Length; i++)
                    {
                        foreach (var table in tables[i])
                        {
                            if (inMemoryDatabases![i].TryGetValue(table, out List<string>? messages))
                            {
                                messages.Add(message);
                            }
                            else
                            {
                                messages = new List<string>() { message };
                                inMemoryDatabases[i].Add(table, messages);
                            }
                        }
                    }

                    var mqttMessage = new MqttApplicationMessageBuilder()
                            .WithTopic(topic)
                            .WithPayload(message)
                            .WithExactlyOnceQoS()
                            .WithRetainFlag()
                            .Build();
                    await mqttClient!.PublishAsync(mqttMessage, cancellationToken.Token);
                }

                service!.StartAsync().ExceptionToConsole();

                await Task.Delay(3000, cancellationToken.Token);

                await mqttClient!.ConnectAsync(mqttClientOptions, cancellationToken.Token);

                if (cancellationToken.IsCancellationRequested)
                    Assert.Inconclusive("Test cancelled!");

                const int maxMessages = 100;
                int count = rand.Next(15, maxMessages);
                for (int i = 0; i < count; i++)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    await PublishTask(topics![rand.Next(topics.Length)], rand.Next().ToString());
                    await Task.Delay(rand.Next(10, 1000), cancellationToken.Token);
                }
                await Task.Delay(TimeSpan.FromSeconds(count), cancellationToken.Token);

                if (cancellationToken.IsCancellationRequested)
                    Assert.Inconclusive("Test cancelled!");

                for (int i = 0; i < sqlitePaths!.Length; i++)
                {
                    await using (SQLiteConnection connect = new($"Data Source={sqlitePaths[i]};Version=3;"))
                    {
                        await connect.OpenAsync(cancellationToken.Token);
                        await using SQLiteCommand command = connect.CreateCommand();
                        command.CommandText = "SELECT name FROM sqlite_schema WHERE type = 'table' AND name NOT LIKE 'sqlite_%';";
                        command.CommandType = CommandType.Text;
                        SQLiteDataReader reader = command.ExecuteReader();
                        var tables = topicDestinations!.SelectMany(td => td.Value[i]).Distinct().ToHashSet();
                        while (await reader.ReadAsync(cancellationToken.Token))
                        {
                            string? table = Convert.ToString(reader["name"]);
                            if (table != null && !tables.Remove(table))
                                Assert.Fail($"Validation failed! The table \"{table}\" shouldn't exist inside \"{sqlitePaths[i]}\"!");
                        }
                        if (tables.Count != 0)
                            Assert.Fail($"Validation failed! The table \"{tables.First()}\" wasn't found exist inside \"{sqlitePaths[i]}\"!");
                    }

                    foreach (var table in inMemoryDatabases![i])
                    {
                        if (cancellationToken.IsCancellationRequested)
                            Assert.Inconclusive("Test cancelled!");

                        List<string> messages = new(table.Value.Count + 10);
                        await using (SQLiteConnection connect = new($"Data Source={sqlitePaths[i]};Version=3;"))
                        {
                            await connect.OpenAsync(cancellationToken.Token);
                            await using SQLiteCommand command = connect.CreateCommand();
                            command.CommandText = $"SELECT Message FROM {table.Key}";
                            command.CommandType = CommandType.Text;
                            SQLiteDataReader reader = command.ExecuteReader();
                            while (await reader.ReadAsync(cancellationToken.Token))
                                messages.Add(Convert.ToString(reader["Message"]) ?? "NULL");
                        }

                        if (messages.Count != table.Value.Count && table.Value.Except(messages).Any())
                            Assert.Fail("Validation failed!");
                    }
                }

                Console.WriteLine("Validation passed!");
            }
            catch (AssertInconclusiveException)
            {
                throw;
            }
            catch (AssertFailedException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.BackgroundColor = ConsoleColor.Yellow;
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
                Console.ResetColor();
                Console.WriteLine();
                Assert.Inconclusive("Exception thrown!");
            }
        }

        public TestService()
        {
            try
            {
                var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
                var tcpConnInfoArray = ipGlobalProperties.GetActiveTcpConnections().Select(tcpConnInfo => tcpConnInfo.LocalEndPoint.Port);
                while (!tcpConnInfoArray.Contains(port)) port++;

                string dirSep = IsOSPlatform(Windows) ? @"\" : "/";
                homeDir = Directory.GetCurrentDirectory();
                if (!homeDir.EndsWith(dirSep)) homeDir += dirSep;

                string config = configurationTemplate.Replace("{PORT}", $"{port}");

                int dbs = 0;
                for (int i = 1; i <= maxReplaceCount; i++)
                {
                    if (config.Contains($"{{DB_PATH_{i}}}"))
                        dbs++;
                    else
                        break;
                }

                sqlitePaths = new string[dbs];
                for (int i = 1; i <= dbs; i++)
                {
                    sqlitePaths[i - 1] = homeDir + $"database_{i}.sqlite";
                    config = config.Replace($"{{DB_PATH_{i}}}", sqlitePaths[i - 1]).Replace($"{{DB_NAME_{i}}}", $"db{i}");

                    if (File.Exists(sqlitePaths[i - 1])) File.Delete(sqlitePaths[i - 1]);
                }

                string configPath = homeDir + "config.json";
                File.WriteAllText(configPath, config);

                var factory = new MqttFactory();
                mqttClient = factory.CreateMqttClient();
                mqttClientOptions = new MqttClientOptionsBuilder()
                    .WithClientId("MqttSql-Simulator-Client")
                    .WithTcpServer("localhost", port)
                    .WithCredentials("user", "password")
                    .WithKeepAlivePeriod(TimeSpan.FromHours(1))
                    .WithSessionExpiryInterval(uint.MaxValue)
                    .Build();

                mqttClient.UseDisconnectedHandler(async e =>
                {
                    Console.WriteLine($"Simulator disconnected! Reason: \"{e.Reason}\"");
                    try
                    {
                        await mqttClient.ConnectAsync(mqttClientOptions, cancellationToken.Token);
                        Console.WriteLine("Simulator reconnected!");
                    }
                    catch
                    {
                        Console.WriteLine("Simulator reconnection Failed!");
                    }
                });

                config = Regex.Replace(
                    config,
                    "(\"connectionString\"\\s*:\\s*\")(.*?)(\")(,|\n|\r)",
                    m => m.Groups[2].Value.Contains(@"\\") ? m.Value : (m.Groups[1].Value + m.Groups[2].Value.Replace(@"\", @"\\") + '"' + m.Groups[4].Value),
                    RegexOptions.IgnoreCase
                );

                var jsonOptions = new JsonSerializerOptions()
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    MaxDepth = 5
                };
                ServiceConfigurationJson? configuration = JsonSerializer.Deserialize<ServiceConfigurationJson>(config, jsonOptions);
                if (configuration == null)
                    throw new JsonException($"Failed the deserialization of the configuration!{Environment.NewLine}Configuration text:{Environment.NewLine}{config}");

                var subscriptions = configuration.Brokers.SelectMany(b => b.Subscriptions);
                topicDestinations = new(subscriptions.Count());
                foreach (var sub in subscriptions)
                {
                    if (!topicDestinations.TryGetValue(sub.Topic, out List<string>[]? bases))
                    {
                        bases = new List<string>[10];
                        for (int i = 0; i < bases.Length; i++) bases[i] = new();
                        topicDestinations.Add(sub.Topic, bases);
                    }
                    bases[int.Parse(sub.Database[2..]) - 1].Add(sub.Table);
                }

                topics = topicDestinations.Keys.ToArray();

                service = new Service(homeDir, dirSep);

                inMemoryDatabases = new Dictionary<string, List<string>>[sqlitePaths.Length];
                for (int i = 0; i < inMemoryDatabases.Length; i++)
                    inMemoryDatabases[i] = new Dictionary<string, List<string>>(100);
            }
            catch (Exception ex)
            {
                cancellationToken.Cancel();
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }

        [TestInitialize]
        public async Task TestServiceInitialize()
        {
            if (cancellationToken.IsCancellationRequested) return;
            try
            {
                var optionsBuilder = new MqttServerOptionsBuilder()
                    .WithDefaultCommunicationTimeout(TimeSpan.FromHours(10))
                    .WithoutEncryptedEndpoint()
                    .WithConnectionBacklog(100)
                    .WithDefaultEndpointPort(port)
                    .WithConnectionValidator(c =>
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            c.ReasonCode = MqttConnectReasonCode.ImplementationSpecificError;
                            return;
                        }

                        if (c.Username == "user" && c.Password == "password")
                        {
                            c.ReasonCode = MqttConnectReasonCode.Success;
                            return;
                        }

                        for (int i = 0; i <= maxReplaceCount; i++)
                        {
                            if (c.Username == $"user{i}" && c.Password == $"password{i}")
                            {
                                c.ReasonCode = MqttConnectReasonCode.Success;
                                return;
                            }
                        }

                        c.ReasonCode = MqttConnectReasonCode.BadUserNameOrPassword;
                    });

                mqttServer = new MqttFactory(new MqttLogger()).CreateMqttServer();
                await mqttServer.StartAsync(optionsBuilder.Build());
            }
            catch (Exception ex)
            {
                cancellationToken.Cancel();
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }

        [TestCleanup]
		public async Task TestServiceCleanup()
		{
			cancellationToken.Cancel();
            service?.Stop();
            if (mqttServer != null)
                await mqttServer.StopAsync();
        }

		public class MqttLogger : IMqttNetLogger
		{
			public void Publish(MqttNetLogLevel logLevel, string source, string message, object[] parameters, Exception? exception)
			{
				Console.WriteLine($"{logLevel}: {source} -> {message}");
				Console.WriteLine(parameters);
				if (exception != null)
				{
					Console.WriteLine(exception.Message);
					Console.WriteLine(exception.StackTrace);
				}
			}
		}

        private static readonly string configurationTemplate =
            @"{
				""databases"": [
					{
						""name"": ""{DB_NAME_1}"",
						""type"": ""SQLite"",
						""connectionString"": ""Data Source={DB_PATH_1};Version=3;""
					},
					{
						""name"": ""{DB_NAME_2}"",
						""type"": ""SQLite"",
						""connectionString"": ""Data Source={DB_PATH_2};Version=3;""
					},
					{
						""name"": ""{DB_NAME_3}"",
						""type"": ""SQLite"",
						""connectionString"": ""Data Source={DB_PATH_3};Version=3;""
					}
				],
				""brokers"": [
					{
						""host"": ""localhost"",
						""port"": {PORT},
						""user"": ""user1"",
						""password"": ""password1"",
						""subscriptions"": [
							{
								""topic"": ""some/topic/1"",
								""qos"": 2,
								""base"": ""{DB_NAME_1}"",
								""table"": ""table_1_1""
							},
							{
								""topic"": ""some/topic/1"",
								""qos"": 2,
								""base"": ""{DB_NAME_1}"",
								""table"": ""table_1_2""
							},
							{
								""topic"": ""some/topic/1"",
								""qos"": 2,
								""base"": ""{DB_NAME_2}"",
								""table"": ""table_2_1""
							},
							{
								""topic"": ""some/topic/2"",
								""qos"": 2,
								""base"": ""{DB_NAME_2}"",
								""table"": ""table_2_2""
							},
							{
								""topic"": ""some/topic/2"",
								""qos"": 2,
								""base"": ""{DB_NAME_2}"",
								""table"": ""table_2_2""
							},
							{
								""topic"": ""some/topic/2"",
								""qos"": 2,
								""base"": ""{DB_NAME_3}"",
								""table"": ""table_3_2""
							}
						]
					},
					{
						""host"": ""localhost"",
						""port"": {PORT},
						""user"": ""user1"",
						""password"": ""password1"",
						""subscriptions"": [
							{
								""topic"": ""some/topic/3"",
								""qos"": 2,
								""base"": ""{DB_NAME_1}"",
								""table"": ""table_1_3""
							},
							{
								""topic"": ""some/topic/3"",
								""qos"": 2,
								""base"": ""{DB_NAME_3}"",
								""table"": ""table_3_3""
							}
						]
					},
					{
						""host"": ""localhost"",
						""port"": {PORT},
						""user"": ""user2"",
						""password"": ""password2"",
						""subscriptions"": [
							{
								""topic"": ""some/topic/1"",
								""qos"": 2,
								""base"": ""{DB_NAME_1}"",
								""table"": ""table_1_1""
							},
							{
								""topic"": ""some/topic/1"",
								""qos"": 2,
								""base"": ""{DB_NAME_1}"",
								""table"": ""table_1_2""
							},
							{
								""topic"": ""some/topic/3"",
								""qos"": 2,
								""base"": ""{DB_NAME_1}"",
								""table"": ""table_1_3""
							},
							{
								""topic"": ""some/topic/3"",
								""qos"": 2,
								""base"": ""{DB_NAME_3}"",
								""table"": ""table_3_3""
							}
						]
					},
					{
						""host"": ""localhost"",
						""port"": {PORT},
						""user"": ""user3"",
						""password"": ""password3"",
						""subscriptions"": [
							{
								""topic"": ""some/topic/2"",
								""qos"": 2,
								""base"": ""{DB_NAME_1}"",
								""table"": ""table_1_2""
							},
							{
								""topic"": ""some/topic/2"",
								""qos"": 2,
								""base"": ""{DB_NAME_1}"",
								""table"": ""table_2_2""
							}
						]
					}
				]
			}"
            .SplitLines()
            .Select(s => s.StartsWith("			") ? s[("			".Length)..] : s)
            .JoinLines();
    }
}
