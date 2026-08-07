using System;
using System.Collections.Generic;
using System.Text;
using static Gitic.TuiExplorer;

namespace Gitic;

public class WorkClassificationPerspective : ITuiPerspective
{
    public int PerspectiveId => 2;
    public string DisplayName => "Work Classification";

    public List<string> GetRightSidebarLines(TuiNode node, int width, AnalysisResult result)
    {
        var lines = new List<string>();
        if (result.CuratedReports == null)
        {
            lines.Add("No classification report data available.");
            return lines;
        }

        var wc = node.WorkClassification ?? new WorkClassificationMetrics();
        int total = wc.Features + wc.Bugs + wc.TechnicalDebt + wc.Chores + wc.Unclassified;
        if (total == 0) total = 1;

        double featPct = (double)wc.Features / total;
        double bugPct = (double)wc.Bugs / total;
        double debtPct = (double)wc.TechnicalDebt / total;
        double chorePct = (double)wc.Chores / total;

        lines.Add($"\x1b[1;38;2;137;180;250m📊 Work Classification perspective\x1b[0m");
        lines.Add($"\x1b[38;2;108;112;147mScope: {node.Name}\x1b[0m");
        lines.Add("");
        lines.Add($"\x1b[1mCommit Statistics:\x1b[0m");
        lines.Add($"  ├─ Features:       {wc.Features,4}  ({featPct:P1})");
        lines.Add($"  ├─ Bugfixes:       {wc.Bugs,4}  ({bugPct:P1})");
        lines.Add($"  ├─ Tech Debt:      {wc.TechnicalDebt,4}  ({debtPct:P1})");
        lines.Add($"  ├─ Chores:         {wc.Chores,4}  ({chorePct:P1})");
        lines.Add($"  └─ Unclassified:   {wc.Unclassified,4}");
        lines.Add("");
        lines.Add($"\x1b[1mVisual Distribution Bar:\x1b[0m");

        // Bar construction
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
        lines.Add(sb.ToString());

        lines.Add("   \x1b[38;2;166;227;161m■ Feat\x1b[0m  \x1b[38;2;243;139;168m■ Bug\x1b[0m  \x1b[38;2;249;226;175m■ Debt\x1b[0m  \x1b[38;2;137;180;250m■ Chore\x1b[0m  \x1b[38;2;147;153;178m░ Unclassified\x1b[0m");

        return lines;
    }
}
