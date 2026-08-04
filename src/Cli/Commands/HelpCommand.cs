using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Gitic;

public class HelpCommand : ICliCommand
{
    private const string DefaultVersion = "0.1.0";
    private static readonly string HelpTemplate = 
@"Gitic Strategic Codebase Analysis (v{0})
A tool to analyze Git repositories and identify code hotspots, contributor ownership, areas, and temporal coupling.

Usage:
  gitic <command> [repo_path] [options]

Commands:
  hotspots [repo_path]                  Identify code hotspots with high complexity/churn
  areas [repo_path]                     Analyze code ownership and changes across directories
  contributors [repo_path]              Show contributor metrics and profiles
  contributor <name> [repo_path]        Analyze a specific contributor's details
  report [repo_path] [options]          Generate reports (visual HTML, Markdown, and/or SVG)
  temporal-coupling [repo_path]        Analyze temporal coupling between files
  lead-time [repo_path]                 Measure code change and merge lead times
  config init                           Generate a starter config file (.gitic.yml)
  version                               Show version information

Options:
  -h, --help                            Show this help menu
  -v, --version                         Show version information
  --config <config>                     Path to non-default configuration file
  --user-config <user-config>           Path to non-default global user configuration file
  --format <format>                     Output format: human, plain, json (default: human)
  --color <color>                       Color mode: auto, always, never (default: auto)
  --html <path>                         Output visual HTML report to path (for report command)
  --md <path>                           Output Markdown summary report to path (for report command)
  --svg <path>                          Output SVG reports to path (for report command)
  --json                                Output results in raw JSON format
  --all-time                            Analyze all history (ignoring time window settings)
  --since <since>                       Filter commits since date (YYYY-MM-DD)
  --until <until>                       Filter commits until date (YYYY-MM-DD)
  --path <path>                         Filter analysis to files matching glob pattern (e.g. 'src/**')
  --depth <depth>                       Directory depth for areas analysis (1-10, default: 2)
  --limit <limit>                       Limit results to top N items
  --sort <sort>                         Sort results by field
  --columns <columns>                   Select columns to show
  --include-merges                      Include merge commits in the analysis
  --include-deleted                     Include deleted files in stats
  --merge-by-email                      Merge contributor identities by email
  --anonymize                           Anonymize contributor names/emails in output
";

    private readonly string? _generatedHelpText;

    public HelpCommand(string? generatedHelpText = null)
    {
        _generatedHelpText = generatedHelpText;
    }

    public Task<CliResult> ExecuteAsync(IConsoleReporter? reporter, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(_generatedHelpText))
        {
            reporter?.Write(_generatedHelpText);
            return Task.FromResult(Cli.CliSuccess(_generatedHelpText));
        }

        string displayVersion = Cli.GetDisplayVersion();

        string helpText = string.Format(HelpTemplate, displayVersion);
        reporter?.Write(helpText);
        return Task.FromResult(Cli.CliSuccess(helpText));
    }
}
