/*
    This file is part of MqttSql (Copyright © 2024 Guiorgy).
    MqttSql is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
    MqttSql is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Affero General Public License for more details.
    You should have received a copy of the GNU Affero General Public License along with Foobar. If not, see <https://www.gnu.org/licenses/>.
*/

using System;

namespace SourceGenerators;

internal sealed class LoggerExtensionsAttributeData(int genericOverrideCount, string[] logLevels) : IEquatable<LoggerExtensionsAttributeData>
{
    public int GenericOverrideCount { get; } = genericOverrideCount;
    public EquatableImmutableArray<string> LogLevels { get; } = logLevels;

    public static implicit operator LoggerExtensionsAttributeData((int genericOverrideCount, string[] logLevels) tuple) => new(tuple.genericOverrideCount, tuple.logLevels);

    public void Deconstruct(out int genericOverrideCount, out ReadOnlySpan<string> logLevels)
    {
        genericOverrideCount = GenericOverrideCount;
        logLevels = LogLevels;
    }

    public bool Equals(LoggerExtensionsAttributeData other) =>
        GenericOverrideCount == other.GenericOverrideCount && LogLevels.Equals(other.LogLevels);

    public override bool Equals(object? obj) => obj is LoggerExtensionsAttributeData other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(GenericOverrideCount, LogLevels);
}
