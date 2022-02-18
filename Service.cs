#if !LOG && DEBUG
#define LOG
#endif

#if DEBUG
using System.Collections.Concurrent;
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
using static MqttSql.Configurations.BaseConfiguration;

namespace MqttSql
{
    public class Service
    {
        public Service(string? homeDir = null, string dirSep = "\\")
        {
            string macAddress = NetworkInterface
                .GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Select(nic => nic.GetPhysicalAddress().ToString())
                .FirstOrDefault() ?? Guid.NewGuid().ToString();
            this.clientId = $"{macAddress}-{Environment.MachineName}-{Environment.UserName}".Replace(' ', '.');

#if DEBUG
            this.homeDir = homeDir ?? Directory.GetCurrentDirectory();
#else
            this.homeDir = homeDir ?? Environment.GetEnvironmentVariable("MqttSqlHome") ?? Directory.GetCurrentDirectory();
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
            var bases = brokers!.SelectMany(broker => broker.Clients.SelectMany(client => client.Subscriptions.SelectMany(sub => sub.Databases))).DistinctMerge();
            var sqliteBases = bases.Where(db => db.Type == DatabaseType.SQLite).ToList();
            sqliteMessageQueues = new(sqliteBases.Count);
            foreach (var sqlite in sqliteBases)
            {
                sqliteMessageQueues.Add(sqlite.ConnectionString, Channel.CreateUnbounded<(BaseConfiguration db, DateTime timestamp, string message)>(channelOptions));
                EnsureSqliteTablesExist(sqlite);
            }
            foreach (var general in bases.Where(db => db.Type == DatabaseType.GeneralSql).ToList())
                EnsureSqlTablesExist(general);
            SubscribeToBrokers();
        }

        private async void ConfigurationFileChanged(object sender, FileSystemEventArgs e)
        {
            configFileChangeWatcher = null;
            DebugLog("Configuration file changed.");
            while (!serviceLoaded)
                await Task.Delay(1000, cancellationToken.Token);
            DebugLog($"Disconnecting {mqttClients!.Count} clients.");
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
            Task.Run(() => StartAsync(), cancellationToken.Token);
        }

        public async Task StartAsync()
        {
            LoadAndStartService();

            foreach (var queue in sqliteMessageQueues!.Values)
            {
                _ = Task.Run(async () =>
                {
                    await foreach (
                        List<(BaseConfiguration db, DateTime timestamp, string message)> entries
                            in queue.Reader.ReadBatchesAsync(cancellationToken.Token))
                    {
                        if (entries.Count == 1)
                        {
                            var (db, timestamp, message) = entries[0];
                            await WriteToSQLiteDatabaseAsync(db, message, timestamp);
                        }
                        else
                        {
                            await WriteToSQLiteDatabaseAsync(entries);
                        }
                        await Task.Delay(1000, cancellationToken.Token);
                    }
                }, cancellationToken.Token);
            }

            await foreach ((BaseConfiguration database, string message)
                in messageQueue.Reader.ReadAllAsync(cancellationToken.Token))
            {
                await WriteToSqlDatabaseAsync(database, message);
                await Task.Delay(50);
            }
        }

        public void Stop()
        {
            cancellationToken.Cancel(false);
            DebugLog("Stopping", true);
        }

        private void LoadConfiguration()
        {
            string GetSQLiteDbPath(string? path)
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
            configFileChangeWatcher = new(homeDir);
            configFileChangeWatcher.Filter = "config.json";
            configFileChangeWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite;
            configFileChangeWatcher.Changed += ConfigurationFileChanged;
            configFileChangeWatcher.EnableRaisingEvents = true;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0063:Use simple 'using' statement", Justification = "Preferred.")]
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
                                                      "id INTEGER NOT NULL PRIMARY KEY," +
                                                      "Timestamp DATETIME DEFAULT (DATETIME(CURRENT_TIMESTAMP, 'localtime')) NOT NULL," +
                                                      "Message VARCHAR NOT NULL" +
                                                  ");";
                            command.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                }
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0063:Use simple 'using' statement", Justification = "Preferred.")]
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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0063:Use simple 'using' statement", Justification = "Preferred.")]
        private async Task WriteToSQLiteDatabaseAsync(BaseConfiguration db, string message, DateTime? timestamp = null)
        {
            await Task.Run(() =>
            {
                try
                {
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
                                    command.CommandText =
                                    timestamp == null
                                        ? "INSERT INTO " + table + "(Message) values ('" + message + "')"
                                        : "INSERT INTO " + table + "(Timestamp, Message) values ('" + ((DateTime)timestamp).ToString("yyyy-MM-dd HH:mm:ss") + "', '" + message + "')";
                                    command.ExecuteNonQuery();
                                }

                                transaction.Commit();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    DebugLog(ex);
                }
            }, cancellationToken.Token);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0063:Use simple 'using' statement", Justification = "Preferred.")]
        private async Task WriteToSQLiteDatabaseAsync(List<(BaseConfiguration db, DateTime timestamp, string message)> entries)
        {
            await Task.Run(() =>
            {
                try
                {
                    if (cancellationToken.IsCancellationRequested) return;
                    DebugLog($"Writing to the database with connection string \"{entries[0].db.ConnectionString}\" {entries.Count} messages");
                    using (var sqlCon = new SQLiteConnection(entries[0].db.ConnectionString))
                    {
                        sqlCon.Open();
                        using (var transaction = sqlCon.BeginTransaction())
                        {
                            using (var command = new SQLiteCommand(sqlCon))
                            {
                                foreach ((BaseConfiguration db, DateTime? timestamp, string message) in entries)
                                {
                                    if (cancellationToken.IsCancellationRequested) return;
                                    command.Transaction = transaction;

                                    foreach (string table in db.Tables)
                                    {
                                        DebugLog($"Writing to the \"{table}\" table the message: \"{message}\"");
                                        command.CommandText = "INSERT INTO " + table + "(Timestamp, Message) values ('" + ((DateTime)timestamp).ToString("yyyy-MM-dd HH:mm:ss") + "', '" + message + "')";
                                        command.ExecuteNonQuery();
                                    }
                                }

                                transaction.Commit();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    DebugLog(ex);
                }
            }, cancellationToken.Token);
        }

        private async Task WriteToSqlDatabaseAsync(BaseConfiguration db, string message)
        {
            await Task.Run(() =>
            {
                throw new NotImplementedException(); // TODO!
            });
        }

        private void SubscribeToBrokers()
        {
            List<Task> tasks = new(brokers!.Length);

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
                            .WithKeepAlivePeriod(TimeSpan.FromHours(1))
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

                        (List<BaseConfiguration>, string) getMessageAndDbs(MqttApplicationMessageReceivedEventArgs e)
                        {
                            string topic = e.ApplicationMessage.Topic;
                            string message = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                            DebugLog($"Message from \"{topic}\" topic recieved: \"{message}\"");
                            return (client.Subscriptions.First(sub => sub.Topic.Equals(topic)).Databases, message);
                        }

                        mqttClient.UseApplicationMessageReceivedHandler(e =>
                        {
                            (List<BaseConfiguration> bases, string message) = getMessageAndDbs(e);
                            foreach (var db in bases)
                                if (db.Type == DatabaseType.SQLite)
                                    Task.Run(() => sqliteMessageQueues![db.ConnectionString].Writer.WriteAsync((db, DateTime.Now, message), cancellationToken.Token));
                                else
                                    Task.Run(() => messageQueue.Writer.WriteAsync((db, message), cancellationToken.Token));
                        });

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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "It can't be static when logging is enabled.")]
#endif
        private void DebugLog(string message, bool flush = false)
        {
#if LOG
            try
            {
                Console.WriteLine(message);
                logBuffer.Enqueue(message);

                if (flush || logBuffer.Count >= logBufferSize)
                {
                    string[] logBufferArray = logBuffer.ToArray();
                    logBuffer.Clear();
                    string stringBuffer = string.Join(Environment.NewLine, logBufferArray);

                    if (logBuffer.Count >= logBufferSize)
                    {
                        if ((new FileInfo(logPath) is var file) && file.Length > 100_000_000)
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
                                writer.Write(Encoding.UTF8.GetBytes(stringBuffer));
                            }
                            flush = false;
                        }
                        else
                        {
                            flush = true;
                        }
                    }

                    if (flush)
                        File.AppendAllText(logPath, stringBuffer);
                }
            }
            catch (Exception ex)
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "It can't be static when logging is enabled.")]
#endif
        private void DebugLog(Exception exception)
        {
#if LOG
            Console.ForegroundColor = ConsoleColor.Red;
            Console.BackgroundColor = ConsoleColor.Yellow;
            DebugLog(exception.Message);
            if (exception.StackTrace != null)
                DebugLog(exception.StackTrace);
            Console.ResetColor();
#endif
        }

#if !LOG
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Critical Code Smell", "S1186:Methods should not be empty")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "It can't be static when logging is enabled.")]
#endif
        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "May be used in the future.")]
        private void DebugLog(string message, ConsoleColor foregroundColor, ConsoleColor backgroundColor)
        {
#if LOG
            Console.ForegroundColor = foregroundColor;
            Console.BackgroundColor = backgroundColor;
            DebugLog(message);
            Console.ResetColor();
            Console.WriteLine();
#endif
        }

#if !LOG
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Critical Code Smell", "S1186:Methods should not be empty")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "It can't be static when logging is enabled.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used in the Debug configuration.")]
#endif
        private void DebugLog(object messageObj)
        {
#if LOG
            DebugLog(messageObj.ToString() ?? "");
#endif
        }

        private readonly CancellationTokenSource cancellationToken = new();

        private readonly string clientId;
        private readonly string homeDir;
        private readonly string configPath;

#if LOG
        private readonly string logPath;
        private const int logBufferSize = 1000;
        private readonly ConcurrentQueue<string> logBuffer = new();
#endif

        private FileSystemWatcher? configFileChangeWatcher;
        private bool serviceLoaded;

        private List<IMqttClient>? mqttClients;
        private BrokerConfiguration[]? brokers;

        private static readonly UnboundedChannelOptions channelOptions = new();
        private Dictionary<string, Channel<(BaseConfiguration db, DateTime timestamp, string message)>>? sqliteMessageQueues;
        private readonly Channel<(BaseConfiguration, string)> messageQueue =
            Channel.CreateUnbounded<(BaseConfiguration, string)>(channelOptions);

        static Service()
        {
            channelOptions.SingleReader = true;
        }
    }
}
