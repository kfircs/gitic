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

        // Intercept help/version checks at the very beginning
        if (_args.Contains("--version") || _args.Contains("-v") || _args.Contains("version"))
        {
            return new ParsedArgs
            {
                Command = "version",
                RepoPath = ".",
                Settings = DefaultAnalysisSettings.Create()
            };
        }

        if (_args.Contains("--help") || _args.Contains("-h") || _args.Contains("help"))
        {
            string displayVersion = Cli.GetDisplayVersion();
            string helpText =
@"Gitic Strategic Codebase Analysis
A high-speed interactive TUI tool to analyze Git repositories.

Running 'gitic' launches the Interactive TUI Dashboard by default.

" + string.Format(HelpCommand.HelpTemplate, displayVersion);

            return new ParsedArgs
            {
                Command = "help",
                RepoPath = ".",
                Settings = DefaultAnalysisSettings.Create(),
                HelpText = helpText
            };
        }

        // 1. Build the command model
        var rootCommand = new RootCommand("Gitic Strategic Codebase Analysis");
        var cliOptions = new CliOptions();
        cliOptions.RegisterOn(rootCommand);

        // Check for specific subcommands
        string firstToken = _args[0].ToLowerInvariant();

        if (firstToken == "baseline")
        {
            var baselineOptions = BaselineCommand.ParseOptionsFromArgs(_args.ToArray());
            var settings = DefaultAnalysisSettings.Create();
            settings.Json = baselineOptions.Json;
            settings.Format = baselineOptions.Format;

            return new ParsedArgs
            {
                Command = "baseline",
                RepoPath = baselineOptions.RepoPath,
                Settings = settings,
                BaselineSubcommand = baselineOptions.Subcommand,
                BaselineTag = baselineOptions.Tag,
                BaselineFrom = baselineOptions.FromPath,
                BaselineTo = baselineOptions.ToPath,
                BaselineMetric = baselineOptions.MetricName,
                BaselineArea = baselineOptions.AreaName,
                BaselineStorageDir = baselineOptions.StorageDir,
                RawArgs = _args
            };
        }

        if (firstToken == "departure-risk" || firstToken == "departure_risk" || firstToken == "departurerisk")
        {
            var riskOptions = DepartureRiskCommand.ParseOptionsFromArgs(_args.ToArray());
            var settings = DefaultAnalysisSettings.Create();
            settings.Json = riskOptions.Json;
            settings.Format = riskOptions.Format;

            return new ParsedArgs
            {
                Command = "departure-risk",
                RepoPath = riskOptions.RepoPath,
                Settings = settings,
                DepartureRiskDeveloper = riskOptions.Developer,
                ContributorName = riskOptions.Developer,
                RawArgs = _args
            };
        }

        if (firstToken == "config")
        {
            string? action = _args.Count > 1 && !_args[1].StartsWith("-") ? _args[1] : null;
            return new ParsedArgs
            {
                Command = "config",
                RepoPath = ".",
                Settings = DefaultAnalysisSettings.Create(),
                ConfigAction = action,
                RawArgs = _args
            };
        }

        if (firstToken == "impact")
        {
            var impactOptions = ImpactCommand.ParseOptionsFromArgs(_args.ToArray());
            var settings = DefaultAnalysisSettings.Create();
            settings.Json = impactOptions.Json;
            settings.Format = impactOptions.Format;
            settings.Quiet = impactOptions.Quiet;

            return new ParsedArgs
            {
                Command = "impact",
                RepoPath = impactOptions.RepoPath,
                Settings = settings,
                ImpactStaged = impactOptions.Staged,
                ImpactInstallHook = impactOptions.InstallHook,
                ImpactWarnThreshold = impactOptions.WarnThreshold,
                ImpactTargetFiles = impactOptions.TargetFiles,
                RawArgs = _args
            };
        }

        if (firstToken == "gate")
        {
            var gateOptions = GateCommand.ParseOptionsFromArgs(_args.ToArray());
            var settings = DefaultAnalysisSettings.Create();
            settings.Json = gateOptions.Json;
            settings.Format = gateOptions.Format;

            return new ParsedArgs
            {
                Command = "gate",
                RepoPath = gateOptions.RepoPath,
                Settings = settings,
                GateBaseline = gateOptions.Baseline,
                GateFormat = gateOptions.Format,
                GateFailFast = gateOptions.FailFast,
                GateConfig = gateOptions.ConfigPath,
                GateStorageDir = gateOptions.StorageDir,
                RawArgs = _args
            };
        }

        if (firstToken == "sprint-report" || firstToken == "sprint_report" || firstToken == "sprintcard")
        {
            var sprintOptions = SprintReportCommand.ParseOptionsFromArgs(_args.ToArray());
            var settings = DefaultAnalysisSettings.Create();
            settings.Json = sprintOptions.Json;
            settings.Format = sprintOptions.Format;

            return new ParsedArgs
            {
                Command = "sprint-report",
                RepoPath = sprintOptions.RepoPath,
                Settings = settings,
                SprintBaseline = sprintOptions.Baseline,
                SprintCurrent = sprintOptions.Current,
                SprintPeriod = sprintOptions.Period,
                SprintStorageDir = sprintOptions.StorageDir,
                MdPath = sprintOptions.MdPath,
                HtmlPath = sprintOptions.HtmlPath,
                RawArgs = _args
            };
        }

        if (firstToken == "trajectory" || firstToken == "contributor-trajectory" ||
            (firstToken == "contributor" && _args.Contains("--trajectory")) ||
            (firstToken == "baseline" && _args.Count > 1 && string.Equals(_args[1], "trajectory", StringComparison.OrdinalIgnoreCase)))
        {
            var trajOptions = ContributorTrajectoryCommand.ParseOptionsFromArgs(_args.ToArray());
            var settings = DefaultAnalysisSettings.Create();
            settings.Json = trajOptions.Json;
            settings.Format = trajOptions.Format;

            return new ParsedArgs
            {
                Command = "trajectory",
                RepoPath = trajOptions.RepoPath,
                Settings = settings,
                Trajectory = true,
                TrajectoryDeveloper = trajOptions.Developer,
                ContributorName = trajOptions.Developer,
                BaselineStorageDir = trajOptions.StorageDir,
                RawArgs = _args
            };
        }

        // Parse standard arguments
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

        var parsedSettings = DefaultAnalysisSettings.Create();

        // Populate settings from options
        parsedSettings.Json = parseResult.GetValue(cliOptions.JsonOption);

        var formatVal = parseResult.GetValue(cliOptions.FormatOption);
        if (formatVal != null)
        {
            if (!string.Equals(formatVal, "human", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(formatVal, "plain", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(formatVal, "json", StringComparison.OrdinalIgnoreCase))
            {
                throw new CommandLineParseError("--format must be 'human', 'plain', or 'json'.");
            }
            parsedSettings.Format = formatVal.ToLower();
            if (string.Equals(formatVal, "json", StringComparison.OrdinalIgnoreCase))
            {
                parsedSettings.Json = true;
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
            parsedSettings.Color = colorVal.ToLower();
        }

        parsedSettings.AllTime = parseResult.GetValue(cliOptions.AllTimeOption);
        parsedSettings.IncludeMerges = parseResult.GetValue(cliOptions.IncludeMergesOption);
        parsedSettings.IncludeDeleted = parseResult.GetValue(cliOptions.IncludeDeletedOption);
        parsedSettings.MergeByEmail = parseResult.GetValue(cliOptions.MergeByEmailOption);
        parsedSettings.Anonymize = parseResult.GetValue(cliOptions.AnonymizeOption);

        parsedSettings.Since = parseResult.GetValue(cliOptions.SinceOption);
        parsedSettings.Path = parseResult.GetValue(cliOptions.PathOption);
        parsedSettings.Depth = parseResult.GetValue(cliOptions.DepthOption);
        parsedSettings.Limit = parseResult.GetValue(cliOptions.LimitOption);
        parsedSettings.Sort = parseResult.GetValue(cliOptions.SortOption);
        parsedSettings.Columns = parseResult.GetValue(cliOptions.ColumnsOption);
        parsedSettings.Quiet = parseResult.GetValue(cliOptions.QuietOption);

        string repoPath = parseResult.GetValue(cliOptions.RepoPathArg) ?? ".";

        string? htmlPath = parseResult.GetValue(cliOptions.HtmlOption);
        string? mdPath = parseResult.GetValue(cliOptions.MdOption);
        string? svgPath = parseResult.GetValue(cliOptions.SvgOption);
        string? developer = parseResult.GetValue(cliOptions.DeveloperOption);
        bool trajectory = parseResult.GetValue(cliOptions.TrajectoryOption);

        if (trajectory)
        {
            return new ParsedArgs
            {
                Command = "trajectory",
                RepoPath = repoPath,
                Settings = parsedSettings,
                Trajectory = true,
                TrajectoryDeveloper = developer,
                ContributorName = developer,
                HtmlPath = htmlPath,
                MdPath = mdPath,
                SvgPath = svgPath,
                RawArgs = _args
            };
        }

        return new ParsedArgs
        {
            Command = "wizard",
            RepoPath = repoPath,
            Settings = parsedSettings,
            ContributorName = developer,
            DepartureRiskDeveloper = developer,
            HtmlPath = htmlPath,
            MdPath = mdPath,
            SvgPath = svgPath,
            ConfigAction = null,
            RawArgs = _args
        };
    }
}
