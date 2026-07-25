using System;
using System.Collections.Generic;
using System.Linq;

namespace Gitic
{
    public static class MetricProcessors
    {
        private const double MsPerDay = 86400000.0;
        private const double ActiveContributorDays = 90.0;
        private const double ActiveContributorLookbackMs = ActiveContributorDays * MsPerDay;
        private const double TopQuarterFraction = 0.25;

        public static List<ContributorMetric> RenderContributors(List<ContributorAccumulator> items)
        {
            return RenderContributorAccumulatorList(items);
        }

        public static List<AutomationMetric> RenderAutomation(List<ContributorAccumulator> items)
        {
            return RenderContributorAccumulatorList(items)
                .Select(m => new AutomationMetric
                {
                    Name = m.Name,
                    Email = m.Email,
                    TotalActivity = m.TotalActivity,
                    Areas = m.Areas
                }).ToList();
        }

        private static List<ContributorMetric> RenderContributorAccumulatorList(List<ContributorAccumulator> items)
        {
            return items
                .Select(item => new ContributorMetric
                {
                    Name = item.Identity.Name,
                    Email = item.Identity.Email,
                    TotalActivity = ScoringUtils.RoundRatio(item.TotalActivity),
                    Areas = ContributorAreas(item)
                })
                .OrderByDescending(c => c.TotalActivity)
                .ToList();
        }

        public static List<ContributorAreaMetric> ContributorAreas(ContributorAccumulator item)
        {
            return item.Areas
                .Select(kv =>
                {
                    string area = kv.Key;
                    double activity = kv.Value;
                    double total = item.TotalActivity;
                    double share = total > 0 ? activity / total : 0.0;
                    return new ContributorAreaMetric
                    {
                        Area = area,
                        Activity = ScoringUtils.RoundRatio(activity),
                        ActivityShare = ScoringUtils.RoundRatio(share),
                        FamiliarityScore = ScoringUtils.RoundRatio(Math.Sqrt(share) * 100.0)
                    };
                })
                .OrderByDescending(c => c.Activity)
                .ToList();
        }

        public static List<FileMetric> SortFilesForCommand(List<FileMetric> files, AnalysisCommand command)
        {
            // scoreKey = command === "areas" ? "heat_score" : "attention_score";
            if (command == AnalysisCommand.Areas)
            {
                return files.OrderByDescending(f => f.HeatScore).ToList();
            }
            return files.OrderByDescending(f => f.AttentionScore).ToList();
        }

        public static List<AreaMetric> SortAreasForCommand(List<AreaMetric> areas, AnalysisCommand command)
        {
            // scoreKey = command === "hotspots" ? "attention_score" : "heat_score";
            if (command == AnalysisCommand.Hotspots)
            {
                return areas.OrderByDescending(a => a.AttentionScore).ToList();
            }
            return areas.OrderByDescending(a => a.HeatScore).ToList();
        }

        public static List<ContributorMetric> SortContributorsForCommand(
            List<ContributorMetric> contributors,
            AnalysisCommand command)
        {
            return contributors.OrderByDescending(c => c.TotalActivity).ToList();
        }

        public static HashSet<string> GetActiveContributorKeys(List<GitCommitRecord> commits)
        {
            var activeKeys = new HashSet<string>();
            if (commits.Count == 0)
            {
                return activeKeys;
            }

            long maxTimestamp = commits.Max(c => c.Timestamp);
            double ninetyDaysAgo = maxTimestamp - ActiveContributorLookbackMs;

            var sortedCommits = commits.OrderByDescending(c => c.Timestamp).ToList();
            int topQuarterCount = Math.Max(1, (int)Math.Floor(commits.Count * TopQuarterFraction));

            for (int i = 0; i < sortedCommits.Count; i++)
            {
                var commit = sortedCommits[i];
                if (i < topQuarterCount || commit.Timestamp >= ninetyDaysAgo)
                {
                    activeKeys.Add(IdentityUtils.IdentityKey(commit.Author));
                    foreach (var co in commit.CoAuthors)
                    {
                        activeKeys.Add(IdentityUtils.IdentityKey(co));
                    }
                }
            }
            return activeKeys;
        }
    }

    public interface IMetricProcessor
    {
        HashSet<string> GetActiveContributorKeys(List<GitCommitRecord> commits);
        List<ContributorMetric> RenderContributors(List<ContributorAccumulator> items);
        List<AutomationMetric> RenderAutomation(List<ContributorAccumulator> items);
        List<AreaMetric> SortAreasForCommand(List<AreaMetric> areas, AnalysisCommand command);
        List<FileMetric> SortFilesForCommand(List<FileMetric> files, AnalysisCommand command);
        List<ContributorMetric> SortContributorsForCommand(List<ContributorMetric> contributors, AnalysisCommand command);
    }

    public class MetricProcessorImpl : IMetricProcessor
    {
        public HashSet<string> GetActiveContributorKeys(List<GitCommitRecord> commits)
        {
            return MetricProcessors.GetActiveContributorKeys(commits);
        }

        public List<ContributorMetric> RenderContributors(List<ContributorAccumulator> items)
        {
            return MetricProcessors.RenderContributors(items);
        }

        public List<AutomationMetric> RenderAutomation(List<ContributorAccumulator> items)
        {
            return MetricProcessors.RenderAutomation(items);
        }

        public List<AreaMetric> SortAreasForCommand(List<AreaMetric> areas, AnalysisCommand command)
        {
            return MetricProcessors.SortAreasForCommand(areas, command);
        }

        public List<FileMetric> SortFilesForCommand(List<FileMetric> files, AnalysisCommand command)
        {
            return MetricProcessors.SortFilesForCommand(files, command);
        }

        public List<ContributorMetric> SortContributorsForCommand(List<ContributorMetric> contributors, AnalysisCommand command)
        {
            return MetricProcessors.SortContributorsForCommand(contributors, command);
        }
    }
}
