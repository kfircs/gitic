using System;
using System.Collections.Generic;
using System.Linq;

namespace Gitic
{
    public interface IResultAnonymizer
    {
        AnalysisResult Anonymize(AnalysisResult result);
    }

    public class ResultAnonymizer : IResultAnonymizer
    {
        private readonly Dictionary<string, GitIdentity> _humanIdentities = new();
        private readonly Dictionary<string, GitIdentity> _automationIdentities = new();

        public ResultAnonymizer()
        {
        }

        private GitIdentity AnonymizeHuman(string name, string email)
        {
            string key = IdentityUtils.IdentityKey(new GitIdentity { Name = name, Email = email });
            if (_humanIdentities.TryGetValue(key, out var existing))
            {
                return existing;
            }
            int index = _humanIdentities.Count + 1;
            var identity = new GitIdentity
            {
                Name = $"Contributor {index}",
                Email = $"contributor-{index}@anonymous.local"
            };
            _humanIdentities[key] = identity;
            return identity;
        }

        private GitIdentity AnonymizeAutomation(string name, string email)
        {
            string key = IdentityUtils.IdentityKey(new GitIdentity { Name = name, Email = email });
            if (_automationIdentities.TryGetValue(key, out var existing))
            {
                return existing;
            }
            int index = _automationIdentities.Count + 1;
            var identity = new GitIdentity
            {
                Name = $"Automation {index}",
                Email = $"automation-{index}@anonymous.local"
            };
            _automationIdentities[key] = identity;
            return identity;
        }

        private ContributorShare AnonymizeHumanContributorShare(ContributorShare contributor)
        {
            var identity = AnonymizeHuman(contributor.Name, contributor.Email);
            return new ContributorShare
            {
                Name = identity.Name,
                Email = identity.Email,
                Activity = contributor.Activity,
                ActivityShare = contributor.ActivityShare
            };
        }

        private ContributorMetric AnonymizeHumanContributorMetric(ContributorMetric contributor)
        {
            var identity = AnonymizeHuman(contributor.Name, contributor.Email);
            return new ContributorMetric
            {
                Name = identity.Name,
                Email = identity.Email,
                TotalActivity = contributor.TotalActivity,
                Areas = contributor.Areas.Select(area => new ContributorAreaMetric
                {
                    Area = area.Area,
                    Activity = area.Activity,
                    ActivityShare = area.ActivityShare,
                    FamiliarityScore = area.FamiliarityScore
                }).ToList()
            };
        }

        public AnalysisResult Anonymize(AnalysisResult result)
        {
            var clonedResult = new AnalysisResult
            {
                SchemaVersion = result.SchemaVersion,
                Tool = result.Tool,
                Analysis = new AnalysisMetadata
                {
                    RepoRoot = result.Analysis.RepoRoot,
                    Command = result.Analysis.Command,
                    GeneratedAt = result.Analysis.GeneratedAt,
                    CommitCount = result.Analysis.CommitCount,
                    IncludedFileChangeCount = result.Analysis.IncludedFileChangeCount
                },
                Settings = new AnalysisSettings
                {
                    Json = result.Settings.Json,
                    AllTime = result.Settings.AllTime,
                    Since = result.Settings.Since,
                    IncludeMerges = result.Settings.IncludeMerges,
                    IncludeDeleted = result.Settings.IncludeDeleted,
                    MergeByEmail = result.Settings.MergeByEmail,
                    Path = result.Settings.Path,
                    Anonymize = result.Settings.Anonymize,
                    Depth = result.Settings.Depth
                },
                Exclusions = result.Exclusions.Select(e => new ExclusionSummary
                {
                    Category = e.Category,
                    Pattern = e.Pattern,
                    Count = e.Count
                }).ToList(),
                Warnings = result.Warnings.ToList(),
                Configuration = new AnalysisConfiguration
                {
                    Scoring = new ScoringConfiguration
                    {
                        Attention = new AttentionWeights
                        {
                            Churn = result.Configuration.Scoring.Attention.Churn,
                            Recency = result.Configuration.Scoring.Attention.Recency,
                            ContributorSpread = result.Configuration.Scoring.Attention.ContributorSpread,
                            LowFamiliarityConcentration = result.Configuration.Scoring.Attention.LowFamiliarityConcentration
                        }
                    },
                    ConfiguredAliasCount = result.Configuration.ConfiguredAliasCount,
                    ConfiguredBotCount = result.Configuration.ConfiguredBotCount,
                    ConfiguredExcludeCount = result.Configuration.ConfiguredExcludeCount,
                    ConfiguredAreaCount = result.Configuration.ConfiguredAreaCount,
                    Identity = new IdentityConfigInfo
                    {
                        MergeOnEmail = result.Configuration.Identity.MergeOnEmail
                    }
                },
                TemporalCoupling = result.TemporalCoupling?.Select(tc => new TemporalCoupling
                {
                    FileA = tc.FileA,
                    FileB = tc.FileB,
                    SharedCommits = tc.SharedCommits,
                    CouplingDegree = tc.CouplingDegree
                }).ToList(),
                LeadTimes = result.LeadTimes == null ? null : new LeadTimesInfo
                {
                    AverageLeadTimeHours = result.LeadTimes.AverageLeadTimeHours,
                    Merges = result.LeadTimes.Merges.Select(m => new MergeLeadTimeRecord
                    {
                        Hash = m.Hash,
                        Message = m.Message,
                        Author = m.Author,
                        Date = m.Date,
                        LeadTimeHours = m.LeadTimeHours,
                        FileCount = m.FileCount
                    }).ToList()
                }
            };

            // Map and clone contributors
            clonedResult.Contributors = result.Contributors.Select(contributor =>
                AnonymizeHumanContributorMetric(contributor)
            ).ToList();

            // Map and clone files
            clonedResult.Files = result.Files.Select(file => new FileMetric
            {
                Path = file.Path,
                Area = file.Area,
                Touches = file.Touches,
                Added = file.Added,
                Deleted = file.Deleted,
                Churn = file.Churn,
                LastTouched = file.LastTouched,
                ContributorCount = file.ContributorCount,
                Contributors = file.Contributors.Select(contributor =>
                    AnonymizeHumanContributorShare(contributor)
                ).ToList(),
                HeatScore = file.HeatScore,
                AttentionScore = file.AttentionScore,
                ScoreBreakdown = new ScoreBreakdown
                {
                    Touches = file.ScoreBreakdown.Touches,
                    Churn = file.ScoreBreakdown.Churn,
                    Recency = file.ScoreBreakdown.Recency,
                    ContributorSpread = file.ScoreBreakdown.ContributorSpread,
                    LowFamiliarityConcentration = file.ScoreBreakdown.LowFamiliarityConcentration
                },
                InnerSymbols = file.InnerSymbols?.Select(s => new InnerSymbolMetric
                {
                    Name = s.Name,
                    Touches = s.Touches
                }).ToList(),
                DebtVolatility = file.DebtVolatility,
                ReworkRate = file.ReworkRate,
                CoordinationOverlap = file.CoordinationOverlap,
                KnowledgeSilo = file.KnowledgeSilo == null ? null : new KnowledgeSiloMetric
                {
                    TruckFactor = file.KnowledgeSilo.TruckFactor,
                    TopOwnerShare = file.KnowledgeSilo.TopOwnerShare,
                    IsSilo = file.KnowledgeSilo.IsSilo,
                    Abandoned = file.KnowledgeSilo.Abandoned
                },
                Size = file.Size,
                Width = file.Width,
                Lines = file.Lines
            }).ToList();

            // Map and clone areas
            clonedResult.Areas = result.Areas.Select(area => new AreaMetric
            {
                Area = area.Area,
                Touches = area.Touches,
                Added = area.Added,
                Deleted = area.Deleted,
                Churn = area.Churn,
                FileCount = area.FileCount,
                LastTouched = area.LastTouched,
                ContributorCount = area.ContributorCount,
                Contributors = area.Contributors.Select(contributor =>
                    AnonymizeHumanContributorShare(contributor)
                ).ToList(),
                HeatScore = area.HeatScore,
                AttentionScore = area.AttentionScore,
                ScoreBreakdown = new ScoreBreakdown
                {
                    Touches = area.ScoreBreakdown.Touches,
                    Churn = area.ScoreBreakdown.Churn,
                    Recency = area.ScoreBreakdown.Recency,
                    ContributorSpread = area.ScoreBreakdown.ContributorSpread,
                    LowFamiliarityConcentration = area.ScoreBreakdown.LowFamiliarityConcentration
                },
                ReworkRate = area.ReworkRate
            }).ToList();

            // Map and clone automation
            clonedResult.Automation = result.Automation.Select(automation =>
            {
                var identity = AnonymizeAutomation(automation.Name, automation.Email);
                return new AutomationMetric
                {
                    Name = identity.Name,
                    Email = identity.Email,
                    TotalActivity = automation.TotalActivity,
                    Areas = automation.Areas.Select(area => new ContributorAreaMetric
                    {
                        Area = area.Area,
                        Activity = area.Activity,
                        ActivityShare = area.ActivityShare,
                        FamiliarityScore = area.FamiliarityScore
                    }).ToList()
                };
            }).ToList();

            return clonedResult;
        }
    }
}
