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

            if (consoleWidth < 40) consoleWidth = 40;
            if (consoleWidth > 200) consoleWidth = 200;
        }
        return consoleWidth;
    }
}
