using MqttSql.src.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;

namespace MqttSql;

public static class Extensions
{
    /// <summary>
    /// Determines whether a sequence contains any of the specified elements by using the default equality comparer.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements of source.</typeparam>
    /// <param name="source">A sequence in which to locate a value.</param>
    /// <param name="values">The values to locate in the sequence.</param>
    /// <returns><see langword="true"/> if the <paramref name="source"/> sequence contains any of the specified values; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="values"/> is <see langword="null"/>.</exception>
    public static bool ContainsAny<TSource>(this IEnumerable<TSource> source, IEnumerable<TSource> values)
    {
        return source.Intersect(values).Any();
    }

    /// <summary>
    /// Determines whether a sequence contains any of the specified elements by using the default equality comparer.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements of source.</typeparam>
    /// <param name="source">A sequence in which to locate a value.</param>
    /// <param name="values">The values to locate in the sequence.</param>
    /// <returns><see langword="true"/> if the <paramref name="source"/> sequence contains any of the specified values; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="values"/> is <see langword="null"/>.</exception>
    public static bool ContainsAny<TSource>(this IEnumerable<TSource> source, params TSource[] values)
    {
        return source.Intersect(values).Any();
    }

    /// <summary>
    /// Determines whether a sequence contains all of the specified elements by using the default equality comparer.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements of source.</typeparam>
    /// <param name="source">A sequence in which to locate a value.</param>
    /// <param name="values">The values to locate in the sequence.</param>
    /// <returns><see langword="true"/> if the <paramref name="source"/> sequence contains all of the specified values; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="values"/> is <see langword="null"/>.</exception>
    public static bool ContainsAll<TSource>(this IEnumerable<TSource> source, IEnumerable<TSource> values)
    {
        return source.Intersect(values).Count() == values.Count();
    }

    /// <summary>
    /// Determines whether a sequence contains all of the specified elements by using the default equality comparer.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements of source.</typeparam>
    /// <param name="source">A sequence in which to locate a value.</param>
    /// <param name="values">The values to locate in the sequence.</param>
    /// <returns><see langword="true"/> if the <paramref name="source"/> sequence contains all of the specified values; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="values"/> is <see langword="null"/>.</exception>
    public static bool ContainsAll<TSource>(this IEnumerable<TSource> source, params TSource[] values)
    {
        return source.Intersect(values).Count() == values.Length;
    }

    /// <summary>
    /// Appends text to the start and end of every line in a string.
    /// </summary>
    /// <param name="str">The string to modify.</param>
    /// <param name="before">The string that every line should start with.</param>
    /// <param name="after">The string that every line should end with.</param>
    /// <returns>The modified string.</returns>
    public static string AppendToLines(this string str, string before, string after)
    {
        return
            before +
            string.Join(after + Environment.NewLine + before,
                str.Split(Environment.NewLine)) +
            after;
    }

    /// <summary>
    /// Appends text to the start of every line in a string.
    /// </summary>
    /// <param name="str">The string to modify.</param>
    /// <param name="before">The string that every line should start with.</param>
    /// <returns>The modified string.</returns>
    public static string AppendBeforeLines(this string str, string before)
    {
        return
            before +
            string.Join(Environment.NewLine + before,
                str.Split(Environment.NewLine));
    }

    /// <summary>
    /// Appends text to the end of every line in a string.
    /// </summary>
    /// <param name="str">The string to modify.</param>
    /// <param name="after">The string that every line should end with.</param>
    /// <returns>The modified string.</returns>
    public static string AppendAfterLines(this string str, string after)
    {
        return
            string.Join(after + Environment.NewLine,
                str.Split(Environment.NewLine)) +
            after;
    }

    /// <summary>
    /// Gets the value associated with the specified key.
    /// </summary>
    /// <typeparam name="TKey">The type of the keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of the values in the dictionary.</typeparam>
    /// <param name="dict">The dictionary to search in.</param>
    /// <param name="key">The key of the value to get.</param>
    /// <returns>The value associated with the specified key, if the key is found; otherwise, <see langword="null"/>.</returns>
    [return: MaybeNull]
    public static TValue GetValueOrNull<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key) where TValue : class
    {
        return dict.TryGetValue(key, out TValue? value) ? value : null;
    }

    /// <summary>
    /// Creates an <see cref="IAsyncEnumerable{List{T}}"/> that enables reading all of the data from the channel in batches.
    /// </summary>
    /// <typeparam name="T">The type of the channel.</typeparam>
    /// <param name="reader">The channel to be read.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the wait operation.</param>
    /// <returns>The <see cref="IAsyncEnumerable{List{T}}"/> that can be awaited.</returns>
    /// <remarks><seealso href="https://stackoverflow.com/a/70698445/11427841">Source at StackOverflow</seealso></remarks>
    public static async IAsyncEnumerable<List<T>> ReadBatchesAsync<T>(
        this ChannelReader<T> reader,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            yield return reader.Flush().ToList();
    }

    /// <summary>
    /// Read all elements inside a <see cref="ChannelReader{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the channel.</typeparam>
    /// <param name="reader">The channel to be read.</param>
    /// <returns>An <see cref="IEnumerable{T}"/> with elements from the channel.</returns>
    /// <remarks><seealso href="https://stackoverflow.com/a/70698445/11427841">Source at StackOverflow</seealso></remarks>
    public static IEnumerable<T> Flush<T>(this ChannelReader<T> reader)
    {
        while (reader.TryRead(out T? item))
            yield return item;
    }

    private static readonly DateTime SampleDateTime = new(2022, 04, 26, 16, 10, 30, 500, DateTimeKind.Utc);

    /// <summary>
    /// Validates <see cref="DateTime"/> formats.
    /// </summary>
    /// <remarks>
    /// A <see cref="DateTime"/> format will be considered valid if:
    ///   - The format string is not empty/white space.
    ///   - Converting <see cref="DateTime.Now"/> to string and parsing it back doesn't throw an exception.
    ///   - Converting <see cref="DateTime.Now"/> to string and parsing it back retains at least some information,
    ///     i.e. the round-trip parsed <see cref="DateTime"/> shouldn't be equal to the <see cref="default"/>.
    ///     <c>((DateTime)default).ToString("yyyy/MM/dd HH:mm:ss.fff", CultureInfo.InvariantCulture) = "0001/01/01 00:00:00.000"</c>
    /// </remarks>
    /// <param name="format">The foramt to validate.</param>
    /// <returns><see langword="true"/> if the <paramref name="format"/> passes validations; otherwise, <see langword="false"/>.</returns>
    public static bool IsValidDateTimeFormat(this string format)
    {
        if (string.IsNullOrWhiteSpace(format)) return false;
        try
        {
            var dt = DateTime.ParseExact(
                SampleDateTime.ToString(format, CultureInfo.InvariantCulture),
                format,
                CultureInfo.InvariantCulture,
                DateTimeStyles.NoCurrentDateDefault);
            return dt != default;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Projects each element of a sequence to an <see cref="IEnumerable{T}/>"/> and flattens the resulting sequences into one sequence.
    /// </summary>
    /// <typeparam name="TResult">The type of the elements of the resulting sequence.</typeparam>
    /// <param name="source">The sequence to be flattened.</param>
    /// <returns>An <see cref="IEnumerable{T}/>"/> whose elements are the result of flattening the source sequence.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/></exception>
    public static IEnumerable<TResult> Flatten<TResult>(this IEnumerable<IEnumerable<TResult>> source)
    {
        return source.SelectMany(x => x);
    }

    /// <summary>
    /// Converts <see cref="DateTime"/> into an ISO 8601:2004 or ISO 8601-1:2019 (RFC 3339) string faster than <see cref="DateTime.ToString"/>.
    /// </summary>
    /// <param name="dateTime">the <see cref="DateTime"/> to be converted.</param>
    /// <param name="milliseconds">if <see langword="false"/>, milliseconds won't be shown.</param>
    /// <param name="strictDateTimeDelimiter">if <see langword="true"/>, the letter <c>'T'</c> will be used as the delimiter between date and time as per ISO 8601-1:2019.</param>
    /// <param name="omitDelimiters">if <see langword="true"/>, delimiters will be omited, except for the letter <c>'T'</c> between date and time when <paramref name="strictDateTimeDelimiter"/> is <see langword="true"/>.</param>
    /// <returns>The <see cref="DateTime"/> in the ISO 8601:2004/ISO 8601-1:2019 format.</returns>
    /// <remarks>
    /// Possible formats using this method:
    /// <list type="bullet">
    /// <item>
    /// <description>"yyyy-MM-dd HH:mm:ss" => <c>dateTime.ToIsoString(milliseconds: false, strictDateTimeDelimiter: false, omitDelimiters: false)</c></description>
    /// </item>
    /// <item>
    /// <description>"yyyy-MM-dd HH:mm:ss.fff" => <c>dateTime.ToIsoString(milliseconds: true, strictDateTimeDelimiter: false, omitDelimiters: false)</c></description>
    /// </item>
    /// <item>
    /// <description>"yyyy-MM-ddTHH:mm:ss" => <c>dateTime.ToIsoString(milliseconds: false, strictDateTimeDelimiter: true, omitDelimiters: false)</c></description>
    /// </item>
    /// <item>
    /// <description>"yyyy-MM-ddTHH:mm:ss.fff" => <c>dateTime.ToIsoString(milliseconds: true, strictDateTimeDelimiter: true, omitDelimiters: false)</c></description>
    /// </item>
    /// <item>
    /// <description>"yyyyMMddTHHmmss" => <c>dateTime.ToIsoString(milliseconds: false, strictDateTimeDelimiter: true, omitDelimiters: true)</c></description>
    /// </item>
    /// <item>
    /// <description>"yyyyMMddTHHmmssfff" => <c>dateTime.ToIsoString(milliseconds: true, strictDateTimeDelimiter: true, omitDelimiters: true)</c></description>
    /// </item>
    /// <item>
    /// <description>"yyyyMMddHHmmss" => <c>dateTime.ToIsoString(milliseconds: false, strictDateTimeDelimiter: false, omitDelimiters: true)</c></description>
    /// </item>
    /// <item>
    /// <description>"yyyyMMddHHmmssfff" => <c>dateTime.ToIsoString(milliseconds: true, strictDateTimeDelimiter: false, omitDelimiters: true)</c></description>
    /// </item>
    /// </list>
    /// </remarks>
    public static string ToIsoString(this DateTime dateTime, bool milliseconds = false, bool strictDateTimeDelimiter = false, bool omitDelimiters = false)
    {
        static char DigitToAsciiChar(int digit) => (char)('0' + digit);

        static void Write2Digits(Span<char> chars, int offset, int value)
        {
            int firstDigit = value / 10;
            int secondDigit = value - (firstDigit * 10);

            chars[offset] = DigitToAsciiChar(firstDigit);
            chars[offset + 1] = DigitToAsciiChar(secondDigit);
        }

        static void Write2DigitsAndPostfix(Span<char> chars, int offset, int value, char postfix)
        {
            Write2Digits(chars, offset, value);

            chars[offset + 2] = postfix;
        }

        static void Write3Digits(Span<char> chars, int offset, int value)
        {
            int firstDigit = value / 100;
            value -= firstDigit * 100;
            int secondDigit = value / 10;
            int thirdDigit = value - (secondDigit * 10);

            chars[offset] = DigitToAsciiChar(firstDigit);
            chars[offset + 1] = DigitToAsciiChar(secondDigit);
            chars[offset + 2] = DigitToAsciiChar(thirdDigit);
        }

        /*static void Write3DigitsAndPostrfix(Span<char> chars, int offset, int value, char postfix)
        {
            Write3Digits(chars, offset, value);

            chars[offset + 3] = postfix;
        }*/

        static void Write4Digits(Span<char> chars, int offset, int value)
        {
            int firstDigit = value / 1000;
            value -= firstDigit * 1000;
            int secondDigit = value / 100;
            value -= secondDigit * 100;
            int thirdDigit = value / 10;
            int fourthDigit = value - (thirdDigit * 10);

            chars[offset] = DigitToAsciiChar(firstDigit);
            chars[offset + 1] = DigitToAsciiChar(secondDigit);
            chars[offset + 2] = DigitToAsciiChar(thirdDigit);
            chars[offset + 3] = DigitToAsciiChar(fourthDigit);
        }

        static void Write4DigitsAndPostfix(Span<char> chars, int offset, int value, char postfix)
        {
            Write4Digits(chars, offset, value);

            chars[offset + 4] = postfix;
        }

        if (omitDelimiters)
        {
            int length = 14 + (strictDateTimeDelimiter ? 1 : 0) + (milliseconds ? 3 : 0);

            return string.Create(length, (dateTime, strictDateTimeDelimiter, milliseconds), (chars, state) =>
            {
                (var _dateTime, var _strictDelimiter, var _milliseconds) = state;

                Write4Digits(chars, 0, _dateTime.Year);
                Write2Digits(chars, 4, _dateTime.Month);

                if (_strictDelimiter) Write2DigitsAndPostfix(chars, 6, _dateTime.Day, 'T');
                else Write2Digits(chars, 6, _dateTime.Day);
                int tOffset = _strictDelimiter ? 1 : 0;

                Write2Digits(chars, 8 + tOffset, _dateTime.Hour);
                Write2Digits(chars, 10 + tOffset, _dateTime.Minute);
                Write2Digits(chars, 12 + tOffset, _dateTime.Second);

                if (_milliseconds) Write3Digits(chars, 14 + tOffset, _dateTime.Millisecond);
            });
        }
        else
        {
            int length = 19 + (milliseconds ? 4 : 0);

            return string.Create(length, (dateTime, strictDateTimeDelimiter, milliseconds), (chars, state) =>
            {
                (var _dateTime, var _strictDelimiter, var _milliseconds) = state;

                Write4DigitsAndPostfix(chars, 0, _dateTime.Year, '-');
                Write2DigitsAndPostfix(chars, 5, _dateTime.Month, '-');
                Write2DigitsAndPostfix(chars, 8, _dateTime.Day, _strictDelimiter ? 'T' : ' ');
                Write2DigitsAndPostfix(chars, 11, _dateTime.Hour, ':');
                Write2DigitsAndPostfix(chars, 14, _dateTime.Minute, ':');

                if (_milliseconds)
                {
                    Write2DigitsAndPostfix(chars, 17, _dateTime.Second, '.');
                    Write3Digits(chars, 20, _dateTime.Millisecond);
                }
                else
                {
                    Write2Digits(chars, 17, _dateTime.Second);
                }
            });
        }
    }

    /// <summary>
    /// Converts <see cref="DateTime"/> into an ISO 8601:2004 string (<c>yyyy-MM-dd HH:mm:ss.fff</c>) faster than <see cref="DateTime.ToString"/>.
    /// </summary>
    /// <param name="dateTime">the <see cref="DateTime"/> to be converted.</param>
    /// <param name="milliseconds">if <see langword="false"/>, milliseconds won't be shown (<c>yyyy-MM-dd HH:mm:ss</c>).</param>
    /// <param name="strictDateTimeDelimiter">if <see langword="true"/>, <c>'T'</c> will be used as the delimiter between date and time as per ISO 8601-1:2019 (<c>yyyy-MM-ddTHH:mm:ss.fff</c>).</param>
    /// <param name="omitDelimiters">if <see langword="true"/>, delimiters will be omited, except for <c>'T'</c> between date and time when
    /// <paramref name="strictDateTimeDelimiter"/> is <see langword="true"/> (<c>yyyyMMddHHmmssfff</c>, <c>yyyyMMddTHHmmssfff</c>).</param>
    /// <returns>The <see cref="DateTime"/> in the ISO 8601:2004 format.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="dateTime"/> is null.</exception>
    public static string ToIsoString(this DateTime? dateTime, bool milliseconds = true, bool strictDateTimeDelimiter = false, bool omitDelimiters = false)
    {
        ArgumentNullException.ThrowIfNull(dateTime);

        DateTime _dateTime = (DateTime)dateTime;

        return ToIsoString(_dateTime, milliseconds, strictDateTimeDelimiter, omitDelimiters);
    }

    /// <summary>
    /// Converts the ISO 8601:2004 (<c>yyyy-MM-dd HH:mm:ss.fff</c>) string representation of a date and time to its <see cref="DateTime"/> equivalent faster than <see cref="DateTime.TryParseExact"/>.
    /// </summary>
    /// <param name="isoDateTimeString">The string containing a date and time to convert.</param>
    /// <param name="milliseconds">Whether the string contains milliseconds.</param>
    /// <param name="noDateTimeDelimiter">if <see langword="true"/>, the delimiter between date and time (<c>'T'</c>) is assumed to be omitted (<c>yyyy-MM-ddHH:mm:ss.fff</c>).</param>
    /// <param name="noDelimiters">if <see langword="true"/>, all delimiters, except for <c>'T'</c> between date and time when
    /// <paramref name="noDateTimeDelimiter"/> is <see langword="false"/>, are assumed to be omitted (<c>yyyyMMddHHmmssfff</c>, <c>yyyyMMddTHHmmssfff</c>).</param>
    /// <returns><see langword="true"/> if <paramref name="isoDateTimeString"/> was converted successfully; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// Possible formats using this method:
    /// <list type="bullet">
    /// <item>
    /// <description>"yyyy-MM-ddTHH:mm:ss" => <c>isoDateTimeString.TryParseIsoDateTime(out DateTime dateTime, milliseconds: false, noDateTimeDelimiter: false, noDelimiters: false)</c></description>
    /// </item>
    /// <item>
    /// <description>"yyyy-MM-ddTHH:mm:ss.fff" => <c>isoDateTimeString.TryParseIsoDateTime(out DateTime dateTime, milliseconds: true, noDateTimeDelimiter: false, noDelimiters: false)</c></description>
    /// </item>
    /// <item>
    /// <description>"yyyyMMddTHHmmss" => <c>isoDateTimeString.TryParseIsoDateTime(out DateTime dateTime, milliseconds: false, noDateTimeDelimiter: false, noDelimiters: true)</c></description>
    /// </item>
    /// <item>
    /// <description>"yyyyMMddTHHmmssfff" => <c>isoDateTimeString.TryParseIsoDateTime(out DateTime dateTime, milliseconds: true, noDateTimeDelimiter: false, noDelimiters: true)</c></description>
    /// </item>
    /// <item>
    /// <description>"yyyyMMddHHmmss" => <c>isoDateTimeString.TryParseIsoDateTime(out DateTime dateTime, milliseconds: false, noDateTimeDelimiter: true, noDelimiters: true)</c></description>
    /// </item>
    /// <item>
    /// <description>"yyyyMMddHHmmssfff" => <c>isoDateTimeString.TryParseIsoDateTime(out DateTime dateTime, milliseconds: true, noDateTimeDelimiter: true, noDelimiters: true)</c></description>
    /// </item>
    /// <item>
    /// <description>"yyyy-MM-ddHH:mm:ss" => <c>isoDateTimeString.TryParseIsoDateTime(out DateTime dateTime, milliseconds: false, noDateTimeDelimiter: true, noDelimiters: false)</c></description>
    /// </item>
    /// <item>
    /// <description>"yyyy-MM-ddHH:mm:ss.fff" => <c>isoDateTimeString.TryParseIsoDateTime(out DateTime dateTime, milliseconds: true, noDateTimeDelimiter: true, noDelimiters: false)</c></description>
    /// </item>
    /// </list>
    /// </remarks>
    public static bool TryParseIsoDateTime(this string isoDateTimeString, out DateTime dateTime, bool milliseconds = false, bool noDateTimeDelimiter = false, bool noDelimiters = false)
    {
        int expectedLength = (milliseconds, noDateTimeDelimiter, noDelimiters) switch
        {
            (false, false, false) => 19, // yyyy-MM-ddTHH:mm:ss
            (true, false, false) => 23, // yyyy-MM-ddTHH:mm:ss.fff
            (false, false, true) => 15, // yyyyMMddTHHmmss
            (true, false, true) => 18, // yyyyMMddTHHmmssfff
            (false, true, true) => 14, // yyyyMMddHHmmss
            (true, true, true) => 17, // yyyyMMddHHmmssfff
            (false, true, false) => 18, // yyyy-MM-ddHH:mm:ss
            (true, true, false) => 22, // yyyy-MM-ddHH:mm:ss.fff
        };

        if (isoDateTimeString == null || isoDateTimeString.Length != expectedLength)
        {
            dateTime = default;
            return false;
        }

        static int AsciiCharToDigit(char digit) => digit - '0';

        static bool IsDigit(int i) => 0 <= i && i <= 9;

        static bool TryRead4Digits(ref ReadOnlySpan<char> chars, bool skipNext, out int value)
        {
            int a = AsciiCharToDigit(chars[0]);
            int b = AsciiCharToDigit(chars[1]);
            int c = AsciiCharToDigit(chars[2]);
            int d = AsciiCharToDigit(chars[3]);

            if (!IsDigit(a) || !IsDigit(b) || !IsDigit(c) || !IsDigit(d))
            {
                value = 0;
                return false;
            }

            chars = chars[(skipNext ? 5 : 4)..];

            value = (a * 1000) + (b * 100) + (c * 10) + d;
            return true;
        }

        static bool TryRead3Digits(ref ReadOnlySpan<char> chars, bool skipNext, out int value)
        {
            int a = AsciiCharToDigit(chars[0]);
            int b = AsciiCharToDigit(chars[1]);
            int c = AsciiCharToDigit(chars[2]);
            if (!IsDigit(a) || !IsDigit(b) || !IsDigit(c))
            {
                value = 0;
                return false;
            }
            chars = chars[(skipNext ? 4 : 3)..];
            value = (a * 100) + (b * 10) + c;
            return true;
        }

        static bool TryRead2Digits(ref ReadOnlySpan<char> chars, bool skipNext, out int value)
        {
            int a = AsciiCharToDigit(chars[0]);
            int b = AsciiCharToDigit(chars[1]);

            if (!IsDigit(a) || !IsDigit(b))
            {
                value = 0;
                return false;
            }

            chars = chars[(skipNext ? 3 : 2)..];

            value = (a * 10) + b;
            return true;
        }

        ReadOnlySpan<char> isoSpan = isoDateTimeString.AsSpan();

        int millisecond = 0;
        if (TryRead4Digits(ref isoSpan, !noDelimiters, out int year)
            && TryRead2Digits(ref isoSpan, !noDelimiters, out int month)
            && TryRead2Digits(ref isoSpan, !noDateTimeDelimiter, out int day)
            && TryRead2Digits(ref isoSpan, !noDelimiters, out int hour)
            && TryRead2Digits(ref isoSpan, !noDelimiters, out int minute)
            && TryRead2Digits(ref isoSpan, !noDelimiters && milliseconds, out int second)
            && (!milliseconds || TryRead3Digits(ref isoSpan, false, out millisecond)))
        {
            dateTime = new DateTime(year, month, day, hour, minute, second, millisecond, DateTimeKind.Unspecified);
            return true;
        }
        else
        {
            dateTime = default;
            return false;
        }
    }

    /// <summary>
    /// Converts the ISO 8601:2004 (<c>yyyy-MM-dd HH:mm:ss.fff</c>) string representation of a date and time to its <see cref="DateTime"/> equivalent faster than <see cref="DateTime.TryParseExact"/>.
    /// </summary>
    /// <param name="isoDateTimeString">The string containing a date and time to convert.</param>
    /// <param name="milliseconds">Whether the string contains milliseconds.</param>
    /// <param name="noDateTimeDelimiter">if <see langword="true"/>, the delimiter between date and time (<c>'T'</c>) is assumed to be omitted (<c>yyyy-MM-ddHH:mm:ss.fff</c>).</param>
    /// <param name="noDelimiters">if <see langword="true"/>, all delimiters, except for <c>'T'</c> between date and time when
    /// <paramref name="noDateTimeDelimiter"/> is <see langword="false"/>, are assumed to be omitted (<c>yyyyMMddHHmmssfff</c>, <c>yyyyMMddTHHmmssfff</c>).</param>
    /// <returns>An object that is equivalent to the date and time contained in <paramref name="isoDateTimeString"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="isoDateTimeString"/> is null.</exception>
    /// <exception cref="FormatException"><paramref name="isoDateTimeString"/> is not in the correct ISO format.</exception>
    /// <remarks>
    /// Possible formats using this method:
    /// <list type="bullet">
    /// <item>
    /// <description>"yyyy-MM-ddTHH:mm:ss" => <c>isoDateTimeString.ParseIsoDateTime(milliseconds: false, noDateTimeDelimiter: false, noDelimiters: false)</c></description>
    /// </item>
    /// <item>
    /// <description>"yyyy-MM-ddTHH:mm:ss.fff" => <c>isoDateTimeString.ParseIsoDateTime(milliseconds: true, noDateTimeDelimiter: false, noDelimiters: false)</c></description>
    /// </item>
    /// <item>
    /// <description>"yyyyMMddTHHmmss" => <c>isoDateTimeString.ParseIsoDateTime(milliseconds: false, noDateTimeDelimiter: false, noDelimiters: true)</c></description>
    /// </item>
    /// <item>
    /// <description>"yyyyMMddTHHmmssfff" => <c>isoDateTimeString.ParseIsoDateTime(milliseconds: true, noDateTimeDelimiter: false, noDelimiters: true)</c></description>
    /// </item>
    /// <item>
    /// <description>"yyyyMMddHHmmss" => <c>isoDateTimeString.ParseIsoDateTime(milliseconds: false, noDateTimeDelimiter: true, noDelimiters: true)</c></description>
    /// </item>
    /// <item>
    /// <description>"yyyyMMddHHmmssfff" => <c>isoDateTimeString.ParseIsoDateTime(milliseconds: true, noDateTimeDelimiter: true, noDelimiters: true)</c></description>
    /// </item>
    /// <item>
    /// <description>"yyyy-MM-ddHH:mm:ss" => <c>isoDateTimeString.ParseIsoDateTime(milliseconds: false, noDateTimeDelimiter: true, noDelimiters: false)</c></description>
    /// </item>
    /// <item>
    /// <description>"yyyy-MM-ddHH:mm:ss.fff" => <c>isoDateTimeString.ParseIsoDateTime(milliseconds: true, noDateTimeDelimiter: true, noDelimiters: false)</c></description>
    /// </item>
    /// </list>
    /// </remarks>
    public static DateTime ParseIsoDateTime(this string isoDateTimeString, bool milliseconds = false, bool noDateTimeDelimiter = false, bool noDelimiters = false)
    {
        ArgumentNullException.ThrowIfNull(isoDateTimeString);

        if (!TryParseIsoDateTime(isoDateTimeString, out DateTime dateTime, milliseconds, noDateTimeDelimiter, noDelimiters))
            throw new FormatException("String is not in the correct ISO format");

        return dateTime;
    }

    /// <summary>
    /// Appends the <paramref name="number"/> as a string followed by the default line terminator to the end of the current <see cref="StringBuilder"/> object.
    /// </summary>
    /// <param name="builder">The <see cref="StringBuilder"/> to append to.</param>
    /// <param name="number">The number to append.</param>
    /// <returns>A reference to <paramref name="builder"/> after the append operation has completed.</returns>
    public static StringBuilder AppendLine(this StringBuilder builder, int number)
    {
        return builder.AppendLine(number.ToString());
    }

    /// <summary>
    /// Appends the <paramref name="character"/> as a string followed by the default line terminator to the end of the current <see cref="StringBuilder"/> object.
    /// </summary>
    /// <param name="builder">The <see cref="StringBuilder"/> to append to.</param>
    /// <param name="character">The character to append.</param>
    /// <returns>A reference to <paramref name="builder"/> after the append operation has completed.</returns>
    public static StringBuilder AppendLine(this StringBuilder builder, char character)
    {
        return builder.AppendLine(character.ToString());
    }

    /// <summary>
    /// Appends the <paramref name="boolean"/> as a string followed by the default line terminator to the end of the current <see cref="StringBuilder"/> object.
    /// </summary>
    /// <param name="builder">The <see cref="StringBuilder"/> to append to.</param>
    /// <param name="boolean">The boolean to append.</param>
    /// <returns>A reference to <paramref name="builder"/> after the append operation has completed.</returns>
    public static StringBuilder AppendLine(this StringBuilder builder, bool boolean)
    {
        return builder.AppendLine(boolean.ToString());
    }

    /// <summary>
    /// Appends the <paramref name="boolean"/> as a string followed by the default line terminator to the end of the current <see cref="StringBuilder"/> object.
    /// </summary>
    /// <param name="builder">The <see cref="StringBuilder"/> to append to.</param>
    /// <param name="boolean">The boolean to append.</param>
    /// <param name="falseString">The string to append when <paramref name="boolean"/> is <see langword="false"/>.</param>
    /// <param name="trueString">The string to append when <paramref name="boolean"/> is <see langword="true"/>.</param>
    /// <returns>A reference to <paramref name="builder"/> after the append operation has completed.</returns>
    public static StringBuilder AppendLine(this StringBuilder builder, bool boolean, string falseString, string trueString)
    {
        return builder.AppendLine(boolean ? trueString : falseString);
    }

    /// <summary>
    /// Appends the <see cref="IAppendStringBuilder"/> as a string followed by the default line terminator to the end of the current <see cref="StringBuilder"/>
    /// </summary>
    /// <param name="builder">The <see cref="StringBuilder"/> to append to.</param>
    /// <param name="appendStringBuilder">The <see cref="IAppendStringBuilder"/> to append.</param>
    /// <returns>A reference to <paramref name="builder"/> after the append operation has completed.</returns>
    public static StringBuilder AppendLine(this StringBuilder builder, IAppendStringBuilder appendStringBuilder)
    {
        // AppendStringBuilder already appends a new line at the end
        return appendStringBuilder.AppendStringBuilder(builder);
    }

    /// <summary>
    /// Appends the array of <see cref="IAppendStringBuilder"/> as a string followed by the default line terminator to the end of the current <see cref="StringBuilder"/>
    /// </summary>
    /// <param name="builder">The <see cref="StringBuilder"/> to append to.</param>
    /// <param name="appendStringBuilders">The array of <see cref="IAppendStringBuilder"/> to append.</param>
    /// <param name="appendLineAfterElement">If <see langword="true"/>, an additional new line will be appended after each element.</param>
    /// <param name="appendLineIfEmpty">If <see langword="true"/>, a new line will be appended even if the array is empty.</param>
    /// <returns>A reference to <paramref name="builder"/> after the append operation has completed.</returns>
    public static StringBuilder AppendLine(this StringBuilder builder, IAppendStringBuilder[] appendStringBuilders, bool appendLineAfterElement = false, bool appendLineIfEmpty = false)
    {
        if (appendStringBuilders.Length == 0)
        {
            if (appendLineIfEmpty) builder.AppendLine();
        }
        else
        {
            if (appendLineAfterElement)
            {
                foreach (var appendStringBuilder in appendStringBuilders)
                    builder.AppendLine(appendStringBuilder).AppendLine();
            }
            else
            {
                foreach (var appendStringBuilder in appendStringBuilders)
                    builder.AppendLine(appendStringBuilder);
            }
        }

        return builder;
    }

    /// <summary>
    /// Converts an array of type <typeparamref name="T"/> to a string that starts with <paramref name="open"/> and ends with <paramref name="close"/>,
    /// each element is prefixed with <paramref name="prefix"/> and postfixed with <paramref name="postfix"/>, and elements are separated with <paramref name="separator"/>.
    /// </summary>
    /// <typeparam name="T">The element type of the array.</typeparam>
    /// <param name="values">The array to convert.</param>
    /// <param name="open">The string with which the resulting string should be prefixed.</param>
    /// <param name="prefix">The string with which every element should be prefixed.</param>
    /// <param name="postfix">The string with which every element should be postfixed.</param>
    /// <param name="separator">The string with which every element should be separated.</param>
    /// <param name="close">The string with which the resulting string should be postfixed.</param>
    /// <param name="prefixPostfixLines">If <see langword="true"/>, every element inside <paramref name="values"/> will be split into lines, and
    /// every line will be prefixed and postfixed with <paramref name="prefix"/> and <paramref name="postfix"/> separately.</param>
    /// <returns>The string representation of the array in the given format.</returns>
    public static string ToString<T>(this T[] values, string? open = null, string? prefix = null, string? postfix = null, string separator = ", ", string? close = null, bool prefixPostfixLines = false)
    {
        IEnumerable<T> _values = values;
        return _values.ToString(open, prefix, postfix, separator, close, prefixPostfixLines);
    }

    /// <summary>
    /// Converts a sequence of type <typeparamref name="T"/> to a string that starts with <paramref name="open"/> and ends with <paramref name="close"/>,
    /// each element is prefixed with <paramref name="prefix"/> and postfixed with <paramref name="postfix"/>, and elements are separated with <paramref name="separator"/>.
    /// </summary>
    /// <typeparam name="T">The element type of the sequence.</typeparam>
    /// <param name="values">The sequence to convert.</param>
    /// <param name="open">The string with which the resulting string should be prefixed.</param>
    /// <param name="prefix">The string with which every element should be postfixed prefixed.</param>
    /// <param name="postfix">The string with which every element should end.</param>
    /// <param name="separator">The string with which every element should be separated.</param>
    /// <param name="close">The string with which the resulting string should be postfixed.</param>
    /// <param name="prefixPostfixLines">If <see langword="true"/>, every element inside <paramref name="values"/> will be split into lines, and
    /// every line will be prefixed and postfixed with <paramref name="prefix"/> and <paramref name="postfix"/> separately.</param>
    /// <returns>The string representation of the sequence in the given format.</returns>
    public static string ToString<T>(this IEnumerable<T> values, string? open = null, string? prefix = null, string? postfix = null, string separator = ", ", string? close = null, bool prefixPostfixLines = false)
    {
        IEnumerable<string?> _values;

        if (prefixPostfixLines)
        {
            if (prefix == null && postfix == null)
                _values = values.Select(value => value?.ToString());
            else if (prefix != null && postfix != null)
                _values = values.Select(value => value?.ToString()?.AppendToLines(prefix, postfix));
            else if (prefix != null/* && postfix == null*/)
                _values = values.Select(value => value?.ToString()?.AppendBeforeLines(prefix));
            else // prefix == null && postfix != null
                _values = values.Select(value => value?.ToString()?.AppendAfterLines(postfix!));
        }
        else
        {
            if (prefix == null && postfix == null)
                _values = values.Select(value => $"{value}");
            else if (prefix != null && postfix != null)
                _values = values.Select(value => $"{prefix}{value}{postfix}");
            else if (prefix != null/* && postfix == null*/)
                _values = values.Select(value => $"{prefix}{value}");
            else // prefix == null && postfix != null
                _values = values.Select(value => $"{value}{postfix!}");
        }

        if (open != null && close != null)
            return $"{open}{string.Join(separator, _values)}{close}";
        else if (open == null && close == null)
            return string.Join(separator, _values);
        else if (open != null/* && close == null*/)
            return $"{open}{string.Join(separator, _values)}";
        else // open == null && close != null
            return $"{string.Join(separator, _values)}{close!}";
    }

    /// <summary>
    /// Check if the given string is equal to one of the given strings ignoring case.
    /// </summary>
    /// <param name="value">The string to search.</param>
    /// <param name="values">The strings to search in.</param>
    /// <returns>Whether <paramref name="value"/> was found inside <paramref name="values"/>.</returns>
    public static bool IsInIgnoreCase(this string value, params string[] values)
    {
        if (values == null || values.Length == 0) return false;

        return values.Contains(value, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Check if the given string is equal to one of the given strings.
    /// </summary>
    /// <param name="value">The string to search.</param>
    /// <param name="values">The strings to search in.</param>
    /// <returns>Whether <paramref name="value"/> was found inside <paramref name="values"/>.</returns>
    public static bool IsIn(this string value, params string[] values)
    {
        if (values == null || values.Length == 0) return false;

        return values.Contains(value);
    }

    /// <summary>
    /// Check if the given object is equal to one of the given objects.
    /// </summary>
    /// <typeparam name="T">The type of <paramref name="value"/> and elements inside <paramref name="values"/>.</typeparam>
    /// <param name="value">The object to search.</param>
    /// <param name="values">The objects to search in.</param>
    /// <returns>Whether <paramref name="value"/> was found inside <paramref name="values"/>.</returns>
    public static bool IsIn<T>(this T value, params T[] values)
    {
        if (values == null || values.Length == 0) return false;

        return values.Contains(value);
    }

    /// <summary>
    /// Check if the given object is equal to one of the given objects.
    /// </summary>
    /// <typeparam name="T">The type of <paramref name="value"/> and elements inside <paramref name="values"/>.</typeparam>
    /// <param name="value">The object to search.</param>
    /// <param name="comparer">The <see cref="IEqualityComparer{T}"/> to use to compare objects.</param>
    /// <param name="values">The objects to search in.</param>
    /// <returns>Whether <paramref name="value"/> was found inside <paramref name="values"/>.</returns>
    public static bool IsIn<T>(this T value, IEqualityComparer<T> comparer, params T[] values)
    {
        if (values == null || values.Length == 0) return false;

        return values.Contains(value, comparer);
    }

    /// <summary>
    /// Returns a new string in which a segment at a specified character position and with a specified length in the current instance is replaced with
    /// another specified string.
    /// </summary>
    /// <param name="source">The source string to modify.</param>
    /// <param name="startIndex">The zero-based starting character position of a segment in this instance.</param>
    /// <param name="length">The number of characters in the segment.</param>
    /// <param name="replacement">The string to replace the speccified segment.</param>
    /// <returns>
    /// A string that is equivalent to the current string, except that <paramref name="length"/> number of characters begining at <paramref name="startIndex"/>
    /// are replaced with <paramref name="replacement"/>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="startIndex"/> or <paramref name="length"/> is less than zero, or <paramref name="startIndex"/> is greater than or equal to the
    /// length of <paramref name="source"/> string.
    /// </exception>
    public static string Replace(this string source, int startIndex, int length, string replacement)
    {
        if (startIndex < 0 || source.Length <= startIndex) throw new ArgumentOutOfRangeException(nameof(startIndex), startIndex, "The indicated position is not within this instance.");
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length), length, "Length is less than zero.");

        length = Math.Min(length, source.Length - startIndex);

        return string.Create(source.Length - length + replacement.Length, (source, startIndex, length, replacement), (chars, state) => {
            state.source.AsSpan()[..state.startIndex].CopyTo(chars);
            state.replacement.AsSpan().CopyTo(chars[state.startIndex..]);
            state.source.AsSpan()[(state.startIndex + state.length)..].CopyTo(chars[(state.startIndex + state.replacement.Length)..]);
        });
    }

    /// <summary>
    /// In a specified input string, replaces a strings that matches a regular expression pattern with a specified replacement string.
    /// </summary>
    /// <param name="regex">A regex object with the specified regex pattern.</param>
    /// <param name="input">The string to search for a match.</param>
    /// <param name="replacement">The replacement string.</param>
    /// <param name="groupIndex">The index of the captured group to replace.</param>
    /// <returns>
    /// A new string that is identical to the input string, except that the replacement string takes the place of the matched string.
    /// If the regular expression pattern is not matched in the current instance, the method returns the current instance unchanged.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="groupIndex"/> is less than zero.</exception>
    public static string ReplaceGroup(this Regex regex, string input, string replacement, int groupIndex = 1)
    {
        if (groupIndex < 1) throw new ArgumentOutOfRangeException(nameof(groupIndex), groupIndex, "Captured groups by a regex match start from 1");
        var match = regex.Match(input);
        if (!match.Success || match.Groups.Count < groupIndex) return input;

        var group = match.Groups[groupIndex];
        if (!group.Success) return input;

        return input.Replace(group.Index, group.Length, replacement);
    }
}
