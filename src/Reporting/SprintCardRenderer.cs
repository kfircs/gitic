using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Kfc.Cli.Core;

namespace Gitic;

/// <summary>
/// Data model representing a Sprint Health Card with comparative DX Index trajectory,
/// ranked improvements, emerging risks/regressions, and prioritized key actions.
/// </summary>
public class SprintHealthCard
{
    [JsonPropertyName("period_label")]
    public string PeriodLabel { get; set; } = "Sprint Health Card";

    [JsonPropertyName("previous_dx_index")]
    public double PreviousDxIndex { get; set; }

    [JsonPropertyName("current_dx_index")]
    public double CurrentDxIndex { get; set; }

    [JsonPropertyName("delta_dx_index")]
    public double DeltaDxIndex { get; set; }

    [JsonPropertyName("status_indicator")]
    public string StatusIndicator { get; set; } = "[UP]";

    [JsonPropertyName("improvements")]
    public List<MetricDelta> Improvements { get; set; } = new();

    [JsonPropertyName("regressions")]
    public List<MetricDelta> Regressions { get; set; } = new();

    [JsonPropertyName("key_actions")]
    public List<string> KeyActions { get; set; } = new();

    [JsonPropertyName("period_date_range")]
    public string? PeriodDateRange { get; set; }

    [JsonPropertyName("all_deltas")]
    public List<MetricDelta> AllDeltas { get; set; } = new();

    public MetricDelta? FindDelta(string metricName)
    {
        return AllDeltas.FirstOrDefault(d =>
            string.Equals(d.MetricName, metricName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(d.MetricName.Replace(" ", "_"), metricName.Replace(" ", "_"), StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Interface for rendering sprint health cards across terminal, markdown, and HTML formats.
/// </summary>
public interface ISprintCardRenderer
{
    SprintHealthCard RenderCard(BaselineSnapshot previous, BaselineSnapshot current, string? periodLabel = null);
    string RenderTerminalBox(SprintHealthCard card);
    string RenderTerminalBox(SprintHealthCard card, bool enableAnsi);
    string RenderMarkdown(SprintHealthCard card);
    string RenderHtml(SprintHealthCard card);
}

/// <summary>
/// Renderer that evaluates bi-weekly delta trends between baseline snapshots and produces
/// visual terminal boxes, GitHub-flavored markdown tables, and standalone HTML summaries.
/// </summary>
public class SprintCardRenderer : ISprintCardRenderer
{
    public SprintHealthCard RenderCard(BaselineSnapshot previous, BaselineSnapshot current, string? periodLabel = null)
    {
        if (previous == null) throw new ArgumentNullException(nameof(previous));
        if (current == null) throw new ArgumentNullException(nameof(current));

        string label = ResolvePeriodLabel(previous, current, periodLabel);
        string? dateRange = ResolveDateRange(previous, current);

        double prevDx = ResolveDxIndex(previous);
        double currDx = ResolveDxIndex(current);
        double deltaDx = Math.Round(currDx - prevDx, 1);

        var allDeltas = new List<MetricDelta>();
        var improvements = new List<MetricDelta>();
        var regressions = new List<MetricDelta>();

        void AddDelta(string name, double fromVal, double toVal, bool lowerIsBetter)
        {
            if (Math.Abs(toVal - fromVal) < 1e-6) return;

            var delta = new MetricDelta(name, fromVal, toVal);
            allDeltas.Add(delta);

            bool isImprovement = lowerIsBetter ? delta.AbsoluteDelta < 0 : delta.AbsoluteDelta > 0;
            if (isImprovement)
            {
                improvements.Add(delta);
            }
            else
            {
                regressions.Add(delta);
            }
        }

        // 1. Delivery & Lead Times
        double prevP50 = GetMetricOrSummary(previous, "p50_lead_time", "p50_lead_time_hours", "average_lead_time_hours", "lead_time");
        double currP50 = GetMetricOrSummary(current, "p50_lead_time", "p50_lead_time_hours", "average_lead_time_hours", "lead_time");
        if (prevP50 > 0 || currP50 > 0)
        {
            AddDelta("P50 Lead Time", prevP50, currP50, lowerIsBetter: true);
        }

        double prevP90 = GetMetricOrSummary(previous, "p90_lead_time", "p90_lead_time_hours");
        double currP90 = GetMetricOrSummary(current, "p90_lead_time", "p90_lead_time_hours");
        if (prevP90 > 0 || currP90 > 0)
        {
            AddDelta("P90 Lead Time", prevP90, currP90, lowerIsBetter: true);
        }

        // 2. Code Stability / Rework Rate
        double prevRework = GetReworkRate(previous);
        double currRework = GetReworkRate(current);
        if (prevRework > 0 || currRework > 0)
        {
            AddDelta("Rework Rate", prevRework, currRework, lowerIsBetter: true);
        }

        // 3. Code Rot / Zombie Files
        double prevZombies = previous.Summary?.ZombieFiles ?? (previous.Metrics.TryGetValue("zombie_files", out var z1) ? z1 : 0);
        double currZombies = current.Summary?.ZombieFiles ?? (current.Metrics.TryGetValue("zombie_files", out var z2) ? z2 : 0);
        if (prevZombies > 0 || currZombies > 0)
        {
            AddDelta("Zombie Files", prevZombies, currZombies, lowerIsBetter: true);
        }

        // 4. Global Hotspot Density
        double prevHd = previous.Metrics.TryGetValue("hotspot_density", out var hd1) ? hd1 : 0.0;
        double currHd = current.Metrics.TryGetValue("hotspot_density", out var hd2) ? hd2 : 0.0;
        if (prevHd > 0 || currHd > 0)
        {
            AddDelta("Hotspot Density", prevHd, currHd, lowerIsBetter: true);
        }

        // 5. Work Classification (Bugs, Tech Debt, Features)
        double prevDebt = previous.Summary?.TechnicalDebt ?? (previous.Metrics.TryGetValue("technical_debt", out var td1) ? td1 : 0);
        double currDebt = current.Summary?.TechnicalDebt ?? (current.Metrics.TryGetValue("technical_debt", out var td2) ? td2 : 0);
        if (prevDebt > 0 || currDebt > 0)
        {
            AddDelta("Technical Debt", prevDebt, currDebt, lowerIsBetter: true);
        }

        double prevBugs = previous.Summary?.Bugs ?? (previous.Metrics.TryGetValue("bugs", out var b1) ? b1 : 0);
        double currBugs = current.Summary?.Bugs ?? (current.Metrics.TryGetValue("bugs", out var b2) ? b2 : 0);
        if (prevBugs > 0 || currBugs > 0)
        {
            AddDelta("Bugs", prevBugs, currBugs, lowerIsBetter: true);
        }

        double prevFeatures = previous.Summary?.Features ?? (previous.Metrics.TryGetValue("features", out var f1) ? f1 : 0);
        double currFeatures = current.Summary?.Features ?? (current.Metrics.TryGetValue("features", out var f2) ? f2 : 0);
        if (prevFeatures > 0 || currFeatures > 0)
        {
            AddDelta("Features", prevFeatures, currFeatures, lowerIsBetter: false);
        }

        // 6. Reviewer Silos & High Volume Commits
        double prevSilos = previous.Summary?.ReviewerSilos ?? (previous.Metrics.TryGetValue("reviewer_silos", out var rs1) ? rs1 : 0);
        double currSilos = current.Summary?.ReviewerSilos ?? (current.Metrics.TryGetValue("reviewer_silos", out var rs2) ? rs2 : 0);
        if (prevSilos > 0 || currSilos > 0)
        {
            AddDelta("Reviewer Silos", prevSilos, currSilos, lowerIsBetter: true);
        }

        double prevHighVol = previous.Summary?.HighVolumeCommits ?? (previous.Metrics.TryGetValue("high_volume_commits", out var hvc1) ? hvc1 : 0);
        double currHighVol = current.Summary?.HighVolumeCommits ?? (current.Metrics.TryGetValue("high_volume_commits", out var hvc2) ? hvc2 : 0);
        if (prevHighVol > 0 || currHighVol > 0)
        {
            AddDelta("High Volume Commits", prevHighVol, currHighVol, lowerIsBetter: true);
        }

        // 7. Per-Area Metrics Comparison
        var allAreaKeys = new HashSet<string>(previous.Areas.Keys, StringComparer.OrdinalIgnoreCase);
        allAreaKeys.UnionWith(current.Areas.Keys);

        foreach (var area in allAreaKeys.OrderBy(a => a))
        {
            previous.Areas.TryGetValue(area, out var aPrev);
            current.Areas.TryGetValue(area, out var aCurr);

            double aPrevHd = aPrev != null && aPrev.Metrics.TryGetValue("hotspot_density", out var ahd1) ? ahd1 : 0.0;
            double aCurrHd = aCurr != null && aCurr.Metrics.TryGetValue("hotspot_density", out var ahd2) ? ahd2 : 0.0;
            if (Math.Abs(aCurrHd - aPrevHd) >= 0.01)
            {
                AddDelta($"Hotspot Density ({area})", aPrevHd, aCurrHd, lowerIsBetter: true);
            }

            double aPrevRw = aPrev?.ReworkRate ?? (aPrev != null && aPrev.Metrics.TryGetValue("rework_rate", out var arw1) ? arw1 : 0.0);
            double aCurrRw = aCurr?.ReworkRate ?? (aCurr != null && aCurr.Metrics.TryGetValue("rework_rate", out var arw2) ? arw2 : 0.0);
            if (Math.Abs(aCurrRw - aPrevRw) >= 0.01)
            {
                AddDelta($"Rework Rate ({area})", aPrevRw, aCurrRw, lowerIsBetter: true);
            }

            double aPrevSo = aPrev != null && aPrev.Metrics.TryGetValue("single_owner_ratio", out var aso1) ? aso1 : 0.0;
            double aCurrSo = aCurr != null && aCurr.Metrics.TryGetValue("single_owner_ratio", out var aso2) ? aso2 : 0.0;
            if (Math.Abs(aCurrSo - aPrevSo) >= 0.05)
            {
                AddDelta($"Single Owner Ratio ({area})", aPrevSo, aCurrSo, lowerIsBetter: true);
            }
        }

        // 8. File-Level Knowledge Silos & Attention Surges
        foreach (var (path, fCurr) in current.Files)
        {
            bool isHighConcentration = fCurr.AttentionScore >= 70.0 || fCurr.HeatScore >= 70.0;
            if (isHighConcentration)
            {
                bool wasHighConcentration = previous.Files.TryGetValue(path, out var fPrev) &&
                                            (fPrev.AttentionScore >= 70.0 || fPrev.HeatScore >= 70.0);
                if (!wasHighConcentration)
                {
                    double prevScore = fPrev?.AttentionScore ?? 0.0;
                    double currScore = fCurr.AttentionScore > 0 ? fCurr.AttentionScore : fCurr.HeatScore;
                    AddDelta($"New knowledge silo: {path}", prevScore, currScore, lowerIsBetter: true);
                }
            }
        }

        // Sort improvements by highest relative or absolute impact
        improvements = improvements
            .OrderByDescending(i => Math.Abs(i.PercentageDelta))
            .ThenByDescending(i => Math.Abs(i.AbsoluteDelta))
            .ToList();

        // Sort regressions by highest severity
        regressions = regressions
            .OrderByDescending(r => Math.Abs(r.PercentageDelta))
            .ThenByDescending(r => Math.Abs(r.AbsoluteDelta))
            .ToList();

        // Overall status indicator
        int critRegressions = regressions.Count(r =>
            Math.Abs(r.PercentageDelta) >= 50.0 ||
            r.MetricName.Contains("silo", StringComparison.OrdinalIgnoreCase) ||
            (r.MetricName.Contains("hotspot", StringComparison.OrdinalIgnoreCase) && r.AbsoluteDelta >= 0.10));

        string statusIndicator;
        if (deltaDx < -5.0 || critRegressions >= 2)
        {
            statusIndicator = "[CRIT]";
        }
        else if (deltaDx < 0.0 || regressions.Count > 0)
        {
            statusIndicator = "[WARN]";
        }
        else
        {
            statusIndicator = "[UP]";
        }

        // Generate prioritized key actions
        var keyActions = GenerateKeyActions(regressions, allDeltas, current, prevDx, currDx);

        return new SprintHealthCard
        {
            PeriodLabel = label,
            PreviousDxIndex = prevDx,
            CurrentDxIndex = currDx,
            DeltaDxIndex = deltaDx,
            StatusIndicator = statusIndicator,
            Improvements = improvements,
            Regressions = regressions,
            KeyActions = keyActions,
            PeriodDateRange = dateRange,
            AllDeltas = allDeltas
        };
    }

    public string RenderTerminalBox(SprintHealthCard card) => RenderTerminalBox(card, enableAnsi: true);

    public string RenderTerminalBox(SprintHealthCard card, bool enableAnsi)
    {
        if (card == null) throw new ArgumentNullException(nameof(card));

        const int boxWidth = 62;
        int innerWidth = boxWidth - 4; // 2 border chars + 2 padding chars

        var lines = new List<string>();

        // Header
        string title = $"🏥 {card.PeriodLabel}";
        lines.Add(enableAnsi ? $"\u001b[1m{title}\u001b[0m" : title);
        if (!string.IsNullOrEmpty(card.PeriodDateRange))
        {
            lines.Add(card.PeriodDateRange);
        }

        lines.Add("---DIVIDER---");

        // DX Index Section
        string dxDeltaSign = card.DeltaDxIndex >= 0 ? $"+{card.DeltaDxIndex:F0}" : $"{card.DeltaDxIndex:F0}";
        string arrow = card.DeltaDxIndex >= 0 ? "↗" : "↘";
        string dxStatusBadge = FormatStatusBadge(card.StatusIndicator, enableAnsi);

        string dxLine = $"DX Index: {card.PreviousDxIndex:F0} → {card.CurrentDxIndex:F0}  {arrow} {dxDeltaSign} pts";
        int padSpaces = Math.Max(1, innerWidth - VisibleLength(dxLine) - VisibleLength(dxStatusBadge));
        lines.Add($"{dxLine}{new string(' ', padSpaces)}{dxStatusBadge}");

        lines.Add("");

        // Improvements Section
        lines.Add(enableAnsi ? "\u001b[1mIMPROVEMENTS:\u001b[0m" : "IMPROVEMENTS:");
        if (card.Improvements.Count == 0)
        {
            lines.Add("  (No significant improvements recorded)");
        }
        else
        {
            foreach (var item in card.Improvements.Take(5))
            {
                string badge = FormatStatusBadge("[UP]", enableAnsi);
                string formattedItem = FormatMetricDeltaLine(item);
                lines.Add($"  {badge} {formattedItem}");
            }
        }

        lines.Add("");

        // Regressions Section
        lines.Add(enableAnsi ? "\u001b[1mREGRESSIONS:\u001b[0m" : "REGRESSIONS:");
        if (card.Regressions.Count == 0)
        {
            lines.Add("  (No regressions detected — all metrics stable or improving)");
        }
        else
        {
            foreach (var item in card.Regressions.Take(5))
            {
                bool isCrit = Math.Abs(item.PercentageDelta) >= 50.0 ||
                              item.MetricName.Contains("silo", StringComparison.OrdinalIgnoreCase) ||
                              (item.MetricName.Contains("hotspot", StringComparison.OrdinalIgnoreCase) && item.AbsoluteDelta >= 0.10);
                string indicator = isCrit ? "[CRIT]" : "[WARN]";
                string badge = FormatStatusBadge(indicator, enableAnsi);
                string formattedItem = FormatMetricDeltaLine(item);
                lines.Add($"  {badge} {formattedItem}");
            }
        }

        lines.Add("");

        // Key Actions Section
        string actionsHeader = "KEY ACTIONS:";
        if (card.PeriodLabel.Contains("Sprint", StringComparison.OrdinalIgnoreCase))
        {
            var match = Regex.Match(card.PeriodLabel, @"Sprint\s+(\d+)", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int sprintNum))
            {
                actionsHeader = $"KEY ACTIONS FOR SPRINT {sprintNum + 1}:";
            }
        }
        lines.Add(enableAnsi ? $"\u001b[1m{actionsHeader}\u001b[0m" : actionsHeader);

        for (int i = 0; i < card.KeyActions.Count; i++)
        {
            lines.Add($"  {i + 1}. {card.KeyActions[i]}");
        }

        // Now assemble the box with borders
        var sb = new StringBuilder();
        sb.AppendLine("╔" + new string('═', boxWidth - 2) + "╗");

        foreach (var line in lines)
        {
            if (line == "---DIVIDER---")
            {
                sb.AppendLine("╠" + new string('═', boxWidth - 2) + "╣");
            }
            else
            {
                int visLen = VisibleLength(line);
                int pad = Math.Max(0, innerWidth - visLen);
                sb.AppendLine("║  " + line + new string(' ', pad) + "  ║");
            }
        }

        sb.Append("╚" + new string('═', boxWidth - 2) + "╝");
        return sb.ToString();
    }

    public string RenderMarkdown(SprintHealthCard card)
    {
        if (card == null) throw new ArgumentNullException(nameof(card));

        var sb = new StringBuilder();

        sb.AppendLine($"# 🏥 {card.PeriodLabel}");
        if (!string.IsNullOrEmpty(card.PeriodDateRange))
        {
            sb.AppendLine($"*{card.PeriodDateRange}*");
        }
        sb.AppendLine();

        string dxDeltaSign = card.DeltaDxIndex >= 0 ? $"+{card.DeltaDxIndex:F0}" : $"{card.DeltaDxIndex:F0}";
        string arrow = card.DeltaDxIndex >= 0 ? "↗" : "↘";
        sb.AppendLine("## DX Index Overview");
        sb.AppendLine($"**DX Index:** {card.PreviousDxIndex:F0} → {card.CurrentDxIndex:F0} ({arrow} {dxDeltaSign} pts) — `{card.StatusIndicator}`");
        sb.AppendLine();

        sb.AppendLine("## Comparative Metrics Delta");
        sb.AppendLine();

        if (card.Improvements.Count > 0)
        {
            sb.AppendLine("### Top Improvements");
            sb.AppendLine("| Status | Metric | Previous | Current | Absolute Delta | % Change |");
            sb.AppendLine("| :---: | :--- | :--- | :--- | :--- | :--- |");
            foreach (var item in card.Improvements)
            {
                string prevStr = FormatMetricValue(item.MetricName, item.FromValue);
                string currStr = FormatMetricValue(item.MetricName, item.ToValue);
                string deltaStr = FormatDeltaValue(item.MetricName, item.AbsoluteDelta);
                string pctStr = FormatPercentageChange(item.PercentageDelta);
                sb.AppendLine($"| [UP] | {item.MetricName} | {prevStr} | {currStr} | {deltaStr} | {pctStr} |");
            }
            sb.AppendLine();
        }

        if (card.Regressions.Count > 0)
        {
            sb.AppendLine("### Emerging Risks & Regressions");
            sb.AppendLine("| Status | Metric | Previous | Current | Absolute Delta | % Change |");
            sb.AppendLine("| :---: | :--- | :--- | :--- | :--- | :--- |");
            foreach (var item in card.Regressions)
            {
                bool isCrit = Math.Abs(item.PercentageDelta) >= 50.0 ||
                              item.MetricName.Contains("silo", StringComparison.OrdinalIgnoreCase) ||
                              (item.MetricName.Contains("hotspot", StringComparison.OrdinalIgnoreCase) && item.AbsoluteDelta >= 0.10);
                string indicator = isCrit ? "[CRIT]" : "[WARN]";
                string prevStr = FormatMetricValue(item.MetricName, item.FromValue);
                string currStr = FormatMetricValue(item.MetricName, item.ToValue);
                string deltaStr = FormatDeltaValue(item.MetricName, item.AbsoluteDelta);
                string pctStr = FormatPercentageChange(item.PercentageDelta);
                sb.AppendLine($"| {indicator} | {item.MetricName} | {prevStr} | {currStr} | {deltaStr} | {pctStr} |");
            }
            sb.AppendLine();
        }

        sb.AppendLine("## Key Actions");
        for (int i = 0; i < card.KeyActions.Count; i++)
        {
            sb.AppendLine($"{i + 1}. {card.KeyActions[i]}");
        }

        return sb.ToString();
    }

    public string RenderHtml(SprintHealthCard card)
    {
        if (card == null) throw new ArgumentNullException(nameof(card));

        string dxDeltaSign = card.DeltaDxIndex >= 0 ? $"+{card.DeltaDxIndex:F0}" : $"{card.DeltaDxIndex:F0}";
        string arrow = card.DeltaDxIndex >= 0 ? "↗" : "↘";
        string badgeClass = card.StatusIndicator switch
        {
            "[CRIT]" => "badge-crit",
            "[WARN]" => "badge-warn",
            _ => "badge-up"
        };

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine($"  <title>{EscapeHtml(card.PeriodLabel)}</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine("    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; margin: 2rem; background: #0f172a; color: #f8fafc; }");
        sb.AppendLine("    .card { background: #1e293b; border-radius: 12px; box-shadow: 0 10px 25px -5px rgba(0, 0, 0, 0.5); max-width: 840px; margin: 0 auto; overflow: hidden; border: 1px solid #334155; }");
        sb.AppendLine("    .card-header { background: #090d16; padding: 1.5rem 2rem; border-bottom: 1px solid #334155; }");
        sb.AppendLine("    .card-title { margin: 0; font-size: 1.5rem; font-weight: 700; color: #f8fafc; display: flex; align-items: center; gap: 0.5rem; }");
        sb.AppendLine("    .card-period { margin-top: 0.35rem; font-size: 0.875rem; color: #94a3b8; }");
        sb.AppendLine("    .card-body { padding: 2rem; }");
        sb.AppendLine("    .dx-summary { display: flex; align-items: center; justify-content: space-between; background: #0f172a; padding: 1.25rem 1.75rem; border-radius: 10px; margin-bottom: 2rem; border: 1px solid #334155; }");
        sb.AppendLine("    .dx-score { font-size: 1.85rem; font-weight: 800; color: #38bdf8; }");
        sb.AppendLine("    .dx-delta { font-size: 1.05rem; font-weight: 600; color: #94a3b8; margin-left: 0.5rem; }");
        sb.AppendLine("    .badge { display: inline-block; padding: 0.3rem 0.75rem; font-size: 0.8rem; font-weight: 700; border-radius: 9999px; letter-spacing: 0.05em; }");
        sb.AppendLine("    .badge-up { background: rgba(34, 197, 94, 0.2); color: #4ade80; border: 1px solid #22c55e; }");
        sb.AppendLine("    .badge-warn { background: rgba(234, 179, 8, 0.2); color: #facc15; border: 1px solid #eab308; }");
        sb.AppendLine("    .badge-crit { background: rgba(239, 68, 68, 0.2); color: #f87171; border: 1px solid #ef4444; }");
        sb.AppendLine("    .section-title { font-size: 1.15rem; font-weight: 700; margin: 1.75rem 0 0.85rem 0; color: #e2e8f0; display: flex; align-items: center; gap: 0.5rem; }");
        sb.AppendLine("    table { width: 100%; border-collapse: collapse; margin-bottom: 1.5rem; font-size: 0.92rem; }");
        sb.AppendLine("    th { text-align: left; padding: 0.75rem 1rem; background: #0f172a; border-bottom: 2px solid #334155; color: #94a3b8; font-weight: 600; }");
        sb.AppendLine("    td { padding: 0.75rem 1rem; border-bottom: 1px solid #334155; }");
        sb.AppendLine("    .actions-list { padding-left: 1.5rem; margin: 0; line-height: 1.7; }");
        sb.AppendLine("    .actions-list li { margin-bottom: 0.5rem; color: #cbd5e1; }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <div class=\"card\">");
        sb.AppendLine("    <div class=\"card-header\">");
        sb.AppendLine($"      <h1 class=\"card-title\">🏥 {EscapeHtml(card.PeriodLabel)}</h1>");
        if (!string.IsNullOrEmpty(card.PeriodDateRange))
        {
            sb.AppendLine($"      <div class=\"card-period\">{EscapeHtml(card.PeriodDateRange)}</div>");
        }
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div class=\"card-body\">");
        sb.AppendLine("      <div class=\"dx-summary\">");
        sb.AppendLine("        <div>");
        sb.AppendLine("          <div style=\"font-size: 0.85rem; color: #94a3b8; margin-bottom: 0.25rem;\">DEVELOPER EXPERIENCE (DX) INDEX</div>");
        sb.AppendLine($"          <div class=\"dx-score\">{card.PreviousDxIndex:F0} → {card.CurrentDxIndex:F0} <span class=\"dx-delta\">({arrow} {dxDeltaSign} pts)</span></div>");
        sb.AppendLine("        </div>");
        sb.AppendLine($"        <span class=\"badge {badgeClass}\">{EscapeHtml(card.StatusIndicator)}</span>");
        sb.AppendLine("      </div>");

        if (card.Improvements.Count > 0)
        {
            sb.AppendLine("      <div class=\"section-title\">✅ Top Improvements</div>");
            sb.AppendLine("      <table>");
            sb.AppendLine("        <thead>");
            sb.AppendLine("          <tr>");
            sb.AppendLine("            <th>Status</th>");
            sb.AppendLine("            <th>Metric</th>");
            sb.AppendLine("            <th>Previous</th>");
            sb.AppendLine("            <th>Current</th>");
            sb.AppendLine("            <th>Absolute Delta</th>");
            sb.AppendLine("            <th>Change</th>");
            sb.AppendLine("          </tr>");
            sb.AppendLine("        </thead>");
            sb.AppendLine("        <tbody>");
            foreach (var item in card.Improvements)
            {
                string prevStr = EscapeHtml(FormatMetricValue(item.MetricName, item.FromValue));
                string currStr = EscapeHtml(FormatMetricValue(item.MetricName, item.ToValue));
                string deltaStr = EscapeHtml(FormatDeltaValue(item.MetricName, item.AbsoluteDelta));
                string pctStr = EscapeHtml(FormatPercentageChange(item.PercentageDelta));
                sb.AppendLine("          <tr>");
                sb.AppendLine("            <td><span class=\"badge badge-up\">[UP]</span></td>");
                sb.AppendLine($"            <td><strong>{EscapeHtml(item.MetricName)}</strong></td>");
                sb.AppendLine($"            <td>{prevStr}</td>");
                sb.AppendLine($"            <td>{currStr}</td>");
                sb.AppendLine($"            <td>{deltaStr}</td>");
                sb.AppendLine($"            <td>{pctStr}</td>");
                sb.AppendLine("          </tr>");
            }
            sb.AppendLine("        </tbody>");
            sb.AppendLine("      </table>");
        }

        if (card.Regressions.Count > 0)
        {
            sb.AppendLine("      <div class=\"section-title\">⚠️ Emerging Risks & Regressions</div>");
            sb.AppendLine("      <table>");
            sb.AppendLine("        <thead>");
            sb.AppendLine("          <tr>");
            sb.AppendLine("            <th>Status</th>");
            sb.AppendLine("            <th>Metric</th>");
            sb.AppendLine("            <th>Previous</th>");
            sb.AppendLine("            <th>Current</th>");
            sb.AppendLine("            <th>Absolute Delta</th>");
            sb.AppendLine("            <th>Change</th>");
            sb.AppendLine("          </tr>");
            sb.AppendLine("        </thead>");
            sb.AppendLine("        <tbody>");
            foreach (var item in card.Regressions)
            {
                bool isCrit = Math.Abs(item.PercentageDelta) >= 50.0 ||
                              item.MetricName.Contains("silo", StringComparison.OrdinalIgnoreCase) ||
                              (item.MetricName.Contains("hotspot", StringComparison.OrdinalIgnoreCase) && item.AbsoluteDelta >= 0.10);
                string indicator = isCrit ? "[CRIT]" : "[WARN]";
                string itemBadgeClass = isCrit ? "badge-crit" : "badge-warn";
                string prevStr = EscapeHtml(FormatMetricValue(item.MetricName, item.FromValue));
                string currStr = EscapeHtml(FormatMetricValue(item.MetricName, item.ToValue));
                string deltaStr = EscapeHtml(FormatDeltaValue(item.MetricName, item.AbsoluteDelta));
                string pctStr = EscapeHtml(FormatPercentageChange(item.PercentageDelta));
                sb.AppendLine("          <tr>");
                sb.AppendLine($"            <td><span class=\"badge {itemBadgeClass}\">{indicator}</span></td>");
                sb.AppendLine($"            <td><strong>{EscapeHtml(item.MetricName)}</strong></td>");
                sb.AppendLine($"            <td>{prevStr}</td>");
                sb.AppendLine($"            <td>{currStr}</td>");
                sb.AppendLine($"            <td>{deltaStr}</td>");
                sb.AppendLine($"            <td>{pctStr}</td>");
                sb.AppendLine("          </tr>");
            }
            sb.AppendLine("        </tbody>");
            sb.AppendLine("      </table>");
        }

        sb.AppendLine("      <div class=\"section-title\">🎯 Key Actions</div>");
        sb.AppendLine("      <ol class=\"actions-list\">");
        foreach (var action in card.KeyActions)
        {
            sb.AppendLine($"        <li>{EscapeHtml(action)}</li>");
        }
        sb.AppendLine("      </ol>");

        sb.AppendLine("    </div>");
        sb.AppendLine("  </div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private static string ResolvePeriodLabel(BaselineSnapshot previous, BaselineSnapshot current, string? periodLabel)
    {
        if (!string.IsNullOrWhiteSpace(periodLabel))
        {
            return periodLabel.Contains("Health Card", StringComparison.OrdinalIgnoreCase)
                ? periodLabel
                : $"{periodLabel} Health Card";
        }

        if (!string.IsNullOrWhiteSpace(current.Tag))
        {
            string tag = current.Tag.Trim();
            if (tag.StartsWith("sprint-", StringComparison.OrdinalIgnoreCase) || tag.StartsWith("sprint_", StringComparison.OrdinalIgnoreCase))
            {
                return $"Sprint {tag.Substring(7)} Health Card";
            }
            return $"{tag} Health Card";
        }

        return "Sprint Health Card";
    }

    private static string? ResolveDateRange(BaselineSnapshot previous, BaselineSnapshot current)
    {
        if (previous.Timestamp != default && current.Timestamp != default && previous.Timestamp != current.Timestamp)
        {
            return $"Period: {previous.Timestamp:MMM d} – {current.Timestamp:MMM d, yyyy}";
        }
        if (current.Timestamp != default)
        {
            return $"Period: {current.Timestamp:MMM d, yyyy}";
        }
        return null;
    }

    private static double ResolveDxIndex(BaselineSnapshot snapshot)
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

    private static double GetMetricOrSummary(BaselineSnapshot snapshot, params string[] metricKeys)
    {
        if (snapshot.Metrics != null)
        {
            foreach (var key in metricKeys)
            {
                if (snapshot.Metrics.TryGetValue(key, out var val)) return val;
            }
        }

        foreach (var key in metricKeys)
        {
            if (key is "average_lead_time_hours" or "lead_time" or "p50_lead_time" && snapshot.Summary != null)
            {
                return snapshot.Summary.AverageLeadTimeHours;
            }
        }

        return 0.0;
    }

    private static double GetReworkRate(BaselineSnapshot snapshot)
    {
        if (snapshot.Metrics != null && snapshot.Metrics.TryGetValue("rework_rate", out var r))
        {
            return r;
        }

        var areaReworks = snapshot.Areas.Values
            .Where(a => a.ReworkRate.HasValue)
            .Select(a => a.ReworkRate!.Value)
            .ToList();

        return areaReworks.Count > 0 ? areaReworks.Average() : 0.0;
    }

    private static List<string> GenerateKeyActions(
        List<MetricDelta> regressions,
        List<MetricDelta> allDeltas,
        BaselineSnapshot current,
        double prevDx,
        double currDx)
    {
        var actions = new List<string>();

        // 1. Hotspot density regression
        var hotspotReg = regressions.FirstOrDefault(r => r.MetricName.Contains("Hotspot", StringComparison.OrdinalIgnoreCase));
        if (hotspotReg != null)
        {
            string area = ExtractAreaName(hotspotReg.MetricName) ?? "Core/";
            double growthPct = Math.Abs(hotspotReg.PercentageDelta);
            actions.Add($"Pair on {area} hotspots (density up {growthPct:F0}%)");
        }

        // 2. Knowledge silo regression
        var siloReg = regressions.FirstOrDefault(r => r.MetricName.Contains("silo", StringComparison.OrdinalIgnoreCase));
        if (siloReg != null)
        {
            string target = siloReg.MetricName.Replace("New knowledge silo:", "").Trim();
            if (string.IsNullOrEmpty(target)) target = "knowledge silos";
            actions.Add($"Start knowledge sharing on {target}");
        }

        // 3. Zombie files / Dead code
        int currZombieCount = current.Summary?.ZombieFiles ?? 0;
        var zombieDelta = allDeltas.FirstOrDefault(d => d.MetricName.Contains("Zombie", StringComparison.OrdinalIgnoreCase));
        if (currZombieCount > 0)
        {
            if (zombieDelta != null && zombieDelta.AbsoluteDelta < 0)
            {
                actions.Add($"Continue dead code cleanup ({currZombieCount} zombie files remaining)");
            }
            else
            {
                actions.Add($"Schedule dead code cleanup sprint ({currZombieCount} zombie files detected)");
            }
        }

        // 4. Lead time latency
        var leadTimeReg = regressions.FirstOrDefault(r => r.MetricName.Contains("Lead Time", StringComparison.OrdinalIgnoreCase));
        if (leadTimeReg != null)
        {
            actions.Add("Investigate PR review latency and decompose large merge batches");
        }

        // 5. Rework rate regression
        var reworkReg = regressions.FirstOrDefault(r => r.MetricName.Contains("Rework", StringComparison.OrdinalIgnoreCase));
        if (reworkReg != null)
        {
            actions.Add("Focus on test coverage and review scrutiny to reduce rework volatility");
        }

        // 6. Bug regression
        var bugReg = regressions.FirstOrDefault(r => r.MetricName.Contains("Bug", StringComparison.OrdinalIgnoreCase));
        if (bugReg != null)
        {
            actions.Add("Schedule bug triage session to stabilize defect-prone code paths");
        }

        // Fallbacks if fewer than 2 actions
        if (actions.Count == 0)
        {
            actions.Add("Maintain current delivery pace and test coverage standards");
            actions.Add("Continue proactive code quality reviews");
        }

        return actions.Take(3).ToList();
    }

    private static string? ExtractAreaName(string metricName)
    {
        var match = Regex.Match(metricName, @"\(([^)]+)\)");
        return match.Success ? match.Groups[1].Value : null;
    }

    public static string FormatMetricValue(string metricName, double value)
    {
        string lower = metricName.ToLowerInvariant();
        if (lower.Contains("rate") || lower.Contains("ratio") || lower.Contains("density"))
        {
            double pct = value <= 1.0 && value > 0 ? value * 100.0 : value;
            return $"{pct:F1}%";
        }
        if (lower.Contains("lead_time") || lower.Contains("lead time"))
        {
            return $"{value:F0}h";
        }
        if (lower.Contains("index") || lower.Contains("score"))
        {
            return $"{value:F0}";
        }
        if (Math.Abs(value % 1) < 1e-4)
        {
            return $"{value:F0}";
        }
        return $"{value:F1}";
    }

    public static string FormatDeltaValue(string metricName, double delta)
    {
        string lower = metricName.ToLowerInvariant();
        if (lower.Contains("rate") || lower.Contains("ratio") || lower.Contains("density"))
        {
            double pp = delta <= 1.0 && delta >= -1.0 ? delta * 100.0 : delta;
            string sign = pp > 0 ? "+" : "";
            return $"{sign}{pp:F1}pp";
        }
        if (lower.Contains("lead_time") || lower.Contains("lead time"))
        {
            string sign = delta > 0 ? "+" : "";
            return $"{sign}{delta:F1}h";
        }
        string absSign = delta > 0 ? "+" : "";
        return $"{absSign}{delta:F0}";
    }

    public static string FormatPercentageChange(double pctChange)
    {
        string sign = pctChange > 0 ? "+" : "";
        return $"{sign}{pctChange:F0}%";
    }

    private static string FormatMetricDeltaLine(MetricDelta delta)
    {
        string prevStr = FormatMetricValue(delta.MetricName, delta.FromValue);
        string currStr = FormatMetricValue(delta.MetricName, delta.ToValue);

        string changeDetail;
        string lower = delta.MetricName.ToLowerInvariant();
        if (lower.Contains("rate") || lower.Contains("ratio") || lower.Contains("density"))
        {
            double pp = delta.AbsoluteDelta <= 1.0 && delta.AbsoluteDelta >= -1.0 ? delta.AbsoluteDelta * 100.0 : delta.AbsoluteDelta;
            string sign = pp > 0 ? "+" : "";
            changeDetail = $"({sign}{pp:F0}pp)";
        }
        else
        {
            string sign = delta.PercentageDelta > 0 ? "+" : "";
            changeDetail = $"({sign}{delta.PercentageDelta:F0}%)";
        }

        return $"{delta.MetricName}:  {prevStr} → {currStr}  {changeDetail}";
    }

    private static string FormatStatusBadge(string indicator, bool enableAnsi)
    {
        if (!enableAnsi) return indicator;

        return indicator switch
        {
            "[UP]" => "\u001b[1;32m[UP]\u001b[0m",
            "[WARN]" => "\u001b[1;33m[WARN]\u001b[0m",
            "[CRIT]" => "\u001b[1;31m[CRIT]\u001b[0m",
            _ => indicator
        };
    }

    private static int VisibleLength(string s)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        return Regex.Replace(s, @"\u001b\[[0-9;]*m", "").Length;
    }

    private static string EscapeHtml(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }
}

/// <summary>
/// CLI Options for sprint-report command.
/// </summary>
public class SprintReportCommandOptions
{
    public string? Baseline { get; set; }
    public string? Current { get; set; }
    public string? Period { get; set; }
    public string? MdPath { get; set; }
    public string? HtmlPath { get; set; }
    public string? StorageDir { get; set; }
    public string RepoPath { get; set; } = ".";
    public bool Json { get; set; }
    public string Format { get; set; } = "human";
}

/// <summary>
/// CLI command handler for 'gitic sprint-report' that outputs visual terminal boxes,
/// standalone markdown summaries, and standalone HTML reports.
/// </summary>
public class SprintReportCommand : IGiticCommand
{
    private readonly SprintReportCommandOptions _options;
    private readonly ISprintCardRenderer _cardRenderer;
    private readonly IBaselineEngine _baselineEngine;
    private readonly IRepositoryAnalyzer _analyzer;
    private readonly IGitClient? _gitClient;

    public SprintReportCommand(
        ParsedArgs parsed,
        ISprintCardRenderer? cardRenderer = null,
        IBaselineEngine? baselineEngine = null,
        IRepositoryAnalyzer? analyzer = null,
        IGitClient? gitClient = null)
    {
        _cardRenderer = cardRenderer ?? new SprintCardRenderer();
        _baselineEngine = baselineEngine ?? new BaselineEngine();
        _analyzer = analyzer ?? new RepositoryAnalyzer();
        _gitClient = gitClient;
        _options = ParseOptionsFromParsedArgs(parsed);
    }

    public SprintReportCommand(
        string[] args,
        ISprintCardRenderer? cardRenderer = null,
        IBaselineEngine? baselineEngine = null,
        IRepositoryAnalyzer? analyzer = null,
        IGitClient? gitClient = null)
    {
        _cardRenderer = cardRenderer ?? new SprintCardRenderer();
        _baselineEngine = baselineEngine ?? new BaselineEngine();
        _analyzer = analyzer ?? new RepositoryAnalyzer();
        _gitClient = gitClient;
        _options = ParseOptionsFromArgs(args);
    }

    public SprintReportCommand(
        SprintReportCommandOptions options,
        ISprintCardRenderer? cardRenderer = null,
        IBaselineEngine? baselineEngine = null,
        IRepositoryAnalyzer? analyzer = null,
        IGitClient? gitClient = null)
    {
        _cardRenderer = cardRenderer ?? new SprintCardRenderer();
        _baselineEngine = baselineEngine ?? new BaselineEngine();
        _analyzer = analyzer ?? new RepositoryAnalyzer();
        _gitClient = gitClient;
        _options = options ?? new SprintReportCommandOptions();
    }

    public Task<CliResult> ExecuteAsync(IConsoleReporter reporter)
    {
        return ExecuteAsync(reporter, CancellationToken.None);
    }

    public async Task<CliResult> ExecuteAsync(IConsoleReporter? reporter, CancellationToken cancellationToken = default)
    {
        string repoRoot = string.IsNullOrWhiteSpace(_options.RepoPath) ? "." : _options.RepoPath;
        var gitClient = _gitClient ?? new GitClient(repoRoot);
        string? resolvedRoot = null;
        try
        {
            resolvedRoot = await gitClient.GetRepositoryRootAsync(cancellationToken);
        }
        catch
        {
            // Best effort
        }

        string actualRoot = !string.IsNullOrEmpty(resolvedRoot) ? resolvedRoot : Path.GetFullPath(repoRoot);
        var (previous, current, error) = ResolveSnapshots(actualRoot);
        if (error != null)
        {
            reporter?.WriteError(error + "\n");
            return Cli.CliFailure(error + "\n", exitCode: 2);
        }

        var card = _cardRenderer.RenderCard(previous!, current!, _options.Period);

        // 1. JSON output if requested
        if (_options.Json || string.Equals(_options.Format, "json", StringComparison.OrdinalIgnoreCase))
        {
            string json = JsonSerializer.Serialize(card, JsonSerializationDefaults.Indented);
            reporter?.Write(json);
            return Cli.CliSuccess(json);
        }

        var outputSb = new StringBuilder();

        // 2. Markdown output if requested
        if (!string.IsNullOrEmpty(_options.MdPath))
        {
            string targetPath = ResolveTargetPath(_options.MdPath, "sprint-health-card.md");
            string md = _cardRenderer.RenderMarkdown(card);
            await WriteAtomicFileAsync(targetPath, md, cancellationToken);
            string msg = $"Wrote Sprint Health Card Markdown report to {targetPath}\n";
            reporter?.Write(msg);
            outputSb.Append(msg);
        }

        // 3. HTML output if requested
        if (!string.IsNullOrEmpty(_options.HtmlPath))
        {
            string targetPath = ResolveTargetPath(_options.HtmlPath, "sprint-health-card.html");
            string html = _cardRenderer.RenderHtml(card);
            await WriteAtomicFileAsync(targetPath, html, cancellationToken);
            string msg = $"Wrote Sprint Health Card HTML report to {targetPath}\n";
            reporter?.Write(msg);
            outputSb.Append(msg);
        }

        // 4. Terminal rendering if neither file was specified or alongside
        if (string.IsNullOrEmpty(_options.MdPath) && string.IsNullOrEmpty(_options.HtmlPath))
        {
            bool enableAnsi = !string.Equals(_options.Format, "plain", StringComparison.OrdinalIgnoreCase);
            string box = _cardRenderer.RenderTerminalBox(card, enableAnsi);
            reporter?.Write(box + "\n");
            outputSb.AppendLine(box);
        }

        return Cli.CliSuccess(outputSb.ToString());
    }

    private (BaselineSnapshot? Previous, BaselineSnapshot? Current, string? Error) ResolveSnapshots(string actualRoot)
    {
        string storageDir = ResolveStorageDir(actualRoot);
        var allBaselines = _baselineEngine.ListBaselines(storageDir);

        BaselineSnapshot? prev = null;
        BaselineSnapshot? curr = null;

        if (!string.IsNullOrWhiteSpace(_options.Baseline))
        {
            prev = FindSnapshot(_options.Baseline, storageDir, allBaselines);
            if (prev == null)
            {
                return (null, null, $"Operational error: Baseline snapshot '{_options.Baseline}' not found.");
            }
        }

        if (!string.IsNullOrWhiteSpace(_options.Current))
        {
            curr = FindSnapshot(_options.Current, storageDir, allBaselines);
            if (curr == null)
            {
                return (null, null, $"Operational error: Current snapshot '{_options.Current}' not found.");
            }
        }

        if (prev == null && curr != null)
        {
            if (allBaselines.Count >= 2)
            {
                int idx = allBaselines.ToList().FindIndex(b => b.Id == curr.Id);
                prev = idx > 0 ? allBaselines[idx - 1] : allBaselines[0];
            }
            else if (allBaselines.Count == 1)
            {
                prev = allBaselines[0];
            }
        }

        if (prev != null && curr == null)
        {
            if (allBaselines.Count >= 2)
            {
                int idx = allBaselines.ToList().FindIndex(b => b.Id == prev.Id);
                curr = (idx >= 0 && idx < allBaselines.Count - 1) ? allBaselines[idx + 1] : allBaselines[^1];
            }
            else if (allBaselines.Count == 1)
            {
                curr = allBaselines[0];
            }
        }

        if (prev == null && curr == null)
        {
            if (allBaselines.Count >= 2)
            {
                prev = allBaselines[^2];
                curr = allBaselines[^1];
            }
            else if (allBaselines.Count == 1)
            {
                prev = allBaselines[0];
                curr = allBaselines[0];
            }
            else
            {
                return (null, null, $"Operational error: No baseline snapshots found in storage directory '{storageDir}'. Specify --baseline and --current or save baselines first.");
            }
        }

        if (prev == null || curr == null)
        {
            return (null, null, "Operational error: Unable to resolve previous and current baseline snapshots.");
        }

        return (prev, curr, null);
    }

    private BaselineSnapshot? FindSnapshot(string target, string storageDir, IReadOnlyList<BaselineSnapshot> baselines)
    {
        if (string.Equals(target, "latest", StringComparison.OrdinalIgnoreCase) || string.Equals(target, "latest.json", StringComparison.OrdinalIgnoreCase))
        {
            return baselines.Count > 0 ? baselines[^1] : null;
        }
        if (string.Equals(target, "previous", StringComparison.OrdinalIgnoreCase))
        {
            return baselines.Count >= 2 ? baselines[^2] : (baselines.Count > 0 ? baselines[0] : null);
        }

        if (File.Exists(target))
        {
            return _baselineEngine.LoadBaseline(target);
        }

        string candidatePath = Path.Combine(storageDir, target);
        if (File.Exists(candidatePath))
        {
            return _baselineEngine.LoadBaseline(candidatePath);
        }
        if (File.Exists(candidatePath + ".json"))
        {
            return _baselineEngine.LoadBaseline(candidatePath + ".json");
        }

        var byTag = baselines.FirstOrDefault(b => string.Equals(b.Tag, target, StringComparison.OrdinalIgnoreCase));
        if (byTag != null) return byTag;

        var byId = baselines.FirstOrDefault(b => string.Equals(b.Id, target, StringComparison.OrdinalIgnoreCase) ||
                                                b.Id.StartsWith(target, StringComparison.OrdinalIgnoreCase));
        if (byId != null) return byId;

        return null;
    }

    private string ResolveStorageDir(string repoRoot)
    {
        if (!string.IsNullOrWhiteSpace(_options.StorageDir))
        {
            return Path.GetFullPath(_options.StorageDir);
        }
        return Path.Combine(Path.GetFullPath(repoRoot), ".gitic", "baselines");
    }

    private static string ResolveTargetPath(string inputPath, string defaultFileName)
    {
        string full = Path.GetFullPath(inputPath);
        if (Directory.Exists(full) || inputPath.EndsWith('/') || inputPath.EndsWith('\\'))
        {
            return Path.Combine(full, defaultFileName);
        }
        return full;
    }

    private static async Task WriteAtomicFileAsync(string targetPath, string content, CancellationToken cancellationToken)
    {
        string? dir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string tempPath = Path.Combine(dir ?? ".", $".tmp_{Path.GetRandomFileName()}");
        try
        {
            await File.WriteAllTextAsync(tempPath, content, Encoding.UTF8, cancellationToken);
            File.Move(tempPath, targetPath, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }
            throw;
        }
    }

    public static SprintReportCommandOptions ParseOptionsFromArgs(string[] args)
    {
        var options = new SprintReportCommandOptions();
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg.Equals("--baseline", StringComparison.OrdinalIgnoreCase) || arg.Equals("-b", StringComparison.OrdinalIgnoreCase) || arg.Equals("--from", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length) options.Baseline = args[++i];
            }
            else if (arg.Equals("--current", StringComparison.OrdinalIgnoreCase) || arg.Equals("-c", StringComparison.OrdinalIgnoreCase) || arg.Equals("--to", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length) options.Current = args[++i];
            }
            else if (arg.Equals("--period", StringComparison.OrdinalIgnoreCase) || arg.Equals("-p", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length) options.Period = args[++i];
            }
            else if (arg.Equals("--md", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length) options.MdPath = args[++i];
            }
            else if (arg.Equals("--html", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length) options.HtmlPath = args[++i];
            }
            else if (arg.Equals("--storage-dir", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length) options.StorageDir = args[++i];
            }
            else if (arg.Equals("--repo-path", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length) options.RepoPath = args[++i];
            }
            else if (arg.Equals("--json", StringComparison.OrdinalIgnoreCase))
            {
                options.Json = true;
            }
            else if (arg.Equals("--format", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length) options.Format = args[++i];
            }
            else if (!arg.StartsWith("-") && !arg.Equals("sprint-report", StringComparison.OrdinalIgnoreCase) && !arg.Equals("sprint_report", StringComparison.OrdinalIgnoreCase) && !arg.Equals("sprintcard", StringComparison.OrdinalIgnoreCase))
            {
                if (options.RepoPath == ".")
                {
                    options.RepoPath = arg;
                }
            }
        }
        return options;
    }

    public static SprintReportCommandOptions ParseOptionsFromParsedArgs(ParsedArgs parsed)
    {
        return new SprintReportCommandOptions
        {
            Baseline = parsed.SprintBaseline ?? parsed.BaselineFrom,
            Current = parsed.SprintCurrent ?? parsed.BaselineTo,
            Period = parsed.SprintPeriod,
            StorageDir = parsed.SprintStorageDir ?? parsed.BaselineStorageDir,
            MdPath = parsed.MdPath,
            HtmlPath = parsed.HtmlPath,
            RepoPath = parsed.RepoPath ?? ".",
            Json = parsed.Settings.Json,
            Format = parsed.Settings.Format ?? "human"
        };
    }
}
