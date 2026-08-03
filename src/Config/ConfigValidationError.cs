using System;
using System.Collections.Generic;

namespace Gitic;

public sealed class ConfigValidationError : Exception
{
    public List<string> Details { get; }

    public ConfigValidationError(List<string> details) : base(string.Join(Environment.NewLine, details))
    {
        Details = details;
    }

    public ConfigValidationError(string message) : base(message) => Details = new List<string> { message };
}

