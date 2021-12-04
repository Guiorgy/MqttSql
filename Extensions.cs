using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace MqttSql
{
    public static class Extensions
    {
        //
        // Summary:
        //     Determines whether a sequence contains any of the specified elements
        //     by using the default equality comparer.
        //
        // Parameters:
        //   source:
        //     A sequence in which to locate a value.
        //
        //   values:
        //     The values to locate in the sequence.
        //
        // Type parameters:
        //   TSource:
        //     The type of the elements of source.
        //
        // Returns:
        //     true if the source sequence contains an element that has any of the
        //     specified values; otherwise, false.
        //
        // Exceptions:
        //   T:System.ArgumentNullException:
        //     source or values is null.
        public static bool ContainsAny<TSource>(this IEnumerable<TSource> source, IEnumerable<TSource> values)
        {
            return source.Intersect(values).Any();
        }
        public static bool ContainsAny<TSource>(this IEnumerable<TSource> source, params TSource[] values)
        {
            return source.Intersect(values).Any();
        }

        //
        // Summary:
        //     Determines whether a sequence contains all of the specified elements
        //     by using the default equality comparer.
        //
        // Parameters:
        //   source:
        //     A sequence in which to locate a value.
        //
        //   values:
        //     The values to locate in the sequence.
        //
        // Type parameters:
        //   TSource:
        //     The type of the elements of source.
        //
        // Returns:
        //     true if the source sequence contains elements that have the specified
        //     values; otherwise, false.
        //
        // Exceptions:
        //   T:System.ArgumentNullException:
        //     source or values is null.
        public static bool ContainsAal<TSource>(this IEnumerable<TSource> source, IEnumerable<TSource> values)
        {
            return source.Intersect(values).Count() == values.Count();
        }
        public static bool ContainsAal<TSource>(this IEnumerable<TSource> source, params TSource[] values)
        {
            return source.Intersect(values).Count() == values.Count();
        }

        //
        // Summary:
        //     Appends text to the start and end of every line in a string.
        //
        // Parameters:
        //   str:
        //     The string to modify.
        //
        //   before:
        //     The string that every line should start with.
        //
        //   after:
        //     The string that every line should end with.
        //
        // Returns:
        //     The modified string.
        public static string AppendToLines(this string str, string before, string after)
        {
            return
                before +
                string.Join(after + Environment.NewLine + before,
                    str.Split(Environment.NewLine)) +
                after;
        }

        //
        // Summary:
        //     Appends text to the start of every line in a string.
        //
        // Parameters:
        //   str:
        //     The string to modify.
        //
        //   before:
        //     The string that every line should start with.
        //
        // Returns:
        //     The modified string.
        public static string AppendBeforeLines(this string str, string before)
        {
            return
                before +
                string.Join(Environment.NewLine + before,
                    str.Split(Environment.NewLine));
        }

        //
        // Summary:
        //     Appends text to the end of every line in a string.
        //
        // Parameters:
        //   str:
        //     The string to modify.
        //
        //   after:
        //     The string that every line should end with.
        //
        // Returns:
        //     The modified string.
        public static string AppendAfterLines(this string str, string after)
        {
            return
                string.Join(after + Environment.NewLine,
                    str.Split(Environment.NewLine)) +
                after;
        }

        //
        // Summary:
        //     Gets the value associated with the specified key.
        //
        // Parameters:
        //   key:
        //     The key of the value to get.
        //
        // Returns:
        //     The value associated with the specified key, if the key is found;
        //     otherwise, null.
        [return: MaybeNull]
        public static TValue GetValueOrNull<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key) where TValue : class
        {
            return dict.TryGetValue(key, out TValue value) ? value : null;
        }
    }
}
