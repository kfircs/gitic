using System;
using System.Collections.Generic;
using System.Linq;

namespace Gitic
{
    public interface IResultAnonymizer
    {
        AnalysisResult Anonymize(AnalysisResult result);
    }

    internal class AnonymizationSession
    {
        internal Dictionary<string, GitIdentity> HumanIdentities { get; } = new();
        internal Dictionary<string, GitIdentity> AutomationIdentities { get; } = new();
    }

    public class ResultAnonymizer : IResultAnonymizer
    {
        private const string ContributorNamePrefix = "Contributor";
        private const string ContributorEmailPrefix = "contributor";
        private const string AutomationNamePrefix = "Automation";
        private const string AutomationEmailPrefix = "automation";

        public ResultAnonymizer()
        {
        }

        private GitIdentity GetOrAnonymize(string name, string email, Dictionary<string, GitIdentity> cache, string namePrefix, string emailPrefix)
        {
            string key = IdentityUtils.IdentityKey(name, email);
            if (cache.TryGetValue(key, out var existing))
            {
                return existing;
            }
            int index = cache.Count + 1;
            var identity = new GitIdentity
            {
                Name = $"{namePrefix} {index}",
                Email = $"{emailPrefix}-{index}@anonymous.local"
            };
            cache[key] = identity;
            return identity;
        }

        private GitIdentity AnonymizeHuman(string name, string email, AnonymizationSession session)
        {
            return GetOrAnonymize(name, email, session.HumanIdentities, ContributorNamePrefix, ContributorEmailPrefix);
        }

        private GitIdentity AnonymizeAutomation(string name, string email, AnonymizationSession session)
        {
            return GetOrAnonymize(name, email, session.AutomationIdentities, AutomationNamePrefix, AutomationEmailPrefix);
        }

        private ContributorShare AnonymizeHumanContributorShare(ContributorShare contributor, AnonymizationSession session)
        {
            return MapToContributorShare(contributor, AnonymizeHuman(contributor.Name, contributor.Email, session));
        }

        private ContributorMetric AnonymizeHumanContributorMetric(ContributorMetric contributor, AnonymizationSession session)
        {
            return MapToContributorMetric(contributor, AnonymizeHuman(contributor.Name, contributor.Email, session));
        }

        private ContributorShare MapToContributorShare(ContributorShare source, GitIdentity identity)
        {
            return new ContributorShare
            {
                Name = identity.Name,
                Email = identity.Email,
                Activity = source.Activity,
                ActivityShare = source.ActivityShare
            };
        }

        private ContributorMetric MapToContributorMetric(ContributorMetric source, GitIdentity identity)
        {
            return new ContributorMetric
            {
                Name = identity.Name,
                Email = identity.Email,
                TotalActivity = source.TotalActivity,
                Areas = CloneAreas(source.Areas)
            };
        }

        private AutomationMetric MapToAutomationMetric(AutomationMetric source, GitIdentity identity)
        {
            return new AutomationMetric
            {
                Name = identity.Name,
                Email = identity.Email,
                TotalActivity = source.TotalActivity,
                Areas = CloneAreas(source.Areas)
            };
        }

        private List<ContributorAreaMetric> CloneAreas(IEnumerable<ContributorAreaMetric> areas)
        {
            return areas.Select(area => new ContributorAreaMetric
            {
                Area = area.Area,
                Activity = area.Activity,
                ActivityShare = area.ActivityShare,
                FamiliarityScore = area.FamiliarityScore
            }).ToList();
        }

        private FileMetric AnonymizeFileMetric(FileMetric file, AnonymizationSession session)
        {
            return new FileMetric
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
                    AnonymizeHumanContributorShare(contributor, session)
                ).ToList(),
                HeatScore = file.HeatScore,
                AttentionScore = file.AttentionScore,
                ScoreBreakdown = file.ScoreBreakdown.Clone(),
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
            };
        }

        private AreaMetric AnonymizeAreaMetric(AreaMetric area, AnonymizationSession session)
        {
            return new AreaMetric
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
                    AnonymizeHumanContributorShare(contributor, session)
                ).ToList(),
                HeatScore = area.HeatScore,
                AttentionScore = area.AttentionScore,
                ScoreBreakdown = area.ScoreBreakdown.Clone(),
                ReworkRate = area.ReworkRate
            };
        }

        private AnalysisMetadata CloneAnalysisMetadata(AnalysisMetadata source)
        {
            return new AnalysisMetadata
            {
                RepoRoot = source.RepoRoot,
                Command = source.Command,
                GeneratedAt = source.GeneratedAt,
                CommitCount = source.CommitCount,
                IncludedFileChangeCount = source.IncludedFileChangeCount
            };
        }

        private ScoringConfiguration CloneScoringConfiguration(ScoringConfiguration source)
        {
            return new ScoringConfiguration
            {
                Attention = new AttentionWeights
                {
                    Churn = source.Attention.Churn,
                    Recency = source.Attention.Recency,
                    ContributorSpread = source.Attention.ContributorSpread,
                    LowFamiliarityConcentration = source.Attention.LowFamiliarityConcentration
                }
            };
        }

        private ExclusionSummary CloneExclusionSummary(ExclusionSummary source)
        {
            return new ExclusionSummary
            {
                Category = source.Category,
                Pattern = source.Pattern,
                Count = source.Count
            };
        }

        private TemporalCoupling CloneTemporalCoupling(TemporalCoupling source)
        {
            return new TemporalCoupling
            {
                FileA = source.FileA,
                FileB = source.FileB,
                SharedCommits = source.SharedCommits,
                CouplingDegree = source.CouplingDegree
            };
        }

        private MergeLeadTimeRecord CloneMergeLeadTimeRecord(MergeLeadTimeRecord source)
        {
            return new MergeLeadTimeRecord
            {
                Hash = source.Hash,
                Message = source.Message,
                Author = source.Author,
                Date = source.Date,
                LeadTimeHours = source.LeadTimeHours,
                FileCount = source.FileCount
            };
        }

        private AnalysisResult CloneResultMetadata(AnalysisResult result)
        {
            return new AnalysisResult
            {
                SchemaVersion = result.SchemaVersion,
                Tool = result.Tool,
                Analysis = CloneAnalysisMetadata(result.Analysis),
                Settings = result.Settings.Clone(),
                Exclusions = result.Exclusions.Select(CloneExclusionSummary).ToList(),
                Warnings = result.Warnings.ToList(),
                Configuration = new AnalysisConfiguration
                {
                    Scoring = CloneScoringConfiguration(result.Configuration.Scoring),
                    ConfiguredAliasCount = result.Configuration.ConfiguredAliasCount,
                    ConfiguredBotCount = result.Configuration.ConfiguredBotCount,
                    ConfiguredExcludeCount = result.Configuration.ConfiguredExcludeCount,
                    ConfiguredAreaCount = result.Configuration.ConfiguredAreaCount,
                    Identity = new IdentityConfigInfo
                    {
                        MergeOnEmail = result.Configuration.Identity.MergeOnEmail
                    }
                },
                TemporalCoupling = result.TemporalCoupling?.Select(CloneTemporalCoupling).ToList(),
                LeadTimes = result.LeadTimes == null ? null : new LeadTimesInfo
                {
                    AverageLeadTimeHours = result.LeadTimes.AverageLeadTimeHours,
                    Merges = result.LeadTimes.Merges.Select(CloneMergeLeadTimeRecord).ToList()
                }
            };
        }

        public AnalysisResult Anonymize(AnalysisResult result)
        {
            var session = new AnonymizationSession();
            var clonedResult = CloneResultMetadata(result);

            // Map and clone contributors
            clonedResult.Contributors = result.Contributors.Select(contributor =>
                AnonymizeHumanContributorMetric(contributor, session)
            ).ToList();

            // Map and clone files
            clonedResult.Files = result.Files.Select(file =>
                AnonymizeFileMetric(file, session)
            ).ToList();

            // Map and clone areas
            clonedResult.Areas = result.Areas.Select(area =>
                AnonymizeAreaMetric(area, session)
            ).ToList();

            // Map and clone automation
            clonedResult.Automation = result.Automation.Select(automation =>
                MapToAutomationMetric(automation, AnonymizeAutomation(automation.Name, automation.Email, session))
            ).ToList();

            return clonedResult;
        }
    }
}
