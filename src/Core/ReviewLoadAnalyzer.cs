using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using Kfc.Cli.Terminal;

namespace Gitic;

/// <summary>
/// Load classification for review participation.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReviewLoadClassification
{
    Balanced,
    Underutilized,
    Overloaded
}

/// <summary>
/// Recommended alternative reviewer candidate for peer redistribution.
/// </summary>
public class AlternativeReviewerCandidate
{
    [JsonPropertyName("candidate_name")]
    public string CandidateName { get; set; } = string.Empty;

    [JsonPropertyName("candidate_email")]
    public string CandidateEmail { get; set; } = string.Empty;

    [JsonPropertyName("target_author")]
    public string TargetAuthor { get; set; } = string.Empty;

    [JsonPropertyName("area")]
    public string Area { get; set; } = string.Empty;

    [JsonPropertyName("familiarity_score")]
    public double FamiliarityScore { get; set; }

    [JsonPropertyName("current_review_share")]
    public double CurrentReviewShare { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Profile representing an individual contributor's review workload, concentration, and redistribution suggestions.
/// </summary>
public class ReviewLoadProfile
{
    [JsonPropertyName("reviewer")]
    public string Reviewer { get; set; } = string.Empty;

    [JsonPropertyName("reviewer_email")]
    public string ReviewerEmail { get; set; } = string.Empty;

    [JsonPropertyName("review_count")]
    public int ReviewCount { get; set; }

    [JsonPropertyName("review_share")]
    public double ReviewShare { get; set; }

    [JsonPropertyName("authors_reviewed")]
    public List<string> AuthorsReviewed { get; set; } = new();

    [JsonPropertyName("review_diversity")]
    public int ReviewDiversity { get; set; }

    [JsonPropertyName("load_classification")]
    public ReviewLoadClassification LoadClassification { get; set; } = ReviewLoadClassification.Balanced;

    [JsonPropertyName("redistribution_suggestion")]
    public string RedistributionSuggestion { get; set; } = string.Empty;

    [JsonPropertyName("author_commit_count")]
    public int AuthorCommitCount { get; set; }

    [JsonPropertyName("author_share")]
    public double AuthorShare { get; set; }

    [JsonPropertyName("candidates")]
    public List<AlternativeReviewerCandidate> Candidates { get; set; } = new();

    [JsonIgnore]
    public bool IsOverloaded => LoadClassification == ReviewLoadClassification.Overloaded;

    [JsonIgnore]
    public bool IsUnderutilized => LoadClassification == ReviewLoadClassification.Underutilized;

    [JsonIgnore]
    public bool IsBalanced => LoadClassification == ReviewLoadClassification.Balanced;
}

/// <summary>
/// Complete review load analysis result containing team profiles, concentration metrics, and recommendations.
/// </summary>
public class ReviewLoadAnalysisResult
{
    [JsonPropertyName("profiles")]
    public List<ReviewLoadProfile> Profiles { get; set; } = new();

    [JsonPropertyName("gini_coefficient")]
    public double GiniCoefficient { get; set; }

    [JsonPropertyName("total_reviews")]
    public int TotalReviews { get; set; }

    [JsonPropertyName("average_reviews_per_reviewer")]
    public double AverageReviewsPerReviewer { get; set; }

    [JsonPropertyName("overloaded_reviewers_count")]
    public int OverloadedReviewersCount => Profiles.Count(p => p.IsOverloaded);

    [JsonPropertyName("actionable_recommendations")]
    public List<string> ActionableRecommendations { get; set; } = new();

    [JsonPropertyName("redistribution_notes")]
    public List<string> RedistributionNotes { get; set; } = new();
}

public partial class AnalysisResult
{
    [JsonPropertyName("review_load")]
    public ReviewLoadAnalysisResult? ReviewLoad { get; set; }
}

/// <summary>
/// Interface for review load balancing and reviewer silo mitigation.
/// </summary>
public interface IReviewLoadAnalyzer
{
    ReviewLoadAnalysisResult AnalyzeReviewLoad(AnalysisResult result);
    List<AlternativeReviewerCandidate> FindAlternativeReviewers(string overloadedReviewer, string author, AnalysisResult result);
    void EnrichReviewCollaboration(AnalysisResult result);
    string FormatSummary(ReviewLoadAnalysisResult result);
    string FormatMarkdown(ReviewLoadAnalysisResult result);
    string FormatCliTable(ReviewLoadAnalysisResult result);
}

/// <summary>
/// Core analyzer that evaluates review workload distribution from co-authorship graphs,
/// detects overloaded reviewers (>2x mean peer load), computes concentration metrics (Gini),
/// and generates capability-aware peer redistribution suggestions.
/// </summary>
public class ReviewLoadAnalyzer : IReviewLoadAnalyzer
{
    public const double OverloadThresholdMultiplier = 2.0;
    public const double UnderutilizedThresholdMultiplier = 0.5;

    public static ReviewLoadAnalysisResult Analyze(AnalysisResult result)
    {
        var analyzer = new ReviewLoadAnalyzer();
        return analyzer.AnalyzeReviewLoad(result);
    }

    public ReviewLoadAnalysisResult AnalyzeReviewLoad(AnalysisResult result)
    {
        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        var analysisResult = new ReviewLoadAnalysisResult();
        var pairs = result.CuratedReports?.ReviewCollaboration?.Pairs ?? new List<ReviewPair>();

        // Gather all participant names from pairs and contributors list
        var participantNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (result.Contributors != null)
        {
            foreach (var c in result.Contributors)
            {
                if (!string.IsNullOrWhiteSpace(c.Name)) participantNames.Add(c.Name);
                else if (!string.IsNullOrWhiteSpace(c.Email)) participantNames.Add(c.Email);
            }
        }

        foreach (var p in pairs)
        {
            if (!string.IsNullOrWhiteSpace(p.Reviewer)) participantNames.Add(p.Reviewer);
            if (!string.IsNullOrWhiteSpace(p.Author)) participantNames.Add(p.Author);
        }

        if (participantNames.Count == 0 && pairs.Count == 0)
        {
            EnrichResult(result, analysisResult);
            return analysisResult;
        }

        // Aggregate reviews conducted and authored per participant
        var reviewsByReviewer = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var authorsReviewedByReviewer = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var authoredCommitsByAuthor = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        int totalReviews = 0;
        foreach (var pair in pairs)
        {
            if (string.IsNullOrWhiteSpace(pair.Reviewer)) continue;

            totalReviews += pair.PrCount;

            reviewsByReviewer.TryGetValue(pair.Reviewer, out int currentRevCount);
            reviewsByReviewer[pair.Reviewer] = currentRevCount + pair.PrCount;

            if (!authorsReviewedByReviewer.TryGetValue(pair.Reviewer, out var authorSet))
            {
                authorSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                authorsReviewedByReviewer[pair.Reviewer] = authorSet;
            }
            if (!string.IsNullOrWhiteSpace(pair.Author))
            {
                authorSet.Add(pair.Author);
            }

            if (!string.IsNullOrWhiteSpace(pair.Author))
            {
                authoredCommitsByAuthor.TryGetValue(pair.Author, out int currentAuthCount);
                authoredCommitsByAuthor[pair.Author] = currentAuthCount + pair.PrCount;
            }
        }

        analysisResult.TotalReviews = totalReviews;

        // Determine candidate participant pool for load calculation
        var pool = participantNames.Count > 0 ? participantNames.ToList() : reviewsByReviewer.Keys.ToList();
        int poolSize = pool.Count;

        double meanReviews = poolSize > 0 ? (double)totalReviews / poolSize : 0.0;
        double meanShare = poolSize > 0 ? 1.0 / poolSize : 0.0;

        analysisResult.AverageReviewsPerReviewer = poolSize > 0 ? Math.Round(meanReviews, 2) : 0.0;

        var profiles = new List<ReviewLoadProfile>();
        foreach (var participant in pool)
        {
            reviewsByReviewer.TryGetValue(participant, out int revCount);
            authorsReviewedByReviewer.TryGetValue(participant, out var authorsSet);
            authoredCommitsByAuthor.TryGetValue(participant, out int authCount);

            var authorsList = authorsSet != null ? authorsSet.OrderBy(a => a, StringComparer.OrdinalIgnoreCase).ToList() : new List<string>();
            double revShare = totalReviews > 0 ? (double)revCount / totalReviews : 0.0;
            double authShare = totalReviews > 0 ? (double)authCount / totalReviews : 0.0;

            // Resolve email from contributors if available
            string email = string.Empty;
            if (result.Contributors != null)
            {
                var cm = result.Contributors.FirstOrDefault(c =>
                    string.Equals(c.Name, participant, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c.Email, participant, StringComparison.OrdinalIgnoreCase));
                if (cm != null && !string.IsNullOrEmpty(cm.Email))
                {
                    email = cm.Email;
                }
            }

            int peerReviewCount = totalReviews - revCount;
            double peerAverageCount = poolSize > 1 ? (double)peerReviewCount / (poolSize - 1) : 0.0;

            ReviewLoadClassification classification = ReviewLoadClassification.Balanced;
            if (poolSize > 1 && totalReviews > 0)
            {
                // Overloaded: review share >= 2x mean share or exceeds 2x peer review average
                bool isOverloaded = (revShare >= (OverloadThresholdMultiplier * meanShare) - 1e-9 && revShare > meanShare) ||
                                    (peerAverageCount > 0 && revCount >= (OverloadThresholdMultiplier * peerAverageCount) - 1e-9 && revCount > peerAverageCount) ||
                                    (peerAverageCount == 0 && revCount > 0);

                bool isUnderutilized = revCount == 0 ||
                                       (revShare < (UnderutilizedThresholdMultiplier * meanShare) - 1e-9) ||
                                       (meanReviews > 0 && revCount < (UnderutilizedThresholdMultiplier * meanReviews) - 1e-9);

                if (isOverloaded)
                {
                    classification = ReviewLoadClassification.Overloaded;
                }
                else if (isUnderutilized)
                {
                    classification = ReviewLoadClassification.Underutilized;
                }
            }

            var profile = new ReviewLoadProfile
            {
                Reviewer = participant,
                ReviewerEmail = email,
                ReviewCount = revCount,
                ReviewShare = Math.Round(revShare, 4),
                AuthorsReviewed = authorsList,
                ReviewDiversity = authorsList.Count,
                LoadClassification = classification,
                AuthorCommitCount = authCount,
                AuthorShare = Math.Round(authShare, 4)
            };

            profiles.Add(profile);
        }

        // Compute Gini Coefficient for review distribution
        analysisResult.GiniCoefficient = CalculateGiniCoefficient(profiles.Select(p => (double)p.ReviewCount).ToList());

        // Sort profiles: Overloaded first, then by review count descending, then by name
        profiles = profiles
            .OrderByDescending(p => p.IsOverloaded)
            .ThenByDescending(p => p.ReviewCount)
            .ThenBy(p => p.Reviewer, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Generate redistribution suggestions and actionable recommendations
        foreach (var profile in profiles)
        {
            if (profile.IsOverloaded)
            {
                var candidatesForProfile = new List<AlternativeReviewerCandidate>();

                foreach (var author in profile.AuthorsReviewed)
                {
                    var altReviewers = FindAlternativeReviewers(profile.Reviewer, author, result);
                    if (altReviewers.Count > 0)
                    {
                        candidatesForProfile.AddRange(altReviewers);
                    }
                }

                profile.Candidates = candidatesForProfile
                    .GroupBy(c => c.CandidateName, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .OrderBy(c => c.CurrentReviewShare)
                    .ThenByDescending(c => c.FamiliarityScore)
                    .ToList();

                var bestCandidates = profile.Candidates.Take(2).ToList();
                string suggestion;

                if (bestCandidates.Count > 0)
                {
                    var candSummaries = bestCandidates.Select(c =>
                    {
                        string areaInfo = !string.IsNullOrEmpty(c.Area) ? $", familiar with {c.Area}" : string.Empty;
                        return $"{c.CandidateName} ({c.CurrentReviewShare:P1} load{areaInfo})";
                    });

                    string targetAuthor = profile.AuthorsReviewed.FirstOrDefault() ?? "peer";
                    string candNames = string.Join(" or ", bestCandidates.Select(c => c.CandidateName));

                    suggestion = $"{profile.Reviewer} reviews {profile.ReviewShare:P1} of all collaborative commits" +
                                 (profile.AuthorShare > 0 ? $" but authors only {profile.AuthorShare:P1}" : "") +
                                 $". {string.Join(" and ", candSummaries)} {(bestCandidates.Count > 1 ? "are" : "is a")} capable alternative — consider routing more of {targetAuthor}'s PRs to {candNames} to balance the load.";

                    foreach (var c in bestCandidates)
                    {
                        analysisResult.ActionableRecommendations.Add(
                            $"Rebalance review load for {profile.Reviewer}: Route reviews from {c.TargetAuthor} to {c.CandidateName}" +
                            (!string.IsNullOrEmpty(c.Area) ? $" ({c.Area})" : string.Empty) +
                            $" to reduce review concentration.");

                        analysisResult.RedistributionNotes.Add(
                            $"Route reviews from {c.TargetAuthor} to {c.CandidateName}" +
                            (!string.IsNullOrEmpty(c.Area) ? $" (familiar with {c.Area})" : string.Empty) +
                            $" to relieve {profile.Reviewer} ({profile.ReviewShare:P1} load).");
                    }
                }
                else
                {
                    suggestion = $"{profile.Reviewer} is overloaded with {profile.ReviewShare:P1} of reviews ({profile.ReviewCount} reviews conducted). Distribute upcoming review assignments across peers.";
                    analysisResult.ActionableRecommendations.Add($"Rebalance review load for {profile.Reviewer}: Distribute review workload among other team contributors.");
                    analysisResult.RedistributionNotes.Add($"Distribute review requests away from overloaded reviewer {profile.Reviewer} ({profile.ReviewShare:P1} load).");
                }

                profile.RedistributionSuggestion = suggestion;
            }
            else if (profile.IsUnderutilized && profile.ReviewCount == 0 && totalReviews > 0)
            {
                profile.RedistributionSuggestion = $"{profile.Reviewer} currently conducts 0 reviews. Consider including them in code reviews to build subsystem knowledge and share team review load.";
            }
            else
            {
                profile.RedistributionSuggestion = "Review workload is within balanced team thresholds.";
            }
        }

        if (analysisResult.ActionableRecommendations.Count == 0)
        {
            analysisResult.ActionableRecommendations.Add("Review distribution is well-balanced across all contributors.");
            analysisResult.RedistributionNotes.Add("Review workload is evenly distributed.");
        }

        analysisResult.Profiles = profiles;

        EnrichResult(result, analysisResult);

        return analysisResult;
    }

    public List<AlternativeReviewerCandidate> FindAlternativeReviewers(string overloadedReviewer, string author, AnalysisResult result)
    {
        var candidates = new List<AlternativeReviewerCandidate>();
        if (result == null) return candidates;

        var pairs = result.CuratedReports?.ReviewCollaboration?.Pairs ?? new List<ReviewPair>();
        int totalReviews = pairs.Sum(p => p.PrCount);

        // Determine author's primary areas
        var authorAreas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (result.Contributors != null)
        {
            var authorMetric = result.Contributors.FirstOrDefault(c =>
                string.Equals(c.Name, author, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.Email, author, StringComparison.OrdinalIgnoreCase));

            if (authorMetric?.Areas != null)
            {
                foreach (var areaMetric in authorMetric.Areas.Where(a => a.Activity > 0).OrderByDescending(a => a.Activity))
                {
                    if (!string.IsNullOrWhiteSpace(areaMetric.Area))
                    {
                        authorAreas.Add(areaMetric.Area);
                    }
                }
            }
        }

        if (authorAreas.Count == 0 && result.Files != null)
        {
            foreach (var file in result.Files)
            {
                if (file.Contributors != null && file.Contributors.Any(c =>
                    string.Equals(c.Name, author, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c.Email, author, StringComparison.OrdinalIgnoreCase)))
                {
                    if (!string.IsNullOrEmpty(file.Area))
                    {
                        authorAreas.Add(file.Area);
                    }
                }
            }
        }

        // Find potential alternative reviewers from contributors
        if (result.Contributors != null)
        {
            foreach (var contributor in result.Contributors)
            {
                string name = !string.IsNullOrWhiteSpace(contributor.Name) ? contributor.Name : contributor.Email;
                if (string.IsNullOrWhiteSpace(name)) continue;

                if (string.Equals(name, overloadedReviewer, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(contributor.Email, overloadedReviewer, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, author, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(contributor.Email, author, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Compute candidate's current review share
                int candReviews = pairs.Where(p => string.Equals(p.Reviewer, name, StringComparison.OrdinalIgnoreCase) ||
                                                   (!string.IsNullOrEmpty(contributor.Email) && string.Equals(p.Reviewer, contributor.Email, StringComparison.OrdinalIgnoreCase)))
                                       .Sum(p => p.PrCount);
                double candReviewShare = totalReviews > 0 ? (double)candReviews / totalReviews : 0.0;

                // Evaluate area familiarity
                double matchingAreaActivity = 0.0;
                var matchingAreas = new List<string>();

                if (contributor.Areas != null)
                {
                    foreach (var areaName in authorAreas)
                    {
                        var areaMetric = contributor.Areas.FirstOrDefault(a => string.Equals(a.Area, areaName, StringComparison.OrdinalIgnoreCase));
                        if (areaMetric != null && areaMetric.Activity > 0)
                        {
                            matchingAreaActivity += areaMetric.Activity;
                            matchingAreas.Add(areaName);
                        }
                    }
                }

                string primaryArea = matchingAreas.Count > 0 ? string.Join(", ", matchingAreas) :
                    (contributor.Areas != null && contributor.Areas.Count > 0 ? contributor.Areas.OrderByDescending(a => a.Activity).First().Area : string.Empty);

                double familiarityScore = matchingAreaActivity > 0 ? matchingAreaActivity : (contributor.TotalActivity * 0.1);
                string reason = matchingAreas.Count > 0
                    ? $"Familiar with {string.Join(", ", matchingAreas)} ({matchingAreaActivity:F0} activity)"
                    : $"General codebase familiarity ({contributor.TotalActivity:F0} total activity)";

                candidates.Add(new AlternativeReviewerCandidate
                {
                    CandidateName = !string.IsNullOrWhiteSpace(contributor.Name) ? contributor.Name : contributor.Email,
                    CandidateEmail = contributor.Email,
                    TargetAuthor = author,
                    Area = primaryArea,
                    FamiliarityScore = Math.Round(familiarityScore, 2),
                    CurrentReviewShare = Math.Round(candReviewShare, 4),
                    Reason = reason
                });
            }
        }

        // Rank candidates: area familiarity first (descending), then lower review load (ascending), then activity
        return candidates
            .OrderByDescending(c => c.FamiliarityScore > 0)
            .ThenByDescending(c => c.FamiliarityScore)
            .ThenBy(c => c.CurrentReviewShare)
            .ThenBy(c => c.CandidateName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void EnrichReviewCollaboration(AnalysisResult result)
    {
        if (result == null) return;
        var analysis = AnalyzeReviewLoad(result);
        EnrichResult(result, analysis);
    }

    private static void EnrichResult(AnalysisResult result, ReviewLoadAnalysisResult analysisResult)
    {
        result.ReviewLoad = analysisResult;

        if (result.CuratedReports == null)
        {
            result.CuratedReports = new CuratedReports();
        }
        if (result.CuratedReports.ReviewCollaboration == null)
        {
            result.CuratedReports.ReviewCollaboration = new ReviewCollaborationMetric();
        }

        result.CuratedReports.ReviewCollaboration.RedistributionNotes = analysisResult.RedistributionNotes.ToList();
    }

    public static double CalculateGiniCoefficient(List<double> values)
    {
        if (values == null || values.Count <= 1) return 0.0;

        var sorted = values.OrderBy(v => v).ToList();
        double sum = sorted.Sum();
        if (sum <= 0.0) return 0.0;

        int n = sorted.Count;
        double weightedSum = 0.0;
        for (int i = 0; i < n; i++)
        {
            weightedSum += (i + 1) * sorted[i];
        }

        double gini = (2.0 * weightedSum) / (n * sum) - (double)(n + 1) / n;
        return Math.Round(Math.Clamp(gini, 0.0, 1.0), 4);
    }

    public string FormatSummary(ReviewLoadAnalysisResult result)
    {
        if (result == null || result.Profiles.Count == 0)
        {
            return "No review collaboration data available.\n";
        }

        var sb = new StringBuilder();
        sb.AppendLine("⚖️  Review Load Balance & Silo Analysis");
        sb.AppendLine("════════════════════════════════════════════════════════════");
        sb.AppendLine($"Total Reviews: {result.TotalReviews} | Concentration (Gini): {result.GiniCoefficient:F2} | Avg Load: {result.AverageReviewsPerReviewer:F1} reviews");
        sb.AppendLine($"Reviewers: {result.Profiles.Count} | Overloaded: {result.OverloadedReviewersCount}");
        sb.AppendLine();

        sb.AppendLine("Reviewer Breakdown:");
        foreach (var p in result.Profiles)
        {
            string statusTag = p.LoadClassification switch
            {
                ReviewLoadClassification.Overloaded => "[⚠️ Overloaded]",
                ReviewLoadClassification.Underutilized => "[ℹ️ Underutilized]",
                _ => "[✅ Balanced]"
            };

            sb.AppendLine($"  • {p.Reviewer,-18} {p.ReviewCount,4} reviews ({p.ReviewShare,6:P1} share, {p.ReviewDiversity} authors) {statusTag}");
        }

        var overloadedProfiles = result.Profiles.Where(p => p.IsOverloaded).ToList();
        if (overloadedProfiles.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Redistribution Suggestions:");
            foreach (var p in overloadedProfiles)
            {
                if (!string.IsNullOrEmpty(p.RedistributionSuggestion))
                {
                    sb.AppendLine($"  → {p.RedistributionSuggestion}");
                }
            }
        }

        if (result.ActionableRecommendations.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Actionable Recommendations:");
            for (int i = 0; i < result.ActionableRecommendations.Count; i++)
            {
                sb.AppendLine($"  [{i + 1}] {result.ActionableRecommendations[i]}");
            }
        }

        return sb.ToString();
    }

    public string FormatCliTable(ReviewLoadAnalysisResult result)
    {
        if (result == null || result.Profiles.Count == 0)
        {
            return "No review collaboration data available.\n";
        }

        int consoleWidth = ConsoleUtils.GetBoundedConsoleWidth(null);
        var visibleColumns = consoleWidth < 90
            ? new List<string> { "reviewer", "reviews", "share", "diversity", "classification" }
            : new List<string> { "reviewer", "reviews", "share", "authors", "diversity", "classification" };

        var table = new ConsoleTableBuilder()
            .WithConsoleWidth(consoleWidth)
            .WithVisibleColumns(visibleColumns)
            .WithBorders(true, true, true)
            .AddColumnEx("reviewer", align: "left", widthPolicy: WidthPolicy.Stretch, truncation: TruncationStyle.Standard, stretchRatio: 0.35, minWidth: 14)
            .AddColumnEx("reviews", width: 10, align: "right")
            .AddColumnEx("share", width: 10, align: "right")
            .AddColumnEx("authors", width: 20, align: "left", truncation: TruncationStyle.Standard)
            .AddColumnEx("diversity", width: 12, align: "right")
            .AddColumnEx("classification", width: 16, align: "center");

        foreach (var p in result.Profiles)
        {
            table.AddRow(new Dictionary<string, string>
            {
                { "reviewer", p.Reviewer },
                { "reviews", p.ReviewCount.ToString() },
                { "share", $"{p.ReviewShare:P1}" },
                { "authors", string.Join(", ", p.AuthorsReviewed) },
                { "diversity", p.ReviewDiversity.ToString() },
                { "classification", p.LoadClassification.ToString() }
            });
        }

        return table.Render();
    }

    public string FormatMarkdown(ReviewLoadAnalysisResult result)
    {
        if (result == null || result.Profiles.Count == 0)
        {
            return "## ⚖️ Review Load Balancer & Silo Mitigation\n\nNo review collaboration data available.\n";
        }

        var sb = new StringBuilder();
        sb.AppendLine("## ⚖️ Review Load Balancer & Silo Mitigation");
        sb.AppendLine();
        sb.AppendLine($"**Total Reviews Analyzed:** {result.TotalReviews} | **Gini Concentration:** {result.GiniCoefficient:F2} | **Overloaded Reviewers:** {result.OverloadedReviewersCount}");
        sb.AppendLine();

        sb.AppendLine("| Reviewer | Reviews | Share | Authors Reviewed | Diversity | Status |");
        sb.AppendLine("| :--- | :---: | :---: | :--- | :---: | :--- |");

        foreach (var p in result.Profiles)
        {
            string statusEmoji = p.LoadClassification switch
            {
                ReviewLoadClassification.Overloaded => "⚠️ Overloaded",
                ReviewLoadClassification.Underutilized => "ℹ️ Underutilized",
                _ => "✅ Balanced"
            };

            string authors = p.AuthorsReviewed.Count > 0 ? string.Join(", ", p.AuthorsReviewed) : "—";
            sb.AppendLine($"| **{p.Reviewer}** | {p.ReviewCount} | {p.ReviewShare:P1} | {authors} | {p.ReviewDiversity} | {statusEmoji} |");
        }

        sb.AppendLine();

        if (result.ActionableRecommendations.Count > 0)
        {
            sb.AppendLine("### 🎯 Redistribution Recommendations");
            sb.AppendLine();
            foreach (var rec in result.ActionableRecommendations)
            {
                sb.AppendLine($"- {rec}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
