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

    public class ParsedArgs
    {
        public string Command { get; init; } = string.Empty;
        public string RepoPath { get; init; } = ".";
        public AnalysisSettings Settings { get; init; } = new();
        public string? ContributorName { get; init; }
        public string? HtmlPath { get; init; }
        public string? MdPath { get; init; }
        public string? SvgPath { get; init; }
        public string? ConfigAction { get; init; }
    }

    public static class DefaultAnalysisSettings
    {
        public static AnalysisSettings Create() => new()
        {
            Json = false,
            AllTime = false,
            Since = null,
            IncludeMerges = false,
            IncludeDeleted = false,
            MergeByEmail = false,
            Path = null,
            Anonymize = false,
            Depth = 2
        };
    }

    public interface ICommandLineParser
    {
        ParsedArgs Parse();
    }

    public class CommandLineParser : ICommandLineParser
    {
        private const int MinDepth = 1;
        private const int MaxDepth = 10;

        private static readonly HashSet<string> ValidCommands = new(StringComparer.OrdinalIgnoreCase)
        {
            "hotspots",
            "areas",
            "contributors",
            "contributor",
            "report",
            "config"
        };

        private readonly List<string> _args;

        public CommandLineParser(string[] args)
        {
            _args = new List<string>(args);
        }

        public ParsedArgs Parse()
        {
            if (_args.Contains("--help") || _args.Contains("-h") || _args.Contains("help"))
            {
                return new ParsedArgs
                {
                    Command = "help",
                    RepoPath = ".",
                    Settings = DefaultAnalysisSettings.Create(),
                    ContributorName = null,
                    HtmlPath = null,
                    ConfigAction = null
                };
            }

            if (_args.Count == 0)
            {
                throw new CommandLineParseError("A command is required.");
            }

            string commandName = _args[0];
            ValidateCommand(commandName);

            var settings = DefaultAnalysisSettings.Create();
            var positionals = new List<string>();
            string? htmlPath = null;
            string? mdPath = null;
            string? svgPath = null;

            for (int index = 1; index < _args.Count; index += 1)
            {
                string arg = _args[index];
                if (arg.StartsWith("--"))
                {
                    ProcessFlag(arg, ref index, settings, ref htmlPath, ref mdPath, ref svgPath);
                }
                else
                {
                    positionals.Add(arg);
                }
            }

            if (commandName == "report")
            {
                settings.IncludeMerges = true;
            }

            if (commandName == "config")
            {
                return new ParsedArgs
                {
                    Command = "config",
                    RepoPath = ".",
                    Settings = settings,
                    ContributorName = null,
                    HtmlPath = htmlPath,
                    MdPath = mdPath,
                    SvgPath = svgPath,
                    ConfigAction = positionals.Count > 0 ? positionals[0] : null
                };
            }

            if (commandName == "contributor")
            {
                string? contributorName = positionals.Count > 0 ? positionals[0] : null;
                ValidateContributorName(contributorName);
                return new ParsedArgs
                {
                    Command = "contributor",
                    RepoPath = positionals.Count > 1 ? positionals[1] : ".",
                    Settings = settings,
                    ContributorName = contributorName,
                    HtmlPath = htmlPath,
                    MdPath = mdPath,
                    SvgPath = svgPath,
                    ConfigAction = null
                };
            }

            return new ParsedArgs
            {
                Command = commandName,
                RepoPath = positionals.Count > 0 ? positionals[0] : ".",
                Settings = settings,
                ContributorName = null,
                HtmlPath = htmlPath,
                MdPath = mdPath,
                SvgPath = svgPath,
                ConfigAction = null
            };
        }

        private void ValidateCommand(string? commandName)
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

        private string ValidateNextValue(string argName, string? value)
        {
            if (value == null)
            {
                throw new CommandLineParseError($"{argName} requires a value.");
            }
            return value;
        }

        private int ValidateDepth(string value)
        {
            if (!int.TryParse(value, out int depth) || depth < MinDepth || depth > MaxDepth)
            {
                throw new CommandLineParseError($"--depth must be an integer between {MinDepth} and {MaxDepth}.");
            }
            return depth;
        }

        private void ValidateUnknownFlag(string arg)
        {
            throw new CommandLineParseError($"Unknown flag: {arg}");
        }

        private void ValidateContributorName(string? contributorName)
        {
            if (string.IsNullOrEmpty(contributorName))
            {
                throw new CommandLineParseError("contributor requires a contributor name.");
            }
        }

        private void ProcessFlag(
            string arg,
            ref int index,
            AnalysisSettings settings,
            ref string? htmlPath,
            ref string? mdPath,
            ref string? svgPath)
        {
            switch (arg)
            {
                case "--json":
                    settings.Json = true;
                    break;
                case "--all-time":
                    settings.AllTime = true;
                    break;
                case "--include-merges":
                    settings.IncludeMerges = true;
                    break;
                case "--merge-by-email":
                    settings.MergeByEmail = true;
                    break;
                case "--include-deleted":
                    settings.IncludeDeleted = true;
                    break;
                case "--anonymize":
                    settings.Anonymize = true;
                    break;
                case "--since":
                    settings.Since = ConsumeValue(arg, index);
                    index += 1;
                    break;
                case "--path":
                    settings.Path = ConsumeValue(arg, index);
                    index += 1;
                    break;
                case "--depth":
                    string rawDepth = ConsumeValue(arg, index);
                    settings.Depth = ValidateDepth(rawDepth);
                    index += 1;
                    break;
                case "--html":
                    htmlPath = ConsumeValue(arg, index);
                    index += 1;
                    break;
                case "--md":
                    mdPath = ConsumeValue(arg, index);
                    index += 1;
                    break;
                case "--svg":
                    svgPath = ConsumeValue(arg, index);
                    index += 1;
                    break;
                default:
                    ValidateUnknownFlag(arg);
                    break;
            }
        }

        private string ConsumeValue(string argName, int currentIndex)
        {
            if (currentIndex + 1 >= _args.Count)
            {
                throw new CommandLineParseError($"{argName} requires a value.");
            }
            string rawValue = _args[currentIndex + 1];
            return ValidateNextValue(argName, rawValue);
        }

        private bool IsCommand(string command)
        {
            return ValidCommands.Contains(command);
        }
    }
}
