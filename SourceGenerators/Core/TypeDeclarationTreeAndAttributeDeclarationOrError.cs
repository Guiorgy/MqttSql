using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SourceGenerators;

internal sealed class TypeDeclarationTreeAndAttributeDeclarationOrError : IEquatable<TypeDeclarationTreeAndAttributeDeclarationOrError>
{
    private readonly TypeDeclarationTreeAndAttributeDeclaration? typeDeclarationTreeAndAttributeDeclaration;
    private readonly DiagnosticMessage? diagnosticMessage;

    public TypeDeclarationTreeAndAttributeDeclarationOrError(TypeDeclarationTreeAndAttributeDeclaration? typeDeclarationTreeAndAttributeDeclaration, DiagnosticMessage? diagnosticMessage)
    {
        Debug.Assert(typeDeclarationTreeAndAttributeDeclaration != null || diagnosticMessage != null, "Either TypeDeclarationTreeAndAttributeDeclaration or DiagnosticMessage must not be null");
        Debug.Assert(typeDeclarationTreeAndAttributeDeclaration == null || diagnosticMessage == null, "Either TypeDeclarationTreeAndAttributeDeclaration or DiagnosticMessage must be null");

        this.typeDeclarationTreeAndAttributeDeclaration = typeDeclarationTreeAndAttributeDeclaration;
        this.diagnosticMessage = diagnosticMessage;
    }

    public TypeDeclarationTreeAndAttributeDeclarationOrError(TypeDeclarationTreeAndAttributeDeclaration typeDeclarationTreeAndAttributeDeclaration) : this(typeDeclarationTreeAndAttributeDeclaration, null)
    {
    }

    public TypeDeclarationTreeAndAttributeDeclarationOrError(DiagnosticMessage diagnosticMessage) : this(null, diagnosticMessage)
    {
    }

    public bool IsTypeDeclarationTreeAndAttributeDeclaration => typeDeclarationTreeAndAttributeDeclaration != null;
    public bool IsError => diagnosticMessage != null;

    private static InvalidCastException InvalidCastException => new($"Attempted to dereference the wrong type from {nameof(TypeDeclarationTreeAndAttributeDeclarationOrError)}");

    public TypeDeclarationTreeAndAttributeDeclaration TypeDeclarationTreeAndAttributeDeclaration => IsTypeDeclarationTreeAndAttributeDeclaration ? typeDeclarationTreeAndAttributeDeclaration! : throw InvalidCastException;
    public DiagnosticMessage DiagnosticMessage => IsError ? diagnosticMessage! : throw InvalidCastException;

    public object Value => (IsTypeDeclarationTreeAndAttributeDeclaration ? typeDeclarationTreeAndAttributeDeclaration : diagnosticMessage)!;

    public static implicit operator TypeDeclarationTreeAndAttributeDeclarationOrError(TypeDeclarationTreeAndAttributeDeclaration typeDeclarationTreeAndAttributeDeclaration) => new(typeDeclarationTreeAndAttributeDeclaration);
    public static implicit operator TypeDeclarationTreeAndAttributeDeclarationOrError(DiagnosticMessage diagnosticMessage) => new(diagnosticMessage);

    public static implicit operator TypeDeclarationTreeAndAttributeDeclaration(TypeDeclarationTreeAndAttributeDeclarationOrError typeDeclarationTreeAndAttributeDeclarationOrError) => typeDeclarationTreeAndAttributeDeclarationOrError.TypeDeclarationTreeAndAttributeDeclaration;
    public static implicit operator DiagnosticMessage(TypeDeclarationTreeAndAttributeDeclarationOrError typeDeclarationTreeAndAttributeDeclarationOrError) => typeDeclarationTreeAndAttributeDeclarationOrError.DiagnosticMessage;

    public bool TryGetTypeDeclarationTree(out TypeDeclarationTreeAndAttributeDeclaration? typeDeclarationTreeAndAttributeDeclaration)
    {
        typeDeclarationTreeAndAttributeDeclaration = this.typeDeclarationTreeAndAttributeDeclaration;
        return IsTypeDeclarationTreeAndAttributeDeclaration;
    }
    public bool TryGetDiagnosticMessage(out DiagnosticMessage? diagnosticMessage)
    {
        diagnosticMessage = this.diagnosticMessage;
        return IsError;
    }

    public bool Equals(TypeDeclarationTreeAndAttributeDeclarationOrError other) =>
        (IsTypeDeclarationTreeAndAttributeDeclaration && other.IsTypeDeclarationTreeAndAttributeDeclaration && typeDeclarationTreeAndAttributeDeclaration!.Equals(other.typeDeclarationTreeAndAttributeDeclaration!))
        || (IsError && other.IsError && diagnosticMessage!.Equals(other.diagnosticMessage!));

    public override bool Equals(object? obj) => obj is TypeDeclarationTreeAndAttributeDeclarationOrError other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            const int multiplier = -1521134295;
            int hashCode = -1648227012;
            hashCode = (hashCode * multiplier) + typeDeclarationTreeAndAttributeDeclaration?.GetHashCode() ?? 0;
            hashCode = (hashCode * multiplier) + diagnosticMessage?.GetHashCode() ?? 0;
            return hashCode;
        }
    }

    public sealed class Comparer : IEqualityComparer<TypeDeclarationTreeAndAttributeDeclarationOrError?>
    {
        bool IEqualityComparer<TypeDeclarationTreeAndAttributeDeclarationOrError?>.Equals(TypeDeclarationTreeAndAttributeDeclarationOrError? x, TypeDeclarationTreeAndAttributeDeclarationOrError? y) =>
            x != null && y != null && x.Equals(y);

        int IEqualityComparer<TypeDeclarationTreeAndAttributeDeclarationOrError?>.GetHashCode(TypeDeclarationTreeAndAttributeDeclarationOrError? typeDeclarationTreeAndAttributeDeclarationOrError) =>
            typeDeclarationTreeAndAttributeDeclarationOrError?.GetHashCode() ?? 0;
    }

    public static readonly Comparer EqualityComparer = new();
}
