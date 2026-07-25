using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Gitic
{
    public class CliResult
    {
        public int ExitCode { get; init; }
        public string Stdout { get; init; } = string.Empty;
        public string Stderr { get; init; } = string.Empty;
    }

    public static class Cli
    {
        public static CliResult CliSuccess(string stdout)
        {
            return new CliResult
            {
                ExitCode = 0,
                Stdout = stdout,
                Stderr = ""
            };
        }

        public static CliResult CliFailure(string stderr, int exitCode = 1)
        {
            return new CliResult
            {
                ExitCode = exitCode,
                Stdout = "",
                Stderr = stderr
            };
        }

        public static async Task<CliResult> RunCliAsync(string[] args, IConsoleReporter? reporter = null)
        {
            ICommandLineParser parser = new CommandLineParser(args);
            ParsedArgs parsed;
            try
            {
                parsed = parser.Parse();
            }
            catch (CommandLineParseError error)
            {
                reporter?.WriteError($"{error.Message}\n");
                return CliFailure($"{error.Message}\n");
            }

            try
            {
                ICliCommand command = CliCommandFactory.CreateCommand(parsed);
                return await command.ExecuteAsync(reporter);
            }
            catch (Exception ex)
            {
                string errMsg = $"Error: {ex.Message}\n";
                reporter?.WriteError(errMsg);
                return CliFailure(errMsg);
            }
        }

        private static CliResult RunHelpCommand()
        {
            string helpText = 
@"Gitic - Gitizer C# Port (v0.1.0)
A tool to analyze Git repositories and identify code hotspots, contributor ownership, areas, and temporal coupling.

Usage:
  gitic <command> [repo_path] [options]

Commands:
  hotspots [repo_path]                  Identify code hotspots with high complexity/churn
  areas [repo_path]                     Analyze code ownership and changes across directories
  contributors [repo_path]              Show contributor metrics and profiles
  contributor <name> [repo_path]        Analyze a specific contributor's details
  report [repo_path] [options]          Generate reports (visual HTML and/or Markdown summary)
  config init                           Generate a starter config file (.gitizer.yml)

Options:
  -h, --help                            Show this help menu
  --html <path>                         Output visual HTML report to path (for report command)
  --md <path>                           Output Markdown summary report to path (for report command)
  --json                                Output results in raw JSON format
  --all-time                            Analyze all history (ignoring time window settings)
  --since <date>                        Filter commits since date (YYYY-MM-DD)
  --path <pattern>                      Filter analysis to files matching glob pattern (e.g. 'src/**')
  --depth <num>                         Directory depth for areas analysis (1-10, default: 2)
  --include-merges                      Include merge commits in the analysis
  --include-deleted                     Include deleted files in stats
  --merge-by-email                      Merge contributor identities by email
  --anonymize                           Anonymize contributor names/emails in output
";
            return CliSuccess(helpText);
        }

        private static CliResult RunConfigCommand(ParsedArgs parsed)
        {
            if (parsed.ConfigAction != "init")
            {
                return CliFailure("config requires an action. Try: gitizer config init\n");
            }

            return CliSuccess(ConfigLoader.RenderStarterConfig());
        }

        private static AnalysisCommand ParseCommand(string cmd)
        {
            if (string.Equals(cmd, "hotspots", StringComparison.OrdinalIgnoreCase)) return AnalysisCommand.Hotspots;
            if (string.Equals(cmd, "areas", StringComparison.OrdinalIgnoreCase)) return AnalysisCommand.Areas;
            if (string.Equals(cmd, "contributors", StringComparison.OrdinalIgnoreCase)) return AnalysisCommand.Contributors;
            if (string.Equals(cmd, "contributor", StringComparison.OrdinalIgnoreCase)) return AnalysisCommand.Contributor;
            if (string.Equals(cmd, "report", StringComparison.OrdinalIgnoreCase)) return AnalysisCommand.Report;
            throw new CommandLineParseError($"Unknown command: {cmd}");
        }
    }
}
