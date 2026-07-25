using System;
using System.Collections.Generic;
using System.Linq;

namespace Gitic
{
    public static class ScoringUtils
    {
        private const double MsPerDay = 86400000.0;
        private const double TruckFactorThresholdPct = 0.5;
        private const double SiloThreshold = 0.70;
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
            return Math.Round(value * 100.0) / 100.0;
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

        public static double CalculateHeatScore(ScoreBreakdown breakdown)
        {
            return new HeatScoreCalculator().Calculate(breakdown);
        }

        public static double CalculateAttentionScore(ScoreBreakdown breakdown, AttentionWeights weights)
        {
            return new AttentionScoreCalculator(weights).Calculate(breakdown);
        }
    }

    public interface IScoreCalculator
    {
        double Calculate(ScoreBreakdown breakdown);
    }

    public class HeatWeights
    {
        public double Touches { get; set; } = 0.45;
        public double Churn { get; set; } = 0.45;
        public double Recency { get; set; } = 0.10;
    }

    public class HeatScoreCalculator : IScoreCalculator
    {
        private readonly HeatWeights _weights;

        public HeatScoreCalculator(HeatWeights? weights = null)
        {
            _weights = weights ?? new HeatWeights();
        }

        public double Calculate(ScoreBreakdown breakdown)
        {
            return Math.Round(
                (breakdown.Touches * _weights.Touches +
                 breakdown.Churn * _weights.Churn +
                 breakdown.Recency * _weights.Recency) * 100.0
            );
        }
    }

    public class AttentionScoreCalculator : IScoreCalculator
    {
        private readonly AttentionWeights _weights;

        public AttentionScoreCalculator(AttentionWeights weights)
        {
            _weights = weights;
        }

        public double Calculate(ScoreBreakdown breakdown)
        {
            return Math.Round(
                (breakdown.Churn * _weights.Churn +
                 breakdown.Recency * _weights.Recency +
                 breakdown.ContributorSpread * _weights.ContributorSpread +
                 breakdown.LowFamiliarityConcentration * _weights.LowFamiliarityConcentration) * 100.0
            );
        }
    }

    public class FamiliarityScoringEngine
    {
        private readonly GitizerConfig _config;
        private readonly HashSet<string> _activeContributorKeys;
        private readonly int _depth;

        public FamiliarityScoringEngine(
            GitizerConfig config,
            HashSet<string>? activeContributorKeys = null,
            int depth = 2)
        {
            _config = config;
            _activeContributorKeys = activeContributorKeys ?? new HashSet<string>();
            _depth = depth;
        }

        private class ScoringContext
        {
            public double MaxTouches { get; set; }
            public double MaxChurn { get; set; }
            public double MaxRecency { get; set; }
            public double MaxNetLines { get; set; }
        }

        private ScoringContext CalculateScoringContext(List<ItemAccumulator> items)
        {
            double maxTouches = items.Count > 0 ? items.Max(item => item.Touches) : 1.0;
            if (maxTouches < 1.0) maxTouches = 1.0;

            double maxChurn = items.Count > 0 ? items.Max(item => item.Churn) : 1.0;
            if (maxChurn < 1.0) maxChurn = 1.0;

            double maxRecency = items.Count > 0 ? items.Max(item => ScoringUtils.CalculateRecencyScore(item.LastTouched)) : 0.001;
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

        private ScoreBreakdown CalculateScoreBreakdown(
            ItemAccumulator item,
            ScoringContext context)
        {
            return new ScoreBreakdown
            {
                Touches = 0.0,
                Churn = ScoringUtils.RoundRatio(item.Churn / context.MaxChurn),
                Recency = ScoringUtils.RoundRatio(ScoringUtils.CalculateRecencyScore(item.LastTouched) / context.MaxRecency),
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

        public List<FileMetric> ScoreFiles(List<ItemAccumulator> items, int? depth = null)
        {
            int targetDepth = depth ?? _depth;
            var context = CalculateScoringContext(items);

            return items.Select(item =>
            {
                var contributors = CalculateContributorShares(item);
                var rawBreakdown = CalculateScoreBreakdown(item, context);
                double reworkRate = item.Touches > 0 ? ScoringUtils.RoundRatio((double)item.BugFixTouches / item.Touches) : 0.0;
                double touchesScore = ScoringUtils.RoundRatio(item.Touches / context.MaxTouches);

                double debtVolatility = ScoringUtils.CalculateDebtVolatility(item, context.MaxChurn, context.MaxNetLines);
                double coordinationOverlap = ScoringUtils.CalculateCoordinationOverlap(contributors, item.Touches);
                var knowledgeSilo = ScoringUtils.CalculateKnowledgeSilo(contributors, _activeContributorKeys);

                var breakdown = new ScoreBreakdown
                {
                    Touches = touchesScore,
                    Churn = rawBreakdown.Churn,
                    Recency = rawBreakdown.Recency,
                    ContributorSpread = rawBreakdown.ContributorSpread,
                    LowFamiliarityConcentration = knowledgeSilo.IsSilo ? knowledgeSilo.TopOwnerShare : 0.0
                };

                double heatScore = new HeatScoreCalculator().Calculate(breakdown);
                double attentionScore = new AttentionScoreCalculator(_config.Scoring.Attention).Calculate(breakdown);

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
                    Area = Exclusions.AreaForPath(item.Key, targetDepth, _config.Areas),
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
            var context = CalculateScoringContext(items);

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

                double heatScore = new HeatScoreCalculator().Calculate(breakdown);
                double attentionScore = new AttentionScoreCalculator(_config.Scoring.Attention).Calculate(breakdown);

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
