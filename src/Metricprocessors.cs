using System;
using System.Collections.Generic;
using System.Linq;

namespace Gitic
{
    public interface IMetricProcessorService
    {
        List<ContributorMetric> RenderContributors(List<ContributorAccumulator> items);
        List<AutomationMetric> RenderAutomation(List<ContributorAccumulator> items);
        List<FileMetric> SortFilesForCommand(List<FileMetric> files, AnalysisCommand command);
        List<AreaMetric> SortAreasForCommand(List<AreaMetric> areas, AnalysisCommand command);
        List<ContributorMetric> SortContributorsForCommand(List<ContributorMetric> contributors, AnalysisCommand command);
        void SortMetrics(AnalysisResult result, AnalysisCommand command);
        HashSet<string> GetActiveContributorKeys(List<GitCommitRecord> commits);
    }

    public class MetricProcessorService : IMetricProcessorService
    {
        private const long MsPerDay = 86400000L;
        private const long ActiveContributorDays = 90L;
        private const long ActiveContributorLookbackMs = ActiveContributorDays * MsPerDay;
        private const double TopQuarterFraction = 0.25;

        public List<ContributorMetric> RenderContributors(List<ContributorAccumulator> items)
        {
            return RenderContributorAccumulatorList(items);
        }

        public List<AutomationMetric> RenderAutomation(List<ContributorAccumulator> items)
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

        private List<ContributorMetric> RenderContributorAccumulatorList(List<ContributorAccumulator> items)
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

        private List<ContributorAreaMetric> ContributorAreas(ContributorAccumulator item)
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

        public List<FileMetric> SortFilesForCommand(List<FileMetric> files, AnalysisCommand command)
        {
            if (command == AnalysisCommand.Areas)
            {
                return files.OrderByDescending(f => f.HeatScore).ToList();
            }
            return files
                .OrderByDescending(f => f.Lines ?? 0)
                .ThenByDescending(f => f.Size ?? 0)
                .ThenByDescending(f => f.AttentionScore)
                .ToList();
        }

        public List<AreaMetric> SortAreasForCommand(List<AreaMetric> areas, AnalysisCommand command)
        {
            if (command == AnalysisCommand.Hotspots)
            {
                return areas.OrderByDescending(a => a.AttentionScore).ToList();
            }
            return areas.OrderByDescending(a => a.HeatScore).ToList();
        }

        public List<ContributorMetric> SortContributorsForCommand(
            List<ContributorMetric> contributors,
            AnalysisCommand command)
        {
            return contributors.OrderByDescending(c => c.TotalActivity).ToList();
        }

        public void SortMetrics(AnalysisResult result, AnalysisCommand command)
        {
            if (result == null) return;
            result.Areas = SortAreasForCommand(result.Areas, command);
            
            if (command == AnalysisCommand.Hotspots && result.Settings != null && !string.IsNullOrEmpty(result.Settings.Sort))
            {
                string sortField = result.Settings.Sort.ToLower();
                if (sortField == "attention")
                {
                    result.Files = result.Files.OrderByDescending(f => f.AttentionScore).ToList();
                }
                else if (sortField == "heat")
                {
                    result.Files = result.Files.OrderByDescending(f => f.HeatScore).ToList();
                }
                else if (sortField == "churn")
                {
                    result.Files = result.Files.OrderByDescending(f => f.Churn).ToList();
                }
                else if (sortField == "rework")
                {
                    result.Files = result.Files.OrderByDescending(f => f.ReworkRate ?? 0).ToList();
                }
                else if (sortField == "coordination")
                {
                    result.Files = result.Files.OrderByDescending(f => f.CoordinationOverlap ?? 0).ToList();
                }
                else if (sortField == "lines")
                {
                    result.Files = result.Files.OrderByDescending(f => f.Lines ?? 0).ToList();
                }
                else
                {
                    result.Files = SortFilesForCommand(result.Files, command);
                }
            }
            else
            {
                result.Files = SortFilesForCommand(result.Files, command);
            }

            result.Contributors = SortContributorsForCommand(result.Contributors, command);
        }

        public HashSet<string> GetActiveContributorKeys(List<GitCommitRecord> commits)
        {
            var activeKeys = new HashSet<string>();
            if (commits.Count == 0)
            {
                return activeKeys;
            }

            long maxTimestamp = commits.Max(c => c.Timestamp);
            long ninetyDaysAgo = maxTimestamp - ActiveContributorLookbackMs;

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
}
