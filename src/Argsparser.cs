using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;

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
        private readonly List<string> _args;

        public CommandLineParser(string[] args)
        {
            _args = args != null ? new List<string>(args) : new List<string>();
        }

        public ParsedArgs Parse()
        {
            if (_args == null)
            {
                throw new CommandLineParseError("Arguments cannot be null.");
            }
            if (_args.Any(arg => arg == null || arg.Trim() == ""))
            {
                throw new CommandLineParseError("Command name or argument cannot be empty or null.");
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

            // 1. Build the command model
            var rootCommand = new RootCommand("Gitic Strategic Codebase Analysis");

            var configOption = new Option<string>("--config") { Description = "Path to non-default configuration file", Recursive = true };
            var userConfigOption = new Option<string>("--user-config") { Description = "Path to non-default global user configuration file", Recursive = true };
            var jsonOption = new Option<bool>("--json") { Description = "Output results in raw JSON format", Recursive = true };
            
            var formatOption = new Option<string>("--format") 
            { 
                Description = "Output format: human, plain, json", 
                DefaultValueFactory = _ => "human",
                Recursive = true 
            };
            
            var colorOption = new Option<string>("--color") 
            { 
                Description = "Color mode: auto, always, never", 
                DefaultValueFactory = _ => "auto",
                Recursive = true 
            };
            
            var allTimeOption = new Option<bool>("--all-time") { Description = "Analyze all history (ignoring time window settings)", Recursive = true };
            var includeMergesOption = new Option<bool>("--include-merges") { Description = "Include merge commits in the analysis", Recursive = true };
            var includeDeletedOption = new Option<bool>("--include-deleted") { Description = "Include deleted files in stats", Recursive = true };
            var mergeByEmailOption = new Option<bool>("--merge-by-email") { Description = "Merge contributor identities by email", Recursive = true };
            var anonymizeOption = new Option<bool>("--anonymize") { Description = "Anonymize contributor names/emails in output", Recursive = true };
            var sinceOption = new Option<string>("--since") { Description = "Filter commits since date (YYYY-MM-DD)", Recursive = true };
            var untilOption = new Option<string>("--until") { Description = "Filter commits until date (YYYY-MM-DD)", Recursive = true };
            var pathOption = new Option<string>("--path") { Description = "Filter analysis to files matching glob pattern", Recursive = true };
            
            var depthOption = new Option<int>("--depth") 
            { 
                Description = "Directory depth for areas analysis (1-10)", 
                DefaultValueFactory = _ => 2,
                Recursive = true 
            };
            depthOption.Validators.Add(result =>
            {
                try
                {
                    var value = result.GetValue(depthOption);
                    if (value < 1 || value > 10)
                    {
                        result.AddError("--depth must be an integer between 1 and 10.");
                    }
                }
                catch
                {
                    result.AddError("--depth must be an integer between 1 and 10.");
                }
            });

            var htmlOption = new Option<string>("--html") { Description = "Output visual HTML report to path", Recursive = true, HelpName = "path" };
            var mdOption = new Option<string>("--md") { Description = "Output Markdown summary report to path", Recursive = true, HelpName = "path" };
            var svgOption = new Option<string>("--svg") { Description = "Output SVG reports to path", Recursive = true, HelpName = "path" };

            // Gitic-004 options placeholder
            var limitOption = new Option<int?>("--limit") { Description = "Limit results to top N items", Recursive = true };
            var sortOption = new Option<string>("--sort") { Description = "Sort results by field", Recursive = true };
            var columnsOption = new Option<string>("--columns") { Description = "Select columns to show", Recursive = true };
            var quietOption = new Option<bool>("--quiet") { Description = "Suppress non-critical warnings", Recursive = true };

            // Add global options
            rootCommand.Options.Add(configOption);
            rootCommand.Options.Add(userConfigOption);
            rootCommand.Options.Add(jsonOption);
            rootCommand.Options.Add(formatOption);
            rootCommand.Options.Add(colorOption);
            rootCommand.Options.Add(allTimeOption);
            rootCommand.Options.Add(includeMergesOption);
            rootCommand.Options.Add(includeDeletedOption);
            rootCommand.Options.Add(mergeByEmailOption);
            rootCommand.Options.Add(anonymizeOption);
            rootCommand.Options.Add(sinceOption);
            rootCommand.Options.Add(untilOption);
            rootCommand.Options.Add(pathOption);
            rootCommand.Options.Add(depthOption);
            rootCommand.Options.Add(htmlOption);
            rootCommand.Options.Add(mdOption);
            rootCommand.Options.Add(svgOption);
            rootCommand.Options.Add(limitOption);
            rootCommand.Options.Add(sortOption);
            rootCommand.Options.Add(columnsOption);
            rootCommand.Options.Add(quietOption);

            // Subcommands
            var hotspotsRepoPathArg = new Argument<string>("repo_path") { Description = "Path to the repository", DefaultValueFactory = _ => "." };
            var hotspotsCommand = new Command("hotspots", "Identify code hotspots with high complexity/churn");
            hotspotsCommand.Arguments.Add(hotspotsRepoPathArg);
            rootCommand.Subcommands.Add(hotspotsCommand);

            var areasRepoPathArg = new Argument<string>("repo_path") { Description = "Path to the repository", DefaultValueFactory = _ => "." };
            var areasCommand = new Command("areas", "Analyze code ownership and changes across directories");
            areasCommand.Arguments.Add(areasRepoPathArg);
            rootCommand.Subcommands.Add(areasCommand);

            var contributorsRepoPathArg = new Argument<string>("repo_path") { Description = "Path to the repository", DefaultValueFactory = _ => "." };
            var contributorsCommand = new Command("contributors", "Show contributor metrics and profiles");
            contributorsCommand.Arguments.Add(contributorsRepoPathArg);
            rootCommand.Subcommands.Add(contributorsCommand);

            var contributorRepoPathArg = new Argument<string>("repo_path") { Description = "Path to the repository", DefaultValueFactory = _ => "." };
            var contributorCommand = new Command("contributor", "Analyze a specific contributor's details");
            var nameArg = new Argument<string>("name") { Description = "The contributor name" };
            contributorCommand.Arguments.Add(nameArg);
            contributorCommand.Arguments.Add(contributorRepoPathArg);
            rootCommand.Subcommands.Add(contributorCommand);

            var reportRepoPathArg = new Argument<string>("repo_path") { Description = "Path to the repository", DefaultValueFactory = _ => "." };
            var reportCommand = new Command("report", "Generate reports (visual HTML, Markdown, and/or SVG)");
            reportCommand.Arguments.Add(reportRepoPathArg);
            rootCommand.Subcommands.Add(reportCommand);

            var temporalCouplingRepoPathArg = new Argument<string>("repo_path") { Description = "Path to the repository", DefaultValueFactory = _ => "." };
            var temporalCouplingCommand = new Command("temporal-coupling", "Analyze temporal coupling between files");
            temporalCouplingCommand.Arguments.Add(temporalCouplingRepoPathArg);
            rootCommand.Subcommands.Add(temporalCouplingCommand);

            var leadTimeRepoPathArg = new Argument<string>("repo_path") { Description = "Path to the repository", DefaultValueFactory = _ => "." };
            var leadTimeCommand = new Command("lead-time", "Measure code change and merge lead times");
            leadTimeCommand.Arguments.Add(leadTimeRepoPathArg);
            rootCommand.Subcommands.Add(leadTimeCommand);

            var configCommand = new Command("config", "Generate a starter config file");
            var actionArg = new Argument<string>("action") { Description = "The config action (e.g., init)", DefaultValueFactory = _ => "init" };
            configCommand.Arguments.Add(actionArg);
            rootCommand.Subcommands.Add(configCommand);

            var versionCommand = new Command("version", "Show version information");
            rootCommand.Subcommands.Add(versionCommand);

            // Case-insensitivity normalization for the command name
            var normalizedArgs = _args.Select((arg, idx) => 
            {
                if (idx == 0 && arg != null)
                {
                    var lower = arg.ToLower();
                    if (lower == "hotspots" || lower == "areas" || lower == "contributors" || 
                        lower == "contributor" || lower == "report" || lower == "config" || lower == "version" || lower == "help" ||
                        lower == "temporal-coupling" || lower == "lead-time")
                    {
                        return lower;
                    }
                }
                return arg;
            })
            .Where(arg => arg != null)
            .Select(arg => arg!)
            .ToList();

            // Intercept help/version checks at the very beginning to avoid unrelated secondary parsing errors
            if (_args.Contains("--help") || _args.Contains("-h") || normalizedArgs.Contains("help"))
            {
                var pr = rootCommand.Parse(normalizedArgs);
                using var stdoutWriter = new StringWriter();
                using var stderrWriter = new StringWriter();
                pr.Invoke(new InvocationConfiguration
                {
                    Output = stdoutWriter,
                    Error = stderrWriter
                });
                string helpText = stdoutWriter.ToString();
                if (string.IsNullOrEmpty(helpText))
                {
                    helpText = stderrWriter.ToString();
                }

                return new ParsedArgs
                {
                    Command = "help",
                    RepoPath = ".",
                    Settings = DefaultAnalysisSettings.Create(),
                    HtmlPath = helpText
                };
            }

            if (_args.Contains("--version") || _args.Contains("-v") || normalizedArgs.Contains("version"))
            {
                return new ParsedArgs
                {
                    Command = "version",
                    RepoPath = ".",
                    Settings = DefaultAnalysisSettings.Create()
                };
            }

            // Parse the normalized arguments
            var parseResult = rootCommand.Parse(normalizedArgs);

            // Handle invalid usage or unrecognized elements
            if (parseResult.Errors.Any())
            {
                var errors = string.Join("\n", parseResult.Errors.Select(e => 
                {
                    var msg = e.Message;
                    if (msg.Contains("--depth") || (e.SymbolResult is OptionResult optionResult && optionResult.Option == depthOption))
                    {
                        return "--depth must be an integer between 1 and 10.";
                    }
                    return msg;
                }).Distinct());

                if (parseResult.Errors.Any(e => e.Message.Contains("--depth") || (e.SymbolResult is OptionResult optionResult && optionResult.Option == depthOption)))
                {
                    throw new CommandLineParseError(errors);
                }
                throw new CommandLineParseError($"{errors}\nTry running 'gitic --help' for usage.");
            }

            // Check if root command has been invoked without subcommands
            if (parseResult.CommandResult.Command == rootCommand)
            {
                throw new CommandLineParseError("A command is required.\nTry running 'gitic --help' for usage.");
            }

            string commandName = parseResult.CommandResult.Command.Name;

            var settings = DefaultAnalysisSettings.Create();

            // Populate settings from options
            settings.Json = parseResult.GetValue(jsonOption);
            
            var formatVal = parseResult.GetValue(formatOption);
            if (formatVal != null)
            {
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
            }

            var colorVal = parseResult.GetValue(colorOption);
            if (colorVal != null)
            {
                if (!string.Equals(colorVal, "auto", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(colorVal, "always", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(colorVal, "never", StringComparison.OrdinalIgnoreCase))
                {
                    throw new CommandLineParseError("--color must be 'auto', 'always', or 'never'.");
                }
                settings.Color = colorVal.ToLower();
            }

            settings.AllTime = parseResult.GetValue(allTimeOption);
            settings.IncludeMerges = parseResult.GetValue(includeMergesOption);
            settings.IncludeDeleted = parseResult.GetValue(includeDeletedOption);
            settings.MergeByEmail = parseResult.GetValue(mergeByEmailOption);
            settings.Anonymize = parseResult.GetValue(anonymizeOption);
            
            settings.Since = parseResult.GetValue(sinceOption);
            settings.Path = parseResult.GetValue(pathOption);
            settings.Depth = parseResult.GetValue(depthOption);
            settings.Limit = parseResult.GetValue(limitOption);
            settings.Sort = parseResult.GetValue(sortOption);
            settings.Columns = parseResult.GetValue(columnsOption);
            settings.Quiet = parseResult.GetValue(quietOption);

            string repoPath = ".";
            string? contributorName = null;
            string? configAction = null;

            if (commandName == "hotspots")
            {
                repoPath = parseResult.GetValue(hotspotsRepoPathArg) ?? ".";
            }
            else if (commandName == "areas")
            {
                repoPath = parseResult.GetValue(areasRepoPathArg) ?? ".";
            }
            else if (commandName == "contributors")
            {
                repoPath = parseResult.GetValue(contributorsRepoPathArg) ?? ".";
            }
            else if (commandName == "temporal-coupling")
            {
                repoPath = parseResult.GetValue(temporalCouplingRepoPathArg) ?? ".";
            }
            else if (commandName == "lead-time")
            {
                repoPath = parseResult.GetValue(leadTimeRepoPathArg) ?? ".";
            }
            else if (commandName == "report")
            {
                repoPath = parseResult.GetValue(reportRepoPathArg) ?? ".";
                settings.IncludeMerges = true;
            }
            else if (commandName == "contributor")
            {
                contributorName = parseResult.GetValue(nameArg);
                repoPath = parseResult.GetValue(contributorRepoPathArg) ?? ".";
                if (string.IsNullOrEmpty(contributorName))
                {
                    throw new CommandLineParseError("contributor requires a contributor name.");
                }
            }
            else if (commandName == "config")
            {
                configAction = parseResult.GetValue(actionArg) ?? "init";
            }

            string? htmlPath = parseResult.GetValue(htmlOption);
            string? mdPath = parseResult.GetValue(mdOption);
            string? svgPath = parseResult.GetValue(svgOption);

            return new ParsedArgs
            {
                Command = commandName,
                RepoPath = repoPath,
                Settings = settings,
                ContributorName = contributorName,
                HtmlPath = htmlPath,
                MdPath = mdPath,
                SvgPath = svgPath,
                ConfigAction = configAction
            };
        }
    }
}
