using MqttSql.Configuration;
using MqttSql.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MqttSql.Database.Managers;

public sealed class GeneralDatabaseManager(Logger logger, CancellationToken cancellationToken) : IDatabaseManager
{
    public static DatabaseType GetDatabaseType() => DatabaseType.GenericSql;

    public static IDatabaseManager GetDatabaseManager(Logger logger, CancellationToken cancellationToken) => new GeneralDatabaseManager(logger, cancellationToken);

    private readonly CancellationToken cancellationToken = cancellationToken;
    private readonly Logger logger = logger;

    public Logger GetLogger() => logger;

    public Task EnsureTablesExistAsync(string connectionString, TableConfiguration[] tables)
    {
        return Task.Run(() =>
        {
            using var sqlConnection = new SQLiteConnection(connectionString);
            sqlConnection.Open();

            using var transaction = sqlConnection.BeginTransaction();

            using var command = new SQLiteCommand(sqlConnection);
            command.Transaction = transaction;

            var created = new HashSet<string>();
            foreach (string table in tables.Select(table => table.Name))
            {
                if (!created.Add(table))
                    continue;

                logger.Debug("Checking the existence of table \"", table, '"');

                command.CommandText =
                    $"""
                    IF OBJECT_ID('{table}', 'U') IS NULL
                    BEGIN
                        CREATE TABLE {table} (
                            Timestamp DATETIME DEFAULT (DATETIME(CURRENT_TIMESTAMP, 'localtime')) NOT NULL PRIMARY KEY,
                            Message VARCHAR NOT NULL
                        )
                    END;
                    """;

                command.ExecuteNonQuery();
            }

            transaction.Commit();
        }, cancellationToken);
    }

    public Task WriteToDatabaseAsync(string connectionString, IEnumerable<(TableConfiguration[] tables, DateTime timestamp, string message)> entries)
    {
        return Task.Run(() =>
        {
            throw new NotImplementedException(); // TODO
        }, cancellationToken);
    }
}
