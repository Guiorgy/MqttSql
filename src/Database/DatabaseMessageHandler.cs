using MqttSql.Configuration;
using MqttSql.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace MqttSql.Database;

public sealed class DatabaseMessageHandler : IDisposable
{
    public record struct DatabaseMessage(
        DatabaseConfiguration Database,
        DateTime Timestamp,
        string Message
    )
    {
        public readonly void Deconstruct(out (TableConfiguration[] tables, DateTime timestamp, string message) entry)
        {
            entry = (Database.Tables, Timestamp, Message);
        }
    }

    private static readonly UnboundedChannelOptions channelOptions = new();
    private readonly Dictionary<DatabaseType, IDatabaseManager> databaseManagers;
    private readonly Dictionary<DatabaseType, Dictionary<string, Channel<DatabaseMessage>>> messageQueues;
    private readonly Logger logger;
    private readonly CancellationToken cancellationToken;

    static DatabaseMessageHandler()
    {
        channelOptions.SingleReader = true;
    }

    private DatabaseMessageHandler(Dictionary<DatabaseType, IDatabaseManager> databaseManagers, Dictionary<DatabaseType, Dictionary<string, Channel<DatabaseMessage>>> messageQueues, Logger logger, CancellationToken cancellationToken)
    {
        this.databaseManagers = databaseManagers;
        this.messageQueues = messageQueues;
        this.logger = logger;
        this.cancellationToken = cancellationToken;
    }

    public static async Task<DatabaseMessageHandler?> Initialize(IEnumerable<DatabaseConfiguration> databases, Logger logger, CancellationToken cancellationToken)
    {
        Dictionary<DatabaseType, IDatabaseManager> _databaseManagers = [];
        Dictionary<DatabaseType, Dictionary<string, Channel<DatabaseMessage>>> _messageQueues = [];

        var databasesByType = databases.GroupBy(database => database.Type);

        List<(string ConnectionString, TableConfiguration[] Tables)> GetTablesForDatabaseType(DatabaseType databaseType)
        {
            var databasesForType = databasesByType.FirstOrDefault(databases => databases.Key == databaseType);
            if (databasesForType == null) return [];

            var databasesByConStr = databasesForType.GroupBy(database => database.ConnectionString);
            return databasesByConStr.Select(group => (group.Key, group.SelectMany(database => database.Tables).Distinct().ToArray())).ToList();
        }

        foreach (var databaseType in Enum.GetValues<DatabaseType>())
        {
            if (cancellationToken.IsCancellationRequested) return null;

            if (databaseType == DatabaseType.None) continue;

            var databaseManager = IDatabaseManager.MakeManagerFor(databaseType, logger, cancellationToken);

            var tablesByConnectionString = GetTablesForDatabaseType(databaseType);
            var queuesByConnectionString = new Dictionary<string, Channel<DatabaseMessage>>(tablesByConnectionString.Count);

            foreach ((var ConnectionString, var Tables) in tablesByConnectionString)
            {
                queuesByConnectionString.Add(ConnectionString, Channel.CreateUnbounded<DatabaseMessage>(channelOptions));
                
                while (! await databaseManager.TryEnsureTablesExistAsync(ConnectionString, Tables))
                {
                    if (cancellationToken.IsCancellationRequested) return null;

                    await Task.Delay(1000, cancellationToken);
                }
            }

            _databaseManagers.Add(databaseType, databaseManager);
            _messageQueues.Add(databaseType, queuesByConnectionString);
        }

        return new(_databaseManagers, _messageQueues, logger, cancellationToken);
    }

    public void WriteMessage(DatabaseMessage message)
    {
        var database = message.Database;

        Task.Run(() =>
            messageQueues[database.Type][database.ConnectionString].Writer.WriteAsync(message, cancellationToken)
        );
    }

    public Task HandleMessagesAsync()
    {
        var tasks = new List<Task>();

        foreach ((var databaseType, var databaseManager) in databaseManagers)
        {
            var messageQueuesByConnectionString = messageQueues[databaseType];

            foreach ((var connectionString, var messageQueue) in messageQueuesByConnectionString)
            {
                var task = Task.Run(async () =>
                {
                    try
                    {
                        await foreach (List<DatabaseMessage> messages in messageQueue.Reader.ReadBatchesAsync(cancellationToken))
                        {
                            await databaseManager.WriteToDatabaseAsync(connectionString, messages);

                            await Task.Delay(1000, cancellationToken);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // ignore
                    }
                }, cancellationToken);

                tasks.Add(task);
            }
        }

        return Task.WhenAll(tasks);
    }

    public void Dispose()
    {
        foreach (var databaseManager in databaseManagers.Values)
        {
            if (databaseManager is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        databaseManagers.Clear();
    }
}
