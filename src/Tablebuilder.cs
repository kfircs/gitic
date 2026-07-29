using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Gitic
{
    public enum TruncationStyle
    {
        None,
        Standard,
        Path
    }

    public enum WidthPolicy
    {
        Fixed,
        Stretch
    }

    public interface IConsoleTableBuilder
    {
        IConsoleTableBuilder AddColumn(string name, int? width = null, string align = "left");
        IConsoleTableBuilder AddRow(List<string> values);
        string Render();

        // Deepening Extensions
        IConsoleTableBuilder WithConsoleWidth(int width);
        IConsoleTableBuilder WithVisibleColumns(IEnumerable<string> columns);
        IConsoleTableBuilder AddColumnEx(
            string name,
            int? width = null,
            string align = "left",
            WidthPolicy widthPolicy = WidthPolicy.Fixed,
            TruncationStyle truncation = TruncationStyle.Standard,
            double stretchRatio = 1.0,
            int? minWidth = null,
            int? defaultWidth = null);
        IConsoleTableBuilder AddRow(Dictionary<string, string> values);
    }

    public class ColumnDef
    {
        public string Name { get; set; } = string.Empty;
        public int? Width { get; set; }
        public string Align { get; set; } = "left"; // "left" or "right"
        public WidthPolicy WidthPolicy { get; set; } = WidthPolicy.Fixed;
        public TruncationStyle Truncation { get; set; } = TruncationStyle.Standard;
        public double StretchRatio { get; set; } = 1.0;
        public int? MinWidth { get; set; }
        public int? DefaultWidth { get; set; }
    }

    public class ConsoleTableBuilder : IConsoleTableBuilder
    {
        private readonly List<ColumnDef> _columns = new();
        private readonly List<Dictionary<string, string>> _rows = new();
        private int? _consoleWidth;
        private List<string>? _visibleColumns;

        private static readonly Regex AnsiRegex = new Regex("\x1B\\[[0-9;]*[a-zA-Z]", RegexOptions.Compiled);

        private static int GetVisibleLength(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }
            return AnsiRegex.Replace(text, string.Empty).Length;
        }

        public static string TruncateStandard(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            int visibleLength = GetVisibleLength(value);
            if (visibleLength <= maxLength)
            {
                return value;
            }

            if (maxLength <= 3)
            {
                return StripAnsiAndTruncate(value, maxLength);
            }
            return StripAnsiAndTruncate(value, maxLength - 3) + "...";
        }

        private static string StripAnsiAndTruncate(string value, int length)
        {
            string clean = AnsiRegex.Replace(value, string.Empty);
            if (clean.Length <= length) return clean;
            return clean.Substring(0, length);
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

        public IConsoleTableBuilder WithConsoleWidth(int width)
        {
            _consoleWidth = width;
            return this;
        }

        public IConsoleTableBuilder WithVisibleColumns(IEnumerable<string> columns)
        {
            _visibleColumns = columns?.ToList();
            return this;
        }

        public IConsoleTableBuilder AddColumn(string name, int? width = null, string align = "left")
        {
            return AddColumnEx(
                name,
                width,
                align,
                WidthPolicy.Fixed,
                TruncationStyle.Standard,
                stretchRatio: 1.0,
                minWidth: null,
                defaultWidth: width);
        }

        public IConsoleTableBuilder AddColumnEx(
            string name,
            int? width = null,
            string align = "left",
            WidthPolicy widthPolicy = WidthPolicy.Fixed,
            TruncationStyle truncation = TruncationStyle.Standard,
            double stretchRatio = 1.0,
            int? minWidth = null,
            int? defaultWidth = null)
        {
            _columns.Add(new ColumnDef
            {
                Name = name,
                Width = width,
                Align = align,
                WidthPolicy = widthPolicy,
                Truncation = truncation,
                StretchRatio = stretchRatio,
                MinWidth = minWidth,
                DefaultWidth = defaultWidth
            });
            return this;
        }

        public IConsoleTableBuilder AddRow(List<string> values)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _columns.Count; i++)
            {
                if (values != null && i < values.Count)
                {
                    dict[_columns[i].Name] = values[i];
                }
                else
                {
                    dict[_columns[i].Name] = string.Empty;
                }
            }
            _rows.Add(dict);
            return this;
        }

        public IConsoleTableBuilder AddRow(Dictionary<string, string> values)
        {
            var dict = new Dictionary<string, string>(values ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
            _rows.Add(dict);
            return this;
        }

        public string Render()
        {
            // 1. Determine console width
            int consoleWidth = _consoleWidth ?? 80;
            if (_consoleWidth == null)
            {
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
            }

            // 2. Determine visible columns
            var visibleColDefs = new List<ColumnDef>();
            if (_visibleColumns != null)
            {
                foreach (var colName in _visibleColumns)
                {
                    var colDef = _columns.FirstOrDefault(c => string.Equals(c.Name, colName, StringComparison.OrdinalIgnoreCase));
                    if (colDef != null)
                    {
                        visibleColDefs.Add(colDef);
                    }
                }
            }
            else
            {
                visibleColDefs.AddRange(_columns);
            }

            // 3. Compute Column Widths (distributing stretch columns)
            var fixedCols = visibleColDefs.Where(c => c.WidthPolicy == WidthPolicy.Fixed).ToList();
            var stretchCols = visibleColDefs.Where(c => c.WidthPolicy == WidthPolicy.Stretch).ToList();

            int spacing = visibleColDefs.Count - 1;
            int fixedWidthsSum = fixedCols.Sum(c => c.Width ?? 0);
            int remainingWidth = consoleWidth - fixedWidthsSum - spacing;
            if (remainingWidth < 0) remainingWidth = 0;

            var assignedWidths = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int stretchCount = stretchCols.Count;
            if (stretchCount > 0)
            {
                double totalRatio = stretchCols.Sum(c => c.StretchRatio);
                if (totalRatio <= 0) totalRatio = 1.0;

                int allocated = 0;
                for (int i = 0; i < stretchCount; i++)
                {
                    var col = stretchCols[i];
                    int share = (i == stretchCount - 1)
                        ? (remainingWidth - allocated)
                        : (int)Math.Floor(remainingWidth * (col.StretchRatio / totalRatio));

                    if (col.MinWidth != null && share < col.MinWidth.Value)
                    {
                        share = col.MinWidth.Value;
                    }
                    assignedWidths[col.Name] = share;
                    allocated += share;
                }
            }

            // 4. Format row helper
            string FormatRow(List<string> cells)
            {
                var formattedCells = new List<string>();
                for (int i = 0; i < cells.Count; i++)
                {
                    string cell = cells[i] ?? string.Empty;
                    if (i >= visibleColDefs.Count)
                    {
                        formattedCells.Add(cell);
                        continue;
                    }
                    var col = visibleColDefs[i];

                    int? colWidth = null;
                    if (col.WidthPolicy == WidthPolicy.Stretch)
                    {
                        if (assignedWidths.TryGetValue(col.Name, out int w))
                        {
                            colWidth = w;
                        }
                    }
                    else
                    {
                        colWidth = col.Width;
                    }

                    if (colWidth == null)
                    {
                        formattedCells.Add(cell);
                        continue;
                    }

                    int width = colWidth.Value;
                    string align = col.Align ?? "left";

                    // Truncate cell value if necessary
                    if (col.Truncation == TruncationStyle.Path)
                    {
                        cell = TruncatePath(cell, width);
                    }
                    else if (col.Truncation == TruncationStyle.Standard)
                    {
                        cell = TruncateStandard(cell, width);
                    }

                    int visibleLength = GetVisibleLength(cell);
                    int paddingLength = width - visibleLength;

                    if (paddingLength <= 0)
                    {
                        formattedCells.Add(cell);
                    }
                    else
                    {
                        string padding = new string(' ', paddingLength);
                        if (align == "left")
                        {
                            formattedCells.Add(cell + padding);
                        }
                        else
                        {
                            formattedCells.Add(padding + cell);
                        }
                    }
                }
                return string.Join(" ", formattedCells);
            }

            var headerRow = FormatRow(visibleColDefs.Select(c => c.Name).ToList());

            var dataRows = new List<string>();
            foreach (var rowDict in _rows)
            {
                var cells = new List<string>();
                foreach (var col in visibleColDefs)
                {
                    if (rowDict.TryGetValue(col.Name, out string val))
                    {
                        cells.Add(val);
                    }
                    else
                    {
                        cells.Add(string.Empty);
                    }
                }
                dataRows.Add(FormatRow(cells));
            }

            var allRows = new List<string> { headerRow };
            allRows.AddRange(dataRows);

            return string.Join("\n", allRows);
        }
    }
}