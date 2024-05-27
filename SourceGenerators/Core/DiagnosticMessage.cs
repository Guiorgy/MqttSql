/*
    This file is part of MqttSql (Copyright © 2024  Guiorgy).
    MqttSql is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
    MqttSql is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
    You should have received a copy of the GNU General Public License along with Foobar. If not, see <https://www.gnu.org/licenses/>.
*/

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;

namespace SourceGenerators;

internal sealed class DiagnosticMessage(Location location, string message, params object?[] messageArgs) : IEquatable<DiagnosticMessage>
{
    private readonly LocationCache locationCache = location;
    public Location Location => locationCache;
    public string Message { get; } = message;
    public EquatableImmutableArray MessageArgs { get; } = messageArgs;

    public DiagnosticMessage(string message, params object?[] messageArgs) : this(Location.None, message, messageArgs)
    {
    }

    public void ReportDiagnostic(SourceProductionContext context)
    {
        context.ReportDiagnostic(
            Diagnostic.Create(
                new DiagnosticDescriptor("GEN", nameof(SourceGenerators), Message, "Generation", DiagnosticSeverity.Error, true),
                Location,
                MessageArgs.Length == 0 ? null : [..MessageArgs]
            )
        );
    }

    public bool Equals(DiagnosticMessage other) =>
        locationCache.Equals(other.locationCache) &&
        Message == other.Message &&
        MessageArgs.Equals(other.MessageArgs);

    public override bool Equals(object? obj) => obj is DiagnosticMessage other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(locationCache, Message, MessageArgs);

    private sealed class LocationCache(string filePath, TextSpan textSpan, LinePositionSpan lineSpan) : IEquatable<LocationCache>
    {
        public string FilePath { get; } = filePath;
        public TextSpan TextSpan { get; } = textSpan;
        public LinePositionSpan LineSpan { get; } = lineSpan;

        public bool IsNone => FilePath.Length == 0;

        public LocationCache(Location location) : this(location.SourceTree?.FilePath ?? "", location.SourceSpan, location.GetLineSpan().Span)
        {
        }

        public LocationCache(SyntaxNode syntaxNode) : this(syntaxNode.GetLocation())
        {
        }

        public static implicit operator LocationCache(Location location) => new(location);
        public static implicit operator LocationCache(SyntaxNode syntaxNode) => new(syntaxNode);
        public static implicit operator Location(LocationCache cache) => cache.IsNone ? Location.None : Location.Create(cache.FilePath, cache.TextSpan, cache.LineSpan);

        public bool Equals(LocationCache other) =>
            (IsNone && other.IsNone) ||
            (
                FilePath == other.FilePath &&
                TextSpan.Equals(other.TextSpan) &&
                LineSpan.Equals(other.LineSpan)
            );

        public override bool Equals(object? obj) => obj is LocationCache cache && Equals(cache);

        public override int GetHashCode() => IsNone ? 0 : HashCode.Combine(FilePath, TextSpan, LineSpan);
    }
}
