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

    private static readonly Dictionary<DatabaseType, Func<Logger, CancellationToken, IDatabaseManager>> MakeManagerMappings = new();

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

    public void EnsureTablesExist(string connectionString, TableConfiguration[] tables);

    public Task WriteToDatabaseAsync(string connectionString, IEnumerable<(TableConfiguration[] tables, DateTime timestamp, string message)> entries);
}
