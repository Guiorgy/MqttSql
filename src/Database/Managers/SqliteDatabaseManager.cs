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

public sealed class SqliteDatabaseManager : IDatabaseManager
{
    public static DatabaseType GetDatabaseType() => DatabaseType.SQLite;

    public static IDatabaseManager GetDatabaseManager(Logger logger, CancellationToken cancellationToken) => new SqliteDatabaseManager(logger, cancellationToken);

    private readonly CancellationToken cancellationToken;
    private readonly Logger logger;

    public SqliteDatabaseManager(Logger logger, CancellationToken cancellationToken)
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

        foreach (string table in tables.Select(table => table.Name))
        {
            logger.Debug("Checking the existence of table \"", table, '"');

            command.CommandText = "CREATE TABLE IF NOT EXISTS " + table + "(" +
                                      "id INTEGER NOT NULL PRIMARY KEY," +
                                      "Timestamp DATETIME DEFAULT (DATETIME(CURRENT_TIMESTAMP, 'localtime')) NOT NULL," +
                                      "Message VARCHAR NOT NULL" +
                                  ");";
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public async Task WriteToDatabaseAsync(string connectionString, IEnumerable<(TableConfiguration[] tables, DateTime timestamp, string message)> entries)
    {
        await Task.Run(() =>
        {
            try
            {
                if (cancellationToken.IsCancellationRequested) return;

                using var sqlConnection = new SQLiteConnection(connectionString);
                sqlConnection.Open();

                using var transaction = sqlConnection.BeginTransaction();

                using var command = new SQLiteCommand(sqlConnection);

                foreach ((TableConfiguration[] tables, DateTime timestamp, string message) in entries)
                {
                    if (cancellationToken.IsCancellationRequested) return;

                    command.Transaction = transaction;

                    Dictionary<string, string>? timestampCache = tables.Length > 10 ? new(tables.Length) : null;

                    foreach ((string table, string timeFormat) in tables.Select(table => (table.Name, table.TimestampFormat)))
                    {
                        logger.Debug("Writing \"", message, "\" message to the \"", table, "\" table");

                        string? timestampString;
                        if (timestampCache != null)
                        {
                            if (!timestampCache.TryGetValue(timeFormat, out timestampString))
                            {
                                timestampString = timestamp.ToStringFast(timeFormat);
                                timestampCache.Add(timeFormat, timestampString);
                            }
                        }
                        else
                        {
                            timestampString = timestamp.ToStringFast(timeFormat);
                        }

                        command.CommandText = "INSERT INTO " + table + "(Timestamp, Message) values (@timestamp, @message)";
                        command.Parameters.Clear();
                        command.Parameters.Add("@timestamp", DbType.DateTime).Value = timestampString;
                        command.Parameters.Add("@message", DbType.String).Value = message;
                        command.ExecuteNonQuery();
                    }
                }

                transaction.Commit();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to write to the database");
            }
        }, cancellationToken);
    }
}
