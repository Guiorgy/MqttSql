using System;
using System.Diagnostics;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
        get
        {
            return IsFirst ? first! : throw InvalidCastException;
        }
        set
        {
            first = value;
            second = null;
        }
    }
    public T2 Second
    {
        get
        {
            return IsSecond ? second! : throw InvalidCastException;
        }
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
