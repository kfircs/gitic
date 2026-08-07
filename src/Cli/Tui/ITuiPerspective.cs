using System.Collections.Generic;

namespace Gitic;

public interface ITuiPerspective
{
    int PerspectiveId { get; }
    string DisplayName { get; }
    List<string> GetRightSidebarLines(TuiNode selectedNode, int width, AnalysisResult result);
}
