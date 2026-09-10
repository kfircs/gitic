using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using Kfc.Cli.Terminal;

namespace Gitic;

/// <summary>
/// Standard category names for proactive action recommendations.
/// </summary>
public static class ActionCategories
{
    public const string KnowledgeRisk = "knowledge_risk";
    public const string ArchitecturalDrift = "architectural_drift";
    public const string DeliveryBottleneck = "delivery_bottleneck";
    public const string CodeHealth = "code_health";
}

/// <summary>
/// A prioritized, ROI-ranked proactive action recommendation.
/// </summary>
public class ActionRecommendation
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("priority")]
    public int Priority { get; set; } = 1; // 1 (highest) to 5 (lowest)

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("narrative")]
    public string Narrative { get; set; } = string.Empty;

    [JsonPropertyName("estimated_impact")]
    public string EstimatedImpact { get; set; } = string.Empty;

    [JsonPropertyName("suggested_assignee")]
    public string SuggestedAssignee { get; set; } = string.Empty;

    [JsonPropertyName("evidence")]
    public List<object> Evidence { get; set; } = new();

    [JsonPropertyName("detection_rules")]
    public List<string> DetectionRules { get; set; } = new();
}

public partial class AnalysisResult
{
    [JsonPropertyName("actions")]
    public List<ActionRecommendation>? Actions { get; set; }
}

/// <summary>
/// Interface for proactive action recommendations engine.
/// </summary>
public interface IActionEngine
{
    List<ActionRecommendation> Evaluate(AnalysisResult result, BaselineSnapshot? baseline = null);
    string FormatCliTable(IEnumerable<ActionRecommendation> actions);
    string FormatMarkdown(IEnumerable<ActionRecommendation> actions);
}

/// <summary>
/// Evaluates codebase metrics and baseline deltas to generate prioritized, actionable recommendations.
/// </summary>
public class ActionEngine : IActionEngine
{
    private readonly IActionRuleProvider _ruleProvider;

    public ActionEngine(IActionRuleProvider? ruleProvider = null)
    {
        _ruleProvider = ruleProvider ?? new DefaultActionRuleProvider();
    }

    /// <summary>
    /// Static convenience method to evaluate analysis results and produce ranked action recommendations.
    /// </summary>
    public static List<ActionRecommendation> EvaluateResult(AnalysisResult result, BaselineSnapshot? baseline = null)
    {
        return new ActionEngine().Evaluate(result, baseline);
    }

    public List<ActionRecommendation> Evaluate(AnalysisResult result, BaselineSnapshot? baseline = null)
    {
        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        var context = new ActionEvaluationContext
        {
            Result = result,
            Baseline = baseline
        };

        var allRecommendations = new List<ActionRecommendation>();
        var rules = _ruleProvider.GetRules();

        foreach (var rule in rules)
        {
            try
            {
                var ruleActions = rule.Evaluate(context);
                if (ruleActions != null && ruleActions.Count > 0)
                {
                    allRecommendations.AddRange(ruleActions);
                }
            }
            catch
            {
                // Isolate individual rule failures from halting the recommendations pipeline
            }
        }

        // Rank actions by Priority ascending (1 is top priority), then Category, then Title
        var ranked = allRecommendations
            .OrderBy(a => a.Priority)
            .ThenBy(a => a.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(a => a.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Assign sequential human-readable IDs: ACT-001, ACT-002, ...
        for (int i = 0; i < ranked.Count; i++)
        {
            ranked[i].Id = $"ACT-{(i + 1):D3}";
        }

        return ranked;
    }

    public string FormatCliTable(IEnumerable<ActionRecommendation> actions)
    {
        var list = actions?.ToList() ?? new List<ActionRecommendation>();
        if (list.Count == 0)
        {
            return "No proactive action recommendations at this time. Codebase metrics are healthy.\n";
        }

        int consoleWidth = ConsoleUtils.GetBoundedConsoleWidth(null);
        var visibleColumns = consoleWidth < 90
            ? new List<string> { "id", "priority", "action", "assignee" }
            : consoleWidth < 120
                ? new List<string> { "id", "priority", "category", "action", "assignee" }
                : new List<string> { "id", "priority", "category", "action", "assignee", "estimated_impact" };

        var table = new ConsoleTableBuilder()
            .WithConsoleWidth(consoleWidth)
            .WithVisibleColumns(visibleColumns)
            .WithBorders(true, true, true)
            .AddColumnEx("id", width: 8, align: "left")
            .AddColumnEx("priority", width: 8, align: "center")
            .AddColumnEx("category", width: 18, align: "left")
            .AddColumnEx("action", align: "left", widthPolicy: WidthPolicy.Stretch, truncation: TruncationStyle.Standard, stretchRatio: 0.5, minWidth: 24)
            .AddColumnEx("assignee", width: 22, align: "left", truncation: TruncationStyle.Standard)
            .AddColumnEx("estimated_impact", width: 25, align: "left", truncation: TruncationStyle.Standard);

        foreach (var action in list)
        {
            table.AddRow(new Dictionary<string, string>
            {
                { "id", action.Id },
                { "priority", $"P{action.Priority}" },
                { "category", action.Category },
                { "action", action.Title },
                { "assignee", action.SuggestedAssignee },
                { "estimated_impact", action.EstimatedImpact }
            });
        }

        return table.Render();
    }

    public string FormatMarkdown(IEnumerable<ActionRecommendation> actions)
    {
        var list = actions?.ToList() ?? new List<ActionRecommendation>();
        var sb = new StringBuilder();

        sb.AppendLine("## 🎯 Recommended Actions");
        sb.AppendLine();

        if (list.Count == 0)
        {
            sb.AppendLine("No proactive action recommendations at this time. Codebase health metrics are within optimal ranges.");
            sb.AppendLine();
            return sb.ToString();
        }

        sb.AppendLine("Prioritized, ROI-ranked actionable recommendations derived from repository metrics and historical trajectories.");
        sb.AppendLine();

        // Summary Table
        sb.AppendLine("| ID | Priority | Category | Action | Suggested Assignee | Estimated Impact |");
        sb.AppendLine("| :--- | :---: | :--- | :--- | :--- | :--- |");

        foreach (var action in list)
        {
            string pTag = $"P{action.Priority}";
            string catDisplay = $"`{action.Category}`";
            sb.AppendLine($"| **{action.Id}** | {pTag} | {catDisplay} | {action.Title} | {action.SuggestedAssignee} | {action.EstimatedImpact} |");
        }
        sb.AppendLine();

        // Detailed Action Rationale Sections
        sb.AppendLine("### 📋 Action Details & Rationale");
        sb.AppendLine();

        foreach (var action in list)
        {
            sb.AppendLine($"#### {action.Id}: {action.Title}");
            sb.AppendLine($"- **Priority:** P{action.Priority}");
            sb.AppendLine($"- **Category:** `{action.Category}`");
            sb.AppendLine($"- **Suggested Assignee:** {action.SuggestedAssignee}");
            sb.AppendLine($"- **Estimated Impact:** {action.EstimatedImpact}");
            if (action.DetectionRules.Count > 0)
            {
                sb.AppendLine($"- **Detection Rules:** {string.Join(", ", action.DetectionRules.Select(r => $"`{r}`"))}");
            }
            sb.AppendLine();
            sb.AppendLine($"> **Rationale:** {action.Narrative}");
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
