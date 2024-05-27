// Based on the work by andrewlock licensed under the MIT license: https://github.com/andrewlock/StronglyTypedId/blob/e5df78d0aa72f2232f423938c0d98d9bf4517092/src/StronglyTypedIds/EquatableArray.cs
// All the modifications to this file are also licensed by Guiorgy under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

#pragma warning disable RCS1139

namespace SourceGenerators;

/// <summary>
/// An immutable, equatable array. This is equivalent to <see cref="ImmutableArray{T}"/> but with value equality support.
/// </summary>
/// <typeparam name="T">The type of values in the array.</typeparam>
/// <param name="array">The input <see cref="Array"/> to wrap.</param>
internal readonly struct EquatableImmutableArray<T>(T[] array) : IEquatable<EquatableImmutableArray<T>>, IReadOnlyList<T> where T : IEquatable<T>
{
    public static readonly EquatableImmutableArray<T> Empty = new([]);

    /// <summary>
    /// The underlying <typeparamref name="T"/> array.
    /// </summary>
    private readonly T[]? _array = array;

    public static implicit operator EquatableImmutableArray<T>(T[] array) => new(array);
    public static implicit operator ReadOnlySpan<T>(EquatableImmutableArray<T> array) => array.AsSpan();

    /// <summary>
    /// Gets the total number of elements in all the dimensions of the array.
    /// </summary>
    /// <returns>The total number of elements in all the dimensions of the array; zero if there are no elements in the array.</returns>
    /// <exception cref="OverflowException">The array is multidimensional and contains more than <see cref="int.MaxValue"/> elements.</exception>
    public int Length => _array?.Length ?? 0;

    /// <sinheritdoc/>
    public int Count => _array?.Length ?? 0;

    /// <sinheritdoc/>
    public T this[int index] => _array![index];

    /// <sinheritdoc/>
    public bool Equals(EquatableImmutableArray<T> array) => AsSpan().SequenceEqual(array.AsSpan());

    /// <sinheritdoc/>
    public override bool Equals(object? obj) => obj is EquatableImmutableArray<T> other && Equals(this, other);

    /// <sinheritdoc/>
    public override int GetHashCode()
    {
        if (_array is not T[] arr) return 0;

        HashCode hashCode = default;

        foreach (T item in arr)
            hashCode.Add(item);

        return hashCode.ToHashCode();
    }

    /// <summary>
    /// Returns a <see cref="ReadOnlySpan{T}"/> wrapping the current items.
    /// </summary>
    /// <returns>A <see cref="ReadOnlySpan{T}"/> wrapping the current items.</returns>
    public ReadOnlySpan<T> AsSpan() => _array.AsSpan();

    /// <summary>
    /// Gets the underlying array if there is one.
    /// </summary>
    public T[]? GetArray() => _array;

    /// <sinheritdoc/>
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => ((IEnumerable<T>)(_array ?? [])).GetEnumerator();

    /// <sinheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<T>)(_array ?? [])).GetEnumerator();

    /// <summary>
    /// Checks whether two <see cref="EquatableImmutableArray{T}"/> values are the same.
    /// </summary>
    /// <param name="left">The first <see cref="EquatableImmutableArray{T}"/> value.</param>
    /// <param name="right">The second <see cref="EquatableImmutableArray{T}"/> value.</param>
    /// <returns>Whether <paramref name="left"/> and <paramref name="right"/> are equal.</returns>
    public static bool operator ==(EquatableImmutableArray<T> left, EquatableImmutableArray<T> right) => left.Equals(right);

    /// <summary>
    /// Checks whether two <see cref="EquatableImmutableArray{T}"/> values are not the same.
    /// </summary>
    /// <param name="left">The first <see cref="EquatableImmutableArray{T}"/> value.</param>
    /// <param name="right">The second <see cref="EquatableImmutableArray{T}"/> value.</param>
    /// <returns>Whether <paramref name="left"/> and <paramref name="right"/> are not equal.</returns>
    public static bool operator !=(EquatableImmutableArray<T> left, EquatableImmutableArray<T> right) => !left.Equals(right);
}

/// <summary>
/// An immutable, equatable array of <see cref="object?"/>. This is equivalent to <see cref="ImmutableArray{object?}"/> but with value equality support.
/// </summary>
/// <param name="array">The input <see cref="Array"/> to wrap.</param>
internal readonly struct EquatableImmutableArray(object?[] array, IEqualityComparer? comparer = null) : IEquatable<EquatableImmutableArray>, IReadOnlyList<object?>
{
    public static readonly EquatableImmutableArray Empty = new([]);

    /// <summary>
    /// The underlying <see cref="object?"/> array.
    /// </summary>
    private readonly object?[]? _array = array;

    private readonly IEqualityComparer _comparer = comparer ?? defaultComparer;

    public static implicit operator EquatableImmutableArray(object?[] array) => new(array);
    public static implicit operator ReadOnlySpan<object?>(EquatableImmutableArray array) => array.AsSpan();

    /// <summary>
    /// Gets the total number of elements in all the dimensions of the array.
    /// </summary>
    /// <returns>The total number of elements in all the dimensions of the array; zero if there are no elements in the array.</returns>
    /// <exception cref="OverflowException">The array is multidimensional and contains more than <see cref="int.MaxValue"/> elements.</exception>
    public int Length => _array?.Length ?? 0;

    /// <sinheritdoc/>
    public int Count => _array?.Length ?? 0;

    /// <sinheritdoc/>
    public object? this[int index] => _array![index];

    /// <sinheritdoc/>
    public bool Equals(EquatableImmutableArray array)
    {
        var lefts = AsSpan();
        var rights = array.AsSpan();

        int length = lefts.Length;
        if (length != rights.Length) return false;

        for (int i = 0; i < length; i++)
        {
            var left = lefts[i];
            var right = rights[i];

            if (!_comparer.Equals(left, right)) return false;
        }

        return true;
    }

    /// <sinheritdoc/>
    public override bool Equals(object? obj) => obj is EquatableImmutableArray other && Equals(this, other);

    /// <sinheritdoc/>
    public override int GetHashCode()
    {
        if (_array is not object?[] arr) return 0;

        HashCode hashCode = default;

        foreach (object? item in arr)
            hashCode.Add(_comparer.GetHashCode(item));

        return hashCode.ToHashCode();
    }

    /// <summary>
    /// Returns a <see cref="ReadOnlySpan{object?}"/> wrapping the current items.
    /// </summary>
    /// <returns>A <see cref="ReadOnlySpan{object?}"/> wrapping the current items.</returns>
    public ReadOnlySpan<object?> AsSpan() => _array.AsSpan();

    /// <summary>
    /// Gets the underlying array if there is one.
    /// </summary>
    public object?[]? GetArray() => _array;

    /// <sinheritdoc/>
    IEnumerator<object?> IEnumerable<object?>.GetEnumerator() => ((IEnumerable<object?>)(_array ?? [])).GetEnumerator();

    /// <sinheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<object?>)(_array ?? [])).GetEnumerator();

    /// <summary>
    /// Checks whether two <see cref="EquatableImmutableArray{object?}"/> values are the same.
    /// </summary>
    /// <param name="left">The first <see cref="EquatableImmutableArray{object?}"/> value.</param>
    /// <param name="right">The second <see cref="EquatableImmutableArray{object?}"/> value.</param>
    /// <returns>Whether <paramref name="left"/> and <paramref name="right"/> are equal.</returns>
    public static bool operator ==(EquatableImmutableArray left, EquatableImmutableArray right) => left.Equals(right);

    /// <summary>
    /// Checks whether two <see cref="EquatableImmutableArray{object?}"/> values are not the same.
    /// </summary>
    /// <param name="left">The first <see cref="EquatableImmutableArray{object?}"/> value.</param>
    /// <param name="right">The second <see cref="EquatableImmutableArray{object?}"/> value.</param>
    /// <returns>Whether <paramref name="left"/> and <paramref name="right"/> are not equal.</returns>
    public static bool operator !=(EquatableImmutableArray left, EquatableImmutableArray right) => !left.Equals(right);

    private static readonly DefaultComparer defaultComparer = new();

    private sealed class DefaultComparer : IEqualityComparer
    {
        public new bool Equals(object? x, object? y) => ((x is null) == (y is null)) && (x?.Equals(y) != false);

        public int GetHashCode(object? obj) => obj?.GetHashCode() ?? 0;
    }
}
