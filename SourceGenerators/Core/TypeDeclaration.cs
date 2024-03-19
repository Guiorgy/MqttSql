using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;

namespace SourceGenerators;

internal sealed class TypeDeclaration(string modifiers, string keyword, string name, string typeParameters, string constraints) : IEquatable<TypeDeclaration>
{
    public string Modifiers { get; } = modifiers;
    public string Keyword { get; } = keyword;
    public string Name { get; } = name;
    public string TypeParameters { get; } = typeParameters;
    public string Constraints { get; } = constraints;

    public TypeDeclaration WithName(string name) => new(Modifiers, Keyword, name, TypeParameters, Constraints);

    public TypeDeclaration(TypeDeclarationSyntax typeDeclarationSyntax) : this(
        modifiers: typeDeclarationSyntax.Modifiers.ToString(),
        keyword: typeDeclarationSyntax.Keyword.ValueText,
        name: typeDeclarationSyntax.Identifier.ToString(),
        typeParameters: typeDeclarationSyntax.TypeParameterList?.ToString() ?? "",
        constraints: typeDeclarationSyntax.ConstraintClauses.ToString()
    )
    {
    }

    public static TypeDeclaration[] GetDeclarationAncestry(TypeDeclarationSyntax typeSyntax)
    {
        var declarations = new List<TypeDeclaration>();

        TypeDeclarationSyntax? parentSyntax = typeSyntax;
        do
        {
            declarations.Add(new(parentSyntax));

            parentSyntax = parentSyntax.Parent as TypeDeclarationSyntax;
        }
        while (parentSyntax != null && IsAllowedKind(parentSyntax.Kind()));

        return [..declarations];
    }

    private static bool IsAllowedKind(SyntaxKind kind) =>
        kind is SyntaxKind.ClassDeclaration or
        SyntaxKind.InterfaceDeclaration or
        SyntaxKind.StructDeclaration or
        SyntaxKind.RecordDeclaration;

    public bool Equals(TypeDeclaration other) =>
        Modifiers == other.Modifiers &&
        Keyword == other.Keyword &&
        Name == other.Name &&
        TypeParameters == other.TypeParameters &&
        Constraints == other.Constraints;

    public override bool Equals(object? obj) =>
        obj is TypeDeclaration other && Equals(other);

    public override int GetHashCode()
    {
        static int StringHash(string s) => EqualityComparer<string>.Default.GetHashCode(s);

        unchecked
        {
            const int multiplier = -1521134295;
            int hashCode = -754136522;
            hashCode = (hashCode * multiplier) + StringHash(Modifiers);
            hashCode = (hashCode * multiplier) + StringHash(Keyword);
            hashCode = (hashCode * multiplier) + StringHash(Name);
            hashCode = (hashCode * multiplier) + StringHash(TypeParameters);
            hashCode = (hashCode * multiplier) + StringHash(Constraints);
            return hashCode;
        }
    }

    public override string ToString()
    {
        string type = $"{Modifiers} {Keyword} {Name}{TypeParameters}";

        return Constraints.Length != 0 ? $"{type} {string.Join(" ", Constraints)}" : type;
    }
}
