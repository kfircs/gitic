using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;

namespace Gitic;

/// <summary>
/// Represents a single temporal coupling violation of an architectural boundary rule.
/// </summary>
public class BoundaryViolation
{
    [JsonPropertyName("rule_name")]
    public string RuleName { get; set; } = string.Empty;

    [JsonPropertyName("fileA")]
    public string FileA { get; set; } = string.Empty;

    [JsonPropertyName("fileB")]
    public string FileB { get; set; } = string.Empty;

    [JsonPropertyName("coupling_degree")]
    public double CouplingDegree { get; set; }

    [JsonPropertyName("shared_commits")]
    public int SharedCommits { get; set; }

    [JsonPropertyName("violation_message")]
    public string ViolationMessage { get; set; } = string.Empty;

    [JsonPropertyName("source_file")]
    public string SourceFile => FileA;

    [JsonPropertyName("target_file")]
    public string TargetFile => FileB;

    public override string ToString() => ViolationMessage;
}

/// <summary>
/// Result of evaluating an individual architectural boundary rule.
/// </summary>
public class BoundaryRuleResult
{
    [JsonPropertyName("rule_name")]
    public string RuleName { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("passed")]
    public bool Passed => Violations.Count == 0;

    [JsonPropertyName("violations")]
    public List<BoundaryViolation> Violations { get; set; } = new();
}

/// <summary>
/// Comprehensive report detailing architectural boundary enforcement results and any detected violations.
/// </summary>
public class BoundaryEnforcementReport
{
    [JsonPropertyName("passed")]
    public bool Passed { get; set; } = true;

    [JsonPropertyName("violations")]
    public List<BoundaryViolation> Violations { get; set; } = new();

    [JsonPropertyName("total_rules_checked")]
    public int TotalRulesChecked { get; set; }

    [JsonPropertyName("rule_results")]
    public List<BoundaryRuleResult> RuleResults { get; set; } = new();

    [JsonPropertyName("rules_passed")]
    public int RulesPassed => RuleResults.Count > 0
        ? RuleResults.Count(r => r.Passed)
        : Math.Max(0, TotalRulesChecked - Violations.Select(v => v.RuleName).Distinct(StringComparer.OrdinalIgnoreCase).Count());

    [JsonPropertyName("rules_failed")]
    public int RulesFailed => RuleResults.Count > 0
        ? RuleResults.Count(r => !r.Passed)
        : Violations.Select(v => v.RuleName).Distinct(StringComparer.OrdinalIgnoreCase).Count();

    /// <summary>
    /// Formats this report into a human-readable diagnostic summary.
    /// </summary>
    public string FormatSummary() => BoundaryEnforcer.Format(this);
}

public partial class AnalysisResult
{
    [JsonPropertyName("boundary_enforcement")]
    public BoundaryEnforcementReport? BoundaryEnforcement { get; set; }
}

/// <summary>
/// Defines the contract for evaluating architectural boundary constraints against repository metrics.
/// </summary>
public interface IBoundaryEnforcer
{
    BoundaryEnforcementReport EnforceBoundaries(List<TemporalCoupling>? couplings, List<BoundaryRule>? rules);
    string FormatSummary(BoundaryEnforcementReport report);
}

/// <summary>
/// Engine for detecting architectural boundary leaks where temporal coupling violates user-declared boundary rules.
/// </summary>
public class BoundaryEnforcer : IBoundaryEnforcer
{
    /// <summary>
    /// Evaluates temporal coupling pairs against declared boundary rules.
    /// </summary>
    public BoundaryEnforcementReport EnforceBoundaries(List<TemporalCoupling>? couplings, List<BoundaryRule>? rules)
    {
        return Enforce(couplings, rules);
    }

    /// <summary>
    /// Evaluates boundaries using an AnalysisResult and GiticConfig.
    /// </summary>
    public BoundaryEnforcementReport EnforceBoundaries(AnalysisResult result, GiticConfig config)
    {
        if (result == null) throw new ArgumentNullException(nameof(result));
        if (config == null) throw new ArgumentNullException(nameof(config));

        var report = Enforce(result.TemporalCoupling, config.Boundaries);
        result.BoundaryEnforcement = report;
        return report;
    }

    /// <summary>
    /// Evaluates boundaries using an AnalysisResult and an optional list of custom boundary rules.
    /// </summary>
    public BoundaryEnforcementReport EnforceBoundaries(AnalysisResult result, List<BoundaryRule>? rules = null)
    {
        if (result == null) throw new ArgumentNullException(nameof(result));

        var rulesToUse = rules ?? new List<BoundaryRule>();
        var report = Enforce(result.TemporalCoupling, rulesToUse);
        result.BoundaryEnforcement = report;
        return report;
    }

    /// <summary>
    /// Formats a boundary enforcement report into a diagnostic text summary.
    /// </summary>
    public string FormatSummary(BoundaryEnforcementReport report)
    {
        return Format(report);
    }

    /// <summary>
    /// Static convenience method to evaluate temporal coupling pairs against boundary rules.
    /// </summary>
    public static BoundaryEnforcementReport Enforce(List<TemporalCoupling>? couplings, List<BoundaryRule>? rules)
    {
        var report = new BoundaryEnforcementReport();

        if (rules == null || rules.Count == 0)
        {
            report.Passed = true;
            report.TotalRulesChecked = 0;
            return report;
        }

        var activeCouplings = couplings ?? new List<TemporalCoupling>();
        var allViolations = new List<BoundaryViolation>();
        var ruleResults = new List<BoundaryRuleResult>();

        foreach (var rule in rules)
        {
            if (rule == null || string.IsNullOrWhiteSpace(rule.Source))
            {
                continue;
            }

            var ruleResult = new BoundaryRuleResult
            {
                RuleName = rule.Name,
                Source = rule.Source
            };

            double threshold = rule.Threshold > 0 ? rule.Threshold : 0.2;

            foreach (var coupling in activeCouplings)
            {
                if (coupling == null) continue;

                // Threshold check: coupling degree must be at least the configured threshold
                if (coupling.CouplingDegree < threshold - 1e-9)
                {
                    continue;
                }

                string fileA = coupling.FileA ?? string.Empty;
                string fileB = coupling.FileB ?? string.Empty;

                if (string.IsNullOrEmpty(fileA) || string.IsNullOrEmpty(fileB))
                {
                    continue;
                }

                bool aMatchesSource = PathUtils.MatchesPathPattern(fileA, rule.Source);
                bool bMatchesSource = PathUtils.MatchesPathPattern(fileB, rule.Source);

                if (!aMatchesSource && !bMatchesSource)
                {
                    continue;
                }

                bool violationDetected = false;

                // Direction 1: FileA is source, FileB is target
                if (aMatchesSource)
                {
                    if (IsTargetViolating(fileB, bMatchesSource, rule))
                    {
                        var violation = CreateViolation(rule, fileA, fileB, coupling.CouplingDegree, coupling.SharedCommits);
                        ruleResult.Violations.Add(violation);
                        allViolations.Add(violation);
                        violationDetected = true;
                    }
                }

                // Direction 2: FileB is source, FileA is target (only if not already flagged in direction 1)
                if (!violationDetected && bMatchesSource)
                {
                    if (IsTargetViolating(fileA, aMatchesSource, rule))
                    {
                        var violation = CreateViolation(rule, fileB, fileA, coupling.CouplingDegree, coupling.SharedCommits);
                        ruleResult.Violations.Add(violation);
                        allViolations.Add(violation);
                    }
                }
            }

            ruleResults.Add(ruleResult);
        }

        report.TotalRulesChecked = rules.Count;
        report.Violations = allViolations;
        report.RuleResults = ruleResults;
        report.Passed = allViolations.Count == 0;

        return report;
    }

    private static bool IsTargetViolating(string targetFile, bool targetMatchesSource, BoundaryRule rule)
    {
        // 1. Explicitly allowed coupling takes precedence
        if (rule.AllowedCoupling != null && rule.AllowedCoupling.Count > 0)
        {
            if (rule.AllowedCoupling.Any(pattern => !string.IsNullOrWhiteSpace(pattern) && PathUtils.MatchesPathPattern(targetFile, pattern)))
            {
                return false;
            }
        }

        // 2. Forbidden coupling globs
        if (rule.ForbiddenCoupling != null && rule.ForbiddenCoupling.Count > 0)
        {
            if (rule.ForbiddenCoupling.Any(pattern => !string.IsNullOrWhiteSpace(pattern) && PathUtils.MatchesPathPattern(targetFile, pattern)))
            {
                return true;
            }
        }
        else if (rule.AllowedCoupling != null && rule.AllowedCoupling.Count > 0)
        {
            // If only allowed_coupling is configured, any target outside the source area that is not allowed is forbidden
            if (!targetMatchesSource)
            {
                return true;
            }
        }

        return false;
    }

    private static BoundaryViolation CreateViolation(
        BoundaryRule rule,
        string sourceFile,
        string targetFile,
        double couplingDegree,
        int sharedCommits)
    {
        string ruleTitle = !string.IsNullOrWhiteSpace(rule.Name) ? rule.Name : $"Boundary({rule.Source})";
        string msg = $"Temporal coupling between '{sourceFile}' and '{targetFile}' (degree: {couplingDegree:0.##}, shared commits: {sharedCommits}) violates boundary rule '{ruleTitle}'.";

        return new BoundaryViolation
        {
            RuleName = ruleTitle,
            FileA = sourceFile,
            FileB = targetFile,
            CouplingDegree = couplingDegree,
            SharedCommits = sharedCommits,
            ViolationMessage = msg
        };
    }

    /// <summary>
    /// Static convenience method to format a boundary enforcement report into a diagnostic summary.
    /// </summary>
    public static string Format(BoundaryEnforcementReport report)
    {
        if (report == null) return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("🏛️ Architectural Boundary Report");
        sb.AppendLine("═══════════════════════════════════════════════");
        sb.AppendLine();

        if (report.RuleResults != null && report.RuleResults.Count > 0)
        {
            foreach (var ruleResult in report.RuleResults)
            {
                string ruleName = !string.IsNullOrEmpty(ruleResult.RuleName) ? ruleResult.RuleName : "Boundary Rule";

                if (ruleResult.Passed)
                {
                    sb.AppendLine($"✅ PASS  \"{ruleName}\"");
                    sb.AppendLine("         0 coupling pairs found crossing this boundary.");
                    sb.AppendLine();
                }
                else
                {
                    var sortedViolations = ruleResult.Violations
                        .OrderByDescending(v => v.CouplingDegree)
                        .ToList();

                    sb.AppendLine($"❌ FAIL  \"{ruleName}\"");
                    sb.AppendLine($"         {sortedViolations.Count} coupling pair(s) found violating boundary:");

                    foreach (var v in sortedViolations)
                    {
                        sb.AppendLine($"           {v.FileA} ↔ {v.FileB}  (degree: {v.CouplingDegree:0.##}, shared commits: {v.SharedCommits})");
                    }

                    if (sortedViolations.Count > 0)
                    {
                        var worst = sortedViolations[0];
                        sb.AppendLine($"         Worst offender: {worst.FileA} ↔ {worst.FileB} (degree: {worst.CouplingDegree:0.##})");
                    }
                    sb.AppendLine();
                }
            }
        }
        else if (report.Violations.Count == 0)
        {
            sb.AppendLine("✅ PASS  All architectural boundary rules passed.");
            sb.AppendLine($"         0 coupling pairs found crossing boundaries ({report.TotalRulesChecked} rule(s) checked).");
            sb.AppendLine();
        }
        else
        {
            var grouped = report.Violations
                .GroupBy(v => v.RuleName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var group in grouped)
            {
                string ruleName = string.IsNullOrEmpty(group.Key) ? "Boundary Rule" : group.Key;
                var sorted = group.OrderByDescending(v => v.CouplingDegree).ToList();

                sb.AppendLine($"❌ FAIL  \"{ruleName}\"");
                sb.AppendLine($"         {sorted.Count} coupling pair(s) found violating boundary:");

                foreach (var v in sorted)
                {
                    sb.AppendLine($"           {v.FileA} ↔ {v.FileB}  (degree: {v.CouplingDegree:0.##}, shared commits: {v.SharedCommits})");
                }

                if (sorted.Count > 0)
                {
                    var worst = sorted[0];
                    sb.AppendLine($"         Worst offender: {worst.FileA} ↔ {worst.FileB} (degree: {worst.CouplingDegree:0.##})");
                }
                sb.AppendLine();
            }
        }

        return sb.ToString().TrimEnd() + Environment.NewLine;
    }
}
