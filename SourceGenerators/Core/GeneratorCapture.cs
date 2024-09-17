/*
    This file is part of MqttSql (Copyright © 2024 Guiorgy).
    MqttSql is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
    MqttSql is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Affero General Public License for more details.
    You should have received a copy of the GNU Affero General Public License along with Foobar. If not, see <https://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SourceGenerators;

internal sealed class GeneratorCapture<T> : IEquatable<GeneratorCapture<T>> where T : class?, IEquatable<T>
{
    private readonly T? capture;
    private readonly DiagnosticMessage? diagnosticMessage;

    public GeneratorCapture(T? capture, DiagnosticMessage? diagnosticMessage)
    {
        Debug.Assert(capture != null || diagnosticMessage != null, "Either Capture or DiagnosticMessage must not be null");
        Debug.Assert(capture == null || diagnosticMessage == null, "Either Capture or DiagnosticMessage must be null");

        this.capture = capture;
        this.diagnosticMessage = diagnosticMessage;
    }

    public GeneratorCapture(T capture) : this(capture, null)
    {
    }

    public GeneratorCapture(DiagnosticMessage diagnosticMessage) : this(null, diagnosticMessage)
    {
    }

    public bool IsSuccess => capture != null;
    public bool IsError => diagnosticMessage != null;

    private static InvalidCastException InvalidCastException => new($"Attempted to dereference the wrong type from {nameof(GeneratorCapture<T>)}");

    public T Capture => IsSuccess ? capture! : throw InvalidCastException;
    public DiagnosticMessage DiagnosticMessage => IsError ? diagnosticMessage! : throw InvalidCastException;

    public object Value => (IsSuccess ? capture : diagnosticMessage)!;

    public static implicit operator GeneratorCapture<T>(T capture) => new(capture);
    public static implicit operator GeneratorCapture<T>(DiagnosticMessage diagnosticMessage) => new(diagnosticMessage);

    public static implicit operator T(GeneratorCapture<T> generatorCapture) => generatorCapture.Capture;
    public static implicit operator DiagnosticMessage(GeneratorCapture<T> generatorCapture) => generatorCapture.DiagnosticMessage;

    public bool TryGetTypeDeclarationTree(out T? capture)
    {
        capture = this.capture;
        return IsSuccess;
    }
    public bool TryGetDiagnosticMessage(out DiagnosticMessage? diagnosticMessage)
    {
        diagnosticMessage = this.diagnosticMessage;
        return IsError;
    }

    public bool Equals(GeneratorCapture<T> other) =>
        (IsSuccess && other.IsSuccess && capture!.Equals(other.capture!))
        || (IsError && other.IsError && diagnosticMessage!.Equals(other.diagnosticMessage!));

    public override bool Equals(object? obj) => obj is GeneratorCapture<T> other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(capture, diagnosticMessage);
}
