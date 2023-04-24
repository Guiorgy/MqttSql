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

public sealed class GeneralDatabaseManager : IDatabaseManager
{
    public static DatabaseType GetDatabaseType() => DatabaseType.GenericSql;

    public static IDatabaseManager GetDatabaseManager(Logger logger, CancellationToken cancellationToken) => new GeneralDatabaseManager(logger, cancellationToken);

    private readonly CancellationToken cancellationToken;
    private readonly Logger logger;

    public GeneralDatabaseManager(Logger logger, CancellationToken cancellationToken)
    {
        this.logger = logger;
        this.cancellationToken = cancellationToken;
    }

    public void EnsureTablesExist(string connectionString, TableConfiguration[] tables)
    {
        using var sqlConnection = new SQLiteConnection(connectionString);
        sqlConnection.Open();

        using var transaction = sqlConnection.BeginTransaction();

        using var command = new SQLiteCommand(sqlConnection);
        command.Transaction = transaction;

        var created = new HashSet<string>();
        foreach (string table in tables.Select(table => table.Name))
        {
            if (created.Contains(table))
                continue;
            else
                created.Add(table);

            logger.Debug("Checking the existence of table \"", table, '"');

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

    public async Task WriteToDatabaseAsync(string connectionString, IEnumerable<(TableConfiguration[] tables, DateTime timestamp, string message)> entries)
    {
        await Task.Run(() =>
        {
            throw new NotImplementedException(); // TODO
        }, cancellationToken);
    }
}
