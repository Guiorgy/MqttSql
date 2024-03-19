/*
    This file is part of MqttSql (Copyright © 2024  Guiorgy).
    MqttSql is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
    MqttSql is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
    You should have received a copy of the GNU General Public License along with Foobar. If not, see <https://www.gnu.org/licenses/>.
*/

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

public sealed class SqliteDatabaseManager(Logger logger, CancellationToken cancellationToken) : IDatabaseManager
{
    public static DatabaseType GetDatabaseType() => DatabaseType.SQLite;

    public static IDatabaseManager GetDatabaseManager(Logger logger, CancellationToken cancellationToken) => new SqliteDatabaseManager(logger, cancellationToken);

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
            using var command = new SQLiteCommand("", sqlConnection, transaction);

            foreach (string table in tables.Select(table => table.Name))
            {
                logger.Debug("Checking the existence of table \"", table, '"');

                command.CommandText =
                    $"""
                    CREATE TABLE IF NOT EXISTS {table} (
                        id INTEGER NOT NULL PRIMARY KEY,
                        Timestamp DATETIME DEFAULT (DATETIME(CURRENT_TIMESTAMP, 'localtime')) NOT NULL,
                        Message VARCHAR NOT NULL
                    );
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
            try
            {
                if (cancellationToken.IsCancellationRequested) return;

                using var sqlConnection = new SQLiteConnection(connectionString);
                sqlConnection.Open();

                using var transaction = sqlConnection.BeginTransaction();
                using var command = new SQLiteCommand("", sqlConnection, transaction);

                foreach ((TableConfiguration[] tables, DateTime timestamp, string message) in entries)
                {
                    if (cancellationToken.IsCancellationRequested) return;

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

                        command.CommandText = $"INSERT INTO {table} (Timestamp, Message) values (@timestamp, @message);";
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
