using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;

namespace Gitic;

/// <summary>
/// Probability level categorization for temporal co-change coupling.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CouplingProbabilityLevel
{
    Low,
    Moderate,
    High
}

/// <summary>
/// Represents a single predicted co-change file item with coupling degree and ownership metadata.
/// </summary>
public class ImpactPredictionItem
{
    [JsonPropertyName("file")]
    public string File { get; set; } = string.Empty;

    [JsonPropertyName("target_file")]
    public string TargetFile { get; set; } = string.Empty;

    [JsonPropertyName("shared_commits")]
    public int SharedCommits { get; set; }

    [JsonPropertyName("coupling_degree")]
    public double CouplingDegree { get; set; }

    [JsonPropertyName("coupling_percentage")]
    public double CouplingPercentage => Math.Round(CouplingDegree * 100.0, 1);

    [JsonPropertyName("probability_level")]
    public CouplingProbabilityLevel ProbabilityLevel { get; set; }

    [JsonPropertyName("area")]
    public string Area { get; set; } = string.Empty;

    [JsonPropertyName("target_area")]
    public string TargetArea { get; set; } = string.Empty;

    [JsonPropertyName("is_cross_boundary")]
    public bool IsCrossBoundary { get; set; }

    [JsonPropertyName("top_contributor")]
    public string? TopContributor { get; set; }

    [JsonPropertyName("top_contributor_email")]
    public string? TopContributorEmail { get; set; }

    [JsonPropertyName("top_contributor_share")]
    public double? TopContributorShare { get; set; }
}

/// <summary>
/// Represents a detected architectural cross-boundary leakage alert.
/// </summary>
public class CrossBoundaryAlert
{
    [JsonPropertyName("target_file")]
    public string TargetFile { get; set; } = string.Empty;

    [JsonPropertyName("target_area")]
    public string TargetArea { get; set; } = string.Empty;

    [JsonPropertyName("cross_boundary_count")]
    public int CrossBoundaryCount { get; set; }

    [JsonPropertyName("total_coupled_count")]
    public int TotalCoupledCount { get; set; }

    [JsonPropertyName("cross_boundary_files")]
    public List<string> CrossBoundaryFiles { get; set; } = new();

    [JsonPropertyName("cross_boundary_areas")]
    public List<string> CrossBoundaryAreas { get; set; } = new();

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Represents a recommended owner coordination notification entry.
/// </summary>
public class OwnerCoordination
{
    [JsonPropertyName("owner")]
    public string Owner { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("files")]
    public List<string> Files { get; set; } = new();

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Report containing predicted co-change impact, cross-boundary alerts, and owner notifications.
/// </summary>
public class ImpactPredictionReport
{
    [JsonPropertyName("target_files")]
    public List<string> TargetFiles { get; set; } = new();

    [JsonPropertyName("warn_threshold")]
    public double WarnThreshold { get; set; } = 0.5;

    [JsonPropertyName("predictions")]
    public List<ImpactPredictionItem> Predictions { get; set; } = new();

    [JsonPropertyName("high_probability_predictions")]
    public List<ImpactPredictionItem> HighProbabilityPredictions { get; set; } = new();

    [JsonPropertyName("moderate_probability_predictions")]
    public List<ImpactPredictionItem> ModerateProbabilityPredictions { get; set; } = new();

    [JsonPropertyName("low_probability_predictions")]
    public List<ImpactPredictionItem> LowProbabilityPredictions { get; set; } = new();

    [JsonPropertyName("cross_boundary_alerts")]
    public List<CrossBoundaryAlert> CrossBoundaryAlerts { get; set; } = new();

    [JsonPropertyName("owner_coordinations")]
    public List<OwnerCoordination> OwnerCoordinations { get; set; } = new();

    [JsonPropertyName("has_high_impact")]
    public bool HasHighImpact => HighProbabilityPredictions.Count > 0;

    [JsonPropertyName("has_cross_boundary_leak")]
    public bool HasCrossBoundaryLeak => CrossBoundaryAlerts.Any(a => a.CrossBoundaryCount > 0);

    /// <summary>
    /// Formats the impact analysis into a formatted box report matching the Gitic vision spec.
    /// </summary>
    public string RenderHumanReport(bool plain = false)
    {
        var sb = new StringBuilder();

        if (TargetFiles.Count == 0)
        {
            return "No target files provided for impact prediction.\n";
        }

        foreach (var targetFile in TargetFiles)
        {
            var targetPredictions = Predictions
                .Where(p => string.Equals(p.TargetFile, targetFile, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var high = targetPredictions.Where(p => p.ProbabilityLevel == CouplingProbabilityLevel.High).ToList();
            var moderate = targetPredictions.Where(p => p.ProbabilityLevel == CouplingProbabilityLevel.Moderate).ToList();
            var low = targetPredictions.Where(p => p.ProbabilityLevel == CouplingProbabilityLevel.Low).ToList();
            var alert = CrossBoundaryAlerts.FirstOrDefault(a => string.Equals(a.TargetFile, targetFile, StringComparison.OrdinalIgnoreCase));
            var owners = OwnerCoordinations
                .Where(o => o.Files.Any(f => targetPredictions.Any(tp => string.Equals(tp.File, f, StringComparison.OrdinalIgnoreCase))))
                .ToList();

            sb.AppendLine(RenderBoxForTarget(targetFile, high, moderate, low, alert, owners, plain));
        }

        return sb.ToString();
    }

    private static string RenderBoxForTarget(
        string targetFile,
        List<ImpactPredictionItem> high,
        List<ImpactPredictionItem> moderate,
        List<ImpactPredictionItem> low,
        CrossBoundaryAlert? alert,
        List<OwnerCoordination> owners,
        bool plain)
    {
        char cTopLeft = plain ? '+' : '╔';
        char cTopRight = plain ? '+' : '╗';
        char cBottomLeft = plain ? '+' : '╚';
        char cBottomRight = plain ? '+' : '╝';
        char cMidLeft = plain ? '+' : '╠';
        char cMidRight = plain ? '+' : '╣';
        char cHoriz = plain ? '-' : '═';
        char cVert = plain ? '|' : '║';

        string title = plain
            ? $"[IMPACT] Impact Analysis: {targetFile}"
            : $"🔮 Impact Analysis: {targetFile}";

        var contentLines = new List<string>();
        contentLines.Add("");

        if (high.Count == 0 && moderate.Count == 0 && low.Count == 0)
        {
            contentLines.Add("  No historical co-change couplings detected for this file.");
            contentLines.Add("");
        }
        else
        {
            if (high.Count > 0)
            {
                contentLines.Add("  HIGH PROBABILITY CO-CHANGES (>60% coupling):");
                for (int i = 0; i < high.Count; i++)
                {
                    var item = high[i];
                    string branch = (i == high.Count - 1) ? (plain ? "\\-" : "└─") : (plain ? "|-" : "├─");
                    contentLines.Add($"    {branch} {item.File,-36} {item.CouplingPercentage,3:0}% ({item.SharedCommits} shared)");
                }
                contentLines.Add("");
            }

            if (moderate.Count > 0)
            {
                contentLines.Add("  MODERATE PROBABILITY (30-60%):");
                for (int i = 0; i < moderate.Count; i++)
                {
                    var item = moderate[i];
                    string branch = (i == moderate.Count - 1) ? (plain ? "\\-" : "└─") : (plain ? "|-" : "├─");
                    contentLines.Add($"    {branch} {item.File,-36} {item.CouplingPercentage,3:0}% ({item.SharedCommits} shared)");
                }
                contentLines.Add("");
            }

            if (alert != null && alert.CrossBoundaryCount > 0)
            {
                contentLines.Add("  CROSS-BOUNDARY ALERT:");
                string warnIcon = plain ? "[!]" : "⚠️";
                contentLines.Add($"    {warnIcon} {alert.CrossBoundaryCount} of {alert.TotalCoupledCount} coupled files are in different modules");
                contentLines.Add($"    This suggests {Path.GetFileName(targetFile)} may be an architectural");
                contentLines.Add("    \"god object\" leaking across boundaries.");
                contentLines.Add("");
            }

            if (owners.Count > 0)
            {
                contentLines.Add("  OWNER COORDINATION NEEDED:");
                string ownersList = string.Join(", ", owners.Select(o => o.Message));
                contentLines.Add($"    {ownersList}");
                string notifyTarget = owners.Count > 1 ? "both" : "owner";
                contentLines.Add($"    Consider: notify {notifyTarget} before merging changes");
                contentLines.Add("");
            }
        }

        int maxLineLength = contentLines.Count > 0 ? contentLines.Max(l => l.Length) : 0;
        int boxWidth = Math.Max(60, Math.Max(title.Length + 6, maxLineLength + 4));

        var sb = new StringBuilder();
        sb.Append(cTopLeft);
        sb.Append(new string(cHoriz, boxWidth - 2));
        sb.AppendLine(cTopRight.ToString());

        string paddedTitle = $"  {title}";
        sb.Append(cVert);
        sb.Append(paddedTitle.PadRight(boxWidth - 2));
        sb.AppendLine(cVert.ToString());

        sb.Append(cMidLeft);
        sb.Append(new string(cHoriz, boxWidth - 2));
        sb.AppendLine(cMidRight.ToString());

        foreach (var line in contentLines)
        {
            sb.Append(cVert);
            sb.Append(line.PadRight(boxWidth - 2));
            sb.AppendLine(cVert.ToString());
        }

        sb.Append(cBottomLeft);
        sb.Append(new string(cHoriz, boxWidth - 2));
        sb.AppendLine(cBottomRight.ToString());

        return sb.ToString();
    }

    /// <summary>
    /// Formats a concise warning for Git hooks or quiet execution when coupling exceeds warn threshold.
    /// </summary>
    public string RenderQuietSummary(double warnThreshold = 0.5)
    {
        var sb = new StringBuilder();

        foreach (var targetFile in TargetFiles)
        {
            var heavyCouplings = Predictions
                .Where(p => string.Equals(p.TargetFile, targetFile, StringComparison.OrdinalIgnoreCase) && p.CouplingDegree >= warnThreshold)
                .OrderByDescending(p => p.CouplingDegree)
                .ToList();

            if (heavyCouplings.Count > 0)
            {
                string coupledList;
                if (heavyCouplings.Count == 1)
                {
                    coupledList = $"{heavyCouplings[0].File} ({heavyCouplings[0].CouplingPercentage:0}%)";
                }
                else if (heavyCouplings.Count == 2)
                {
                    coupledList = $"{heavyCouplings[0].File} ({heavyCouplings[0].CouplingPercentage:0}%) and {heavyCouplings[1].File} ({heavyCouplings[1].CouplingPercentage:0}%)";
                }
                else
                {
                    coupledList = $"{heavyCouplings[0].File} ({heavyCouplings[0].CouplingPercentage:0}%), {heavyCouplings[1].File} ({heavyCouplings[1].CouplingPercentage:0}%), and {heavyCouplings.Count - 2} other(s)";
                }

                sb.AppendLine($"⚠️ Your staged changes touch {Path.GetFileName(targetFile)} — {coupledList} frequently co-change with it.");
                sb.AppendLine($"   Run `gitic impact {targetFile}` for details.");
            }

            var alert = CrossBoundaryAlerts.FirstOrDefault(a => string.Equals(a.TargetFile, targetFile, StringComparison.OrdinalIgnoreCase));
            if (alert != null && alert.CrossBoundaryCount > 0)
            {
                sb.AppendLine($"⚠️ Cross-boundary alert: {alert.CrossBoundaryCount} of {alert.TotalCoupledCount} coupled files for {Path.GetFileName(targetFile)} belong to different modules ({string.Join(", ", alert.CrossBoundaryAreas)}).");
            }
        }

        return sb.ToString().TrimEnd();
    }
}

/// <summary>
/// Interface for change impact prediction based on historical temporal coupling graphs.
/// </summary>
public interface IImpactPredictor
{
    ImpactPredictionReport PredictImpact(AnalysisResult result, IEnumerable<string> targetFiles, double warnThreshold = 0.5);
    ImpactPredictionReport PredictImpact(AnalysisResult result, string targetFile, double warnThreshold = 0.5);
}

/// <summary>
/// Engine for predicting ripple-effect co-changes, boundary leaks, and ownership coordination.
/// </summary>
public class ImpactPredictor : IImpactPredictor
{
    public ImpactPredictionReport PredictImpact(AnalysisResult result, string targetFile, double warnThreshold = 0.5)
    {
        return PredictImpact(result, new[] { targetFile }, warnThreshold);
    }

    public ImpactPredictionReport PredictImpact(AnalysisResult result, IEnumerable<string> targetFiles, double warnThreshold = 0.5)
    {
        if (result == null) throw new ArgumentNullException(nameof(result));

        var targetList = (targetFiles ?? Enumerable.Empty<string>())
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Select(NormalizePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var report = new ImpactPredictionReport
        {
            TargetFiles = targetList,
            WarnThreshold = warnThreshold
        };

        if (targetList.Count == 0 || result.TemporalCoupling == null || result.TemporalCoupling.Count == 0)
        {
            return report;
        }

        var allCoupledItems = new List<ImpactPredictionItem>();

        foreach (var target in targetList)
        {
            string targetArea = ResolveArea(target, result);
            var coupledForThisTarget = new List<ImpactPredictionItem>();

            foreach (var tc in result.TemporalCoupling)
            {
                string normA = NormalizePath(tc.FileA);
                string normB = NormalizePath(tc.FileB);

                bool matchA = IsTargetMatch(normA, target);
                bool matchB = IsTargetMatch(normB, target);

                if (!matchA && !matchB) continue;

                string coupledFile = matchA ? normB : normA;
                if (string.Equals(coupledFile, target, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                double degree = tc.CouplingDegree;
                if (degree > 1.0)
                {
                    degree /= 100.0;
                }

                CouplingProbabilityLevel level;
                if (degree > 0.60)
                {
                    level = CouplingProbabilityLevel.High;
                }
                else if (degree >= 0.30)
                {
                    level = CouplingProbabilityLevel.Moderate;
                }
                else
                {
                    level = CouplingProbabilityLevel.Low;
                }

                string coupledArea = ResolveArea(coupledFile, result);
                bool isCrossBoundary = !string.Equals(targetArea, coupledArea, StringComparison.OrdinalIgnoreCase);

                var item = new ImpactPredictionItem
                {
                    TargetFile = target,
                    File = coupledFile,
                    SharedCommits = tc.SharedCommits,
                    CouplingDegree = Math.Round(degree, 4),
                    ProbabilityLevel = level,
                    TargetArea = targetArea,
                    Area = coupledArea,
                    IsCrossBoundary = isCrossBoundary
                };

                EnrichItemWithContributorInfo(item, result);

                // If duplicate coupled file for this target, keep higher coupling degree
                var existing = coupledForThisTarget.FirstOrDefault(p => string.Equals(p.File, coupledFile, StringComparison.OrdinalIgnoreCase));
                if (existing == null)
                {
                    coupledForThisTarget.Add(item);
                }
                else if (item.CouplingDegree > existing.CouplingDegree)
                {
                    coupledForThisTarget.Remove(existing);
                    coupledForThisTarget.Add(item);
                }
            }

            // Check Cross-boundary alert for this target
            var crossBoundaryFiles = coupledForThisTarget.Where(p => p.IsCrossBoundary).ToList();
            if (crossBoundaryFiles.Count > 0)
            {
                var distinctAreas = crossBoundaryFiles.Select(p => p.Area).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                string alertMsg = $"⚠️ {crossBoundaryFiles.Count} of {coupledForThisTarget.Count} coupled files are in different modules ({string.Join(", ", distinctAreas)}). This suggests {Path.GetFileName(target)} may be an architectural \"god object\" leaking across boundaries.";

                report.CrossBoundaryAlerts.Add(new CrossBoundaryAlert
                {
                    TargetFile = target,
                    TargetArea = targetArea,
                    CrossBoundaryCount = crossBoundaryFiles.Count,
                    TotalCoupledCount = coupledForThisTarget.Count,
                    CrossBoundaryFiles = crossBoundaryFiles.Select(p => p.File).ToList(),
                    CrossBoundaryAreas = distinctAreas,
                    Message = alertMsg
                });
            }

            allCoupledItems.AddRange(coupledForThisTarget);
        }

        // Rank all predictions strictly by CouplingDegree descending, then SharedCommits descending, then File name ascending
        report.Predictions = allCoupledItems
            .OrderByDescending(p => p.CouplingDegree)
            .ThenByDescending(p => p.SharedCommits)
            .ThenBy(p => p.File, StringComparer.OrdinalIgnoreCase)
            .ToList();

        report.HighProbabilityPredictions = report.Predictions
            .Where(p => p.ProbabilityLevel == CouplingProbabilityLevel.High)
            .ToList();

        report.ModerateProbabilityPredictions = report.Predictions
            .Where(p => p.ProbabilityLevel == CouplingProbabilityLevel.Moderate)
            .ToList();

        report.LowProbabilityPredictions = report.Predictions
            .Where(p => p.ProbabilityLevel == CouplingProbabilityLevel.Low)
            .ToList();

        // Extract owner coordination list for heavily coupled files (degree >= warnThreshold or High/Moderate)
        report.OwnerCoordinations = BuildOwnerCoordinations(report.Predictions, warnThreshold);

        return report;
    }

    private static bool IsTargetMatch(string filePath, string target)
    {
        if (string.Equals(filePath, target, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!target.Contains('/') && (filePath.EndsWith("/" + target, StringComparison.OrdinalIgnoreCase) || string.Equals(filePath, target, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (!filePath.Contains('/') && (target.EndsWith("/" + filePath, StringComparison.OrdinalIgnoreCase) || string.Equals(filePath, target, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }

    public static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        string normalized = path.Replace('\\', '/').Trim();
        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized.Substring(2);
        }
        return normalized.Trim('/');
    }

    public static string ResolveArea(string filePath, AnalysisResult? result)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return ".";
        string norm = NormalizePath(filePath);

        if (result?.Files != null)
        {
            var fileMetric = result.Files.FirstOrDefault(f =>
                string.Equals(NormalizePath(f.Path), norm, StringComparison.OrdinalIgnoreCase) ||
                NormalizePath(f.Path).EndsWith("/" + norm, StringComparison.OrdinalIgnoreCase) ||
                norm.EndsWith("/" + NormalizePath(f.Path), StringComparison.OrdinalIgnoreCase));

            if (fileMetric != null && !string.IsNullOrWhiteSpace(fileMetric.Area))
            {
                return fileMetric.Area;
            }
        }

        if (result?.Areas != null && result.Areas.Count > 0)
        {
            var matchedArea = result.Areas
                .Where(a => !string.IsNullOrEmpty(a.Area) && a.Area != "." &&
                            (norm.StartsWith(NormalizePath(a.Area) + "/", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(norm, NormalizePath(a.Area), StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(a => a.Area.Length)
                .FirstOrDefault();

            if (matchedArea != null)
            {
                return matchedArea.Area;
            }
        }

        var segments = norm.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length <= 1)
        {
            return ".";
        }

        int takeCount = (segments.Length >= 3 && (segments[0].Equals("src", StringComparison.OrdinalIgnoreCase) || segments[0].Equals("tests", StringComparison.OrdinalIgnoreCase))) ? 2 : 1;
        return string.Join("/", segments.Take(Math.Min(takeCount, segments.Length - 1)));
    }

    private static void EnrichItemWithContributorInfo(ImpactPredictionItem item, AnalysisResult result)
    {
        if (result?.Files == null) return;

        var fileMetric = result.Files.FirstOrDefault(f =>
            string.Equals(NormalizePath(f.Path), item.File, StringComparison.OrdinalIgnoreCase) ||
            NormalizePath(f.Path).EndsWith("/" + item.File, StringComparison.OrdinalIgnoreCase) ||
            item.File.EndsWith("/" + NormalizePath(f.Path), StringComparison.OrdinalIgnoreCase));

        if (fileMetric?.Contributors != null && fileMetric.Contributors.Count > 0)
        {
            var top = fileMetric.Contributors
                .OrderByDescending(c => c.ActivityShare)
                .ThenByDescending(c => c.Activity)
                .FirstOrDefault();

            if (top != null && !string.IsNullOrWhiteSpace(top.Name))
            {
                item.TopContributor = top.Name;
                item.TopContributorEmail = top.Email;
                item.TopContributorShare = Math.Round(top.ActivityShare, 3);
            }
        }
    }

    private static List<OwnerCoordination> BuildOwnerCoordinations(List<ImpactPredictionItem> predictions, double warnThreshold)
    {
        var heavilyCoupled = predictions
            .Where(p => !string.IsNullOrWhiteSpace(p.TopContributor) &&
                        (p.CouplingDegree >= warnThreshold || p.ProbabilityLevel != CouplingProbabilityLevel.Low))
            .ToList();

        var coordinations = new List<OwnerCoordination>();

        var grouped = heavilyCoupled
            .GroupBy(p => p.TopContributor!, StringComparer.OrdinalIgnoreCase);

        foreach (var group in grouped)
        {
            string owner = group.Key;
            string? email = group.First().TopContributorEmail;
            var files = group.Select(p => p.File).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            string fileNames = string.Join(", ", files.Select(Path.GetFileName));

            coordinations.Add(new OwnerCoordination
            {
                Owner = owner,
                Email = email,
                Files = files,
                Message = $"{owner} (owns {fileNames})"
            });
        }

        return coordinations;
    }
}
