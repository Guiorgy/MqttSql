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
