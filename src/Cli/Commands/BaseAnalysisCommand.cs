using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gitic;

/// <summary>
/// Abstract base command class that orchestrates Git history extraction and repository analysis.
/// </summary>
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

/// <summary>
/// Abstract class for commands that render analysis results into interactive/plain CLI tables or JSON output.
/// </summary>
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
