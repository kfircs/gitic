using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gitic
{
    public interface ICliCommand
    {
        Task<CliResult> ExecuteAsync(IConsoleReporter? reporter, CancellationToken cancellationToken = default);
    }

    public interface ICliCommandFactory
    {
        ICliCommand CreateCommand(ParsedArgs parsed);
    }

    public class CliCommandFactoryImpl : ICliCommandFactory
    {
        public ICliCommand CreateCommand(ParsedArgs parsed)
        {
            return CliCommandFactory.CreateCommand(parsed);
        }
    }

    public static class CliCommandFactory
    {
        public static ICliCommand CreateCommand(ParsedArgs parsed)
        {
            if (string.Equals(parsed.Command, "help", StringComparison.OrdinalIgnoreCase))
            {
                return new HelpCommand(parsed.HelpText);
            }

            if (string.Equals(parsed.Command, "version", StringComparison.OrdinalIgnoreCase))
            {
                return new VersionCommand();
            }

            if (string.Equals(parsed.Command, "config", StringComparison.OrdinalIgnoreCase))
            {
                return new ConfigCommand(parsed);
            }

            if (string.Equals(parsed.Command, "hotspots", StringComparison.OrdinalIgnoreCase))
            {
                return new HotspotsCommand(parsed);
            }

            if (string.Equals(parsed.Command, "areas", StringComparison.OrdinalIgnoreCase))
            {
                return new AreasCommand(parsed);
            }

            if (string.Equals(parsed.Command, "contributors", StringComparison.OrdinalIgnoreCase))
            {
                return new ContributorsCommand(parsed);
            }

            if (string.Equals(parsed.Command, "contributor", StringComparison.OrdinalIgnoreCase))
            {
                return new ContributorCommand(parsed);
            }

            if (string.Equals(parsed.Command, "report", StringComparison.OrdinalIgnoreCase))
            {
                return new ReportCommand(parsed);
            }

            if (string.Equals(parsed.Command, "wizard", StringComparison.OrdinalIgnoreCase))
            {
                return new WizardCommand(parsed);
            }

            if (string.Equals(parsed.Command, "temporal-coupling", StringComparison.OrdinalIgnoreCase))
            {
                return new TemporalCouplingCommand(parsed);
            }

            if (string.Equals(parsed.Command, "lead-time", StringComparison.OrdinalIgnoreCase))
            {
                return new LeadTimeCommand(parsed);
            }

            if (string.Equals(parsed.Command, "ge-report", StringComparison.OrdinalIgnoreCase))
            {
                return new GeReportCommand(parsed);
            }

            throw new CommandLineParseError($"Unknown command: {parsed.Command}");
        }
    }

    public class VersionCommand : ICliCommand
    {
        public Task<CliResult> ExecuteAsync(IConsoleReporter? reporter, CancellationToken cancellationToken = default)
        {
            var assembly = typeof(Cli).Assembly;
            var version = assembly.GetName().Version?.ToString(3) ?? "0.1.0";
            var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            var displayVersion = string.IsNullOrEmpty(infoVersion) ? version : infoVersion;

            string versionText = $"gitic version {displayVersion}\n";
            reporter?.Write(versionText);
            return Task.FromResult(Cli.CliSuccess(versionText));
        }
    }

    public class HelpCommand : ICliCommand
    {
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

            var assembly = typeof(Cli).Assembly;
            var version = assembly.GetName().Version?.ToString(3) ?? "0.1.0";
            var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            var displayVersion = string.IsNullOrEmpty(infoVersion) ? version : infoVersion;

            string helpText = 
$@"Gitic Strategic Codebase Analysis (v{displayVersion})
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
            reporter?.Write(helpText);
            return Task.FromResult(Cli.CliSuccess(helpText));
        }
    }

    public class ConfigCommand : ICliCommand
    {
        private readonly ParsedArgs _parsed;

        public ConfigCommand(ParsedArgs parsed)
        {
            _parsed = parsed;
        }

        public Task<CliResult> ExecuteAsync(IConsoleReporter? reporter, CancellationToken cancellationToken = default)
        {
            if (_parsed.ConfigAction != "init")
            {
                string errMsg = "config requires an action. Try: gitic config init\n";
                reporter?.WriteError(errMsg);
                return Task.FromResult(Cli.CliFailure(errMsg, exitCode: 2));
            }

            var engine = new ConfigurationEngine();
            string stdout = engine.RenderStarterConfig();
            reporter?.Write(stdout);
            return Task.FromResult(Cli.CliSuccess(stdout));
        }
    }

    public abstract class BaseAnalysisCommand : ICliCommand
    {
        protected readonly ParsedArgs Parsed;

        protected BaseAnalysisCommand(ParsedArgs parsed)
        {
            Parsed = parsed;
        }

        protected abstract AnalysisCommand CommandType { get; }

        public async Task<CliResult> ExecuteAsync(IConsoleReporter? reporter, CancellationToken cancellationToken = default)
        {
            var gitClient = new GitClient(Parsed.RepoPath);
            string? repoRoot = await gitClient.GetRepositoryRootAsync(cancellationToken);
            if (repoRoot == null)
            {
                string errMsg = $"Path {Parsed.RepoPath} is not inside a Git repository.\n" +
                                "Run gitic from a Git worktree or pass the path to one.\n";
                reporter?.WriteError(errMsg);
                return Cli.CliFailure(errMsg);
            }

            bool isInteractiveHuman = !Console.IsErrorRedirected &&
                                      !Parsed.Settings.Quiet &&
                                      string.Equals(Parsed.Settings.Format, "human", StringComparison.OrdinalIgnoreCase) &&
                                      !Parsed.Settings.Json;

            if (isInteractiveHuman)
            {
                reporter?.WriteError("Analyzing repository...\n");
            }

            var input = new AnalyzeInput
            {
                RepoRoot = repoRoot,
                Command = CommandType,
                Settings = Parsed.Settings,
                ContributorName = Parsed.ContributorName
            };

            IRepositoryAnalyzer analyzer = new RepositoryAnalyzer();
            AnalysisResult result;
            try
            {
                result = await analyzer.AnalyzeAsync(input, cancellationToken);
            }
            catch (ConfigValidationError error)
            {
                string errMsg = $"Invalid Gitic config:\n{string.Join("\n", error.Details)}\n";
                reporter?.WriteError(errMsg);
                return Cli.CliFailure(errMsg);
            }

            return await ProcessResultAsync(result, reporter, cancellationToken);
        }

        protected abstract Task<CliResult> ProcessResultAsync(AnalysisResult result, IConsoleReporter? reporter, CancellationToken cancellationToken = default);
    }

    public abstract class StandardRenderAnalysisCommand : BaseAnalysisCommand
    {
        protected StandardRenderAnalysisCommand(ParsedArgs parsed) : base(parsed) { }

        protected override async Task<CliResult> ProcessResultAsync(AnalysisResult result, IConsoleReporter? reporter, CancellationToken cancellationToken = default)
        {
            if (Parsed.Settings.Json || string.Equals(Parsed.Settings.Format, "json", StringComparison.OrdinalIgnoreCase))
            {
                var jsonRenderer = new JsonRenderer();
                string jsonOutput = await jsonRenderer.RenderAsync(result, cancellationToken);
                reporter?.Write(jsonOutput);
                return Cli.CliSuccess(jsonOutput);
            }

            var renderer = new CliTableRenderer(CommandType, Parsed.Settings);
            string tableOutput = await renderer.RenderAsync(result, cancellationToken);
            reporter?.Write(tableOutput);

            var stderrSb = new StringBuilder();
            if (result.Exclusions != null && result.Exclusions.Count > 0)
            {
                string exclusionText = "exclusions " + string.Join(", ", result.Exclusions.Select(e => $"{e.Category}:{e.Count}")) + "\n";
                stderrSb.Append(exclusionText);
            }
            if (result.Diagnostics != null && result.Diagnostics.Count > 0)
            {
                var diagnosticsToShow = result.Diagnostics;
                if (Parsed.Settings.Quiet)
                {
                    diagnosticsToShow = result.Diagnostics.Where(d =>
                        string.Equals(d.Severity, "Critical", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(d.Severity, "Error", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(d.Severity, "Failure", StringComparison.OrdinalIgnoreCase)
                    ).ToList();
                }

                if (diagnosticsToShow.Count > 0)
                {
                    var grouped = diagnosticsToShow
                        .GroupBy(d => d.Severity.ToUpperInvariant())
                        .OrderBy(g => Warnings.GetSeverityOrder(g.Key));

                    foreach (var group in grouped)
                    {
                        stderrSb.AppendLine($"[{group.Key}]");
                        foreach (var diag in group)
                        {
                            stderrSb.AppendLine($"  {diag.Code}: {diag.Message}");
                            if (!string.IsNullOrEmpty(diag.Hint))
                            {
                                stderrSb.AppendLine($"  Hint: {diag.Hint}");
                            }
                        }
                    }
                }
            }

            string stderrOutput = stderrSb.ToString();
            if (!string.IsNullOrEmpty(stderrOutput))
            {
                reporter?.WriteError(stderrOutput);
            }

            return Cli.CliSuccess(tableOutput, stderrOutput);
        }
    }

    public class HotspotsCommand : StandardRenderAnalysisCommand
    {
        public HotspotsCommand(ParsedArgs parsed) : base(parsed) { }
        protected override AnalysisCommand CommandType => AnalysisCommand.Hotspots;
    }

    public class AreasCommand : StandardRenderAnalysisCommand
    {
        public AreasCommand(ParsedArgs parsed) : base(parsed) { }
        protected override AnalysisCommand CommandType => AnalysisCommand.Areas;
    }

    public class ContributorsCommand : StandardRenderAnalysisCommand
    {
        public ContributorsCommand(ParsedArgs parsed) : base(parsed) { }
        protected override AnalysisCommand CommandType => AnalysisCommand.Contributors;
    }

    public class TemporalCouplingCommand : StandardRenderAnalysisCommand
    {
        public TemporalCouplingCommand(ParsedArgs parsed) : base(parsed) { }
        protected override AnalysisCommand CommandType => AnalysisCommand.TemporalCoupling;
    }

    public class LeadTimeCommand : StandardRenderAnalysisCommand
    {
        public LeadTimeCommand(ParsedArgs parsed) : base(parsed) { }
        protected override AnalysisCommand CommandType => AnalysisCommand.LeadTime;
    }

    public class ContributorCommand : StandardRenderAnalysisCommand
    {
        public ContributorCommand(ParsedArgs parsed) : base(parsed) { }
        protected override AnalysisCommand CommandType => AnalysisCommand.Contributor;

        protected override async Task<CliResult> ProcessResultAsync(AnalysisResult result, IConsoleReporter? reporter, CancellationToken cancellationToken = default)
        {
            IContributorLookupRegistry registry = new ContributorLookupRegistry(result.Contributors);
            try
            {
                var filtered = registry.Find(Parsed.ContributorName ?? "");
                result.Contributors = new List<ContributorMetric> { filtered };
            }
            catch (Exception ex) when (ex is ContributorNotFoundError || ex is AmbiguousContributorError)
            {
                reporter?.WriteError($"{ex.Message}\n");
                return Cli.CliFailure($"{ex.Message}\n");
            }

            return await base.ProcessResultAsync(result, reporter, cancellationToken);
        }
    }

    public class ReportCommand : BaseAnalysisCommand
    {
        public ReportCommand(ParsedArgs parsed) : base(parsed) { }
        protected override AnalysisCommand CommandType => AnalysisCommand.Report;

        protected override async Task<CliResult> ProcessResultAsync(AnalysisResult result, IConsoleReporter? reporter, CancellationToken cancellationToken = default)
        {
            if (Parsed.HtmlPath == null && Parsed.MdPath == null && Parsed.SvgPath == null)
            {
                string errMsg = "report requires --html <path>, --md <path>, or --svg <path>.\n";
                reporter?.WriteError(errMsg);
                return Cli.CliFailure(errMsg, exitCode: 2);
            }

            var outputSb = new StringBuilder();
            var tempFiles = new List<(string TempPath, string TargetPath)>();
            try
            {
                if (Parsed.HtmlPath != null)
                {
                    var htmlRenderer = new HtmlRenderer();
                    string htmlContent = await htmlRenderer.RenderAsync(result, cancellationToken);
                    string targetPath = Parsed.HtmlPath;
                    if (Directory.Exists(targetPath))
                    {
                        targetPath = Path.Combine(targetPath, "report.html");
                    }
                    
                    string dir = Path.GetDirectoryName(targetPath) ?? ".";
                    string tempPath = Path.Combine(dir, $".report.html.{Path.GetRandomFileName()}.tmp");
                    
                    await File.WriteAllTextAsync(tempPath, htmlContent, cancellationToken);
                    tempFiles.Add((tempPath, targetPath));
                    outputSb.Append($"Wrote HTML report to {targetPath}\n");
                }
                if (Parsed.MdPath != null)
                {
                    var mdRenderer = new MarkdownRenderer();
                    string mdContent = await mdRenderer.RenderAsync(result, cancellationToken);
                    string targetPath = Parsed.MdPath;
                    if (Directory.Exists(targetPath))
                    {
                        targetPath = Path.Combine(targetPath, "report.md");
                    }
                    
                    string dir = Path.GetDirectoryName(targetPath) ?? ".";
                    string tempPath = Path.Combine(dir, $".report.md.{Path.GetRandomFileName()}.tmp");
                    
                    await File.WriteAllTextAsync(tempPath, mdContent, cancellationToken);
                    tempFiles.Add((tempPath, targetPath));
                    outputSb.Append($"Wrote Markdown report to {targetPath}\n");
                }
                if (Parsed.SvgPath != null)
                {
                    var svgSummaryRenderer = new SvgSummaryRenderer();
                    var svgComplexityRenderer = new SvgComplexityRenderer();
                    string svgContent = await svgSummaryRenderer.RenderAsync(result, cancellationToken);
                    string complexitySvgContent = await svgComplexityRenderer.RenderAsync(result, cancellationToken);
                    
                    string targetPath = Parsed.SvgPath;
                    string targetComplexityPath = Parsed.SvgPath;
                    if (Directory.Exists(targetPath))
                    {
                        targetPath = Path.Combine(targetPath, "report.svg");
                        targetComplexityPath = Path.Combine(targetComplexityPath, "report-complexity.svg");
                    }
                    else
                    {
                        string dir = Path.GetDirectoryName(targetPath) ?? ".";
                        string name = Path.GetFileNameWithoutExtension(targetPath);
                        targetComplexityPath = Path.Combine(dir, $"{name}-complexity.svg");
                    }
                    
                    string dirSvg = Path.GetDirectoryName(targetPath) ?? ".";
                    string tempPath = Path.Combine(dirSvg, $".report.svg.{Path.GetRandomFileName()}.tmp");

                    string dirComp = Path.GetDirectoryName(targetComplexityPath) ?? ".";
                    string tempComplexityPath = Path.Combine(dirComp, $".report-complexity.svg.{Path.GetRandomFileName()}.tmp");

                    await File.WriteAllTextAsync(tempPath, svgContent, cancellationToken);
                    tempFiles.Add((tempPath, targetPath));

                    await File.WriteAllTextAsync(tempComplexityPath, complexitySvgContent, cancellationToken);
                    tempFiles.Add((tempComplexityPath, targetComplexityPath));

                    outputSb.Append($"Wrote SVG report to {targetPath}\nWrote Svg Complexity report to {targetComplexityPath}\n");
                }

                // Move all temporary files into place atomically
                foreach (var pair in tempFiles)
                {
                    File.Move(pair.TempPath, pair.TargetPath, overwrite: true);
                }
            }
            catch
            {
                // Clean up any temp files we created
                foreach (var pair in tempFiles)
                {
                    try
                    {
                        if (File.Exists(pair.TempPath))
                        {
                            File.Delete(pair.TempPath);
                        }
                    }
                    catch { /* Ignore cleanup errors */ }
                }
                throw;
            }

            string reportOutput = outputSb.ToString();
            reporter?.Write(reportOutput);
            return Cli.CliSuccess(reportOutput);
        }
    }

    public class GeReportCommand : BaseAnalysisCommand
    {
        public GeReportCommand(ParsedArgs parsed) : base(parsed) { }
        protected override AnalysisCommand CommandType => AnalysisCommand.GeReport;

        protected override async Task<CliResult> ProcessResultAsync(AnalysisResult result, IConsoleReporter? reporter, CancellationToken cancellationToken = default)
        {
            var geRenderer = new GeReportRenderer();
            string mdContent = await geRenderer.RenderAsync(result, cancellationToken);
            
            reporter?.Write(mdContent);
            return Cli.CliSuccess(mdContent);
        }
    }
}
