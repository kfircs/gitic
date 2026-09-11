using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Gitic;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DepartureRiskLevel
{
    None,
    Low,
    Moderate,
    Critical
}

public class KnowledgeTransferCandidate
{
    [JsonPropertyName("candidate_name")]
    public string CandidateName { get; set; } = string.Empty;

    [JsonPropertyName("candidate_email")]
    public string CandidateEmail { get; set; } = string.Empty;

    [JsonPropertyName("familiarity_score")]
    public double FamiliarityScore { get; set; }

    [JsonPropertyName("context_distance")]
    public double ContextDistance { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;

    [JsonPropertyName("priority")]
    public int Priority { get; set; } = 1;
}

public class DepartureRiskFile
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("area")]
    public string Area { get; set; } = string.Empty;

    [JsonPropertyName("ownership_share")]
    public double OwnershipShare { get; set; }

    [JsonPropertyName("touches")]
    public int Touches { get; set; }

    [JsonPropertyName("lines")]
    public int Lines { get; set; }

    [JsonPropertyName("risk_level")]
    public DepartureRiskLevel RiskLevel { get; set; }

    [JsonPropertyName("is_sole_owner")]
    public bool IsSoleOwner { get; set; }

    [JsonPropertyName("secondary_contributors")]
    public List<ContributorShare> SecondaryContributors { get; set; } = new();

    [JsonPropertyName("recommended_candidate")]
    public KnowledgeTransferCandidate? RecommendedCandidate { get; set; }

    [JsonPropertyName("coupling_degree")]
    public double CouplingDegree { get; set; }

    [JsonPropertyName("transfer_complexity")]
    public string TransferComplexity { get; set; } = "Low";
}

public class KnowledgeTransferPlanItem
{
    [JsonPropertyName("priority")]
    public int Priority { get; set; }

    [JsonPropertyName("file")]
    public string File { get; set; } = string.Empty;

    [JsonPropertyName("area")]
    public string Area { get; set; } = string.Empty;

    [JsonPropertyName("risk_level")]
    public DepartureRiskLevel RiskLevel { get; set; }

    [JsonPropertyName("transfer_to")]
    public string TransferTo { get; set; } = string.Empty;

    [JsonPropertyName("transfer_to_email")]
    public string TransferToEmail { get; set; } = string.Empty;

    [JsonPropertyName("why")]
    public string Why { get; set; } = string.Empty;

    [JsonPropertyName("context_distance")]
    public double ContextDistance { get; set; }

    [JsonPropertyName("complexity")]
    public string Complexity { get; set; } = "Low";

    [JsonPropertyName("estimated_effort")]
    public string EstimatedEffort { get; set; } = "1-2 days";
}

public class DepartureRiskReport
{
    [JsonPropertyName("target_developer")]
    public string TargetDeveloper { get; set; } = string.Empty;

    [JsonPropertyName("target_email")]
    public string TargetEmail { get; set; } = string.Empty;

    [JsonPropertyName("generated_at")]
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("critical_risk_files")]
    public List<DepartureRiskFile> CriticalRiskFiles { get; set; } = new();

    [JsonPropertyName("moderate_risk_files")]
    public List<DepartureRiskFile> ModerateRiskFiles { get; set; } = new();

    [JsonPropertyName("total_affected_files")]
    public int TotalAffectedFiles => CriticalRiskFiles.Count + ModerateRiskFiles.Count;

    [JsonPropertyName("total_affected_areas")]
    public int TotalAffectedAreas { get; set; }

    [JsonPropertyName("total_affected_lines")]
    public int TotalAffectedLines { get; set; }

    [JsonPropertyName("transfer_plan")]
    public List<KnowledgeTransferPlanItem> TransferPlan { get; set; } = new();

    [JsonPropertyName("estimated_onboarding_time")]
    public string EstimatedOnboardingTime { get; set; } = string.Empty;

    [JsonPropertyName("overall_risk_score")]
    public double OverallRiskScore { get; set; }
}

public interface IDepartureRiskAnalyzer
{
    DepartureRiskReport AnalyzeDepartureRisk(AnalysisResult result, string targetDeveloper);
    List<KnowledgeTransferCandidate> RankCandidates(FileMetric file, AnalysisResult result, string targetDeveloper);
    KnowledgeTransferCandidate SelectBestCandidate(FileMetric file, AnalysisResult result, string targetDeveloper);
}

public class DepartureRiskAnalyzer : IDepartureRiskAnalyzer
{
    public DepartureRiskReport AnalyzeDepartureRisk(AnalysisResult result, string targetDeveloper)
    {
        if (result == null) throw new ArgumentNullException(nameof(result));
        if (string.IsNullOrWhiteSpace(targetDeveloper))
        {
            throw new ArgumentException("Target developer cannot be null or whitespace.", nameof(targetDeveloper));
        }

        var (resolvedName, resolvedEmail) = ResolveTargetDeveloper(result, targetDeveloper);

        var report = new DepartureRiskReport
        {
            TargetDeveloper = resolvedName,
            TargetEmail = resolvedEmail,
            GeneratedAt = DateTimeOffset.UtcNow
        };

        if (result.Files == null || result.Files.Count == 0)
        {
            report.EstimatedOnboardingTime = "0 days (no risk detected)";
            return report;
        }

        foreach (var file in result.Files)
        {
            var targetShare = file.Contributors?.FirstOrDefault(c => MatchesTarget(c.Name, c.Email, targetDeveloper, resolvedName, resolvedEmail));
            if (targetShare == null)
            {
                continue;
            }

            double ownershipShare = targetShare.ActivityShare;
            if (ownershipShare <= 0 && file.Touches > 0)
            {
                ownershipShare = ScoringUtils.RoundRatio(targetShare.Activity / file.Touches);
            }
            if (ownershipShare <= 0 && targetShare.Activity > 0)
            {
                ownershipShare = 1.0;
            }

            if (ownershipShare <= 0)
            {
                continue;
            }

            var allContributors = file.Contributors ?? new List<ContributorShare>();
            var activeContributors = allContributors.Where(c => c.Activity > 0 || c.ActivityShare > 0).ToList();
            bool isSoleOwner = (activeContributors.Count == 1 && MatchesTarget(activeContributors[0].Name, activeContributors[0].Email, targetDeveloper, resolvedName, resolvedEmail)) ||
                               (allContributors.Count == 1 && MatchesTarget(allContributors[0].Name, allContributors[0].Email, targetDeveloper, resolvedName, resolvedEmail));

            var secondaries = allContributors
                .Where(c => !MatchesTarget(c.Name, c.Email, targetDeveloper, resolvedName, resolvedEmail))
                .OrderByDescending(c => c.ActivityShare)
                .ThenByDescending(c => c.Activity)
                .ToList();

            DepartureRiskLevel level = DepartureRiskLevel.None;

            // Critical: activity share >= 70% or sole active contributor
            if (isSoleOwner || ownershipShare >= 0.70)
            {
                level = DepartureRiskLevel.Critical;
            }
            // Moderate: top contributor (>50%) and secondary familiar contributors exist (and < 70%)
            else if (ownershipShare > 0.50)
            {
                level = DepartureRiskLevel.Moderate;
            }

            if (level == DepartureRiskLevel.None)
            {
                continue;
            }

            var (complexity, effort) = CalculateTransferComplexity(file, result);
            double maxCoupling = 0.0;
            if (result.TemporalCoupling != null)
            {
                var couplings = result.TemporalCoupling
                    .Where(c => string.Equals(c.FileA, file.Path, StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(c.FileB, file.Path, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (couplings.Count > 0)
                {
                    maxCoupling = couplings.Max(c => c.CouplingDegree);
                }
            }

            var candidate = SelectBestCandidate(file, result, targetDeveloper);

            var riskFile = new DepartureRiskFile
            {
                Path = file.Path,
                Area = file.Area ?? string.Empty,
                OwnershipShare = ownershipShare,
                Touches = file.Touches,
                Lines = file.Lines ?? file.LinesOfCode ?? 0,
                RiskLevel = level,
                IsSoleOwner = isSoleOwner,
                SecondaryContributors = secondaries,
                RecommendedCandidate = candidate,
                CouplingDegree = maxCoupling,
                TransferComplexity = complexity
            };

            if (level == DepartureRiskLevel.Critical)
            {
                report.CriticalRiskFiles.Add(riskFile);
            }
            else
            {
                report.ModerateRiskFiles.Add(riskFile);
            }
        }

        // Sort files
        report.CriticalRiskFiles = report.CriticalRiskFiles
            .OrderByDescending(f => f.OwnershipShare)
            .ThenByDescending(f => f.Touches)
            .ThenByDescending(f => f.Lines)
            .ToList();

        report.ModerateRiskFiles = report.ModerateRiskFiles
            .OrderByDescending(f => f.OwnershipShare)
            .ThenByDescending(f => f.Touches)
            .ThenByDescending(f => f.Lines)
            .ToList();

        var allAffectedFiles = report.CriticalRiskFiles.Concat(report.ModerateRiskFiles).ToList();

        // Populate Knowledge Transfer Plan
        int priority = 1;
        foreach (var rf in allAffectedFiles)
        {
            var cand = rf.RecommendedCandidate;
            var originalFile = result.Files.FirstOrDefault(f => string.Equals(f.Path, rf.Path, StringComparison.OrdinalIgnoreCase)) ??
                               new FileMetric { Path = rf.Path, Touches = rf.Touches, Lines = rf.Lines };
            var (_, effort) = CalculateTransferComplexity(originalFile, result);

            report.TransferPlan.Add(new KnowledgeTransferPlanItem
            {
                Priority = priority++,
                File = rf.Path,
                Area = rf.Area,
                RiskLevel = rf.RiskLevel,
                TransferTo = cand?.CandidateName ?? "Unassigned",
                TransferToEmail = cand?.CandidateEmail ?? string.Empty,
                Why = cand?.Reason ?? "None",
                ContextDistance = cand?.ContextDistance ?? 3.0,
                Complexity = rf.TransferComplexity,
                EstimatedEffort = effort
            });
        }

        // Compute summary metrics
        report.TotalAffectedAreas = allAffectedFiles
            .Select(f => f.Area)
            .Where(a => !string.IsNullOrEmpty(a))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        report.TotalAffectedLines = allAffectedFiles.Sum(f => f.Lines);

        if (allAffectedFiles.Count == 0)
        {
            report.OverallRiskScore = 0.0;
            report.EstimatedOnboardingTime = "0 days (no risk detected)";
        }
        else
        {
            double criticalScore = report.CriticalRiskFiles.Count * 20.0;
            double moderateScore = report.ModerateRiskFiles.Count * 8.0;
            double avgShare = allAffectedFiles.Average(f => f.OwnershipShare) * 40.0;
            double linesFactor = Math.Min(20.0, report.TotalAffectedLines / 200.0);
            report.OverallRiskScore = Math.Min(100.0, Math.Round(criticalScore + moderateScore + avgShare + linesFactor, 1));

            if (report.CriticalRiskFiles.Count >= 3 || report.TotalAffectedFiles >= 8 || report.TotalAffectedLines >= 3000)
            {
                report.EstimatedOnboardingTime = "2-3 sprints for full coverage";
            }
            else if (report.CriticalRiskFiles.Count >= 1 || report.TotalAffectedFiles >= 3)
            {
                report.EstimatedOnboardingTime = "1-2 sprints for full coverage";
            }
            else
            {
                report.EstimatedOnboardingTime = "1 sprint for full coverage";
            }
        }

        return report;
    }

    public List<KnowledgeTransferCandidate> RankCandidates(FileMetric file, AnalysisResult result, string targetDeveloper)
    {
        if (file == null) throw new ArgumentNullException(nameof(file));
        if (result == null) throw new ArgumentNullException(nameof(result));

        var (targetName, targetEmail) = ResolveTargetDeveloper(result, targetDeveloper);
        var candidates = new List<KnowledgeTransferCandidate>();
        var seenContributors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Direct file secondary contributors (Tier 1: lowest context distance)
        if (file.Contributors != null)
        {
            var fileSecondaries = file.Contributors
                .Where(c => !MatchesTarget(c.Name, c.Email, targetDeveloper, targetName, targetEmail))
                .OrderByDescending(c => c.ActivityShare)
                .ThenByDescending(c => c.Activity);

            foreach (var sec in fileSecondaries)
            {
                string key = !string.IsNullOrEmpty(sec.Name) ? sec.Name : sec.Email;
                if (string.IsNullOrEmpty(key) || seenContributors.Contains(key)) continue;
                seenContributors.Add(key);

                double share = sec.ActivityShare > 0 ? sec.ActivityShare : (file.Touches > 0 ? sec.Activity / file.Touches : 0.1);
                double fam = share * 100.0;
                double contextDistance = 0.0 + (1.0 - Math.Clamp(share, 0.0, 1.0)) * 0.5;

                candidates.Add(new KnowledgeTransferCandidate
                {
                    CandidateName = sec.Name,
                    CandidateEmail = sec.Email,
                    FamiliarityScore = Math.Round(fam, 1),
                    ContextDistance = Math.Round(contextDistance, 4),
                    Reason = $"{Math.Round(fam)}% file familiarity"
                });
            }
        }

        // 2. Same area contributors (Tier 2: moderate context distance)
        if (!string.IsNullOrEmpty(file.Area))
        {
            if (result.Contributors != null)
            {
                var areaMatches = result.Contributors
                    .Where(c => !MatchesTarget(c.Name, c.Email, targetDeveloper, targetName, targetEmail))
                    .Select(c => new
                    {
                        Contributor = c,
                        AreaMetric = c.Areas?.FirstOrDefault(a => string.Equals(a.Area, file.Area, StringComparison.OrdinalIgnoreCase))
                    })
                    .Where(x => x.AreaMetric != null && (x.AreaMetric.Activity > 0 || x.AreaMetric.FamiliarityScore > 0 || x.AreaMetric.ActivityShare > 0))
                    .OrderByDescending(x => x.AreaMetric!.FamiliarityScore)
                    .ThenByDescending(x => x.AreaMetric!.ActivityShare)
                    .ThenByDescending(x => x.AreaMetric!.Activity);

                foreach (var match in areaMatches)
                {
                    string key = !string.IsNullOrEmpty(match.Contributor.Name) ? match.Contributor.Name : match.Contributor.Email;
                    if (string.IsNullOrEmpty(key) || seenContributors.Contains(key)) continue;
                    seenContributors.Add(key);

                    double fam = match.AreaMetric!.FamiliarityScore > 0
                        ? match.AreaMetric.FamiliarityScore
                        : (match.AreaMetric.ActivityShare * 100.0);
                    double contextDistance = 1.0 + (100.0 - Math.Clamp(fam, 0.0, 100.0)) / 100.0 * 0.5;

                    candidates.Add(new KnowledgeTransferCandidate
                    {
                        CandidateName = match.Contributor.Name,
                        CandidateEmail = match.Contributor.Email,
                        FamiliarityScore = Math.Round(fam, 1),
                        ContextDistance = Math.Round(contextDistance, 4),
                        Reason = $"{Math.Round(fam)}% area familiarity ({file.Area})"
                    });
                }
            }

            // Fallback to AreaMetric contributors in result.Areas
            if (result.Areas != null)
            {
                var area = result.Areas.FirstOrDefault(a => string.Equals(a.Area, file.Area, StringComparison.OrdinalIgnoreCase));
                if (area?.Contributors != null)
                {
                    var areaContrs = area.Contributors
                        .Where(c => !MatchesTarget(c.Name, c.Email, targetDeveloper, targetName, targetEmail))
                        .OrderByDescending(c => c.ActivityShare)
                        .ThenByDescending(c => c.Activity);

                    foreach (var ac in areaContrs)
                    {
                        string key = !string.IsNullOrEmpty(ac.Name) ? ac.Name : ac.Email;
                        if (string.IsNullOrEmpty(key) || seenContributors.Contains(key)) continue;
                        seenContributors.Add(key);

                        double fam = ac.ActivityShare * 100.0;
                        double contextDistance = 1.0 + (100.0 - Math.Clamp(fam, 0.0, 100.0)) / 100.0 * 0.5;

                        candidates.Add(new KnowledgeTransferCandidate
                        {
                            CandidateName = ac.Name,
                            CandidateEmail = ac.Email,
                            FamiliarityScore = Math.Round(fam, 1),
                            ContextDistance = Math.Round(contextDistance, 4),
                            Reason = $"{Math.Round(fam)}% area familiarity ({file.Area})"
                        });
                    }
                }
            }
        }

        // 3. Other contributors in repo (Tier 3: general codebase familiarity)
        if (result.Contributors != null)
        {
            var otherContrs = result.Contributors
                .Where(c => !MatchesTarget(c.Name, c.Email, targetDeveloper, targetName, targetEmail))
                .OrderByDescending(c => c.TotalActivity);

            foreach (var oc in otherContrs)
            {
                string key = !string.IsNullOrEmpty(oc.Name) ? oc.Name : oc.Email;
                if (string.IsNullOrEmpty(key) || seenContributors.Contains(key)) continue;
                seenContributors.Add(key);

                double contextDistance = 2.0 + 1.0 / (1.0 + Math.Max(0.0, oc.TotalActivity));

                candidates.Add(new KnowledgeTransferCandidate
                {
                    CandidateName = oc.Name,
                    CandidateEmail = oc.Email,
                    FamiliarityScore = 0.0,
                    ContextDistance = Math.Round(contextDistance, 4),
                    Reason = "Codebase familiarity (no area history)"
                });
            }
        }

        // Sort candidates by ContextDistance ascending, then FamiliarityScore descending
        var sorted = candidates
            .OrderBy(c => c.ContextDistance)
            .ThenByDescending(c => c.FamiliarityScore)
            .ToList();

        for (int i = 0; i < sorted.Count; i++)
        {
            sorted[i].Priority = i + 1;
        }

        return sorted;
    }

    public KnowledgeTransferCandidate SelectBestCandidate(FileMetric file, AnalysisResult result, string targetDeveloper)
    {
        var ranked = RankCandidates(file, result, targetDeveloper);
        if (ranked.Count > 0)
        {
            return ranked[0];
        }

        return new KnowledgeTransferCandidate
        {
            CandidateName = "Unassigned",
            CandidateEmail = string.Empty,
            FamiliarityScore = 0.0,
            ContextDistance = 3.0,
            Reason = "No other active contributors in repository",
            Priority = 1
        };
    }

    public static (string Level, string Effort) CalculateTransferComplexity(FileMetric file, AnalysisResult? result)
    {
        int lines = file.Lines ?? file.LinesOfCode ?? 0;
        int touches = file.Touches;
        double maxCoupling = 0.0;
        int coupledCount = 0;

        if (result?.TemporalCoupling != null && result.TemporalCoupling.Count > 0)
        {
            var couplings = result.TemporalCoupling
                .Where(c => string.Equals(c.FileA, file.Path, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(c.FileB, file.Path, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (couplings.Count > 0)
            {
                maxCoupling = couplings.Max(c => c.CouplingDegree);
                coupledCount = couplings.Count;
            }
        }

        int linesScore = lines > 800 ? 3 : lines > 300 ? 2 : lines > 100 ? 1 : 0;
        int touchesScore = touches > 20 ? 3 : touches > 10 ? 2 : touches > 3 ? 1 : 0;
        int couplingScore = (maxCoupling >= 0.6 || coupledCount > 3) ? 3 :
                            (maxCoupling >= 0.3 || coupledCount > 1) ? 2 :
                            coupledCount > 0 ? 1 : 0;

        int totalScore = linesScore + touchesScore + couplingScore;

        if (totalScore >= 5 || lines > 800)
        {
            return ("High", "1-2 sprints");
        }
        if (totalScore >= 3 || lines > 300 || touches > 10)
        {
            return ("Moderate", "3-5 days");
        }
        return ("Low", "1-2 days");
    }

    private static (string TargetName, string TargetEmail) ResolveTargetDeveloper(AnalysisResult result, string targetDeveloper)
    {
        if (string.IsNullOrWhiteSpace(targetDeveloper))
        {
            throw new ArgumentException("Target developer cannot be null or whitespace.", nameof(targetDeveloper));
        }

        if (result.Contributors != null && result.Contributors.Count > 0)
        {
            var exact = result.Contributors.FirstOrDefault(c => string.Equals(c.Name, targetDeveloper, StringComparison.Ordinal));
            if (exact != null) return (exact.Name, exact.Email);

            var match = result.Contributors.FirstOrDefault(c =>
                string.Equals(c.Name, targetDeveloper, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.Email, targetDeveloper, StringComparison.OrdinalIgnoreCase));
            if (match != null) return (match.Name, match.Email);
        }

        if (result.Files != null)
        {
            foreach (var file in result.Files)
            {
                if (file.Contributors == null) continue;
                var fc = file.Contributors.FirstOrDefault(c =>
                    string.Equals(c.Name, targetDeveloper, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c.Email, targetDeveloper, StringComparison.OrdinalIgnoreCase));
                if (fc != null) return (fc.Name, fc.Email);
            }
        }

        return (targetDeveloper, string.Empty);
    }

    private static bool MatchesTarget(string? name, string? email, string targetDeveloper, string resolvedName, string resolvedEmail)
    {
        if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(email)) return false;

        if (!string.IsNullOrEmpty(name))
        {
            if (string.Equals(name, targetDeveloper, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, resolvedName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (!string.IsNullOrEmpty(email))
        {
            if (string.Equals(email, targetDeveloper, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(resolvedEmail) && string.Equals(email, resolvedEmail, StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        return false;
    }
}
