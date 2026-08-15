using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Gitic;

public interface ICuratedReportsEngine
{
    CuratedReports Calculate(List<GitCommitRecord> commits, List<FileMetric> files, LeadTimesInfo? leadTimes);
}

public class CuratedReportsEngine : ICuratedReportsEngine
{
    private const double MsPerDay = 86400000.0;

    private readonly ICommitClassifier _classifier;

    private static readonly List<ClassificationRule> CuratedRules = new List<ClassificationRule>
    {
        new ClassificationRule(
            "bugfix",
            new Regex(@"(?:bug|fix|revert|issue|crash|error|prevent|problem|fail|correct|leak)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"^(?:fix|revert)(?:\(.+\))?:", RegexOptions.IgnoreCase | RegexOptions.Compiled)
        ),
        new ClassificationRule(
            "feature",
            new Regex(@"(?:feat|feature|add|implement|introduce)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"^feat(?:\(.+\))?:", RegexOptions.IgnoreCase | RegexOptions.Compiled)
        ),
        new ClassificationRule(
            "refactor",
            new Regex(@"(?:refactor|debt|cleanup|tech debt)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"^refactor(?:\(.+\))?:", RegexOptions.IgnoreCase | RegexOptions.Compiled)
        ),
        new ClassificationRule(
            "chore",
            new Regex(@"(?:chore|docs|test|build|ci)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"^(?:chore|docs|test|build|ci)(?:\(.+\))?:", RegexOptions.IgnoreCase | RegexOptions.Compiled)
        )
    };

    public CuratedReportsEngine(ICommitClassifier? classifier = null)
    {
        _classifier = classifier ?? new CommitClassifier(CuratedRules);
    }

    public CuratedReports Calculate(List<GitCommitRecord> commits, List<FileMetric> files, LeadTimesInfo? leadTimes)
    {
        commits ??= new List<GitCommitRecord>();
        CuratedReports reports = new();

        int thresholdDays = DetermineThresholdDays(commits);
        DateTimeOffset referenceDate = DetermineReferenceDate(commits);

        CalculateWorkClassification(commits, files, reports.WorkClassification);
        CalculateOnboarding(commits, reports.Onboarding);
        CalculateReviewCollaboration(commits, reports.ReviewCollaboration);
        CalculateCodeRot(files, reports.CodeRot, thresholdDays, referenceDate);
        CalculateAiCodeStrain(commits, reports.AiCodeStrain);

        return reports;
    }

    private int DetermineThresholdDays(List<GitCommitRecord> commits)
    {
        if (commits == null || commits.Count == 0)
        {
            return 365;
        }

        long minTimestamp = commits.Min(c => c.Timestamp);
        long maxTimestamp = commits.Max(c => c.Timestamp);
        double spanDays = (maxTimestamp - minTimestamp) / MsPerDay;

        if (spanDays < 90) // under 3 months
        {
            return 14; // 14 days
        }
        if (spanDays < 365) // under 1 year
        {
            return 90; // 90 days (approx 3 months)
        }

        return 365;
    }

    private DateTimeOffset DetermineReferenceDate(List<GitCommitRecord> commits)
    {
        if (commits == null || commits.Count == 0)
        {
            return DateTimeOffset.UtcNow;
        }

        DateTimeOffset maxDate = DateTimeOffset.MinValue;
        foreach (var c in commits)
        {
            DateTimeOffset commitDate = DateTimeOffset.MinValue;
            if (!string.IsNullOrEmpty(c.Date) && DateTimeOffset.TryParse(c.Date, out var parsedDt))
            {
                commitDate = parsedDt;
            }
            else if (c.Timestamp > 0)
            {
                commitDate = DateTimeOffset.FromUnixTimeMilliseconds(c.Timestamp);
            }

            if (commitDate > maxDate)
            {
                maxDate = commitDate;
            }
        }

        return maxDate > DateTimeOffset.MinValue ? maxDate : DateTimeOffset.UtcNow;
    }

    private void CalculateWorkClassification(List<GitCommitRecord> commits, List<FileMetric> files, WorkClassificationMetrics report)
    {
        var fileLookup = files.ToDictionary(
            f => PathUtils.NormalizeGitPath(f.Path),
            f => f,
            StringComparer.OrdinalIgnoreCase
        );

        foreach (var c in commits)
        {
            var category = _classifier.Classify(c.Message);
            IncrementWorkClassification(report, category);

            // Also associate this commit classification with each of the touched files
            foreach (var gitFile in c.Files)
            {
                string normPath = PathUtils.NormalizeGitPath(gitFile.Path);
                if (fileLookup.TryGetValue(normPath, out var fileMetric))
                {
                    if (fileMetric.WorkClassification == null)
                    {
                        fileMetric.WorkClassification = new WorkClassificationMetrics();
                    }
                    IncrementWorkClassification(fileMetric.WorkClassification, category);
                }
            }
        }
    }

    private static void IncrementWorkClassification(WorkClassificationMetrics metrics, string category)
    {
        switch (category)
        {
            case "feature":
                metrics.Features++;
                break;
            case "bugfix":
                metrics.Bugs++;
                break;
            case "refactor":
                metrics.TechnicalDebt++;
                break;
            case "chore":
                metrics.Chores++;
                break;
            default:
                metrics.Unclassified++;
                break;
        }
    }

    private void CalculateOnboarding(List<GitCommitRecord> commits, List<DeveloperOnboardingMetric> onboarding)
    {
        var firstCommits = commits
            .GroupBy(c => c.Author.Name)
            .Select(g => new
            {
                Author = g.Key,
                FirstCommit = g.OrderBy(c => c.Timestamp).First(),
                LastCommit = g.OrderByDescending(c => c.Timestamp).First()
            })
            .OrderByDescending(x => x.FirstCommit.Timestamp)
            .ToList();

        foreach (var item in firstCommits)
        {
            double days = (item.LastCommit.Timestamp - item.FirstCommit.Timestamp) / MsPerDay;
            onboarding.Add(new()
            {
                Developer = item.Author,
                FirstCommitDate = item.FirstCommit.Date,
                DaysActive = (int)Math.Max(1, Math.Round(days))
            });
        }
    }

    private void CalculateReviewCollaboration(List<GitCommitRecord> commits, ReviewCollaborationMetric report)
    {
        Dictionary<string, int> pairs = new();

        foreach (var c in commits)
        {
            // We use CoAuthors as a proxy for review collaboration
            if (c.CoAuthors != null && c.CoAuthors.Count > 0)
            {
                foreach (var coAuthor in c.CoAuthors)
                {
                    if (c.Author.Name != coAuthor.Name)
                    {
                        string pairKey = $"{c.Author.Name}|{coAuthor.Name}";
                        pairs.TryGetValue(pairKey, out int count);
                        pairs[pairKey] = count + 1;
                    }
                }
            }
        }

        foreach (var kvp in pairs.OrderByDescending(x => x.Value).Take(20))
        {
            var split = kvp.Key.Split('|');
            report.Pairs.Add(new()
            {
                Author = split[0],
                Reviewer = split[1],
                PrCount = kvp.Value
            });
        }

        // Simple silo calculation: people who only review 1 person
        int silos = pairs.GroupBy(p => p.Key.Split('|')[1]).Count(g => g.Count() == 1);
        report.ReviewerSilos = silos;
    }

    private void CalculateCodeRot(List<FileMetric> files, CodeRotMetric report, int thresholdDays, DateTimeOffset referenceDate)
    {
        report.ThresholdDays = thresholdDays;
        long thresholdMs = referenceDate.AddDays(-thresholdDays).ToUnixTimeMilliseconds();

        foreach (var f in files)
        {
            if (!string.IsNullOrEmpty(f.LastTouched))
            {
                if (DateTimeOffset.TryParse(f.LastTouched, out var lastTouchedDt))
                {
                    if (lastTouchedDt.ToUnixTimeMilliseconds() < thresholdMs)
                    {
                        report.ZombieFileCount++;
                        report.ZombieLines += f.Lines ?? 0;
                    }
                }
            }
        }
    }

    private void CalculateAiCodeStrain(List<GitCommitRecord> commits, AiCodeStrainMetric report)
    {
        foreach (var c in commits)
        {
            // Proxy for AI-generated code: huge commits without much time/review structure
            if (c.Files.Count > 20)
            {
                report.HighVolumeCommits++;
            }
        }

        // Warning if more than 5% of commits are massive
        if (commits.Count > 0 && ((double)report.HighVolumeCommits / commits.Count) > 0.05)
        {
            report.ReviewVelocityWarning = true;
        }
    }
}
