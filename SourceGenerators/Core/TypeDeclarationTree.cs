using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SourceGenerators;

internal sealed class TypeDeclarationTree(string[] usings, string @namespace, TypeDeclaration[] typeDeclarationAncestry) : IEquatable<TypeDeclarationTree>
{
    public string[] Usings { get; } = usings;
    public string Namespace { get; } = @namespace;
    public TypeDeclaration[] TypeDeclarationAncestry { get; } = typeDeclarationAncestry;

    public string UniqueName => string.Join(".", TypeDeclarationAncestry.Select(d => d.Name).And(Namespace).Reverse());

    public TypeDeclarationTree(string[] usings, string @namespace, TypeDeclarationSyntax typeDeclarationSyntax) : this(
        usings: usings,
        @namespace: @namespace,
        typeDeclarationAncestry: TypeDeclaration.GetDeclarationAncestry(typeDeclarationSyntax)
    )
    {
    }

    public TypeDeclarationTree(string @namespace, TypeDeclarationSyntax typeDeclarationSyntax) : this(
        usings: typeDeclarationSyntax.GetUsings().ToArray(),
        @namespace: @namespace,
        typeDeclarationAncestry: TypeDeclaration.GetDeclarationAncestry(typeDeclarationSyntax)
    )
    {
    }

    public TypeDeclarationTree(TypeDeclarationSyntax typeDeclarationSyntax) : this(
        @namespace: typeDeclarationSyntax.GetNamespace(),
        typeDeclarationSyntax: typeDeclarationSyntax
    )
    {
    }

    public override bool Equals(object? obj) =>
        obj is TypeDeclarationTree other && Equals(other);

    public bool Equals(TypeDeclarationTree other) =>
        Usings.AsSpan().SequenceEqual(other.Usings) &&
        Namespace == other.Namespace &&
        TypeDeclarationAncestry.AsSpan().SequenceEqual(other.TypeDeclarationAncestry);

    public override int GetHashCode()
    {
        unchecked
        {
            const int multiplier = -1521134295;
            int hashCode = 1681965786;
            hashCode = (hashCode * multiplier) + EqualityComparer<string[]>.Default.GetHashCode(Usings);
            hashCode = (hashCode * multiplier) + EqualityComparer<string>.Default.GetHashCode(Namespace);
            hashCode = (hashCode * multiplier) + EqualityComparer<TypeDeclaration[]>.Default.GetHashCode(TypeDeclarationAncestry);
            return hashCode;
        }
    }
}
