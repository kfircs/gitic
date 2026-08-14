using System;
using System.Collections.Generic;
using System.Linq;

namespace Gitic
{
    public static class TuiPrompter
    {
        public static int PromptSingleSelection(string prompt, string[] options)
        {
            if (Console.IsInputRedirected || Console.IsOutputRedirected || Console.IsErrorRedirected)
            {
                Console.WriteLine(prompt);
                for (int i = 0; i < options.Length; i++) Console.WriteLine($"[{i}] {options[i]}");
                Console.Write("Enter selection (number): ");
                string? line = Console.ReadLine();
                if (line == null || line == "back" || line == "escape") return -1;
                if (int.TryParse(line, out int val) && val >= 0 && val < options.Length) return val;
                return 0;
            }

            int currentSelection = 0;
            ConsoleKey key;
            int startTop = Console.CursorTop;
            bool firstDraw = true;

            try { Console.CursorVisible = false; } catch { }

            do
            {
                if (!firstDraw)
                {
                    try { Console.SetCursorPosition(0, startTop); } catch { }
                }
                firstDraw = false;

                int winW = 80;
                try { if (!Console.IsOutputRedirected && Console.WindowWidth > 0) winW = Console.WindowWidth; } catch { }
                int bWidth = Math.Max(50, Math.Min(80, winW));
                Console.WriteLine($"\x1b[38;2;249;226;175m┌{new string('─', bWidth - 2)}┐\x1b[0m");
                Console.WriteLine($"\x1b[38;2;249;226;175m│\x1b[0m {ConsoleUtils.PadRightAnsi($"\x1b[1m󰜎 {prompt}\x1b[0m", bWidth - 4)} \x1b[38;2;249;226;175m│\x1b[0m");
                Console.WriteLine($"\x1b[38;2;249;226;175m├{new string('─', bWidth - 2)}┤\x1b[0m");
                for (int i = 0; i < options.Length; i++)
                {
                    if (i == currentSelection)
                    {
                        Console.WriteLine($"\x1b[38;2;249;226;175m│\x1b[0m {ConsoleUtils.PadRightAnsi($"\x1b[38;2;137;180;250m󰅂\x1b[0m \x1b[1;38;2;137;180;250m{options[i]}\x1b[0m", bWidth - 4)} \x1b[38;2;249;226;175m│\x1b[0m");
                    }
                    else
                    {
                        Console.WriteLine($"\x1b[38;2;249;226;175m│\x1b[0m {ConsoleUtils.PadRightAnsi($"  {options[i]}", bWidth - 4)} \x1b[38;2;249;226;175m│\x1b[0m");
                    }
                }
                Console.WriteLine($"\x1b[38;2;249;226;175m├{new string('─', bWidth - 2)}┤\x1b[0m");
                Console.WriteLine($"\x1b[38;2;249;226;175m│\x1b[0m {ConsoleUtils.PadRightAnsi("\x1b[38;2;108;112;147m󰌑 Up/Down: Navigate │ Enter: Select\x1b[0m", bWidth - 4)} \x1b[38;2;249;226;175m│\x1b[0m");
                Console.WriteLine($"\x1b[38;2;249;226;175m└{new string('─', bWidth - 2)}┘\x1b[0m");

                key = Console.ReadKey(true).Key;

                switch (key)
                {
                    case ConsoleKey.UpArrow:
                        currentSelection = (currentSelection == 0) ? options.Length - 1 : currentSelection - 1;
                        break;
                    case ConsoleKey.DownArrow:
                        currentSelection = (currentSelection == options.Length - 1) ? 0 : currentSelection + 1;
                        break;
                    case ConsoleKey.Escape:
                        try { Console.CursorVisible = true; } catch { }
                        return -1;
                }
            } while (key != ConsoleKey.Enter);

            try { Console.CursorVisible = true; } catch { }
            return currentSelection;
        }

        public static List<int> PromptMultiSelection(string prompt, string[] options)
        {
            if (Console.IsInputRedirected || Console.IsOutputRedirected || Console.IsErrorRedirected)
            {
                Console.WriteLine(prompt);
                for (int i = 0; i < options.Length; i++) Console.WriteLine($"[{i}] {options[i]}");
                Console.Write("Enter selections (comma separated numbers): ");
                var line = Console.ReadLine();
                if (line == null || line == "back" || line == "escape") return new List<int> { -1 };
                var parts = line.Split(',', StringSplitOptions.RemoveEmptyEntries);
                var sel = parts.Select(p => int.TryParse(p, out int v) ? v : -1).Where(v => v >= 0 && v < options.Length).ToList();
                return sel.Count > 0 ? sel : new List<int> { 0 };
            }

            int currentSelection = 0;
            HashSet<int> selected = new HashSet<int>();
            ConsoleKey key;
            int startTop = Console.CursorTop;
            bool firstDraw = true;

            try { Console.CursorVisible = false; } catch { }

            do
            {
                if (!firstDraw)
                {
                    try { Console.SetCursorPosition(0, startTop); } catch { }
                }
                firstDraw = false;

                int winW = 80;
                try { if (!Console.IsOutputRedirected && Console.WindowWidth > 0) winW = Console.WindowWidth; } catch { }
                int bWidth = Math.Max(50, Math.Min(80, winW));
                Console.WriteLine($"\x1b[38;2;249;226;175m┌{new string('─', bWidth - 2)}┐\x1b[0m");
                Console.WriteLine($"\x1b[38;2;249;226;175m│\x1b[0m {ConsoleUtils.PadRightAnsi($"\x1b[1m󰜎 {prompt}\x1b[0m", bWidth - 4)} \x1b[38;2;249;226;175m│\x1b[0m");
                Console.WriteLine($"\x1b[38;2;249;226;175m├{new string('─', bWidth - 2)}┤\x1b[0m");
                for (int i = 0; i < options.Length; i++)
                {
                    string checkbox = selected.Contains(i) ? "󰄲" : "󰄱";
                    if (i == currentSelection)
                    {
                        Console.WriteLine($"\x1b[38;2;249;226;175m│\x1b[0m {ConsoleUtils.PadRightAnsi($"\x1b[38;2;137;180;250m󰅂\x1b[0m {checkbox} \x1b[1;38;2;137;180;250m{options[i]}\x1b[0m", bWidth - 4)} \x1b[38;2;249;226;175m│\x1b[0m");
                    }
                    else
                    {
                        Console.WriteLine($"\x1b[38;2;249;226;175m│\x1b[0m {ConsoleUtils.PadRightAnsi($"  {checkbox} {options[i]}", bWidth - 4)} \x1b[38;2;249;226;175m│\x1b[0m");
                    }
                }
                Console.WriteLine($"\x1b[38;2;249;226;175m├{new string('─', bWidth - 2)}┤\x1b[0m");
                Console.WriteLine($"\x1b[38;2;249;226;175m│\x1b[0m {ConsoleUtils.PadRightAnsi("\x1b[38;2;108;112;147m󰌑 Up/Down: Navigate │ Space: Toggle │ Enter: Select\x1b[0m", bWidth - 4)} \x1b[38;2;249;226;175m│\x1b[0m");
                Console.WriteLine($"\x1b[38;2;249;226;175m└{new string('─', bWidth - 2)}┘\x1b[0m");

                key = Console.ReadKey(true).Key;

                switch (key)
                {
                    case ConsoleKey.UpArrow:
                        currentSelection = (currentSelection == 0) ? options.Length - 1 : currentSelection - 1;
                        break;
                    case ConsoleKey.DownArrow:
                        currentSelection = (currentSelection == options.Length - 1) ? 0 : currentSelection + 1;
                        break;
                    case ConsoleKey.Spacebar:
                        if (selected.Contains(currentSelection))
                            selected.Remove(currentSelection);
                        else
                            selected.Add(currentSelection);
                        break;
                    case ConsoleKey.Escape:
                        try { Console.CursorVisible = true; } catch { }
                        return new List<int> { -1 };
                }
            } while (key != ConsoleKey.Enter);

            try { Console.CursorVisible = true; } catch { }
            return selected.OrderBy(x => x).ToList();
        }
    }
}
