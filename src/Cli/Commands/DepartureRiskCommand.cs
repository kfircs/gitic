using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Kfc.Cli.Core;

namespace Gitic;

public class DepartureRiskCommandOptions
{
    public string? Developer { get; set; }
    public string RepoPath { get; set; } = ".";
    public bool Json { get; set; }
    public string Format { get; set; } = "human";
}

public class DepartureRiskCommand : IGiticCommand
{
    private readonly DepartureRiskCommandOptions _options;
    private readonly IDepartureRiskAnalyzer _riskAnalyzer;
    private readonly IRepositoryAnalyzer _analyzer;
    private readonly IGitClient? _gitClient;

    public DepartureRiskCommand(
        ParsedArgs parsed,
        IDepartureRiskAnalyzer? riskAnalyzer = null,
        IRepositoryAnalyzer? analyzer = null,
        IGitClient? gitClient = null)
    {
        _riskAnalyzer = riskAnalyzer ?? new DepartureRiskAnalyzer();
        _analyzer = analyzer ?? new RepositoryAnalyzer();
        _gitClient = gitClient;
        _options = ParseOptionsFromParsedArgs(parsed);
    }

    public DepartureRiskCommand(
        string[] args,
        IDepartureRiskAnalyzer? riskAnalyzer = null,
        IRepositoryAnalyzer? analyzer = null,
        IGitClient? gitClient = null)
    {
        _riskAnalyzer = riskAnalyzer ?? new DepartureRiskAnalyzer();
        _analyzer = analyzer ?? new RepositoryAnalyzer();
        _gitClient = gitClient;
        _options = ParseOptionsFromArgs(args);
    }

    public Task<CliResult> ExecuteAsync(IConsoleReporter reporter)
    {
        return ExecuteAsync(reporter, CancellationToken.None);
    }

    public async Task<CliResult> ExecuteAsync(IConsoleReporter? reporter, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Developer))
        {
            string errMsg = "Departure risk analysis requires a target developer. Use --developer <name_or_email>.\n";
            reporter?.WriteError(errMsg);
            return Cli.CliFailure(errMsg, exitCode: 2);
        }

        string repoRoot = string.IsNullOrWhiteSpace(_options.RepoPath) ? "." : _options.RepoPath;
        var gitClient = _gitClient ?? new GitClient(repoRoot);
        string? resolvedRoot = null;
        try
        {
            resolvedRoot = await gitClient.GetRepositoryRootAsync(cancellationToken);
        }
        catch
        {
            // Ignore if lookup fails
        }

        string actualRoot = !string.IsNullOrEmpty(resolvedRoot) ? resolvedRoot : Path.GetFullPath(repoRoot);

        try
        {
            var analyzeInput = new AnalyzeInput
            {
                RepoRoot = actualRoot,
                Command = AnalysisCommand.DepartureRisk,
                GitClient = gitClient,
                Settings = new AnalysisSettings
                {
                    Json = _options.Json,
                    Format = _options.Format
                }
            };

            var analysisResult = await _analyzer.AnalyzeAsync(analyzeInput, cancellationToken);
            var report = _riskAnalyzer.AnalyzeDepartureRisk(analysisResult, _options.Developer);

            if (_options.Json || string.Equals(_options.Format, "json", StringComparison.OrdinalIgnoreCase))
            {
                string jsonOutput = JsonSerializer.Serialize(report, JsonSerializationDefaults.Indented);
                reporter?.Write(jsonOutput);
                return Cli.CliSuccess(jsonOutput);
            }

            string tableOutput = RenderTerminalReport(report);
            reporter?.Write(tableOutput);
            return Cli.CliSuccess(tableOutput);
        }
        catch (Exception ex)
        {
            string errMsg = $"Departure risk analysis failed: {ex.Message}\n";
            reporter?.WriteError(errMsg);
            return Cli.CliFailure(errMsg, exitCode: 1);
        }
    }

    public static string RenderTerminalReport(DepartureRiskReport report)
    {
        var sb = new StringBuilder();
        string targetDisplay = string.IsNullOrEmpty(report.TargetEmail)
            ? report.TargetDeveloper
            : $"{report.TargetDeveloper} <{report.TargetEmail}>";

        sb.AppendLine("╔══════════════════════════════════════════════════════════════════════════════════════╗");
        sb.AppendLine($"║  🚪 Departure Risk Analysis: {targetDisplay}".PadRight(87) + "║");
        sb.AppendLine("╠══════════════════════════════════════════════════════════════════════════════════════╣");
        sb.AppendLine("║                                                                                      ║");

        // Critical Risk Files
        sb.AppendLine("║  CRITICAL RISK (sole owner / >=70% ownership):".PadRight(87) + "║");
        if (report.CriticalRiskFiles.Count == 0)
        {
            sb.AppendLine("║    None detected.".PadRight(87) + "║");
        }
        else
        {
            for (int i = 0; i < report.CriticalRiskFiles.Count; i++)
            {
                var file = report.CriticalRiskFiles[i];
                string branch = (i == report.CriticalRiskFiles.Count - 1) ? "└─" : "├─";
                string fileLine = $"    {branch} {file.Path}    {Math.Round(file.OwnershipShare * 100)}% ownership, {file.Touches} touches";
                sb.AppendLine($"║{fileLine}".PadRight(87) + "║");
            }
        }
        sb.AppendLine("║                                                                                      ║");

        // Moderate Risk Files
        sb.AppendLine("║  MODERATE RISK (primary owner >50%, has backup):".PadRight(87) + "║");
        if (report.ModerateRiskFiles.Count == 0)
        {
            sb.AppendLine("║    None detected.".PadRight(87) + "║");
        }
        else
        {
            for (int i = 0; i < report.ModerateRiskFiles.Count; i++)
            {
                var file = report.ModerateRiskFiles[i];
                string branch = (i == report.ModerateRiskFiles.Count - 1) ? "└─" : "├─";
                string secondariesStr = "";
                if (file.SecondaryContributors != null && file.SecondaryContributors.Count > 0)
                {
                    var topSec = file.SecondaryContributors.Take(2).Select(s => $"{s.Name}: {Math.Round(s.ActivityShare * 100)}%");
                    secondariesStr = $" ({string.Join(", ", topSec)})";
                }
                string fileLine = $"    {branch} {file.Path}    {Math.Round(file.OwnershipShare * 100)}%{secondariesStr}";
                sb.AppendLine($"║{fileLine}".PadRight(87) + "║");
            }
        }
        sb.AppendLine("║                                                                                      ║");

        // Summary
        string summaryLine = $"  TOTAL AFFECTED: {report.TotalAffectedAreas} areas, {report.TotalAffectedFiles} files, ~{report.TotalAffectedLines:N0} lines";
        sb.AppendLine($"║{summaryLine}".PadRight(87) + "║");
        string scoreLine = $"  OVERALL RISK SCORE: {report.OverallRiskScore:F1} / 100";
        sb.AppendLine($"║{scoreLine}".PadRight(87) + "║");
        sb.AppendLine("║                                                                                      ║");

        // Recommended Knowledge Transfer Plan
        sb.AppendLine("║  RECOMMENDED KNOWLEDGE TRANSFER PLAN:".PadRight(87) + "║");
        if (report.TransferPlan.Count == 0)
        {
            sb.AppendLine("║    No transfer plan required.".PadRight(87) + "║");
        }
        else
        {
            sb.AppendLine("║  ┌──────────┬────────────────────────────┬─────────────┬─────────────┬─────────────────┐  ║");
            sb.AppendLine("║  │ Priority │ File                       │ Area        │ Transfer To │ Why             │  ║");
            sb.AppendLine("║  ├──────────┼────────────────────────────┼─────────────┼─────────────┼─────────────────┤  ║");
            foreach (var item in report.TransferPlan)
            {
                string fileTrunc = item.File.Length > 26 ? item.File[..23] + "..." : item.File;
                string areaTrunc = item.Area.Length > 11 ? item.Area[..8] + "..." : item.Area;
                string toTrunc = item.TransferTo.Length > 11 ? item.TransferTo[..8] + "..." : item.TransferTo;
                string whyTrunc = item.Why.Length > 15 ? item.Why[..12] + "..." : item.Why;

                string row = string.Format("║  │ {0,-8} │ {1,-26} │ {2,-11} │ {3,-11} │ {4,-15} │  ║",
                    item.Priority, fileTrunc, areaTrunc, toTrunc, whyTrunc);
                sb.AppendLine(row);
            }
            sb.AppendLine("║  └──────────┴────────────────────────────┴─────────────┴─────────────┴─────────────────┘  ║");
        }
        sb.AppendLine("║                                                                                      ║");

        // Estimated Onboarding
        string onboardingLine = $"  ESTIMATED ONBOARDING TIME: {report.EstimatedOnboardingTime}";
        sb.AppendLine($"║{onboardingLine}".PadRight(87) + "║");
        sb.AppendLine("╚══════════════════════════════════════════════════════════════════════════════════════╝");

        return sb.ToString();
    }

    public static DepartureRiskCommandOptions ParseOptionsFromParsedArgs(ParsedArgs parsed)
    {
        var options = new DepartureRiskCommandOptions
        {
            Developer = parsed.DepartureRiskDeveloper ?? parsed.ContributorName,
            RepoPath = parsed.RepoPath,
            Json = parsed.Settings.Json,
            Format = parsed.Settings.Format
        };

        if (parsed.RawArgs != null && parsed.RawArgs.Count > 0)
        {
            var rawOptions = ParseOptionsFromArgs(parsed.RawArgs.ToArray());
            if (string.IsNullOrEmpty(options.Developer)) options.Developer = rawOptions.Developer;
            if (options.RepoPath == "." && rawOptions.RepoPath != ".") options.RepoPath = rawOptions.RepoPath;
            if (!options.Json && rawOptions.Json) options.Json = true;
            if (options.Format == "human" && rawOptions.Format != "human") options.Format = rawOptions.Format;
        }

        return options;
    }

    public static DepartureRiskCommandOptions ParseOptionsFromArgs(string[] args)
    {
        var options = new DepartureRiskCommandOptions();
        if (args == null || args.Length == 0) return options;

        int startIndex = 0;
        if (string.Equals(args[0], "departure-risk", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(args[0], "departure_risk", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(args[0], "departurerisk", StringComparison.OrdinalIgnoreCase))
        {
            startIndex = 1;
        }

        for (int i = startIndex; i < args.Length; i++)
        {
            string arg = args[i];

            if (string.Equals(arg, "--developer", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "-d", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length) options.Developer = args[++i];
            }
            else if (string.Equals(arg, "--json", StringComparison.OrdinalIgnoreCase))
            {
                options.Json = true;
            }
            else if (string.Equals(arg, "--format", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(arg, "-f", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length) options.Format = args[++i];
            }
            else if (string.Equals(arg, "--repo", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(arg, "-r", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length) options.RepoPath = args[++i];
            }
            else if (!arg.StartsWith("-"))
            {
                if (string.IsNullOrEmpty(options.Developer))
                {
                    options.Developer = arg;
                }
                else
                {
                    options.RepoPath = arg;
                }
            }
        }

        return options;
    }
}
