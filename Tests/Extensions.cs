using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Tests;

public static class Extensions
{
    private static readonly string[] LineEndings = new[] { "\r\n", "\r", "\n" };

    //
    // Summary:
    //     Splits a string into lines and returns them as array.
    public static string[] SplitLines(this string str)
    {
        return str.Split(LineEndings, StringSplitOptions.None);
    }

    //
    // Summary:
    //     Combines an array of lines of strings into a single string.
    public static string JoinLines(this string[] lines)
    {
        return string.Join(Environment.NewLine, lines);
    }
    public static string JoinLines(this IEnumerable<string> lines)
    {
        return string.Join(Environment.NewLine, lines);
    }

    //
    // Summary:
    //     Compares two strings line-by-line for differences.
    public static IEnumerable<string> Difference(this string first, string second)
    {
        return first.SplitLines().Zip(second.SplitLines()).Select((ss, i) => $"Line {i + 1}:\n\t{ss.First}\n\t{ss.Second}")
            .Except(first.SplitLines().Select((s, i) => $"Line {i + 1}:\n\t{s}\n\t{s}"));
    }

    //
    // Summary:
    //     Compares two strings line-by-line for differences.
    public static string DifferenceString(this string first, string second)
    {
        return string.Join(Environment.NewLine, Difference(first, second));
    }

    //
    // Summary:
    //     Continue a Task on Faulted and print the Exception to the standard Console.
    public static void ExceptionToConsole(this Task task)
    {
        task.ContinueWith(t =>
        {
            if (t.Exception != null)
            {
                Console.WriteLine(t.Exception.Message);
                Console.WriteLine(t.Exception.StackTrace);
            }
        }, TaskContinuationOptions.OnlyOnFaulted);
    }
}
