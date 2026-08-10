using System;

namespace Gitic;

public record struct TuiViewport(int Width, int Height, int LeftWidth, int RightWidth)
{
    public readonly int SafeWidth => Width - 2;
    public readonly int ContentHeight => Height - 8;

    public static TuiViewport Calculate(int terminalWidth, int terminalHeight)
    {
        int w = Math.Max(70, terminalWidth);
        int h = Math.Max(15, terminalHeight - 1);
        int safeW = w - 2;
        int leftW = Math.Max(20, (int)(safeW * 0.33));
        int rightW = Math.Max(30, safeW - leftW - 7);
        return new TuiViewport(w, h, leftW, rightW);
    }
}

public static class TuiScrollManager
{
    public static (int SelectedIndex, int ScrollOffset) AdjustScroll(int selectedIndex, int scrollOffset, int itemCount, int contentHeight)
    {
        if (itemCount == 0)
        {
            return (0, 0);
        }
        int index = Math.Clamp(selectedIndex, 0, itemCount - 1);
        int offset = scrollOffset;
        if (index < offset)
        {
            offset = index;
        }
        else if (index >= offset + contentHeight)
        {
            offset = index - contentHeight + 1;
        }
        return (index, offset);
    }
}
