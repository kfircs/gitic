using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Gitic
{
    public interface IReportRenderer
    {
        Task<string> RenderAsync(AnalysisResult result);
    }

    public class JsonRenderer : IReportRenderer
    {
        public Task<string> RenderAsync(AnalysisResult result)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
            string json = JsonSerializer.Serialize(result, options) + "\n";
            return Task.FromResult(json);
        }
    }

    public class HtmlRenderer : IReportRenderer
    {
        private readonly string _htmlPath;

        public HtmlRenderer(string htmlPath)
        {
            _htmlPath = htmlPath;
        }

        public async Task<string> RenderAsync(AnalysisResult result)
        {
            var options = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
            string rawJson = JsonSerializer.Serialize(result, options);
            string data = rawJson.Replace("</script", "<\\/script", StringComparison.OrdinalIgnoreCase);
            string html = ReportTemplateHelper.GetHtmlReportTemplate(data);

            string targetPath = _htmlPath;
            if (Directory.Exists(targetPath))
            {
                targetPath = Path.Combine(targetPath, "report.html");
            }

            await File.WriteAllTextAsync(targetPath, html);
            return $"Wrote HTML report to {targetPath}\n";
        }
    }

    public class MarkdownRenderer : IReportRenderer
    {
        private readonly string _mdPath;

        public MarkdownRenderer(string mdPath)
        {
            _mdPath = mdPath;
        }

        public async Task<string> RenderAsync(AnalysisResult result)
        {
            var sb = new System.Text.StringBuilder();
            
            string repoName = Path.GetFileName(result.Analysis.RepoRoot.TrimEnd(Path.DirectorySeparatorChar));
            if (string.IsNullOrEmpty(repoName)) repoName = "Repository";

            sb.AppendLine($"# 📊 Gitic Analysis Report: {repoName}");
            sb.AppendLine($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");

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

            sb.AppendLine("---\n*Report generated by **Gitic** — Gitizer C# Port (v0.1.0)*");

            string targetPath = _mdPath;
            if (Directory.Exists(targetPath))
            {
                targetPath = Path.Combine(targetPath, "report.md");
            }

            await File.WriteAllTextAsync(targetPath, sb.ToString());
            return $"Wrote Markdown report to {targetPath}\n";
        }
    }

    public class CliReportFormatter
    {
        private readonly AnalysisResult _result;

        public CliReportFormatter(AnalysisResult result)
        {
            _result = result;
        }

        public string Format(string tableString, bool includeWarnings = false)
        {
            if (tableString.StartsWith("No contributor activity matched"))
            {
                return tableString;
            }

            string output = $"{tableString}\n";
            output += RenderExclusions(_result);
            if (includeWarnings)
            {
                output += RenderWarnings(_result);
            }
            return output;
        }

        private string RenderExclusions(AnalysisResult result)
        {
            if (result.Exclusions == null || result.Exclusions.Count == 0)
            {
                return "";
            }
            string summary = string.Join(", ", result.Exclusions
                .Select(exclusion => $"{exclusion.Category}:{exclusion.Count}"));
            return $"exclusions {summary}\n";
        }

        private string RenderWarnings(AnalysisResult result)
        {
            if (result.Warnings == null || result.Warnings.Count == 0)
            {
                return "";
            }
            return $"warnings {string.Join("; ", result.Warnings)}\n";
        }
    }

    public class CliTableRenderer : IReportRenderer
    {
        private readonly AnalysisCommand _command;

        public CliTableRenderer(AnalysisCommand command)
        {
            _command = command;
        }

        public Task<string> RenderAsync(AnalysisResult result)
        {
            if (result.Analysis.IncludedFileChangeCount == 0)
            {
                return Task.FromResult("No commits matched the selected analysis window. Try --all-time or a wider --since value.\n");
            }

            var formatter = new CliReportFormatter(result);

            if (_command == AnalysisCommand.Contributors)
            {
                string tableString = RenderContributorTable(result);
                return Task.FromResult(formatter.Format(tableString, includeWarnings: false));
            }
            if (_command == AnalysisCommand.Contributor)
            {
                string tableString = RenderSingleContributorTable(result);
                return Task.FromResult(formatter.Format(tableString, includeWarnings: false));
            }
            if (_command == AnalysisCommand.Areas)
            {
                string tableString = RenderAreaTable(result);
                return Task.FromResult(formatter.Format(tableString, includeWarnings: true));
            }

            // Default fallback to hotspots
            {
                string tableString = RenderHotspotTable(result);
                return Task.FromResult(formatter.Format(tableString, includeWarnings: true));
            }
        }

        private string RenderHotspotTable(AnalysisResult result)
        {
            IConsoleTableBuilder table = new ConsoleTableBuilder()
                .AddColumn("file", 28, "left")
                .AddColumn("attention", 9, "right")
                .AddColumn("heat", 5, "right")
                .AddColumn("churn", 6, "right")
                .AddColumn("contributors", 12, "right")
                .AddColumn("top activity share", 24, "left")
                .AddColumn("reasons");

            var filesToRender = result.Files.Take(20).ToList();
            foreach (var file in filesToRender)
            {
                table.AddRow(new List<string>
                {
                    file.Path,
                    file.AttentionScore.ToString(),
                    file.HeatScore.ToString(),
                    file.Churn.ToString(),
                    file.ContributorCount.ToString(),
                    TopContributor(file.Contributors),
                    ScoreReasons(file.ScoreBreakdown)
                });
            }

            return table.Render();
        }

        private string RenderAreaTable(AnalysisResult result)
        {
            IConsoleTableBuilder table = new ConsoleTableBuilder()
                .AddColumn("area", 28, "left")
                .AddColumn("heat", 5, "right")
                .AddColumn("attention", 9, "right")
                .AddColumn("churn", 6, "right")
                .AddColumn("contributors", 12, "right")
                .AddColumn("top activity share", 24, "left")
                .AddColumn("reasons");

            foreach (var area in result.Areas)
            {
                table.AddRow(new List<string>
                {
                    area.Area,
                    area.HeatScore.ToString(),
                    area.AttentionScore.ToString(),
                    area.Churn.ToString(),
                    area.ContributorCount.ToString(),
                    TopContributor(area.Contributors),
                    ScoreReasons(area.ScoreBreakdown)
                });
            }

            return table.Render();
        }

        private string RenderContributorTable(AnalysisResult result)
        {
            IConsoleTableBuilder table = new ConsoleTableBuilder()
                .AddColumn("contributor", 24, "left")
                .AddColumn("activity", 8, "right")
                .AddColumn("top area");

            foreach (var contributor in result.Contributors)
            {
                string topArea = contributor.Areas.Count > 0 ? contributor.Areas[0].Area : "";
                table.AddRow(new List<string>
                {
                    contributor.Name,
                    contributor.TotalActivity.ToString(),
                    topArea
                });
            }

            return table.Render();
        }

        private string RenderSingleContributorTable(AnalysisResult result)
        {
            if (result.Contributors == null || result.Contributors.Count == 0)
            {
                return "No contributor activity matched the selected analysis.\n";
            }

            var contributor = result.Contributors[0];
            IConsoleTableBuilder table = new ConsoleTableBuilder()
                .AddColumn("area", 28, "left")
                .AddColumn("familiarity", 11, "right")
                .AddColumn("activity", 8, "right")
                .AddColumn("share");

            foreach (var area in contributor.Areas)
            {
                table.AddRow(new List<string>
                {
                    area.Area,
                    area.FamiliarityScore.ToString(),
                    area.Activity.ToString(),
                    $"{Math.Round(area.ActivityShare * 100)}%"
                });
            }

            return $"{contributor.Name} <{contributor.Email}>\n{table.Render()}";
        }

        private string TopContributor(List<ContributorShare> contributors)
        {
            if (contributors == null || contributors.Count == 0)
            {
                return "";
            }
            var contributor = contributors[0];
            return $"{contributor.Name} {Math.Round(contributor.ActivityShare * 100)}%";
        }

        private string ScoreReasons(ScoreBreakdown breakdown)
        {
            var reasons = new List<Tuple<string, double>>
            {
                Tuple.Create("churn", breakdown.Churn),
                Tuple.Create("recent", breakdown.Recency),
                Tuple.Create("spread", breakdown.ContributorSpread),
                Tuple.Create("low familiarity", breakdown.LowFamiliarityConcentration)
            };

            var sortedReasons = reasons
                .OrderByDescending(r => r.Item2)
                .Take(2)
                .Where(r => r.Item2 > 0)
                .Select(r => $"{r.Item1} {Math.Round(r.Item2 * 100)}%")
                .ToList();

            return sortedReasons.Count == 0 ? "no activity" : string.Join(", ", sortedReasons);
        }
    }

    public static class SvgGeneratorHelper
    {
        private static string EscapeXml(string value)
        {
            return value.Replace("&", "&amp;")
                        .Replace("<", "&lt;")
                        .Replace(">", "&gt;")
                        .Replace("\"", "&quot;")
                        .Replace("'", "&apos;");
        }

        public static string GenerateSvg(AnalysisResult result)
        {
            var sb = new System.Text.StringBuilder();

            int width = 800;
            int height = 450;
            int padLeft = 70;
            int padRight = 50;
            int padTop = 50;
            int padBottom = 60;

            int plotWidth = width - padLeft - padRight;
            int plotHeight = height - padTop - padBottom;

            var sortedFiles = result.Files
                .OrderByDescending(f => f.AttentionScore)
                .ThenBy(f => f.Path)
                .ToList();

            double maxChurn = sortedFiles.Count > 0 ? sortedFiles.Max(f => f.Churn) : 100;
            if (maxChurn <= 0) maxChurn = 100;

            sb.AppendLine($"<svg viewBox=\"0 0 {width} {height}\" width=\"100%\" height=\"auto\" xmlns=\"http://www.w3.org/2000/svg\" style=\"background-color:#0f172a; border-radius:8px; border:1px solid #1e293b; font-family:system-ui, -apple-system, sans-serif;\">");

            sb.AppendLine("  <defs>");
            sb.AppendLine("    <linearGradient id=\"bgGrad\" x1=\"0\" y1=\"0\" x2=\"1\" y2=\"1\">");
            sb.AppendLine("      <stop offset=\"0%\" stop-color=\"#0f172a\" />");
            sb.AppendLine("      <stop offset=\"100%\" stop-color=\"#1e293b\" />");
            sb.AppendLine("    </linearGradient>");
            sb.AppendLine("  </defs>");

            sb.AppendLine($"  <rect width=\"{width}\" height=\"{height}\" fill=\"url(#bgGrad)\" rx=\"8\" />");

            double midX = padLeft + plotWidth / 2.0;
            double midY = padTop + plotHeight / 2.0;

            sb.AppendLine($"  <rect x=\"{midX}\" y=\"{padTop}\" width=\"{plotWidth / 2.0}\" height=\"{plotHeight / 2.0}\" fill=\"#ef4444\" fill-opacity=\"0.02\" />");
            sb.AppendLine($"  <rect x=\"{padLeft}\" y=\"{padTop}\" width=\"{plotWidth / 2.0}\" height=\"{plotHeight / 2.0}\" fill=\"#f59e0b\" fill-opacity=\"0.01\" />");
            sb.AppendLine($"  <rect x=\"{midX}\" y=\"{midY}\" width=\"{plotWidth / 2.0}\" height=\"{plotHeight / 2.0}\" fill=\"#3b82f6\" fill-opacity=\"0.01\" />");
            sb.AppendLine($"  <rect x=\"{padLeft}\" y=\"{midY}\" width=\"{plotWidth / 2.0}\" height=\"{plotHeight / 2.0}\" fill=\"#10b981\" fill-opacity=\"0.01\" />");

            sb.AppendLine($"  <line x1=\"{midX}\" y1=\"{padTop}\" x2=\"{midX}\" y2=\"{padTop + plotHeight}\" stroke=\"#334155\" stroke-dasharray=\"4 4\" stroke-width=\"1\" opacity=\"0.5\" />");
            sb.AppendLine($"  <line x1=\"{padLeft}\" y1=\"{midY}\" x2=\"{padLeft + plotWidth}\" y2=\"{midY}\" stroke=\"#334155\" stroke-dasharray=\"4 4\" stroke-width=\"1\" opacity=\"0.5\" />");

            sb.AppendLine($"  <line x1=\"{padLeft}\" y1=\"{padTop + plotHeight}\" x2=\"{padLeft + plotWidth}\" y2=\"{padTop + plotHeight}\" stroke=\"#475569\" stroke-width=\"1.5\" />");
            sb.AppendLine($"  <line x1=\"{padLeft}\" y1=\"{padTop}\" x2=\"{padLeft}\" y2=\"{padTop + plotHeight}\" stroke=\"#475569\" stroke-width=\"1.5\" />");

            sb.AppendLine($"  <text x=\"{padLeft + plotWidth - 10}\" y=\"{padTop + 20}\" fill=\"#ef4444\" font-size=\"11\" font-weight=\"bold\" text-anchor=\"end\" opacity=\"0.7\">🔥 Volatile Hotspots</text>");
            sb.AppendLine($"  <text x=\"{padLeft + 10}\" y=\"{padTop + 20}\" fill=\"#f59e0b\" font-size=\"11\" font-weight=\"bold\" text-anchor=\"start\" opacity=\"0.7\">📦 Complex Heritage</text>");
            sb.AppendLine($"  <text x=\"{padLeft + plotWidth - 10}\" y=\"{padTop + plotHeight - 15}\" fill=\"#3b82f6\" font-size=\"11\" font-weight=\"bold\" text-anchor=\"end\" opacity=\"0.7\">⚡ Active Refactoring</text>");
            sb.AppendLine($"  <text x=\"{padLeft + 10}\" y=\"{padTop + plotHeight - 15}\" fill=\"#10b981\" font-size=\"11\" font-weight=\"bold\" text-anchor=\"start\" opacity=\"0.7\">🌿 Low Maintenance</text>");

            sb.AppendLine($"  <text x=\"{padLeft + plotWidth / 2.0}\" y=\"{height - 15}\" fill=\"#94a3b8\" font-size=\"12\" text-anchor=\"middle\" font-weight=\"500\">Churn Volume (lines changed)</text>");
            sb.AppendLine($"  <text x=\"18\" y=\"{padTop + plotHeight / 2.0}\" fill=\"#94a3b8\" font-size=\"12\" text-anchor=\"middle\" transform=\"rotate(-90 18 {padTop + plotHeight / 2.0})\" font-weight=\"500\">Attention Score (0 - 100)</text>");

            for (int val = 0; val <= 100; val += 25)
            {
                double y = padTop + plotHeight * (1.0 - val / 100.0);
                sb.AppendLine($"  <line x1=\"{padLeft - 4}\" y1=\"{y}\" x2=\"{padLeft}\" y2=\"{y}\" stroke=\"#475569\" stroke-width=\"1.5\" />");
                sb.AppendLine($"  <text x=\"{padLeft - 8}\" y=\"{y + 4}\" fill=\"#64748b\" font-size=\"10\" text-anchor=\"end\">{val}</text>");
            }

            double[] xTicks = { 0, maxChurn / 2.0, maxChurn };
            foreach (double val in xTicks)
            {
                double x = padLeft + plotWidth * (val / maxChurn);
                sb.AppendLine($"  <line x1=\"{x}\" y1=\"{padTop + plotHeight}\" x2=\"{x}\" y2=\"{padTop + plotHeight + 4}\" stroke=\"#475569\" stroke-width=\"1.5\" />");
                sb.AppendLine($"  <text x=\"{x}\" y=\"{padTop + plotHeight + 18}\" fill=\"#64748b\" font-size=\"10\" text-anchor=\"middle\">{val:F0}</text>");
            }

            var filesToPlot = sortedFiles.Take(80).Reverse().ToList();

            foreach (var file in filesToPlot)
            {
                double cx = padLeft + plotWidth * (file.Churn / maxChurn);
                double cy = padTop + plotHeight * (1.0 - file.AttentionScore / 100.0);

                double r = 4.0;
                if (file.Lines.HasValue)
                {
                    r = 4.0 + (file.Lines.Value / 2000.0) * 12.0;
                    if (r > 16) r = 16;
                }

                string color = "#3b82f6";
                if (file.AttentionScore >= 70)
                {
                    color = file.ReworkRate.GetValueOrDefault(0) > 0.20 ? "#ef4444" : "#f59e0b";
                }
                else if (file.AttentionScore >= 40)
                {
                    color = "#eab308";
                }
                else
                {
                    color = "#10b981";
                }

                string tooltip = $"{file.Path}\nAttention: {file.AttentionScore:F1}\nLines: {file.Lines.GetValueOrDefault(0)}\nChurn: {file.Churn}\nRework Rate: {file.ReworkRate.GetValueOrDefault(0)*100:F1}%";

                sb.AppendLine($"  <circle cx=\"{cx:F1}\" cy=\"{cy:F1}\" r=\"{r:F1}\" fill=\"{color}\" fill-opacity=\"0.75\" stroke=\"#1e293b\" stroke-width=\"1\">");
                sb.AppendLine($"    <title>{EscapeXml(tooltip)}</title>");
                sb.AppendLine("  </circle>");
            }

            var top5Label = sortedFiles.Take(5).ToList();
            int labelCount = 0;
            foreach (var file in top5Label)
            {
                double cx = padLeft + plotWidth * (file.Churn / maxChurn);
                double cy = padTop + plotHeight * (1.0 - file.AttentionScore / 100.0);

                string fileLabel = Path.GetFileName(file.Path);
                
                string textAnchor = cx > width / 2.0 ? "end" : "start";
                double textOffset = cx > width / 2.0 ? -12 : 12;
                double textYOffset = labelCount % 2 == 0 ? -12 : 12;

                sb.AppendLine($"  <line x1=\"{cx}\" y1=\"{cy}\" x2=\"{cx + textOffset}\" y2=\"{cy + textYOffset}\" stroke=\"#94a3b8\" stroke-width=\"0.8\" opacity=\"0.6\" />");
                sb.AppendLine($"  <text x=\"{cx + textOffset + (cx > width / 2.0 ? -3 : 3)}\" y=\"{cy + textYOffset + 4}\" fill=\"#f1f5f9\" font-size=\"9\" font-weight=\"bold\" text-anchor=\"{textAnchor}\" opacity=\"0.95\">{fileLabel}</text>");

                labelCount++;
            }

            sb.AppendLine("</svg>");
            return sb.ToString();
            }

            public static string GenerateComplexityRangesSvg(AnalysisResult result)
            {
            var sb = new System.Text.StringBuilder();

            var allFilesWithLines = result.Files.Where(f => f.Lines.HasValue && f.Lines.Value > 0).Select(f => (double)f.Lines!.Value).ToList();
            var allFilesWithWidth = result.Files.Where(f => f.Width.HasValue && f.Width.Value > 0).Select(f => (double)f.Width!.Value).ToList();

            double overallMaxLines = allFilesWithLines.Count > 0 ? allFilesWithLines.Max() : 100;
            double overallMaxWidth = allFilesWithWidth.Count > 0 ? allFilesWithWidth.Max() : 100;
            if (overallMaxLines <= 0) overallMaxLines = 100;
            if (overallMaxWidth <= 0) overallMaxWidth = 100;

            var topAreas = result.Areas
                .OrderByDescending(a => a.FileCount)
                .ThenByDescending(a => a.Touches)
                .Take(5)
                .ToList();

            int headerHeight = 65;
            int rowHeight = 45;
            int footerHeight = 40;
            int width = 800;
            int height = headerHeight + (topAreas.Count * rowHeight) + footerHeight;

            sb.AppendLine($"<svg viewBox=\"0 0 {width} {height}\" width=\"100%\" height=\"auto\" xmlns=\"http://www.w3.org/2000/svg\" style=\"background-color:#0f172a; border-radius:8px; border:1px solid #1e293b; font-family:system-ui, -apple-system, sans-serif;\">");

            sb.AppendLine("  <defs>");
            sb.AppendLine("    <linearGradient id=\"linesGrad\" x1=\"0\" y1=\"0\" x2=\"1\" y2=\"0\">");
            sb.AppendLine("      <stop offset=\"0%\" stop-color=\"#3b82f6\" stop-opacity=\"0.4\" />");
            sb.AppendLine("      <stop offset=\"100%\" stop-color=\"#3b82f6\" stop-opacity=\"0.9\" />");
            sb.AppendLine("    </linearGradient>");
            sb.AppendLine("    <linearGradient id=\"widthGrad\" x1=\"0\" y1=\"0\" x2=\"1\" y2=\"0\">");
            sb.AppendLine("      <stop offset=\"0%\" stop-color=\"#10b981\" stop-opacity=\"0.4\" />");
            sb.AppendLine("      <stop offset=\"100%\" stop-color=\"#10b981\" stop-opacity=\"0.9\" />");
            sb.AppendLine("    </linearGradient>");
            sb.AppendLine("  </defs>");

            sb.AppendLine("  <text x=\"20\" y=\"30\" fill=\"#f1f5f9\" font-size=\"14\" font-weight=\"bold\">📏 Complexity Distribution by App Module</text>");

            int colLabelY = 55;
            int labelWidth = 160;
            int barWidth = 270;
            
            int leftBarX = 20 + labelWidth;
            int rightBarX = 20 + labelWidth + barWidth + 40;

            sb.AppendLine($"  <text x=\"20\" y=\"{colLabelY}\" fill=\"#64748b\" font-size=\"10\" font-weight=\"bold\">MODULE / DIRECTORY</text>");
            sb.AppendLine($"  <text x=\"{leftBarX + barWidth / 2}\" y=\"{colLabelY}\" fill=\"#3b82f6\" font-size=\"10\" font-weight=\"bold\" text-anchor=\"middle\">FILE LENGTH (LINES) [Max: {overallMaxLines:F0}]</text>");
            sb.AppendLine($"  <text x=\"{rightBarX + barWidth / 2}\" y=\"{colLabelY}\" fill=\"#10b981\" font-size=\"10\" font-weight=\"bold\" text-anchor=\"middle\">MAX LINE WIDTH (CHARS) [Max: {overallMaxWidth:F0}]</text>");

            sb.AppendLine($"  <line x1=\"{leftBarX}\" y1=\"{colLabelY + 5}\" x2=\"{leftBarX}\" y2=\"{height - footerHeight + 5}\" stroke=\"#1e293b\" stroke-width=\"1\" />");
            sb.AppendLine($"  <line x1=\"{leftBarX + barWidth}\" y1=\"{colLabelY + 5}\" x2=\"{leftBarX + barWidth}\" y2=\"{height - footerHeight + 5}\" stroke=\"#1e293b\" stroke-width=\"1\" opacity=\"0.5\" />");
            sb.AppendLine($"  <line x1=\"{rightBarX}\" y1=\"{colLabelY + 5}\" x2=\"{rightBarX}\" y2=\"{height - footerHeight + 5}\" stroke=\"#1e293b\" stroke-width=\"1\" />");
            sb.AppendLine($"  <line x1=\"{rightBarX + barWidth}\" y1=\"{colLabelY + 5}\" x2=\"{rightBarX + barWidth}\" y2=\"{height - footerHeight + 5}\" stroke=\"#1e293b\" stroke-width=\"1\" opacity=\"0.5\" />");

            int rowIdx = 0;
            foreach (var area in topAreas)
            {
                int rowY = headerHeight + (rowIdx * rowHeight);

                var areaFilesWithLines = result.Files
                    .Where(f => f.Area == area.Area && f.Lines.HasValue && f.Lines.Value > 0)
                    .Select(f => (double)f.Lines!.Value)
                    .ToList();
                var areaFilesWithWidth = result.Files
                    .Where(f => f.Area == area.Area && f.Width.HasValue && f.Width.Value > 0)
                    .Select(f => (double)f.Width!.Value)
                    .ToList();

                double minLines = areaFilesWithLines.Count > 0 ? areaFilesWithLines.Min() : 0;
                double maxLines = areaFilesWithLines.Count > 0 ? areaFilesWithLines.Max() : 0;
                double avgLines = areaFilesWithLines.Count > 0 ? areaFilesWithLines.Average() : 0;

                double minWidth = areaFilesWithWidth.Count > 0 ? areaFilesWithWidth.Min() : 0;
                double maxWidth = areaFilesWithWidth.Count > 0 ? areaFilesWithWidth.Max() : 0;
                double avgWidth = areaFilesWithWidth.Count > 0 ? areaFilesWithWidth.Average() : 0;

                string areaName = area.Area;
                if (areaName.Length > 24)
                {
                    areaName = "..." + areaName.Substring(areaName.Length - 21);
                }
                if (string.IsNullOrEmpty(areaName) || areaName == ".")
                {
                    areaName = "[Root Directory]";
                }

                string rowBg = rowIdx % 2 == 0 ? "transparent" : "#1e293b";
                sb.AppendLine($"  <rect x=\"10\" y=\"{rowY - 5}\" width=\"{width - 20}\" height=\"{rowHeight}\" fill=\"{rowBg}\" fill-opacity=\"0.15\" rx=\"4\" />");

                sb.AppendLine($"  <text x=\"20\" y=\"{rowY + 18}\" fill=\"#f1f5f9\" font-size=\"11\" font-weight=\"bold\">{areaName}</text>");
                sb.AppendLine($"  <text x=\"20\" y=\"{rowY + 30}\" fill=\"#64748b\" font-size=\"9\">{area.FileCount} files, {area.Touches} touches</text>");

                sb.AppendLine($"  <rect x=\"{leftBarX}\" y=\"{rowY + 10}\" width=\"{barWidth}\" height=\"12\" fill=\"#1e293b\" rx=\"3\" />");
                if (maxLines > 0)
                {
                    double rangeStartX = leftBarX + barWidth * (minLines / overallMaxLines);
                    double rangeWidth = barWidth * ((maxLines - minLines) / overallMaxLines);
                    if (rangeWidth < 2) rangeWidth = 2;
                    sb.AppendLine($"  <rect x=\"{rangeStartX:F1}\" y=\"{rowY + 10}\" width=\"{rangeWidth:F1}\" height=\"12\" fill=\"url(#linesGrad)\" rx=\"3\" />");

                    double avgX = leftBarX + barWidth * (avgLines / overallMaxLines);
                    sb.AppendLine($"  <line x1=\"{avgX:F1}\" y1=\"{rowY + 7}\" x2=\"{avgX:F1}\" y2=\"{rowY + 25}\" stroke=\"#f43f5e\" stroke-width=\"1.5\" />");
                    sb.AppendLine($"  <circle cx=\"{avgX:F1}\" cy=\"{rowY + 16}\" r=\"3.5\" fill=\"#f43f5e\" stroke=\"#f1f5f9\" stroke-width=\"1\" />");
                    sb.AppendLine($"  <text x=\"{avgX:F1}\" y=\"{rowY + 4}\" fill=\"#f43f5e\" font-size=\"8\" font-weight=\"bold\" text-anchor=\"middle\">{avgLines:F0}</text>");
                }
                sb.AppendLine($"  <text x=\"{leftBarX}\" y=\"{rowY + 34}\" fill=\"#475569\" font-size=\"8\">Min: {minLines:F0}</text>");
                sb.AppendLine($"  <text x=\"{leftBarX + barWidth}\" y=\"{rowY + 34}\" fill=\"#475569\" font-size=\"8\" text-anchor=\"end\">Max: {maxLines:F0}</text>");

                sb.AppendLine($"  <rect x=\"{rightBarX}\" y=\"{rowY + 10}\" width=\"{barWidth}\" height=\"12\" fill=\"#1e293b\" rx=\"3\" />");
                if (maxWidth > 0)
                {
                    double rangeStartX = rightBarX + barWidth * (minWidth / overallMaxWidth);
                    double rangeWidth = barWidth * ((maxWidth - minWidth) / overallMaxWidth);
                    if (rangeWidth < 2) rangeWidth = 2;
                    sb.AppendLine($"  <rect x=\"{rangeStartX:F1}\" y=\"{rowY + 10}\" width=\"{rangeWidth:F1}\" height=\"12\" fill=\"url(#widthGrad)\" rx=\"3\" />");

                    double avgX = rightBarX + barWidth * (avgWidth / overallMaxWidth);
                    sb.AppendLine($"  <line x1=\"{avgX:F1}\" y1=\"{rowY + 7}\" x2=\"{avgX:F1}\" y2=\"{rowY + 25}\" stroke=\"#f43f5e\" stroke-width=\"1.5\" />");
                    sb.AppendLine($"  <circle cx=\"{avgX:F1}\" cy=\"{rowY + 16}\" r=\"3.5\" fill=\"#f43f5e\" stroke=\"#f1f5f9\" stroke-width=\"1\" />");
                    sb.AppendLine($"  <text x=\"{avgX:F1}\" y=\"{rowY + 4}\" fill=\"#f43f5e\" font-size=\"8\" font-weight=\"bold\" text-anchor=\"middle\">{avgWidth:F0}</text>");
                }
                sb.AppendLine($"  <text x=\"{rightBarX}\" y=\"{rowY + 34}\" fill=\"#475569\" font-size=\"8\">Min: {minWidth:F0}</text>");
                sb.AppendLine($"  <text x=\"{rightBarX + barWidth}\" y=\"{rowY + 34}\" fill=\"#475569\" font-size=\"8\" text-anchor=\"end\">Max: {maxWidth:F0}</text>");

                rowIdx++;
            }

            sb.AppendLine($"  <text x=\"400\" y=\"{height - 15}\" fill=\"#64748b\" font-size=\"9\" text-anchor=\"middle\">Comparative horizontal scale relative to overall codebase maxima. Magenta pins indicate the average complexity per module.</text>");

            sb.AppendLine("</svg>");
            return sb.ToString();
            }
            }

            public class SvgRenderer : IReportRenderer
            {
            private readonly string _svgPath;

            public SvgRenderer(string svgPath)
            {
            _svgPath = svgPath;
            }

            public async Task<string> RenderAsync(AnalysisResult result)
            {
            string svg = SvgGeneratorHelper.GenerateSvg(result);
            string complexitySvg = SvgGeneratorHelper.GenerateComplexityRangesSvg(result);

            string targetPath = _svgPath;
            string targetComplexityPath = _svgPath;
            if (Directory.Exists(targetPath))
            {
                targetPath = Path.Combine(targetPath, "report.svg");
                targetComplexityPath = Path.Combine(targetComplexityPath, "report-complexity.svg");
            }
            else
            {
                string dir = Path.GetDirectoryName(targetPath) ?? ".";
                string name = Path.GetFileNameWithoutExtension(targetPath);
                targetComplexityPath = Path.Combine(dir, $"{name}-complexity.svg");
            }

            await File.WriteAllTextAsync(targetPath, svg);
            await File.WriteAllTextAsync(targetComplexityPath, complexitySvg);
            return $"Wrote SVG report to {targetPath}\nWrote Svg Complexity report to {targetComplexityPath}\n";
            }
            }
            }
