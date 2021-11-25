#if !LOG && DEBUG
#define LOG
#endif

using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using MQTTnet.Client.Subscribing;
using MQTTnet.Protocol;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MqttSql
{
    public class Service
    {
        public Service(string homeDir = null)
        {
            this.homeDir = homeDir == null ? Environment.GetEnvironmentVariable("MqttSqlHome") : homeDir;
            this.dbPath = this.homeDir + (this.homeDir.EndsWith("\\") ? "" : "\\") + "database.sqlite";
            this.configPath = this.homeDir + (this.homeDir.EndsWith("\\") ? "" : "\\") + "config.json";
            this.logPath = this.homeDir + (this.homeDir.EndsWith("\\") ? "" : "\\") + "logs.txt";
            DebugLog($"Home: \"{this.homeDir}\"");
            DebugLog($"Database: \"{this.dbPath}\"");
            DebugLog($"Configuration: \"{this.configPath}\"");
            DebugLog($"Logs: \"{this.logPath}\"");
        }

        public void Start()
        {
            Task.Run(() =>
            {
                EnsureDbExists();
                ReadJsonConfig();
                var tables = configurations.Select(cfg => cfg.Table);
                EnsureTablesExist(tables);
                SubscribeToBrokers(configurations);
            }, cancellationToken.Token);
        }

        public void Stop()
        {
            cancellationToken.Cancel(false);
        }

        private void EnsureDbExists()
        {
            if (!File.Exists(dbPath))
            {
                DebugLog($"Creating database file \"{dbPath}\"");
                SQLiteConnection.CreateFile(dbPath);
            }
        }

        private void ReadJsonConfig()
        {
            DebugLog($"Loading configuration \"{configPath}\":");
            string json = File.ReadAllText(configPath);
            DebugLog(Regex.Replace(json,
                "(\"password\"\\s*:\\s*\")(.*?)(\")(,|\n|\r)",
                m => m.Groups[1].Value + new string('*', m.Groups[2].Length) + '"' + m.Groups[4].Value));
            configurations = JsonConvert.DeserializeObject<List<ServiceConfiguration>>(json);
            DebugLog("Configuration loaded:");
            foreach (var cfg in configurations)
                DebugLog(cfg.ToSafeString());
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
                string sql = "INSERT INTO " + table + "(Message) values (\"" + message + "\")";
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
                    string sql = "INSERT INTO " + table + "(Message) values (\"" + message + "\")";
                    SQLiteCommand command = new SQLiteCommand(sql, sqlCon);
                    command.ExecuteNonQuery();
                }
            });
        }

        private void SubscribeToBrokers(IEnumerable<ServiceConfiguration> configurations)
        {
            var connections =
                configurations
                .OrderBy(cfg => $"{cfg.Host}:{cfg.Port}[{cfg.User},{cfg.Password}]")
                .GroupBy(cfg => $"{cfg.Host}:{cfg.Port}[{cfg.User},{cfg.Password}]");
            foreach (var con in connections)
            {
                Task.Run(async () =>
                {
                    var cfg = con.First();
                    var factory = new MqttFactory();
                    var mqttClient = factory.CreateMqttClient();
                    var options = new MqttClientOptionsBuilder()
                        .WithClientId(clientId)
                        .WithTcpServer(cfg.Host, cfg.Port)
                        .WithCredentials(cfg.User, cfg.Password)
                        .Build();

                    bool connectionFailed = false;
                    mqttClient.UseDisconnectedHandler(async e =>
                    {
                        DebugLog($"Disconnected from \"{cfg.Host}\"! Reason: \"{e.Reason}\"");
                        await Task.Delay(connectionFailed ? TimeSpan.FromMinutes(15) : TimeSpan.FromSeconds(10));
                        connectionFailed = true;

                        try
                        {
                            DebugLog($"Attempting to recconnect to \"{cfg.Host}\"");
                            await mqttClient.ConnectAsync(options, cancellationToken.Token);
                            connectionFailed = false;
                            DebugLog("Reconnected!");
                        }
                        catch
                        {
                            DebugLog("Reconnection Failed!");
                        }
                    });

                    var TopicTable = new Dictionary<string, string>(con.Count());
                    mqttClient.UseConnectedHandler(async e =>
                    {
                        DebugLog($"Connection established with \"{cfg.Host}\"");
                        foreach (var cfg2 in con)
                        {
                            DebugLog($"Subscribing to \"{cfg2.Topic}\" topic");
                            TopicTable.Add(cfg2.Topic, cfg2.Table);
                            await mqttClient.SubscribeAsync(
                                new MqttClientSubscribeOptionsBuilder()
                                    .WithTopicFilter(cfg2.Topic, qualityOfServiceLevel: MqttQualityOfServiceLevel.ExactlyOnce)
                                    .Build()
                            );
                        }
                    });

                    mqttClient.UseApplicationMessageReceivedHandler(e =>
                    {
                        string topic = e.ApplicationMessage.Topic;
                        string message = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                        DebugLog($"Message from \"{topic}\" topic recieved: \"{message}\"");
                        WriteToTableTask(TopicTable[topic], message).Start();
                    });

                    DebugLog($"Connecting to \"{cfg.Host}\"");
                    await mqttClient.ConnectAsync(options, cancellationToken.Token);
                }, cancellationToken.Token);
            }
        }

        private void DebugLog(string message)
        {
#if LOG
            File.AppendAllText(logPath, message + Environment.NewLine);
#endif
        }

        private void DebugLog(object messageObj)
        {
#if LOG
            DebugLog(messageObj.ToString());
#endif
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S1450:Private fields only used as local variables in methods should become local variables")]
        private readonly string homeDir;
        private readonly string dbPath;
        private readonly string configPath;
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S1450:Private fields only used as local variables in methods should become local variables")]
        private readonly string logPath;

        private List<ServiceConfiguration> configurations;
        private readonly string clientId = Environment.MachineName.Replace(' ', '.');
        private readonly CancellationTokenSource cancellationToken = new CancellationTokenSource();

        private readonly static object sqlLock = new object();
        private long lastSqlWrite = 0;
    }
}
