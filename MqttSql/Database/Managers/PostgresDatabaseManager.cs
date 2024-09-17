/*
    This file is part of MqttSql (Copyright © 2024 Guiorgy).
    MqttSql is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
    MqttSql is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Affero General Public License for more details.
    You should have received a copy of the GNU Affero General Public License along with Foobar. If not, see <https://www.gnu.org/licenses/>.
*/

using MqttSql.Configuration;
using MqttSql.Logging;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MqttSql.Database.Managers;

public sealed class PostgresDatabaseManager(Logger logger, CancellationToken cancellationToken) : IDatabaseManager, IDisposable
{
    public static DatabaseType GetDatabaseType() => DatabaseType.PostgreSql;

    public static IDatabaseManager GetDatabaseManager(Logger logger, CancellationToken cancellationToken) => new PostgresDatabaseManager(logger, cancellationToken);

    private readonly CancellationToken cancellationToken = cancellationToken;
    private readonly Logger logger = logger;

    public Logger GetLogger() => logger;

    private readonly Dictionary<string, NpgsqlDataSource> dataSurceCache = [];
    private readonly object dataSurceCacheLock = new();
    private bool disposed;

    private NpgsqlDataSource? GetDataSource(string connectionString)
    {
        if (disposed) return null;

        if (dataSurceCache.TryGetValue(connectionString, out NpgsqlDataSource? dataSource)) return dataSource;

        lock (dataSurceCacheLock)
        {
            if (disposed) return null;

            if (dataSurceCache.TryGetValue(connectionString, out dataSource)) return dataSource;

            dataSource = NpgsqlDataSource.Create(connectionString);
            dataSurceCache.Add(connectionString, dataSource);

            return dataSource;
        }
    }

    public async Task EnsureTablesExistAsync(string connectionString, TableConfiguration[] tables)
    {
        var dataSource = GetDataSource(connectionString);
        if (dataSource == null) return;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(null, connection, transaction);

        foreach (var (table, i) in tables.Select((table, i) => (table.Name, i)))
        {
            logger.Debug("Checking the existence of table \"", table, '"');

            command.CommandText =
                $"""
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT * FROM information_schema.tables
                        WHERE table_schema = current_schema() AND table_name = '{table}'
                    ) THEN
                        CREATE TABLE {table} (
                            id SERIAL PRIMARY KEY,
                            Timestamp TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                            Message TEXT NOT NULL
                        );
                        CREATE INDEX idx_{table}_timestamp ON {table} (
                	        Timestamp ASC
                        );
                    END IF;
                END;
                $$ LANGUAGE plpgsql;
                """;

            _ = await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task WriteToDatabaseAsync(string connectionString, IEnumerable<(TableConfiguration[] tables, DateTime timestamp, string message)> entries)
    {
        var dataSource = GetDataSource(connectionString);
        if (dataSource == null) return;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(null, connection, transaction);

        foreach ((TableConfiguration[] tables, DateTime timestamp, string message) in entries)
        {
            if (cancellationToken.IsCancellationRequested) return;

            foreach ((string table, string timeFormat) in tables.Select(table => (table.Name, table.TimestampFormat)))
            {
                logger.Debug("Writing \"", message, "\" message to the \"", table, "\" table");

                command.CommandText = $"INSERT INTO {table} (Timestamp, Message) values ($1, $2);";

                command.Parameters.Clear();
                _ = command.Parameters.AddWithValue(timestamp);
                _ = command.Parameters.AddWithValue(message);

                _ = await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public void Dispose()
    {
        if (dataSurceCache == null) return;

        lock (dataSurceCacheLock)
        {
            if (dataSurceCache == null) return;

            foreach (var dataSource in dataSurceCache.Values)
                dataSource.Dispose();
            dataSurceCache.Clear();

            disposed = true;
        }
    }
}
