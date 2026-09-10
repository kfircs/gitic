using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gitic;

/// <summary>
/// Type of violation detected during quality gate evaluation.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GateViolationType
{
    MaxThresholdExceeded,
    MinThresholdBreached,
    RegressionExceeded
}

/// <summary>
/// Defines a single quality gate assertion rule with optional min/max thresholds,
/// scope (global or per_area), and baseline regression tolerance.
/// </summary>
public class QualityGateRule
{
    [JsonPropertyName("metric")]
    public string MetricName { get; set; } = string.Empty;

    [JsonPropertyName("metric_name")]
    public string? MetricNameAlias
    {
        get => MetricName;
        set { if (!string.IsNullOrEmpty(value)) MetricName = value; }
    }

    [JsonPropertyName("scope")]
    public string Scope { get; set; } = "global";

    [JsonPropertyName("max")]
    public double? MaxThreshold { get; set; }

    [JsonPropertyName("max_threshold")]
    public double? MaxThresholdAlias
    {
        get => MaxThreshold;
        set { if (value.HasValue) MaxThreshold = value; }
    }

    [JsonPropertyName("min")]
    public double? MinThreshold { get; set; }

    [JsonPropertyName("min_threshold")]
    public double? MinThresholdAlias
    {
        get => MinThreshold;
        set { if (value.HasValue) MinThreshold = value; }
    }

    [JsonPropertyName("regression_max")]
    public double? MaxRegressionAllowed { get; set; }

    [JsonPropertyName("max_regression_allowed")]
    public double? MaxRegressionAllowedAlias
    {
        get => MaxRegressionAllowed;
        set { if (value.HasValue) MaxRegressionAllowed = value; }
    }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    public QualityGateRule() { }

    public QualityGateRule(
        string metricName,
        string scope = "global",
        double? max = null,
        double? min = null,
        double? maxRegression = null,
        string? message = null)
    {
        MetricName = metricName;
        Scope = scope;
        MaxThreshold = max;
        MinThreshold = min;
        MaxRegressionAllowed = maxRegression;
        Message = message;
    }
}

/// <summary>
/// Represents an assertion violation detected during quality gate evaluation.
/// </summary>
public class GateViolation
{
    [JsonPropertyName("rule")]
    public QualityGateRule Rule { get; set; } = new();

    [JsonPropertyName("metric_name")]
    public string MetricName { get; set; } = string.Empty;

    [JsonPropertyName("scope")]
    public string Scope { get; set; } = "global";

    [JsonPropertyName("area")]
    public string? Area { get; set; }

    [JsonPropertyName("file")]
    public string? File { get; set; }

    [JsonPropertyName("line")]
    public int? Line { get; set; } = 1;

    [JsonPropertyName("actual_value")]
    public double ActualValue { get; set; }

    [JsonPropertyName("threshold_value")]
    public double? ThresholdValue { get; set; }

    [JsonPropertyName("baseline_value")]
    public double? BaselineValue { get; set; }

    [JsonPropertyName("regression_value")]
    public double? RegressionValue { get; set; }

    [JsonPropertyName("violation_type")]
    public GateViolationType ViolationType { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Result of evaluating quality gate assertions against repository analysis metrics.
/// </summary>
public class QualityGateEvaluationResult
{
    [JsonPropertyName("is_passed")]
    public bool IsPassed { get; set; } = true;

    [JsonPropertyName("violations")]
    public List<GateViolation> Violations { get; set; } = new();

    [JsonPropertyName("rules_evaluated")]
    public int RulesEvaluated { get; set; }

    [JsonPropertyName("rules_passed")]
    public int RulesPassed { get; set; }

    [JsonPropertyName("rules_failed")]
    public int RulesFailed => Violations.Count;

    [JsonPropertyName("evaluated_at")]
    public DateTimeOffset EvaluatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string FormatGitHubAnnotations()
    {
        if (Violations == null || Violations.Count == 0)
        {
            return string.Empty;
        }

        var lines = new List<string>();
        foreach (var v in Violations)
        {
            string title = !string.IsNullOrEmpty(v.Area)
                ? $"Quality Gate: {v.MetricName} [{v.Area}]"
                : $"Quality Gate: {v.MetricName}";

            string filePart = !string.IsNullOrEmpty(v.File)
                ? $"file={v.File},line={v.Line ?? 1},"
                : "";

            string escapedMsg = v.Message.Replace("\r", "").Replace("\n", "%0A");
            lines.Add($"::error {filePart}title={title}::{escapedMsg}");
        }

        return string.Join("\n", lines);
    }

    public string FormatTerminalSummary()
    {
        var sb = new StringBuilder();
        string statusText = IsPassed ? "PASSED" : "FAILED";
        string icon = IsPassed ? "✅" : "❌";

        sb.AppendLine("╔══════════════════════════════════════════════════════════════════════════╗");
        sb.AppendLine($"║  🚦 CI Quality Gate Evaluation: {statusText,-40} ║");
        sb.AppendLine("╠══════════════════════════════════════════════════════════════════════════╣");

        if (IsPassed)
        {
            sb.AppendLine("║  All quality gate assertions passed successfully.                        ║");
            sb.AppendLine($"║  Rules evaluated: {RulesEvaluated,-54} ║");
        }
        else
        {
            sb.AppendLine($"║  {icon} {Violations.Count} gate assertion violation(s) detected:{"",-38} ║");
            sb.AppendLine("╟──────────────────────────────────────────────────────────────────────────╢");

            foreach (var v in Violations)
            {
                string scopeInfo = !string.IsNullOrEmpty(v.Area) ? $"area '{v.Area}'" : "global";
                string typeDesc = v.ViolationType switch
                {
                    GateViolationType.MaxThresholdExceeded => $"exceeds max {v.ThresholdValue:0.##}",
                    GateViolationType.MinThresholdBreached => $"below min {v.ThresholdValue:0.##}",
                    GateViolationType.RegressionExceeded => $"regressed by {v.RegressionValue:0.##} (max {v.Rule.MaxRegressionAllowed:0.##})",
                    _ => "violated"
                };

                sb.AppendLine($"║  • [{v.MetricName}] ({scopeInfo}): actual {v.ActualValue:0.##} {typeDesc}");
                if (!string.IsNullOrEmpty(v.Message))
                {
                    sb.AppendLine($"║    ➜ {v.Message}");
                }
            }
        }

        sb.AppendLine("╚══════════════════════════════════════════════════════════════════════════╝");
        return sb.ToString();
    }
}

public partial class AnalysisResult
{
    [JsonPropertyName("quality_gate")]
    public QualityGateEvaluationResult? QualityGate { get; set; }
}

/// <summary>
/// Engine for evaluating configured quality gate thresholds and baseline regressions.
/// </summary>
public interface IQualityGateEngine
{
    QualityGateEvaluationResult EvaluateGates(
        AnalysisResult result,
        BaselineSnapshot? baseline = null,
        List<QualityGateRule>? customRules = null,
        bool failFast = false);

    List<QualityGateRule> GetDefaultRules();
    List<QualityGateRule> LoadRulesFromConfig(string configPath);
    List<QualityGateRule> ParseRulesFromYaml(string yamlContent, string source = "");
}

public class QualityGateEngine : IQualityGateEngine
{
    public static readonly List<QualityGateRule> DefaultRules = new()
    {
        new QualityGateRule
        {
            MetricName = "hotspot_density",
            Scope = "global",
            MaxThreshold = 0.35,
            Message = "Hotspot density exceeds 35% — refactor high-churn files before merging"
        },
        new QualityGateRule
        {
            MetricName = "p90_lead_time",
            Scope = "global",
            MaxThreshold = 72.0,
            Message = "P90 lead time exceeds 72h — investigate process bottleneck"
        },
        new QualityGateRule
        {
            MetricName = "dx_index",
            Scope = "global",
            MinThreshold = 55.0,
            MaxRegressionAllowed = 10.0,
            Message = "DX Index dropped significantly — review sprint health card"
        }
    };

    public List<QualityGateRule> GetDefaultRules() =>
        DefaultRules.Select(r => new QualityGateRule(r.MetricName, r.Scope, r.MaxThreshold, r.MinThreshold, r.MaxRegressionAllowed, r.Message)).ToList();

    public QualityGateEvaluationResult EvaluateGates(
        AnalysisResult result,
        BaselineSnapshot? baseline = null,
        List<QualityGateRule>? customRules = null,
        bool failFast = false)
    {
        if (result == null) throw new ArgumentNullException(nameof(result));

        var rulesToEvaluate = customRules ?? GetDefaultRules();
        var violations = new List<GateViolation>();
        int rulesEvaluated = 0;

        foreach (var rule in rulesToEvaluate)
        {
            rulesEvaluated++;
            bool isPerArea = string.Equals(rule.Scope, "per_area", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(rule.Scope, "per-area", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(rule.Scope, "area", StringComparison.OrdinalIgnoreCase);

            if (isPerArea)
            {
                var areaNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (result.Areas != null)
                {
                    foreach (var a in result.Areas)
                    {
                        if (!string.IsNullOrWhiteSpace(a.Area)) areaNames.Add(a.Area);
                    }
                }
                if (result.Files != null)
                {
                    foreach (var f in result.Files)
                    {
                        if (!string.IsNullOrWhiteSpace(f.Area)) areaNames.Add(f.Area);
                    }
                }

                if (areaNames.Count == 0 && baseline?.Areas != null)
                {
                    foreach (var a in baseline.Areas.Keys)
                    {
                        if (!string.IsNullOrWhiteSpace(a)) areaNames.Add(a);
                    }
                }

                foreach (var area in areaNames.OrderBy(a => a, StringComparer.OrdinalIgnoreCase))
                {
                    double actual = GetMetricValue(result, rule.MetricName, area);
                    double? baselineVal = baseline != null ? GetMetricValueFromBaseline(baseline, rule.MetricName, area) : null;

                    EvaluateTarget(rule, actual, baselineVal, area, result, violations, failFast);
                    if (failFast && violations.Count > 0) break;
                }
            }
            else
            {
                double actual = GetMetricValue(result, rule.MetricName, null);
                double? baselineVal = baseline != null ? GetMetricValueFromBaseline(baseline, rule.MetricName, null) : null;

                EvaluateTarget(rule, actual, baselineVal, null, result, violations, failFast);
            }

            if (failFast && violations.Count > 0)
            {
                break;
            }
        }

        var evaluationResult = new QualityGateEvaluationResult
        {
            IsPassed = violations.Count == 0,
            Violations = violations,
            RulesEvaluated = rulesEvaluated,
            RulesPassed = Math.Max(0, rulesEvaluated - violations.Select(v => v.Rule.MetricName).Distinct(StringComparer.OrdinalIgnoreCase).Count()),
            EvaluatedAt = DateTimeOffset.UtcNow
        };

        result.QualityGate = evaluationResult;
        return evaluationResult;
    }

    private void EvaluateTarget(
        QualityGateRule rule,
        double actual,
        double? baselineVal,
        string? area,
        AnalysisResult result,
        List<GateViolation> violations,
        bool failFast)
    {
        string scopeLabel = !string.IsNullOrEmpty(area) ? area : "global";
        string? targetFile = ResolveTargetFile(result, rule.MetricName, area);

        // 1. Evaluate Max Threshold
        if (rule.MaxThreshold.HasValue && actual > rule.MaxThreshold.Value)
        {
            string message = !string.IsNullOrEmpty(rule.Message)
                ? FormatCustomMessage(rule.Message, rule.MetricName, area, actual, rule.MaxThreshold.Value, null)
                : $"{rule.MetricName} ({scopeLabel}) is {actual:0.##}, which exceeds maximum allowed threshold of {rule.MaxThreshold.Value:0.##}";

            violations.Add(new GateViolation
            {
                Rule = rule,
                MetricName = rule.MetricName,
                Scope = rule.Scope,
                Area = area,
                File = targetFile,
                ActualValue = actual,
                ThresholdValue = rule.MaxThreshold.Value,
                ViolationType = GateViolationType.MaxThresholdExceeded,
                Message = message
            });

            if (failFast) return;
        }

        // 2. Evaluate Min Threshold
        if (rule.MinThreshold.HasValue && actual < rule.MinThreshold.Value)
        {
            string message = !string.IsNullOrEmpty(rule.Message)
                ? FormatCustomMessage(rule.Message, rule.MetricName, area, actual, rule.MinThreshold.Value, null)
                : $"{rule.MetricName} ({scopeLabel}) is {actual:0.##}, which is below minimum required threshold of {rule.MinThreshold.Value:0.##}";

            violations.Add(new GateViolation
            {
                Rule = rule,
                MetricName = rule.MetricName,
                Scope = rule.Scope,
                Area = area,
                File = targetFile,
                ActualValue = actual,
                ThresholdValue = rule.MinThreshold.Value,
                ViolationType = GateViolationType.MinThresholdBreached,
                Message = message
            });

            if (failFast) return;
        }

        // 3. Evaluate Baseline Regression
        if (rule.MaxRegressionAllowed.HasValue && baselineVal.HasValue)
        {
            double regression;
            if (IsHigherBetterMetric(rule.MetricName, rule))
            {
                // Higher is better: dropping from baseline indicates deterioration
                regression = baselineVal.Value - actual;
            }
            else
            {
                // Lower is better: rising above baseline indicates deterioration
                regression = actual - baselineVal.Value;
            }

            if (regression > rule.MaxRegressionAllowed.Value)
            {
                string message = !string.IsNullOrEmpty(rule.Message)
                    ? FormatCustomMessage(rule.Message, rule.MetricName, area, actual, rule.MaxRegressionAllowed.Value, regression)
                    : $"{rule.MetricName} ({scopeLabel}) regressed by {regression:0.##} (from {baselineVal.Value:0.##} to {actual:0.##}), exceeding maximum allowed regression of {rule.MaxRegressionAllowed.Value:0.##}";

                violations.Add(new GateViolation
                {
                    Rule = rule,
                    MetricName = rule.MetricName,
                    Scope = rule.Scope,
                    Area = area,
                    File = targetFile,
                    ActualValue = actual,
                    BaselineValue = baselineVal.Value,
                    RegressionValue = regression,
                    ThresholdValue = rule.MaxRegressionAllowed.Value,
                    ViolationType = GateViolationType.RegressionExceeded,
                    Message = message
                });
            }
        }
    }

    public static double GetMetricValue(AnalysisResult result, string metricName, string? area = null)
    {
        if (result == null) return 0.0;
        string key = metricName.ToLowerInvariant().Trim().Replace('-', '_');

        if (!string.IsNullOrEmpty(area))
        {
            var areaMetric = result.Areas?.FirstOrDefault(a => string.Equals(a.Area, area, StringComparison.OrdinalIgnoreCase));
            var areaFiles = result.Files?.Where(f => string.Equals(f.Area, area, StringComparison.OrdinalIgnoreCase)).ToList();

            return key switch
            {
                "hotspot_density" or "hotspots_density" or "hotspot_ratio" =>
                    ComputeAreaHotspotDensity(areaMetric, areaFiles),

                "single_owner_ratio" or "silo_ratio" or "single_owner" or "ownership_silo_ratio" =>
                    ComputeAreaSingleOwnerRatio(areaMetric, areaFiles),

                "rework_rate" or "rework" =>
                    areaMetric?.ReworkRate ?? (areaFiles != null && areaFiles.Any(f => f.ReworkRate.HasValue)
                        ? areaFiles.Where(f => f.ReworkRate.HasValue).Average(f => f.ReworkRate!.Value)
                        : 0.0),

                "churn" or "total_churn" =>
                    areaMetric?.Churn ?? (areaFiles?.Sum(f => f.Churn) ?? 0),

                "touches" or "total_touches" =>
                    areaMetric?.Touches ?? (areaFiles?.Sum(f => f.Touches) ?? 0),

                "files" or "file_count" =>
                    areaMetric?.FileCount ?? (areaFiles?.Count ?? 0),

                "attention_score" or "attention" =>
                    areaMetric?.AttentionScore ?? (areaFiles != null && areaFiles.Count > 0 ? areaFiles.Average(f => f.AttentionScore) : 0.0),

                "heat_score" or "heat" =>
                    areaMetric?.HeatScore ?? (areaFiles != null && areaFiles.Count > 0 ? areaFiles.Average(f => f.HeatScore) : 0.0),

                _ => areaMetric != null ? 0.0 : 0.0
            };
        }

        return key switch
        {
            "hotspot_density" or "hotspots_density" or "hotspot_ratio" =>
                ComputeGlobalHotspotDensity(result),

            "single_owner_ratio" or "silo_ratio" or "single_owner" or "ownership_silo_ratio" =>
                ComputeGlobalSingleOwnerRatio(result),

            "p90_lead_time" or "p90_lead_time_hours" or "lead_time_p90" =>
                ComputeP90LeadTime(result.LeadTimes),

            "p50_lead_time" or "p50_lead_time_hours" or "median_lead_time" or "lead_time_p50" =>
                ComputeP50LeadTime(result.LeadTimes),

            "cross_boundary_coupling_rate" or "cross_boundary_rate" or "cross_boundary_coupling" =>
                ComputeCrossBoundaryCouplingRate(result),

            "dx_index" or "dxindex" or "dx" =>
                result.DxIndex?.CompositeScore ?? DxIndexCalculator.Compute(result).CompositeScore,

            "rework_rate" or "rework" =>
                ComputeGlobalReworkRate(result),

            "zombie_files" or "zombie_file_count" =>
                result.CuratedReports?.CodeRot?.ZombieFileCount ?? 0,

            "zombie_lines" =>
                result.CuratedReports?.CodeRot?.ZombieLines ?? 0,

            "zombie_ratio" or "zombie_file_ratio" =>
                result.Files != null && result.Files.Count > 0
                    ? (double)(result.CuratedReports?.CodeRot?.ZombieFileCount ?? 0) / result.Files.Count
                    : 0.0,

            "commits" or "commit_count" =>
                result.Analysis?.CommitCount ?? 0,

            "files" or "file_count" =>
                result.Files?.Count ?? 0,

            "churn" or "total_churn" =>
                result.Files?.Sum(f => f.Churn) ?? 0,

            "touches" or "total_touches" =>
                result.Files?.Sum(f => f.Touches) ?? 0,

            "lead_time" or "average_lead_time_hours" =>
                result.LeadTimes?.AverageLeadTimeHours ?? 0.0,

            "heat_score" or "heat" =>
                result.Files != null && result.Files.Count > 0 ? result.Files.Average(f => f.HeatScore) : 0.0,

            "attention_score" or "attention" =>
                result.Files != null && result.Files.Count > 0 ? result.Files.Average(f => f.AttentionScore) : 0.0,

            "reviewer_silos" =>
                result.CuratedReports?.ReviewCollaboration?.ReviewerSilos ?? 0,

            "high_volume_commits" =>
                result.CuratedReports?.AiCodeStrain?.HighVolumeCommits ?? 0,

            "features" =>
                result.CuratedReports?.WorkClassification?.Features ?? 0,

            "bugs" =>
                result.CuratedReports?.WorkClassification?.Bugs ?? 0,

            "technical_debt" or "tech_debt" =>
                result.CuratedReports?.WorkClassification?.TechnicalDebt ?? 0,

            "chores" =>
                result.CuratedReports?.WorkClassification?.Chores ?? 0,

            _ => 0.0
        };
    }

    public static double? GetMetricValueFromBaseline(BaselineSnapshot baseline, string metricName, string? area = null)
    {
        if (baseline == null) return null;
        string key = metricName.ToLowerInvariant().Trim().Replace('-', '_');

        if (!string.IsNullOrEmpty(area))
        {
            if (baseline.Areas.TryGetValue(area, out var areaMetrics))
            {
                if (areaMetrics.Metrics != null && areaMetrics.Metrics.TryGetValue(key, out double directAreaMetric))
                {
                    return directAreaMetric;
                }

                return key switch
                {
                    "hotspot_density" or "hotspots_density" or "hotspot_ratio" =>
                        areaMetrics.Metrics != null && areaMetrics.Metrics.TryGetValue("hotspot_density", out var hd) ? hd : 0.0,

                    "single_owner_ratio" or "silo_ratio" or "single_owner" =>
                        areaMetrics.Metrics != null && areaMetrics.Metrics.TryGetValue("single_owner_ratio", out var so) ? so : 0.0,

                    "rework_rate" or "rework" =>
                        areaMetrics.ReworkRate ?? (areaMetrics.Metrics != null && areaMetrics.Metrics.TryGetValue("rework_rate", out var rw) ? rw : 0.0),

                    "churn" or "total_churn" => areaMetrics.Churn,
                    "touches" or "total_touches" => areaMetrics.Touches,
                    "files" or "file_count" => areaMetrics.FileCount,
                    "attention_score" or "attention" => areaMetrics.AttentionScore,
                    "heat_score" or "heat" => areaMetrics.HeatScore,
                    _ => areaMetrics.Metrics != null && areaMetrics.Metrics.TryGetValue(key, out var customVal) ? customVal : (double?)null
                };
            }
            return null;
        }

        // Global metric check
        if (baseline.Metrics != null)
        {
            if (baseline.Metrics.TryGetValue(key, out double val)) return val;
            if (baseline.Metrics.TryGetValue(metricName, out double rawVal)) return rawVal;

            if (key is "dx_index" or "dxindex" or "dx" && baseline.Metrics.TryGetValue("dx_index", out double dx))
            {
                return dx;
            }
            if (key is "p90_lead_time" or "p90_lead_time_hours" or "lead_time_p90" && baseline.Metrics.TryGetValue("p90_lead_time", out double p90))
            {
                return p90;
            }
            if (key is "hotspot_density" or "hotspots_density" && baseline.Metrics.TryGetValue("hotspot_density", out double hd))
            {
                return hd;
            }
        }

        return key switch
        {
            "commits" or "commit_count" => baseline.Summary.CommitCount,
            "files" or "file_count" => baseline.Summary.FileCount,
            "churn" or "total_churn" => baseline.Summary.TotalChurn,
            "touches" or "total_touches" => baseline.Summary.TotalTouches,
            "lead_time" or "average_lead_time_hours" => baseline.Summary.AverageLeadTimeHours,
            "zombie_files" => baseline.Summary.ZombieFiles,
            "zombie_lines" => baseline.Summary.ZombieLines,
            "features" => baseline.Summary.Features,
            "bugs" => baseline.Summary.Bugs,
            "technical_debt" or "tech_debt" => baseline.Summary.TechnicalDebt,
            "chores" => baseline.Summary.Chores,
            "reviewer_silos" => baseline.Summary.ReviewerSilos,
            "high_volume_commits" => baseline.Summary.HighVolumeCommits,
            "rework_rate" or "rework" when baseline.Areas.Count > 0 =>
                baseline.Areas.Values.Where(a => a.ReworkRate.HasValue).DefaultIfEmpty().Average(a => a?.ReworkRate ?? 0.0),
            "attention_score" or "attention" when baseline.Areas.Count > 0 =>
                baseline.Areas.Values.Average(a => a.AttentionScore),
            "heat_score" or "heat" when baseline.Areas.Count > 0 =>
                baseline.Areas.Values.Average(a => a.HeatScore),
            _ => null
        };
    }

    public List<QualityGateRule> LoadRulesFromConfig(string configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath)) throw new ArgumentException("Config path cannot be null or empty.", nameof(configPath));
        if (!File.Exists(configPath)) throw new FileNotFoundException($"Quality gate configuration file not found at '{configPath}'.", configPath);

        string content = File.ReadAllText(configPath);
        if (configPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("gates", out var gatesElem) && gatesElem.ValueKind == JsonValueKind.Array)
                {
                    var list = JsonSerializer.Deserialize<List<QualityGateRule>>(gatesElem.GetRawText(), JsonSerializationDefaults.Indented);
                    if (list != null) return list;
                }
                var directList = JsonSerializer.Deserialize<List<QualityGateRule>>(content, JsonSerializationDefaults.Indented);
                if (directList != null) return directList;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to parse quality gate rules from JSON config at '{configPath}': {ex.Message}", ex);
            }
        }

        return ParseRulesFromYaml(content, configPath);
    }

    public List<QualityGateRule> ParseRulesFromYaml(string yamlContent, string source = "")
    {
        if (string.IsNullOrWhiteSpace(yamlContent)) return new List<QualityGateRule>();

        var parsed = YamlSubsetParserHelper.ParseYamlSubset(yamlContent, source);
        var rules = new List<QualityGateRule>();

        List<object?>? gateEntries = null;
        if (parsed is Dictionary<string, object?> dict)
        {
            if (dict.TryGetValue("gates", out var gObj) && gObj is List<object?> listG)
            {
                gateEntries = listG;
            }
            else if (dict.TryGetValue("quality_gates", out var qgObj) && qgObj is List<object?> listQg)
            {
                gateEntries = listQg;
            }
        }
        else if (parsed is List<object?> rootList)
        {
            gateEntries = rootList;
        }

        if (gateEntries == null) return rules;

        foreach (var entry in gateEntries)
        {
            if (entry is Dictionary<string, object?> ruleMap)
            {
                var rule = new QualityGateRule();

                if (ruleMap.TryGetValue("metric", out var mVal) && mVal != null)
                {
                    rule.MetricName = mVal.ToString()!;
                }
                else if (ruleMap.TryGetValue("metric_name", out var mnVal) && mnVal != null)
                {
                    rule.MetricName = mnVal.ToString()!;
                }

                if (ruleMap.TryGetValue("scope", out var sVal) && sVal != null)
                {
                    rule.Scope = sVal.ToString()!;
                }

                if (ruleMap.TryGetValue("max", out var maxVal) && maxVal != null)
                {
                    rule.MaxThreshold = ConfigUtils.ConvertToDouble(maxVal);
                }
                else if (ruleMap.TryGetValue("max_threshold", out var maxTVal) && maxTVal != null)
                {
                    rule.MaxThreshold = ConfigUtils.ConvertToDouble(maxTVal);
                }

                if (ruleMap.TryGetValue("min", out var minVal) && minVal != null)
                {
                    rule.MinThreshold = ConfigUtils.ConvertToDouble(minVal);
                }
                else if (ruleMap.TryGetValue("min_threshold", out var minTVal) && minTVal != null)
                {
                    rule.MinThreshold = ConfigUtils.ConvertToDouble(minTVal);
                }

                if (ruleMap.TryGetValue("regression_max", out var regVal) && regVal != null)
                {
                    rule.MaxRegressionAllowed = ConfigUtils.ConvertToDouble(regVal);
                }
                else if (ruleMap.TryGetValue("max_regression", out var maxRVal) && maxRVal != null)
                {
                    rule.MaxRegressionAllowed = ConfigUtils.ConvertToDouble(maxRVal);
                }
                else if (ruleMap.TryGetValue("max_regression_allowed", out var maxRaVal) && maxRaVal != null)
                {
                    rule.MaxRegressionAllowed = ConfigUtils.ConvertToDouble(maxRaVal);
                }

                if (ruleMap.TryGetValue("message", out var msgVal) && msgVal != null)
                {
                    rule.Message = msgVal.ToString()!;
                }

                rules.Add(rule);
            }
        }

        return rules;
    }

    private static bool IsHigherBetterMetric(string metricName, QualityGateRule rule)
    {
        string lower = metricName.ToLowerInvariant();
        if (lower.Contains("dx_index") || lower.Contains("dxindex") || lower.Contains("health_score") || lower.Contains("freshness"))
        {
            return true;
        }
        if (rule.MinThreshold.HasValue && !rule.MaxThreshold.HasValue)
        {
            return true;
        }
        return false;
    }

    private static string FormatCustomMessage(
        string template,
        string metric,
        string? area,
        double actual,
        double threshold,
        double? regression)
    {
        string msg = template;
        if (!string.IsNullOrEmpty(area))
        {
            msg = msg.Replace("{area}", area);
        }
        msg = msg.Replace("{metric}", metric);
        msg = msg.Replace("{actual}", actual.ToString("0.##", CultureInfo.InvariantCulture));
        msg = msg.Replace("{threshold}", threshold.ToString("0.##", CultureInfo.InvariantCulture));
        msg = msg.Replace("{max}", threshold.ToString("0.##", CultureInfo.InvariantCulture));
        msg = msg.Replace("{min}", threshold.ToString("0.##", CultureInfo.InvariantCulture));
        if (regression.HasValue)
        {
            msg = msg.Replace("{regression}", regression.Value.ToString("0.##", CultureInfo.InvariantCulture));
        }
        return msg;
    }

    private static double ComputeGlobalHotspotDensity(AnalysisResult result)
    {
        if (result.Files != null && result.Files.Count > 0)
        {
            int hotspots = result.Files.Count(f => f.AttentionScore >= 60.0 || (f.AttentionScore == 0 && f.HeatScore >= 60.0));
            return (double)hotspots / result.Files.Count;
        }
        if (result.Areas != null && result.Areas.Count > 0)
        {
            int hotspotAreas = result.Areas.Count(a => a.AttentionScore >= 60.0 || (a.AttentionScore == 0 && a.HeatScore >= 60.0));
            return (double)hotspotAreas / result.Areas.Count;
        }
        return 0.0;
    }

    private static double ComputeAreaHotspotDensity(AreaMetric? areaMetric, List<FileMetric>? areaFiles)
    {
        if (areaFiles != null && areaFiles.Count > 0)
        {
            int hotspots = areaFiles.Count(f => f.AttentionScore >= 60.0 || (f.AttentionScore == 0 && f.HeatScore >= 60.0));
            return (double)hotspots / areaFiles.Count;
        }
        if (areaMetric != null)
        {
            return areaMetric.AttentionScore >= 60.0 || areaMetric.HeatScore >= 60.0 ? 1.0 : 0.0;
        }
        return 0.0;
    }

    private static double ComputeGlobalSingleOwnerRatio(AnalysisResult result)
    {
        if (result.Files != null && result.Files.Count > 0)
        {
            int singleOwnerCount = result.Files.Count(f =>
                f.KnowledgeSilo?.IsSilo == true ||
                (f.KnowledgeSilo != null && f.KnowledgeSilo.TopOwnerShare > 0.50) ||
                (f.KnowledgeSilo != null && f.KnowledgeSilo.TruckFactor == 1) ||
                f.ContributorCount == 1 ||
                (f.Contributors != null && f.Contributors.Count == 1) ||
                (f.Contributors != null && f.Contributors.Any(c => c.ActivityShare > 0.50)));

            return (double)singleOwnerCount / result.Files.Count;
        }

        if (result.Areas != null && result.Areas.Count > 0)
        {
            int siloAreas = result.Areas.Count(a =>
                a.ContributorCount == 1 ||
                (a.Contributors != null && a.Contributors.Count == 1) ||
                (a.Contributors != null && a.Contributors.Any(c => c.ActivityShare > 0.50)));

            return (double)siloAreas / result.Areas.Count;
        }

        return 0.0;
    }

    private static double ComputeAreaSingleOwnerRatio(AreaMetric? areaMetric, List<FileMetric>? areaFiles)
    {
        if (areaFiles != null && areaFiles.Count > 0)
        {
            int singleOwnerCount = areaFiles.Count(f =>
                f.KnowledgeSilo?.IsSilo == true ||
                (f.KnowledgeSilo != null && f.KnowledgeSilo.TopOwnerShare > 0.50) ||
                (f.KnowledgeSilo != null && f.KnowledgeSilo.TruckFactor == 1) ||
                f.ContributorCount == 1 ||
                (f.Contributors != null && f.Contributors.Count == 1) ||
                (f.Contributors != null && f.Contributors.Any(c => c.ActivityShare > 0.50)));

            return (double)singleOwnerCount / areaFiles.Count;
        }

        if (areaMetric != null)
        {
            if (areaMetric.ContributorCount == 1 || (areaMetric.Contributors != null && areaMetric.Contributors.Count == 1))
            {
                return 1.0;
            }
            if (areaMetric.Contributors != null && areaMetric.Contributors.Count > 0)
            {
                return areaMetric.Contributors.Max(c => c.ActivityShare);
            }
        }

        return 0.0;
    }

    private static double ComputeP90LeadTime(LeadTimesInfo? leadTimes)
    {
        if (leadTimes?.Merges != null && leadTimes.Merges.Count > 0)
        {
            var times = leadTimes.Merges.Select(m => m.LeadTimeHours).ToList();
            return PercentileCalculator.CalculatePercentile(times, 90.0);
        }
        if (leadTimes != null && leadTimes.AverageLeadTimeHours > 0)
        {
            return leadTimes.AverageLeadTimeHours * 1.5;
        }
        return 0.0;
    }

    private static double ComputeP50LeadTime(LeadTimesInfo? leadTimes)
    {
        if (leadTimes?.Merges != null && leadTimes.Merges.Count > 0)
        {
            var times = leadTimes.Merges.Select(m => m.LeadTimeHours).ToList();
            return PercentileCalculator.CalculatePercentile(times, 50.0);
        }
        if (leadTimes != null && leadTimes.AverageLeadTimeHours > 0)
        {
            return leadTimes.AverageLeadTimeHours;
        }
        return 0.0;
    }

    private static double ComputeCrossBoundaryCouplingRate(AnalysisResult result)
    {
        var couplings = result.TemporalCoupling;
        if (couplings == null || couplings.Count == 0) return 0.0;

        var fileAreaMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (result.Files != null)
        {
            foreach (var f in result.Files)
            {
                if (!string.IsNullOrEmpty(f.Path) && !string.IsNullOrEmpty(f.Area))
                {
                    fileAreaMap[f.Path] = f.Area;
                }
            }
        }

        int crossBoundaryCount = 0;
        foreach (var c in couplings)
        {
            string boundaryA = GetBoundary(c.FileA, fileAreaMap);
            string boundaryB = GetBoundary(c.FileB, fileAreaMap);
            if (!string.Equals(boundaryA, boundaryB, StringComparison.OrdinalIgnoreCase))
            {
                crossBoundaryCount++;
            }
        }

        return (double)crossBoundaryCount / couplings.Count;
    }

    private static string GetBoundary(string filePath, Dictionary<string, string> fileAreaMap)
    {
        if (fileAreaMap.TryGetValue(filePath, out var area) && !string.IsNullOrWhiteSpace(area))
        {
            return area;
        }
        string normalized = filePath.Replace('\\', '/').TrimStart('/');
        int slash = normalized.IndexOf('/');
        return slash > 0 ? normalized.Substring(0, slash) : "root";
    }

    private static double ComputeGlobalReworkRate(AnalysisResult result)
    {
        var files = result.Files?.Where(f => f.ReworkRate.HasValue).ToList();
        if (files != null && files.Count > 0)
        {
            return files.Average(f => f.ReworkRate!.Value);
        }
        var areas = result.Areas?.Where(a => a.ReworkRate.HasValue).ToList();
        if (areas != null && areas.Count > 0)
        {
            return areas.Average(a => a.ReworkRate!.Value);
        }
        return 0.0;
    }

    private static string? ResolveTargetFile(AnalysisResult result, string metricName, string? area)
    {
        if (!string.IsNullOrEmpty(area) && result.Files != null)
        {
            var areaFiles = result.Files.Where(f => string.Equals(f.Area, area, StringComparison.OrdinalIgnoreCase)).ToList();
            var top = areaFiles.OrderByDescending(f => f.AttentionScore).ThenByDescending(f => f.Touches).FirstOrDefault();
            if (top != null) return top.Path;
        }

        if (string.Equals(metricName, "hotspot_density", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(metricName, "hotspots_density", StringComparison.OrdinalIgnoreCase))
        {
            var top = result.Files?.OrderByDescending(f => f.AttentionScore).FirstOrDefault();
            if (top != null) return top.Path;
        }

        if (string.Equals(metricName, "cross_boundary_coupling_rate", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(metricName, "cross_boundary_rate", StringComparison.OrdinalIgnoreCase))
        {
            var top = result.TemporalCoupling?.OrderByDescending(c => c.CouplingDegree).FirstOrDefault();
            if (top != null) return top.FileA;
        }

        return result.Files?.FirstOrDefault()?.Path;
    }
}
