using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gitic;

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
        if (parsed == null) throw new ArgumentNullException(nameof(parsed));

        return parsed.Command?.ToLowerInvariant() switch
        {
            "help" => new HelpCommand(parsed.HelpText),
            "version" => new VersionCommand(),
            "config" => new ConfigCommand(parsed),
            "hotspots" => new HotspotsCommand(parsed),
            "areas" => new AreasCommand(parsed),
            "contributors" => new ContributorsCommand(parsed),
            "contributor" => new ContributorCommand(parsed),
            "report" => new ReportCommand(parsed),
            "wizard" => new WizardCommand(parsed),
            "temporal-coupling" => new TemporalCouplingCommand(parsed),
            "lead-time" => new LeadTimeCommand(parsed),
            "ge-report" => new GeReportCommand(parsed),
            _ => throw new CommandLineParseError($"Unknown command: {parsed.Command}")
        };
    }
}

public abstract class BaseAnalysisCommand : ICliCommand
{
    protected readonly ParsedArgs Parsed;
    private readonly IGitClient? _gitClient;
    private readonly IRepositoryAnalyzer? _analyzer;

    protected BaseAnalysisCommand(ParsedArgs parsed, IGitClient? gitClient = null, IRepositoryAnalyzer? analyzer = null)
    {
        Parsed = parsed;
        _gitClient = gitClient;
        _analyzer = analyzer;
    }

    protected abstract AnalysisCommand CommandType { get; }

    private async Task<AnalysisResult> ExecuteAnalysisAsync(IConsoleReporter? reporter, CancellationToken cancellationToken)
    {
        var gitClient = _gitClient ?? new GitClient(Parsed.RepoPath);
        string? repoRoot = await gitClient.GetRepositoryRootAsync(cancellationToken);
        if (repoRoot == null)
        {
            throw new InvalidOperationException($"Path {Parsed.RepoPath} is not inside a Git repository.\n" +
                            "Run gitic from a Git worktree or pass the path to one.\n");
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
            ContributorName = Parsed.ContributorName,
            GitClient = gitClient
        };

        var analyzer = _analyzer ?? new RepositoryAnalyzer();
        return await analyzer.AnalyzeAsync(input, cancellationToken);
    }

    public async Task<CliResult> ExecuteAsync(IConsoleReporter? reporter, CancellationToken cancellationToken = default)
    {
        AnalysisResult result;
        try
        {
            result = await ExecuteAnalysisAsync(reporter, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            reporter?.WriteError(ex.Message);
            return Cli.CliFailure(ex.Message);
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
    protected StandardRenderAnalysisCommand(ParsedArgs parsed, IGitClient? gitClient = null, IRepositoryAnalyzer? analyzer = null) 
        : base(parsed, gitClient, analyzer) { }

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
    public HotspotsCommand(ParsedArgs parsed, IGitClient? gitClient = null, IRepositoryAnalyzer? analyzer = null) 
        : base(parsed, gitClient, analyzer) { }
    protected override AnalysisCommand CommandType => AnalysisCommand.Hotspots;
}

public class AreasCommand : StandardRenderAnalysisCommand
{
    public AreasCommand(ParsedArgs parsed, IGitClient? gitClient = null, IRepositoryAnalyzer? analyzer = null) 
        : base(parsed, gitClient, analyzer) { }
    protected override AnalysisCommand CommandType => AnalysisCommand.Areas;
}

public class ContributorsCommand : StandardRenderAnalysisCommand
{
    public ContributorsCommand(ParsedArgs parsed, IGitClient? gitClient = null, IRepositoryAnalyzer? analyzer = null) 
        : base(parsed, gitClient, analyzer) { }
    protected override AnalysisCommand CommandType => AnalysisCommand.Contributors;
}

public class TemporalCouplingCommand : StandardRenderAnalysisCommand
{
    public TemporalCouplingCommand(ParsedArgs parsed, IGitClient? gitClient = null, IRepositoryAnalyzer? analyzer = null) 
        : base(parsed, gitClient, analyzer) { }
    protected override AnalysisCommand CommandType => AnalysisCommand.TemporalCoupling;
}

public class LeadTimeCommand : StandardRenderAnalysisCommand
{
    public LeadTimeCommand(ParsedArgs parsed, IGitClient? gitClient = null, IRepositoryAnalyzer? analyzer = null) 
        : base(parsed, gitClient, analyzer) { }
    protected override AnalysisCommand CommandType => AnalysisCommand.LeadTime;
}

public class ContributorCommand : StandardRenderAnalysisCommand
{
    public ContributorCommand(ParsedArgs parsed, IGitClient? gitClient = null, IRepositoryAnalyzer? analyzer = null) 
        : base(parsed, gitClient, analyzer) { }
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
    public ReportCommand(ParsedArgs parsed, IGitClient? gitClient = null, IRepositoryAnalyzer? analyzer = null) 
        : base(parsed, gitClient, analyzer) { }
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
                string targetPath = Parsed.HtmlPath;
                if (Directory.Exists(targetPath))
                {
                    targetPath = Path.Combine(targetPath, "report.html");
                }
                
                string dir = Path.GetDirectoryName(targetPath) ?? ".";
                string tempPath = Path.Combine(dir, $".report.html.{Path.GetRandomFileName()}.tmp");
                
                tempFiles.Add((tempPath, targetPath));
                using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
                {
                    await htmlRenderer.RenderToStreamAsync(result, fs, cancellationToken);
                }
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
    public GeReportCommand(ParsedArgs parsed, IGitClient? gitClient = null, IRepositoryAnalyzer? analyzer = null) 
        : base(parsed, gitClient, analyzer) { }
    protected override AnalysisCommand CommandType => AnalysisCommand.GeReport;

    protected override async Task<CliResult> ProcessResultAsync(AnalysisResult result, IConsoleReporter? reporter, CancellationToken cancellationToken = default)
    {
        var geRenderer = new GeReportRenderer();
        string mdContent = await geRenderer.RenderAsync(result, cancellationToken);
        
        reporter?.Write(mdContent);
        return Cli.CliSuccess(mdContent);
    }
}
