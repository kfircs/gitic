using System;
using System.Collections.Generic;

namespace Gitic
{
    public class CommandLineParseError : Exception
    {
        public CommandLineParseError(string message) : base(message)
        {
        }
    }

    public class CommandLineValidator
    {
        public void ValidateCommand(string? commandName)
        {
            if (string.IsNullOrEmpty(commandName))
            {
                throw new CommandLineParseError("A command is required.");
            }
            if (!IsCommand(commandName))
            {
                throw new CommandLineParseError($"Unknown command: {commandName}");
            }
        }

        public string ValidateNextValue(string argName, string? value)
        {
            if (value == null)
            {
                throw new CommandLineParseError($"{argName} requires a value.");
            }
            return value;
        }

        public int ValidateDepth(string value)
        {
            if (!int.TryParse(value, out int depth) || depth < 1)
            {
                throw new CommandLineParseError("--depth must be a positive integer.");
            }
            if (depth > 10)
            {
                throw new CommandLineParseError("--depth must be between 1 and 10.");
            }
            return depth;
        }

        public void ValidateUnknownFlag(string arg)
        {
            throw new CommandLineParseError($"Unknown flag: {arg}");
        }

        public void ValidateContributorName(string? contributorName)
        {
            if (string.IsNullOrEmpty(contributorName))
            {
                throw new CommandLineParseError("contributor requires a contributor name.");
            }
        }

        private bool IsCommand(string command)
        {
            return command == "hotspots" ||
                   command == "areas" ||
                   command == "contributors" ||
                   command == "contributor" ||
                   command == "report" ||
                   command == "config";
        }
    }
}
