using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SourceGenerators;

internal sealed class TypeDeclarationTreeOrError : IEquatable<TypeDeclarationTreeOrError>
{
    private readonly TypeDeclarationTree? typeDeclarationTree;
    private readonly DiagnosticMessage? diagnosticMessage;

    public TypeDeclarationTreeOrError(TypeDeclarationTree? typeDeclarationTree, DiagnosticMessage? diagnosticMessage)
    {
        Debug.Assert(typeDeclarationTree != null || diagnosticMessage != null, "Either TypeDeclarationTree or DiagnosticMessage must not be null");
        Debug.Assert(typeDeclarationTree == null || diagnosticMessage == null, "Either TypeDeclarationTree or DiagnosticMessage must be null");

        this.typeDeclarationTree = typeDeclarationTree;
        this.diagnosticMessage = diagnosticMessage;
    }

    public TypeDeclarationTreeOrError(TypeDeclarationTree typeDeclarationTree) : this(typeDeclarationTree, null)
    {
    }

    public TypeDeclarationTreeOrError(DiagnosticMessage diagnosticMessage) : this(null, diagnosticMessage)
    {
    }

    public bool IsTypeDeclarationTree => typeDeclarationTree != null;
    public bool IsError => diagnosticMessage != null;

    private static InvalidCastException InvalidCastException => new($"Attempted to dereference the wrong type from {nameof(TypeDeclarationTreeOrError)}");

    public TypeDeclarationTree TypeDeclarationTree => IsTypeDeclarationTree ? typeDeclarationTree! : throw InvalidCastException;
    public DiagnosticMessage DiagnosticMessage => IsError ? diagnosticMessage! : throw InvalidCastException;

    public object Value => (IsTypeDeclarationTree ? typeDeclarationTree : diagnosticMessage)!;

    public static implicit operator TypeDeclarationTreeOrError(TypeDeclarationTree typeDeclarationTree) => new(typeDeclarationTree);
    public static implicit operator TypeDeclarationTreeOrError(DiagnosticMessage diagnosticMessage) => new(diagnosticMessage);

    public static implicit operator TypeDeclarationTree(TypeDeclarationTreeOrError typeDeclarationTreeOrError) => typeDeclarationTreeOrError.TypeDeclarationTree;
    public static implicit operator DiagnosticMessage(TypeDeclarationTreeOrError typeDeclarationTreeOrError) => typeDeclarationTreeOrError.DiagnosticMessage;

    public bool TryGetTypeDeclarationTree(out TypeDeclarationTree? typeDeclarationTree)
    {
        typeDeclarationTree = this.typeDeclarationTree;
        return IsTypeDeclarationTree;
    }
    public bool TryGetDiagnosticMessage(out DiagnosticMessage? diagnosticMessage)
    {
        diagnosticMessage = this.diagnosticMessage;
        return IsError;
    }

    public bool Equals(TypeDeclarationTreeOrError other) =>
        (IsTypeDeclarationTree && other.IsTypeDeclarationTree && typeDeclarationTree!.Equals(other.typeDeclarationTree!))
        || (IsError && other.IsError && diagnosticMessage!.Equals(other.diagnosticMessage!));

    public override bool Equals(object? obj) => obj is TypeDeclarationTreeOrError other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            const int multiplier = -1521134295;
            int hashCode = -1648227012;
            hashCode = (hashCode * multiplier) + EqualityComparer<TypeDeclarationTree?>.Default.GetHashCode(typeDeclarationTree);
            hashCode = (hashCode * multiplier) + EqualityComparer<DiagnosticMessage?>.Default.GetHashCode(diagnosticMessage);
            return hashCode;
        }
    }

    public sealed class Comparer : IEqualityComparer<TypeDeclarationTreeOrError?>
    {
        bool IEqualityComparer<TypeDeclarationTreeOrError?>.Equals(TypeDeclarationTreeOrError? x, TypeDeclarationTreeOrError? y) =>
            x != null && y != null && x.Equals(y);

        int IEqualityComparer<TypeDeclarationTreeOrError?>.GetHashCode(TypeDeclarationTreeOrError? typeDeclarationTreeOrError) =>
            typeDeclarationTreeOrError?.GetHashCode() ?? 0;
    }

    public static readonly Comparer EqualityComparer = new();
}