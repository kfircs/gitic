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

public class BaselineCommandOptions
{
    public string? Subcommand { get; set; }
    public string? Tag { get; set; }
    public string? FromPath { get; set; }
    public string? ToPath { get; set; }
    public string? MetricName { get; set; }
    public string? AreaName { get; set; }
    public string? StorageDir { get; set; }
    public string RepoPath { get; set; } = ".";
    public bool Json { get; set; }
    public string Format { get; set; } = "human";
}

public class BaselineCommand : IGiticCommand
{
    private readonly BaselineCommandOptions _options;
    private readonly IBaselineEngine _baselineEngine;
    private readonly IRepositoryAnalyzer _analyzer;
    private readonly IGitClient? _gitClient;

    public BaselineCommand(
        ParsedArgs parsed,
        IBaselineEngine? baselineEngine = null,
        IRepositoryAnalyzer? analyzer = null,
        IGitClient? gitClient = null)
    {
        _baselineEngine = baselineEngine ?? new BaselineEngine();
        _analyzer = analyzer ?? new RepositoryAnalyzer();
        _gitClient = gitClient;

        _options = ParseOptionsFromParsedArgs(parsed);
    }

    public BaselineCommand(
        string[] args,
        IBaselineEngine? baselineEngine = null,
        IRepositoryAnalyzer? analyzer = null,
        IGitClient? gitClient = null)
    {
        _baselineEngine = baselineEngine ?? new BaselineEngine();
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
        if (string.IsNullOrWhiteSpace(_options.Subcommand))
        {
            string errMsg = "baseline requires a subcommand: save, diff, or trend. Try: gitic baseline --help\n";
            reporter?.WriteError(errMsg);
            return Cli.CliFailure(errMsg, exitCode: 2);
        }

        return _options.Subcommand.ToLowerInvariant() switch
        {
            "save" => await ExecuteSaveAsync(reporter, cancellationToken),
            "diff" => await ExecuteDiffAsync(reporter, cancellationToken),
            "trend" => await ExecuteTrendAsync(reporter, cancellationToken),
            _ => HandleUnknownSubcommand(_options.Subcommand, reporter)
        };
    }

    private CliResult HandleUnknownSubcommand(string subcommand, IConsoleReporter? reporter)
    {
        string errMsg = $"Unknown baseline subcommand: {subcommand}. Expected save, diff, or trend.\n";
        reporter?.WriteError(errMsg);
        return Cli.CliFailure(errMsg, exitCode: 2);
    }

    private async Task<CliResult> ExecuteSaveAsync(IConsoleReporter? reporter, CancellationToken cancellationToken)
    {
        string repoRoot = string.IsNullOrWhiteSpace(_options.RepoPath) ? "." : _options.RepoPath;
        var gitClient = _gitClient ?? new GitClient(repoRoot);
        string? resolvedRoot = null;
        try
        {
            resolvedRoot = await gitClient.GetRepositoryRootAsync(cancellationToken);
        }
        catch
        {
            // Ignore git lookup error if not in git repo
        }

        string actualRoot;
        if (!string.IsNullOrEmpty(resolvedRoot))
        {
            actualRoot = resolvedRoot;
        }
        else
        {
            if (!Directory.Exists(repoRoot))
            {
                try
                {
                    Directory.CreateDirectory(repoRoot);
                }
                catch
                {
                    // Fallback to current directory
                }
            }
            actualRoot = Path.GetFullPath(repoRoot);
        }

        string storageDir = ResolveStorageDir(actualRoot);

        try
        {
            var analyzeInput = new AnalyzeInput
            {
                RepoRoot = actualRoot,
                Command = AnalysisCommand.Hotspots,
                GitClient = gitClient
            };

            var analysisResult = await _analyzer.AnalyzeAsync(analyzeInput, cancellationToken);
            var snapshot = _baselineEngine.CreateSnapshot(analysisResult, tag: _options.Tag);

            string savedPath = _baselineEngine.SaveBaseline(storageDir, snapshot, _options.Tag);

            if (_options.Json || string.Equals(_options.Format, "json", StringComparison.OrdinalIgnoreCase))
            {
                string jsonOutput = JsonSerializer.Serialize(new
                {
                    status = "saved",
                    path = savedPath,
                    snapshot = snapshot
                }, JsonSerializationDefaults.Indented);
                reporter?.Write(jsonOutput);
                return Cli.CliSuccess(jsonOutput);
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Baseline snapshot saved successfully to: {savedPath}");
            sb.AppendLine($"  Tag: {snapshot.Tag ?? "none"}");
            sb.AppendLine($"  Commits: {snapshot.Summary.CommitCount}");
            sb.AppendLine($"  Files: {snapshot.Summary.FileCount}");
            sb.AppendLine($"  Total Churn: {snapshot.Summary.TotalChurn:N0}");
            sb.AppendLine($"  Total Touches: {snapshot.Summary.TotalTouches:N0}");

            string stdout = sb.ToString();
            reporter?.Write(stdout);
            return Cli.CliSuccess(stdout);
        }
        catch (Exception ex)
        {
            string errMsg = $"Failed to save baseline: {ex.Message}\n";
            reporter?.WriteError(errMsg);
            return Cli.CliFailure(errMsg, exitCode: 1);
        }
    }

    private async Task<CliResult> ExecuteDiffAsync(IConsoleReporter? reporter, CancellationToken cancellationToken)
    {
        await Task.Yield();
        string storageDir = ResolveStorageDir(_options.RepoPath);

        BaselineSnapshot baselineA;
        BaselineSnapshot baselineB;

        try
        {
            if (!string.IsNullOrEmpty(_options.FromPath) && !string.IsNullOrEmpty(_options.ToPath))
            {
                baselineA = _baselineEngine.LoadBaseline(_options.FromPath);
                baselineB = _baselineEngine.LoadBaseline(_options.ToPath);
            }
            else
            {
                var baselines = _baselineEngine.ListBaselines(storageDir);
                if (baselines.Count < 2)
                {
                    string errMsg = "baseline diff requires two snapshots. Provide --from <path> and --to <path>, or ensure at least two baselines exist in storage.\n";
                    reporter?.WriteError(errMsg);
                    return Cli.CliFailure(errMsg, exitCode: 1);
                }

                if (!string.IsNullOrEmpty(_options.FromPath))
                {
                    baselineA = _baselineEngine.LoadBaseline(_options.FromPath);
                    baselineB = baselines[^1];
                }
                else if (!string.IsNullOrEmpty(_options.ToPath))
                {
                    baselineA = baselines[^2];
                    baselineB = _baselineEngine.LoadBaseline(_options.ToPath);
                }
                else
                {
                    baselineA = baselines[^2];
                    baselineB = baselines[^1];
                }
            }

            var diff = _baselineEngine.DiffBaselines(baselineA, baselineB);

            if (_options.Json || string.Equals(_options.Format, "json", StringComparison.OrdinalIgnoreCase))
            {
                string jsonOutput = JsonSerializer.Serialize(diff, JsonSerializationDefaults.Indented);
                reporter?.Write(jsonOutput);
                return Cli.CliSuccess(jsonOutput);
            }

            string tableOutput = RenderDiffTable(diff);
            reporter?.Write(tableOutput);
            return Cli.CliSuccess(tableOutput);
        }
        catch (Exception ex)
        {
            string errMsg = $"Failed to calculate baseline diff: {ex.Message}\n";
            reporter?.WriteError(errMsg);
            return Cli.CliFailure(errMsg, exitCode: 1);
        }
    }

    private async Task<CliResult> ExecuteTrendAsync(IConsoleReporter? reporter, CancellationToken cancellationToken)
    {
        await Task.Yield();
        string storageDir = ResolveStorageDir(_options.RepoPath);

        try
        {
            var baselines = _baselineEngine.ListBaselines(storageDir);
            if (baselines.Count == 0)
            {
                string errMsg = $"No baseline snapshots found in storage directory '{storageDir}'.\n";
                reporter?.WriteError(errMsg);
                return Cli.CliFailure(errMsg, exitCode: 1);
            }

            string metric = string.IsNullOrWhiteSpace(_options.MetricName) ? "churn" : _options.MetricName;
            var trend = _baselineEngine.CalculateTrend(baselines, metric, _options.AreaName);

            if (_options.Json || string.Equals(_options.Format, "json", StringComparison.OrdinalIgnoreCase))
            {
                string jsonOutput = JsonSerializer.Serialize(trend, JsonSerializationDefaults.Indented);
                reporter?.Write(jsonOutput);
                return Cli.CliSuccess(jsonOutput);
            }

            string tableOutput = RenderTrendTable(trend);
            reporter?.Write(tableOutput);
            return Cli.CliSuccess(tableOutput);
        }
        catch (Exception ex)
        {
            string errMsg = $"Failed to calculate baseline trend: {ex.Message}\n";
            reporter?.WriteError(errMsg);
            return Cli.CliFailure(errMsg, exitCode: 1);
        }
    }

    private async Task<CliResult> ExecuteTrajectoryAsync(IConsoleReporter? reporter, CancellationToken cancellationToken)
    {
        var trajCmd = new ContributorTrajectoryCommand(new ContributorTrajectoryCommandOptions
        {
            Developer = _options.Tag,
            StorageDir = _options.StorageDir,
            RepoPath = _options.RepoPath,
            Json = _options.Json,
            Format = _options.Format
        }, _baselineEngine);
        return await trajCmd.ExecuteAsync(reporter, cancellationToken);
    }

    private string ResolveStorageDir(string repoRoot)
    {
        if (!string.IsNullOrWhiteSpace(_options.StorageDir))
        {
            return _options.StorageDir;
        }

        return Path.Combine(repoRoot, ".gitic", "baselines");
    }

    private static string RenderDiffTable(BaselineDiffResult diff)
    {
        var sb = new StringBuilder();
        sb.AppendLine("📊 Baseline Comparison");
        sb.AppendLine($"From: {(string.IsNullOrEmpty(diff.FromTag) ? diff.FromBaselineId[..Math.Min(8, diff.FromBaselineId.Length)] : diff.FromTag)} ({diff.FromTimestamp:yyyy-MM-dd HH:mm:ss} UTC)");
        sb.AppendLine($"To:   {(string.IsNullOrEmpty(diff.ToTag) ? diff.ToBaselineId[..Math.Min(8, diff.ToBaselineId.Length)] : diff.ToTag)} ({diff.ToTimestamp:yyyy-MM-dd HH:mm:ss} UTC)");
        sb.AppendLine();

        sb.AppendLine("SUMMARY DELTAS:");
        sb.AppendLine(string.Format("{0,-28} {1,14} {2,14} {3,14} {4,12}", "Metric", "From", "To", "Delta", "Change %"));
        sb.AppendLine(new string('-', 86));

        foreach (var delta in diff.SummaryDeltas)
        {
            string deltaSign = delta.AbsoluteDelta > 0 ? "+" : "";
            string pctSign = delta.PercentageDelta > 0 ? "+" : "";
            sb.AppendLine(string.Format("{0,-28} {1,14:N1} {2,14:N1} {3,14} {4,11:N1}%",
                delta.MetricName,
                delta.FromValue,
                delta.ToValue,
                $"{deltaSign}{delta.AbsoluteDelta:N1}",
                $"{pctSign}{delta.PercentageDelta}"));
        }

        if (diff.AddedAreas.Count > 0 || diff.RemovedAreas.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("AREA CHANGES:");
            if (diff.AddedAreas.Count > 0) sb.AppendLine($"  Added Areas: {string.Join(", ", diff.AddedAreas)}");
            if (diff.RemovedAreas.Count > 0) sb.AppendLine($"  Removed Areas: {string.Join(", ", diff.RemovedAreas)}");
        }

        return sb.ToString();
    }

    private static string RenderTrendTable(TrendResult trend)
    {
        var sb = new StringBuilder();
        string areaLabel = string.IsNullOrEmpty(trend.Area) ? "Overall" : $"Area: {trend.Area}";
        sb.AppendLine($"📈 Historical Trend: {trend.MetricName} ({areaLabel})");
        string changeSign = trend.AbsoluteChange > 0 ? "+" : "";
        string pctSign = trend.PercentageChange > 0 ? "+" : "";
        sb.AppendLine($"Direction: {trend.Direction} | Start: {trend.StartValue:N1} | End: {trend.EndValue:N1} | Change: {changeSign}{trend.AbsoluteChange:N1} ({pctSign}{trend.PercentageChange:N1}%)");
        sb.AppendLine($"Min: {trend.MinValue:N1} | Max: {trend.MaxValue:N1} | Avg: {trend.AverageValue:N1}");
        sb.AppendLine();

        sb.AppendLine(string.Format("{0,-22} {1,-14} {2,-12} {3,12} {4,14}", "Timestamp (UTC)", "Tag", "Commit", "Value", "Delta"));
        sb.AppendLine(new string('-', 78));

        double firstVal = trend.DataPoints.Count > 0 ? trend.DataPoints[0].Value : 0;
        foreach (var dp in trend.DataPoints)
        {
            double deltaFromStart = dp.Value - firstVal;
            string deltaSign = deltaFromStart > 0 ? "+" : "";
            string tagStr = string.IsNullOrEmpty(dp.Tag) ? "-" : dp.Tag;
            string commitStr = string.IsNullOrEmpty(dp.CommitHash) ? "-" : (dp.CommitHash.Length > 7 ? dp.CommitHash[..7] : dp.CommitHash);

            sb.AppendLine(string.Format("{0,-22} {1,-14} {2,-12} {3,12:N1} {4,14}",
                dp.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                tagStr,
                commitStr,
                dp.Value,
                $"{deltaSign}{deltaFromStart:N1}"));
        }

        return sb.ToString();
    }

    private static BaselineCommandOptions ParseOptionsFromParsedArgs(ParsedArgs parsed)
    {
        var options = new BaselineCommandOptions
        {
            Subcommand = parsed.BaselineSubcommand,
            Tag = parsed.BaselineTag,
            FromPath = parsed.BaselineFrom,
            ToPath = parsed.BaselineTo,
            MetricName = parsed.BaselineMetric,
            AreaName = parsed.BaselineArea,
            StorageDir = parsed.BaselineStorageDir,
            RepoPath = parsed.RepoPath,
            Json = parsed.Settings.Json,
            Format = parsed.Settings.Format
        };

        if (parsed.RawArgs != null && parsed.RawArgs.Count > 0)
        {
            var rawOptions = ParseOptionsFromArgs(parsed.RawArgs.ToArray());
            if (string.IsNullOrEmpty(options.Subcommand)) options.Subcommand = rawOptions.Subcommand;
            if (string.IsNullOrEmpty(options.Tag)) options.Tag = rawOptions.Tag;
            if (string.IsNullOrEmpty(options.FromPath)) options.FromPath = rawOptions.FromPath;
            if (string.IsNullOrEmpty(options.ToPath)) options.ToPath = rawOptions.ToPath;
            if (string.IsNullOrEmpty(options.MetricName)) options.MetricName = rawOptions.MetricName;
            if (string.IsNullOrEmpty(options.AreaName)) options.AreaName = rawOptions.AreaName;
            if (string.IsNullOrEmpty(options.StorageDir)) options.StorageDir = rawOptions.StorageDir;
            if (options.RepoPath == "." && rawOptions.RepoPath != ".") options.RepoPath = rawOptions.RepoPath;
            if (!options.Json && rawOptions.Json) options.Json = true;
            if (options.Format == "human" && rawOptions.Format != "human") options.Format = rawOptions.Format;
        }

        return options;
    }

    public static BaselineCommandOptions ParseOptionsFromArgs(string[] args)
    {
        var options = new BaselineCommandOptions();
        if (args == null || args.Length == 0) return options;

        int startIndex = 0;
        if (string.Equals(args[0], "baseline", StringComparison.OrdinalIgnoreCase))
        {
            startIndex = 1;
        }

        if (startIndex < args.Length && !args[startIndex].StartsWith("-"))
        {
            options.Subcommand = args[startIndex];
            startIndex++;
        }

        for (int i = startIndex; i < args.Length; i++)
        {
            string arg = args[i];

            if (string.Equals(arg, "--tag", StringComparison.OrdinalIgnoreCase) || string.Equals(arg, "-t", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length) options.Tag = args[++i];
            }
            else if (string.Equals(arg, "--from", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length) options.FromPath = args[++i];
            }
            else if (string.Equals(arg, "--to", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length) options.ToPath = args[++i];
            }
            else if (string.Equals(arg, "--metric", StringComparison.OrdinalIgnoreCase) || string.Equals(arg, "-m", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length) options.MetricName = args[++i];
            }
            else if (string.Equals(arg, "--area", StringComparison.OrdinalIgnoreCase) || string.Equals(arg, "-a", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length) options.AreaName = args[++i];
            }
            else if (string.Equals(arg, "--storage", StringComparison.OrdinalIgnoreCase) || string.Equals(arg, "--dir", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length) options.StorageDir = args[++i];
            }
            else if (string.Equals(arg, "--json", StringComparison.OrdinalIgnoreCase))
            {
                options.Json = true;
            }
            else if (string.Equals(arg, "--format", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length) options.Format = args[++i];
            }
            else if (string.Equals(arg, "--repo", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length) options.RepoPath = args[++i];
            }
            else if (!arg.StartsWith("-"))
            {
                if (string.IsNullOrEmpty(options.Subcommand))
                {
                    options.Subcommand = arg;
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
