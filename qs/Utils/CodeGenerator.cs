namespace qs.Utils
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    internal static class CodeGenerator
    {
        internal static readonly Regex codeRegex = new Regex("\\d{6}");

        private static Random dice = new Random();

        internal static string GetCode()
        {
            var buffer = new byte[6];
            dice.NextBytes(buffer);
            return buffer.Select(b => (b % 10).ToString()).Aggregate((a, b) => a + b);
        }
    }
}
