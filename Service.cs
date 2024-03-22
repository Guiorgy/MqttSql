/*
    This file is part of MqttSql (Copyright © 2024  Guiorgy).
    MqttSql is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
    MqttSql is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
    You should have received a copy of the GNU General Public License along with Foobar. If not, see <https://www.gnu.org/licenses/>.
*/

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
using System.Net.NetworkInformation;
using System.Text;
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
            this.homeDir = homeDir == null ? Environment.GetEnvironmentVariable("MqttSqlHome") : homeDir;
            this.dbPath = this.homeDir + (this.homeDir.EndsWith(dirSep) ? "" : dirSep) + "database.sqlite";
            this.configPath = this.homeDir + (this.homeDir.EndsWith(dirSep) ? "" : dirSep) + "config.json";
            DebugLog($"Home: \"{this.homeDir}\"");
            DebugLog($"Database: \"{this.dbPath}\"");
            DebugLog($"Configuration: \"{this.configPath}\"");
            DebugLog($"ClientId: \"{this.clientId}\"");

#if LOG
            this.logPath = this.homeDir + (this.homeDir.EndsWith(dirSep) ? "" : dirSep) + "logs.txt";
            DebugLog($"Logs: \"{this.logPath}\"");
#endif
        }

        public void Start()
        {
            Task.Run(() =>
            {
                EnsureDbExists();
                ReadJsonConfig();
                var tables = configurations.Select(cfg => cfg.Table);
                EnsureTablesExist(tables);
                SubscribeToBrokers();
            }, cancellationToken.Token);
        }

        public async Task StartAsync()
        {
            messageQueue = Channel.CreateUnbounded<Tuple<string, string>>();
            EnsureDbExists();
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
            	Console.WriteLine(message + Environment.NewLine);
            	File.AppendAllText(logPath, message + Environment.NewLine);
            }
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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S1450:Private fields only used as local variables in methods should become local variables")]
        private readonly string homeDir;
        private readonly string dbPath;
        private readonly string configPath;
#if LOG
        private readonly string logPath;
        private int logWrites = 0;
#endif

        private List<ServiceConfiguration> configurations;
        private readonly string clientId;
        private readonly CancellationTokenSource cancellationToken = new CancellationTokenSource();

        private readonly static object sqlLock = new object();
        private long lastSqlWrite = 0;

        private Channel<Tuple<string, string>> messageQueue = null;
    }
}
