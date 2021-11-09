namespace qs
{
    using qs.Utils;
    using System;
    using System.Linq;
    using System.Text.RegularExpressions;

    class Program
    {
        static void Main(string[] args)
        {
            if(args.Length == 0)
            {
                PrintHelp();
            }

            if (args[0] == "share")
            {
                FileSharer.Share(args.Skip(1).ToArray());
                return;
            }

            if (CodeGenerator.codeRegex.IsMatch(args[0]))
            {
                FileReceiver.Receive(args[0]);
                return;
            }

            PrintHelp();
        }

        private static void PrintHelp()
        {
            Console.WriteLine("Incorrect usage. Please use the following command\n" +
                "To share files please use\n" +
                $"    {AppDomain.CurrentDomain.FriendlyName} share <File1> [File2 ...]\n" +
                $"\n" +
                $"To receive files please use\n" +
                $"    {AppDomain.CurrentDomain.FriendlyName} <6DigitCode>");

            Environment.Exit(0);
        }
    }
}
