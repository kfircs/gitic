using System;

namespace Gitic;

public static class ConsoleUtils
{
    private const int DefaultConsoleWidth = 80;
    private const int MinConsoleWidth = 40;
    private const int MaxConsoleWidth = 200;

    public static int GetBoundedConsoleWidth(int? overrideWidth = null)
    {
        int consoleWidth = overrideWidth ?? DefaultConsoleWidth;
        if (overrideWidth == null)
        {
            try
            {
                if (!Console.IsOutputRedirected)
                {
                    consoleWidth = Console.WindowWidth;
                }
            }
            catch
            {
                // Swallowing exceptions is safe here: if Console API queries fail (e.g., in non-interactive
                // environments where Console.WindowWidth throws), we fallback to the default consoleWidth value of 80.
            }

            consoleWidth = Math.Clamp(consoleWidth, MinConsoleWidth, MaxConsoleWidth);
        }
        return consoleWidth;
    }

    public static string PadRightAnsi(string text, int totalWidth, string? bgAnsi = null)
    {
        int visibleLength = GetAnsiVisibleLength(text);
        int paddingCount = totalWidth - visibleLength;
        if (paddingCount > 0)
        {
            if (bgAnsi != null)
            {
                // If text ends with reset, strip it first so padding receives the background color
                if (text.EndsWith("\x1b[0m", StringComparison.Ordinal))
                {
                    return text[..^4] + new string(' ', paddingCount) + "\x1b[0m";
                }
                return bgAnsi + text + new string(' ', paddingCount) + "\x1b[0m";
            }
            return text + new string(' ', paddingCount);
        }
        return text;
    }

#pragma warning disable CA1416
    public static bool TryGetCursorVisible(bool defaultValue = true)
    {
        try
        {
            return Console.CursorVisible;
        }
        catch
        {
            return defaultValue;
        }
    }

    public static void TrySetCursorVisible(bool visible)
    {
        try
        {
            Console.CursorVisible = visible;
        }
        catch { }
    }
#pragma warning restore CA1416

    public static int GetAnsiVisibleLength(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        int visibleLength = 0;
        bool inEscape = false;
        foreach (char c in text)
        {
            if (c == '\x1b')
            {
                inEscape = true;
            }
            else if (inEscape && c == 'm')
            {
                inEscape = false;
            }
            else if (!inEscape)
            {
                visibleLength++;
            }
        }
        return visibleLength;
    }
}
