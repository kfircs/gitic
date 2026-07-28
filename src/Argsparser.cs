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
            Depth = 2,
            Format = "human",
            Color = "auto"
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
            "config",
            "version"
        };

        private readonly List<string> _args;
        private int _index;
        private string? _htmlPath;
        private string? _mdPath;
        private string? _svgPath;

        public CommandLineParser(string[] args)
        {
            _args = new List<string>(args);
        }

        public ParsedArgs Parse()
        {
            if (_args.Contains("--version") || _args.Contains("-v") || _args.Contains("version"))
            {
                return new ParsedArgs
                {
                    Command = "version",
                    RepoPath = ".",
                    Settings = DefaultAnalysisSettings.Create(),
                    ContributorName = null,
                    HtmlPath = null,
                    ConfigAction = null
                };
            }

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
                throw new CommandLineParseError(
@"Gitic: Strategic Codebase Analysis
A tool to analyze Git repositories and identify code hotspots, contributor ownership, areas, and temporal coupling.

Usage:
  gitic <command> [repo_path] [options]

Useful next steps:
  1. Run 'gitic hotspots' to identify high-complexity/high-churn files in the current repository.
  2. Run 'gitic --help' to see all available commands and options.");
            }

            string commandName = _args[0];
            ValidateCommand(commandName);

            var settings = DefaultAnalysisSettings.Create();
            var positionals = new List<string>();
            _htmlPath = null;
            _mdPath = null;
            _svgPath = null;

            for (_index = 1; _index < _args.Count; _index += 1)
            {
                string arg = _args[_index];
                if (arg.StartsWith("--"))
                {
                    ProcessFlag(arg, settings);
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
                    HtmlPath = _htmlPath,
                    MdPath = _mdPath,
                    SvgPath = _svgPath,
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
                    HtmlPath = _htmlPath,
                    MdPath = _mdPath,
                    SvgPath = _svgPath,
                    ConfigAction = null
                };
            }

            return new ParsedArgs
            {
                Command = commandName,
                RepoPath = positionals.Count > 0 ? positionals[0] : ".",
                Settings = settings,
                ContributorName = null,
                HtmlPath = _htmlPath,
                MdPath = _mdPath,
                SvgPath = _svgPath,
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
            AnalysisSettings settings)
        {
            switch (arg)
            {
                case "--json":
                    settings.Json = true;
                    settings.Format = "json";
                    break;
                case "--format":
                    string formatVal = ConsumeValue(arg);
                    if (!string.Equals(formatVal, "human", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(formatVal, "plain", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(formatVal, "json", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new CommandLineParseError("--format must be 'human', 'plain', or 'json'.");
                    }
                    settings.Format = formatVal.ToLower();
                    if (string.Equals(formatVal, "json", StringComparison.OrdinalIgnoreCase))
                    {
                        settings.Json = true;
                    }
                    _index += 1;
                    break;
                case "--color":
                    string colorVal = ConsumeValue(arg);
                    if (!string.Equals(colorVal, "auto", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(colorVal, "always", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(colorVal, "never", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new CommandLineParseError("--color must be 'auto', 'always', or 'never'.");
                    }
                    settings.Color = colorVal.ToLower();
                    _index += 1;
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
                    settings.Since = ConsumeValue(arg);
                    _index += 1;
                    break;
                case "--path":
                    settings.Path = ConsumeValue(arg);
                    _index += 1;
                    break;
                case "--depth":
                    string rawDepth = ConsumeValue(arg);
                    settings.Depth = ValidateDepth(rawDepth);
                    _index += 1;
                    break;
                case "--html":
                    _htmlPath = ConsumeValue(arg);
                    _index += 1;
                    break;
                case "--md":
                    _mdPath = ConsumeValue(arg);
                    _index += 1;
                    break;
                case "--svg":
                    _svgPath = ConsumeValue(arg);
                    _index += 1;
                    break;
                default:
                    ValidateUnknownFlag(arg);
                    break;
            }
        }

        private string ConsumeValue(string argName)
        {
            if (_index + 1 >= _args.Count)
            {
                throw new CommandLineParseError($"{argName} requires a value.");
            }
            string rawValue = _args[_index + 1];
            return ValidateNextValue(argName, rawValue);
        }

        private bool IsCommand(string command)
        {
            return ValidCommands.Contains(command);
        }
    }
}
