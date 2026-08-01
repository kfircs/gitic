using System;

namespace Gitic
{
    public class CommandLineParseError : Exception
    {
        public CommandLineParseError() : base() { }

        public CommandLineParseError(string message) : base(message)
        {
        }

        public CommandLineParseError(string message, Exception innerException) : base(message, innerException) { }
    }
}
