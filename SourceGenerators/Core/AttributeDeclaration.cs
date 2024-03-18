using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace SourceGenerators;

internal sealed class AttributeDeclaration(string @class, ImmutableArray<TypedConstant> constructorArgs, ImmutableArray<KeyValuePair<string, TypedConstant>> namedArgs) : IEquatable<AttributeDeclaration>
{
    public string Class { get; } = @class;
    public ImmutableArray<TypedConstant> ConstructorArguments { get; } = constructorArgs;
    public ImmutableArray<KeyValuePair<string, TypedConstant>> NamedArguments { get; } = namedArgs;

    public AttributeDeclaration(AttributeData attributeData) : this(
        @class: attributeData.AttributeClass?.Name ?? "",
        constructorArgs: attributeData.ConstructorArguments,
        namedArgs: attributeData.NamedArguments
    )
    {
    }

    public bool Equals(AttributeDeclaration other) =>
        Class == other.Class &&
        ConstructorArguments.Equals(other.ConstructorArguments) &&
        NamedArguments.Equals(other.NamedArguments);

    public override bool Equals(object? obj) => obj is AttributeDeclaration other && Equals(other);

    public override int GetHashCode()
    {
        const int multiplier = -1521134295;
        int hashCode = -1324099206;
        hashCode = (hashCode * multiplier) + EqualityComparer<string>.Default.GetHashCode(Class);
        hashCode = (hashCode * multiplier) + ConstructorArguments.GetHashCode();
        hashCode = (hashCode * multiplier) + NamedArguments.GetHashCode();
        return hashCode;
    }
}
