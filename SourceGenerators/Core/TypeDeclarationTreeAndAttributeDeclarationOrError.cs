/*
    This file is part of MqttSql (Copyright © 2024  Guiorgy).
    MqttSql is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
    MqttSql is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
    You should have received a copy of the GNU General Public License along with Foobar. If not, see <https://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SourceGenerators;

internal sealed class TypeDeclarationTreeAndAttributeDataOrError<T> : IEquatable<TypeDeclarationTreeAndAttributeDataOrError<T>> where T : notnull
{
    private readonly TypeDeclarationTreeAndAttributeData<T>? typeDeclarationTreeAndAttributeData;
    private readonly DiagnosticMessage? diagnosticMessage;

    public TypeDeclarationTreeAndAttributeDataOrError(TypeDeclarationTreeAndAttributeData<T>? typeDeclarationTreeAndAttributeData, DiagnosticMessage? diagnosticMessage)
    {
        Debug.Assert(typeDeclarationTreeAndAttributeData != null || diagnosticMessage != null, "Either TypeDeclarationTreeAndAttributeData or DiagnosticMessage must not be null");
        Debug.Assert(typeDeclarationTreeAndAttributeData == null || diagnosticMessage == null, "Either TypeDeclarationTreeAndAttributeData or DiagnosticMessage must be null");

        this.typeDeclarationTreeAndAttributeData = typeDeclarationTreeAndAttributeData;
        this.diagnosticMessage = diagnosticMessage;
    }

    public TypeDeclarationTreeAndAttributeDataOrError(TypeDeclarationTreeAndAttributeData<T> typeDeclarationTreeAndAttributeData) : this(typeDeclarationTreeAndAttributeData, null)
    {
    }

    public TypeDeclarationTreeAndAttributeDataOrError(DiagnosticMessage diagnosticMessage) : this(null, diagnosticMessage)
    {
    }

    public bool IsTypeDeclarationTreeAndAttributeData => typeDeclarationTreeAndAttributeData != null;
    public bool IsError => diagnosticMessage != null;

    private static InvalidCastException InvalidCastException => new($"Attempted to dereference the wrong type from {nameof(TypeDeclarationTreeAndAttributeDataOrError<T>)}");

    public TypeDeclarationTreeAndAttributeData<T> TypeDeclarationTreeAndAttributeData => IsTypeDeclarationTreeAndAttributeData ? typeDeclarationTreeAndAttributeData! : throw InvalidCastException;
    public DiagnosticMessage DiagnosticMessage => IsError ? diagnosticMessage! : throw InvalidCastException;

    public object Value => (IsTypeDeclarationTreeAndAttributeData ? typeDeclarationTreeAndAttributeData : diagnosticMessage)!;

    public static implicit operator TypeDeclarationTreeAndAttributeDataOrError<T>(TypeDeclarationTreeAndAttributeData<T> typeDeclarationTreeAndAttributeData) => new(typeDeclarationTreeAndAttributeData);
    public static implicit operator TypeDeclarationTreeAndAttributeDataOrError<T>(DiagnosticMessage diagnosticMessage) => new(diagnosticMessage);

    public static implicit operator TypeDeclarationTreeAndAttributeData<T>(TypeDeclarationTreeAndAttributeDataOrError<T> typeDeclarationTreeAndAttributeDataOrError) => typeDeclarationTreeAndAttributeDataOrError.TypeDeclarationTreeAndAttributeData;
    public static implicit operator DiagnosticMessage(TypeDeclarationTreeAndAttributeDataOrError<T> typeDeclarationTreeAndAttributeDataOrError) => typeDeclarationTreeAndAttributeDataOrError.DiagnosticMessage;

    public bool TryGetTypeDeclarationTree(out TypeDeclarationTreeAndAttributeData<T>? typeDeclarationTreeAndAttributeData)
    {
        typeDeclarationTreeAndAttributeData = this.typeDeclarationTreeAndAttributeData;
        return IsTypeDeclarationTreeAndAttributeData;
    }
    public bool TryGetDiagnosticMessage(out DiagnosticMessage? diagnosticMessage)
    {
        diagnosticMessage = this.diagnosticMessage;
        return IsError;
    }

    public bool Equals(TypeDeclarationTreeAndAttributeDataOrError<T> other) =>
        (IsTypeDeclarationTreeAndAttributeData && other.IsTypeDeclarationTreeAndAttributeData && typeDeclarationTreeAndAttributeData!.Equals(other.typeDeclarationTreeAndAttributeData!))
        || (IsError && other.IsError && diagnosticMessage!.Equals(other.diagnosticMessage!));

    public override bool Equals(object? obj) => obj is TypeDeclarationTreeAndAttributeDataOrError<T> other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            const int multiplier = -1521134295;
            int hashCode = -1648227012;
            hashCode = (hashCode * multiplier) + typeDeclarationTreeAndAttributeData?.GetHashCode() ?? 0;
            hashCode = (hashCode * multiplier) + diagnosticMessage?.GetHashCode() ?? 0;
            return hashCode;
        }
    }

    public sealed class Comparer<_T> : IEqualityComparer<TypeDeclarationTreeAndAttributeDataOrError<_T>?> where _T : notnull
    {
        bool IEqualityComparer<TypeDeclarationTreeAndAttributeDataOrError<_T>?>.Equals(TypeDeclarationTreeAndAttributeDataOrError<_T>? x, TypeDeclarationTreeAndAttributeDataOrError<_T>? y) =>
            x != null && y != null && x.Equals(y);

        int IEqualityComparer<TypeDeclarationTreeAndAttributeDataOrError<_T>?>.GetHashCode(TypeDeclarationTreeAndAttributeDataOrError<_T>? typeDeclarationTreeAndAttributeDataOrError) =>
            typeDeclarationTreeAndAttributeDataOrError?.GetHashCode() ?? 0;
    }

    public static readonly Comparer<T> EqualityComparer = new();
}
