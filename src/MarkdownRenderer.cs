using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Gitic
{
    public class MarkdownRenderer : IReportRenderer
    {
        public MarkdownRenderer()
        {
        }

        public Task<string> RenderAsync(AnalysisResult result)
        {
            var sb = new StringBuilder();
            
            string repoName = Path.GetFileName(result.Analysis.RepoRoot.TrimEnd(Path.DirectorySeparatorChar));
            if (string.IsNullOrEmpty(repoName)) repoName = "Repository";

            sb.AppendLine($"# 📊 Gitic Analysis Report: {repoName}");
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

            sb.AppendLine("## 📈 Repository Overview");
            sb.AppendLine($"- **Repository Root:** `{result.Analysis.RepoRoot}`");
            sb.AppendLine($"- **Analysis Command:** `{result.Analysis.Command}`");
            sb.AppendLine($"- **Total Files Analyzed:** {result.Files.Count}");
            sb.AppendLine($"- **Total Contributors:** {result.Contributors.Count}");
            sb.AppendLine($"- **Time Window Filter:** {(result.Settings.AllTime ? "All History" : result.Settings.Since ?? "Default Window")}");
            sb.AppendLine();

            sb.AppendLine("## 🔥 Top Code Hotspots & Attention Metrics");
            sb.AppendLine("These files have the highest **Attention Score**, which combines change recency, churn volume, code complexity (file length/size), and contributor dispersion to find code needing active review.");
            sb.AppendLine();

            sb.AppendLine("### 📊 Visual Hotspot Quadrant Map");
            sb.AppendLine("Hover over the circles to view file metrics. Larger circles indicate larger file sizes. Red/Orange points denote high attention/rework.");
            sb.AppendLine();
            string embeddedSvg = SvgGeneratorHelper.GenerateSvg(result);
            sb.AppendLine(embeddedSvg);
            sb.AppendLine();

            sb.AppendLine("### 📊 Complexity Distribution (Min / Max / Avg)");
            sb.AppendLine("Shows the span of file lengths (in lines) and max line widths (in characters) across the codebase.");
            sb.AppendLine();
            string complexitySvg = SvgGeneratorHelper.GenerateComplexityRangesSvg(result);
            sb.AppendLine(complexitySvg);
            sb.AppendLine();

            sb.AppendLine("| File Path | Lines | Size (KB) | Churn | Rework Rate | Attention Score | Major Risk Signals |");
            sb.AppendLine("| :--- | :---: | :---: | :---: | :---: | :---: | :--- |");

            var topHotspots = result.Files
                .OrderByDescending(f => f.AttentionScore)
                .Take(15)
                .ToList();

            var highlights = new List<string>();

            foreach (var file in topHotspots)
            {
                double reworkPct = (file.ReworkRate ?? 0) * 100;
                string reworkStr = file.ReworkRate.HasValue ? $"{reworkPct:F1}%" : "N/A";
                string sizeStr = file.Size.HasValue ? $"{(file.Size.Value / 1024.0):F1}" : "N/A";
                string linesStr = file.Lines.HasValue ? $"{file.Lines.Value}" : "N/A";

                var risks = new List<string>();
                if (reworkPct > 20)
                {
                    risks.Add($"High Rework ({reworkStr})");
                    highlights.Add($"⚠️ **Rework Alert:** `{file.Path}` has a rework rate of **{reworkStr}**. A significant portion of its churn is revisions of recent commits, which often signals unstable requirements or architectural fragility.");
                }
                if (file.Lines > 1000)
                {
                    risks.Add($"Large File ({linesStr} lines)");
                    highlights.Add($"📏 **File Length Alert:** `{file.Path}` has **{linesStr}** lines of code. This exceeds standard file complexity recommendations and is a strong candidate for refactoring/decomposition.");
                }
                if (file.KnowledgeSilo != null && file.KnowledgeSilo.IsSilo)
                {
                    string ownerStr = $"{file.KnowledgeSilo.TopOwnerShare * 100:F0}%";
                    risks.Add($"Knowledge Silo ({ownerStr})");
                    highlights.Add($"👤 **Knowledge Silo Alert:** `{file.Path}` is authored **{ownerStr}** by a single developer. This presents key-person risk if they are unavailable.");
                }

                string riskSignals = risks.Count > 0 ? string.Join(", ", risks) : "Stable / Low Risk";

                sb.AppendLine($"| `{file.Path}` | {linesStr} | {sizeStr} | {file.Churn} | {reworkStr} | {file.AttentionScore:F1} | {riskSignals} |");
            }
            sb.AppendLine();

            if (highlights.Count > 0)
            {
                sb.AppendLine("### ⚠️ Key Signal Highlights");
                foreach (var h in highlights.Distinct().Take(8))
                {
                    sb.AppendLine($"- {h}");
                }
                sb.AppendLine();
            }

            sb.AppendLine("## 👥 Top Contributors & Ownership");
            sb.AppendLine("Contributors ordered by total repository touch activity.");
            sb.AppendLine();
            sb.AppendLine("| Contributor | Email | Activity Touches | Top Impact Areas |");
            sb.AppendLine("| :--- | :--- | :---: | :--- |");

            var topContributors = result.Contributors
                .OrderByDescending(c => c.TotalActivity)
                .Take(10)
                .ToList();

            foreach (var contributor in topContributors)
            {
                var topAreas = result.Areas
                    .Where(a => a.Contributors.Any(c => c.Email == contributor.Email))
                    .Select(a => new { Area = a.Area, Share = a.Contributors.First(c => c.Email == contributor.Email).ActivityShare })
                    .OrderByDescending(a => a.Share)
                    .Take(2)
                    .Select(a => $"`{a.Area}` ({a.Share * 100:F0}%)")
                    .ToList();

                string areasStr = topAreas.Count > 0 ? string.Join(", ", topAreas) : "Generalist";

                sb.AppendLine($"| **{contributor.Name}** | `{contributor.Email}` | {contributor.TotalActivity:F0} | {areasStr} |");
            }
            sb.AppendLine();

            sb.AppendLine("## 📁 Module / Area Ownership");
            sb.AppendLine("Code directories analyzed by touch counts and ownership spread.");
            sb.AppendLine();
            sb.AppendLine("| Directory | File Count | Touches | Churn | Top Contributor / Ownership |");
            sb.AppendLine("| :--- | :---: | :---: | :---: | :--- |");

            var topAreasList = result.Areas
                .OrderByDescending(a => a.Touches)
                .Take(10)
                .ToList();

            foreach (var area in topAreasList)
            {
                var topOwner = area.Contributors
                    .OrderByDescending(c => c.ActivityShare)
                    .FirstOrDefault();

                string ownerStr = topOwner != null 
                    ? $"**{topOwner.Name}** ({topOwner.ActivityShare * 100:F0}%)" 
                    : "Shared";

                sb.AppendLine($"| `{area.Area}` | {area.FileCount} | {area.Touches} | {area.Churn} | {ownerStr} |");
            }
            sb.AppendLine();

            if (result.Warnings != null && result.Warnings.Count > 0)
            {
                sb.AppendLine("## ⚠️ Warnings & Recommendations");
                foreach (var warning in result.Warnings)
                {
                    sb.AppendLine($"- {warning}");
                }
                sb.AppendLine();
            }

            var assembly = typeof(Cli).Assembly;
            var version = assembly.GetName().Version?.ToString(3) ?? "0.1.0";
            var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            var displayVersion = string.IsNullOrEmpty(infoVersion) ? version : infoVersion;
            sb.AppendLine($"---\n*Report generated by **Gitic** (v{displayVersion})*");

            return Task.FromResult(sb.ToString());
        }
    }
}
