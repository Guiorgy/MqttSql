using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;

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
        public static bool ContainsAll<TSource>(this IEnumerable<TSource> source, IEnumerable<TSource> values)
        {
            return source.Intersect(values).Count() == values.Count();
        }
        public static bool ContainsAll<TSource>(this IEnumerable<TSource> source, params TSource[] values)
        {
            return source.Intersect(values).Count() == values.Length;
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
        //     The value associated with the specified key, if the key is
        //     found; otherwise, null.
        [return: MaybeNull]
        public static TValue GetValueOrNull<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key) where TValue : class
        {
            return dict.TryGetValue(key, out TValue? value) ? value : null;
        }

        //
        // Summary:
        //     Returns distinctly merged elements from a sequence by using the default
        //     equality comparer to compare values.
        //
        // Parameters:
        //   source:
        //     The sequence to merge duplicate elements in.
        //
        // Type parameters:
        //   TSource:
        //     The type of the elements of source.
        //
        // Returns:
        //     An System.Collections.Generic.IEnumerable`1 that contains distinctly merged
        //     elements from the source sequence.
        //
        // Exceptions:
        //   T:System.ArgumentNullException:
        //     source is null.
        public static IEnumerable<TSource> DistinctMerge<TSource>(this IEnumerable<TSource> source, IEqualityComparer<TSource>? comparer = null) where TSource : ICloneable, IMergeable<TSource>
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (comparer != null) throw new NotImplementedException("comparer");
            return DistinctMergeIterator(source, comparer);
        }

        private static IEnumerable<TSource> DistinctMergeIterator<TSource>(IEnumerable<TSource> source, IEqualityComparer<TSource>? comparer) where TSource : ICloneable, IMergeable<TSource>
        {
            HashSet<TSource> set = new(comparer);
            foreach (TSource element in source)
            {
                if (set.TryGetValue(element, out TSource? oldElement))
                {
                    oldElement.Merge(element);
                }
                else
                {
                    TSource newElement = (TSource)(element as ICloneable)!.Clone();
                    set.Add(newElement);
                    yield return newElement;
                }
            }
        }

        //
        // Summary:
        //     Creates an IAsyncEnumerable<IEnumerable<T>> that enables reading all of
        //     the data from the channel in batches.
        //
        // Parameters:
        //   reader:
        //     The ChannelReader to read from.
        //   cancellationToken:
        //     The Task cancellation token.
        // Source: https://stackoverflow.com/a/70698445/11427841
        public static async IAsyncEnumerable<List<T>> ReadBatchesAsync<T>(
            this ChannelReader<T> reader,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                yield return reader.Flush().ToList();
        }

        public static IEnumerable<T> Flush<T>(this ChannelReader<T> reader)
        {
            while (reader.TryRead(out T? item))
                yield return item;
        }
    }
}
