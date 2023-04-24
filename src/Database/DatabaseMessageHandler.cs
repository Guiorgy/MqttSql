using MqttSql.Configuration;
using MqttSql.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace MqttSql.Database;

public sealed class DatabaseMessageHandler
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
    private readonly Dictionary<DatabaseType, IDatabaseManager> databaseManagers = new();
    private readonly Dictionary<DatabaseType, Dictionary<string, Channel<DatabaseMessage>>> messageQueues = new();
    private readonly Logger logger;
    private readonly CancellationToken cancellationToken;

    static DatabaseMessageHandler()
    {
        channelOptions.SingleReader = true;
    }

    public DatabaseMessageHandler(IEnumerable<DatabaseConfiguration> databases, Logger logger, CancellationToken cancellationToken)
    {
        this.logger = logger;
        this.cancellationToken = cancellationToken;

        var databasesByType = databases.GroupBy(database => database.Type);

        List<(string ConnectionString, TableConfiguration[] Tables)> GetTablesForDatabaseType(DatabaseType databaseType)
        {
            return databasesByType.FirstOrDefault(databases => databases.Key == databaseType)
                ?.GroupBy(database => database.ConnectionString)
                ?.Select(group => (group.Key, group.SelectMany(database => database.Tables).Distinct().ToArray()))
                ?.ToList()
                ?? new();
        }

        foreach (var databaseType in Enum.GetValues<DatabaseType>())
        {
            if (databaseType == DatabaseType.None) continue;

            var databaseManager = IDatabaseManager.MakeManagerFor(databaseType, logger, cancellationToken);

            var tablesByConnectionString = GetTablesForDatabaseType(databaseType);
            var queuesByConnectionString = new Dictionary<string, Channel<DatabaseMessage>>(tablesByConnectionString.Count);

            foreach ((var ConnectionString, var Tables) in tablesByConnectionString)
            {
                queuesByConnectionString.Add(ConnectionString, Channel.CreateUnbounded<DatabaseMessage>(channelOptions));
                databaseManager.EnsureTablesExist(ConnectionString, Tables);
            }

            databaseManagers.Add(databaseType, databaseManager);
            messageQueues.Add(databaseType, queuesByConnectionString);
        }
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
}
