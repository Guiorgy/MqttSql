using System;
using System.Collections.Generic;
using System.Linq;

namespace Tests
{
    public static class Extensions
    {
        private static readonly string[] LineEndings = new[] { "\r\n", "\r", "\n" };

        public static IEnumerable<string> Difference(this string first, string second)
        {
            return first.Split(LineEndings, StringSplitOptions.None).Select((s, i) => $"Line {i + 1}: {s}")
                .Except(second.Split(LineEndings, StringSplitOptions.None).Select((s, i) => $"Line {i + 1}: {s}"));
        }

        public static string DifferenceString(this string first, string second)
        {
            return string.Join(Environment.NewLine, Difference(first, second));
        }
    }
}
