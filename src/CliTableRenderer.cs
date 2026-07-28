using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Gitic
{
    public class CliReportFormatter
    {
        private readonly AnalysisResult _result;

        public CliReportFormatter(AnalysisResult result)
        {
            _result = result;
        }

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

        public Task<string> RenderAsync(AnalysisResult result)
        {
            if (result.Analysis.IncludedFileChangeCount == 0)
            {
                return Task.FromResult("No commits matched the selected analysis window. Try --all-time or a wider --since value.\n");
            }

            if (_command == AnalysisCommand.Contributors)
            {
                return Task.FromResult(RenderContributorTable(result));
            }
            if (_command == AnalysisCommand.Contributor)
            {
                return Task.FromResult(RenderSingleContributorTable(result));
            }
            if (_command == AnalysisCommand.Areas)
            {
                return Task.FromResult(RenderAreaTable(result));
            }

            // Default fallback to hotspots
            {
                return Task.FromResult(RenderHotspotTable(result));
            }
        }

        public static string TruncatePath(string path, int maxLength)
        {
            if (string.IsNullOrEmpty(path) || path.Length <= maxLength)
            {
                return path;
            }

            if (maxLength <= 5)
            {
                return path.Substring(path.Length - maxLength);
            }

            int keepEnd = maxLength / 2;
            int keepStart = maxLength - keepEnd - 3; // -3 for "..."

            if (keepStart <= 0)
            {
                return "..." + path.Substring(path.Length - (maxLength - 3));
            }

            return path.Substring(0, keepStart) + "..." + path.Substring(path.Length - keepEnd);
        }

        private string RenderHotspotTable(AnalysisResult result)
        {
            // 1. Determine terminal width
            int consoleWidth = 80;
            try
            {
                if (!Console.IsOutputRedirected)
                {
                    consoleWidth = Console.WindowWidth;
                }
            }
            catch { }

            // Bounded terminal width between 40 and 200
            if (consoleWidth < 40) consoleWidth = 40;
            if (consoleWidth > 200) consoleWidth = 200;

            // 2. Select columns to display
            var visibleColumns = new List<string>();
            if (!string.IsNullOrEmpty(_settings.Columns))
            {
                visibleColumns = _settings.Columns.Split(',')
                    .Select(c => c.Trim().ToLower())
                    .Where(c => !string.IsNullOrEmpty(c))
                    .ToList();
            }
            else
            {
                // Default based on terminal width
                if (consoleWidth < 60)
                {
                    visibleColumns = new List<string> { "file", "attention" };
                }
                else if (consoleWidth < 100)
                {
                    visibleColumns = new List<string> { "file", "attention", "heat", "reasons" };
                }
                else
                {
                    visibleColumns = new List<string> { "file", "attention", "heat", "ownership", "rework", "coordination", "reasons" };
                }
            }

            // 3. Set column properties (alignment and standard width)
            var columnDefs = new Dictionary<string, (string align, int stdWidth)>
            {
                { "file", ("left", 20) }, // will be adjusted dynamically
                { "attention", ("right", 10) },
                { "heat", ("right", 6) },
                { "churn", ("right", 6) },
                { "contributors", ("right", 12) },
                { "ownership", ("right", 9) },
                { "rework", ("right", 8) },
                { "coordination", ("right", 12) },
                { "reasons", ("left", 25) }
            };

            // Calculate other columns width sum
            int otherWidths = 0;
            int colCount = 0;
            foreach (var col in visibleColumns)
            {
                if (col == "file") continue;
                if (columnDefs.TryGetValue(col, out var def))
                {
                    otherWidths += def.stdWidth;
                    colCount++;
                }
            }

            // Allocate remaining width to file column
            int spacing = visibleColumns.Count - 1;
            int fileWidth = consoleWidth - otherWidths - spacing;
            if (fileWidth < 12) fileWidth = 12; // Min file width

            // Re-adjust reasons if width is very large
            int reasonsWidth = 25;
            if (visibleColumns.Contains("reasons"))
            {
                int totalAllocated = fileWidth + otherWidths + spacing;
                if (totalAllocated < consoleWidth)
                {
                    reasonsWidth += (consoleWidth - totalAllocated);
                }
            }

            IConsoleTableBuilder table = new ConsoleTableBuilder();
            foreach (var col in visibleColumns)
            {
                if (col == "file")
                {
                    table.AddColumn("file", fileWidth, "left");
                }
                else if (col == "reasons")
                {
                    table.AddColumn("reasons", reasonsWidth, "left");
                }
                else if (columnDefs.TryGetValue(col, out var def))
                {
                    table.AddColumn(col, def.stdWidth, def.align);
                }
            }

            // 4. Sort and Limit data model
            int limit = _settings.Limit ?? 20;
            var filesToRender = result.Files.Take(limit).ToList();

            foreach (var file in filesToRender)
            {
                var rowCells = new List<string>();
                foreach (var col in visibleColumns)
                {
                    if (col == "file")
                    {
                        rowCells.Add(TruncatePath(file.Path, fileWidth));
                    }
                    else if (col == "attention")
                    {
                        rowCells.Add(_termFormatter.FormatAttention(file.AttentionScore, file.AttentionScore.ToString("F1")));
                    }
                    else if (col == "heat")
                    {
                        rowCells.Add(_termFormatter.FormatHeat(file.HeatScore, file.HeatScore.ToString("F1")));
                    }
                    else if (col == "churn")
                    {
                        rowCells.Add(file.Churn.ToString());
                    }
                    else if (col == "contributors")
                    {
                        rowCells.Add(file.ContributorCount.ToString());
                    }
                    else if (col == "ownership")
                    {
                        double share = file.KnowledgeSilo?.TopOwnerShare ?? 0;
                        rowCells.Add($"{Math.Round(share * 100)}%");
                    }
                    else if (col == "rework")
                    {
                        double rework = file.ReworkRate ?? 0;
                        rowCells.Add($"{Math.Round(rework * 100)}%");
                    }
                    else if (col == "coordination")
                    {
                        double coord = file.CoordinationOverlap ?? 0;
                        rowCells.Add(Math.Round(coord).ToString());
                    }
                    else if (col == "reasons")
                    {
                        string scoreReasons = ScoreReasons(file.ScoreBreakdown);
                        if (scoreReasons.Length > reasonsWidth)
                        {
                            scoreReasons = scoreReasons.Substring(0, reasonsWidth - 3) + "...";
                        }
                        rowCells.Add(scoreReasons);
                    }
                }
                table.AddRow(rowCells);
            }

            return table.Render();
        }

        private string RenderAreaTable(AnalysisResult result)
        {
            IConsoleTableBuilder table = new ConsoleTableBuilder()
                .AddColumn("area", 28, "left")
                .AddColumn("heat", 5, "right")
                .AddColumn("attention", 9, "right")
                .AddColumn("churn", 6, "right")
                .AddColumn("contributors", 12, "right")
                .AddColumn("top activity share", 24, "left")
                .AddColumn("reasons");

            foreach (var area in result.Areas)
            {
                table.AddRow(new List<string>
                {
                    area.Area,
                    area.HeatScore.ToString(),
                    area.AttentionScore.ToString(),
                    area.Churn.ToString(),
                    area.ContributorCount.ToString(),
                    TopContributor(area.Contributors),
                    ScoreReasons(area.ScoreBreakdown)
                });
            }

            return table.Render();
        }

        private string RenderContributorTable(AnalysisResult result)
        {
            IConsoleTableBuilder table = new ConsoleTableBuilder()
                .AddColumn("contributor", 24, "left")
                .AddColumn("activity", 8, "right")
                .AddColumn("top area");

            foreach (var contributor in result.Contributors)
            {
                string topArea = contributor.Areas.Count > 0 ? contributor.Areas[0].Area : "";
                table.AddRow(new List<string>
                {
                    contributor.Name,
                    contributor.TotalActivity.ToString(),
                    topArea
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
            IConsoleTableBuilder table = new ConsoleTableBuilder()
                .AddColumn("area", 28, "left")
                .AddColumn("familiarity", 11, "right")
                .AddColumn("activity", 8, "right")
                .AddColumn("share");

            foreach (var area in contributor.Areas)
            {
                table.AddRow(new List<string>
                {
                    area.Area,
                    area.FamiliarityScore.ToString(),
                    area.Activity.ToString(),
                    $"{Math.Round(area.ActivityShare * 100)}%"
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
                return _isColorEnabled ? $"\x1b[1;31m{text}\x1b[0m" : text;
            }
            else if (score >= 50.0)
            {
                return _isColorEnabled ? $"\x1b[33m{textValue}\x1b[0m" : textValue;
            }
            return textValue;
        }

        public string FormatHeat(double score, string textValue)
        {
            if (score >= 80.0)
            {
                string symbol = _useUnicode ? "🔥  " : "* ";
                string text = $"{symbol}{textValue}";
                return _isColorEnabled ? $"\x1b[1;31m{text}\x1b[0m" : text;
            }
            else if (score >= 50.0)
            {
                return _isColorEnabled ? $"\x1b[33m{textValue}\x1b[0m" : textValue;
            }
            return textValue;
        }
    }
}
