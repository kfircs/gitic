using System;
using System.Collections.Generic;
using static Gitic.TuiExplorer;

namespace Gitic;

public class AiCodeStrainPerspective : ITuiPerspective
{
    public int PerspectiveId => 5;
    public string DisplayName => "AI Code Strain [TBD]";

    public List<string> GetRightSidebarLines(TuiNode node, int width, AnalysisResult result)
    {
        var lines = new List<string>();
        lines.Add($"\x1b[1;38;2;166;227;161m🤖 Copilot & AI Code Strain Profile\x1b[0m");
        lines.Add($"\x1b[38;2;108;112;147mMeasures indicators of AI assisted high-volume churn\x1b[0m");
        lines.Add("");
        lines.Add($"\x1b[1;38;2;249;226;175m🚧 [TBD] Perspective Under Construction\x1b[0m");
        lines.Add("");
        lines.Add("This perspective is currently being researched.");
        lines.Add("Future releases will measure AI code generation");
        lines.Add("patterns and reviewer saturation:");
        lines.Add("");
        lines.Add("  ├─ Churn Intensity and rewrite signatures");
        lines.Add("  ├─ Extreme commit frequency detection");
        lines.Add("  └─ AI-boilerplate complexity expansion");
        lines.Add("");
        lines.Add($"\x1b[38;2;108;112;147mSelected node: {node.Name}\x1b[0m");
        return lines;
    }
}
