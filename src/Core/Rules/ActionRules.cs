using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Gitic;

/// <summary>
/// Context passed into action recommendation rules for evaluation.
/// </summary>
public class ActionEvaluationContext
{
    public AnalysisResult Result { get; set; } = new();
    public BaselineSnapshot? Baseline { get; set; }
}

/// <summary>
/// Interface for proactive action recommendation detection rules.
/// </summary>
public interface IActionRule
{
    string RuleId { get; }
    List<ActionRecommendation> Evaluate(ActionEvaluationContext context);
}

/// <summary>
/// Provider interface for discovering and supplying action recommendation rules.
/// </summary>
public interface IActionRuleProvider
{
    List<IActionRule> GetRules();
}

/// <summary>
/// Default action rule provider containing all core proactive heuristics.
/// </summary>
public class DefaultActionRuleProvider : IActionRuleProvider
{
    public List<IActionRule> GetRules() => new()
    {
        new RefactorGodFileRule(),
        new DistributeSiloRule(),
        new CouplingHubRule(),
        new ReviewBottleneckRule(),
        new AcceleratingRotRule()
    };
}

/// <summary>
/// Detects oversized, heavily modified files acting as change multipliers (god files).
/// Trigger: lines > 800 AND touches > 20 AND coupling degree > 3.
/// </summary>
public class RefactorGodFileRule : IActionRule
{
    public string RuleId => "refactor_god_file";

    public List<ActionRecommendation> Evaluate(ActionEvaluationContext context)
    {
        var actions = new List<ActionRecommendation>();
        if (context.Result.Files == null || context.Result.Files.Count == 0)
        {
            return actions;
        }

        foreach (var file in context.Result.Files)
        {
            int lines = file.Lines ?? file.LinesOfCode ?? 0;
            int touches = file.Touches;

            var couplings = context.Result.TemporalCoupling?
                .Where(tc => ActionRuleUtils.IsMatchingFile(tc.FileA, file.Path) || ActionRuleUtils.IsMatchingFile(tc.FileB, file.Path))
                .ToList() ?? new List<TemporalCoupling>();

            int couplingDegree = couplings.Count;
            double sumCoupling = couplings.Sum(c => c.CouplingDegree > 1.0 ? c.CouplingDegree / 100.0 : c.CouplingDegree);

            if (lines > 800 && touches > 20 && (couplingDegree > 3 || sumCoupling > 3.0))
            {
                string fileName = Path.GetFileName(file.Path);
                string suggestedAssignee = ResolvePrimaryMaintainer(file, context.Result);

                actions.Add(new ActionRecommendation
                {
                    Priority = 1,
                    Category = ActionCategories.CodeHealth,
                    Title = $"Split {fileName} — change multiplier affecting {couplingDegree} other files",
                    Narrative = $"{file.Path} contains {lines:N0} lines of code and has been modified {touches} times in the analysis window. It is temporally coupled to {couplingDegree} other files. Because changes to this file frequently trigger cascading modifications across dependent modules, refactoring and decomposing it into smaller, single-responsibility units will reduce merge friction and blast radius.",
                    EstimatedImpact = $"Reduces change amplification and merge conflict risk across {couplingDegree} dependent files",
                    SuggestedAssignee = suggestedAssignee,
                    Evidence = new List<object> { file, couplings },
                    DetectionRules = new List<string> { RuleId }
                });
            }
        }

        return actions;
    }

    private static string ResolvePrimaryMaintainer(FileMetric file, AnalysisResult result)
    {
        if (file.Contributors != null && file.Contributors.Count > 0)
        {
            var top = file.Contributors
                .OrderByDescending(c => c.ActivityShare)
                .ThenByDescending(c => c.Activity)
                .FirstOrDefault();

            if (top != null && !string.IsNullOrWhiteSpace(top.Name))
            {
                int sharePct = (int)Math.Round(top.ActivityShare * 100);
                return $"{top.Name} ({sharePct}% familiarity, primary author)";
            }
        }

        if (result.Contributors != null && result.Contributors.Count > 0)
        {
            return $"{result.Contributors[0].Name} (lead maintainer)";
        }

        return "Lead Maintainer (schedule architectural refactoring)";
    }
}

/// <summary>
/// Detects critical hotspot files dominated by a single developer (truck factor = 1).
/// Trigger: Top owner share > 0.75 in top-20 hotspots; recommends a secondary developer.
/// </summary>
public class DistributeSiloRule : IActionRule
{
    public string RuleId => "distribute_silo";

    public List<ActionRecommendation> Evaluate(ActionEvaluationContext context)
    {
        var actions = new List<ActionRecommendation>();
        if (context.Result.Files == null || context.Result.Files.Count == 0)
        {
            return actions;
        }

        // Evaluate top 20 hotspot files ranked by AttentionScore descending, then HeatScore descending
        var topHotspots = context.Result.Files
            .OrderByDescending(f => f.AttentionScore)
            .ThenByDescending(f => f.HeatScore)
            .Take(20)
            .ToList();

        foreach (var file in topHotspots)
        {
            double topShare = 0.0;
            string topOwner = string.Empty;

            if (file.KnowledgeSilo != null && file.KnowledgeSilo.TopOwnerShare > 0)
            {
                topShare = file.KnowledgeSilo.TopOwnerShare;
            }

            if (file.Contributors != null && file.Contributors.Count > 0)
            {
                var topContr = file.Contributors
                    .OrderByDescending(c => c.ActivityShare)
                    .ThenByDescending(c => c.Activity)
                    .FirstOrDefault();

                if (topContr != null)
                {
                    if (topContr.ActivityShare > topShare || topShare <= 0)
                    {
                        topShare = topContr.ActivityShare;
                    }
                    topOwner = topContr.Name;
                }
            }

            if (topShare > 0.75)
            {
                string fileName = Path.GetFileName(file.Path);
                string suggestedAssignee = ResolveSecondaryAssignee(file, topOwner, context.Result);
                int sharePct = (int)Math.Round(topShare * 100);
                string ownerDisplay = string.IsNullOrWhiteSpace(topOwner) ? "a single developer" : topOwner;

                string reworkClause = file.ReworkRate.HasValue && file.ReworkRate.Value > 0.15
                    ? $" Its rework rate is elevated at {Math.Round(file.ReworkRate.Value * 100):F0}%."
                    : "";

                actions.Add(new ActionRecommendation
                {
                    Priority = 1,
                    Category = ActionCategories.KnowledgeRisk,
                    Title = $"Distribute ownership of {fileName}",
                    Narrative = $"{file.Path} is a critical hotspot (attention score: {file.AttentionScore:F1}) owned {sharePct}% by {ownerDisplay} (truck factor = 1).{reworkClause} If {ownerDisplay} is unavailable, maintenance and downstream features in this area risk being blocked. Pair programming or targeted knowledge transfer is recommended to build team redundancy.",
                    EstimatedImpact = "~3hrs/week coordination savings and eliminates single-owner bus-factor risk",
                    SuggestedAssignee = suggestedAssignee,
                    Evidence = new List<object> { file, file.KnowledgeSilo ?? (object)new KnowledgeSiloMetric(), file.Contributors ?? (object)new List<ContributorShare>() },
                    DetectionRules = new List<string> { RuleId }
                });
            }
        }

        return actions;
    }

    private static string ResolveSecondaryAssignee(FileMetric file, string primaryOwner, AnalysisResult result)
    {
        // 1. Check for secondary contributors directly on the file
        if (file.Contributors != null && file.Contributors.Count > 1)
        {
            var secondary = file.Contributors
                .Where(c => !string.Equals(c.Name, primaryOwner, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(c => c.ActivityShare)
                .ThenByDescending(c => c.Activity)
                .FirstOrDefault();

            if (secondary != null && !string.IsNullOrWhiteSpace(secondary.Name))
            {
                int secPct = (int)Math.Round(secondary.ActivityShare * 100);
                return $"{secondary.Name} ({secPct}% existing familiarity, lowest context-switch cost)";
            }
        }

        // 2. Check for contributors active in the same area
        if (!string.IsNullOrEmpty(file.Area) && result.Contributors != null)
        {
            var areaDev = result.Contributors
                .Where(c => !string.Equals(c.Name, primaryOwner, StringComparison.OrdinalIgnoreCase))
                .Select(c => new
                {
                    Contributor = c,
                    AreaMetric = c.Areas?.FirstOrDefault(a => string.Equals(a.Area, file.Area, StringComparison.OrdinalIgnoreCase))
                })
                .Where(x => x.AreaMetric != null && (x.AreaMetric.Activity > 0 || x.AreaMetric.FamiliarityScore > 0))
                .OrderByDescending(x => x.AreaMetric!.FamiliarityScore)
                .ThenByDescending(x => x.AreaMetric!.ActivityShare)
                .FirstOrDefault();

            if (areaDev != null && !string.IsNullOrWhiteSpace(areaDev.Contributor.Name))
            {
                int famPct = (int)Math.Round(areaDev.AreaMetric!.FamiliarityScore > 0 
                    ? areaDev.AreaMetric.FamiliarityScore 
                    : areaDev.AreaMetric.ActivityShare * 100);
                return $"{areaDev.Contributor.Name} ({famPct}% area familiarity in {file.Area})";
            }
        }

        // 3. General repository contributor
        if (result.Contributors != null)
        {
            var repoDev = result.Contributors
                .Where(c => !string.Equals(c.Name, primaryOwner, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(c => c.TotalActivity)
                .FirstOrDefault();

            if (repoDev != null && !string.IsNullOrWhiteSpace(repoDev.Name))
            {
                return $"{repoDev.Name} (active codebase contributor)";
            }
        }

        return $"Secondary Developer (pair with {(string.IsNullOrEmpty(primaryOwner) ? "primary owner" : primaryOwner)})";
    }
}

/// <summary>
/// Detects files acting as change multipliers and coupling hubs across multiple distinct codebase areas.
/// Trigger: temporal couplings spanning 2+ distinct areas with degree >= 0.5 or total coupled files >= 3.
/// </summary>
public class CouplingHubRule : IActionRule
{
    public string RuleId => "coupling_hub";

    public List<ActionRecommendation> Evaluate(ActionEvaluationContext context)
    {
        var actions = new List<ActionRecommendation>();
        var couplings = context.Result.TemporalCoupling;
        if (couplings == null || couplings.Count == 0)
        {
            return actions;
        }

        // Collect all distinct files mentioned in temporal couplings
        var allFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in couplings)
        {
            if (!string.IsNullOrWhiteSpace(c.FileA)) allFiles.Add(c.FileA);
            if (!string.IsNullOrWhiteSpace(c.FileB)) allFiles.Add(c.FileB);
        }

        foreach (var hubFile in allFiles)
        {
            string hubArea = ImpactPredictor.ResolveArea(hubFile, context.Result);

            var pairedCouplings = couplings
                .Where(c => ActionRuleUtils.IsMatchingFile(c.FileA, hubFile) || ActionRuleUtils.IsMatchingFile(c.FileB, hubFile))
                .ToList();

            var coupledFiles = pairedCouplings
                .Select(c => ActionRuleUtils.IsMatchingFile(c.FileA, hubFile) ? c.FileB : c.FileA)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var externalAreas = coupledFiles
                .Select(f => ImpactPredictor.ResolveArea(f, context.Result))
                .Where(a => !string.Equals(a, hubArea, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(a) && a != ".")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            double maxDegree = pairedCouplings.Count > 0 
                ? pairedCouplings.Max(c => c.CouplingDegree > 1.0 ? c.CouplingDegree / 100.0 : c.CouplingDegree)
                : 0.0;

            // Trigger if file acts as a hub connected to multiple files across external areas
            if (coupledFiles.Count >= 2 && (externalAreas.Count >= 2 || (externalAreas.Count >= 1 && (coupledFiles.Count >= 3 || maxDegree >= 0.5))))
            {
                string fileName = Path.GetFileName(hubFile);
                string assignee = ResolveHubMaintainer(hubFile, context.Result);
                string areasList = string.Join(", ", externalAreas);

                actions.Add(new ActionRecommendation
                {
                    Priority = 2,
                    Category = ActionCategories.ArchitecturalDrift,
                    Title = $"Decouple cross-module coupling hub {fileName}",
                    Narrative = $"{hubFile} is a change multiplier temporally coupled to {coupledFiles.Count} file(s) spanning {externalAreas.Count} external area(s) ({areasList}). Frequent co-changes across module boundaries indicate implicit architectural leakage and high coupling. Extracting shared abstractions or enforcing interface boundaries will insulate modules from cascading changes.",
                    EstimatedImpact = "Enforces modular boundaries, isolates component changes, and reduces architectural regression risk",
                    SuggestedAssignee = assignee,
                    Evidence = new List<object> { hubFile, pairedCouplings },
                    DetectionRules = new List<string> { RuleId }
                });
            }
        }

        return actions
            .OrderByDescending(a => ((List<TemporalCoupling>)a.Evidence[1]).Count)
            .ToList();
    }

    private static string ResolveHubMaintainer(string filePath, AnalysisResult result)
    {
        if (result.Files != null)
        {
            var fileMetric = result.Files.FirstOrDefault(f => ActionRuleUtils.IsMatchingFile(f.Path, filePath));
            if (fileMetric?.Contributors != null && fileMetric.Contributors.Count > 0)
            {
                var top = fileMetric.Contributors.OrderByDescending(c => c.ActivityShare).FirstOrDefault();
                if (top != null && !string.IsNullOrWhiteSpace(top.Name))
                {
                    return $"{top.Name} (maintainer of {Path.GetFileName(filePath)})";
                }
            }
        }

        string area = ImpactPredictor.ResolveArea(filePath, result);
        if (result.Contributors != null)
        {
            var areaContr = result.Contributors
                .Where(c => c.Areas?.Any(a => string.Equals(a.Area, area, StringComparison.OrdinalIgnoreCase)) == true)
                .OrderByDescending(c => c.TotalActivity)
                .FirstOrDefault();

            if (areaContr != null && !string.IsNullOrWhiteSpace(areaContr.Name))
            {
                return $"{areaContr.Name} (area maintainer)";
            }
        }

        return "Architecture Team / Module Maintainers";
    }
}

/// <summary>
/// Detects review collaboration bottlenecks where a single reviewer handles > 30% of co-authored commits.
/// Trigger: Single reviewer handles > 30% of all co-authored / collaborative PRs.
/// </summary>
public class ReviewBottleneckRule : IActionRule
{
    public string RuleId => "review_bottleneck";

    public List<ActionRecommendation> Evaluate(ActionEvaluationContext context)
    {
        var actions = new List<ActionRecommendation>();
        var reviewCollaboration = context.Result.CuratedReports?.ReviewCollaboration;
        if (reviewCollaboration?.Pairs == null || reviewCollaboration.Pairs.Count == 0)
        {
            return actions;
        }

        int totalReviews = reviewCollaboration.Pairs.Sum(p => p.PrCount);
        if (totalReviews == 0)
        {
            return actions;
        }

        var reviewerGroups = reviewCollaboration.Pairs
            .GroupBy(p => p.Reviewer, StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                Reviewer = g.Key,
                Count = g.Sum(p => p.PrCount),
                Share = (double)g.Sum(p => p.PrCount) / totalReviews,
                Authors = g.Select(p => p.Author).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            })
            .Where(r => r.Share > 0.30)
            .OrderByDescending(r => r.Share)
            .ToList();

        var allReviewers = reviewCollaboration.Pairs
            .Select(p => p.Reviewer)
            .Concat(reviewCollaboration.Pairs.Select(p => p.Author))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var r in reviewerGroups)
        {
            int sharePct = (int)Math.Round(r.Share * 100);
            var candidates = allReviewers
                .Where(name => !string.Equals(name, r.Reviewer, StringComparison.OrdinalIgnoreCase))
                .Take(2)
                .ToList();

            string candidateSuggestion = candidates.Count > 0
                ? $"Redistribute reviews from {r.Reviewer} to {string.Join(", ", candidates)}"
                : $"Redistribute reviews from {r.Reviewer} across other team members";

            actions.Add(new ActionRecommendation
            {
                Priority = 2,
                Category = ActionCategories.DeliveryBottleneck,
                Title = $"Redistribute code review load from {r.Reviewer}",
                Narrative = $"{r.Reviewer} handles {sharePct}% ({r.Count}/{totalReviews}) of all collaborative reviews across {r.Authors.Count} author(s). Concentrating code reviews on a single engineer creates a key delivery bottleneck and increases pull request turnaround latency.",
                EstimatedImpact = "Reduces review turnaround latency and balances team cognitive load",
                SuggestedAssignee = candidateSuggestion,
                Evidence = new List<object> { reviewCollaboration, r },
                DetectionRules = new List<string> { RuleId }
            });
        }

        return actions;
    }
}

/// <summary>
/// Detects accelerating codebase rot where zombie files grow significantly (> 20% vs baseline or high absolute volume).
/// </summary>
public class AcceleratingRotRule : IActionRule
{
    public string RuleId => "accelerating_rot";

    public List<ActionRecommendation> Evaluate(ActionEvaluationContext context)
    {
        var actions = new List<ActionRecommendation>();
        var codeRot = context.Result.CuratedReports?.CodeRot;
        int currZombieFiles = codeRot?.ZombieFileCount ?? 0;
        int currZombieLines = codeRot?.ZombieLines ?? 0;
        int thresholdDays = codeRot?.ThresholdDays ?? 365;

        if (context.Baseline != null && context.Baseline.Summary != null)
        {
            int prevZombieFiles = context.Baseline.Summary.ZombieFiles;
            int prevZombieLines = context.Baseline.Summary.ZombieLines;

            double growth = prevZombieFiles > 0 
                ? (double)(currZombieFiles - prevZombieFiles) / prevZombieFiles 
                : (currZombieFiles > 0 ? 1.0 : 0.0);

            if (currZombieFiles > prevZombieFiles && (growth > 0.20 || (currZombieFiles - prevZombieFiles) >= 5))
            {
                int growthPct = (int)Math.Round(growth * 100);
                actions.Add(new ActionRecommendation
                {
                    Priority = 3,
                    Category = ActionCategories.CodeHealth,
                    Title = "Clean up accelerating dead code and zombie files",
                    Narrative = $"Zombie file count grew by {growthPct}% since the last baseline ({prevZombieFiles} → {currZombieFiles} files, totaling {currZombieLines:N0} zombie lines untouched for >{thresholdDays} days). Codebase rot is accelerating and increases cognitive maintenance overhead. Scheduling a cleanup sprint is recommended.",
                    EstimatedImpact = "Reduces codebase surface area, decreases cognitive overhead, and cleans up dead paths",
                    SuggestedAssignee = "Team Maintainers (schedule technical debt cleanup sprint)",
                    Evidence = new List<object> { codeRot ?? new object(), context.Baseline },
                    DetectionRules = new List<string> { RuleId }
                });
            }
        }
        else if (currZombieFiles >= 5 || (context.Result.Files.Count > 0 && ((double)currZombieFiles / context.Result.Files.Count) >= 0.15))
        {
            actions.Add(new ActionRecommendation
            {
                Priority = 3,
                Category = ActionCategories.CodeHealth,
                Title = "Clean up dead code and zombie files",
                Narrative = $"{currZombieFiles} zombie files ({currZombieLines:N0} lines) have had no activity for over {thresholdDays} days. Stale files create confusion for developers and increase codebase complexity without delivering active value.",
                EstimatedImpact = "Reduces codebase cognitive load and eliminates dead code paths",
                SuggestedAssignee = "Team Maintainers (schedule technical debt cleanup sprint)",
                Evidence = new List<object> { codeRot ?? new object() },
                DetectionRules = new List<string> { RuleId }
            });
        }

        return actions;
    }
}

/// <summary>
/// Helper utilities for action rule evaluation.
/// </summary>
internal static class ActionRuleUtils
{
    public static bool IsMatchingFile(string? pathA, string? pathB)
    {
        if (string.IsNullOrWhiteSpace(pathA) || string.IsNullOrWhiteSpace(pathB)) return false;
        string normA = pathA.Replace('\\', '/').Trim('/');
        string normB = pathB.Replace('\\', '/').Trim('/');
        if (string.Equals(normA, normB, StringComparison.OrdinalIgnoreCase)) return true;
        if (normA.EndsWith("/" + normB, StringComparison.OrdinalIgnoreCase)) return true;
        if (normB.EndsWith("/" + normA, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
