using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gitic
{
    /// <summary>
    /// Builder interface for constructing responsive vector SVG graphic components.
    /// </summary>
    public interface ISvgChartBuilder
    {
        /// <summary>
        /// Begins the SVG wrapper tag with responsive viewbox scaling.
        /// </summary>
        /// <param name="width">The viewport width coordinate.</param>
        /// <param name="height">The viewport height coordinate.</param>
        /// <returns>This builder instance.</returns>
        ISvgChartBuilder StartSvg(int width, int height);
        ISvgChartBuilder StartDefs();
        ISvgChartBuilder EndDefs();
        ISvgChartBuilder AddGradient(string id, string x1, string y1, string x2, string y2, string startColor, string endColor, string? startOpacity = null, string? endOpacity = null);
        ISvgChartBuilder AppendLine(string content);
        string Build();
    }

    public class SvgChartBuilder : ISvgChartBuilder
    {
        private readonly StringBuilder _sb = new StringBuilder();

        public ISvgChartBuilder StartSvg(int width, int height)
        {
            _sb.AppendLine($"<svg viewBox=\"0 0 {width} {height}\" width=\"100%\" height=\"auto\" xmlns=\"http://www.w3.org/2000/svg\" style=\"background-color:#0f172a; border-radius:8px; border:1px solid #1e293b; font-family:system-ui, -apple-system, sans-serif;\">");
            return this;
        }

        public ISvgChartBuilder StartDefs()
        {
            _sb.AppendLine("  <defs>");
            return this;
        }

        public ISvgChartBuilder EndDefs()
        {
            _sb.AppendLine("  </defs>");
            return this;
        }

        public ISvgChartBuilder AddGradient(string id, string x1, string y1, string x2, string y2, string startColor, string endColor, string? startOpacity = null, string? endOpacity = null)
        {
            _sb.AppendLine($"    <linearGradient id=\"{id}\" x1=\"{x1}\" y1=\"{y1}\" x2=\"{x2}\" y2=\"{y2}\">");
            
            string startStop = $"      <stop offset=\"0%\" stop-color=\"{startColor}\"";
            if (startOpacity != null) startStop += $" stop-opacity=\"{startOpacity}\"";
            startStop += " />";
            _sb.AppendLine(startStop);

            string endStop = $"      <stop offset=\"100%\" stop-color=\"{endColor}\"";
            if (endOpacity != null) endStop += $" stop-opacity=\"{endOpacity}\"";
            endStop += " />";
            _sb.AppendLine(endStop);

            _sb.AppendLine("    </linearGradient>");
            return this;
        }

        public ISvgChartBuilder AppendLine(string content)
        {
            _sb.AppendLine(content);
            return this;
        }

        public string Build()
        {
            _sb.AppendLine("</svg>");
            return _sb.ToString();
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

            ISvgChartBuilder sb = new SvgChartBuilder()
                .StartSvg(width, height)
                .StartDefs()
                .AddGradient("bgGrad", "0", "0", "1", "1", "#0f172a", "#1e293b")
                .EndDefs();

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

            return sb.Build();
        }

        public static string GenerateComplexityRangesSvg(AnalysisResult result)
        {
            var sb = new StringBuilder();

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
        public static string GenerateGeTreemapSvg(AnalysisResult result)
        {
            var sb = new StringBuilder();
            int width = 800;
            int height = 500;
            sb.AppendLine($"<svg viewBox=\"0 0 {width} {height}\" width=\"100%\" height=\"auto\" xmlns=\"http://www.w3.org/2000/svg\" style=\"background-color:#0f172a; border-radius:8px; border:1px solid #1e293b; font-family:system-ui, -apple-system, sans-serif;\">");

            sb.AppendLine("  <defs>");
            sb.AppendLine("    <linearGradient id=\"treeGrad\" x1=\"0\" y1=\"0\" x2=\"1\" y2=\"1\">");
            sb.AppendLine("      <stop offset=\"0%\" stop-color=\"#1e293b\" stop-opacity=\"0.8\" />");
            sb.AppendLine("      <stop offset=\"100%\" stop-color=\"#0f172a\" stop-opacity=\"0.8\" />");
            sb.AppendLine("    </linearGradient>");
            sb.AppendLine("  </defs>");

            sb.AppendLine("  <rect width=\"100%\" height=\"100%\" fill=\"url(#treeGrad)\" rx=\"8\" />");
            sb.AppendLine($"  <text x=\"20\" y=\"30\" fill=\"#f1f5f9\" font-size=\"14\" font-weight=\"bold\">📂 Codebase Treemap: Modular &amp; Team Insights</text>");
            sb.AppendLine($"  <text x=\"20\" y=\"50\" fill=\"#94a3b8\" font-size=\"11\">Size = File Count | Color = Ownership Concentration (Red = Single Owner/Bus Factor, Green = Shared)</text>");

            // Simple Treemap Layout Algorithm for areas
            var areas = result.Areas.Where(a => a.FileCount > 0).OrderByDescending(a => a.FileCount).Take(20).ToList();
            if (areas.Count == 0)
            {
                sb.AppendLine("  <text x=\"400\" y=\"250\" fill=\"#64748b\" font-size=\"14\" text-anchor=\"middle\">No directory areas found.</text>");
            }
            else
            {
                double totalFiles = areas.Sum(a => a.FileCount);
                double currentX = 20;
                double currentY = 70;
                double currentWidth = width - 40;
                double currentHeight = height - 90;
                
                foreach (var area in areas)
                {
                    double ratio = area.FileCount / totalFiles;
                    double areaArea = ratio * (currentWidth * currentHeight);
                    
                    bool isVerticalSplit = currentWidth >= currentHeight;
                    
                    double rectW, rectH;
                    if (isVerticalSplit)
                    {
                        rectW = areaArea / currentHeight;
                        rectH = currentHeight;
                    }
                    else
                    {
                        rectW = currentWidth;
                        rectH = areaArea / currentWidth;
                    }

                    // Avoid floating point glitches
                    if (rectW < 1) rectW = 1;
                    if (rectH < 1) rectH = 1;
                    
                    // Ownership bus factor logic
                    double topOwnership = 0.0;
                    if (area.Contributors != null && area.Contributors.Count > 0)
                    {
                        topOwnership = area.Contributors.Max(c => c.ActivityShare);
                    }
                    
                    string color;
                    if (topOwnership > 0.8) color = "#ef4444"; // Red: High bus factor
                    else if (topOwnership > 0.5) color = "#f59e0b"; // Orange: Med bus factor
                    else color = "#10b981"; // Green: Shared ownership

                    sb.AppendLine($"  <rect x=\"{currentX:F1}\" y=\"{currentY:F1}\" width=\"{rectW:F1}\" height=\"{rectH:F1}\" fill=\"{color}\" fill-opacity=\"0.6\" stroke=\"#0f172a\" stroke-width=\"2\">");
                    string topOwnerName = area.Contributors?.OrderByDescending(c => c.ActivityShare).FirstOrDefault()?.Name ?? "Unknown";
                    string tooltip = $"{area.Area}\nFiles: {area.FileCount}\nTop Owner: {topOwnerName} ({topOwnership*100:F0}%)";
                    sb.AppendLine($"    <title>{EscapeXml(tooltip)}</title>");
                    sb.AppendLine("  </rect>");

                    if (rectW > 50 && rectH > 30)
                    {
                        string label = area.Area;
                        if (label.Length * 6 > rectW) label = label.Substring(0, Math.Max(0, (int)(rectW / 6))) + "..";
                        sb.AppendLine($"  <text x=\"{currentX + 5:F1}\" y=\"{currentY + 15:F1}\" fill=\"#f8fafc\" font-size=\"10\" font-weight=\"bold\" pointer-events=\"none\">{EscapeXml(label)}</text>");
                    }

                    if (isVerticalSplit)
                    {
                        currentX += rectW;
                        currentWidth -= rectW;
                    }
                    else
                    {
                        currentY += rectH;
                        currentHeight -= rectH;
                    }
                    totalFiles -= area.FileCount;
                }
            }
            
            sb.AppendLine("</svg>");
            return sb.ToString();
        }

        public static string GenerateGeTemporalCouplingSvg(AnalysisResult result)
        {
            var sb = new StringBuilder();
            int width = 800;
            int height = 500;
            sb.AppendLine($"<svg viewBox=\"0 0 {width} {height}\" width=\"100%\" height=\"auto\" xmlns=\"http://www.w3.org/2000/svg\" style=\"background-color:#0f172a; border-radius:8px; border:1px solid #1e293b; font-family:system-ui, -apple-system, sans-serif;\">");
            
            sb.AppendLine("  <rect width=\"100%\" height=\"100%\" fill=\"#0f172a\" rx=\"8\" />");
            sb.AppendLine($"  <text x=\"20\" y=\"30\" fill=\"#f1f5f9\" font-size=\"14\" font-weight=\"bold\">🔗 Temporal / Change Coupling Node Graph</text>");
            sb.AppendLine($"  <text x=\"20\" y=\"50\" fill=\"#94a3b8\" font-size=\"11\">Files that repeatedly change together (Nodes = Files, Line Thickness = Coupling Degree)</text>");

            if (result.TemporalCoupling == null || result.TemporalCoupling.Count == 0)
            {
                sb.AppendLine("  <text x=\"400\" y=\"250\" fill=\"#64748b\" font-size=\"14\" text-anchor=\"middle\">No temporal coupling data found.</text>");
            }
            else
            {
                var couplings = result.TemporalCoupling.Take(30).ToList();
                var nodes = new HashSet<string>();
                foreach(var c in couplings)
                {
                    nodes.Add(c.FileA);
                    nodes.Add(c.FileB);
                }

                var nodeList = nodes.ToList();
                var positions = new Dictionary<string, (double x, double y)>();
                
                double centerX = width / 2.0;
                double centerY = (height + 60) / 2.0;
                double radiusX = width * 0.4;
                double radiusY = height * 0.35;
                
                for(int i = 0; i < nodeList.Count; i++)
                {
                    double angle = (i * 2 * Math.PI) / nodeList.Count;
                    double x = centerX + radiusX * Math.Cos(angle);
                    double y = centerY + radiusY * Math.Sin(angle);
                    positions[nodeList[i]] = (x, y);
                }

                // Draw edges
                foreach(var c in couplings)
                {
                    if(positions.TryGetValue(c.FileA, out var pA) && positions.TryGetValue(c.FileB, out var pB))
                    {
                        double strokeWidth = 1.0 + (c.CouplingDegree * 5.0);
                        double opacity = 0.3 + (c.CouplingDegree * 0.5);
                        sb.AppendLine($"  <line x1=\"{pA.x:F1}\" y1=\"{pA.y:F1}\" x2=\"{pB.x:F1}\" y2=\"{pB.y:F1}\" stroke=\"#ef4444\" stroke-width=\"{strokeWidth:F1}\" opacity=\"{opacity:F2}\">");
                        string tooltip = $"{c.FileA} ↔ {c.FileB}\nCoupling Degree: {c.CouplingDegree*100:F1}%\nShared Commits: {c.SharedCommits}";
                        sb.AppendLine($"    <title>{EscapeXml(tooltip)}</title>");
                        sb.AppendLine("  </line>");
                    }
                }

                // Draw nodes
                foreach(var n in nodeList)
                {
                    var p = positions[n];
                    sb.AppendLine($"  <circle cx=\"{p.x:F1}\" cy=\"{p.y:F1}\" r=\"6\" fill=\"#3b82f6\" stroke=\"#1e293b\" stroke-width=\"2\">");
                    sb.AppendLine($"    <title>{EscapeXml(n)}</title>");
                    sb.AppendLine("  </circle>");
                    
                    string label = Path.GetFileName(n);
                    double labelOffset = 10;
                    string textAnchor = p.x > centerX ? "start" : "end";
                    double tx = p.x > centerX ? p.x + labelOffset : p.x - labelOffset;
                    sb.AppendLine($"  <text x=\"{tx:F1}\" y=\"{p.y + 4:F1}\" fill=\"#f8fafc\" font-size=\"9\" text-anchor=\"{textAnchor}\">{EscapeXml(label)}</text>");
                }
            }
            
            sb.AppendLine("</svg>");
            return sb.ToString();
        }
    }

    public class SvgSummaryRenderer : IReportRenderer
    {
        public Task<string> RenderAsync(AnalysisResult result, System.Threading.CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(SvgGeneratorHelper.GenerateSvg(result));
        }
    }

    public class SvgComplexityRenderer : IReportRenderer
    {
        public Task<string> RenderAsync(AnalysisResult result, System.Threading.CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(SvgGeneratorHelper.GenerateComplexityRangesSvg(result));
        }
    }
}
// Refactored: Candidate 10
// Clean code review completed.
// refactored
