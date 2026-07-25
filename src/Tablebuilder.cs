using System;
using System.Collections.Generic;
using System.Linq;

namespace Gitic
{
    public class ColumnDef
    {
        public string Name { get; set; } = string.Empty;
        public int? Width { get; set; }
        public string Align { get; set; } = "left"; // "left" or "right"
    }

    public class ConsoleTableBuilder
    {
        private readonly List<ColumnDef> _columns = new();
        private readonly List<List<string>> _rows = new();

        public ConsoleTableBuilder AddColumn(string name, int? width = null, string align = "left")
        {
            _columns.Add(new ColumnDef { Name = name, Width = width, Align = align });
            return this;
        }

        public ConsoleTableBuilder AddRow(List<string> values)
        {
            _rows.Add(values);
            return this;
        }

        public string Render()
        {
            Func<List<string>, string> formatRow = (cells) =>
            {
                var formattedCells = new List<string>();
                for (int i = 0; i < cells.Count; i++)
                {
                    string cell = cells[i];
                    if (i >= _columns.Count)
                    {
                        formattedCells.Add(cell);
                        continue;
                    }
                    var col = _columns[i];
                    if (col.Width == null)
                    {
                        formattedCells.Add(cell);
                        continue;
                    }

                    int width = col.Width.Value;
                    string align = col.Align ?? "left";

                    if (align == "left")
                    {
                        formattedCells.Add(cell.PadRight(width));
                    }
                    else
                    {
                        formattedCells.Add(cell.PadLeft(width));
                    }
                }
                return string.Join(" ", formattedCells);
            };

            var headerRow = formatRow(_columns.Select(c => c.Name).ToList());
            var dataRows = _rows.Select(row => formatRow(row)).ToList();

            var allRows = new List<string> { headerRow };
            allRows.AddRange(dataRows);

            return string.Join("\n", allRows);
        }
    }
}
