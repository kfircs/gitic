using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using Kfc.Cli.Core;

namespace Gitic;

/// <summary>
/// Parser responsible for reading command line arguments, validating command boundaries,
/// and returning a unified ParsedArgs metadata structure.
/// </summary>
public class CommandLineParser : ICommandLineParser
{
    private readonly IReadOnlyList<string> _args;
    private readonly ICliCommandFactory _commandFactory;

    public CommandLineParser(string[] args) : this(args, new CliCommandFactoryImpl())
    {
    }

    public CommandLineParser(string[] args, ICliCommandFactory commandFactory)
    {
        _args = args is not null ? [.. args] : [];
        _commandFactory = commandFactory ?? new CliCommandFactoryImpl();
    }

    public ICommand ParseToCommand()
    {
        var parsed = Parse();
        return _commandFactory.CreateCommand(parsed);
    }

    public ParsedArgs Parse()
    {
        if (_args.Any(arg => arg == null || arg.Trim() == ""))
        {
            throw new CommandLineParseError("Command name or argument cannot be empty or null.");
        }

        if (_args.Count == 0)
        {
            // Default to launching the TUI Wizard/Dashboard
            return new ParsedArgs
            {
                Command = "wizard",
                RepoPath = ".",
                Settings = DefaultAnalysisSettings.Create()
            };
        }

        // 1. Build the command model
        var rootCommand = new RootCommand("Gitic Strategic Codebase Analysis");
        var cliOptions = new CliOptions();
        cliOptions.RegisterOn(rootCommand);

        // Intercept help/version checks at the very beginning
        if (_args.Contains("--help") || _args.Contains("-h") || _args.Contains("help"))
        {
            var pr = rootCommand.Parse(_args);
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

            helpText =
@"Gitic Strategic Codebase Analysis
A high-speed interactive TUI tool to analyze Git repositories.

Running 'gitic' launches the Interactive TUI Dashboard by default.

" + helpText;

            return new ParsedArgs
            {
                Command = "help",
                RepoPath = ".",
                Settings = DefaultAnalysisSettings.Create(),
                HelpText = helpText
            };
        }

        if (_args.Contains("--version") || _args.Contains("-v") || _args.Contains("version"))
        {
            return new ParsedArgs
            {
                Command = "version",
                RepoPath = ".",
                Settings = DefaultAnalysisSettings.Create()
            };
        }

        // Parse the arguments
        var parseResult = rootCommand.Parse(_args);

        // Handle invalid usage or unrecognized elements
        if (parseResult.Errors.Any())
        {
            var errors = string.Join("\n", parseResult.Errors.Select(e =>
            {
                var msg = e.Message;
                if (msg.Contains("--depth") || (e.SymbolResult is OptionResult optionResult && optionResult.Option == cliOptions.DepthOption))
                {
                    return "--depth must be an integer between 1 and 10.";
                }
                return msg;
            }).Distinct());

            if (parseResult.Errors.Any(e => e.Message.Contains("--depth") || (e.SymbolResult is OptionResult optionResult && optionResult.Option == cliOptions.DepthOption)))
            {
                throw new CommandLineParseError(errors);
            }
            throw new CommandLineParseError($"{errors}\nTry running 'gitic --help' for usage.");
        }

        var settings = DefaultAnalysisSettings.Create();

        // Populate settings from options
        settings.Json = parseResult.GetValue(cliOptions.JsonOption);

        var formatVal = parseResult.GetValue(cliOptions.FormatOption);
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

        var colorVal = parseResult.GetValue(cliOptions.ColorOption);
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

        settings.AllTime = parseResult.GetValue(cliOptions.AllTimeOption);
        settings.IncludeMerges = parseResult.GetValue(cliOptions.IncludeMergesOption);
        settings.IncludeDeleted = parseResult.GetValue(cliOptions.IncludeDeletedOption);
        settings.MergeByEmail = parseResult.GetValue(cliOptions.MergeByEmailOption);
        settings.Anonymize = parseResult.GetValue(cliOptions.AnonymizeOption);

        settings.Since = parseResult.GetValue(cliOptions.SinceOption);
        settings.Path = parseResult.GetValue(cliOptions.PathOption);
        settings.Depth = parseResult.GetValue(cliOptions.DepthOption);
        settings.Limit = parseResult.GetValue(cliOptions.LimitOption);
        settings.Sort = parseResult.GetValue(cliOptions.SortOption);
        settings.Columns = parseResult.GetValue(cliOptions.ColumnsOption);
        settings.Quiet = parseResult.GetValue(cliOptions.QuietOption);

        string repoPath = parseResult.GetValue(cliOptions.RepoPathArg) ?? ".";

        string? htmlPath = parseResult.GetValue(cliOptions.HtmlOption);
        string? mdPath = parseResult.GetValue(cliOptions.MdOption);
        string? svgPath = parseResult.GetValue(cliOptions.SvgOption);

        return new ParsedArgs
        {
            Command = "wizard",
            RepoPath = repoPath,
            Settings = settings,
            ContributorName = null,
            HtmlPath = htmlPath,
            MdPath = mdPath,
            SvgPath = svgPath,
            ConfigAction = null
        };
    }
}
