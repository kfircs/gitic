using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Kfc.Cli.Core;
using Kfc.Cli.Terminal;

namespace Gitic;

/// <summary>
/// Categorization of contributor specialization shifts across longitudinal baselines.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SpecializationDriftCategory
{
    Deepening,
    Withdrawing,
    Expanding,
    Stable
}

/// <summary>
/// Represents a historical observation point of contributor output, rework, and cognitive load at a specific baseline.
/// </summary>
public class ContributorTrajectoryPoint
{
    [JsonPropertyName("snapshot_date")]
    public DateTimeOffset SnapshotDate { get; set; }

    [JsonPropertyName("commit_count")]
    public int CommitCount { get; set; }

    [JsonPropertyName("rework_rate")]
    public double ReworkRate { get; set; }

    [JsonPropertyName("context_switches_per_week")]
    public double ContextSwitchesPerWeek { get; set; }

    [JsonPropertyName("primary_areas")]
    public List<string> PrimaryAreas { get; set; } = new();

    [JsonPropertyName("tag")]
    public string? Tag { get; set; }

    [JsonPropertyName("commit_hash")]
    public string? CommitHash { get; set; }
}

/// <summary>
/// Represents the multi-baseline trajectory profile, timeline, and qualitative narrative of a developer.
/// </summary>
public class ContributorTrajectoryProfile
{
    [JsonPropertyName("developer")]
    public string Developer { get; set; } = string.Empty;

    [JsonPropertyName("drift_category")]
    public SpecializationDriftCategory DriftCategory { get; set; }

    [JsonPropertyName("timeline")]
    public List<ContributorTrajectoryPoint> Timeline { get; set; } = new();

    [JsonPropertyName("trajectory_narrative")]
    public string TrajectoryNarrative { get; set; } = string.Empty;

    [JsonIgnore]
    public ContributorTrajectoryPoint? Latest => Timeline.Count > 0 ? Timeline[^1] : null;

    [JsonIgnore]
    public ContributorTrajectoryPoint? Baseline => Timeline.Count > 0 ? Timeline[0] : null;

    [JsonIgnore]
    public int TotalSnapshots => Timeline.Count;
}

/// <summary>
/// Contract for aggregating contributor metrics across baselines and detecting evolution trends.
/// </summary>
public interface IContributorTrajectoryEngine
{
    List<ContributorTrajectoryProfile> AnalyzeTrajectories(IEnumerable<BaselineSnapshot> historicalBaselines);
    string FormatTimeline(ContributorTrajectoryProfile profile);
    string FormatSummaryTable(IEnumerable<ContributorTrajectoryProfile> profiles);
}

/// <summary>
/// Aggregates historical baseline snapshots to track developer specialization shifts, rework trends,
/// and cognitive load evolution over time.
/// </summary>
public class ContributorTrajectoryEngine : IContributorTrajectoryEngine
{
    public List<ContributorTrajectoryProfile> AnalyzeTrajectories(IEnumerable<BaselineSnapshot> historicalBaselines)
    {
        if (historicalBaselines == null)
        {
            return new List<ContributorTrajectoryProfile>();
        }

        var orderedBaselines = historicalBaselines.OrderBy(b => b.Timestamp).ToList();
        if (orderedBaselines.Count == 0)
        {
            return new List<ContributorTrajectoryProfile>();
        }

        // Collect all distinct contributors across snapshots
        var allContributors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var snap in orderedBaselines)
        {
            if (snap.Contributors != null)
            {
                foreach (var name in snap.Contributors.Keys)
                {
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        allContributors.Add(name.Trim());
                    }
                }
            }
        }

        var profiles = new List<ContributorTrajectoryProfile>();

        foreach (var dev in allContributors.OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            var timeline = new List<ContributorTrajectoryPoint>();
            bool hasAppeared = false;

            foreach (var snap in orderedBaselines)
            {
                if (snap.Contributors != null && snap.Contributors.TryGetValue(dev, out var metric))
                {
                    hasAppeared = true;
                    timeline.Add(new ContributorTrajectoryPoint
                    {
                        SnapshotDate = snap.Timestamp,
                        CommitCount = metric.CommitCount,
                        ReworkRate = metric.ReworkRate,
                        ContextSwitchesPerWeek = metric.ContextSwitchesPerWeek,
                        PrimaryAreas = new List<string>(metric.PrimaryAreas),
                        Tag = snap.Tag,
                        CommitHash = snap.CommitHash
                    });
                }
                else if (hasAppeared)
                {
                    // Contributor was previously recorded in earlier baseline, but is inactive in this one
                    timeline.Add(new ContributorTrajectoryPoint
                    {
                        SnapshotDate = snap.Timestamp,
                        CommitCount = 0,
                        ReworkRate = 0.0,
                        ContextSwitchesPerWeek = 0.0,
                        PrimaryAreas = new List<string>(),
                        Tag = snap.Tag,
                        CommitHash = snap.CommitHash
                    });
                }
            }

            if (timeline.Count == 0) continue;

            var driftCategory = DetermineDriftCategory(timeline);
            var profile = new ContributorTrajectoryProfile
            {
                Developer = dev,
                DriftCategory = driftCategory,
                Timeline = timeline
            };

            profile.TrajectoryNarrative = GenerateNarrative(profile);
            profiles.Add(profile);
        }

        return profiles;
    }

    /// <summary>
    /// Evaluates specialization drift across chronological trajectory points.
    /// </summary>
    public static SpecializationDriftCategory DetermineDriftCategory(IReadOnlyList<ContributorTrajectoryPoint> timeline)
    {
        if (timeline == null || timeline.Count < 2)
        {
            return SpecializationDriftCategory.Stable;
        }

        var first = timeline[0];
        var latest = timeline[^1];

        var initialAreas = first.PrimaryAreas ?? new List<string>();
        var finalAreas = latest.PrimaryAreas ?? new List<string>();

        var addedAreas = finalAreas.Except(initialAreas, StringComparer.OrdinalIgnoreCase).ToList();
        var removedAreas = initialAreas.Except(finalAreas, StringComparer.OrdinalIgnoreCase).ToList();

        // 1. Withdrawing:
        // - Latest commit count is 0
        // - Or final areas is empty while initial areas was not
        // - Or commit count dropped drastically (>= 60% drop) without adding new areas
        if (latest.CommitCount == 0 ||
            (initialAreas.Count > 0 && finalAreas.Count == 0) ||
            (first.CommitCount >= 5 && latest.CommitCount <= first.CommitCount * 0.4 && addedAreas.Count == 0))
        {
            return SpecializationDriftCategory.Withdrawing;
        }

        // 2. Expanding:
        // - New areas added that weren't present in initial areas
        // - Or primary area breadth increased
        if (addedAreas.Count > 0 || finalAreas.Count > initialAreas.Count)
        {
            return SpecializationDriftCategory.Expanding;
        }

        // 3. Deepening:
        // - Primary area breadth decreased (concentrated into fewer areas / dropped peripheral areas)
        // - Or context switches decreased significantly (>= 25% drop) with stable commit pace
        if (finalAreas.Count < initialAreas.Count)
        {
            return SpecializationDriftCategory.Deepening;
        }

        if (first.ContextSwitchesPerWeek > 2.0 && latest.ContextSwitchesPerWeek <= first.ContextSwitchesPerWeek * 0.75)
        {
            return SpecializationDriftCategory.Deepening;
        }

        return SpecializationDriftCategory.Stable;
    }

    /// <summary>
    /// Generates qualitative trajectory narrative describing specialization shifts, rework changes, and cognitive load trends.
    /// </summary>
    public static string GenerateNarrative(ContributorTrajectoryProfile profile)
    {
        if (profile.Timeline == null || profile.Timeline.Count == 0)
        {
            return $"{profile.Developer} has no historical trajectory data.";
        }

        if (profile.Timeline.Count == 1)
        {
            var single = profile.Timeline[0];
            string areas = single.PrimaryAreas.Count > 0 ? string.Join(", ", single.PrimaryAreas) : "general";
            return $"{profile.Developer} is tracked at a single baseline with {single.CommitCount} commits across {areas}. Longitudinal trajectory requires multiple baselines.";
        }

        var first = profile.Timeline[0];
        var latest = profile.Timeline[^1];
        var sb = new StringBuilder();

        switch (profile.DriftCategory)
        {
            case SpecializationDriftCategory.Deepening:
                string deepAreas = latest.PrimaryAreas.Count > 0 ? string.Join(", ", latest.PrimaryAreas) : "primary subsystem";
                sb.Append($"{profile.Developer} is deepening specialization into {deepAreas}. ");
                break;
            case SpecializationDriftCategory.Expanding:
                var newAreas = latest.PrimaryAreas.Except(first.PrimaryAreas, StringComparer.OrdinalIgnoreCase).ToList();
                string newAreasStr = newAreas.Count > 0 ? string.Join(", ", newAreas) : string.Join(", ", latest.PrimaryAreas);
                sb.Append($"{profile.Developer} is expanding domain scope into new areas ({newAreasStr}). ");
                break;
            case SpecializationDriftCategory.Withdrawing:
                sb.Append($"{profile.Developer} is withdrawing activity (commits decreased from {first.CommitCount} to {latest.CommitCount}). ");
                break;
            case SpecializationDriftCategory.Stable:
            default:
                string stableAreas = latest.PrimaryAreas.Count > 0 ? string.Join(", ", latest.PrimaryAreas) : "assigned areas";
                sb.Append($"{profile.Developer} maintains a stable focus across {stableAreas}. ");
                break;
        }

        // Rework rate narrative
        double reworkDelta = latest.ReworkRate - first.ReworkRate;
        if (Math.Abs(reworkDelta) >= 0.02)
        {
            if (reworkDelta < 0)
            {
                sb.Append($"Rework rate improved from {first.ReworkRate * 100:F1}% to {latest.ReworkRate * 100:F1}%. ");
            }
            else
            {
                sb.Append($"Rework rate increased from {first.ReworkRate * 100:F1}% to {latest.ReworkRate * 100:F1}%. ");
            }
        }

        // Cognitive load / Context switches narrative
        double switchDelta = latest.ContextSwitchesPerWeek - first.ContextSwitchesPerWeek;
        if (Math.Abs(switchDelta) >= 1.0)
        {
            if (switchDelta < 0)
            {
                sb.Append($"Context-switching decreased from {first.ContextSwitchesPerWeek:F1} to {latest.ContextSwitchesPerWeek:F1}/wk (reduced cognitive load). ");
            }
            else
            {
                sb.Append($"Context-switching increased from {first.ContextSwitchesPerWeek:F1} to {latest.ContextSwitchesPerWeek:F1}/wk (higher cognitive fragmentation). ");
            }
        }

        return sb.ToString().Trim();
    }

    /// <summary>
    /// Formats a detailed chronological growth timeline and narrative for a single contributor profile.
    /// </summary>
    public string FormatTimeline(ContributorTrajectoryProfile profile)
    {
        if (profile == null) throw new ArgumentNullException(nameof(profile));

        var sb = new StringBuilder();
        sb.AppendLine($"📊 Contributor Trajectory: {profile.Developer}");
        sb.AppendLine(new string('=', Math.Max(30, 26 + profile.Developer.Length)));
        sb.AppendLine($"Specialization Drift: {profile.DriftCategory}");
        sb.AppendLine();

        sb.AppendLine("Historical Growth Timeline:");
        sb.AppendLine(string.Format("{0,-12} | {1,8} | {2,10} | {3,12} | {4,-24}", "Date", "Commits", "Rework", "Switches/Wk", "Primary Areas"));
        sb.AppendLine(new string('-', 76));

        foreach (var pt in profile.Timeline)
        {
            string dateStr = pt.SnapshotDate.ToString("yyyy-MM-dd");
            string reworkStr = $"{pt.ReworkRate * 100:F1}%";
            string switchesStr = $"{pt.ContextSwitchesPerWeek:F1}/wk";
            string areasStr = pt.PrimaryAreas.Count > 0 ? string.Join(", ", pt.PrimaryAreas) : "(none)";

            sb.AppendLine(string.Format("{0,-12} | {1,8} | {2,10} | {3,12} | {4,-24}",
                dateStr,
                pt.CommitCount,
                reworkStr,
                switchesStr,
                areasStr));
        }

        sb.AppendLine();
        if (profile.Timeline.Count >= 2)
        {
            var first = profile.Timeline[0];
            var latest = profile.Timeline[^1];
            int commitDelta = latest.CommitCount - first.CommitCount;
            string commitSign = commitDelta > 0 ? "+" : "";
            double reworkDelta = (latest.ReworkRate - first.ReworkRate) * 100;
            string reworkSign = reworkDelta > 0 ? "+" : "";
            double switchDelta = latest.ContextSwitchesPerWeek - first.ContextSwitchesPerWeek;
            string switchSign = switchDelta > 0 ? "+" : "";

            sb.AppendLine("Evolution Summary:");
            sb.AppendLine($"  Commit Volume:    {first.CommitCount} → {latest.CommitCount} ({commitSign}{commitDelta})");
            sb.AppendLine($"  Rework Rate:      {first.ReworkRate * 100:F1}% → {latest.ReworkRate * 100:F1}% ({reworkSign}{reworkDelta:F1}%)");
            sb.AppendLine($"  Context Switches: {first.ContextSwitchesPerWeek:F1}/wk → {latest.ContextSwitchesPerWeek:F1}/wk ({switchSign}{switchDelta:F1}/wk)");
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(profile.TrajectoryNarrative))
        {
            sb.AppendLine($"Trajectory Insight: {profile.TrajectoryNarrative}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Formats a summary table for all contributor trajectories.
    /// </summary>
    public string FormatSummaryTable(IEnumerable<ContributorTrajectoryProfile> profiles)
    {
        var list = profiles?.ToList() ?? new List<ContributorTrajectoryProfile>();
        if (list.Count == 0)
        {
            return "No contributor trajectory profiles available.\n";
        }

        int consoleWidth = ConsoleUtils.GetBoundedConsoleWidth(null);
        var table = new ConsoleTableBuilder()
            .WithConsoleWidth(consoleWidth)
            .WithVisibleColumns(new List<string> { "developer", "drift", "snapshots", "baseline_commits", "latest_commits", "rework", "switches_wk" })
            .WithBorders(true, true, true)
            .AddColumnEx("developer", align: "left", widthPolicy: WidthPolicy.Stretch, minWidth: 14)
            .AddColumnEx("drift", width: 14, align: "center")
            .AddColumnEx("snapshots", width: 10, align: "right")
            .AddColumnEx("baseline_commits", width: 16, align: "right")
            .AddColumnEx("latest_commits", width: 16, align: "right")
            .AddColumnEx("rework", width: 12, align: "right")
            .AddColumnEx("switches_wk", width: 14, align: "right");

        foreach (var p in list)
        {
            var baseline = p.Baseline;
            var latest = p.Latest;

            table.AddRow(new Dictionary<string, string>
            {
                { "developer", p.Developer },
                { "drift", p.DriftCategory.ToString() },
                { "snapshots", p.TotalSnapshots.ToString() },
                { "baseline_commits", baseline?.CommitCount.ToString() ?? "0" },
                { "latest_commits", latest?.CommitCount.ToString() ?? "0" },
                { "rework", latest != null ? $"{latest.ReworkRate * 100:F1}%" : "0.0%" },
                { "switches_wk", latest != null ? $"{latest.ContextSwitchesPerWeek:F1}" : "0.0" }
            });
        }

        return table.Render();
    }
}

/// <summary>
/// CLI options for the Contributor Trajectory command.
/// </summary>
public class ContributorTrajectoryCommandOptions
{
    public string? Developer { get; set; }
    public string? StorageDir { get; set; }
    public string RepoPath { get; set; } = ".";
    public bool Json { get; set; }
    public string Format { get; set; } = "human";
}

/// <summary>
/// CLI command implementation for inspecting historical contributor growth trajectories across baselines.
/// </summary>
public class ContributorTrajectoryCommand : IGiticCommand
{
    private readonly ContributorTrajectoryCommandOptions _options;
    private readonly IBaselineEngine _baselineEngine;
    private readonly IContributorTrajectoryEngine _trajectoryEngine;

    public ContributorTrajectoryCommand(
        ContributorTrajectoryCommandOptions options,
        IBaselineEngine? baselineEngine = null,
        IContributorTrajectoryEngine? trajectoryEngine = null)
    {
        _options = options ?? new ContributorTrajectoryCommandOptions();
        _baselineEngine = baselineEngine ?? new BaselineEngine();
        _trajectoryEngine = trajectoryEngine ?? new ContributorTrajectoryEngine();
    }

    public ContributorTrajectoryCommand(
        ParsedArgs parsed,
        IBaselineEngine? baselineEngine = null,
        IContributorTrajectoryEngine? trajectoryEngine = null)
    {
        _baselineEngine = baselineEngine ?? new BaselineEngine();
        _trajectoryEngine = trajectoryEngine ?? new ContributorTrajectoryEngine();
        _options = ParseOptionsFromParsedArgs(parsed);
    }

    public ContributorTrajectoryCommand(
        string[] args,
        IBaselineEngine? baselineEngine = null,
        IContributorTrajectoryEngine? trajectoryEngine = null)
    {
        _baselineEngine = baselineEngine ?? new BaselineEngine();
        _trajectoryEngine = trajectoryEngine ?? new ContributorTrajectoryEngine();
        _options = ParseOptionsFromArgs(args);
    }

    public Task<CliResult> ExecuteAsync(IConsoleReporter reporter)
    {
        return ExecuteAsync(reporter, CancellationToken.None);
    }

    public async Task<CliResult> ExecuteAsync(IConsoleReporter? reporter, CancellationToken cancellationToken = default)
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

            var profiles = _trajectoryEngine.AnalyzeTrajectories(baselines);
            if (!string.IsNullOrWhiteSpace(_options.Developer))
            {
                profiles = profiles.Where(p =>
                    string.Equals(p.Developer, _options.Developer, StringComparison.OrdinalIgnoreCase) ||
                    p.Developer.Contains(_options.Developer, StringComparison.OrdinalIgnoreCase)).ToList();

                if (profiles.Count == 0)
                {
                    string errMsg = $"No trajectory profiles found for contributor '{_options.Developer}'.\n";
                    reporter?.WriteError(errMsg);
                    return Cli.CliFailure(errMsg, exitCode: 1);
                }
            }

            if (_options.Json || string.Equals(_options.Format, "json", StringComparison.OrdinalIgnoreCase))
            {
                string json = JsonSerializer.Serialize(profiles, JsonSerializationDefaults.Indented);
                reporter?.Write(json);
                return Cli.CliSuccess(json);
            }

            var sb = new StringBuilder();
            foreach (var profile in profiles)
            {
                sb.AppendLine(_trajectoryEngine.FormatTimeline(profile));
            }

            string output = sb.ToString();
            reporter?.Write(output);
            return Cli.CliSuccess(output);
        }
        catch (Exception ex)
        {
            string errMsg = $"Failed to calculate contributor trajectory: {ex.Message}\n";
            reporter?.WriteError(errMsg);
            return Cli.CliFailure(errMsg, exitCode: 1);
        }
    }

    private string ResolveStorageDir(string repoRoot)
    {
        if (!string.IsNullOrWhiteSpace(_options.StorageDir))
        {
            return _options.StorageDir;
        }

        return Path.Combine(repoRoot, ".gitic", "baselines");
    }

    public static ContributorTrajectoryCommandOptions ParseOptionsFromParsedArgs(ParsedArgs parsed)
    {
        var options = new ContributorTrajectoryCommandOptions
        {
            Developer = parsed.TrajectoryDeveloper ?? parsed.ContributorName ?? parsed.DepartureRiskDeveloper,
            StorageDir = parsed.BaselineStorageDir ?? parsed.SprintStorageDir,
            RepoPath = parsed.RepoPath,
            Json = parsed.Settings.Json,
            Format = parsed.Settings.Format
        };

        if (parsed.RawArgs != null && parsed.RawArgs.Count > 0)
        {
            var raw = ParseOptionsFromArgs(parsed.RawArgs.ToArray());
            if (string.IsNullOrEmpty(options.Developer)) options.Developer = raw.Developer;
            if (string.IsNullOrEmpty(options.StorageDir)) options.StorageDir = raw.StorageDir;
            if (options.RepoPath == "." && raw.RepoPath != ".") options.RepoPath = raw.RepoPath;
            if (!options.Json && raw.Json) options.Json = true;
            if (options.Format == "human" && raw.Format != "human") options.Format = raw.Format;
        }

        return options;
    }

    public static ContributorTrajectoryCommandOptions ParseOptionsFromArgs(string[] args)
    {
        var options = new ContributorTrajectoryCommandOptions();
        if (args == null || args.Length == 0) return options;

        int startIndex = 0;
        if (string.Equals(args[0], "trajectory", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(args[0], "contributor-trajectory", StringComparison.OrdinalIgnoreCase))
        {
            startIndex = 1;
        }
        else if (string.Equals(args[0], "baseline", StringComparison.OrdinalIgnoreCase) &&
                 args.Length > 1 && string.Equals(args[1], "trajectory", StringComparison.OrdinalIgnoreCase))
        {
            startIndex = 2;
        }
        else if (string.Equals(args[0], "contributor", StringComparison.OrdinalIgnoreCase))
        {
            startIndex = 1;
            if (startIndex < args.Length && !args[startIndex].StartsWith("-"))
            {
                options.Developer = args[startIndex];
                startIndex++;
            }
        }

        for (int i = startIndex; i < args.Length; i++)
        {
            string arg = args[i];
            if (string.Equals(arg, "--developer", StringComparison.OrdinalIgnoreCase) || string.Equals(arg, "-d", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length) options.Developer = args[++i];
            }
            else if (string.Equals(arg, "--storage", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(arg, "--storage-dir", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(arg, "--dir", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length) options.StorageDir = args[++i];
            }
            else if (string.Equals(arg, "--repo", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length) options.RepoPath = args[++i];
            }
            else if (string.Equals(arg, "--json", StringComparison.OrdinalIgnoreCase))
            {
                options.Json = true;
            }
            else if (string.Equals(arg, "--format", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length) options.Format = args[++i];
            }
            else if (string.Equals(arg, "--trajectory", StringComparison.OrdinalIgnoreCase))
            {
                // trajectory flag
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
