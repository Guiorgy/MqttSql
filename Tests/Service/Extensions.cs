using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Tests;

public static class Extensions
{
    private static readonly string[] NewLines = ["\r\n", "\r", "\n"];

    /// <summary>
    /// Splits a string into lines and returns them as an array.
    /// </summary>
    /// <param name="string">The string to be split.</param>
    /// <returns>An array of lines from the source string.</returns>
    public static string[] SplitLines(this string @string) => @string.Split(NewLines, StringSplitOptions.None);

    /// <summary>
    /// Joins an array of lines of strings into a single string.
    /// </summary>
    /// <param name="lines">The lines to be joined.</param>
    /// <returns>A single string with all lines joined.</returns>
    public static string JoinLines(this string[] lines) => string.Join(Environment.NewLine, lines);

    /// <summary>
    /// Joins a collection of lines of strings into a single string.
    /// </summary>
    /// <param name="lines">The lines to be joined.</param>
    /// <returns>A single string with all lines joined.</returns>
    public static string JoinLines(this IEnumerable<string> lines) => string.Join(Environment.NewLine, lines);

    /// <summary>
    /// Compares two strings line-by-line for differences.
    /// </summary>
    /// <param name="first">The first string to compare.</param>
    /// <param name="second">The second string to compare.</param>
    /// <returns>The lines that did not match and their line numbers.</returns>
    public static string DifferenceString(this string first, string second)
    {
        return string.Join(Environment.NewLine, Difference(first, second));

        static IEnumerable<string> Difference(string first, string second) =>
            first
                .SplitLines()
                .Zip(second.SplitLines())
                .Where(x => x.First != x.Second)
                .Select((x, i) => $"Line {i + 1}:\n\t{x.First}\n\t{x.Second}");
    }

    /// <summary>
    /// Continues a Task on failure (<see cref="TaskStatus.Faulted"/>) and prints the Exception to the standard Console.
    /// </summary>
    /// <param name="task">The task to whose error to print to console.</param>
    public static void ExceptionToConsole(this Task task)
    {
        _ = task.ContinueWith(t =>
        {
            if (t.Exception != null)
            {
                Console.WriteLine(t.Exception.Message);
                Console.WriteLine(t.Exception.StackTrace);
            }
        }, TaskContinuationOptions.OnlyOnFaulted);
    }
}
