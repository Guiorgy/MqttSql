using System;
using System.Collections.Generic;

namespace SourceGenerators;

internal sealed class TypeDeclarationTreeAndAttributeDeclaration(TypeDeclarationTree typeDeclarationTree, AttributeDeclaration? attributeDeclaration = null) : IEquatable<TypeDeclarationTreeAndAttributeDeclaration>
{
    public TypeDeclarationTree TypeDeclarationTree { get; } = typeDeclarationTree;
    public AttributeDeclaration? AttributeDeclaration { get; } = attributeDeclaration;

    private static bool Equal(AttributeDeclaration? x, AttributeDeclaration? y) =>
        (x == null && y == null) || (x != null && y != null && x.Equals(y));

    public bool Equals(TypeDeclarationTreeAndAttributeDeclaration other) =>
        TypeDeclarationTree.Equals(other.TypeDeclarationTree) && Equal(AttributeDeclaration, other.AttributeDeclaration);

    public override bool Equals(object? obj) => obj is TypeDeclarationTreeAndAttributeDeclaration other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            const int multiplier = -1521134295;
            int hashCode = 914465144;
            hashCode = (hashCode * multiplier) + EqualityComparer<TypeDeclarationTree>.Default.GetHashCode(TypeDeclarationTree);
            hashCode = (hashCode * multiplier) + EqualityComparer<AttributeDeclaration?>.Default.GetHashCode(AttributeDeclaration);
            return hashCode;
        }
    }
}
