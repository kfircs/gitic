using Kfc.Cli.Tui;
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
    private readonly Dictionary<int, ITuiPerspective> _perspectives = new()
    {
        { 1, new LinesStructurePerspective() },
        { 2, new WorkClassificationPerspective() },
        { 3, new CodeRotPerspective() },
        { 4, new ReviewCollaborationPerspective() },
        { 5, new AiCodeStrainPerspective() }
    };
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
        int rawWidth = 80;
        int rawHeight = 24;
        try
        {
            if (!Console.IsOutputRedirected && Console.WindowWidth > 0) rawWidth = Console.WindowWidth;
            if (!Console.IsOutputRedirected && Console.WindowHeight > 0) rawHeight = Console.WindowHeight;
        }
        catch { }

        var viewport = TuiViewport.Calculate(rawWidth, rawHeight);
        int leftWidth = viewport.LeftWidth;
        int rightWidth = viewport.RightWidth;
        int contentHeight = viewport.ContentHeight;

        // Header Border and Line Layouts (exactly safeWidth long!)
        string title = " 🖥️  Gitic Interactive TUI Explorer ";
        string perspectiveTitle = $"Perspective: [{_perspective}] {GetPerspectiveName()}";
        string breadcrumbs = GetBreadcrumbs(viewport.SafeWidth - 4);

        string topBorder = "┌" + new string('─', leftWidth + 2) + "┬" + new string('─', rightWidth + 2) + "┐";
        string midBorder1 = "├" + new string('─', leftWidth + 2) + "┼" + new string('─', rightWidth + 2) + "┤";
        string midBorder2 = "├" + new string('─', leftWidth + 2) + "┼" + new string('─', rightWidth + 2) + "┤";

        Console.WriteLine($"\x1b[38;2;203;166;247m{topBorder}\x1b[0m");
        Console.WriteLine($"\x1b[38;2;203;166;247m│\x1b[0m \x1b[1;38;2;137;180;250m{PadRightAnsi(title, leftWidth)}\x1b[0m \x1b[38;2;203;166;247m│\x1b[0m \x1b[38;2;166;227;161m{PadRightAnsi(perspectiveTitle, rightWidth)}\x1b[0m \x1b[38;2;203;166;247m│\x1b[0m");
        Console.WriteLine($"\x1b[38;2;108;112;147m{midBorder1}\x1b[0m");
        Console.WriteLine($"\x1b[38;2;203;166;247m│\x1b[0m \x1b[38;2;180;190;254m{PadRightAnsi("Breadcrumbs: " + breadcrumbs, leftWidth + rightWidth + 3)}\x1b[0m \x1b[38;2;203;166;247m│\x1b[0m");
        Console.WriteLine($"\x1b[38;2;108;112;147m{midBorder2}\x1b[0m");

        // List & Detail Panel Setup
        var children = _current.Node.Children;

        // Manage scrolling and boundary logic using TuiScrollManager
        var (clampedIndex, newScrollOffset) = TuiScrollManager.AdjustScroll(_current.SelectedIndex, _current.ScrollOffset, children.Count, contentHeight);
        _current.SelectedIndex = clampedIndex;
        _current.ScrollOffset = newScrollOffset;

        TuiNode? selectedNode = children.Count > 0 ? children[_current.SelectedIndex] : null;
        var activePerspective = _perspectives.TryGetValue(_perspective, out var p) ? p : _perspectives[1];
        var rightLines = selectedNode == null ? new List<string>() : activePerspective.GetRightSidebarLines(selectedNode, rightWidth, _result);

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
            string rightCell = (selectedNode == null)
                ? (i == 0 ? "\x1b[38;2;108;112;147m(Select an item to view stats)\x1b[0m" : "")
                : (i < rightLines.Count ? rightLines[i] : "");

            Console.WriteLine(TuiPanel.DrawRow(leftCell, leftWidth, rightCell, rightWidth));
        }

        // Footer
        Console.WriteLine(TuiPanel.DrawBorderBottom(leftWidth, rightWidth, true));

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

    private string GetPerspectiveName() => _perspectives.TryGetValue(_perspective, out var p) ? p.DisplayName : "Unknown";

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
