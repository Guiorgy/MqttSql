#if !LOG && DEBUG
#define LOG
#endif

using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using MQTTnet.Client.Subscribing;
using MQTTnet.Protocol;
using MqttSql.Configurations;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using static MqttSql.Configurations.SubscriptionConfiguration;
using static MqttSql.Configurations.SubscriptionConfiguration.BaseConfiguration;

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

#if DEBUG
            this.homeDir = homeDir ?? Directory.GetCurrentDirectory();
#else
            this.homeDir = homeDir ?? Environment.GetEnvironmentVariable("MqttSqlHome");
#endif
            if (!this.homeDir.EndsWith(dirSep)) this.homeDir += dirSep;

            this.configPath = this.homeDir + "config.json";

#if LOG
            this.logPath = this.homeDir + "logs.txt";
            DebugLog($"Logs: \"{this.logPath}\"");
            DebugLog($"ClientId: \"{this.clientId}\"");
            DebugLog($"Home: \"{this.homeDir}\"");
            DebugLog($"Configuration: \"{this.configPath}\"");
#endif
        }

        private void LoadAndStartService()
        {
            LoadConfiguration();
            var bases = brokers.SelectMany(broker => broker.Clients.SelectMany(client => client.Subscriptions.SelectMany(sub => sub.Databases))).Distinct();
            var sqliteBases = bases.Where(db => db.Type == DatabaseType.SQLite);
            lastSqliteWrite = new Dictionary<string, long>(sqliteBases.Count());
            foreach (var sqlite in sqliteBases)
            {
                lastSqliteWrite.Add(sqlite.ConnectionString, 0);
                EnsureSqliteTablesExist(sqlite);
            }
            foreach (var general in bases.Where(db => db.Type == DatabaseType.GeneralSql))
                EnsureSqlTablesExist(general);
            SubscribeToBrokers();
        }

        private async void ConfigurationFileChanged(object sender, FileSystemEventArgs e)
        {
            configFileChangeWatcher = null;
            DebugLog("Configuration file changed.");
            while (!serviceLoaded)
                await Task.Delay(1000, cancellationToken.Token);
            DebugLog($"Disconnecting {mqttClients.Count} clients.");
            foreach (var client in mqttClients)
            {
                await client.DisconnectAsync();
                client.Dispose();
            }
            mqttClients = null;
            await Task.Delay(10000, cancellationToken.Token);
            serviceLoaded = false;
            DebugLog("Loading new configuration.");
            LoadAndStartService();
        }

        public void Start()
        {
            Task.Run(() => LoadAndStartService(), cancellationToken.Token);
        }

        public async Task StartAsync()
        {
            messageQueue = Channel.CreateUnbounded<(List<BaseConfiguration>, string)>();
            LoadAndStartService();
            await foreach ((List<BaseConfiguration> databases, string message)
                in messageQueue.Reader.ReadAllAsync(cancellationToken.Token))
            {
                foreach (var db in databases)
                    WriteToDatabase(db, message);
                await Task.Delay(1000, cancellationToken.Token);
            }
        }

        public void Stop()
        {
            cancellationToken.Cancel(false);
        }

        private void LoadConfiguration()
        {
            string GetSQLiteDbPath(string path)
            {
                if (string.IsNullOrWhiteSpace(path))
                    path = homeDir + "database.sqlite";
                else if (path.StartsWith('.'))
                    path = Path.GetFullPath(homeDir + path);
                if (!File.Exists(path))
                {
                    DebugLog($"Creating database file \"{path}\"");
                    SQLiteConnection.CreateFile(path);
                }
                return path;
            }
            
#if DEBUG
            this.brokers = ConfigurationLoader.LoadBrokersFromJson(configPath, GetSQLiteDbPath, DebugLog);
#else
            this.brokers = ConfigurationLoader.LoadBrokersFromJson(configPath, GetSQLiteDbPath);
#endif

            if (configFileChangeWatcher != null)
            {
                configFileChangeWatcher.Dispose();
                configFileChangeWatcher = null;
            }
            configFileChangeWatcher = new(configPath);
            configFileChangeWatcher.NotifyFilter = NotifyFilters.LastWrite;
            configFileChangeWatcher.Changed += ConfigurationFileChanged;
        }

        private void EnsureSqliteTablesExist(BaseConfiguration sqliteDb)
        {
            using (var sqlCon = new SQLiteConnection(sqliteDb.ConnectionString))
            {
                sqlCon.Open();
                using (var transaction = sqlCon.BeginTransaction())
                {
                    using (var command = new SQLiteCommand(sqlCon))
                    {
                        command.Transaction = transaction;

                        var created = new HashSet<string>();
                        foreach (string table in sqliteDb.Tables)
                        {
                            if (created.Contains(table))
                                continue;
                            else
                                created.Add(table);
                            DebugLog($"Checking the existence of table \"{table}\"");
                            command.CommandText = "CREATE TABLE IF NOT EXISTS " + table + "(" +
                                                      "Timestamp DATETIME DEFAULT (DATETIME(CURRENT_TIMESTAMP, 'localtime')) NOT NULL PRIMARY KEY," +
                                                      "Message VARCHAR NOT NULL" +
                                                  ");";
                            command.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                }
            }
        }

        private void EnsureSqlTablesExist(BaseConfiguration db)
        {
            using (var sqlCon = new SQLiteConnection(db.ConnectionString))
            {
                sqlCon.Open();
                using (var transaction = sqlCon.BeginTransaction())
                {
                    using (var command = new SQLiteCommand(sqlCon))
                    {
                        command.Transaction = transaction;

                        var created = new HashSet<string>();
                        foreach (string table in db.Tables)
                        {
                            if (created.Contains(table))
                                continue;
                            else
                                created.Add(table);
                            DebugLog($"Checking the existence of table \"{table}\"");
                            command.CommandText = "IF OBJECT_ID('" + table + "', 'U') IS NULL" +
                                                  "BEGIN" +
                                                      "CREATE TABLE " + table + "(" +
                                                          "Timestamp DATETIME DEFAULT (DATETIME(CURRENT_TIMESTAMP, 'localtime')) NOT NULL PRIMARY KEY," +
                                                          "Message VARCHAR NOT NULL" +
                                                      ")" +
                                                  "END;";
                            command.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                }
            }
        }

        private void WriteToDatabase(BaseConfiguration db, string message)
        {
            if (db.Type == DatabaseType.SQLite)
                lastSqliteWrite[db.ConnectionString] = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            DebugLog($"Writing to the database with connection string \"{db.ConnectionString}\" the message: \"{message}\"");
            using (var sqlCon = new SQLiteConnection(db.ConnectionString))
            {
                sqlCon.Open();
                using (var transaction = sqlCon.BeginTransaction())
                {
                    using (var command = new SQLiteCommand(sqlCon))
                    {
                        command.Transaction = transaction;

                        foreach (string table in db.Tables)
                        {
                            DebugLog($"Writing to the \"{table}\" table");
                            command.CommandText = "INSERT INTO " + table + "(Message) values ('" + message + "')";
                            command.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                }
            }
        }

        private Task WriteToDatabaseTask(BaseConfiguration db, string message)
        {
            return new Task(() =>
            {
                if (db.Type == DatabaseType.SQLite)
                {
                    lock (sqlLock)
                    {
                        while (!cancellationToken.IsCancellationRequested &&
                            DateTimeOffset.Now.ToUnixTimeMilliseconds() - lastSqliteWrite[db.ConnectionString] < 1000)
                        {
                            Task.Delay(250, cancellationToken.Token);
                        }
                        lastSqliteWrite[db.ConnectionString] = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                    }
                }
                if (cancellationToken.IsCancellationRequested) return;
                DebugLog($"Writing to the database with connection string \"{db.ConnectionString}\" the message: \"{message}\"");
                using (var sqlCon = new SQLiteConnection(db.ConnectionString))
                {
                    sqlCon.Open();
                    using (var transaction = sqlCon.BeginTransaction())
                    {
                        using (var command = new SQLiteCommand(sqlCon))
                        {
                            if (cancellationToken.IsCancellationRequested) return;
                            command.Transaction = transaction;

                            foreach (string table in db.Tables)
                            {
                                DebugLog($"Writing to the \"{table}\" table");
                                command.CommandText = "INSERT INTO " + table + "(Message) values ('" + message + "')";
                                command.ExecuteNonQuery();
                            }

                            transaction.Commit();
                        }
                    }
                }
            }, cancellationToken.Token);
        }

        private void SubscribeToBrokers()
        {
            List<Task> tasks = new(brokers.Length);

            mqttClients = new(brokers.Sum(broker => broker.Clients.Count));
            foreach (var broker in brokers)
            {
                if (cancellationToken.IsCancellationRequested || configFileChangeWatcher == null) break;
                tasks.Add(Task.Run(async () =>
                {
                    foreach (var (client, index) in broker.Clients.Select((client, index) => (client, index)))
                    {
                        if (cancellationToken.IsCancellationRequested || configFileChangeWatcher == null) break;

                        var factory = new MqttFactory();
                        var mqttClient = factory.CreateMqttClient();
                        var options = new MqttClientOptionsBuilder()
                            .WithClientId(clientId + (broker.Clients.Count != 1 ? $"-{index}" : ""))
                            .WithTcpServer(broker.Host, broker.Port)
                            .WithCredentials(client.User, client.Password)
                            .Build();

                        bool connectionFailed = false;
                        mqttClient.UseDisconnectedHandler(async e =>
                        {
                            DebugLog($"Disconnected from \"{broker.Host}\"! Reason: \"{e.Reason}\"");
                            if (cancellationToken.IsCancellationRequested || configFileChangeWatcher == null) return;
                            await Task.Delay(connectionFailed ? TimeSpan.FromMinutes(15) : TimeSpan.FromSeconds(10), cancellationToken.Token);
                            if (cancellationToken.IsCancellationRequested || configFileChangeWatcher == null) return;
                            connectionFailed = true;

                            try
                            {
                                DebugLog($"Attempting to recconnect to \"{broker.Host}\"");
                                await mqttClient.ReconnectAsync(cancellationToken.Token);
                                connectionFailed = false;
                                DebugLog("Reconnected!");
                            }
                            catch
                            {
                                DebugLog("Reconnection Failed!");
                                cancellationToken.Cancel();
#pragma warning disable PH_P007 // Unused Cancellation Token
                                await Task.Delay(1000);
#pragma warning restore PH_P007 // Unused Cancellation Token
                                Environment.Exit(-1);
                            }
                        });

                        mqttClient.UseConnectedHandler(async _ =>
                        {
                            DebugLog($"Connection established with \"{broker.Host}\"");
                            foreach (var sub in client.Subscriptions)
                            {
                                DebugLog($"Subscribing to \"{sub.Topic}\" topic");
                                await mqttClient.SubscribeAsync(
                                    new MqttClientSubscribeOptionsBuilder()
                                        .WithTopicFilter(sub.Topic, qualityOfServiceLevel: (MqttQualityOfServiceLevel)(sub.QOS))
                                        .Build()
                                );
                            }
                        });

                        (List<BaseConfiguration>, string) getMessageAndDb(MqttApplicationMessageReceivedEventArgs e)
                        {
                            string topic = e.ApplicationMessage.Topic;
                            string message = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                            DebugLog($"Message from \"{topic}\" topic recieved: \"{message}\"");
                            return (client.Subscriptions.First(sub => sub.Topic.Equals(topic)).Databases, message);
                        }

                        if (messageQueue == null)
                        {
                            mqttClient.UseApplicationMessageReceivedHandler(e =>
                            {
                                (List<BaseConfiguration> bases, string message) = getMessageAndDb(e);
                                foreach (var db in bases)
                                    WriteToDatabaseTask(db, message).Start();
                            });
                        }
                        else
                        {
                            mqttClient.UseApplicationMessageReceivedHandler(e => messageQueue.Writer.TryWrite(getMessageAndDb(e)));
                        }

                        DebugLog($"Connecting to \"{broker.Host}\"");
                        await mqttClient.ConnectAsync(options, cancellationToken.Token);
                        mqttClients.Add(mqttClient);

                        await Task.Delay(1000, cancellationToken.Token);
                    }
                }, cancellationToken.Token));
            }

            Task.WhenAll(tasks).ContinueWith(_ => serviceLoaded = true, cancellationToken.Token);
        }

#if !LOG
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Critical Code Smell", "S1186:Methods should not be empty")]
#endif
        private void DebugLog(string message)
        {
#if LOG
            try
            {
                Console.WriteLine(message + Environment.NewLine);
                if ((logWrites = logWrites > 1000 ? 0 : logWrites + 1) > 1000
                    && (new FileInfo(logPath) is var file) && file.Length > 100_000_000)
                {
                    byte[] buffer;
                    using (BinaryReader reader = new(file.Open(FileMode.Open)))
                    {
                        reader.BaseStream.Position = file.Length - 1_000_000;
                        buffer = reader.ReadBytes(1_000_000);
                    }
                    using (BinaryWriter writer = new(file.Open(FileMode.Truncate)))
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
            } catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.BackgroundColor = ConsoleColor.White;
                Console.WriteLine(ex.ToString());
                Console.ResetColor();
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

        private readonly CancellationTokenSource cancellationToken = new();

        private readonly string clientId;
        private readonly string homeDir;
        private readonly string configPath;

#if LOG
        private readonly string logPath;
        private int logWrites;
#endif

        private FileSystemWatcher configFileChangeWatcher;
        private bool serviceLoaded;

        private List<IMqttClient> mqttClients;
        private BrokerConfiguration[] brokers;

        private static readonly object sqlLock = new();
        private Dictionary<string, long> lastSqliteWrite;

        private Channel<(List<BaseConfiguration>, string)> messageQueue;
    }
}
