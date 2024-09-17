/*
    This file is part of MqttSql (Copyright © 2024 Guiorgy).
    MqttSql is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
    MqttSql is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Affero General Public License for more details.
    You should have received a copy of the GNU Affero General Public License along with Foobar. If not, see <https://www.gnu.org/licenses/>.
*/

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Linq;

namespace SourceGenerators;

internal sealed class TypeDeclarationTree(string[] usings, string @namespace, TypeDeclaration[] typeDeclarationAncestry) : IEquatable<TypeDeclarationTree>
{
    public EquatableImmutableArray<string> Usings => usings;
    public string Namespace { get; } = @namespace;
    public EquatableImmutableArray<TypeDeclaration> TypeDeclarationAncestry { get; } = typeDeclarationAncestry;

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

    public bool Equals(TypeDeclarationTree other) =>
        Usings.SequenceEqual(other.Usings) &&
        Namespace == other.Namespace &&
        TypeDeclarationAncestry.AsSpan().SequenceEqual(other.TypeDeclarationAncestry.AsSpan());

    public override bool Equals(object? obj) =>
        obj is TypeDeclarationTree other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Usings, Namespace, TypeDeclarationAncestry);
}
