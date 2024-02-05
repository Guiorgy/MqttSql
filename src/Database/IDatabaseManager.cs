using MqttSql.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MqttSql.Logging;

namespace MqttSql.Database;

public interface IDatabaseManager
{
    public static DatabaseType GetDatabaseType() => DatabaseType.None;

    public static IDatabaseManager GetDatabaseManager(Logger logger, CancellationToken cancellationToken) => throw new NotImplementedException();

    private static readonly Dictionary<DatabaseType, Func<Logger, CancellationToken, IDatabaseManager>> MakeManagerMappings = [];

    static IDatabaseManager()
    {
        var interfaceType = typeof(IDatabaseManager);
        var implementingTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(s => s.GetTypes())
            .Where(interfaceType.IsAssignableFrom);

        foreach (var implementingType in implementingTypes)
        {
            var databaseType = (DatabaseType)implementingType.GetMethod("GetDatabaseType")!.Invoke(null, null)!;
            var getDatabaseManager = implementingType.GetMethod("GetDatabaseManager")!;

            MakeManagerMappings[databaseType] = (logger, cancellationToken) =>
                (IDatabaseManager)getDatabaseManager.Invoke(null, new object[] { logger, cancellationToken })!;
        }
    }

    public static IDatabaseManager MakeManagerFor(DatabaseType databaseType, Logger logger, CancellationToken cancellationToken)
    {
        if (databaseType == DatabaseType.None) throw new ArgumentException($"{typeof(DatabaseType).FullName}.{databaseType} is not supported", nameof(databaseType));

        if (!MakeManagerMappings.TryGetValue(databaseType, out var getDatabaseManager))
            throw new NotImplementedException($"{typeof(DatabaseType).FullName}.{databaseType} is not implemented");

        return getDatabaseManager(logger, cancellationToken);
    }

    public Logger GetLogger();

    public Task EnsureTablesExistAsync(string connectionString, TableConfiguration[] tables);

    public void EnsureTablesExist(string connectionString, TableConfiguration[] tables)
    {
        EnsureTablesExistAsync(connectionString, tables).ContinueWith(task =>
        {
            const string messagePart0 = "Failed to ensure the existance of the table in the database with connection string \"";
            const string messagePart1 = "\"";

            if (task.Exception != null)
            {
                if (task.Exception.InnerException != null || task.Exception.InnerExceptions.Count == 1)
                {
                    GetLogger().Error(task.Exception.InnerException ?? task.Exception.InnerExceptions[0], messagePart0, connectionString, messagePart1);
                }
                else
                {
                    GetLogger().Error((Exception)task.Exception, messagePart0, connectionString, messagePart1);
                }
            }
            else
            {
                GetLogger().Error(messagePart0, connectionString, messagePart1);
            }
        }, TaskContinuationOptions.OnlyOnFaulted);
    }

    public Task WriteToDatabaseAsync(string connectionString, IEnumerable<(TableConfiguration[] tables, DateTime timestamp, string message)> entries);

    public void WriteToDatabase(string connectionString, IEnumerable<(TableConfiguration[] tables, DateTime timestamp, string message)> entries)
    {
        WriteToDatabaseAsync(connectionString, entries).ContinueWith(task =>
        {
            const string messagePart0 = "Failed to write to the database with connection string \"";
            const string messagePart1 = "\"";

            if (task.Exception != null)
            {
                if (task.Exception.InnerException != null || task.Exception.InnerExceptions.Count == 1)
                {
                    GetLogger().Error(task.Exception.InnerException ?? task.Exception.InnerExceptions[0], messagePart0, connectionString, messagePart1);
                }
                else
                {
                    GetLogger().Error((Exception)task.Exception, messagePart0, connectionString, messagePart1);
                }
            }
            else
            {
                GetLogger().Error(messagePart0, connectionString, messagePart1);
            }
        }, TaskContinuationOptions.OnlyOnFaulted);
    }
}
