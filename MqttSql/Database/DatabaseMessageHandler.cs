/*
    This file is part of MqttSql (Copyright © 2024 Guiorgy).
    MqttSql is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
    MqttSql is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Affero General Public License for more details.
    You should have received a copy of the GNU Affero General Public License along with Foobar. If not, see <https://www.gnu.org/licenses/>.
*/

using MqttSql.Configuration;
using MqttSql.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace MqttSql.Database;

public sealed class DatabaseMessageHandler : IDisposable, IAsyncDisposable
{
    public record struct DatabaseMessage(
        DatabaseConfiguration Database,
        DateTime Timestamp,
        string Message
    )
    {
        public readonly void Deconstruct(out (TableConfiguration[] tables, DateTime timestamp, string message) entry) => entry = (Database.Tables, Timestamp, Message);
    }

    private static readonly UnboundedChannelOptions channelOptions = new();
    private readonly Dictionary<DatabaseType, IDatabaseManager> databaseManagers;
    private readonly Dictionary<DatabaseType, Dictionary<string, Channel<DatabaseMessage>>> messageQueues;
    private readonly Logger logger;
    private readonly CancellationToken cancellationToken;
    private bool disposed;

    static DatabaseMessageHandler()
    {
        channelOptions.SingleWriter = false;
        channelOptions.SingleReader = true;
        channelOptions.AllowSynchronousContinuations  = false;
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

                ExponentialBackoff? exponentialBackoff = null;

                while (! await databaseManager.TryEnsureTablesExistAsync(ConnectionString, Tables))
                {
                    if (cancellationToken.IsCancellationRequested) return null;

                    exponentialBackoff ??= new ExponentialBackoff(
                        initialDelay: TimeSpan.FromSeconds(10),
                        maxDelay: TimeSpan.FromMinutes(15),
                        multiplier: 1.7,
                        maxRetries: 0
                    );

                    if (exponentialBackoff.FirstTime)
                        logger.Information("Failed to ensure the existence of tables. Retrying until successful");

                    await exponentialBackoff.Delay(cancellationToken);

                    if (cancellationToken.IsCancellationRequested) return null;
                }
            }

            _databaseManagers.Add(databaseType, databaseManager);
            _messageQueues.Add(databaseType, queuesByConnectionString);
        }

        return new(_databaseManagers, _messageQueues, logger, cancellationToken);
    }

    public void WriteMessage(DatabaseMessage message)
    {
        if (disposed) return;

        var database = message.Database;

        if (!messageQueues[database.Type][database.ConnectionString].Writer.TryWrite(message))
            logger.Information("Failed to enqueue a database message");
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

        _ = Task.WhenAny(tasks).ContinueWith((_) =>
        {
            if (!cancellationToken.IsCancellationRequested) logger.Error("One of ", nameof(HandleMessagesAsync), " message handlers exited without cancellation");
        });

        return Task.WhenAll(tasks);
    }

    public bool QueuesAreEmpty => messageQueues.Values.SelectMany(dict => dict.Values).All(channel => channel.IsEmpty());

    private void ClearManagers()
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

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;

        ClearManagers();
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;

        const int maxDisposeWaitForQueFlush = 10_000;
        const int waitForFlushRetries = 10;
        const int delayMilliseconds = maxDisposeWaitForQueFlush / waitForFlushRetries;

        for (int i = 0; i < waitForFlushRetries; i++)
        {
            if (QueuesAreEmpty) break;

            await Task.Delay(delayMilliseconds);
        }

        ClearManagers();
    }
}
