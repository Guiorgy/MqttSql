/*
    This file is part of MqttSql (Copyright © 2024  Guiorgy).
    MqttSql is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
    MqttSql is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
    You should have received a copy of the GNU General Public License along with Foobar. If not, see <https://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using static MqttSql.Database.DatabaseMessageHandler;

namespace MqttSql.Database;

public static class Extensions
{
    public static string ToFriendlyString(this DatabaseType databaseType)
    {
        return databaseType switch
        {
            DatabaseType.None => nameof(DatabaseType.None),
            DatabaseType.GenericSql => "Generic SQL",
            DatabaseType.PostgreSql => "PostgreSQL",
            DatabaseType.SQLite => nameof(DatabaseType.SQLite),
            _ => throw new NotImplementedException($"You forgot to update a switch statement after modifying the {nameof(DatabaseType)} enum.")
        };
    }

    public static Task WriteToDatabaseAsync(this IDatabaseManager databaseManager, string connectionString, IEnumerable<DatabaseMessage> entries)
    {
        return databaseManager.WriteToDatabaseAsync(
            connectionString,
            entries.Select(message => { message.Deconstruct(out var entry); return entry; }).ToArray()
        );
    }

    public static string ToStringFast(this DateTime dateTime, string format)
    {
        return format switch
        {
            "yyyy-MM-dd HH:mm:ss" => dateTime.ToIsoString(milliseconds: false, strictDateTimeDelimiter: false, omitDelimiters: false),
            "yyyy-MM-dd HH:mm:ss.fff" => dateTime.ToIsoString(milliseconds: true, strictDateTimeDelimiter: false, omitDelimiters: false),
            "yyyy-MM-ddTHH:mm:ss" => dateTime.ToIsoString(milliseconds: false, strictDateTimeDelimiter: true, omitDelimiters: false),
            "yyyy-MM-ddTHH:mm:ss.fff" => dateTime.ToIsoString(milliseconds: true, strictDateTimeDelimiter: true, omitDelimiters: false),
            "yyyyMMddTHHmmss" => dateTime.ToIsoString(milliseconds: false, strictDateTimeDelimiter: true, omitDelimiters: true),
            "yyyyMMddTHHmmssfff" => dateTime.ToIsoString(milliseconds: true, strictDateTimeDelimiter: true, omitDelimiters: true),
            "yyyyMMddHHmmss" => dateTime.ToIsoString(milliseconds: false, strictDateTimeDelimiter: false, omitDelimiters: true),
            "yyyyMMddHHmmssfff" => dateTime.ToIsoString(milliseconds: true, strictDateTimeDelimiter: false, omitDelimiters: true),
            _ => dateTime.ToString(format, CultureInfo.InvariantCulture)
        };
    }
}
