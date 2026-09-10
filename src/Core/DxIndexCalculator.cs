using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;

namespace Gitic;

/// <summary>
/// Qualitative health rating for Developer Experience (DX) scores.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DxRating
{
    Critical,
    Watch,
    Healthy,
    Excellent
}

/// <summary>
/// Represents the score and details for an individual DX sub-dimension.
/// </summary>
public class DxDimensionScore
{
    [JsonPropertyName("dimension")]
    public string Dimension { get; set; } = string.Empty;

    [JsonPropertyName("score")]
    public double Score { get; set; }

    [JsonPropertyName("weight")]
    public double Weight { get; set; }

    [JsonPropertyName("rating")]
    public DxRating Rating { get; set; }

    [JsonPropertyName("details")]
    public string? Details { get; set; }

    [JsonIgnore]
    public double WeightedScore => Math.Round(Score * Weight, 2);
}

/// <summary>
/// A drag factor that pulls down the composite Developer Experience (DX) index score.
/// </summary>
public class DxDragFactor
{
    [JsonPropertyName("dimension")]
    public string Dimension { get; set; } = string.Empty;

    [JsonPropertyName("drag_score")]
    public double DragScore { get; set; }

    [JsonPropertyName("score")]
    public double Score { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;

    [JsonPropertyName("recommendation")]
    public string Recommendation { get; set; } = string.Empty;
}

/// <summary>
/// Structured breakdown of the 6 core DX sub-dimensions.
/// </summary>
public class DxIndexBreakdown
{
    [JsonPropertyName("delivery_flow")]
    public DxDimensionScore DeliveryFlow { get; set; } = new();

    [JsonPropertyName("code_stability")]
    public DxDimensionScore CodeStability { get; set; } = new();

    [JsonPropertyName("ownership_health")]
    public DxDimensionScore OwnershipHealth { get; set; } = new();

    [JsonPropertyName("architectural_integrity")]
    public DxDimensionScore ArchitecturalIntegrity { get; set; } = new();

    [JsonPropertyName("cognitive_load")]
    public DxDimensionScore CognitiveLoad { get; set; } = new();

    [JsonPropertyName("code_freshness")]
    public DxDimensionScore CodeFreshness { get; set; } = new();
}

/// <summary>
/// Composite Developer Experience (DX) Index result containing the overall score,
/// rating, sub-dimension breakdown, and ranked drag factors.
/// </summary>
public class DxIndexResult
{
    [JsonPropertyName("composite_score")]
    public double CompositeScore { get; set; }

    [JsonPropertyName("rating")]
    public DxRating Rating { get; set; }

    [JsonPropertyName("breakdown")]
    public DxIndexBreakdown Breakdown { get; set; } = new();

    [JsonPropertyName("dimensions")]
    public List<DxDimensionScore> Dimensions { get; set; } = new();

    [JsonPropertyName("top_drag_factors")]
    public List<DxDragFactor> TopDragFactors { get; set; } = new();

    [JsonIgnore]
    public double DeliveryFlowScore => Breakdown.DeliveryFlow.Score;

    [JsonIgnore]
    public double CodeStabilityScore => Breakdown.CodeStability.Score;

    [JsonIgnore]
    public double OwnershipHealthScore => Breakdown.OwnershipHealth.Score;

    [JsonIgnore]
    public double ArchitecturalIntegrityScore => Breakdown.ArchitecturalIntegrity.Score;

    [JsonIgnore]
    public double CognitiveLoadScore => Breakdown.CognitiveLoad.Score;

    [JsonIgnore]
    public double CodeFreshnessScore => Breakdown.CodeFreshness.Score;
}

public partial class AnalysisResult
{
    [JsonPropertyName("dx_index")]
    public DxIndexResult? DxIndex { get; set; }
}

/// <summary>
/// Interface for calculating the Developer Experience (DX) Index composite metric.
/// </summary>
public interface IDxIndexCalculator
{
    DxIndexResult Calculate(AnalysisResult analysisResult);
}

/// <summary>
/// Calculates composite Developer Experience (DX) Index health score from delivery,
/// stability, ownership, coupling, cognitive load, and rot dimensions.
/// </summary>
public class DxIndexCalculator : IDxIndexCalculator
{
    public const double DeliveryFlowWeight = 0.20;
    public const double CodeStabilityWeight = 0.20;
    public const double OwnershipHealthWeight = 0.20;
    public const double ArchitecturalIntegrityWeight = 0.15;
    public const double CognitiveLoadWeight = 0.15;
    public const double CodeFreshnessWeight = 0.10;

    public const string DeliveryFlowDimensionName = "Delivery Flow";
    public const string CodeStabilityDimensionName = "Code Stability";
    public const string OwnershipHealthDimensionName = "Ownership Health";
    public const string ArchitecturalIntegrityDimensionName = "Architectural Integrity";
    public const string CognitiveLoadDimensionName = "Cognitive Load";
    public const string CodeFreshnessDimensionName = "Code Freshness";

    public static DxRating GetRating(double score)
    {
        if (score >= 90.0) return DxRating.Excellent;
        if (score >= 70.0) return DxRating.Healthy;
        if (score >= 50.0) return DxRating.Watch;
        return DxRating.Critical;
    }

    public static DxIndexResult Compute(AnalysisResult analysisResult) =>
        new DxIndexCalculator().Calculate(analysisResult);

    public DxIndexResult Calculate(AnalysisResult analysisResult)
    {
        if (analysisResult == null)
        {
            throw new ArgumentNullException(nameof(analysisResult));
        }

        var delivery = CalculateDeliveryFlow(analysisResult.LeadTimes);
        var stability = CalculateCodeStability(analysisResult.Files, analysisResult.Areas);
        var ownership = CalculateOwnershipHealth(analysisResult.Areas, analysisResult.Files);
        var architecture = CalculateArchitecturalIntegrity(analysisResult.TemporalCoupling, analysisResult.Files);
        var cognitive = CalculateCognitiveLoad(analysisResult.Contributors);
        var freshness = CalculateCodeFreshness(analysisResult.CuratedReports?.CodeRot, analysisResult.Files.Count);

        var dimensions = new List<DxDimensionScore>
        {
            delivery,
            stability,
            ownership,
            architecture,
            cognitive,
            freshness
        };

        double rawComposite = (delivery.Score * delivery.Weight) +
                              (stability.Score * stability.Weight) +
                              (ownership.Score * ownership.Weight) +
                              (architecture.Score * architecture.Weight) +
                              (cognitive.Score * cognitive.Weight) +
                              (freshness.Score * freshness.Weight);

        double compositeScore = Math.Clamp(Math.Round(rawComposite, 1), 0.0, 100.0);
        DxRating rating = GetRating(compositeScore);

        var dragFactors = IdentifyDragFactors(dimensions);

        return new DxIndexResult
        {
            CompositeScore = compositeScore,
            Rating = rating,
            Breakdown = new DxIndexBreakdown
            {
                DeliveryFlow = delivery,
                CodeStability = stability,
                OwnershipHealth = ownership,
                ArchitecturalIntegrity = architecture,
                CognitiveLoad = cognitive,
                CodeFreshness = freshness
            },
            Dimensions = dimensions,
            TopDragFactors = dragFactors
        };
    }

    public DxDimensionScore CalculateDeliveryFlow(LeadTimesInfo? leadTimes)
    {
        double p50 = 0.0;
        double p90 = 0.0;
        double score;
        string details;

        var merges = leadTimes?.Merges;
        if (merges != null && merges.Count > 0)
        {
            var sortedTimes = merges.Select(m => m.LeadTimeHours).OrderBy(t => t).ToList();
            p50 = Percentile(sortedTimes, 0.50);
            p90 = Percentile(sortedTimes, 0.90);

            double score50 = ScoreLeadTimeP50(p50);
            double score90 = ScoreLeadTimeP90(p90);

            // If either P50 > 48h or P90 > 168h, it is Critical (< 30)
            if (p50 > 48.0 || p90 > 168.0)
            {
                score = Math.Min(Math.Min(score50, score90), 29.0);
            }
            else
            {
                score = 0.5 * score50 + 0.5 * score90;
            }

            score = Math.Clamp(Math.Round(score, 1), 0.0, 100.0);
            details = $"P50: {p50:F1}h, P90: {p90:F1}h across {merges.Count} merges";
        }
        else if (leadTimes != null && leadTimes.AverageLeadTimeHours > 0)
        {
            p50 = leadTimes.AverageLeadTimeHours;
            p90 = leadTimes.AverageLeadTimeHours * 1.5;
            double score50 = ScoreLeadTimeP50(p50);
            double score90 = ScoreLeadTimeP90(p90);
            score = Math.Clamp(Math.Round(0.5 * score50 + 0.5 * score90, 1), 0.0, 100.0);
            details = $"Estimated from avg lead time: {leadTimes.AverageLeadTimeHours:F1}h";
        }
        else
        {
            // No lead times measured / fast trunk
            score = 100.0;
            details = "No merge lead times recorded";
        }

        return new DxDimensionScore
        {
            Dimension = DeliveryFlowDimensionName,
            Score = score,
            Weight = DeliveryFlowWeight,
            Rating = GetRating(score),
            Details = details
        };
    }

    public DxDimensionScore CalculateCodeStability(IEnumerable<FileMetric>? files, IEnumerable<AreaMetric>? areas = null)
    {
        double avgRework = 0.0;
        int evaluatedCount = 0;
        string details;

        var fileList = files?.Where(f => f.ReworkRate.HasValue).ToList();
        if (fileList != null && fileList.Count > 0)
        {
            avgRework = fileList.Average(f => f.ReworkRate!.Value);
            evaluatedCount = fileList.Count;
            details = $"Avg rework rate: {avgRework * 100.0:F1}% across {evaluatedCount} files";
        }
        else
        {
            var areaList = areas?.Where(a => a.ReworkRate.HasValue).ToList();
            if (areaList != null && areaList.Count > 0)
            {
                avgRework = areaList.Average(a => a.ReworkRate!.Value);
                evaluatedCount = areaList.Count;
                details = $"Avg rework rate: {avgRework * 100.0:F1}% across {evaluatedCount} areas";
            }
            else
            {
                details = "No rework rate data recorded";
            }
        }

        double score = ScoreReworkRate(avgRework);
        score = Math.Clamp(Math.Round(score, 1), 0.0, 100.0);

        return new DxDimensionScore
        {
            Dimension = CodeStabilityDimensionName,
            Score = score,
            Weight = CodeStabilityWeight,
            Rating = GetRating(score),
            Details = details
        };
    }

    public DxDimensionScore CalculateOwnershipHealth(IEnumerable<AreaMetric>? areas, IEnumerable<FileMetric>? files = null)
    {
        int totalAreas = 0;
        int siloAreas = 0;
        double siloRatio = 0.0;
        string details;

        var areaList = areas?.ToList();
        if (areaList != null && areaList.Count > 0)
        {
            totalAreas = areaList.Count;
            foreach (var area in areaList)
            {
                bool isSilo = false;
                if (area.Contributors != null && area.Contributors.Count > 0)
                {
                    double topShare = area.Contributors.Max(c => c.ActivityShare);
                    if (topShare > 0.50)
                    {
                        isSilo = true;
                    }
                }
                else if (area.ContributorCount == 1)
                {
                    isSilo = true;
                }

                if (isSilo)
                {
                    siloAreas++;
                }
            }

            siloRatio = totalAreas > 0 ? (double)siloAreas / totalAreas : 0.0;
            details = $"{siloAreas} of {totalAreas} areas have silos ({siloRatio * 100.0:F1}%)";
        }
        else
        {
            var fileList = files?.ToList();
            if (fileList != null && fileList.Count > 0)
            {
                int siloFiles = fileList.Count(f =>
                    f.KnowledgeSilo?.IsSilo == true ||
                    (f.KnowledgeSilo != null && f.KnowledgeSilo.TopOwnerShare > 0.50) ||
                    (f.KnowledgeSilo != null && f.KnowledgeSilo.TruckFactor == 1 && f.ContributorCount > 1));

                siloRatio = (double)siloFiles / fileList.Count;
                details = $"{siloFiles} of {fileList.Count} files have knowledge silos ({siloRatio * 100.0:F1}%)";
            }
            else
            {
                details = "No ownership or area data recorded";
            }
        }

        double score = ScoreOwnershipSilos(siloRatio, siloAreas, totalAreas);
        score = Math.Clamp(Math.Round(score, 1), 0.0, 100.0);

        return new DxDimensionScore
        {
            Dimension = OwnershipHealthDimensionName,
            Score = score,
            Weight = OwnershipHealthWeight,
            Rating = GetRating(score),
            Details = details
        };
    }

    public DxDimensionScore CalculateArchitecturalIntegrity(IEnumerable<TemporalCoupling>? couplings, IEnumerable<FileMetric>? files = null)
    {
        var couplingList = couplings?.ToList();
        if (couplingList == null || couplingList.Count == 0)
        {
            return new DxDimensionScore
            {
                Dimension = ArchitecturalIntegrityDimensionName,
                Score = 100.0,
                Weight = ArchitecturalIntegrityWeight,
                Rating = DxRating.Excellent,
                Details = "No temporal coupling pairs detected"
            };
        }

        var fileAreaMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (files != null)
        {
            foreach (var f in files)
            {
                if (!string.IsNullOrEmpty(f.Path) && !string.IsNullOrEmpty(f.Area))
                {
                    fileAreaMap[f.Path] = f.Area;
                }
            }
        }

        int crossBoundaryCount = 0;
        foreach (var c in couplingList)
        {
            string boundaryA = GetBoundary(c.FileA, fileAreaMap);
            string boundaryB = GetBoundary(c.FileB, fileAreaMap);
            if (!string.Equals(boundaryA, boundaryB, StringComparison.OrdinalIgnoreCase))
            {
                crossBoundaryCount++;
            }
        }

        double crossBoundaryRate = (double)crossBoundaryCount / couplingList.Count;
        double score = ScoreCrossBoundaryRate(crossBoundaryRate);
        score = Math.Clamp(Math.Round(score, 1), 0.0, 100.0);

        return new DxDimensionScore
        {
            Dimension = ArchitecturalIntegrityDimensionName,
            Score = score,
            Weight = ArchitecturalIntegrityWeight,
            Rating = GetRating(score),
            Details = $"Cross-boundary coupling rate: {crossBoundaryRate * 100.0:F1}% ({crossBoundaryCount}/{couplingList.Count} pairs)"
        };
    }

    public DxDimensionScore CalculateCognitiveLoad(IEnumerable<ContributorMetric>? contributors)
    {
        var contributorList = contributors?.ToList();
        if (contributorList == null || contributorList.Count == 0)
        {
            return new DxDimensionScore
            {
                Dimension = CognitiveLoadDimensionName,
                Score = 100.0,
                Weight = CognitiveLoadWeight,
                Rating = DxRating.Excellent,
                Details = "No contributor context switching data"
            };
        }

        double avgAreas = contributorList.Average(c => c.Areas?.Count ?? 1);
        double score = ScoreCognitiveLoad(avgAreas);
        score = Math.Clamp(Math.Round(score, 1), 0.0, 100.0);

        return new DxDimensionScore
        {
            Dimension = CognitiveLoadDimensionName,
            Score = score,
            Weight = CognitiveLoadWeight,
            Rating = GetRating(score),
            Details = $"Avg areas touched per developer: {avgAreas:F1}"
        };
    }

    public DxDimensionScore CalculateCodeFreshness(CodeRotMetric? codeRot, int totalFiles)
    {
        if (totalFiles <= 0 || codeRot == null)
        {
            return new DxDimensionScore
            {
                Dimension = CodeFreshnessDimensionName,
                Score = 100.0,
                Weight = CodeFreshnessWeight,
                Rating = DxRating.Excellent,
                Details = "No code rot or zombie file data"
            };
        }

        double zombieRatio = (double)codeRot.ZombieFileCount / totalFiles;
        double score = ScoreZombieRatio(zombieRatio);
        score = Math.Clamp(Math.Round(score, 1), 0.0, 100.0);

        return new DxDimensionScore
        {
            Dimension = CodeFreshnessDimensionName,
            Score = score,
            Weight = CodeFreshnessWeight,
            Rating = GetRating(score),
            Details = $"Zombie file ratio: {zombieRatio * 100.0:F1}% ({codeRot.ZombieFileCount}/{totalFiles} files)"
        };
    }

    private static double ScoreLeadTimeP50(double p50)
    {
        if (p50 <= 4.0)
        {
            return 100.0 - (p50 / 4.0) * 10.0;
        }
        if (p50 <= 24.0)
        {
            return 90.0 - ((p50 - 4.0) / 20.0) * 30.0;
        }
        if (p50 <= 48.0)
        {
            return 60.0 - ((p50 - 24.0) / 24.0) * 31.0;
        }
        return Math.Max(0.0, 29.0 - ((p50 - 48.0) / 48.0) * 29.0);
    }

    private static double ScoreLeadTimeP90(double p90)
    {
        if (p90 <= 24.0)
        {
            return 100.0 - (p90 / 24.0) * 10.0;
        }
        if (p90 <= 72.0)
        {
            return 90.0 - ((p90 - 24.0) / 48.0) * 30.0;
        }
        if (p90 <= 168.0)
        {
            return 60.0 - ((p90 - 72.0) / 96.0) * 31.0;
        }
        return Math.Max(0.0, 29.0 - ((p90 - 168.0) / 168.0) * 29.0);
    }

    private static double ScoreReworkRate(double r)
    {
        if (r <= 0.0)
        {
            return 100.0;
        }
        if (r < 0.10)
        {
            return 100.0 - (r / 0.10) * 10.0;
        }
        if (r <= 0.25)
        {
            return 90.0 - ((r - 0.10) / 0.15) * 35.0;
        }
        if (r <= 0.30)
        {
            return 55.0 - ((r - 0.25) / 0.05) * 26.0;
        }
        return Math.Max(0.0, 29.0 - ((r - 0.30) / 0.20) * 29.0);
    }

    private static double ScoreOwnershipSilos(double siloRatio, int siloAreas, int totalAreas)
    {
        if (siloRatio <= 0.0)
        {
            return 100.0;
        }
        if (siloRatio > 0.30)
        {
            return Math.Max(0.0, 29.0 - ((siloRatio - 0.30) / 0.40) * 29.0);
        }
        if (siloRatio <= 0.10)
        {
            return 100.0 - (siloRatio / 0.10) * 10.0;
        }
        return 90.0 - ((siloRatio - 0.10) / 0.20) * 35.0;
    }

    private static double ScoreCrossBoundaryRate(double rate)
    {
        if (rate <= 0.0)
        {
            return 100.0;
        }
        if (rate < 0.10)
        {
            return 100.0 - (rate / 0.10) * 10.0;
        }
        if (rate <= 0.25)
        {
            return 90.0 - ((rate - 0.10) / 0.15) * 30.0;
        }
        if (rate <= 0.40)
        {
            return 60.0 - ((rate - 0.25) / 0.15) * 31.0;
        }
        return Math.Max(0.0, 29.0 - ((rate - 0.40) / 0.30) * 29.0);
    }

    private static double ScoreCognitiveLoad(double avgAreas)
    {
        if (avgAreas <= 1.0)
        {
            return 100.0;
        }
        if (avgAreas < 3.0)
        {
            return 100.0 - ((avgAreas - 1.0) / 2.0) * 10.0;
        }
        if (avgAreas <= 6.0)
        {
            return 90.0 - ((avgAreas - 3.0) / 3.0) * 30.0;
        }
        if (avgAreas <= 8.0)
        {
            return 60.0 - ((avgAreas - 6.0) / 2.0) * 31.0;
        }
        return Math.Max(0.0, 29.0 - ((avgAreas - 8.0) / 4.0) * 29.0);
    }

    private static double ScoreZombieRatio(double ratio)
    {
        if (ratio <= 0.0)
        {
            return 100.0;
        }
        if (ratio < 0.05)
        {
            return 100.0 - (ratio / 0.05) * 10.0;
        }
        if (ratio <= 0.15)
        {
            return 90.0 - ((ratio - 0.05) / 0.10) * 30.0;
        }
        if (ratio <= 0.25)
        {
            return 60.0 - ((ratio - 0.15) / 0.10) * 31.0;
        }
        return Math.Max(0.0, 29.0 - ((ratio - 0.25) / 0.25) * 29.0);
    }

    private static string GetBoundary(string filePath, Dictionary<string, string> fileAreaMap)
    {
        if (fileAreaMap.TryGetValue(filePath, out var area) && !string.IsNullOrWhiteSpace(area))
        {
            return area;
        }

        string normalized = filePath.Replace('\\', '/').TrimStart('/');
        int slash = normalized.IndexOf('/');
        return slash > 0 ? normalized.Substring(0, slash) : "root";
    }

    private static double Percentile(List<double> sorted, double p)
    {
        if (sorted.Count == 0) return 0.0;
        if (sorted.Count == 1) return sorted[0];

        double idx = p * (sorted.Count - 1);
        int lower = (int)Math.Floor(idx);
        int upper = (int)Math.Ceiling(idx);

        if (lower == upper) return sorted[lower];
        return sorted[lower] + (idx - lower) * (sorted[upper] - sorted[lower]);
    }

    private static List<DxDragFactor> IdentifyDragFactors(List<DxDimensionScore> dimensions)
    {
        const double targetBenchmark = 80.0;
        var dragFactors = new List<DxDragFactor>();

        foreach (var dim in dimensions)
        {
            double dragImpact = Math.Round((100.0 - dim.Score) * dim.Weight, 1);
            if (dim.Score < targetBenchmark || dragImpact >= 2.0)
            {
                var (reason, rec) = DescribeDrag(dim);
                dragFactors.Add(new DxDragFactor
                {
                    Dimension = dim.Dimension,
                    Score = dim.Score,
                    DragScore = dragImpact,
                    Reason = reason,
                    Recommendation = rec
                });
            }
        }

        return dragFactors.OrderByDescending(d => d.DragScore).ToList();
    }

    private static (string Reason, string Recommendation) DescribeDrag(DxDimensionScore dim)
    {
        return dim.Dimension switch
        {
            DeliveryFlowDimensionName => (
                $"Delivery flow score is {dim.Score:F0}/100 ({dim.Details}). Merges experience high lead time latency.",
                "Streamline code review turnaround and decompose large pull requests to lower P90 lead time."),
            CodeStabilityDimensionName => (
                $"Code stability score is {dim.Score:F0}/100 ({dim.Details}). Code modifications frequently require rework.",
                "Refactor volatile hotspots, increase test coverage, and improve specifications for high-churn files."),
            OwnershipHealthDimensionName => (
                $"Ownership health score is {dim.Score:F0}/100 ({dim.Details}). Knowledge concentration increases bus factor risk.",
                "Distribute module ownership through pair programming, cross-team reviews, and shared maintainership."),
            ArchitecturalIntegrityDimensionName => (
                $"Architectural integrity score is {dim.Score:F0}/100 ({dim.Details}). Frequent cross-boundary co-changes indicate modular leakage.",
                "Enforce module boundaries, decouple co-changing cross-subsystem components, and extract shared abstractions."),
            CognitiveLoadDimensionName => (
                $"Cognitive load score is {dim.Score:F0}/100 ({dim.Details}). Developers switch context across too many disparate codebase areas.",
                "Align team ownership boundaries with codebase subsystems to reduce developer context-switching breadth."),
            CodeFreshnessDimensionName => (
                $"Code freshness score is {dim.Score:F0}/100 ({dim.Details}). A notable fraction of files are stale/zombie code.",
                "Schedule a technical debt sprint to archive or delete unused zombie files and deprecated modules."),
            _ => ($"Sub-dimension score is {dim.Score:F0}/100 ({dim.Details}).", "Review and address highlighted dimension metrics.")
        };
    }
}
