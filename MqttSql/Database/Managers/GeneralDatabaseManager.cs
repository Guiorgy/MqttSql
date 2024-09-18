/*
    This file is part of MqttSql (Copyright © 2024 Guiorgy).
    MqttSql is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
    MqttSql is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Affero General Public License for more details.
    You should have received a copy of the GNU Affero General Public License along with MqttSql. If not, see <https://www.gnu.org/licenses/>.
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

                _ = command.ExecuteNonQuery();
            }

            transaction.Commit();
        }, cancellationToken);
    }

    public Task WriteToDatabaseAsync(string connectionString, IEnumerable<(TableConfiguration[] tables, DateTime timestamp, string message)> entries)
        => Task.Run(() => throw new NotImplementedException() /* TODO */, cancellationToken);
}
