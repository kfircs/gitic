using System;
using System.Collections.Generic;

namespace Gitic
{
    public class ParsedArgs
    {
        public string Command { get; set; } = string.Empty;
        public string RepoPath { get; set; } = ".";
        public AnalysisSettings Settings { get; set; } = new();
        public string? ContributorName { get; set; }
        public string? HtmlPath { get; set; }
        public string? MdPath { get; set; }
        public string? SvgPath { get; set; }
        public string? ConfigAction { get; set; }
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

    public class CommandLineParser
    {
        private readonly List<string> _args;
        private readonly CommandLineValidator _validator;

        public CommandLineParser(string[] args)
        {
            _args = new List<string>(args);
            _validator = new CommandLineValidator();
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
            _validator.ValidateCommand(commandName);

            var settings = DefaultAnalysisSettings.Create();
            var positionals = new List<string>();
            string? htmlPath = null;
            string? mdPath = null;
            string? svgPath = null;

            string ConsumeValue(string argName, int currentIndex)
            {
                if (currentIndex + 1 >= _args.Count)
                {
                    throw new CommandLineParseError($"{argName} requires a value.");
                }
                string rawValue = _args[currentIndex + 1];
                return _validator.ValidateNextValue(argName, rawValue);
            }

            for (int index = 1; index < _args.Count; index += 1)
            {
                string arg = _args[index];
                if (arg == "--json")
                {
                    settings.Json = true;
                }
                else if (arg == "--all-time")
                {
                    settings.AllTime = true;
                }
                else if (arg == "--include-merges")
                {
                    settings.IncludeMerges = true;
                }
                else if (arg == "--merge-by-email")
                {
                    settings.MergeByEmail = true;
                }
                else if (arg == "--include-deleted")
                {
                    settings.IncludeDeleted = true;
                }
                else if (arg == "--anonymize")
                {
                    settings.Anonymize = true;
                }
                else if (arg == "--since")
                {
                    settings.Since = ConsumeValue(arg, index);
                    index += 1;
                }
                else if (arg == "--path")
                {
                    settings.Path = ConsumeValue(arg, index);
                    index += 1;
                }
                else if (arg == "--depth")
                {
                    string rawDepth = ConsumeValue(arg, index);
                    settings.Depth = _validator.ValidateDepth(rawDepth);
                    index += 1;
                }
                else if (arg == "--html")
                {
                    htmlPath = ConsumeValue(arg, index);
                    index += 1;
                }
                else if (arg == "--md")
                {
                    mdPath = ConsumeValue(arg, index);
                    index += 1;
                }
                else if (arg == "--svg")
                {
                    svgPath = ConsumeValue(arg, index);
                    index += 1;
                }
                else if (arg.StartsWith("--"))
                {
                    _validator.ValidateUnknownFlag(arg);
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
                _validator.ValidateContributorName(contributorName);
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
    }
}
