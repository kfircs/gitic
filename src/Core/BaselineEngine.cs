using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gitic;

public class BaselineSnapshot
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("tag")]
    public string? Tag { get; set; }

    [JsonPropertyName("commit_hash")]
    public string? CommitHash { get; set; }

    [JsonPropertyName("repo_root")]
    public string? RepoRoot { get; set; }

    [JsonPropertyName("summary")]
    public BaselineSummaryMetrics Summary { get; set; } = new();

    [JsonPropertyName("areas")]
    public Dictionary<string, AreaBaselineMetrics> Areas { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("files")]
    public Dictionary<string, FileBaselineMetrics> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("metrics")]
    public Dictionary<string, double> Metrics { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("contributors")]
    public Dictionary<string, ContributorBaselineMetrics> Contributors { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class ContributorBaselineMetrics
{
    [JsonPropertyName("developer")]
    public string Developer { get; set; } = string.Empty;

    [JsonPropertyName("commit_count")]
    public int CommitCount { get; set; }

    [JsonPropertyName("rework_rate")]
    public double ReworkRate { get; set; }

    [JsonPropertyName("context_switches_per_week")]
    public double ContextSwitchesPerWeek { get; set; }

    [JsonPropertyName("primary_areas")]
    public List<string> PrimaryAreas { get; set; } = new();

    [JsonPropertyName("area_distribution")]
    public Dictionary<string, double> AreaDistribution { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class BaselineSummaryMetrics
{
    [JsonPropertyName("commit_count")]
    public int CommitCount { get; set; }

    [JsonPropertyName("file_count")]
    public int FileCount { get; set; }

    [JsonPropertyName("total_churn")]
    public int TotalChurn { get; set; }

    [JsonPropertyName("total_touches")]
    public int TotalTouches { get; set; }

    [JsonPropertyName("average_lead_time_hours")]
    public double AverageLeadTimeHours { get; set; }

    [JsonPropertyName("zombie_files")]
    public int ZombieFiles { get; set; }

    [JsonPropertyName("zombie_lines")]
    public int ZombieLines { get; set; }

    [JsonPropertyName("features")]
    public int Features { get; set; }

    [JsonPropertyName("bugs")]
    public int Bugs { get; set; }

    [JsonPropertyName("technical_debt")]
    public int TechnicalDebt { get; set; }

    [JsonPropertyName("chores")]
    public int Chores { get; set; }

    [JsonPropertyName("reviewer_silos")]
    public int ReviewerSilos { get; set; }

    [JsonPropertyName("high_volume_commits")]
    public int HighVolumeCommits { get; set; }
}

public class AreaBaselineMetrics
{
    [JsonPropertyName("area")]
    public string Area { get; set; } = string.Empty;

    [JsonPropertyName("file_count")]
    public int FileCount { get; set; }

    [JsonPropertyName("touches")]
    public int Touches { get; set; }

    [JsonPropertyName("churn")]
    public int Churn { get; set; }

    [JsonPropertyName("heat_score")]
    public double HeatScore { get; set; }

    [JsonPropertyName("attention_score")]
    public double AttentionScore { get; set; }

    [JsonPropertyName("rework_rate")]
    public double? ReworkRate { get; set; }

    [JsonPropertyName("contributors")]
    public List<ContributorShare> Contributors { get; set; } = new();

    [JsonPropertyName("metrics")]
    public Dictionary<string, double> Metrics { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class FileBaselineMetrics
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("area")]
    public string Area { get; set; } = string.Empty;

    [JsonPropertyName("touches")]
    public int Touches { get; set; }

    [JsonPropertyName("churn")]
    public int Churn { get; set; }

    [JsonPropertyName("lines")]
    public int? Lines { get; set; }

    [JsonPropertyName("heat_score")]
    public double HeatScore { get; set; }

    [JsonPropertyName("attention_score")]
    public double AttentionScore { get; set; }

    [JsonPropertyName("rework_rate")]
    public double? ReworkRate { get; set; }
}

public class MetricDelta
{
    [JsonPropertyName("metric_name")]
    public string MetricName { get; set; } = string.Empty;

    [JsonPropertyName("from_value")]
    public double FromValue { get; set; }

    [JsonPropertyName("to_value")]
    public double ToValue { get; set; }

    [JsonPropertyName("absolute_delta")]
    public double AbsoluteDelta { get; set; }

    [JsonPropertyName("percentage_delta")]
    public double PercentageDelta { get; set; }

    public MetricDelta() { }

    public MetricDelta(string metricName, double fromValue, double toValue)
    {
        MetricName = metricName;
        FromValue = fromValue;
        ToValue = toValue;
        AbsoluteDelta = toValue - fromValue;

        if (Math.Abs(fromValue) < 1e-9)
        {
            PercentageDelta = toValue > 0 ? 100.0 : (toValue < 0 ? -100.0 : 0.0);
        }
        else
        {
            PercentageDelta = ((toValue - fromValue) / fromValue) * 100.0;
        }
    }
}

public class BaselineDiffResult
{
    [JsonPropertyName("from_baseline_id")]
    public string FromBaselineId { get; set; } = string.Empty;

    [JsonPropertyName("to_baseline_id")]
    public string ToBaselineId { get; set; } = string.Empty;

    [JsonPropertyName("from_tag")]
    public string? FromTag { get; set; }

    [JsonPropertyName("to_tag")]
    public string? ToTag { get; set; }

    [JsonPropertyName("from_timestamp")]
    public DateTimeOffset FromTimestamp { get; set; }

    [JsonPropertyName("to_timestamp")]
    public DateTimeOffset ToTimestamp { get; set; }

    [JsonPropertyName("summary_deltas")]
    public List<MetricDelta> SummaryDeltas { get; set; } = new();

    [JsonPropertyName("area_deltas")]
    public Dictionary<string, List<MetricDelta>> AreaDeltas { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("file_deltas")]
    public Dictionary<string, List<MetricDelta>> FileDeltas { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("added_areas")]
    public List<string> AddedAreas { get; set; } = new();

    [JsonPropertyName("removed_areas")]
    public List<string> RemovedAreas { get; set; } = new();

    [JsonPropertyName("added_files")]
    public List<string> AddedFiles { get; set; } = new();

    [JsonPropertyName("removed_files")]
    public List<string> RemovedFiles { get; set; } = new();

    public MetricDelta? GetSummaryDelta(string metricName)
    {
        return SummaryDeltas.FirstOrDefault(d => string.Equals(d.MetricName, metricName, StringComparison.OrdinalIgnoreCase));
    }

    public MetricDelta? GetAreaDelta(string area, string metricName)
    {
        if (AreaDeltas.TryGetValue(area, out var deltas))
        {
            return deltas.FirstOrDefault(d => string.Equals(d.MetricName, metricName, StringComparison.OrdinalIgnoreCase));
        }
        return null;
    }

    public MetricDelta? GetFileDelta(string path, string metricName)
    {
        if (FileDeltas.TryGetValue(path, out var deltas))
        {
            return deltas.FirstOrDefault(d => string.Equals(d.MetricName, metricName, StringComparison.OrdinalIgnoreCase));
        }
        return null;
    }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TrendDirection
{
    Stable,
    Increasing,
    Decreasing
}

public class TrendDataPoint
{
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; }

    [JsonPropertyName("tag")]
    public string? Tag { get; set; }

    [JsonPropertyName("commit_hash")]
    public string? CommitHash { get; set; }

    [JsonPropertyName("value")]
    public double Value { get; set; }
}

public class TrendResult
{
    [JsonPropertyName("metric_name")]
    public string MetricName { get; set; } = string.Empty;

    [JsonPropertyName("area")]
    public string? Area { get; set; }

    [JsonPropertyName("data_points")]
    public List<TrendDataPoint> DataPoints { get; set; } = new();

    [JsonPropertyName("start_value")]
    public double StartValue { get; set; }

    [JsonPropertyName("end_value")]
    public double EndValue { get; set; }

    [JsonPropertyName("absolute_change")]
    public double AbsoluteChange { get; set; }

    [JsonPropertyName("percentage_change")]
    public double PercentageChange { get; set; }

    [JsonPropertyName("direction")]
    public TrendDirection Direction { get; set; }

    [JsonPropertyName("min_value")]
    public double MinValue { get; set; }

    [JsonPropertyName("max_value")]
    public double MaxValue { get; set; }

    [JsonPropertyName("average_value")]
    public double AverageValue { get; set; }
}

public interface IBaselineEngine
{
    BaselineSnapshot CreateSnapshot(AnalysisResult result, string? tag = null, string? commitHash = null);
    string SaveBaseline(string storageDir, BaselineSnapshot snapshot, string? tag = null);
    BaselineSnapshot LoadBaseline(string path);
    IReadOnlyList<BaselineSnapshot> ListBaselines(string storageDir);
    BaselineDiffResult DiffBaselines(BaselineSnapshot baselineA, BaselineSnapshot baselineB);
    TrendResult CalculateTrend(IEnumerable<BaselineSnapshot> snapshots, string metricName, string? area = null);
}

public class BaselineEngine : IBaselineEngine
{
    public BaselineSnapshot CreateSnapshot(AnalysisResult result, string? tag = null, string? commitHash = null)
    {
        if (result == null) throw new ArgumentNullException(nameof(result));

        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        if (!string.IsNullOrEmpty(result.Analysis?.GeneratedAt) && DateTimeOffset.TryParse(result.Analysis.GeneratedAt, out var parsedDt))
        {
            timestamp = parsedDt;
        }

        var snapshot = new BaselineSnapshot
        {
            Id = Guid.NewGuid().ToString("N"),
            Timestamp = timestamp,
            Tag = tag,
            CommitHash = commitHash,
            RepoRoot = result.Analysis?.RepoRoot ?? string.Empty,
            Summary = new BaselineSummaryMetrics
            {
                CommitCount = result.Analysis?.CommitCount ?? 0,
                FileCount = result.Files?.Count ?? 0,
                TotalChurn = result.Files?.Sum(f => f.Churn) ?? 0,
                TotalTouches = result.Files?.Sum(f => f.Touches) ?? 0,
                AverageLeadTimeHours = result.LeadTimes?.AverageLeadTimeHours ?? 0,
                ZombieFiles = result.CuratedReports?.CodeRot?.ZombieFileCount ?? 0,
                ZombieLines = result.CuratedReports?.CodeRot?.ZombieLines ?? 0,
                Features = result.CuratedReports?.WorkClassification?.Features ?? 0,
                Bugs = result.CuratedReports?.WorkClassification?.Bugs ?? 0,
                TechnicalDebt = result.CuratedReports?.WorkClassification?.TechnicalDebt ?? 0,
                Chores = result.CuratedReports?.WorkClassification?.Chores ?? 0,
                ReviewerSilos = result.CuratedReports?.ReviewCollaboration?.ReviewerSilos ?? 0,
                HighVolumeCommits = result.CuratedReports?.AiCodeStrain?.HighVolumeCommits ?? 0
            }
        };

        if (result.Areas != null)
        {
            foreach (var area in result.Areas)
            {
                var areaFiles = result.Files?.Where(f => string.Equals(f.Area, area.Area, StringComparison.OrdinalIgnoreCase)).ToList();
                double areaHotspotDensity = 0.0;
                double areaSingleOwnerRatio = 0.0;
                if (areaFiles != null && areaFiles.Count > 0)
                {
                    int areaHotspots = areaFiles.Count(f => f.AttentionScore >= 60.0 || (f.AttentionScore == 0 && f.HeatScore >= 60.0));
                    areaHotspotDensity = (double)areaHotspots / areaFiles.Count;
                    int singleOwners = areaFiles.Count(f =>
                        f.KnowledgeSilo?.IsSilo == true ||
                        (f.KnowledgeSilo != null && f.KnowledgeSilo.TopOwnerShare > 0.50) ||
                        (f.KnowledgeSilo != null && f.KnowledgeSilo.TruckFactor == 1) ||
                        f.ContributorCount == 1 ||
                        (f.Contributors != null && f.Contributors.Count == 1));
                    areaSingleOwnerRatio = (double)singleOwners / areaFiles.Count;
                }
                else if (area.Contributors != null && area.Contributors.Count > 0)
                {
                    areaSingleOwnerRatio = area.Contributors.Max(c => c.ActivityShare);
                }

                snapshot.Areas[area.Area] = new AreaBaselineMetrics
                {
                    Area = area.Area,
                    FileCount = area.FileCount,
                    Touches = area.Touches,
                    Churn = area.Churn,
                    HeatScore = area.HeatScore,
                    AttentionScore = area.AttentionScore,
                    ReworkRate = area.ReworkRate,
                    Contributors = area.Contributors?.Select(c => new ContributorShare
                    {
                        Name = c.Name,
                        Email = c.Email,
                        Activity = c.Activity,
                        ActivityShare = c.ActivityShare
                    }).ToList() ?? new(),
                    Metrics = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["file_count"] = area.FileCount,
                        ["touches"] = area.Touches,
                        ["churn"] = area.Churn,
                        ["heat_score"] = area.HeatScore,
                        ["attention_score"] = area.AttentionScore,
                        ["rework_rate"] = area.ReworkRate ?? 0.0,
                        ["hotspot_density"] = areaHotspotDensity,
                        ["single_owner_ratio"] = areaSingleOwnerRatio
                    }
                };
            }
        }

        if (result.Files != null)
        {
            foreach (var file in result.Files)
            {
                snapshot.Files[file.Path] = new FileBaselineMetrics
                {
                    Path = file.Path,
                    Area = file.Area,
                    Touches = file.Touches,
                    Churn = file.Churn,
                    Lines = file.Lines,
                    HeatScore = file.HeatScore,
                    AttentionScore = file.AttentionScore,
                    ReworkRate = file.ReworkRate
                };
            }
        }

        snapshot.Metrics["commits"] = snapshot.Summary.CommitCount;
        snapshot.Metrics["commit_count"] = snapshot.Summary.CommitCount;
        snapshot.Metrics["files"] = snapshot.Summary.FileCount;
        snapshot.Metrics["file_count"] = snapshot.Summary.FileCount;
        snapshot.Metrics["churn"] = snapshot.Summary.TotalChurn;
        snapshot.Metrics["total_churn"] = snapshot.Summary.TotalChurn;
        snapshot.Metrics["touches"] = snapshot.Summary.TotalTouches;
        snapshot.Metrics["total_touches"] = snapshot.Summary.TotalTouches;
        snapshot.Metrics["lead_time"] = snapshot.Summary.AverageLeadTimeHours;
        snapshot.Metrics["average_lead_time_hours"] = snapshot.Summary.AverageLeadTimeHours;
        snapshot.Metrics["zombie_files"] = snapshot.Summary.ZombieFiles;
        snapshot.Metrics["zombie_lines"] = snapshot.Summary.ZombieLines;
        snapshot.Metrics["features"] = snapshot.Summary.Features;
        snapshot.Metrics["bugs"] = snapshot.Summary.Bugs;
        snapshot.Metrics["technical_debt"] = snapshot.Summary.TechnicalDebt;
        snapshot.Metrics["tech_debt"] = snapshot.Summary.TechnicalDebt;
        snapshot.Metrics["chores"] = snapshot.Summary.Chores;
        snapshot.Metrics["reviewer_silos"] = snapshot.Summary.ReviewerSilos;
        snapshot.Metrics["high_volume_commits"] = snapshot.Summary.HighVolumeCommits;

        if (result.DxIndex != null)
        {
            snapshot.Metrics["dx_index"] = result.DxIndex.CompositeScore;
        }
        else
        {
            try
            {
                var dx = DxIndexCalculator.Compute(result);
                snapshot.Metrics["dx_index"] = dx.CompositeScore;
            }
            catch
            {
                // DxIndex calculation is best-effort for snapshot metrics
            }
        }

        if (result.Files != null && result.Files.Count > 0)
        {
            int hotspots = result.Files.Count(f => f.AttentionScore >= 60.0 || (f.AttentionScore == 0 && f.HeatScore >= 60.0));
            snapshot.Metrics["hotspot_density"] = (double)hotspots / result.Files.Count;
        }

        if (result.LeadTimes?.Merges != null && result.LeadTimes.Merges.Count > 0)
        {
            var p90 = PercentileCalculator.CalculatePercentile(result.LeadTimes.Merges.Select(m => m.LeadTimeHours), 90.0);
            snapshot.Metrics["p90_lead_time"] = p90;
            snapshot.Metrics["p90_lead_time_hours"] = p90;
        }

        if (result.Contributors != null)
        {
            var cogLookup = (result.CognitiveProfiles ?? new List<ContributorCognitiveProfile>())
                .ToDictionary(p => p.Developer, p => p, StringComparer.OrdinalIgnoreCase);

            foreach (var contributor in result.Contributors)
            {
                if (string.IsNullOrWhiteSpace(contributor.Name)) continue;

                cogLookup.TryGetValue(contributor.Name, out var cogProfile);

                var primaryAreas = contributor.Areas?
                    .OrderByDescending(a => a.Activity)
                    .Select(a => a.Area)
                    .Where(a => !string.IsNullOrEmpty(a))
                    .ToList() ?? new List<string>();

                var areaDist = contributor.Areas?
                    .Where(a => !string.IsNullOrEmpty(a.Area))
                    .ToDictionary(a => a.Area, a => a.ActivityShare, StringComparer.OrdinalIgnoreCase)
                    ?? new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

                double devReworkRate = 0.0;
                var devFiles = result.Files?.Where(f => f.Contributors?.Any(c => string.Equals(c.Name, contributor.Name, StringComparison.OrdinalIgnoreCase)) == true).ToList();
                if (devFiles != null && devFiles.Count > 0)
                {
                    double totalWeight = devFiles.Sum(f => f.Touches);
                    if (totalWeight > 0)
                    {
                        devReworkRate = devFiles.Sum(f => (f.ReworkRate ?? 0.0) * f.Touches) / totalWeight;
                    }
                }

                snapshot.Contributors[contributor.Name] = new ContributorBaselineMetrics
                {
                    Developer = contributor.Name,
                    CommitCount = cogProfile?.TotalCommits ?? (int)Math.Round(contributor.TotalActivity),
                    ReworkRate = Math.Round(devReworkRate, 3),
                    ContextSwitchesPerWeek = cogProfile?.EstimatedSwitchesPerWeek ?? 0.0,
                    PrimaryAreas = primaryAreas,
                    AreaDistribution = areaDist
                };
            }
        }

        if (result.CognitiveProfiles != null)
        {
            foreach (var cog in result.CognitiveProfiles)
            {
                if (string.IsNullOrWhiteSpace(cog.Developer)) continue;
                if (!snapshot.Contributors.ContainsKey(cog.Developer))
                {
                    snapshot.Contributors[cog.Developer] = new ContributorBaselineMetrics
                    {
                        Developer = cog.Developer,
                        CommitCount = cog.TotalCommits,
                        ReworkRate = 0.0,
                        ContextSwitchesPerWeek = cog.EstimatedSwitchesPerWeek,
                        PrimaryAreas = !string.IsNullOrEmpty(cog.PrimaryArea) ? new List<string> { cog.PrimaryArea } : new List<string>(),
                        AreaDistribution = cog.AreaDistribution.ToDictionary(kv => kv.Key, kv => (double)kv.Value, StringComparer.OrdinalIgnoreCase)
                    };
                }
            }
        }

        return snapshot;
    }

    public string SaveBaseline(string storageDir, BaselineSnapshot snapshot, string? tag = null)
    {
        if (string.IsNullOrWhiteSpace(storageDir)) throw new ArgumentException("Storage directory cannot be null or empty.", nameof(storageDir));
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

        if (!string.IsNullOrWhiteSpace(tag))
        {
            snapshot.Tag = tag;
        }

        string filePath;
        if (storageDir.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            filePath = storageDir;
            string? dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
        else
        {
            Directory.CreateDirectory(storageDir);
            string timeStr = snapshot.Timestamp.ToString("yyyyMMdd_HHmmss");
            string namePart = !string.IsNullOrWhiteSpace(snapshot.Tag) 
                ? SanitizeFileName(snapshot.Tag) 
                : (snapshot.Id.Length >= 8 ? snapshot.Id[..8] : snapshot.Id);
            filePath = Path.Combine(storageDir, $"baseline_{timeStr}_{namePart}.json");
        }

        string json = JsonSerializer.Serialize(snapshot, JsonSerializationDefaults.Indented);
        File.WriteAllText(filePath, json);
        return filePath;
    }

    public BaselineSnapshot LoadBaseline(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path cannot be null or empty.", nameof(path));

        if (Directory.Exists(path))
        {
            var baselines = ListBaselines(path);
            if (baselines.Count == 0)
            {
                throw new FileNotFoundException($"No baseline snapshots found in directory '{path}'.");
            }
            return baselines[^1];
        }

        if (!File.Exists(path))
        {
            string fallbackDir = Path.Combine(".gitic", "baselines");
            if (Directory.Exists(fallbackDir))
            {
                var tagged = ListBaselines(fallbackDir).FirstOrDefault(b => string.Equals(b.Tag, path, StringComparison.OrdinalIgnoreCase));
                if (tagged != null) return tagged;
            }

            throw new FileNotFoundException($"Baseline file not found at '{path}'.", path);
        }

        string json = File.ReadAllText(path);
        var snapshot = JsonSerializer.Deserialize<BaselineSnapshot>(json, JsonSerializationDefaults.Indented);
        if (snapshot == null)
        {
            throw new InvalidOperationException($"Failed to deserialize baseline snapshot from '{path}'.");
        }

        return snapshot;
    }

    public IReadOnlyList<BaselineSnapshot> ListBaselines(string storageDir)
    {
        if (string.IsNullOrWhiteSpace(storageDir) || !Directory.Exists(storageDir))
        {
            return Array.Empty<BaselineSnapshot>();
        }

        var files = Directory.GetFiles(storageDir, "*.json");
        var list = new List<BaselineSnapshot>();

        foreach (var file in files)
        {
            try
            {
                string json = File.ReadAllText(file);
                var snapshot = JsonSerializer.Deserialize<BaselineSnapshot>(json, JsonSerializationDefaults.Indented);
                if (snapshot != null)
                {
                    list.Add(snapshot);
                }
            }
            catch
            {
                // Skip invalid or unreadable JSON files
            }
        }

        return list.OrderBy(s => s.Timestamp).ToList();
    }

    public BaselineDiffResult DiffBaselines(BaselineSnapshot baselineA, BaselineSnapshot baselineB)
    {
        if (baselineA == null) throw new ArgumentNullException(nameof(baselineA));
        if (baselineB == null) throw new ArgumentNullException(nameof(baselineB));

        var diff = new BaselineDiffResult
        {
            FromBaselineId = baselineA.Id,
            ToBaselineId = baselineB.Id,
            FromTag = baselineA.Tag,
            ToTag = baselineB.Tag,
            FromTimestamp = baselineA.Timestamp,
            ToTimestamp = baselineB.Timestamp
        };

        // Summary Deltas
        diff.SummaryDeltas.Add(new MetricDelta("commit_count", baselineA.Summary.CommitCount, baselineB.Summary.CommitCount));
        diff.SummaryDeltas.Add(new MetricDelta("file_count", baselineA.Summary.FileCount, baselineB.Summary.FileCount));
        diff.SummaryDeltas.Add(new MetricDelta("total_churn", baselineA.Summary.TotalChurn, baselineB.Summary.TotalChurn));
        diff.SummaryDeltas.Add(new MetricDelta("total_touches", baselineA.Summary.TotalTouches, baselineB.Summary.TotalTouches));
        diff.SummaryDeltas.Add(new MetricDelta("average_lead_time_hours", baselineA.Summary.AverageLeadTimeHours, baselineB.Summary.AverageLeadTimeHours));
        diff.SummaryDeltas.Add(new MetricDelta("zombie_files", baselineA.Summary.ZombieFiles, baselineB.Summary.ZombieFiles));
        diff.SummaryDeltas.Add(new MetricDelta("zombie_lines", baselineA.Summary.ZombieLines, baselineB.Summary.ZombieLines));
        diff.SummaryDeltas.Add(new MetricDelta("features", baselineA.Summary.Features, baselineB.Summary.Features));
        diff.SummaryDeltas.Add(new MetricDelta("bugs", baselineA.Summary.Bugs, baselineB.Summary.Bugs));
        diff.SummaryDeltas.Add(new MetricDelta("technical_debt", baselineA.Summary.TechnicalDebt, baselineB.Summary.TechnicalDebt));
        diff.SummaryDeltas.Add(new MetricDelta("chores", baselineA.Summary.Chores, baselineB.Summary.Chores));
        diff.SummaryDeltas.Add(new MetricDelta("reviewer_silos", baselineA.Summary.ReviewerSilos, baselineB.Summary.ReviewerSilos));
        diff.SummaryDeltas.Add(new MetricDelta("high_volume_commits", baselineA.Summary.HighVolumeCommits, baselineB.Summary.HighVolumeCommits));

        // Area Deltas
        var allAreas = new HashSet<string>(baselineA.Areas.Keys, StringComparer.OrdinalIgnoreCase);
        allAreas.UnionWith(baselineB.Areas.Keys);

        foreach (var areaName in allAreas)
        {
            bool inA = baselineA.Areas.TryGetValue(areaName, out var areaA);
            bool inB = baselineB.Areas.TryGetValue(areaName, out var areaB);

            if (!inA && inB)
            {
                diff.AddedAreas.Add(areaName);
            }
            else if (inA && !inB)
            {
                diff.RemovedAreas.Add(areaName);
            }

            var deltas = new List<MetricDelta>
            {
                new("file_count", areaA?.FileCount ?? 0, areaB?.FileCount ?? 0),
                new("touches", areaA?.Touches ?? 0, areaB?.Touches ?? 0),
                new("churn", areaA?.Churn ?? 0, areaB?.Churn ?? 0),
                new("heat_score", areaA?.HeatScore ?? 0.0, areaB?.HeatScore ?? 0.0),
                new("attention_score", areaA?.AttentionScore ?? 0.0, areaB?.AttentionScore ?? 0.0),
                new("rework_rate", areaA?.ReworkRate ?? 0.0, areaB?.ReworkRate ?? 0.0)
            };

            diff.AreaDeltas[areaName] = deltas;
        }

        // File Deltas
        var allFiles = new HashSet<string>(baselineA.Files.Keys, StringComparer.OrdinalIgnoreCase);
        allFiles.UnionWith(baselineB.Files.Keys);

        foreach (var filePath in allFiles)
        {
            bool inA = baselineA.Files.TryGetValue(filePath, out var fileA);
            bool inB = baselineB.Files.TryGetValue(filePath, out var fileB);

            if (!inA && inB)
            {
                diff.AddedFiles.Add(filePath);
            }
            else if (inA && !inB)
            {
                diff.RemovedFiles.Add(filePath);
            }

            var deltas = new List<MetricDelta>
            {
                new("touches", fileA?.Touches ?? 0, fileB?.Touches ?? 0),
                new("churn", fileA?.Churn ?? 0, fileB?.Churn ?? 0),
                new("lines", fileA?.Lines ?? 0, fileB?.Lines ?? 0),
                new("heat_score", fileA?.HeatScore ?? 0.0, fileB?.HeatScore ?? 0.0),
                new("attention_score", fileA?.AttentionScore ?? 0.0, fileB?.AttentionScore ?? 0.0),
                new("rework_rate", fileA?.ReworkRate ?? 0.0, fileB?.ReworkRate ?? 0.0)
            };

            diff.FileDeltas[filePath] = deltas;
        }

        return diff;
    }

    public TrendResult CalculateTrend(IEnumerable<BaselineSnapshot> snapshots, string metricName, string? area = null)
    {
        if (snapshots == null) throw new ArgumentNullException(nameof(snapshots));
        if (string.IsNullOrWhiteSpace(metricName)) throw new ArgumentException("Metric name cannot be null or empty.", nameof(metricName));

        var ordered = snapshots.OrderBy(s => s.Timestamp).ToList();
        var result = new TrendResult
        {
            MetricName = metricName,
            Area = area
        };

        foreach (var snapshot in ordered)
        {
            double val = ExtractMetricValue(snapshot, metricName, area);
            result.DataPoints.Add(new TrendDataPoint
            {
                Timestamp = snapshot.Timestamp,
                Tag = snapshot.Tag,
                CommitHash = snapshot.CommitHash,
                Value = val
            });
        }

        if (result.DataPoints.Count > 0)
        {
            result.StartValue = result.DataPoints[0].Value;
            result.EndValue = result.DataPoints[^1].Value;
            result.AbsoluteChange = result.EndValue - result.StartValue;

            if (Math.Abs(result.StartValue) < 1e-9)
            {
                result.PercentageChange = result.EndValue > 0 ? 100.0 : (result.EndValue < 0 ? -100.0 : 0.0);
            }
            else
            {
                result.PercentageChange = ((result.EndValue - result.StartValue) / result.StartValue) * 100.0;
            }

            if (Math.Abs(result.AbsoluteChange) < 1e-6)
            {
                result.Direction = TrendDirection.Stable;
            }
            else
            {
                result.Direction = result.AbsoluteChange > 0 ? TrendDirection.Increasing : TrendDirection.Decreasing;
            }

            result.MinValue = result.DataPoints.Min(d => d.Value);
            result.MaxValue = result.DataPoints.Max(d => d.Value);
            result.AverageValue = result.DataPoints.Average(d => d.Value);
        }

        return result;
    }

    private static double ExtractMetricValue(BaselineSnapshot snapshot, string metricName, string? area)
    {
        if (!string.IsNullOrWhiteSpace(area))
        {
            if (snapshot.Areas.TryGetValue(area, out var areaMetrics))
            {
                return metricName.ToLowerInvariant() switch
                {
                    "churn" => areaMetrics.Churn,
                    "touches" => areaMetrics.Touches,
                    "files" or "file_count" => areaMetrics.FileCount,
                    "heat" or "heat_score" => areaMetrics.HeatScore,
                    "attention" or "attention_score" => areaMetrics.AttentionScore,
                    "rework" or "rework_rate" => areaMetrics.ReworkRate ?? 0.0,
                    _ => areaMetrics.Metrics.TryGetValue(metricName, out var customVal) ? customVal : 0.0
                };
            }
            return 0.0;
        }

        if (snapshot.Metrics.TryGetValue(metricName, out var directVal))
        {
            return directVal;
        }

        return metricName.ToLowerInvariant() switch
        {
            "commits" or "commit_count" => snapshot.Summary.CommitCount,
            "files" or "file_count" => snapshot.Summary.FileCount,
            "churn" or "total_churn" => snapshot.Summary.TotalChurn,
            "touches" or "total_touches" => snapshot.Summary.TotalTouches,
            "lead_time" or "average_lead_time_hours" => snapshot.Summary.AverageLeadTimeHours,
            "zombie_files" => snapshot.Summary.ZombieFiles,
            "zombie_lines" => snapshot.Summary.ZombieLines,
            "features" => snapshot.Summary.Features,
            "bugs" => snapshot.Summary.Bugs,
            "tech_debt" or "technical_debt" => snapshot.Summary.TechnicalDebt,
            "chores" => snapshot.Summary.Chores,
            "reviewer_silos" => snapshot.Summary.ReviewerSilos,
            "high_volume_commits" => snapshot.Summary.HighVolumeCommits,
            _ => 0.0
        };
    }

    private static string SanitizeFileName(string input)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var chars = input.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray();
        return new string(chars);
    }
}
