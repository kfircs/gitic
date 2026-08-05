using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gitic;

public class TuiExplorerState
{
    public TuiNode Node { get; set; } = new();
    public int SelectedIndex { get; set; }
    public int ScrollOffset { get; set; }
}

public class TuiExplorer
{
    private readonly Stack<TuiExplorerState> _history = new();
    private TuiExplorerState _current = new();
    private int _perspective = 1; // 1: Lines & Structure, 2: Work Classification, 3: Code Rot, 4: Review Collaboration, 5: AI Code Strain
    private AnalysisResult _result = new();
    private bool _running = true;

    public async Task LaunchAsync(AnalysisResult result)
    {
        _result = result;
        var rootNode = TuiNode.BuildTree(result.Files);
        rootNode.Name = "Repository Root";

        _current = new TuiExplorerState
        {
            Node = rootNode,
            SelectedIndex = 0,
            ScrollOffset = 0
        };

        bool oldCursorVisible = true;
        try
        {
            oldCursorVisible = Console.CursorVisible;
            Console.CursorVisible = false;
        }
        catch { }

        try
        {
            while (_running)
            {
                Render();
                await HandleInputAsync();
            }
        }
        finally
        {
            try { Console.CursorVisible = oldCursorVisible; } catch { }
            Console.Clear();
        }
    }

    private void Render()
    {
        Console.Clear();
        int width = 80;
        int height = 24;
        try
        {
            if (!Console.IsOutputRedirected && Console.WindowWidth > 0) width = Console.WindowWidth;
            if (!Console.IsOutputRedirected && Console.WindowHeight > 0) height = Console.WindowHeight;
        }
        catch { }

        // Enforce safe minimum bounds and subtract 1 row to prevent terminal scrolling
        width = Math.Max(70, width);
        height = Math.Max(15, height - 1);

        // We want the total visible width of borders and content lines to be exactly safeWidth (or width - 2 for safety)
        int safeWidth = width - 2; // Subtract 2 characters for safety against terminal border wraps
        int leftWidth = (int)(safeWidth * 0.33);
        leftWidth = Math.Max(20, leftWidth);
        int rightWidth = safeWidth - leftWidth - 7; // leaves 7 chars for: border (1) + space (1) + border (1) + space (1) + border (1) + spaces (2)
        rightWidth = Math.Max(30, rightWidth);

        // Header Border and Line Layouts (exactly safeWidth long!)
        string title = " 🖥️  Gitic Interactive TUI Explorer ";
        string perspectiveTitle = $"Perspective: [{_perspective}] {GetPerspectiveName()}";
        string breadcrumbs = GetBreadcrumbs(safeWidth - 4);

        string topBorder = "┌" + new string('─', leftWidth + 2) + "┬" + new string('─', rightWidth + 2) + "┐";
        string midBorder1 = "├" + new string('─', leftWidth + 2) + "┼" + new string('─', rightWidth + 2) + "┤";
        string midBorder2 = "├" + new string('─', leftWidth + 2) + "┼" + new string('─', rightWidth + 2) + "┤";

        Console.WriteLine($"\x1b[38;2;203;166;247m{topBorder}\x1b[0m");
        Console.WriteLine($"\x1b[38;2;203;166;247m│\x1b[0m \x1b[1;38;2;137;180;250m{PadRightAnsi(title, leftWidth)}\x1b[0m \x1b[38;2;203;166;247m│\x1b[0m \x1b[38;2;166;227;161m{PadRightAnsi(perspectiveTitle, rightWidth)}\x1b[0m \x1b[38;2;203;166;247m│\x1b[0m");
        Console.WriteLine($"\x1b[38;2;108;112;147m{midBorder1}\x1b[0m");
        Console.WriteLine($"\x1b[38;2;203;166;247m│\x1b[0m \x1b[38;2;180;190;254m{PadRightAnsi("Breadcrumbs: " + breadcrumbs, leftWidth + rightWidth + 3)}\x1b[0m \x1b[38;2;203;166;247m│\x1b[0m");
        Console.WriteLine($"\x1b[38;2;108;112;147m{midBorder2}\x1b[0m");

        // List & Detail Panel Setup
        int contentHeight = height - 8; // Adjust for header and footer rows
        var children = _current.Node.Children;
        
        // Ensure index in bounds
        if (_current.SelectedIndex < 0) _current.SelectedIndex = 0;
        if (children.Count > 0 && _current.SelectedIndex >= children.Count) _current.SelectedIndex = children.Count - 1;

        // Manage scrolling
        if (_current.SelectedIndex < _current.ScrollOffset)
        {
            _current.ScrollOffset = _current.SelectedIndex;
        }
        else if (children.Count > 0 && _current.SelectedIndex >= _current.ScrollOffset + contentHeight)
        {
            _current.ScrollOffset = _current.SelectedIndex - contentHeight + 1;
        }

        TuiNode? selectedNode = children.Count > 0 ? children[_current.SelectedIndex] : null;

        for (int i = 0; i < contentHeight; i++)
        {
            int itemIndex = _current.ScrollOffset + i;

            // Draw Left Column: List item
            string leftCell = "";
            if (itemIndex < children.Count)
            {
                var child = children[itemIndex];
                string prefix = child.IsDirectory ? "📁 " : "📄 ";
                string name = child.Name;
                if (child.IsDirectory && child.FileCount > 0)
                {
                    name += $" ({child.FileCount})";
                }

                if (itemIndex == _current.SelectedIndex)
                {
                    leftCell = $"\x1b[1;38;2;137;180;250m󰅂 \x1b[4;38;2;137;180;250m{prefix}{name}\x1b[0m";
                }
                else
                {
                    leftCell = $"  {prefix}{name}";
                }
            }
            else if (children.Count == 0 && i == 0)
            {
                leftCell = "  (No code files here)";
            }

            // Draw Right Column: Sidebar segment
            string rightCell = GetRightSidebarLine(selectedNode, i, rightWidth);

            Console.WriteLine($"\x1b[38;2;203;166;247m│\x1b[0m {PadRightAnsi(leftCell, leftWidth)} \x1b[38;2;203;166;247m│\x1b[0m {PadRightAnsi(rightCell, rightWidth)} \x1b[38;2;203;166;247m│\x1b[0m");
        }

        // Footer
        string botBorder = "└" + new string('─', leftWidth + 2) + "┴" + new string('─', rightWidth + 2) + "┘";
        Console.WriteLine($"\x1b[38;2;203;166;247m{botBorder}\x1b[0m");
        
        string shortcuts = "\x1b[1;38;2;249;226;175m Shortcuts:\x1b[0m j/k/↑/↓:Move │ l/󰌑:Enter │ h/Esc/Backspace:Back │ Tab/1-5:Perspectives │ q:Quit";
        Console.Write(PadRightAnsi(shortcuts, leftWidth + rightWidth + 7));
    }

    private string GetBreadcrumbs(int maxWidth)
    {
        var list = new List<string>();
        foreach (var h in _history)
        {
            list.Insert(0, h.Node.Name);
        }
        list.Add(_current.Node.Name);

        string joined = string.Join(" 󰅂 ", list);
        if (joined.Length > maxWidth && joined.Contains(" 󰅂 "))
        {
            return "..." + joined.Substring(joined.IndexOf(" 󰅂 "));
        }
        return joined;
    }

    private string GetPerspectiveName() => _perspective switch
    {
        1 => "Lines & Structure",
        2 => "Work Classification",
        3 => "Code Rot / Zombies",
        4 => "Review Collaboration",
        5 => "AI Code Strain",
        _ => "Unknown"
    };

    private string GetRightSidebarLine(TuiNode? node, int lineIndex, int width)
    {
        if (node == null) return lineIndex == 0 ? "\x1b[38;2;108;112;147m(Select an item to view stats)\x1b[0m" : "";

        return _perspective switch
        {
            1 => RenderLinesAndStructure(node, lineIndex, width),
            2 => RenderWorkClassification(node, lineIndex, width),
            3 => RenderCodeRot(node, lineIndex, width),
            4 => RenderReviewCollaboration(node, lineIndex, width),
            5 => RenderAiCodeStrain(node, lineIndex, width),
            _ => ""
        };
    }

    #region Perspective 1: Lines & Structure
    private string RenderLinesAndStructure(TuiNode node, int line, int width)
    {
        if (node.IsDirectory)
        {
            var minFile = node.FindMinLoCFile();
            var maxFile = node.FindMaxLoCFile();
            string minName = minFile != null ? minFile.Name : "N/A";
            string maxName = maxFile != null ? maxFile.Name : "N/A";
            int avgLines = node.FileCount > 0 ? node.TotalLines / node.FileCount : 0;

            double minPct = node.MaxLines > 0 ? (double)node.MinLines / node.MaxLines : 0;
            double avgPct = node.MaxLines > 0 ? (double)avgLines / node.MaxLines : 0;

            // Progress bar characters
            int barLen = 15;
            int minBlocks = (int)Math.Round(minPct * barLen);
            int avgBlocks = (int)Math.Round(avgPct * barLen);
            int maxBlocks = barLen;

            // Styled progress bars using Teal for filled blocks and Gray for shaded blocks
            string minBar = $"{CatppuccinMocha.Gray}[{CatppuccinMocha.Teal}{new string('█', minBlocks)}{CatppuccinMocha.Gray}{new string('░', barLen - minBlocks)}]{CatppuccinMocha.Reset}";
            string avgBar = $"{CatppuccinMocha.Gray}[{CatppuccinMocha.Teal}{new string('█', avgBlocks)}{CatppuccinMocha.Gray}{new string('░', barLen - avgBlocks)}]{CatppuccinMocha.Reset}";
            string maxBar = $"{CatppuccinMocha.Gray}[{CatppuccinMocha.Teal}{new string('█', maxBlocks)}{CatppuccinMocha.Gray}{new string('░', barLen - maxBlocks)}]{CatppuccinMocha.Reset}";

            switch (line)
            {
                case 0: 
                    return $"{CatppuccinMocha.Peach}📂 {(node.RelativePath == "" ? "Repository Statistics" : "Module Statistics: " + node.RelativePath)}{CatppuccinMocha.Reset}";
                case 1: 
                    return $"  {CatppuccinMocha.Text}Total Lines of Code:   {CatppuccinMocha.Yellow}{node.TotalLines:N0}{CatppuccinMocha.Text} lines{CatppuccinMocha.Reset}";
                case 2: 
                    return $"  {CatppuccinMocha.Text}Valid Code Files:      {CatppuccinMocha.Yellow}{node.FileCount:N0}{CatppuccinMocha.Text} files{CatppuccinMocha.Reset}";
                case 3: 
                    return "";
                case 4: 
                    return $"\x1b[1m{CatppuccinMocha.Pink}Lines of Code Distribution:{CatppuccinMocha.Reset}";
                case 5: 
                    return $"  {CatppuccinMocha.Gray}├─{CatppuccinMocha.Text} Minimum File LoC:   {CatppuccinMocha.Green}{node.MinLines:N0}{CatppuccinMocha.Text}  {CatppuccinMocha.Lavender}({minName}){CatppuccinMocha.Reset}";
                case 6: 
                    return $"  {CatppuccinMocha.Gray}├─{CatppuccinMocha.Text} Average File LoC:   {CatppuccinMocha.Green}{avgLines:N0}{CatppuccinMocha.Reset}";
                case 7: 
                    return $"  {CatppuccinMocha.Gray}└─{CatppuccinMocha.Text} Maximum File LoC:   {CatppuccinMocha.Green}{node.MaxLines:N0}{CatppuccinMocha.Text}  {CatppuccinMocha.Lavender}({maxName}){CatppuccinMocha.Reset}";
                case 8: 
                    return "";
                case 9: 
                    return $"\x1b[1m{CatppuccinMocha.Pink}LoC Size Distribution Curve:{CatppuccinMocha.Reset}";
                case 10: 
                    return $"  {minBar} {CatppuccinMocha.Text}Min ({CatppuccinMocha.Green}{node.MinLines}{CatppuccinMocha.Text})       {CatppuccinMocha.Blue}■───{CatppuccinMocha.Teal} {minPct:P0}{CatppuccinMocha.Reset}";
                case 11: 
                    return $"  {avgBar} {CatppuccinMocha.Text}Avg ({CatppuccinMocha.Green}{avgLines}{CatppuccinMocha.Text})      {CatppuccinMocha.Blue}■──────────{CatppuccinMocha.Teal} {avgPct:P0}{CatppuccinMocha.Reset}";
                case 12: 
                    return $"  {maxBar} {CatppuccinMocha.Text}Max ({CatppuccinMocha.Green}{node.MaxLines}{CatppuccinMocha.Text}) {CatppuccinMocha.Blue}■──────────────────{CatppuccinMocha.Teal} 100%{CatppuccinMocha.Reset}";
                case 13: 
                    return "";
                case 14:
                    {
                        if (node.RelativePath == "")
                        {
                            return $"\x1b[1m{CatppuccinMocha.Pink}Key Contributors:{CatppuccinMocha.Reset}";
                        }
                        else
                        {
                            return $"\x1b[1m{CatppuccinMocha.Pink}Risk Factors:{CatppuccinMocha.Reset}";
                        }
                    }
                case 15:
                    {
                        if (node.RelativePath == "")
                        {
                            var contribs = GetDirectoryContributorsLines(node);
                            return contribs.Count > 0 ? $"{CatppuccinMocha.Text}{contribs[0]}{CatppuccinMocha.Reset}" : "";
                        }
                        else
                        {
                            var highAttFile = node.FindHighestAttentionFile();
                            string highAttName = highAttFile != null ? highAttFile.Name : "N/A";
                            double highAttScore = highAttFile?.FileMetric?.AttentionScore ?? 0.0;
                            string alertColor = highAttScore > 60.0 ? CatppuccinMocha.Red : highAttScore > 30.0 ? CatppuccinMocha.Yellow : CatppuccinMocha.Green;
                            return $"  {CatppuccinMocha.Gray}└─{CatppuccinMocha.Text} Highest Attention Score:  {alertColor}{highAttScore:F1}{CatppuccinMocha.Text} {CatppuccinMocha.Lavender}({highAttName}){CatppuccinMocha.Reset}";
                        }
                    }
                case 16:
                    {
                        if (node.RelativePath == "")
                        {
                            var contribs = GetDirectoryContributorsLines(node);
                            return contribs.Count > 1 ? $"{CatppuccinMocha.Text}{contribs[1]}{CatppuccinMocha.Reset}" : "";
                        }
                        return "";
                    }
                default: return "";
            }
        }
        else
        {
            var metric = node.FileMetric;
            if (metric == null) return "";

            switch (line)
            {
                case 0: 
                    return $"{CatppuccinMocha.Peach}📄 File Statistics: {node.Name}{CatppuccinMocha.Reset}";
                case 1: 
                    return $"  {CatppuccinMocha.Text}Lines of Code:         {CatppuccinMocha.Yellow}{node.TotalLines:N0}{CatppuccinMocha.Text} lines{CatppuccinMocha.Reset}";
                case 2: 
                    return $"  {CatppuccinMocha.Text}File Physical Size:     {CatppuccinMocha.Yellow}{FormatBytes(metric.Size ?? 0)}{CatppuccinMocha.Reset}";
                case 3: 
                    return $"  {CatppuccinMocha.Text}Max Line Width:        {CatppuccinMocha.Yellow}{node.MaxWidth:N0}{CatppuccinMocha.Text} characters{CatppuccinMocha.Reset}";
                case 4: 
                    return "";
                case 5: 
                    return $"\x1b[1m{CatppuccinMocha.Pink}Git Metrics:{CatppuccinMocha.Reset}";
                case 6: 
                    return $"  {CatppuccinMocha.Gray}├─{CatppuccinMocha.Text} Cumulative Touches:  {CatppuccinMocha.Green}{metric.Touches:N0}{CatppuccinMocha.Text} times{CatppuccinMocha.Reset}";
                case 7: 
                    return $"  {CatppuccinMocha.Gray}├─{CatppuccinMocha.Text} Cumulative Churn:    {CatppuccinMocha.Green}{metric.Churn:N0}{CatppuccinMocha.Text} lines changed{CatppuccinMocha.Reset}";
                case 8: 
                    return $"  {CatppuccinMocha.Gray}└─{CatppuccinMocha.Text} Last Touched:        {CatppuccinMocha.Green}{metric.LastTouched}{CatppuccinMocha.Text} {CatppuccinMocha.Lavender}({GetDaysAgoString(metric.LastTouched)}){CatppuccinMocha.Reset}";
                case 9: 
                    return "";
                case 10: 
                    return $"\x1b[1m{CatppuccinMocha.Pink}Hotspot Risk Profile:{CatppuccinMocha.Reset}";
                case 11: 
                    {
                        string attAlertColor = metric.AttentionScore > 60.0 ? CatppuccinMocha.Red : metric.AttentionScore > 30.0 ? CatppuccinMocha.Yellow : CatppuccinMocha.Green;
                        string alertText = metric.AttentionScore > 60.0 ? "[⚠ High Attention]" : metric.AttentionScore > 30.0 ? "[■ Moderate Attention]" : "[Normal]";
                        return $"  {CatppuccinMocha.Gray}├─{CatppuccinMocha.Text} Attention Score:     {attAlertColor}{metric.AttentionScore:F1}{CatppuccinMocha.Text}  {attAlertColor}{alertText}{CatppuccinMocha.Reset}";
                    }
                case 12: 
                    {
                        string heatAlertColor = metric.HeatScore > 60.0 ? CatppuccinMocha.Red : metric.HeatScore > 30.0 ? CatppuccinMocha.Yellow : CatppuccinMocha.Green;
                        string alertText = metric.HeatScore > 60.0 ? "[🔥 High Heat]" : metric.HeatScore > 30.0 ? "[■ Moderate Heat]" : "[Normal]";
                        return $"  {CatppuccinMocha.Gray}└─{CatppuccinMocha.Text} Heat Score:          {heatAlertColor}{metric.HeatScore:F1}{CatppuccinMocha.Text}   {heatAlertColor}{alertText}{CatppuccinMocha.Reset}";
                    }
                case 13: 
                    return "";
                case 14: 
                    return $"\x1b[1m{CatppuccinMocha.Pink}Ownership Spread:{CatppuccinMocha.Reset}";
                case 15:
                    {
                        var contribs = GetContributorsLines(metric);
                        return contribs.Count > 0 ? $"{CatppuccinMocha.Text}{contribs[0]}{CatppuccinMocha.Reset}" : "";
                    }
                case 16:
                    {
                        var contribs = GetContributorsLines(metric);
                        return contribs.Count > 1 ? $"{CatppuccinMocha.Text}{contribs[1]}{CatppuccinMocha.Reset}" : "";
                    }
                default: return "";
            }
        }
    }

    private static string GetDaysAgoString(string lastTouched)
    {
        if (string.IsNullOrEmpty(lastTouched)) return "N/A";
        if (DateTime.TryParse(lastTouched, out var date))
        {
            int days = (int)(DateTime.Now - date).TotalDays;
            return days <= 0 ? "today" : days == 1 ? "1 day ago" : $"{days} days ago";
        }
        return "N/A";
    }

    private static List<string> GetContributorsLines(FileMetric metric)
    {
        var result = new List<string>();
        if (metric.Contributors == null || metric.Contributors.Count == 0)
        {
            result.Add("  - No contributor record");
            return result;
        }

        var sorted = metric.Contributors.OrderByDescending(c => c.ActivityShare).Take(2).ToList();
        foreach (var c in sorted)
        {
            result.Add($"  - {c.Name}:                  {c.ActivityShare:P0} activity share");
        }
        return result;
    }

    private List<string> GetDirectoryContributorsLines(TuiNode node)
    {
        var result = new List<string>();
        var dict = new Dictionary<string, double>();
        AccumulateContributors(node, dict);
        if (dict.Count == 0)
        {
            result.Add("  - No contributor record");
            return result;
        }

        double total = dict.Values.Sum();
        if (total == 0) total = 1;

        var sorted = dict.OrderByDescending(kv => kv.Value).Take(2).ToList();
        foreach (var kv in sorted)
        {
            double share = kv.Value / total;
            result.Add($"  - {kv.Key} ({share:P0} share)");
        }
        return result;
    }

    private void AccumulateContributors(TuiNode node, Dictionary<string, double> dict)
    {
        if (!node.IsDirectory)
        {
            if (node.FileMetric?.Contributors != null)
            {
                foreach (var c in node.FileMetric.Contributors)
                {
                    if (dict.ContainsKey(c.Name))
                        dict[c.Name] += c.Activity;
                    else
                        dict[c.Name] = c.Activity;
                }
            }
            return;
        }
        foreach (var child in node.Children)
        {
            AccumulateContributors(child, dict);
        }
    }
    #endregion

    #region Perspective 2: Work Classification
    private string RenderWorkClassification(TuiNode node, int line, int width)
    {
        if (_result.CuratedReports == null) return line == 0 ? "No classification report data available." : "";
        var wc = _result.CuratedReports.WorkClassification;
        int total = wc.Features + wc.Bugs + wc.TechnicalDebt + wc.Chores + wc.Unclassified;
        if (total == 0) total = 1;

        double featPct = (double)wc.Features / total;
        double bugPct = (double)wc.Bugs / total;
        double debtPct = (double)wc.TechnicalDebt / total;
        double chorePct = (double)wc.Chores / total;

        switch (line)
        {
            case 0: return $"\x1b[1;38;2;137;180;250m📊 Work Classification perspective\x1b[0m (Repository Scope)";
            case 1: return $"\x1b[38;2;108;112;147mHow commits are distributed by type\x1b[0m";
            case 2: return "";
            case 3: return $"\x1b[1mCommit Statistics:\x1b[0m";
            case 4: return $"  ├─ Features:       {wc.Features,4}  ({featPct:P1})";
            case 5: return $"  ├─ Bugfixes:       {wc.Bugs,4}  ({bugPct:P1})";
            case 6: return $"  ├─ Tech Debt:      {wc.TechnicalDebt,4}  ({debtPct:P1})";
            case 7: return $"  ├─ Chores:         {wc.Chores,4}  ({chorePct:P1})";
            case 8: return $"  └─ Unclassified:   {wc.Unclassified,4}";
            case 9: return "";
            case 10: return $"\x1b[1mVisual Distribution Bar:\x1b[0m";
            case 11:
                {
                    int barWidth = Math.Max(20, width - 15);
                    int fW = (int)(featPct * barWidth);
                    int bW = (int)(bugPct * barWidth);
                    int dW = (int)(debtPct * barWidth);
                    int cW = (int)(chorePct * barWidth);
                    int uW = Math.Max(0, barWidth - fW - bW - dW - cW);

                    var sb = new StringBuilder();
                    sb.Append("  [");
                    sb.Append("\x1b[38;2;166;227;161m" + new string('█', fW)); // Green for Feat
                    sb.Append("\x1b[38;2;243;139;168m" + new string('█', bW)); // Red for Bug
                    sb.Append("\x1b[38;2;249;226;175m" + new string('█', dW)); // Yellow for Debt
                    sb.Append("\x1b[38;2;137;180;250m" + new string('█', cW)); // Blue for Chore
                    sb.Append("\x1b[38;2;147;153;178m" + new string('░', uW)); // Gray for Unclassified
                    sb.Append("\x1b[0m]");
                    return sb.ToString();
                }
            case 12: return "   \x1b[38;2;166;227;161m■ Feat\x1b[0m  \x1b[38;2;243;139;168m■ Bug\x1b[0m  \x1b[38;2;249;226;175m■ Debt\x1b[0m  \x1b[38;2;137;180;250m■ Chore\x1b[0m  \x1b[38;2;147;153;178m░ Unclassified\x1b[0m";
            default: return "";
        }
    }
    #endregion

    #region Perspective 3: Code Rot / Zombies
    private string RenderCodeRot(TuiNode node, int line, int width)
    {
        if (_result.CuratedReports == null) return line == 0 ? "No Code Rot report data available." : "";
        var rot = _result.CuratedReports.CodeRot;

        switch (line)
        {
            case 0: return $"\x1b[1;38;2;249;226;175m🧟 Code Rot & Zombie Files\x1b[0m";
            case 1: return $"\x1b[38;2;108;112;147mFiles untouched for more than 1 year\x1b[0m";
            case 2: return "";
            case 3: return $"\x1b[1mSummary Stats (Full Repo):\x1b[0m";
            case 4: return $"  ├─ Zombie Files Count:    {rot.ZombieFileCount:N0}";
            case 5: return $"  └─ Zombie Lines of Code:  {rot.ZombieLines:N0}";
            case 6: return "";
            case 7: return $"\x1b[1mScope-Specific Analysis:\x1b[0m";
            case 8: return $"  Hovered: \x1b[1;38;2;137;180;250m{node.Name}\x1b[0m";
            case 9:
                {
                    // Let's count how many files under the hovered node are zombies (last touched > 365 days ago)
                    int zombieCountInHovered = CountZombiesUnderNode(node);
                    return $"  ├─ Zombie Files in this folder: {zombieCountInHovered:N0}";
                }
            case 10:
                {
                    long zombieLinesInHovered = CountZombieLinesUnderNode(node);
                    return $"  └─ Zombie Lines in this folder: {zombieLinesInHovered:N0}";
                }
            case 11: return "";
            case 12: return $"\x1b[38;2;108;112;147mZombie code increases maintenance cognitive overhead.\x1b[0m";
            case 13: return $"\x1b[38;2;108;112;147mConsider refactoring or pruning unused files.\x1b[0m";
            default: return "";
        }
    }

    private int CountZombiesUnderNode(TuiNode node)
    {
        if (!node.IsDirectory)
        {
            return IsZombieFile(node.FileMetric) ? 1 : 0;
        }
        return node.Children.Sum(CountZombiesUnderNode);
    }

    private long CountZombieLinesUnderNode(TuiNode node)
    {
        if (!node.IsDirectory)
        {
            return IsZombieFile(node.FileMetric) ? (node.FileMetric?.Lines ?? 0) : 0;
        }
        return node.Children.Sum(CountZombieLinesUnderNode);
    }

    private bool IsZombieFile(FileMetric? f)
    {
        if (f == null || string.IsNullOrEmpty(f.LastTouched)) return false;
        if (DateTime.TryParse(f.LastTouched, out var date))
        {
            return (DateTime.Now - date).TotalDays > 365;
        }
        return false;
    }
    #endregion

    #region Perspective 4: Review Collaboration
    private string RenderReviewCollaboration(TuiNode node, int line, int width)
    {
        if (_result.CuratedReports == null) return line == 0 ? "No review collaboration report data available." : "";
        var rc = _result.CuratedReports.ReviewCollaboration;

        switch (line)
        {
            case 0: return $"\x1b[1;38;2;180;190;254m👥 Review Collaboration & Silos\x1b[0m";
            case 1: return $"\x1b[38;2;108;112;147mCode reviews, approvals and teamwork links\x1b[0m";
            case 2: return "";
            case 3: return $"\x1b[1mCollaboration Warnings:\x1b[0m";
            case 4: return $"  └─ Reviewer Silos Count:   {rc.ReviewerSilos,3} {(rc.ReviewerSilos > 2 ? "\x1b[1;38;2;243;139;168m[Silo Risk]\x1b[0m" : "[Normal]")}";
            case 5: return "";
            case 6: return $"\x1b[1mTop Review Pairs (Who reviews whom):\x1b[0m";
            default:
                {
                    int index = line - 7;
                    if (index >= 0 && index < rc.Pairs.Count && index < 6)
                    {
                        var pair = rc.Pairs[index];
                        return $"  ├─ {pair.Author} reviewed by {pair.Reviewer} ({pair.PrCount} PRs)";
                    }
                    if (index == Math.Min(rc.Pairs.Count, 6))
                    {
                        return "  └─ (End of top peer reviews list)";
                    }
                    return "";
                }
        }
    }
    #endregion

    #region Perspective 5: AI Code Strain
    private string RenderAiCodeStrain(TuiNode node, int line, int width)
    {
        if (_result.CuratedReports == null) return line == 0 ? "No AI Code Strain report data available." : "";
        var ai = _result.CuratedReports.AiCodeStrain;

        switch (line)
        {
            case 0: return $"\x1b[1;38;2;166;227;161m🤖 Copilot & AI Code Strain Profile\x1b[0m";
            case 1: return $"\x1b[38;2;108;112;147mMeasures indicators of AI assisted high-volume churn\x1b[0m";
            case 2: return "";
            case 3: return $"\x1b[1mAI Strain Indicators:\x1b[0m";
            case 4: return $"  ├─ High-Volume Commits:   {ai.HighVolumeCommits:N0} commits (>20 files)";
            case 5: return $"  └─ Review Velocity Warning: {(ai.ReviewVelocityWarning ? "\x1b[1;38;2;243;139;168m⚠️  WARNING [Strained Review Capacity]\x1b[0m" : "\x1b[38;2;166;227;161mHealthy\x1b[0m")}";
            case 6: return "";
            case 7: return $"\x1b[1mHovered Directory Stats:\x1b[0m";
            case 8: return $"  ├─ Module:                {node.Name}";
            case 9: return $"  ├─ Cumulative Churn:      {node.TotalChurn:N0} lines changed";
            case 10: return $"  └─ Avg Churn/Touch:       {(node.TotalTouches > 0 ? (double)node.TotalChurn / node.TotalTouches : 0):F1}";
            case 11: return "";
            case 12: return $"\x1b[38;2;108;112;147mLarge churn with low review velocity suggests higher risk\x1b[0m";
            case 13: return $"\x1b[38;2;108;112;147mof bugs or architectural regression.\x1b[0m";
            default: return "";
        }
    }
    #endregion

    private async Task HandleInputAsync()
    {
        if (Console.IsInputRedirected)
        {
            _running = false;
            return;
        }

        ConsoleKeyInfo keyInfo;
        try
        {
            keyInfo = Console.ReadKey(true);
        }
        catch
        {
            _running = false;
            return;
        }

        var children = _current.Node.Children;

        switch (keyInfo.Key)
        {
            case ConsoleKey.J:
            case ConsoleKey.DownArrow:
                if (children.Count > 0)
                {
                    _current.SelectedIndex = (_current.SelectedIndex + 1) % children.Count;
                }
                break;

            case ConsoleKey.K:
            case ConsoleKey.UpArrow:
                if (children.Count > 0)
                {
                    _current.SelectedIndex = (_current.SelectedIndex - 1 + children.Count) % children.Count;
                }
                break;

            case ConsoleKey.L:
            case ConsoleKey.Enter:
            case ConsoleKey.RightArrow:
                if (children.Count > 0)
                {
                    var selected = children[_current.SelectedIndex];
                    if (selected.IsDirectory)
                    {
                        // Save state to stack
                        _history.Push(new TuiExplorerState
                        {
                            Node = _current.Node,
                            SelectedIndex = _current.SelectedIndex,
                            ScrollOffset = _current.ScrollOffset
                        });

                        // Enter subdirectory
                        _current = new TuiExplorerState
                        {
                            Node = selected,
                            SelectedIndex = 0,
                            ScrollOffset = 0
                        };
                    }
                }
                break;

            case ConsoleKey.H:
            case ConsoleKey.Escape:
            case ConsoleKey.LeftArrow:
            case ConsoleKey.Backspace:
                if (_history.Count > 0)
                {
                    _current = _history.Pop();
                }
                break;

            case ConsoleKey.Tab:
                _perspective = (_perspective % 5) + 1;
                break;

            case ConsoleKey.D1:
                _perspective = 1;
                break;
            case ConsoleKey.D2:
                _perspective = 2;
                break;
            case ConsoleKey.D3:
                _perspective = 3;
                break;
            case ConsoleKey.D4:
                _perspective = 4;
                break;
            case ConsoleKey.D5:
                _perspective = 5;
                break;

            case ConsoleKey.Q:
                _running = false;
                break;
        }
    }

    private static string PadRightAnsi(string text, int totalWidth, string? bgAnsi = null)
    {
        int visibleLength = 0;
        bool inEscape = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\x1b') inEscape = true;
            else if (inEscape && c == 'm') inEscape = false;
            else if (!inEscape) visibleLength++;
        }
        int paddingCount = totalWidth - visibleLength;
        if (paddingCount > 0)
        {
            if (bgAnsi != null)
            {
                // If text ends with reset, strip it first so padding receives the background color
                if (text.EndsWith("\x1b[0m"))
                {
                    return text.Substring(0, text.Length - 4) + new string(' ', paddingCount) + "\x1b[0m";
                }
                return bgAnsi + text + new string(' ', paddingCount) + "\x1b[0m";
            }
            return text + new string(' ', paddingCount);
        }
        return text;
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffix = { "B", "KB", "MB", "GB", "TB" };
        int i = 0;
        double dblBytes = bytes;
        while (dblBytes >= 1024 && i < suffix.Length - 1)
        {
            dblBytes /= 1024;
            i++;
        }
        return $"{dblBytes:F2} {suffix[i]}";
    }

    public static class CatppuccinMocha
    {
        public const string Mauve = "\x1b[38;2;203;166;247m";
        public const string Blue = "\x1b[38;2;137;180;250m";
        public const string Green = "\x1b[38;2;166;227;161m";
        public const string Yellow = "\x1b[38;2;249;226;175m";
        public const string Peach = "\x1b[38;2;250;179;135m";
        public const string Red = "\x1b[38;2;243;139;168m";
        public const string Lavender = "\x1b[38;2;180;190;254m";
        public const string Sapphire = "\x1b[38;2;116;199;236m";
        public const string Pink = "\x1b[38;2;245;194;231m";
        public const string Teal = "\x1b[38;2;148;226;213m";
        public const string Text = "\x1b[38;2;205;214;244m";
        public const string Subtext = "\x1b[38;2;166;173;200m";
        public const string Gray = "\x1b[38;2;108;112;147m";
        public const string SelectedBg = "\x1b[48;2;73;77;100m"; // Surface1 bg
        public const string SelectedFgBg = "\x1b[48;2;73;77;100m\x1b[1;38;2;137;180;250m"; // Surface1 bg + Blue text
        public const string Reset = "\x1b[0m";
    }
}
