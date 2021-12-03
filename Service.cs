#if !LOG && DEBUG
#define LOG
#endif

using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using MQTTnet.Client.Subscribing;
using MQTTnet.Protocol;
using MqttSql.Configurations;
using static MqttSql.ConfigurationsJson.BaseConfiguration;
using BaseConfigurationJson = MqttSql.ConfigurationsJson.BaseConfiguration;
using BrokerConfigurationJson = MqttSql.ConfigurationsJson.BrokerConfiguration;
using ServiceConfigurationJson = MqttSql.ConfigurationsJson.ServiceConfiguration;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace MqttSql
{
    public class Service
    {
        public Service(string homeDir = null, string dirSep = "\\")
        {
            string macAddress = NetworkInterface
                .GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Select(nic => nic.GetPhysicalAddress().ToString())
                .FirstOrDefault();
            this.clientId = $"{macAddress}-{Environment.MachineName}-{Environment.UserName}".Replace(' ', '.');
            DebugLog($"ClientId: \"{this.clientId}\"");

            this.homeDir = homeDir ?? Environment.GetEnvironmentVariable("MqttSqlHome");
            if (!this.homeDir.EndsWith(dirSep)) this.homeDir += dirSep;
            DebugLog($"Home: \"{this.homeDir}\"");

            this.configPath = this.homeDir + "config.json";
            DebugLog($"Configuration: \"{this.configPath}\"");

#if LOG
            this.logPath = this.homeDir + "logs.txt";
            DebugLog($"Logs: \"{this.logPath}\"");
#endif
        }

        public void Start()
        {
            Task.Run(() =>
            {
                ReadJsonConfig();
                var tables = configurations.Select(cfg => cfg.Table);
                EnsureTablesExist(tables);
                SubscribeToBrokers();
            }, cancellationToken.Token);
        }

        public async Task StartAsync()
        {
            messageQueue = Channel.CreateUnbounded<Tuple<string, string>>();
            ReadJsonConfig();
            var tables = configurations.Select(cfg => cfg.Table);
            EnsureTablesExist(tables);
            SubscribeToBrokers();
            await foreach ((string table, string message)
                in messageQueue.Reader.ReadAllAsync(cancellationToken.Token))
            {
                WriteToTable(table, message);
                await Task.Delay(1000);
            }
        }

        public void Stop()
        {
            cancellationToken.Cancel(false);
        }

        private void ReadJsonConfig()
        {
            DebugLog($"Loading configuration \"{configPath}\":");
            string json = File.ReadAllText(configPath);
            DebugLog(Regex.Replace(json,
                "(\"password\"\\s*:\\s*\")(.*?)(\")(,|\n|\r)",
                m => m.Groups[1].Value + new string('*', m.Groups[2].Length) + '"' + m.Groups[4].Value));
            var jsonOptions = new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                MaxDepth = 5
            };
            ServiceConfigurationJson configuration = JsonSerializer.Deserialize<ServiceConfigurationJson>(json, jsonOptions);
            DebugLog("Configuration loaded:");
            DebugLog(configuration.ToString());
            ConfigureBrokers(configuration);
        }

        private void ConfigureBrokers(ServiceConfigurationJson configuration)
        {
            var databases = new Dictionary<string, BaseConfigurationJson>(configuration.Databases.Length);
            foreach (var db in configuration.Databases)
            {
                if (!databases.ContainsKey(db.Name))
                {
                    if (db.Type != DatabaseType.SqlLite)
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
                            path = homeDir + "database.sqlite";
                        else if (path.StartsWith('.'))
                            path = Path.GetFullPath(homeDir + path);
                        if (!File.Exists(path))
                        {
                            DebugLog($"Creating database file \"{path}\"");
                            SQLiteConnection.CreateFile(path);
                        }
                        connectionString =
                            Regex.IsMatch(connectionString, "(Data Source\\s*=\\s*)(.*?)(;|$)") ?
                                Regex.Replace(db.ConnectionString,
                                "(Data Source\\s*=\\s*)(.*?)(;|$)",
                                $"$1{path}$3") :
                                $"Data Source={path};{connectionString}";
                        databases.Add(db.Name, new BaseConfigurationJson(db.Name, DatabaseType.SqlLite, connectionString));
                    }
                }
                else DebugLog($"Duplicate database names ({db.Name}) in the service configuration file. Some settings will be ignored!");
            }

            var brokers = new List<BrokerConfiguration>(configuration.Brokers.Length);
            foreach (var broker in configuration.Brokers)
            {
                var similar = brokers.FirstOrDefault(b => b.Equals(broker));
                if (similar == null)
                    brokers.Add(new BrokerConfiguration(databases, broker));
                else
                    similar.Merge(databases, broker);
            }
            this.brokers = brokers.ToArray();
        }

        private void EnsureTableExists(string table)
        {
            DebugLog($"Checking the existence of table \"{table}\"");
            using (var sqlCon = new SQLiteConnection(dbPath))
            {
                sqlCon.Open();
                string sql = "CREAT TABLE IF NOT EXISTS " + table + "("
                               + "Timestamp DATETIME DEFAULT (DATETIME(CURRENT_TIMESTAMP, 'localtime')) NOT NULL PRIMARY KEY,"
                               + "Message VARCHAR"
                           + ")";
                SQLiteCommand command = new SQLiteCommand(sql, sqlCon);
                command.ExecuteNonQuery();
            }
        }

        private void EnsureTablesExist(IEnumerable<string> tables)
        {
            using (var sqlCon = new SQLiteConnection("Data Source = " + dbPath + "; Version = 3;"))
            {
                sqlCon.Open();
                using (var transaction = sqlCon.BeginTransaction())
                {
                    using (var command = new SQLiteCommand(sqlCon))
                    {
                        command.Transaction = transaction;

                        var created = new HashSet<string>();
                        foreach (string table in tables)
                        {
                            if (created.Contains(table))
                                continue;
                            else
                                created.Add(table);
                            DebugLog($"Checking the existence of table \"{table}\"");
                            command.CommandText = "CREATE TABLE IF NOT EXISTS " + table + "("
                                                    + "Timestamp DATETIME DEFAULT (DATETIME(CURRENT_TIMESTAMP, 'localtime')) NOT NULL PRIMARY KEY,"
                                                    + "Message VARCHAR NOT NULL"
                                                + ");";
                            command.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                }
            }
        }

        private void WriteToTable(string table, string message)
        {
            lastSqlWrite = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            DebugLog($"Writing to table \"{table}\" the message: \"{message}\"");
            using (var sqlCon = new SQLiteConnection("Data Source = " + dbPath + "; Version = 3;"))
            {
                sqlCon.Open();
                string sql = "INSERT INTO " + table + "(Message) values ('" + message + "')";
                SQLiteCommand command = new SQLiteCommand(sql, sqlCon);
                command.ExecuteNonQuery();
            }
        }

        private Task WriteToTableTask(string table, string message)
        {
            return new Task(() =>
            {
                lock (sqlLock)
                {
                    while (DateTimeOffset.Now.ToUnixTimeMilliseconds() - lastSqlWrite < 1000)
                        Task.Delay(250);
                    lastSqlWrite = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                }
                DebugLog($"Writing to table \"{table}\" the message: \"{message}\"");
                using (var sqlCon = new SQLiteConnection("Data Source = " + dbPath + "; Version = 3;"))
                {
                    sqlCon.Open();
                    string sql = "INSERT INTO " + table + "(Message) values ('" + message + "')";
                    SQLiteCommand command = new SQLiteCommand(sql, sqlCon);
                    command.ExecuteNonQuery();
                }
            });
        }

        private void SubscribeToBrokers()
        {
            var connections =
                configurations
                .OrderBy(cfg => $"{cfg.Host}:{cfg.Port}[{cfg.User},{cfg.Password}]")
                .GroupBy(cfg => $"{cfg.Host}:{cfg.Port}[{cfg.User},{cfg.Password}]");
            foreach (var congroup in connections)
            {
                _ = Task.Run(async () =>
                {
                    var TopicTable = new Dictionary<string, string>(congroup.Count());
                    foreach (var cfg in congroup) TopicTable.Add(cfg.Topic, cfg.Table);
                    var connection = congroup.First();

                    var factory = new MqttFactory();
                    var mqttClient = factory.CreateMqttClient();
                    var options = new MqttClientOptionsBuilder()
                        .WithClientId(clientId)
                        .WithTcpServer(connection.Host, connection.Port)
                        .WithCredentials(connection.User, connection.Password)
                        .Build();

                    bool connectionFailed = false;
                    mqttClient.UseDisconnectedHandler(async e =>
                    {
                        DebugLog($"Disconnected from \"{connection.Host}\"! Reason: \"{e.Reason}\"");
                        await Task.Delay(connectionFailed ? TimeSpan.FromMinutes(15) : TimeSpan.FromSeconds(10));
                        connectionFailed = true;

                        try
                        {
                            DebugLog($"Attempting to recconnect to \"{connection.Host}\"");
                            await mqttClient.ReconnectAsync(cancellationToken.Token);
                            connectionFailed = false;
                            DebugLog("Reconnected!");
                        }
                        catch
                        {
                            DebugLog("Reconnection Failed!");
                            cancellationToken.Cancel();
                            await Task.Delay(1000);
                            Environment.Exit(-1);
                        }
                    });

                    mqttClient.UseConnectedHandler(async e =>
                    {
                        DebugLog($"Connection established with \"{connection.Host}\"");
                        foreach ((string topic, string table) in TopicTable)
                        {
                            DebugLog($"Subscribing to \"{topic}\" topic");
                            await mqttClient.SubscribeAsync(
                                new MqttClientSubscribeOptionsBuilder()
                                    .WithTopicFilter(topic, qualityOfServiceLevel: MqttQualityOfServiceLevel.ExactlyOnce)
                                    .Build()
                            );
                        }
                    });

                    if (messageQueue == null)
                    {
                        mqttClient.UseApplicationMessageReceivedHandler(e =>
                        {
                            string topic = e.ApplicationMessage.Topic;
                            string message = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                            DebugLog($"Message from \"{topic}\" topic recieved: \"{message}\"");
                            WriteToTableTask(TopicTable[topic], message).Start();
                        });
                    }
                    else
                    {
                        mqttClient.UseApplicationMessageReceivedHandler(e =>
                        {
                            string topic = e.ApplicationMessage.Topic;
                            string message = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                            DebugLog($"Message from \"{topic}\" topic recieved: \"{message}\"");
                            messageQueue.Writer.TryWrite(Tuple.Create(TopicTable[topic], message));
                        });
                    }

                    DebugLog($"Connecting to \"{connection.Host}\"");
                    await mqttClient.ConnectAsync(options, cancellationToken.Token);
                }, cancellationToken.Token);
            }
        }


#if !LOG
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Critical Code Smell", "S1186:Methods should not be empty")]
#endif
        private void DebugLog(string message)
        {
#if LOG
            Console.WriteLine(message + Environment.NewLine);
            if ((logWrites = logWrites > 1000 ? 0 : logWrites + 1) > 1000
                && (new FileInfo(logPath) is var file) && file.Length > 100_000_000)
            {
                byte[] buffer;
                using (BinaryReader reader = new BinaryReader(file.Open(FileMode.Open)))
                {
                    reader.BaseStream.Position = file.Length - 1_000_000;
                    buffer = reader.ReadBytes(1_000_000);
                }
                using (BinaryWriter writer = new BinaryWriter(file.Open(FileMode.Truncate)))
                {
                    writer.BaseStream.Position = 0;
                    writer.Write(buffer);
                    writer.Write(Encoding.UTF8.GetBytes(message + Environment.NewLine));
                }
            }
            else
            {
            	File.AppendAllText(logPath, message + Environment.NewLine);
            }
#endif
        }

#if !LOG
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Critical Code Smell", "S1186:Methods should not be empty")]
#endif
        private void DebugLog(string message, ConsoleColor foregroundColor, ConsoleColor backgroundColor)
        {
#if LOG
            Console.ForegroundColor = foregroundColor;
            Console.BackgroundColor = backgroundColor;
            DebugLog(message);
            Console.ResetColor();
#endif
        }

#if !LOG
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Critical Code Smell", "S1186:Methods should not be empty")]
#endif
        private void DebugLog(object messageObj)
        {
#if LOG
            DebugLog(messageObj.ToString());
#endif
        }

        private readonly CancellationTokenSource cancellationToken = new CancellationTokenSource();

        private readonly string clientId;
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S1450:Private fields only used as local variables in methods should become local variables")]
        private readonly string homeDir;
        private readonly string configPath;

#if LOG
        private readonly string logPath;
        private int logWrites = 0;
#endif

        private BrokerConfiguration[] brokers;

        private readonly static object sqlLock = new object();
        private long lastSqlWrite = 0;

        private Channel<Tuple<string, string>> messageQueue = null;
    }
}
