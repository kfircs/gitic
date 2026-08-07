using System;
using System.Collections.Generic;
using System.Linq;
using static Gitic.TuiExplorer;

namespace Gitic;

public class CodeRotPerspective : ITuiPerspective
{
    public int PerspectiveId => 3;
    public string DisplayName => "Code Rot / Zombies";

    public List<string> GetRightSidebarLines(TuiNode node, int width, AnalysisResult result)
    {
        var lines = new List<string>();
        if (result.CuratedReports == null)
        {
            lines.Add("No Code Rot report data available.");
            return lines;
        }

        var rot = result.CuratedReports.CodeRot;
        int thresholdDays = rot.ThresholdDays > 0 ? rot.ThresholdDays : 365;
        string thresholdLabel = thresholdDays >= 365 ? "1 year" : $"{thresholdDays} days";

        lines.Add($"\x1b[1;38;2;249;226;175m🧟 Code Rot & Zombie Files\x1b[0m");
        lines.Add($"\x1b[38;2;108;112;147mFiles untouched for more than {thresholdLabel}\x1b[0m");
        lines.Add("");
        lines.Add($"\x1b[1mSummary Stats (Full Repo):\x1b[0m");
        lines.Add($"  ├─ Zombie Files Count:    {rot.ZombieFileCount:N0}");
        lines.Add($"  └─ Zombie Lines of Code:  {rot.ZombieLines:N0}");
        lines.Add("");
        lines.Add($"\x1b[1mScope-Specific Analysis:\x1b[0m");
        lines.Add($"  Hovered: \x1b[1;38;2;137;180;250m{node.Name}\x1b[0m");

        int zombieCountInHovered = CountZombiesUnderNode(node, thresholdDays);
        lines.Add($"  ├─ Zombie Files in this folder: {zombieCountInHovered:N0}");

        long zombieLinesInHovered = CountZombieLinesUnderNode(node, thresholdDays);
        lines.Add($"  └─ Zombie Lines in this folder: {zombieLinesInHovered:N0}");

        lines.Add("");
        lines.Add($"\x1b[38;2;108;112;147mZombie code increases maintenance cognitive overhead.\x1b[0m");
        lines.Add($"\x1b[38;2;108;112;147mConsider refactoring or pruning unused files.\x1b[0m");

        return lines;
    }

    private int CountZombiesUnderNode(TuiNode node, int thresholdDays)
    {
        if (!node.IsDirectory)
        {
            return IsZombieFile(node.FileMetric, thresholdDays) ? 1 : 0;
        }
        return node.Children.Sum(c => CountZombiesUnderNode(c, thresholdDays));
    }

    private long CountZombieLinesUnderNode(TuiNode node, int thresholdDays)
    {
        if (!node.IsDirectory)
        {
            return IsZombieFile(node.FileMetric, thresholdDays) ? (node.FileMetric?.Lines ?? 0) : 0;
        }
        return node.Children.Sum(c => CountZombieLinesUnderNode(c, thresholdDays));
    }

    private bool IsZombieFile(FileMetric? f, int thresholdDays)
    {
        if (f == null || string.IsNullOrEmpty(f.LastTouched)) return false;
        if (DateTime.TryParse(f.LastTouched, out var date))
        {
            return (DateTime.Now - date).TotalDays > thresholdDays;
        }
        return false;
    }
}
