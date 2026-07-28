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
            if (_command == AnalysisCommand.TemporalCoupling)
            {
                return Task.FromResult(RenderTemporalCouplingTable(result));
            }
            if (_command == AnalysisCommand.LeadTime)
            {
                return Task.FromResult(RenderLeadTimeTable(result));
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

        private string RenderTemporalCouplingTable(AnalysisResult result)
        {
            if (result.TemporalCoupling == null || result.TemporalCoupling.Count == 0)
            {
                return "No temporal coupling pairs found (requires >= 3 shared commits). Try widening the analysis window, modifying limits, or specifying --include-merges.\n";
            }

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
                // Default
                visibleColumns = new List<string> { "file_a", "file_b", "shared", "coupling" };
            }

            // 3. Set column properties
            var columnDefs = new Dictionary<string, (string align, int stdWidth)>
            {
                { "shared", ("right", 8) },
                { "coupling", ("right", 10) }
            };

            int otherWidths = 0;
            foreach (var col in visibleColumns)
            {
                if (col == "file_a" || col == "file_b") continue;
                if (columnDefs.TryGetValue(col, out var def))
                {
                    otherWidths += def.stdWidth;
                }
            }

            int spacing = visibleColumns.Count - 1;
            int remainingWidth = consoleWidth - otherWidths - spacing;
            if (remainingWidth < 24) remainingWidth = 24;

            int fileAWidth = remainingWidth / 2;
            int fileBWidth = remainingWidth - fileAWidth;

            IConsoleTableBuilder table = new ConsoleTableBuilder();
            foreach (var col in visibleColumns)
            {
                if (col == "file_a")
                {
                    table.AddColumn("file_a", fileAWidth, "left");
                }
                else if (col == "file_b")
                {
                    table.AddColumn("file_b", fileBWidth, "left");
                }
                else if (columnDefs.TryGetValue(col, out var def))
                {
                    table.AddColumn(col, def.stdWidth, def.align);
                }
            }

            // 4. Limit and Render
            int limit = _settings.Limit ?? 20;
            var listToRender = result.TemporalCoupling.Take(limit).ToList();

            foreach (var item in listToRender)
            {
                var rowCells = new List<string>();
                foreach (var col in visibleColumns)
                {
                    if (col == "file_a")
                    {
                        rowCells.Add(TruncatePath(item.FileA, fileAWidth));
                    }
                    else if (col == "file_b")
                    {
                        rowCells.Add(TruncatePath(item.FileB, fileBWidth));
                    }
                    else if (col == "shared")
                    {
                        rowCells.Add(item.SharedCommits.ToString());
                    }
                    else if (col == "coupling")
                    {
                        double couplingVal = item.CouplingDegree;
                        string displayVal = $"{Math.Round(couplingVal * 100)}%";
                        if (couplingVal >= 0.7)
                        {
                            rowCells.Add(_termFormatter.FormatHeat(100.0, displayVal));
                        }
                        else if (couplingVal >= 0.5)
                        {
                            rowCells.Add(_termFormatter.FormatAttention(50.0, displayVal));
                        }
                        else
                        {
                            rowCells.Add(displayVal);
                        }
                    }
                }
                table.AddRow(rowCells);
            }

            return table.Render();
        }

        private string RenderLeadTimeTable(AnalysisResult result)
        {
            if (result.LeadTimes == null || result.LeadTimes.Merges.Count == 0)
            {
                return "No merge commits in the analysis window; branch lead time is unmeasured. Run with --include-merges or widen the window to measure lead time.\n";
            }

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
                    visibleColumns = new List<string> { "hash", "lead_time" };
                }
                else if (consoleWidth < 100)
                {
                    visibleColumns = new List<string> { "hash", "date", "lead_time", "author" };
                }
                else
                {
                    visibleColumns = new List<string> { "hash", "date", "lead_time", "author", "files", "message" };
                }
            }

            // 3. Set column properties
            var columnDefs = new Dictionary<string, (string align, int stdWidth)>
            {
                { "hash", ("left", 8) },
                { "date", ("left", 20) },
                { "lead_time", ("right", 15) },
                { "author", ("left", 15) },
                { "files", ("right", 8) },
                { "message", ("left", 30) }
            };

            int otherWidths = 0;
            foreach (var col in visibleColumns)
            {
                if (col == "message") continue;
                if (columnDefs.TryGetValue(col, out var def))
                {
                    otherWidths += def.stdWidth;
                }
            }

            int spacing = visibleColumns.Count - 1;
            int messageWidth = consoleWidth - otherWidths - spacing;
            if (messageWidth < 10) messageWidth = 10;

            IConsoleTableBuilder table = new ConsoleTableBuilder();
            foreach (var col in visibleColumns)
            {
                if (col == "message")
                {
                    table.AddColumn("message", messageWidth, "left");
                }
                else if (columnDefs.TryGetValue(col, out var def))
                {
                    table.AddColumn(col, def.stdWidth, def.align);
                }
            }

            // 4. Limit and Render
            int limit = _settings.Limit ?? 20;
            var listToRender = result.LeadTimes.Merges.Take(limit).ToList();

            foreach (var m in listToRender)
            {
                var rowCells = new List<string>();
                foreach (var col in visibleColumns)
                {
                    if (col == "hash")
                    {
                        rowCells.Add(m.Hash.Length > 7 ? m.Hash.Substring(0, 7) : m.Hash);
                    }
                    else if (col == "date")
                    {
                        rowCells.Add(m.Date.Length > 19 ? m.Date.Substring(0, 19) : m.Date);
                    }
                    else if (col == "lead_time")
                    {
                        rowCells.Add($"{m.LeadTimeHours:F1} hours");
                    }
                    else if (col == "author")
                    {
                        rowCells.Add(m.Author.Length > 15 ? m.Author.Substring(0, 12) + "..." : m.Author);
                    }
                    else if (col == "files")
                    {
                        rowCells.Add(m.FileCount.ToString());
                    }
                    else if (col == "message")
                    {
                        string msg = m.Message.Replace("\r", "").Replace("\n", " ").Trim();
                        rowCells.Add(msg.Length > messageWidth ? msg.Substring(0, messageWidth - 3) + "..." : msg);
                    }
                }
                table.AddRow(rowCells);
            }

            string avgLeadTimeStr = $"Average Lead Time: {result.LeadTimes.AverageLeadTimeHours:F1} hours\n\n";
            return avgLeadTimeStr + table.Render();
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
                    visibleColumns = new List<string> { "area", "attention" };
                }
                else if (consoleWidth < 100)
                {
                    visibleColumns = new List<string> { "area", "attention", "heat", "reasons" };
                }
                else
                {
                    visibleColumns = new List<string> { "area", "attention", "heat", "ownership", "rework", "contributors", "reasons" };
                }
            }

            // 3. Set column properties
            var columnDefs = new Dictionary<string, (string align, int stdWidth)>
            {
                { "area", ("left", 20) }, // dynamically adjusted
                { "attention", ("right", 10) },
                { "heat", ("right", 6) },
                { "ownership", ("left", 24) },
                { "rework", ("right", 8) },
                { "contributors", ("right", 12) },
                { "reasons", ("left", 25) }
            };

            // Calculate other columns width sum
            int otherWidths = 0;
            int colCount = 0;
            foreach (var col in visibleColumns)
            {
                if (col == "area") continue;
                if (columnDefs.TryGetValue(col, out var def))
                {
                    otherWidths += def.stdWidth;
                    colCount++;
                }
            }

            int spacing = visibleColumns.Count - 1;
            int areaWidth = consoleWidth - otherWidths - spacing;
            if (areaWidth < 12) areaWidth = 12;

            int reasonsWidth = 25;
            if (visibleColumns.Contains("reasons"))
            {
                int totalAllocated = areaWidth + otherWidths + spacing;
                if (totalAllocated < consoleWidth)
                {
                    reasonsWidth += (consoleWidth - totalAllocated);
                }
            }

            IConsoleTableBuilder table = new ConsoleTableBuilder();
            foreach (var col in visibleColumns)
            {
                if (col == "area")
                {
                    table.AddColumn("area", areaWidth, "left");
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

            // 4. Sort and Limit Areas (from the result model)
            int limit = _settings.Limit ?? 20;
            var areasToRender = result.Areas.Take(limit).ToList();

            foreach (var area in areasToRender)
            {
                var rowCells = new List<string>();
                foreach (var col in visibleColumns)
                {
                    if (col == "area")
                    {
                        rowCells.Add(TruncatePath(area.Area, areaWidth));
                    }
                    else if (col == "attention")
                    {
                        rowCells.Add(_termFormatter.FormatAttention(area.AttentionScore, area.AttentionScore.ToString("F1")));
                    }
                    else if (col == "heat")
                    {
                        rowCells.Add(_termFormatter.FormatHeat(area.HeatScore, area.HeatScore.ToString("F1")));
                    }
                    else if (col == "ownership")
                    {
                        rowCells.Add(TopContributor(area.Contributors));
                    }
                    else if (col == "rework")
                    {
                        double rework = area.ReworkRate ?? 0;
                        rowCells.Add($"{Math.Round(rework * 100)}%");
                    }
                    else if (col == "contributors")
                    {
                        rowCells.Add(area.ContributorCount.ToString());
                    }
                    else if (col == "reasons")
                    {
                        string scoreReasons = ScoreReasons(area.ScoreBreakdown);
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

        private string RenderContributorTable(AnalysisResult result)
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

            if (consoleWidth < 40) consoleWidth = 40;
            if (consoleWidth > 200) consoleWidth = 200;

            // 2. Combine and compute human + bot metrics
            var combined = new List<(string name, string email, string type, double activity, double share, double familiarity, string topAreas)>();

            double totalAllActivity = result.Contributors.Sum(c => c.TotalActivity) + result.Automation.Sum(c => c.TotalActivity);

            foreach (var h in result.Contributors)
            {
                double share = totalAllActivity > 0 ? (h.TotalActivity / totalAllActivity) : 0;
                double familiarity = h.Areas.Count > 0 ? h.Areas.Average(a => a.FamiliarityScore) : 0;
                string topAreas = string.Join(", ", h.Areas.Take(2).Select(a => a.Area));
                combined.Add((h.Name, h.Email, "human", h.TotalActivity, share, familiarity, topAreas));
            }

            foreach (var b in result.Automation)
            {
                double share = totalAllActivity > 0 ? (b.TotalActivity / totalAllActivity) : 0;
                double familiarity = b.Areas.Count > 0 ? b.Areas.Average(a => a.FamiliarityScore) : 0;
                string topAreas = string.Join(", ", b.Areas.Take(2).Select(a => a.Area));
                combined.Add((b.Name, b.Email, "bot", b.TotalActivity, share, familiarity, topAreas));
            }

            // Apply custom Sorting
            if (!string.IsNullOrEmpty(_settings.Sort))
            {
                string sortField = _settings.Sort.ToLower();
                if (sortField == "name" || sortField == "contributor")
                {
                    combined = combined.OrderBy(c => c.name).ToList();
                }
                else if (sortField == "activity" || sortField == "share")
                {
                    combined = combined.OrderByDescending(c => c.activity).ToList();
                }
                else if (sortField == "familiarity")
                {
                    combined = combined.OrderByDescending(c => c.familiarity).ToList();
                }
            }
            else
            {
                // Default sorting by activity
                combined = combined.OrderByDescending(c => c.activity).ToList();
            }

            // 3. Select columns to display
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
                    visibleColumns = new List<string> { "contributor", "activity" };
                }
                else if (consoleWidth < 100)
                {
                    visibleColumns = new List<string> { "contributor", "type", "activity", "share", "top areas" };
                }
                else
                {
                    visibleColumns = new List<string> { "contributor", "type", "activity", "share", "familiarity", "top areas" };
                }
            }

            // 4. Set column properties
            var columnDefs = new Dictionary<string, (string align, int stdWidth)>
            {
                { "contributor", ("left", 24) }, // adjusted dynamically
                { "type", ("left", 8) },
                { "activity", ("right", 10) },
                { "share", ("right", 8) },
                { "familiarity", ("right", 11) },
                { "top areas", ("left", 30) } // adjusted dynamically
            };

            // Calculate other columns width sum
            int otherWidths = 0;
            int colCount = 0;
            foreach (var col in visibleColumns)
            {
                if (col == "contributor" || col == "top areas") continue;
                if (columnDefs.TryGetValue(col, out var def))
                {
                    otherWidths += def.stdWidth;
                    colCount++;
                }
            }

            int spacing = visibleColumns.Count - 1;
            int availableForStretch = consoleWidth - otherWidths - spacing;
            if (availableForStretch < 20) availableForStretch = 20;

            int contributorWidth = 24;
            int topAreasWidth = 30;

            if (visibleColumns.Contains("contributor") && visibleColumns.Contains("top areas"))
            {
                contributorWidth = (int)(availableForStretch * 0.45);
                topAreasWidth = availableForStretch - contributorWidth;
                if (contributorWidth < 12) contributorWidth = 12;
                if (topAreasWidth < 12) topAreasWidth = 12;
            }
            else if (visibleColumns.Contains("contributor"))
            {
                contributorWidth = availableForStretch;
            }
            else if (visibleColumns.Contains("top areas"))
            {
                topAreasWidth = availableForStretch;
            }

            IConsoleTableBuilder table = new ConsoleTableBuilder();
            foreach (var col in visibleColumns)
            {
                if (col == "contributor")
                {
                    table.AddColumn("contributor", contributorWidth, "left");
                }
                else if (col == "top areas")
                {
                    table.AddColumn("top areas", topAreasWidth, "left");
                }
                else if (columnDefs.TryGetValue(col, out var def))
                {
                    table.AddColumn(col, def.stdWidth, def.align);
                }
            }

            // 5. Apply Limit and Render
            int limit = _settings.Limit ?? 20;
            var listToRender = combined.Take(limit).ToList();

            foreach (var item in listToRender)
            {
                var rowCells = new List<string>();
                foreach (var col in visibleColumns)
                {
                    if (col == "contributor")
                    {
                        rowCells.Add(TruncatePath(item.name, contributorWidth));
                    }
                    else if (col == "type")
                    {
                        rowCells.Add(item.type);
                    }
                    else if (col == "activity")
                    {
                        rowCells.Add(item.activity.ToString("F0"));
                    }
                    else if (col == "share")
                    {
                        rowCells.Add($"{Math.Round(item.share * 100)}%");
                    }
                    else if (col == "familiarity")
                    {
                        rowCells.Add($"{Math.Round(item.familiarity)}%");
                    }
                    else if (col == "top areas")
                    {
                        rowCells.Add(TruncatePath(item.topAreas, topAreasWidth));
                    }
                }
                table.AddRow(rowCells);
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
