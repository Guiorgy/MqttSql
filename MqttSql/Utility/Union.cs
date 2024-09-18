/*
    This file is part of MqttSql (Copyright © 2024 Guiorgy).
    MqttSql is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
    MqttSql is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Affero General Public License for more details.
    You should have received a copy of the GNU Affero General Public License along with MqttSql. If not, see <https://www.gnu.org/licenses/>.
*/

using System;
using System.Diagnostics;

namespace MqttSql.Utility;

public sealed class Union<T1, T2> where T1 : class where T2 : class
{
    private T1? first;
    private T2? second;

    static Union()
    {
        Debug.Assert(typeof(T1) != typeof(T2), $"The two types in {nameof(Union<T1, T2>)} must be distinct");
    }

    private static void ThrowBothNullUnsupported() => throw new NotSupportedException($"One of the two types in {nameof(Union<T1, T2>)} must not be null");
    private static void ThrowBothNotNullUnsupported() => throw new NotSupportedException($"One of the two types in {nameof(Union<T1, T2>)} must be null");
    private static InvalidCastException InvalidCastException => new($"Attempted to dereference the wrong type from {nameof(Union<T1, T2>)}");

    public Union(T1? first, T2? second)
    {
        if (first == null && second == null) ThrowBothNullUnsupported();
        if (first != null && second != null) ThrowBothNotNullUnsupported();

        this.first = first;
        this.second = second;
    }

    public Union(T1 first) : this(first, null)
    {
    }

    public Union(T2 second) : this(null, second)
    {
    }

    public bool IsFirst => first != null;
    public bool IsSecond => second != null;

    public T1 First
    {
        get => IsFirst ? first! : throw InvalidCastException;
        set
        {
            first = value;
            second = null;
        }
    }
    public T2 Second
    {
        get => IsSecond ? second! : throw InvalidCastException;
        set
        {
            first = null;
            second = value;
        }
    }
    public object Value => (IsFirst ? first : second)!;

    public static implicit operator Union<T1, T2>(T1 first) => new(first);
    public static implicit operator Union<T1, T2>(T2 second) => new(second);

    public static implicit operator T1(Union<T1, T2> union) => union.First;
    public static implicit operator T2(Union<T1, T2> union) => union.Second;

    public bool TryGetFirst(out T1? first)
    {
        first = this.first;
        return IsFirst;
    }
    public bool TryGetFirst(out T2? second)
    {
        second = this.second;
        return IsSecond;
    }
}
