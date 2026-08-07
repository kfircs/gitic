using System;
using System.Collections.Generic;
using static Gitic.TuiExplorer;

namespace Gitic;

public class ReviewCollaborationPerspective : ITuiPerspective
{
    public int PerspectiveId => 4;
    public string DisplayName => "Review Collaboration [TBD]";

    public List<string> GetRightSidebarLines(TuiNode node, int width, AnalysisResult result)
    {
        var lines = new List<string>();
        lines.Add($"\x1b[1;38;2;180;190;254m👥 Review Collaboration & Silos\x1b[0m");
        lines.Add($"\x1b[38;2;108;112;147mCode reviews, approvals and teamwork links\x1b[0m");
        lines.Add("");
        lines.Add($"\x1b[1;38;2;249;226;175m🚧 [TBD] Perspective Under Construction\x1b[0m");
        lines.Add("");
        lines.Add("This perspective is currently being researched.");
        lines.Add("Future releases will analyze standard git trailers");
        lines.Add("and offline metadata to surface:");
        lines.Add("");
        lines.Add("  ├─ Review silos and single-reviewer dependencies");
        lines.Add("  ├─ Collaboration pairs and team topology map");
        lines.Add("  └─ Human-to-AI collaboration patterns");
        lines.Add("");
        lines.Add($"\x1b[38;2;108;112;147mSelected node: {node.Name}\x1b[0m");
        return lines;
    }
}
