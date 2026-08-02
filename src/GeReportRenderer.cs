using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gitic;

public class GeReportRenderer : IReportRenderer
{
    public Task<string> RenderAsync(AnalysisResult result, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StringBuilder sb = new();

        string repoName = Path.GetFileName(result.Analysis.RepoRoot.TrimEnd(Path.DirectorySeparatorChar));
        if (string.IsNullOrEmpty(repoName)) repoName = "Repository";

        sb.AppendLine($"# 📊 Git Forensics Dashboard: {repoName}");
        string genDateStr;
        if (DateTimeOffset.TryParse(result.Analysis.GeneratedAt, out var parsedGenAt))
        {
            genDateStr = parsedGenAt.ToString("yyyy-MM-dd HH:mm:ss") + " UTC";
        }
        else
        {
            genDateStr = result.Analysis.GeneratedAt;
        }
        sb.AppendLine($"Generated on: {genDateStr}\n");

        // 1. Action Feed (Top 5 things to fix today)
        sb.AppendLine("## 1. Action Feed (Top 5 things to fix today)");
        sb.AppendLine();
        
        List<string> actionItems = [];

        // Find top rework files
        var highReworkFiles = result.Files
            .Where(f => f.ReworkRate.HasValue && f.ReworkRate.Value > 0.2)
            .OrderByDescending(f => f.ReworkRate)
            .Take(3)
            .ToList();
        
        foreach (var f in highReworkFiles)
        {
            actionItems.Add($"* ⚠️ **Refactor Warning:** `{f.Path}` has a high rework rate ({(f.ReworkRate.GetValueOrDefault()*100):F1}%) and is edited by {f.ContributorCount} different authors.");
        }

        // Find bus factor files/areas
        var topSiloAreas = result.Areas
            .Where(a => a.FileCount > 5)
            .Select(a => new { Area = a, TopOwnerShare = a.Contributors?.OrderByDescending(c => c.ActivityShare).FirstOrDefault()?.ActivityShare ?? 0.0 })
            .Where(x => x.TopOwnerShare > 0.8)
            .OrderByDescending(x => x.TopOwnerShare)
            .Take(2)
            .ToList();

        foreach (var a in topSiloAreas)
        {
            actionItems.Add($"* 👤 **Key Person Risk:** Folder `{a.Area.Area}` is {(a.TopOwnerShare*100):F0}% owned by one developer.");
        }

        if (actionItems.Count == 0)
        {
            actionItems.Add("* ✅ **All Clear:** No immediate critical warnings for rework or key person risks found today.");
        }

        foreach (var item in actionItems.Take(5))
        {
            sb.AppendLine(item);
        }
        sb.AppendLine();

        // 2. Interactive Maps (Hotspots & Ownership)
        sb.AppendLine("## 2. Interactive Maps (Hotspots & Ownership)");
        sb.AppendLine();

        sb.AppendLine("### The 2D Hotspot Scatterplot (For Tech Debt Prioritization)");
        sb.AppendLine("Focus only on the top-right quadrant. Large files that are edited often cause daily friction.");
        sb.AppendLine();
        string hotspotSvg = SvgGeneratorHelper.GenerateSvg(result);
        sb.AppendLine(hotspotSvg);
        sb.AppendLine();

        sb.AppendLine("### The Codebase Treemap (For Modular & Team Insights)");
        sb.AppendLine("Code directories. Red/Orange denotes single-author high-risk zones, Green denotes healthy collaboration.");
        sb.AppendLine();
        string treemapSvg = SvgGeneratorHelper.GenerateGeTreemapSvg(result);
        sb.AppendLine(treemapSvg);
        sb.AppendLine();

        // 3. Deep Dives (Diffs, coupling, and trends)
        sb.AppendLine("## 3. Deep Dives (Diffs, coupling, and trends)");
        sb.AppendLine();

        sb.AppendLine("### Interactive Temporal / Change Coupling Graphs");
        sb.AppendLine("Files that repeatedly change together in the same commits. Thick lines warn of hidden architectural decay (shotgun surgery).");
        sb.AppendLine();
        string temporalSvg = SvgGeneratorHelper.GenerateGeTemporalCouplingSvg(result);
        sb.AppendLine(temporalSvg);
        sb.AppendLine();
        
        sb.AppendLine("### Detailed Hotspot Metrics");
        sb.AppendLine("| File Path | Lines | Size (KB) | Churn | Rework Rate | Attention Score |");
        sb.AppendLine("| :--- | :---: | :---: | :---: | :---: | :---: |");

        var topHotspots = result.Files
            .OrderByDescending(f => f.AttentionScore)
            .Take(10)
            .ToList();

        foreach (var file in topHotspots)
        {
            double reworkPct = (file.ReworkRate ?? 0) * 100;
            string reworkStr = file.ReworkRate.HasValue ? $"{reworkPct:F1}%" : "N/A";
            string sizeStr = file.Size.HasValue ? $"{(file.Size.Value / 1024.0):F1}" : "N/A";
            string linesStr = file.Lines.HasValue ? $"{file.Lines.Value}" : "N/A";

            sb.AppendLine($"| `{file.Path}` | {linesStr} | {sizeStr} | {file.Churn} | {reworkStr} | {file.AttentionScore:F1} |");
        }
        sb.AppendLine();

        var assembly = typeof(Cli).Assembly;
        var version = assembly.GetName().Version?.ToString(3) ?? "0.1.0";
        var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var displayVersion = string.IsNullOrEmpty(infoVersion) ? version : infoVersion;
        sb.AppendLine($"---\n*Dashboard generated by **Gitic GeReport** (v{displayVersion})*");

        return Task.FromResult(sb.ToString());
    }
}
