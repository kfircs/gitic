using System;

namespace Gitic
{
    public interface IConsoleReporter
    {
        void Write(string message);
        void WriteLine(string message);
        void WriteError(string message);
        void WriteErrorLine(string message);
    }

    public class ConsoleReporter : IConsoleReporter
    {
        public void Write(string message)
        {
            Console.Write(message);
        }

        public void WriteLine(string message)
        {
            Console.WriteLine(message);
        }

        public void WriteError(string message)
        {
            Console.Error.Write(message);
        }

        public void WriteErrorLine(string message)
        {
            Console.Error.WriteLine(message);
        }
    }
}
