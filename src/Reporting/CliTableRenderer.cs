using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gitic;
// clean code refactor
/// <summary>
/// Formats CLI text reports, appending exclusions and warning information to the rendered table.
/// </summary>
public class CliReportFormatter
{
    private readonly AnalysisResult _result;

    public CliReportFormatter(AnalysisResult result)
    {
        _result = result;
    }

    /// <summary>
    /// Formats the final output string by prepending/appending table metadata, exclusions, and optional warnings.
    /// </summary>
    /// <param name="tableString">The core rendered table as a string.</param>
    /// <param name="includeWarnings">Whether warnings should be formatted and appended.</param>
    /// <returns>A fully-formatted console-ready report string.</returns>
    public string Format(string tableString, bool includeWarnings = false)
    {
        if (tableString.StartsWith("No contributor activity matched"))
        {
            return tableString;
        }

        string output = $"{tableString}\n";
        output += RenderExclusions(_result);
        if (includeWarnings)
        {
            output += RenderWarnings(_result);
        }
        return output;
    }

    private string RenderExclusions(AnalysisResult result)
    {
        if (result.Exclusions == null || result.Exclusions.Count == 0)
        {
            return "";
        }
        string summary = string.Join(", ", result.Exclusions
            .Select(exclusion => $"{exclusion.Category}:{exclusion.Count}"));
        return $"exclusions {summary}\n";
    }

    private string RenderWarnings(AnalysisResult result)
    {
        if (result.Warnings == null || result.Warnings.Count == 0)
        {
            return "";
        }
        return $"warnings {string.Join("; ", result.Warnings)}\n";
    }
}

public class CliTableRenderer : IReportRenderer
{
    private readonly AnalysisCommand _command;
    private readonly AnalysisSettings _settings;
    private readonly TerminalFormatter _termFormatter;

    public CliTableRenderer(AnalysisCommand command, AnalysisSettings? settings = null)
    {
        _command = command;
        _settings = settings ?? DefaultAnalysisSettings.Create();
        _termFormatter = new TerminalFormatter(_settings);
    }

    public Task<string> RenderAsync(AnalysisResult result, System.Threading.CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (result.Analysis.IncludedFileChangeCount == 0)
        {
            return Task.FromResult("No commits matched the selected analysis window. Try --all-time or a wider --since value.\n");
        }

        string content = "";
        if (_command == AnalysisCommand.Contributors)
        {
            content = RenderContributorTable(result);
        }
        else if (_command == AnalysisCommand.Contributor)
        {
            content = RenderSingleContributorTable(result);
        }
        else if (_command == AnalysisCommand.Areas)
        {
            content = RenderAreaTable(result);
        }
        else if (_command == AnalysisCommand.TemporalCoupling)
        {
            content = RenderTemporalCouplingTable(result);
        }
        else if (_command == AnalysisCommand.LeadTime)
        {
            content = RenderLeadTimeTable(result);
        }
        else
        {
            content = RenderHotspotTable(result);
        }

        if (string.Equals(_settings.Format, "human", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(GetBanner(result) + content);
        }

        return Task.FromResult(content);
    }

    private string GetBanner(AnalysisResult result)
    {
        bool useColor = _termFormatter.IsColorEnabled;
        
        string cCyan = useColor ? "\x1b[1;36m" : "";
        string cReset = useColor ? "\x1b[0m" : "";
        string cBold = useColor ? "\x1b[1m" : "";
        string cDim = useColor ? "\x1b[2m" : "";

        var sb = new StringBuilder();
        sb.AppendLine($"{cCyan}   ___ _ _   _      {cReset}");
        sb.AppendLine($"{cCyan}  / __(_) |_(_) ___ {cReset}  {cBold}Strategic Codebase Analysis{cReset}");
        sb.AppendLine($"{cCyan} / _\\ | | __| |/ __|{cReset}  {cDim}Repository: {result.Analysis.RepoRoot}{cReset}");
        sb.AppendLine($"{cCyan}/ /   | | |_| | (__ {cReset}  {cDim}Commits: {result.Analysis.CommitCount} | Files: {result.Analysis.IncludedFileChangeCount}{cReset}");
        sb.AppendLine($"{cCyan}\\/    |_|\\__|_|\\___|{cReset}");
        
        var parts = new List<string>();
        if (result.Settings.AllTime)
        {
            parts.Add("Window: All Time");
        }
        else
        {
            string sinceStr = string.IsNullOrEmpty(result.Settings.Since) ? "Any" : result.Settings.Since;
            parts.Add($"Window: Since {sinceStr}");
        }

        if (!string.IsNullOrEmpty(result.Settings.Path))
        {
            parts.Add($"Filter: {result.Settings.Path}");
        }
        if (result.Settings.Limit != null)
        {
            parts.Add($"Limit: {result.Settings.Limit}");
        }

        sb.AppendLine($"{cDim}{string.Join(" | ", parts)}{cReset}\n");
        return sb.ToString();
    }

    private struct CombinedContributor
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public string Type { get; set; }
        public double Activity { get; set; }
        public double Share { get; set; }
        public double Familiarity { get; set; }
        public string TopAreas { get; set; }
    }

    private int GetConsoleWidth()
    {
        int consoleWidth = 80;
        try
        {
            if (!Console.IsOutputRedirected)
            {
                consoleWidth = Console.WindowWidth;
            }
        }
        catch { }

        if (consoleWidth < 40) consoleWidth = 40;
        if (consoleWidth > 200) consoleWidth = 200;
        return consoleWidth;
    }

    private List<string> GetVisibleColumns(int consoleWidth, Func<int, List<string>> defaultColumnsSelector)
    {
        if (!string.IsNullOrEmpty(_settings.Columns))
        {
            return _settings.Columns.Split(',')
                .Select(c => c.Trim().ToLower())
                .Where(c => !string.IsNullOrEmpty(c))
                .ToList();
        }
        return defaultColumnsSelector(consoleWidth);
    }

    private IConsoleTableBuilder CreateTableBuilder(List<string> visibleColumns)
    {
        bool enableBorders = string.Equals(_settings.Format, "human", StringComparison.OrdinalIgnoreCase);
        return new ConsoleTableBuilder()
            .WithConsoleWidth(GetConsoleWidth())
            .WithVisibleColumns(visibleColumns)
            .WithBorders(enableBorders, _termFormatter.UseUnicode, _termFormatter.IsColorEnabled);
    }

    private string RenderTemporalCouplingTable(AnalysisResult result)
    {
        if (result.TemporalCoupling == null || result.TemporalCoupling.Count == 0)
        {
            return "No temporal coupling pairs found (requires >= 3 shared commits). Try widening the analysis window, modifying limits, or specifying --include-merges.\n";
        }

        int consoleWidth = GetConsoleWidth();
        var visibleColumns = GetVisibleColumns(consoleWidth, _ => new List<string> { "file_a", "file_b", "shared", "coupling" });

        var table = CreateTableBuilder(visibleColumns)
            .AddColumnEx("file_a", align: "left", widthPolicy: WidthPolicy.Stretch, truncation: TruncationStyle.Path, minWidth: 12)
            .AddColumnEx("file_b", align: "left", widthPolicy: WidthPolicy.Stretch, truncation: TruncationStyle.Path, minWidth: 12)
            .AddColumnEx("shared", width: 8, align: "right")
            .AddColumnEx("coupling", width: 10, align: "right");

        int limit = _settings.Limit ?? 20;
        foreach (var item in result.TemporalCoupling.Take(limit))
        {
            double couplingVal = item.CouplingDegree;
            string displayVal = $"{Math.Round(couplingVal * 100)}%";
            string formattedCoupling = couplingVal >= 0.7
                ? _termFormatter.FormatHeat(100.0, displayVal)
                : couplingVal >= 0.5
                    ? _termFormatter.FormatAttention(50.0, displayVal)
                    : displayVal;

            table.AddRow(new Dictionary<string, string>
            {
                { "file_a", item.FileA },
                { "file_b", item.FileB },
                { "shared", item.SharedCommits.ToString() },
                { "coupling", formattedCoupling }
            });
        }

        return table.Render();
    }

    private string RenderLeadTimeTable(AnalysisResult result)
    {
        if (result.LeadTimes?.Merges == null || result.LeadTimes.Merges.Count == 0)
        {
            return "No merge commits in the analysis window; branch lead time is unmeasured. Run with --include-merges or widen the window to measure lead time.\n";
        }

        int consoleWidth = GetConsoleWidth();
        var visibleColumns = GetVisibleColumns(consoleWidth, cw =>
        {
            if (cw < 60)
            {
                return new List<string> { "hash", "lead_time" };
            }
            if (cw < 100)
            {
                return new List<string> { "hash", "date", "lead_time", "author" };
            }
            return new List<string> { "hash", "date", "lead_time", "author", "files", "message" };
        });

        var table = CreateTableBuilder(visibleColumns)
            .AddColumnEx("hash", width: 8, align: "left")
            .AddColumnEx("date", width: 20, align: "left")
            .AddColumnEx("lead_time", width: 15, align: "right")
            .AddColumnEx("author", width: 15, align: "left")
            .AddColumnEx("files", width: 8, align: "right")
            .AddColumnEx("message", align: "left", widthPolicy: WidthPolicy.Stretch, truncation: TruncationStyle.Standard, minWidth: 10);

        int limit = _settings.Limit ?? 20;
        foreach (var m in result.LeadTimes.Merges.Take(limit))
        {
            string hash = m.Hash.Length > 7 ? m.Hash.Substring(0, 7) : m.Hash;
            string date = m.Date.Length > 19 ? m.Date.Substring(0, 19) : m.Date;
            string author = m.Author.Length > 15 ? m.Author.Substring(0, 12) + "..." : m.Author;
            string msg = m.Message.Replace("\r", "").Replace("\n", " ").Trim();

            table.AddRow(new Dictionary<string, string>
            {
                { "hash", hash },
                { "date", date },
                { "lead_time", $"{m.LeadTimeHours:F1} hours" },
                { "author", author },
                { "files", m.FileCount.ToString() },
                { "message", msg }
            });
        }

        string tableRendered = table.Render();
        string prependString = $"Average Lead Time: {result.LeadTimes.AverageLeadTimeHours:F1} hours\n\n";
        return prependString + tableRendered;
    }

    private string RenderHotspotTable(AnalysisResult result)
    {
        if (result.Files == null || result.Files.Count == 0)
        {
            return string.Empty;
        }

        int consoleWidth = GetConsoleWidth();
        var visibleColumns = GetVisibleColumns(consoleWidth, cw =>
        {
            if (cw < 60)
            {
                return new List<string> { "file", "attention" };
            }
            if (cw < 100)
            {
                return new List<string> { "file", "attention", "heat", "reasons" };
            }
            return new List<string> { "file", "attention", "heat", "ownership", "rework", "coordination", "reasons" };
        });

        var table = CreateTableBuilder(visibleColumns)
            .AddColumnEx("file", align: "left", widthPolicy: WidthPolicy.Stretch, truncation: TruncationStyle.Path, minWidth: 12)
            .AddColumnEx("attention", width: 10, align: "right")
            .AddColumnEx("heat", width: 6, align: "right")
            .AddColumnEx("churn", width: 6, align: "right")
            .AddColumnEx("contributors", width: 12, align: "right")
            .AddColumnEx("ownership", width: 9, align: "right")
            .AddColumnEx("rework", width: 8, align: "right")
            .AddColumnEx("coordination", width: 12, align: "right")
            .AddColumnEx("reasons", width: 25, align: "left");

        int limit = _settings.Limit ?? 20;
        foreach (var file in result.Files.Take(limit))
        {
            double share = file.KnowledgeSilo?.TopOwnerShare ?? 0;
            double rework = file.ReworkRate ?? 0;
            double coord = file.CoordinationOverlap ?? 0;

            table.AddRow(new Dictionary<string, string>
            {
                { "file", file.Path },
                { "attention", _termFormatter.FormatAttention(file.AttentionScore, file.AttentionScore.ToString("F1")) },
                { "heat", _termFormatter.FormatHeat(file.HeatScore, file.HeatScore.ToString("F1")) },
                { "churn", file.Churn.ToString() },
                { "contributors", file.ContributorCount.ToString() },
                { "ownership", $"{Math.Round(share * 100)}%" },
                { "rework", $"{Math.Round(rework * 100)}%" },
                { "coordination", Math.Round(coord).ToString() },
                { "reasons", ScoreReasons(file.ScoreBreakdown) }
            });
        }

        return table.Render();
    }

    private string RenderAreaTable(AnalysisResult result)
    {
        if (result.Areas == null || result.Areas.Count == 0)
        {
            return string.Empty;
        }

        int consoleWidth = GetConsoleWidth();
        var visibleColumns = GetVisibleColumns(consoleWidth, cw =>
        {
            if (cw < 60)
            {
                return new List<string> { "area", "attention" };
            }
            if (cw < 100)
            {
                return new List<string> { "area", "attention", "heat", "reasons" };
            }
            return new List<string> { "area", "attention", "heat", "ownership", "rework", "contributors", "reasons" };
        });

        var table = CreateTableBuilder(visibleColumns)
            .AddColumnEx("area", align: "left", widthPolicy: WidthPolicy.Stretch, truncation: TruncationStyle.Path, minWidth: 12)
            .AddColumnEx("attention", width: 10, align: "right")
            .AddColumnEx("heat", width: 6, align: "right")
            .AddColumnEx("ownership", width: 24, align: "left")
            .AddColumnEx("rework", width: 8, align: "right")
            .AddColumnEx("contributors", width: 12, align: "right")
            .AddColumnEx("reasons", width: 25, align: "left");

        int limit = _settings.Limit ?? 20;
        foreach (var area in result.Areas.Take(limit))
        {
            double rework = area.ReworkRate ?? 0;

            table.AddRow(new Dictionary<string, string>
            {
                { "area", area.Area },
                { "attention", _termFormatter.FormatAttention(area.AttentionScore, area.AttentionScore.ToString("F1")) },
                { "heat", _termFormatter.FormatHeat(area.HeatScore, area.HeatScore.ToString("F1")) },
                { "ownership", TopContributor(area.Contributors) },
                { "rework", $"{Math.Round(rework * 100)}%" },
                { "contributors", area.ContributorCount.ToString() },
                { "reasons", ScoreReasons(area.ScoreBreakdown) }
            });
        }

        return table.Render();
    }

    private string RenderContributorTable(AnalysisResult result)
    {
        var combined = new List<CombinedContributor>();

        double totalAllActivity = (result.Contributors?.Sum(c => c.TotalActivity) ?? 0) + (result.Automation?.Sum(c => c.TotalActivity) ?? 0);

        if (result.Contributors != null)
        {
            foreach (var h in result.Contributors)
            {
                double share = totalAllActivity > 0 ? (h.TotalActivity / totalAllActivity) : 0;
                double familiarity = h.Areas.Count > 0 ? h.Areas.Average(a => a.FamiliarityScore) : 0;
                string topAreas = string.Join(", ", h.Areas.Take(2).Select(a => a.Area));
                combined.Add(new CombinedContributor
                {
                    Name = h.Name,
                    Email = h.Email,
                    Type = "human",
                    Activity = h.TotalActivity,
                    Share = share,
                    Familiarity = familiarity,
                    TopAreas = topAreas
                });
            }
        }

        if (result.Automation != null)
        {
            foreach (var b in result.Automation)
            {
                double share = totalAllActivity > 0 ? (b.TotalActivity / totalAllActivity) : 0;
                double familiarity = b.Areas.Count > 0 ? b.Areas.Average(a => a.FamiliarityScore) : 0;
                string topAreas = string.Join(", ", b.Areas.Take(2).Select(a => a.Area));
                combined.Add(new CombinedContributor
                {
                    Name = b.Name,
                    Email = b.Email,
                    Type = "bot",
                    Activity = b.TotalActivity,
                    Share = share,
                    Familiarity = familiarity,
                    TopAreas = topAreas
                });
            }
        }

        // Apply custom Sorting
        if (!string.IsNullOrEmpty(_settings.Sort))
        {
            string sortField = _settings.Sort.ToLower();
            if (sortField == "name" || sortField == "contributor")
            {
                combined = combined.OrderBy(c => c.Name).ToList();
            }
            else if (sortField == "activity" || sortField == "share")
            {
                combined = combined.OrderByDescending(c => c.Activity).ToList();
            }
            else if (sortField == "familiarity")
            {
                combined = combined.OrderByDescending(c => c.Familiarity).ToList();
            }
        }
        else
        {
            // Default sorting by activity
            combined = combined.OrderByDescending(c => c.Activity).ToList();
        }

        int consoleWidth = GetConsoleWidth();
        var visibleColumns = GetVisibleColumns(consoleWidth, cw =>
        {
            if (cw < 60)
            {
                return new List<string> { "contributor", "activity" };
            }
            if (cw < 100)
            {
                return new List<string> { "contributor", "type", "activity", "share", "top areas" };
            }
            return new List<string> { "contributor", "type", "activity", "share", "familiarity", "top areas" };
        });

        var table = CreateTableBuilder(visibleColumns)
            .AddColumnEx("contributor", align: "left", widthPolicy: WidthPolicy.Stretch, truncation: TruncationStyle.Path, stretchRatio: 0.45, minWidth: 12)
            .AddColumnEx("type", width: 8, align: "left")
            .AddColumnEx("activity", width: 10, align: "right")
            .AddColumnEx("share", width: 8, align: "right")
            .AddColumnEx("familiarity", width: 11, align: "right")
            .AddColumnEx("top areas", align: "left", widthPolicy: WidthPolicy.Stretch, truncation: TruncationStyle.Path, stretchRatio: 0.55, minWidth: 12);

        int limit = _settings.Limit ?? 20;
        foreach (var item in combined.Take(limit))
        {
            table.AddRow(new Dictionary<string, string>
            {
                { "contributor", item.Name },
                { "type", item.Type },
                { "activity", item.Activity.ToString("F0") },
                { "share", $"{Math.Round(item.Share * 100)}%" },
                { "familiarity", $"{Math.Round(item.Familiarity)}%" },
                { "top areas", item.TopAreas }
            });
        }

        return table.Render();
    }

    private string RenderSingleContributorTable(AnalysisResult result)
    {
        if (result.Contributors == null || result.Contributors.Count == 0)
        {
            return "No contributor activity matched the selected analysis.\n";
        }

        var contributor = result.Contributors[0];
        bool enableBorders = string.Equals(_settings.Format, "human", StringComparison.OrdinalIgnoreCase);
        IConsoleTableBuilder table = new ConsoleTableBuilder()
            .WithConsoleWidth(GetConsoleWidth())
            .WithBorders(enableBorders, _termFormatter.UseUnicode, _termFormatter.IsColorEnabled)
            .AddColumnEx("area", width: 28, align: "left")
            .AddColumnEx("familiarity", width: 11, align: "right")
            .AddColumnEx("activity", width: 8, align: "right")
            .AddColumnEx("share");

        foreach (var area in contributor.Areas)
        {
            table.AddRow(new Dictionary<string, string>
            {
                { "area", area.Area },
                { "familiarity", area.FamiliarityScore.ToString() },
                { "activity", area.Activity.ToString() },
                { "share", $"{Math.Round(area.ActivityShare * 100)}%" }
            });
        }

        return $"{contributor.Name} <{contributor.Email}>\n{table.Render()}";
    }

    private string TopContributor(List<ContributorShare> contributors)
    {
        if (contributors == null || contributors.Count == 0)
        {
            return "";
        }
        var contributor = contributors[0];
        return $"{contributor.Name} {Math.Round(contributor.ActivityShare * 100)}%";
    }

    private string ScoreReasons(ScoreBreakdown breakdown)
    {
        var reasons = new List<Tuple<string, double>>
        {
            Tuple.Create("churn", breakdown.Churn),
            Tuple.Create("recent", breakdown.Recency),
            Tuple.Create("spread", breakdown.ContributorSpread),
            Tuple.Create("low familiarity", breakdown.LowFamiliarityConcentration)
        };

        var sortedReasons = reasons
            .OrderByDescending(r => r.Item2)
            .Take(2)
            .Where(r => r.Item2 > 0)
            .Select(r => $"{r.Item1} {Math.Round(r.Item2 * 100)}%")
            .ToList();

        return sortedReasons.Count == 0 ? "no activity" : string.Join(", ", sortedReasons);
    }
}

public class TerminalFormatter
{
    private readonly bool _isColorEnabled;
    private readonly bool _useUnicode;

    public bool IsColorEnabled => _isColorEnabled;
    public bool UseUnicode => _useUnicode;

    public TerminalFormatter(AnalysisSettings settings)
    {
        string? termEnv = Environment.GetEnvironmentVariable("TERM");
        bool isNoColorPresent = Environment.GetEnvironmentVariable("NO_COLOR") != null;
        bool isOutputRedirected = Console.IsOutputRedirected;

        string colorOption = settings.Color ?? "auto";
        string formatOption = settings.Format ?? "human";

        if (string.Equals(formatOption, "plain", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(formatOption, "json", StringComparison.OrdinalIgnoreCase))
        {
            _isColorEnabled = false;
            _useUnicode = false;
        }
        else
        {
            if (string.Equals(colorOption, "never", StringComparison.OrdinalIgnoreCase))
            {
                _isColorEnabled = false;
            }
            else if (string.Equals(colorOption, "always", StringComparison.OrdinalIgnoreCase))
            {
                _isColorEnabled = true;
            }
            else // "auto"
            {
                if (isNoColorPresent || string.Equals(termEnv, "dumb", StringComparison.OrdinalIgnoreCase) || isOutputRedirected)
                {
                    _isColorEnabled = false;
                }
                else
                {
                    _isColorEnabled = true;
                }
            }

            if (string.Equals(colorOption, "always", StringComparison.OrdinalIgnoreCase))
            {
                _useUnicode = true;
            }
            else if (string.Equals(termEnv, "dumb", StringComparison.OrdinalIgnoreCase) || isOutputRedirected)
            {
                _useUnicode = false;
            }
            else
            {
                _useUnicode = true;
            }
        }
    }

    public string FormatAttention(double score, string textValue)
    {
        if (score >= 80.0)
        {
            string symbol = _useUnicode ? "⚠️  " : "[!] ";
            string text = $"{symbol}{textValue}";
            return _isColorEnabled ? $"\x1b[38;2;243;139;168m{text}\x1b[0m" : text;
        }
        else if (score >= 50.0)
        {
            return _isColorEnabled ? $"\x1b[38;2;249;226;175m{textValue}\x1b[0m" : textValue;
        }
        return textValue;
    }

    public string FormatHeat(double score, string textValue)
    {
        if (score >= 80.0)
        {
            string symbol = _useUnicode ? "🔥  " : "* ";
            string text = $"{symbol}{textValue}";
            return _isColorEnabled ? $"\x1b[38;2;243;139;168m{text}\x1b[0m" : text;
        }
        else if (score >= 50.0)
        {
            return _isColorEnabled ? $"\x1b[38;2;249;226;175m{textValue}\x1b[0m" : textValue;
        }
        return textValue;
    }
}
// Refactored: Candidate 2
// Clean code review completed.