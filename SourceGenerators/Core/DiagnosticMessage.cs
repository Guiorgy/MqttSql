/*
    This file is part of MqttSql (Copyright © 2024  Guiorgy).
    MqttSql is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
    MqttSql is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
    You should have received a copy of the GNU General Public License along with Foobar. If not, see <https://www.gnu.org/licenses/>.
*/

using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SourceGenerators;

internal sealed class DiagnosticMessage(Location location, string message, params object?[] messageArgs) : IEquatable<DiagnosticMessage>
{
    public Location Location { get; } = location;
    public string Message { get; } = message;
    public object?[] MessageArgs { get; } = messageArgs;

    public DiagnosticMessage(string message, params object?[] messageArgs) : this(Location.None, message, messageArgs)
    {
    }

    public void ReportDiagnostic(SourceProductionContext context)
    {
        context.ReportDiagnostic(
            Diagnostic.Create(
                new DiagnosticDescriptor("GEN", nameof(SourceGenerators), Message, "Generation", DiagnosticSeverity.Error, true),
                Location,
                MessageArgs
            )
        );
    }

    public bool Equals(DiagnosticMessage other) =>
        EqualityComparer<Location>.Default.Equals(Location, other.Location) &&
        Message == other.Message &&
        MessageArgs.SequenceEqual(other.MessageArgs);

    public override bool Equals(object? obj) => obj is DiagnosticMessage other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            const int multiplier = -1521134295;
            int hashCode = -1896323061;
            hashCode = (hashCode * multiplier) + EqualityComparer<Location>.Default.GetHashCode(Location);
            hashCode = (hashCode * multiplier) + EqualityComparer<string>.Default.GetHashCode(Message);
            hashCode = (hashCode * multiplier) + EqualityComparer<object?[]>.Default.GetHashCode(MessageArgs);
            return hashCode;
        }
    }
}
