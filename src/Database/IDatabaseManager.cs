/*
    This file is part of MqttSql (Copyright © 2024  Guiorgy).
    MqttSql is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
    MqttSql is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
    You should have received a copy of the GNU General Public License along with Foobar. If not, see <https://www.gnu.org/licenses/>.
*/

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

    public async Task<bool> TryEnsureTablesExistAsync(string connectionString, TableConfiguration[] tables)
    {
        try
        {
            await EnsureTablesExistAsync(connectionString, tables);

            return true;
        }
        catch (Exception ex)
        {
            GetLogger().Error(
                ex.InnerException ?? ex,
                "Failed to ensure the existance of the table in the database with connection string \"", connectionString, "\""
            );

            return false;
        }
    }

    public void EnsureTablesExist(string connectionString, TableConfiguration[] tables)
    {
        _ = TryEnsureTablesExistAsync(connectionString, tables);
    }

    public Task WriteToDatabaseAsync(string connectionString, IEnumerable<(TableConfiguration[] tables, DateTime timestamp, string message)> entries);

    public async Task<bool> TryWriteToDatabaseAsync(string connectionString, IEnumerable<(TableConfiguration[] tables, DateTime timestamp, string message)> entries)
    {
        try
        {
            await WriteToDatabaseAsync(connectionString, entries);

            return true;
        }
        catch (Exception ex)
        {
            GetLogger().Error(
                ex.InnerException ?? ex,
                "Failed to write to the database with connection string \"", connectionString, "\""
            );

            return false;
        }
    }

    public void WriteToDatabase(string connectionString, IEnumerable<(TableConfiguration[] tables, DateTime timestamp, string message)> entries)
    {
        _ = TryWriteToDatabaseAsync(connectionString, entries);
    }
}
