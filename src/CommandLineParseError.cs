using System;

namespace Gitic;

public sealed class CommandLineParseError : Exception
{
    public CommandLineParseError() { }

    public CommandLineParseError(string message) : base(message) { }

    public CommandLineParseError(string message, Exception innerException) : base(message, innerException) { }
}
