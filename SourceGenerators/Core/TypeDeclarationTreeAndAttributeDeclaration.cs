using System;
using System.Collections.Generic;

namespace SourceGenerators;

internal sealed class TypeDeclarationTreeAndAttributeData<T>(TypeDeclarationTree typeDeclarationTree, T attributeData) : IEquatable<TypeDeclarationTreeAndAttributeData<T>> where T : notnull
{
    public TypeDeclarationTree TypeDeclarationTree { get; } = typeDeclarationTree;
    public T AttributeData { get; } = attributeData;

    public bool Equals(TypeDeclarationTreeAndAttributeData<T> other) =>
        TypeDeclarationTree.Equals(other.TypeDeclarationTree) && AttributeData.Equals(other.AttributeData);

    public override bool Equals(object? obj) => obj is TypeDeclarationTreeAndAttributeData<T> other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            const int multiplier = -1521134295;
            int hashCode = 914465144;
            hashCode = (hashCode * multiplier) + TypeDeclarationTree?.GetHashCode() ?? 0;
            hashCode = (hashCode * multiplier) + EqualityComparer<T>.Default.GetHashCode(AttributeData);
            return hashCode;
        }
    }
}
