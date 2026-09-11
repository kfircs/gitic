using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json.Serialization;

namespace Gitic;

/// <summary>
/// Trajectory and qualitative tone for natural language narrative reporting.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NarrativeTone
{
    Improving,
    Stable,
    Regressing
}

/// <summary>
/// Structured natural language narrative report synthesizing codebase health,
/// delivery velocity, ownership risks, and prioritized recommendations for stakeholders.
/// </summary>
public class NarrativeReport
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("executive_summary_paragraph")]
    public string ExecutiveSummaryParagraph { get; set; } = string.Empty;

    [JsonPropertyName("velocity_and_delivery_paragraph")]
    public string VelocityAndDeliveryParagraph { get; set; } = string.Empty;

    [JsonPropertyName("ownership_and_risk_paragraph")]
    public string OwnershipAndRiskParagraph { get; set; } = string.Empty;

    [JsonPropertyName("recommendations_paragraph")]
    public string RecommendationsParagraph { get; set; } = string.Empty;

    [JsonPropertyName("tone")]
    public NarrativeTone Tone { get; set; } = NarrativeTone.Stable;

    [JsonPropertyName("current_dx_index")]
    public double CurrentDxIndex { get; set; }

    [JsonPropertyName("previous_dx_index")]
    public double? PreviousDxIndex { get; set; }

    [JsonPropertyName("delta_dx_index")]
    public double? DeltaDxIndex { get; set; }

    [JsonPropertyName("generated_at")]
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("key_recommendations")]
    public List<string> KeyRecommendations { get; set; } = new();

    [JsonPropertyName("summary_deltas")]
    public List<MetricDelta> SummaryDeltas { get; set; } = new();
}

public partial class AnalysisResult
{
    [JsonPropertyName("narrative")]
    public NarrativeReport? Narrative { get; set; }
}

/// <summary>
/// Interface for natural language narrative generation across markdown and HTML formats.
/// </summary>
public interface INarrativeEngine
{
    NarrativeReport GenerateNarrative(AnalysisResult result, BaselineSnapshot? previous = null);
    string GenerateProseMarkdown(NarrativeReport report);
    string GenerateProseHtml(NarrativeReport report);
}

/// <summary>
/// Rule-based template engine that converts quantitative gitic metrics and baseline deltas
/// into human-readable executive prose narratives for engineering leadership and stakeholders.
/// </summary>
public class NarrativeEngine : INarrativeEngine
{
    public NarrativeReport GenerateNarrative(AnalysisResult result, BaselineSnapshot? previous = null)
    {
        if (result == null) throw new ArgumentNullException(nameof(result));

        DateTimeOffset generatedAt = ResolveGeneratedAt(result);
        string title = GenerateTitle(result, previous, generatedAt);

        // Compute DX Indices
        double currentDx = ResolveCurrentDxIndex(result);
        double? previousDx = previous != null ? ResolveBaselineDxIndex(previous) : null;
        double? deltaDx = previousDx.HasValue ? Math.Round(currentDx - previousDx.Value, 1) : null;

        // Determine Tone
        NarrativeTone tone = DetermineTone(currentDx, previousDx, deltaDx);

        // Collect Deltas & Key Metrics
        var summaryDeltas = ComputeSummaryDeltas(result, previous);

        // Generate Paragraphs
        string execSummary = GenerateExecutiveSummaryParagraph(tone, currentDx, previousDx, deltaDx);
        string velocity = GenerateVelocityAndDeliveryParagraph(result, previous);
        string ownership = GenerateOwnershipAndRiskParagraph(result, previous);
        var (recommendations, actionList) = GenerateRecommendations(result, previous, currentDx, previousDx);

        return new NarrativeReport
        {
            Title = title,
            ExecutiveSummaryParagraph = execSummary,
            VelocityAndDeliveryParagraph = velocity,
            OwnershipAndRiskParagraph = ownership,
            RecommendationsParagraph = recommendations,
            Tone = tone,
            CurrentDxIndex = currentDx,
            PreviousDxIndex = previousDx,
            DeltaDxIndex = deltaDx,
            GeneratedAt = generatedAt,
            KeyRecommendations = actionList,
            SummaryDeltas = summaryDeltas
        };
    }

    public string GenerateProseMarkdown(NarrativeReport report)
    {
        if (report == null) throw new ArgumentNullException(nameof(report));

        var sb = new StringBuilder();
        sb.AppendLine($"# 📝 {report.Title}");
        sb.AppendLine();
        sb.AppendLine("## Executive Summary");
        sb.AppendLine(report.ExecutiveSummaryParagraph);
        sb.AppendLine();
        sb.AppendLine("## Velocity & Delivery");
        sb.AppendLine(report.VelocityAndDeliveryParagraph);
        sb.AppendLine();
        sb.AppendLine("## Ownership & Risk Analysis");
        sb.AppendLine(report.OwnershipAndRiskParagraph);
        sb.AppendLine();
        sb.AppendLine("## Recommended Actions");
        sb.AppendLine(report.RecommendationsParagraph);

        return sb.ToString();
    }

    public string GenerateProseHtml(NarrativeReport report)
    {
        if (report == null) throw new ArgumentNullException(nameof(report));

        string badgeClass = report.Tone switch
        {
            NarrativeTone.Improving => "badge-improving",
            NarrativeTone.Regressing => "badge-regressing",
            _ => "badge-stable"
        };

        string toneLabel = report.Tone.ToString();

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine($"  <title>{EscapeHtml(report.Title)}</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine("    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; margin: 2rem; background: #0f172a; color: #f8fafc; line-height: 1.6; }");
        sb.AppendLine("    .narrative-card { background: #1e293b; border-radius: 12px; box-shadow: 0 10px 25px -5px rgba(0, 0, 0, 0.5); max-width: 840px; margin: 0 auto; overflow: hidden; border: 1px solid #334155; }");
        sb.AppendLine("    .narrative-header { background: #090d16; padding: 1.5rem 2rem; border-bottom: 1px solid #334155; display: flex; justify-content: space-between; align-items: center; }");
        sb.AppendLine("    .narrative-title { margin: 0; font-size: 1.4rem; font-weight: 700; color: #f8fafc; }");
        sb.AppendLine("    .narrative-body { padding: 2rem; }");
        sb.AppendLine("    .narrative-section { margin-bottom: 1.75rem; }");
        sb.AppendLine("    .narrative-section h2 { font-size: 1.15rem; font-weight: 700; margin: 0 0 0.5rem 0; color: #38bdf8; display: flex; align-items: center; gap: 0.5rem; }");
        sb.AppendLine("    .narrative-section p { margin: 0; color: #cbd5e1; font-size: 0.95rem; line-height: 1.65; white-space: pre-line; }");
        sb.AppendLine("    .badge { display: inline-block; padding: 0.3rem 0.75rem; font-size: 0.8rem; font-weight: 700; border-radius: 9999px; letter-spacing: 0.05em; }");
        sb.AppendLine("    .badge-improving { background: rgba(34, 197, 94, 0.2); color: #4ade80; border: 1px solid #22c55e; }");
        sb.AppendLine("    .badge-stable { background: rgba(56, 189, 248, 0.2); color: #38bdf8; border: 1px solid #0284c7; }");
        sb.AppendLine("    .badge-regressing { background: rgba(239, 68, 68, 0.2); color: #f87171; border: 1px solid #ef4444; }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <div class=\"narrative-card\">");
        sb.AppendLine("    <div class=\"narrative-header\">");
        sb.AppendLine($"      <h1 class=\"narrative-title\">📝 {EscapeHtml(report.Title)}</h1>");
        sb.AppendLine($"      <span class=\"badge {badgeClass}\">{EscapeHtml(toneLabel)}</span>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div class=\"narrative-body\">");
        sb.AppendLine("      <div class=\"narrative-section\">");
        sb.AppendLine("        <h2>Executive Summary</h2>");
        sb.AppendLine($"        <p>{EscapeHtml(report.ExecutiveSummaryParagraph)}</p>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div class=\"narrative-section\">");
        sb.AppendLine("        <h2>Velocity & Delivery</h2>");
        sb.AppendLine($"        <p>{EscapeHtml(report.VelocityAndDeliveryParagraph)}</p>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div class=\"narrative-section\">");
        sb.AppendLine("        <h2>Ownership & Risk Analysis</h2>");
        sb.AppendLine($"        <p>{EscapeHtml(report.OwnershipAndRiskParagraph)}</p>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div class=\"narrative-section\">");
        sb.AppendLine("        <h2>Recommended Focus & Actions</h2>");
        sb.AppendLine($"        <p>{EscapeHtml(report.RecommendationsParagraph)}</p>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    public string EmbedIntoMarkdown(string baseMarkdown, NarrativeReport report)
    {
        if (string.IsNullOrEmpty(baseMarkdown)) return GenerateProseMarkdown(report);
        if (report == null) return baseMarkdown;

        var prose = new StringBuilder();
        prose.AppendLine("## 📝 Executive Narrative Summary");
        prose.AppendLine();
        prose.AppendLine(report.ExecutiveSummaryParagraph);
        prose.AppendLine();
        prose.AppendLine("### Velocity & Delivery");
        prose.AppendLine(report.VelocityAndDeliveryParagraph);
        prose.AppendLine();
        prose.AppendLine("### Ownership & Risk Analysis");
        prose.AppendLine(report.OwnershipAndRiskParagraph);
        prose.AppendLine();
        prose.AppendLine("### Recommended Actions");
        prose.AppendLine(report.RecommendationsParagraph);
        prose.AppendLine();

        int overviewIdx = baseMarkdown.IndexOf("## 📈 Repository Overview", StringComparison.OrdinalIgnoreCase);
        if (overviewIdx >= 0)
        {
            return baseMarkdown.Insert(overviewIdx, prose.ToString() + "\n");
        }

        return prose.ToString() + "\n" + baseMarkdown;
    }

    public string EmbedIntoHtml(string baseHtml, NarrativeReport report)
    {
        if (string.IsNullOrEmpty(baseHtml)) return GenerateProseHtml(report);
        if (report == null) return baseHtml;

        string proseBlock = $@"
    <div class=""narrative-embed"" style=""background: #1e293b; padding: 1.5rem; border-radius: 8px; margin-bottom: 2rem; border: 1px solid #334155;"">
      <h2 style=""color: #38bdf8; margin-top: 0;"">📝 Executive Narrative Summary</h2>
      <p style=""color: #cbd5e1; line-height: 1.6;"">{EscapeHtml(report.ExecutiveSummaryParagraph)}</p>
      <h3 style=""color: #94a3b8; font-size: 1rem; margin-top: 1rem;"">Velocity &amp; Delivery</h3>
      <p style=""color: #cbd5e1; line-height: 1.6;"">{EscapeHtml(report.VelocityAndDeliveryParagraph)}</p>
      <h3 style=""color: #94a3b8; font-size: 1rem; margin-top: 1rem;"">Ownership &amp; Risk</h3>
      <p style=""color: #cbd5e1; line-height: 1.6;"">{EscapeHtml(report.OwnershipAndRiskParagraph)}</p>
      <h3 style=""color: #94a3b8; font-size: 1rem; margin-top: 1rem;"">Recommended Actions</h3>
      <p style=""color: #cbd5e1; line-height: 1.6; white-space: pre-line;"">{EscapeHtml(report.RecommendationsParagraph)}</p>
    </div>";

        int bodyIdx = baseHtml.IndexOf("<body", StringComparison.OrdinalIgnoreCase);
        if (bodyIdx >= 0)
        {
            int closeBodyTag = baseHtml.IndexOf('>', bodyIdx);
            if (closeBodyTag >= 0)
            {
                return baseHtml.Insert(closeBodyTag + 1, "\n" + proseBlock);
            }
        }

        return proseBlock + baseHtml;
    }

    private static DateTimeOffset ResolveGeneratedAt(AnalysisResult result)
    {
        if (!string.IsNullOrEmpty(result.Analysis?.GeneratedAt) &&
            DateTimeOffset.TryParse(result.Analysis.GeneratedAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }
        return DateTimeOffset.UtcNow;
    }

    private static string GenerateTitle(AnalysisResult result, BaselineSnapshot? previous, DateTimeOffset timestamp)
    {
        string monthYear = timestamp.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        string repoName = !string.IsNullOrEmpty(result.Analysis?.RepoRoot)
            ? ReportUtils.GetRepositoryName(result.Analysis.RepoRoot)
            : string.Empty;

        if (!string.IsNullOrEmpty(repoName))
        {
            return $"Repository Health Summary: {repoName} — {monthYear}";
        }

        return $"Repository Health Summary — {monthYear}";
    }

    private static double ResolveCurrentDxIndex(AnalysisResult result)
    {
        if (result.DxIndex != null)
        {
            return Math.Round(result.DxIndex.CompositeScore, 1);
        }

        try
        {
            var calc = DxIndexCalculator.Compute(result);
            return Math.Round(calc.CompositeScore, 1);
        }
        catch
        {
            return 70.0;
        }
    }

    private static double ResolveBaselineDxIndex(BaselineSnapshot snapshot)
    {
        if (snapshot.Metrics != null)
        {
            if (snapshot.Metrics.TryGetValue("dx_index", out var dx)) return Math.Round(dx, 1);
            if (snapshot.Metrics.TryGetValue("dx", out var dx2)) return Math.Round(dx2, 1);
        }

        try
        {
            var dummyResult = new AnalysisResult
            {
                Files = snapshot.Files.Values.Select(f => new FileMetric
                {
                    Path = f.Path,
                    Area = f.Area,
                    Touches = f.Touches,
                    Churn = f.Churn,
                    Lines = f.Lines,
                    HeatScore = f.HeatScore,
                    AttentionScore = f.AttentionScore,
                    ReworkRate = f.ReworkRate
                }).ToList(),
                Areas = snapshot.Areas.Values.Select(a => new AreaMetric
                {
                    Area = a.Area,
                    Touches = a.Touches,
                    Churn = a.Churn,
                    FileCount = a.FileCount,
                    HeatScore = a.HeatScore,
                    AttentionScore = a.AttentionScore,
                    ReworkRate = a.ReworkRate
                }).ToList(),
                LeadTimes = new LeadTimesInfo
                {
                    AverageLeadTimeHours = snapshot.Summary.AverageLeadTimeHours
                },
                CuratedReports = new CuratedReports
                {
                    CodeRot = new CodeRotMetric
                    {
                        ZombieFileCount = snapshot.Summary.ZombieFiles,
                        ZombieLines = snapshot.Summary.ZombieLines
                    },
                    WorkClassification = new WorkClassificationMetrics
                    {
                        Features = snapshot.Summary.Features,
                        Bugs = snapshot.Summary.Bugs,
                        TechnicalDebt = snapshot.Summary.TechnicalDebt,
                        Chores = snapshot.Summary.Chores
                    }
                }
            };

            var calc = DxIndexCalculator.Compute(dummyResult);
            return Math.Round(calc.CompositeScore, 1);
        }
        catch
        {
            return 70.0;
        }
    }

    private static NarrativeTone DetermineTone(double currentDx, double? previousDx, double? deltaDx)
    {
        if (deltaDx.HasValue)
        {
            if (deltaDx.Value >= 1.5) return NarrativeTone.Improving;
            if (deltaDx.Value <= -1.5) return NarrativeTone.Regressing;
            return NarrativeTone.Stable;
        }

        if (currentDx >= 75.0) return NarrativeTone.Improving;
        if (currentDx < 55.0) return NarrativeTone.Regressing;
        return NarrativeTone.Stable;
    }

    private static List<MetricDelta> ComputeSummaryDeltas(AnalysisResult result, BaselineSnapshot? previous)
    {
        var deltas = new List<MetricDelta>();
        if (previous == null) return deltas;

        double currP50 = result.LeadTimes?.Merges?.Count > 0
            ? Percentile(result.LeadTimes.Merges.Select(m => m.LeadTimeHours), 0.50)
            : (result.LeadTimes?.AverageLeadTimeHours ?? 0.0);
        double prevP50 = previous.Summary?.AverageLeadTimeHours ??
            (previous.Metrics.TryGetValue("p50_lead_time", out var p50v) ? p50v : 0.0);

        if (currP50 > 0 || prevP50 > 0)
        {
            deltas.Add(new MetricDelta("p50_lead_time", prevP50, currP50));
        }

        double currRework = GetAverageReworkRate(result);
        double prevRework = previous.Metrics.TryGetValue("rework_rate", out var rwv) ? rwv : 0.0;
        if (currRework > 0 || prevRework > 0)
        {
            deltas.Add(new MetricDelta("rework_rate", prevRework, currRework));
        }

        double currZombies = result.CuratedReports?.CodeRot?.ZombieFileCount ?? 0;
        double prevZombies = previous.Summary?.ZombieFiles ??
            (previous.Metrics.TryGetValue("zombie_files", out var zv) ? zv : 0.0);
        if (currZombies > 0 || prevZombies > 0)
        {
            deltas.Add(new MetricDelta("zombie_files", prevZombies, currZombies));
        }

        double currCommits = result.Analysis?.CommitCount ?? 0;
        double prevCommits = previous.Summary?.CommitCount ?? 0;
        if (currCommits > 0 || prevCommits > 0)
        {
            deltas.Add(new MetricDelta("commit_count", prevCommits, currCommits));
        }

        return deltas;
    }

    private static string GenerateExecutiveSummaryParagraph(NarrativeTone tone, double currentDx, double? previousDx, double? deltaDx)
    {
        if (previousDx.HasValue && deltaDx.HasValue)
        {
            string sign = deltaDx.Value >= 0 ? $"+{deltaDx.Value:F0}" : $"{deltaDx.Value:F0}";
            return tone switch
            {
                NarrativeTone.Improving =>
                    $"Your team's overall codebase health **improved notably this period**. The Developer Experience (DX) Index rose from {previousDx.Value:F0} to {currentDx:F0} ({sign} pts), reflecting enhanced delivery flow, reduced rework volatility, and proactive technical debt management across core subsystems.",
                NarrativeTone.Regressing =>
                    $"The codebase experienced an **emerging health regression this period**, with the Developer Experience (DX) Index declining from {previousDx.Value:F0} to {currentDx:F0} ({sign} pts). Accumulating technical debt, single-owner knowledge silos, and elevated change volatility are introducing friction into ongoing engineering velocity.",
                _ =>
                    $"The codebase maintained a **consistent and stable health profile**, with the Developer Experience (DX) Index holding steady at {currentDx:F0}/100 (previously {previousDx.Value:F0}). Subsystem stability, commit cadence, and architectural integrity remain balanced across primary development streams."
            };
        }

        return tone switch
        {
            NarrativeTone.Improving =>
                $"The codebase currently exhibits a **healthy and improving engineering state**, achieving a Developer Experience (DX) Index of {currentDx:F0}/100. Core workflows demonstrate solid delivery flow, low rework volatility, and well-managed module complexity across the repository.",
            NarrativeTone.Regressing =>
                $"The codebase is currently experiencing **elevated engineering friction**, reflected in a Developer Experience (DX) Index of {currentDx:F0}/100. Critical bottlenecks in single-owner knowledge silos, elevated rework, and review latency require targeted intervention.",
            _ =>
                $"The codebase reflects a **stable engineering baseline**, recording an overall Developer Experience (DX) Index of {currentDx:F0}/100. Core delivery throughput and subsystem ownership metrics remain consistent across primary areas."
        };
    }

    private static string GenerateVelocityAndDeliveryParagraph(AnalysisResult result, BaselineSnapshot? previous)
    {
        double currP50 = 0.0;
        double currP90 = 0.0;
        int mergesCount = result.LeadTimes?.Merges?.Count ?? 0;

        if (result.LeadTimes?.Merges != null && result.LeadTimes.Merges.Count > 0)
        {
            var leadTimes = result.LeadTimes.Merges.Select(m => m.LeadTimeHours).ToList();
            currP50 = Percentile(leadTimes, 0.50);
            currP90 = Percentile(leadTimes, 0.90);
        }
        else if (result.LeadTimes != null && result.LeadTimes.AverageLeadTimeHours > 0)
        {
            currP50 = result.LeadTimes.AverageLeadTimeHours;
            currP90 = currP50 * 1.5;
        }

        double prevP50 = previous?.Summary?.AverageLeadTimeHours ??
            (previous != null && previous.Metrics.TryGetValue("p50_lead_time", out var p50v) ? p50v : 0.0);
        double prevP90 = previous != null && previous.Metrics.TryGetValue("p90_lead_time", out var p90v) ? p90v : 0.0;

        string leadSentence;
        if (previous != null && prevP50 > 0 && currP50 > 0)
        {
            if (currP50 < prevP50)
            {
                double reductionPct = ((prevP50 - currP50) / prevP50) * 100.0;
                leadSentence = $"Your team's delivery velocity **improved notably**: the median lead time from branch creation to merge dropped from {prevP50:F0} hours to {currP50:F0} hours ({reductionPct:F0}% reduction), with P90 tail latency falling to {currP90:F0} hours.";
            }
            else if (currP50 > prevP50)
            {
                double increasePct = ((currP50 - prevP50) / prevP50) * 100.0;
                leadSentence = $"Delivery flow **experienced latency regression**: median merge lead time increased from {prevP50:F0} hours to {currP50:F0} hours (+{increasePct:F0}%), with P90 tail latency expanding to {currP90:F0} hours, signaling review bottlenecks or outsized pull requests.";
            }
            else
            {
                leadSentence = $"Delivery lead time held steady with a median turnaround of {currP50:F0} hours and P90 tail latency at {currP90:F0} hours.";
            }
        }
        else if (currP50 > 0)
        {
            leadSentence = $"Delivery velocity is tracking at a median merge lead time of {currP50:F0} hours, with P90 tail latency recorded at {currP90:F0} hours across {mergesCount} merged pull requests.";
        }
        else
        {
            leadSentence = $"Repository commit cadence tracked {result.Analysis?.CommitCount ?? 0} commits across {result.Files?.Count ?? 0} analyzed files.";
        }

        // Work classification
        var work = result.CuratedReports?.WorkClassification;
        string workSentence = string.Empty;
        if (work != null && (work.Features > 0 || work.Bugs > 0 || work.TechnicalDebt > 0 || work.Chores > 0))
        {
            int total = work.Features + work.Bugs + work.TechnicalDebt + work.Chores;
            if (total > 0)
            {
                double featPct = (double)work.Features / total * 100.0;
                double bugPct = (double)work.Bugs / total * 100.0;
                double debtPct = (double)work.TechnicalDebt / total * 100.0;
                workSentence = $" Development activity comprised {work.Features} feature enhancements ({featPct:F0}%), {work.Bugs} bug fixes ({bugPct:F0}%), and {work.TechnicalDebt} technical debt items ({debtPct:F0}%).";
            }
        }

        // High volume / strain
        int highVol = result.CuratedReports?.AiCodeStrain?.HighVolumeCommits ?? 0;
        string strainSentence = highVol > 0
            ? $" Additionally, {highVol} high-volume commit batches were detected, suggesting opportunities for finer-grained pull requests."
            : string.Empty;

        return $"{leadSentence}{workSentence}{strainSentence}".Trim();
    }

    private static string GenerateOwnershipAndRiskParagraph(AnalysisResult result, BaselineSnapshot? previous)
    {
        // Top area by touches / churn
        var topArea = result.Areas?.OrderByDescending(a => a.Touches).FirstOrDefault();
        double areaHd = 0.0;
        if (topArea != null && result.Files != null && result.Files.Count > 0)
        {
            var areaFiles = result.Files.Where(f => string.Equals(f.Area, topArea.Area, StringComparison.OrdinalIgnoreCase)).ToList();
            if (areaFiles.Count > 0)
            {
                int hotspots = areaFiles.Count(f => f.AttentionScore >= 60.0 || (f.AttentionScore == 0 && f.HeatScore >= 60.0));
                areaHd = (double)hotspots / areaFiles.Count * 100.0;
            }
        }

        double prevAreaHd = 0.0;
        if (previous != null && topArea != null && previous.Areas.TryGetValue(topArea.Area, out var pArea))
        {
            prevAreaHd = pArea.Metrics.TryGetValue("hotspot_density", out var phd) ? phd * 100.0 : 0.0;
        }

        string hotspotSentence;
        if (topArea != null)
        {
            if (previous != null && areaHd > prevAreaHd && areaHd >= 30.0)
            {
                hotspotSentence = $"However, **code ownership risk is growing**. The `{topArea.Area}` module's hotspot density increased from {prevAreaHd:F0}% to {areaHd:F0}% — meaning nearly half of its files are receiving unusually heavy, concentrated changes.";
            }
            else if (areaHd >= 30.0)
            {
                hotspotSentence = $"Subsystem analysis indicates elevated change concentration in `{topArea.Area}`, where hotspot density reached {areaHd:F0}%.";
            }
            else
            {
                hotspotSentence = $"Change distribution across subsystems remains balanced, with `{topArea.Area}` tracking a moderate hotspot density of {areaHd:F0}%.";
            }
        }
        else
        {
            hotspotSentence = "Code modification activity is distributed across the codebase.";
        }

        // Prominent silo file
        var topSiloFile = result.Files?
            .Where(f => f.KnowledgeSilo?.IsSilo == true || (f.KnowledgeSilo?.TopOwnerShare ?? 0) > 0.70 || f.ContributorCount == 1)
            .OrderByDescending(f => f.AttentionScore > 0 ? f.AttentionScore : f.HeatScore)
            .FirstOrDefault();

        string siloSentence = string.Empty;
        if (topSiloFile != null)
        {
            string authorName = topSiloFile.Contributors?.OrderByDescending(c => c.ActivityShare).FirstOrDefault()?.Name
                ?? (topSiloFile.Contributors?.FirstOrDefault()?.Name ?? "a single contributor");
            double ownerShare = (topSiloFile.KnowledgeSilo?.TopOwnerShare ?? topSiloFile.Contributors?.FirstOrDefault()?.ActivityShare ?? 0.80) * 100.0;
            int truckFactor = topSiloFile.KnowledgeSilo?.TruckFactor ?? 1;

            siloSentence = $" More concerning, `{topSiloFile.Path}` is now a single-owner knowledge silo: {authorName} authored {ownerShare:F0}% of all changes (truck factor = {truckFactor}), and no other team member has meaningful familiarity with it. If {authorName} is unavailable, downstream work across dependent modules faces potential blockage.";
        }
        else
        {
            siloSentence = " Knowledge distribution is healthy across contributors with no single-owner maintainer bottlenecks detected.";
        }

        // Rework rate
        double avgRework = GetAverageReworkRate(result) * 100.0;
        string reworkSentence = string.Empty;
        if (avgRework > 20.0)
        {
            reworkSentence = $" The repository exhibits an elevated average rework rate of {avgRework:F1}%, indicating that {avgRework:F0}% of modifications represent follow-up fixes or revisions to recently touched code.";
        }
        else if (avgRework > 0.0)
        {
            reworkSentence = $" Code stability remains solid with an average rework rate of {avgRework:F1}%.";
        }

        // Architectural Coupling
        string couplingSentence = string.Empty;
        var coupledPair = result.TemporalCoupling?
            .OrderByDescending(c => c.CouplingDegree)
            .FirstOrDefault(c => c.CouplingDegree >= 0.40);

        if (coupledPair != null)
        {
            couplingSentence = $" Furthermore, temporal coupling analysis identified tight co-change between `{coupledPair.FileA}` and `{coupledPair.FileB}` ({coupledPair.CouplingDegree * 100:F0}% coupling degree), which may indicate architectural boundary leakage.";
        }

        return $"{hotspotSentence}{siloSentence}{reworkSentence}{couplingSentence}".Trim();
    }

    private static (string RecommendationsText, List<string> ActionList) GenerateRecommendations(
        AnalysisResult result,
        BaselineSnapshot? previous,
        double currentDx,
        double? previousDx)
    {
        var actions = new List<string>();

        // 1. Knowledge Silo Pair Programming
        var topSiloFile = result.Files?
            .Where(f => f.KnowledgeSilo?.IsSilo == true || (f.KnowledgeSilo?.TopOwnerShare ?? 0) > 0.70 || f.ContributorCount == 1)
            .OrderByDescending(f => f.AttentionScore > 0 ? f.AttentionScore : f.HeatScore)
            .FirstOrDefault();

        if (topSiloFile != null)
        {
            string topAuthor = topSiloFile.Contributors?.OrderByDescending(c => c.ActivityShare).FirstOrDefault()?.Name ?? "the primary author";
            var secondaryContributor = result.Contributors?.FirstOrDefault(c => !string.Equals(c.Name, topAuthor, StringComparison.OrdinalIgnoreCase))?.Name ?? "a secondary engineer";
            actions.Add($"Schedule 2-3 pair programming sessions between {topAuthor} and {secondaryContributor} on `{topSiloFile.Path}` to distribute knowledge.");
        }

        // 2. Code Freshness / Zombie Cleanup
        int currZombies = result.CuratedReports?.CodeRot?.ZombieFileCount ?? 0;
        int prevZombies = previous?.Summary?.ZombieFiles ?? 0;
        if (previous != null && prevZombies > currZombies)
        {
            int cleaned = prevZombies - currZombies;
            actions.Add($"The {cleaned} zombie files cleaned up this cycle brought your code freshness score up — continue with another cleanup pass targeting stale files ({currZombies} remaining).");
        }
        else if (currZombies > 0)
        {
            actions.Add($"Schedule a dead code cleanup pass targeting {currZombies} identified zombie files ({result.CuratedReports?.CodeRot?.ZombieLines ?? 0} lines) to improve code freshness.");
        }

        // 3. Temporal Coupling / Architectural Boundary
        var coupledPair = result.TemporalCoupling?
            .OrderByDescending(c => c.CouplingDegree)
            .FirstOrDefault(c => c.CouplingDegree >= 0.40);

        if (coupledPair != null)
        {
            actions.Add($"Investigate why `{coupledPair.FileA}` is becoming tightly coupled with `{coupledPair.FileB}` — this may indicate an architectural boundary leak.");
        }

        // 4. Hotspot / Rework Focus
        var topArea = result.Areas?.OrderByDescending(a => a.Touches).FirstOrDefault();
        double avgRework = GetAverageReworkRate(result) * 100.0;
        if (topArea != null && avgRework > 15.0)
        {
            actions.Add($"Pair on `{topArea.Area}` hotspots to reduce concentrated churn and lower the {avgRework:F0}% rework rate.");
        }

        // 5. Delivery Latency
        double currP50 = result.LeadTimes?.AverageLeadTimeHours ?? 0.0;
        if (currP50 > 24.0)
        {
            actions.Add("Decompose large pull requests and streamline review turnaround to lower median merge lead time.");
        }

        // Fallback default actions if none triggered
        if (actions.Count == 0)
        {
            actions.Add("Maintain proactive code reviews and keep pull requests small and incremental.");
            actions.Add("Continue monitoring hotspot density and test coverage across active modules.");
        }

        var sb = new StringBuilder();
        sb.AppendLine("**Recommended focus for the upcoming cycle:**");
        for (int i = 0; i < actions.Count; i++)
        {
            sb.AppendLine($"{i + 1}. {actions[i]}");
        }

        return (sb.ToString().TrimEnd(), actions);
    }

    private static double GetAverageReworkRate(AnalysisResult result)
    {
        var fileList = result.Files?.Where(f => f.ReworkRate.HasValue).ToList();
        if (fileList != null && fileList.Count > 0)
        {
            return fileList.Average(f => f.ReworkRate!.Value);
        }

        var areaList = result.Areas?.Where(a => a.ReworkRate.HasValue).ToList();
        if (areaList != null && areaList.Count > 0)
        {
            return areaList.Average(a => a.ReworkRate!.Value);
        }

        return 0.0;
    }

    private static double Percentile(IEnumerable<double> sequence, double percentile)
    {
        var list = sequence.OrderBy(x => x).ToList();
        if (list.Count == 0) return 0.0;
        if (list.Count == 1) return list[0];

        double idx = percentile * (list.Count - 1);
        int lower = (int)Math.Floor(idx);
        int upper = (int)Math.Ceiling(idx);

        if (lower == upper) return list[lower];
        return list[lower] + (idx - lower) * (list[upper] - list[lower]);
    }

    private static string EscapeHtml(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s.Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
    }
}
