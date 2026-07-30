using System;
using System.Collections.Generic;
using System.Linq;

namespace Gitic
{
    public interface ICuratedReportsEngine
    {
        CuratedReports Calculate(List<GitCommitRecord> commits, List<FileMetric> files, LeadTimesInfo? leadTimes);
    }

    public class CuratedReportsEngine : ICuratedReportsEngine
    {
        public CuratedReports Calculate(List<GitCommitRecord> commits, List<FileMetric> files, LeadTimesInfo? leadTimes)
        {
            var reports = new CuratedReports();

            CalculateWorkClassification(commits, reports.WorkClassification);
            CalculateOnboarding(commits, reports.Onboarding);
            CalculateReviewCollaboration(commits, reports.ReviewCollaboration);
            CalculateCodeRot(files, reports.CodeRot);
            CalculateAiCodeStrain(commits, reports.AiCodeStrain);

            return reports;
        }

        private void CalculateWorkClassification(List<GitCommitRecord> commits, WorkClassificationMetrics report)
        {
            foreach (var c in commits)
            {
                string msg = c.Message.ToLowerInvariant();
                if (msg.Contains("feat") || msg.Contains("feature") || msg.Contains("add") || msg.Contains("new"))
                    report.Features++;
                else if (msg.Contains("fix") || msg.Contains("bug") || msg.Contains("resolve"))
                    report.Bugs++;
                else if (msg.Contains("refactor") || msg.Contains("debt") || msg.Contains("cleanup") || msg.Contains("tech debt"))
                    report.TechnicalDebt++;
                else if (msg.Contains("chore") || msg.Contains("docs") || msg.Contains("test") || msg.Contains("build") || msg.Contains("ci"))
                    report.Chores++;
                else
                    report.Unclassified++;
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
                double days = (item.LastCommit.Timestamp - item.FirstCommit.Timestamp) / 86400000.0;
                onboarding.Add(new DeveloperOnboardingMetric
                {
                    Developer = item.Author,
                    FirstCommitDate = item.FirstCommit.Date,
                    DaysActive = (int)Math.Max(1, Math.Round(days))
                });
            }
        }

        private void CalculateReviewCollaboration(List<GitCommitRecord> commits, ReviewCollaborationMetric report)
        {
            var pairs = new Dictionary<string, int>();

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
                report.Pairs.Add(new ReviewPair
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

        private void CalculateCodeRot(List<FileMetric> files, CodeRotMetric report)
        {
            long oneYearAgoMs = DateTimeOffset.UtcNow.AddYears(-1).ToUnixTimeMilliseconds();

            foreach (var f in files)
            {
                if (!string.IsNullOrEmpty(f.LastTouched))
                {
                    if (DateTimeOffset.TryParse(f.LastTouched, out var lastTouchedDt))
                    {
                        if (lastTouchedDt.ToUnixTimeMilliseconds() < oneYearAgoMs)
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
}
