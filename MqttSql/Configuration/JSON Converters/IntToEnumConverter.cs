/*
    This file is part of MqttSql (Copyright © 2024 Guiorgy).
    MqttSql is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
    MqttSql is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Affero General Public License for more details.
    You should have received a copy of the GNU Affero General Public License along with MqttSql. If not, see <https://www.gnu.org/licenses/>.
*/

using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MqttSql.Configuration;

[RequiresDynamicCode("Use IntToEnumConverter<T> instead.")]
public sealed class IntToEnumConverter : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        if (!CanConvert(typeToConvert))
        {
            throw new ArgumentException("Type must be an enum.", nameof(typeToConvert));
        }

        return (JsonConverter)Activator.CreateInstance(typeof(IntToEnumConverter<>).MakeGenericType(typeToConvert))!;
    }
}

public sealed class IntToEnumConverter<T> : JsonConverter<T> where T : struct, Enum
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        int value = JsonSerializer.Deserialize<int>(ref reader, options);
        T @enum = (T)(object)value;

        if (!Enum.IsDefined(@enum))
            throw new InvalidEnumArgumentException($"{nameof(T)} doesn't define an option with the value {value}");

        return @enum;
    }

    [SuppressMessage("Critical Code Smell", "S927:Parameter names should match base declaration and other partial definitions")]
    public override void Write(Utf8JsonWriter writer, T @enum, JsonSerializerOptions options)
    {
        int value = (int)(object)@enum;

        if (!Enum.IsDefined(@enum))
            throw new InvalidEnumArgumentException($"{nameof(T)} doesn't define an option with the value {value}");

        JsonSerializer.Serialize(writer, value);
    }
}
