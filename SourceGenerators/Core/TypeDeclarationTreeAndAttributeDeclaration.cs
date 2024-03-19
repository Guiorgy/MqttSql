/*
    This file is part of MqttSql (Copyright © 2024  Guiorgy).
    MqttSql is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
    MqttSql is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
    You should have received a copy of the GNU General Public License along with Foobar. If not, see <https://www.gnu.org/licenses/>.
*/

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
