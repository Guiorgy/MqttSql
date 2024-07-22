using System;
using System.Threading.Tasks;

namespace Tests;

public static class Extensions
{
    /// <summary>
    /// Continues a Task on failure (<see cref="TaskStatus.Faulted"/>) and prints the Exception to the standard Console.
    /// </summary>
    /// <param name="task">The task whose error to print to console.</param>
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

    /// <summary>
    /// Prepends a prefix to every line in a string.
    /// </summary>
    /// <param name="source">The sourcestring.</param>
    /// <param name="prefix">The prefix to prepend to each line.</param>
    /// <param name="lineSeparator">The line separation delimiter.</param>
    /// <returns>The modified string.</returns>
    public static string PrependToLines(this string source, string prefix, string lineSeparator = "\n") => prefix + source.Replace(lineSeparator, lineSeparator + prefix);
}
