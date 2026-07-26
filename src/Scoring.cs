using System;
using System.Collections.Generic;
using System.Linq;

namespace Gitic
{
    public interface IKnowledgeSiloCalculator
    {
        KnowledgeSiloMetric CalculateKnowledgeSilo(
            List<ContributorShare> contributors,
            HashSet<string> activeContributorKeys);
    }

    public class KnowledgeSiloCalculator : IKnowledgeSiloCalculator
    {
        private const double TruckFactorThresholdPct = 0.5;
        private const double SiloThreshold = 0.70;

        public KnowledgeSiloMetric CalculateKnowledgeSilo(
            List<ContributorShare> contributors,
            HashSet<string> activeContributorKeys)
        {
            int truckFactor = 1;
            double totalActivity = contributors.Sum(c => c.Activity);
            if (totalActivity > 0)
            {
                double runningSum = 0.0;
                int count = 0;
                foreach (var contr in contributors)
                {
                    runningSum += contr.Activity;
                    count++;
                    if (runningSum >= totalActivity * TruckFactorThresholdPct)
                    {
                        truckFactor = count;
                        break;
                    }
                }
            }

            double topOwnerShare = contributors.Count > 0 ? contributors[0].ActivityShare : 0.0;
            bool isSilo = topOwnerShare >= SiloThreshold;

            bool abandoned = true;
            foreach (var contr in contributors)
            {
                string key = IdentityUtils.IdentityKey(new GitIdentity { Name = contr.Name, Email = contr.Email });
                if (activeContributorKeys.Contains(key))
                {
                    abandoned = false;
                    break;
                }
            }
            if (contributors.Count == 0)
            {
                abandoned = false;
            }

            return new KnowledgeSiloMetric
            {
                TruckFactor = truckFactor,
                TopOwnerShare = topOwnerShare,
                IsSilo = isSilo,
                Abandoned = abandoned
            };
        }
    }

    public interface IScoringUtilityService
    {
        double CalculateRecencyScore(long timestamp);
        double CalculateDebtVolatility(ItemAccumulator item, double maxChurn, double maxNetLines);
        double CalculateCoordinationOverlap(List<ContributorShare> contributors, int itemTouches);
    }

    public class ScoringUtilityService : IScoringUtilityService
    {
        public double CalculateRecencyScore(long timestamp)
        {
            return ScoringUtils.CalculateRecencyScore(timestamp);
        }

        public double CalculateDebtVolatility(
            ItemAccumulator item,
            double maxChurn,
            double maxNetLines)
        {
            return ScoringUtils.CalculateDebtVolatility(item, maxChurn, maxNetLines);
        }

        public double CalculateCoordinationOverlap(
            List<ContributorShare> contributors,
            int itemTouches)
        {
            return ScoringUtils.CalculateCoordinationOverlap(contributors, itemTouches);
        }
    }

    public static class ScoringUtils
    {
        private const double MsPerDay = 86400000.0;
        public const double ConcentrationHealthyMax = 0.50;
        public const double ConcentrationWatchMax = 0.70;

        private const double RecencyDecayHalfLifeDays = 30.0;
        private const double DebtVolatilityMultiplier = 100.0;
        private const double ScoreScaleMultiplier = 100.0;
        private const double MaxCoordinationScore = 100.0;
        private const double MinCoordinationScore = 0.0;
        private const int CoordinationMaxContributors = 5;
        private const int CoordinationMaxTouches = 10;
        private const double CoordinationMultiplier = 2.0;

        private const double HeatScoreTouchesWeight = 0.45;
        private const double HeatScoreChurnWeight = 0.45;
        private const double HeatScoreRecencyWeight = 0.1;

        public static string ConcentrationTier(double share)
        {
            if (share < ConcentrationHealthyMax) return "healthy";
            if (share < ConcentrationWatchMax) return "watch";
            return "silo";
        }

        public static double RoundRatio(double value)
        {
            return Math.Round(value * 100.0) / 100.0;
        }

        public static double RoundActivity(double value)
        {
            return RoundRatio(value);
        }

        public static double CalculateRecencyScore(long timestamp)
        {
            if (timestamp == 0)
            {
                return 0;
            }
            double nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            double ageDays = (nowMs - timestamp) / MsPerDay;
            return Math.Exp(-ageDays * (Math.Log(2.0) / RecencyDecayHalfLifeDays));
        }

        public static double CalculateDebtVolatility(
            ItemAccumulator item,
            double maxChurn,
            double maxNetLines)
        {
            double netLines = Math.Max(0.0, item.Added - item.Deleted);
            return Math.Round(
                (item.Churn / maxChurn) * (netLines / maxNetLines) * DebtVolatilityMultiplier
            );
        }

        public static double CalculateCoordinationOverlap(
            List<ContributorShare> contributors,
            int itemTouches)
        {
            double entropy = 0.0;
            foreach (var contr in contributors)
            {
                double p = contr.ActivityShare;
                if (p > 0)
                {
                    entropy -= p * Math.Log2(p);
                }
            }
            return Math.Min(
                MaxCoordinationScore,
                Math.Max(
                    MinCoordinationScore,
                    Math.Round(
                        entropy *
                        Math.Min(CoordinationMaxContributors, contributors.Count) *
                        Math.Min(CoordinationMaxTouches, itemTouches) *
                        CoordinationMultiplier
                    )
                )
            );
        }

        public static KnowledgeSiloMetric CalculateKnowledgeSilo(
            List<ContributorShare> contributors,
            HashSet<string> activeContributorKeys)
        {
            return new KnowledgeSiloCalculator().CalculateKnowledgeSilo(contributors, activeContributorKeys);
        }

        public static double CalculateHeatScore(ScoreBreakdown breakdown, HeatWeights? weights = null)
        {
            var w = weights ?? new HeatWeights();
            return Math.Round(
                (breakdown.Touches * w.Touches +
                 breakdown.Churn * w.Churn +
                 breakdown.Recency * w.Recency) * 100.0
            );
        }

        public static double CalculateAttentionScore(ScoreBreakdown breakdown, AttentionWeights weights)
        {
            return Math.Round(
                (breakdown.Churn * weights.Churn +
                 breakdown.Recency * weights.Recency +
                 breakdown.ContributorSpread * weights.ContributorSpread +
                 breakdown.LowFamiliarityConcentration * weights.LowFamiliarityConcentration) * 100.0
            );
        }
    }

    public class HeatWeights
    {
        public double Touches { get; set; } = 0.45;
        public double Churn { get; set; } = 0.45;
        public double Recency { get; set; } = 0.10;
    }

    public interface IFamiliarityScoringEngine
    {
        List<FileMetric> ScoreFiles(List<ItemAccumulator> items, int depth);
        List<AreaMetric> ScoreAreas(List<ItemAccumulator> items);
    }

    public class FamiliarityScoringEngine : IFamiliarityScoringEngine
    {
        private readonly GitizerConfig _config;
        private readonly HashSet<string> _activeContributorKeys;
        private readonly int _depth;
        private readonly IKnowledgeSiloCalculator _siloCalculator;
        private readonly IAreaMapper _areaMapper;
        private readonly IScoringUtilityService _scoringUtilityService;

        public FamiliarityScoringEngine(
            GitizerConfig config,
            HashSet<string>? activeContributorKeys = null,
            int depth = 2,
            IKnowledgeSiloCalculator? siloCalculator = null,
            IAreaMapper? areaMapper = null,
            IScoringUtilityService? scoringUtilityService = null)
        {
            _config = config;
            _activeContributorKeys = activeContributorKeys ?? new HashSet<string>();
            _depth = depth;
            _siloCalculator = siloCalculator ?? new KnowledgeSiloCalculator();
            _areaMapper = areaMapper ?? new AreaMapper();
            _scoringUtilityService = scoringUtilityService ?? new ScoringUtilityService();
        }

        private class ScoringContext
        {
            public double MaxTouches { get; set; }
            public double MaxChurn { get; set; }
            public double MaxRecency { get; set; }
            public double MaxNetLines { get; set; }

            public static ScoringContext Create(List<ItemAccumulator> items, IScoringUtilityService scoringUtilityService)
            {
                double maxTouches = items.Count > 0 ? items.Max(item => item.Touches) : 1.0;
                if (maxTouches < 1.0) maxTouches = 1.0;

                double maxChurn = items.Count > 0 ? items.Max(item => item.Churn) : 1.0;
                if (maxChurn < 1.0) maxChurn = 1.0;

                double maxRecency = items.Count > 0 ? items.Max(item => scoringUtilityService.CalculateRecencyScore(item.LastTouched)) : 0.001;
                if (maxRecency < 0.001) maxRecency = 0.001;

                double maxNetLines = items.Count > 0 ? items.Max(item => Math.Max(0.0, item.Added - item.Deleted)) : 1.0;
                if (maxNetLines < 1.0) maxNetLines = 1.0;

                return new ScoringContext
                {
                    MaxTouches = maxTouches,
                    MaxChurn = maxChurn,
                    MaxRecency = maxRecency,
                    MaxNetLines = maxNetLines
                };
            }
        }

        private ScoreBreakdown CalculateScoreBreakdown(
            ItemAccumulator item,
            ScoringContext context)
        {
            return new ScoreBreakdown
            {
                Touches = 0.0,
                Churn = ScoringUtils.RoundRatio(item.Churn / context.MaxChurn),
                Recency = ScoringUtils.RoundRatio(_scoringUtilityService.CalculateRecencyScore(item.LastTouched) / context.MaxRecency),
                ContributorSpread = item.Touches > 0 ? ScoringUtils.RoundRatio((double)item.ContributorCredits.Count / item.Touches) : 0.0,
                LowFamiliarityConcentration = 0.0
            };
        }

        private List<ContributorShare> CalculateContributorShares(ItemAccumulator item)
        {
            double total = item.ContributorCredits.Values.Sum(c => c.Activity);
            if (total == 0.0)
            {
                return new List<ContributorShare>();
            }

            return item.ContributorCredits.Values
                .Select(credit => new ContributorShare
                {
                    Name = credit.Identity.Name,
                    Email = credit.Identity.Email,
                    Activity = ScoringUtils.RoundActivity(credit.Activity),
                    ActivityShare = ScoringUtils.RoundRatio(credit.Activity / total)
                })
                .OrderByDescending(c => c.Activity)
                .ThenBy(c => c.Name, StringComparer.Ordinal)
                .ToList();
        }

        public List<FileMetric> ScoreFiles(List<ItemAccumulator> items, int depth)
        {
            int targetDepth = depth;
            var context = ScoringContext.Create(items, _scoringUtilityService);

            return items.Select(item =>
            {
                var contributors = CalculateContributorShares(item);
                var rawBreakdown = CalculateScoreBreakdown(item, context);
                double reworkRate = item.Touches > 0 ? ScoringUtils.RoundRatio((double)item.BugFixTouches / item.Touches) : 0.0;
                double touchesScore = ScoringUtils.RoundRatio(item.Touches / context.MaxTouches);

                double debtVolatility = _scoringUtilityService.CalculateDebtVolatility(item, context.MaxChurn, context.MaxNetLines);
                double coordinationOverlap = _scoringUtilityService.CalculateCoordinationOverlap(contributors, item.Touches);
                var knowledgeSilo = _siloCalculator.CalculateKnowledgeSilo(contributors, _activeContributorKeys);

                var breakdown = new ScoreBreakdown
                {
                    Touches = touchesScore,
                    Churn = rawBreakdown.Churn,
                    Recency = rawBreakdown.Recency,
                    ContributorSpread = rawBreakdown.ContributorSpread,
                    LowFamiliarityConcentration = knowledgeSilo.IsSilo ? knowledgeSilo.TopOwnerShare : 0.0
                };

                double heatScore = ScoringUtils.CalculateHeatScore(breakdown);
                double attentionScore = ScoringUtils.CalculateAttentionScore(breakdown, _config.Scoring.Attention);

                string lastTouchedStr = DateTimeOffset.FromUnixTimeMilliseconds(item.LastTouched).UtcDateTime.ToString("yyyy-MM-dd");

                var innerSymbols = item.Symbols
                    .Select(kv => new InnerSymbolMetric { Name = kv.Key, Touches = kv.Value })
                    .OrderByDescending(s => s.Touches)
                    .ThenBy(s => s.Name, StringComparer.Ordinal)
                    .Take(15)
                    .ToList();

                return new FileMetric
                {
                    Path = item.Key,
                    Area = _areaMapper.AreaForPath(item.Key, targetDepth, _config.Areas),
                    Touches = item.Touches,
                    Added = item.Added,
                    Deleted = item.Deleted,
                    Churn = item.Churn,
                    LastTouched = lastTouchedStr,
                    ContributorCount = contributors.Count,
                    Contributors = contributors,
                    HeatScore = heatScore,
                    AttentionScore = attentionScore,
                    ScoreBreakdown = breakdown,
                    InnerSymbols = innerSymbols,
                    DebtVolatility = debtVolatility,
                    ReworkRate = reworkRate,
                    CoordinationOverlap = coordinationOverlap,
                    KnowledgeSilo = knowledgeSilo
                };
            }).ToList();
        }

        public List<AreaMetric> ScoreAreas(List<ItemAccumulator> items)
        {
            var context = ScoringContext.Create(items, _scoringUtilityService);

            return items.Select(item =>
            {
                var contributors = CalculateContributorShares(item);
                var rawBreakdown = CalculateScoreBreakdown(item, context);
                double reworkRate = item.Touches > 0 ? ScoringUtils.RoundRatio((double)item.BugFixTouches / item.Touches) : 0.0;
                double touchesScore = ScoringUtils.RoundRatio(item.Touches / context.MaxTouches);

                var breakdown = new ScoreBreakdown
                {
                    Touches = touchesScore,
                    Churn = rawBreakdown.Churn,
                    Recency = rawBreakdown.Recency,
                    ContributorSpread = rawBreakdown.ContributorSpread,
                    LowFamiliarityConcentration = 0.0
                };

                double heatScore = ScoringUtils.CalculateHeatScore(breakdown);
                double attentionScore = ScoringUtils.CalculateAttentionScore(breakdown, _config.Scoring.Attention);

                string lastTouchedStr = DateTimeOffset.FromUnixTimeMilliseconds(item.LastTouched).UtcDateTime.ToString("yyyy-MM-dd");

                return new AreaMetric
                {
                    Area = item.Key,
                    Touches = item.Touches,
                    Added = item.Added,
                    Deleted = item.Deleted,
                    Churn = item.Churn,
                    FileCount = item.Files.Count,
                    LastTouched = lastTouchedStr,
                    ContributorCount = contributors.Count,
                    Contributors = contributors,
                    HeatScore = heatScore,
                    AttentionScore = attentionScore,
                    ScoreBreakdown = breakdown,
                    ReworkRate = reworkRate
                };
            }).ToList();
        }
    }
}
