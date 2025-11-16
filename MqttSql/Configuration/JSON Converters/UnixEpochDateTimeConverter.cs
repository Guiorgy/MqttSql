/*
    This file is part of MqttSql (Copyright © 2024 Guiorgy).
    MqttSql is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
    MqttSql is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Affero General Public License for more details.
    You should have received a copy of the GNU Affero General Public License along with MqttSql. If not, see <https://www.gnu.org/licenses/>.
*/

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MqttSql.Configuration;

public sealed class UnixEpochDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        long unixEpochSeconds = reader.GetInt64();
        return DateTimeOffset.FromUnixTimeSeconds(unixEpochSeconds).DateTime;
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("ISO", value.ToIsoString());
        writer.WriteNumber("UnixEpoch", ((DateTimeOffset)value).ToUnixTimeSeconds());
        writer.WriteEndObject();
    }
}
