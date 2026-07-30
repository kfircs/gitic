using System;
using System.Collections.Generic;

namespace Gitic
{
    public class ConfigValidationError : Exception
    {
        public List<string> Details { get; }

        public ConfigValidationError(List<string> details) : base(string.Join("\n", details))
        {
            Details = details;
        }

        public ConfigValidationError(string message) : base(message)
        {
            Details = new List<string> { message };
        }
    }
}
