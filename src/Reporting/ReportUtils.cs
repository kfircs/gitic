using System;
using System.IO;

namespace Gitic;

public static class ReportUtils
{
    public static string GetRepositoryName(string repoRoot)
    {
        string name = Path.GetFileName(repoRoot.TrimEnd(Path.DirectorySeparatorChar));
        return string.IsNullOrEmpty(name) ? "Repository" : name;
    }

    public static string FormatGeneratedAt(string generatedAt)
    {
        if (DateTimeOffset.TryParse(generatedAt, out var parsedGenAt))
        {
            return parsedGenAt.ToString("yyyy-MM-dd HH:mm:ss") + " UTC";
        }
        return generatedAt;
    }
}
