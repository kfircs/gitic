using System;

namespace Gitic;

public static class ConsoleUtils
{
    public static int GetBoundedConsoleWidth(int? overrideWidth = null)
    {
        int consoleWidth = overrideWidth ?? 80;
        if (overrideWidth == null)
        {
            try
            {
                if (!Console.IsOutputRedirected)
                {
                    consoleWidth = Console.WindowWidth;
                }
            }
            catch { }

            consoleWidth = Math.Clamp(consoleWidth, 40, 200);
        }
        return consoleWidth;
    }

    public static string PadRightAnsi(string text, int totalWidth, string? bgAnsi = null)
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
}
