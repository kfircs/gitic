using System;
using System.Collections.Generic;
using System.Linq;
using static Gitic.TuiExplorer;

namespace Gitic;

public class LinesStructurePerspective : ITuiPerspective
{
    public int PerspectiveId => 1;
    public string DisplayName => "Lines & Structure";

    public List<string> GetRightSidebarLines(TuiNode node, int width, AnalysisResult result)
    {
        var lines = new List<string>();

        DateTime referenceDate = DateTime.UtcNow;
        if (result?.Analysis != null && !string.IsNullOrEmpty(result.Analysis.GeneratedAt))
        {
            if (DateTime.TryParse(result.Analysis.GeneratedAt, out var parsed))
            {
                referenceDate = parsed;
            }
        }

        if (node.IsDirectory)
        {
            var minFile = node.FindMinLoCFile();
            var maxFile = node.FindMaxLoCFile();
            string minName = minFile != null ? minFile.Name : "N/A";
            string maxName = maxFile != null ? maxFile.Name : "N/A";
            int avgLines = node.FileCount > 0 ? node.TotalLines / node.FileCount : 0;

            double minPct = node.MaxLines > 0 ? (double)node.MinLines / node.MaxLines : 0;
            double avgPct = node.MaxLines > 0 ? (double)avgLines / node.MaxLines : 0;

            // Generate single-line timeline curve dynamically based on right-hand panel width
            string singleLineCurve = "";
            string legendLine = "";
            if (node.MaxLines > node.MinLines)
            {
                double range = node.MaxLines - node.MinLines;
                double avgRatio = (avgLines - node.MinLines) / range;
                int trackLen = Math.Max(10, width - 38); // Calculate safe available horizontal characters
                int leftLen = (int)Math.Round(avgRatio * trackLen);
                leftLen = Math.Clamp(leftLen, 0, trackLen);
                int rightLen = trackLen - leftLen;

                string leftTrack = new string('─', leftLen);
                string rightTrack = new string('─', rightLen);

                singleLineCurve = $"  {CatppuccinMocha.Green}Min ({node.MinLines}){CatppuccinMocha.Gray}{leftTrack}{CatppuccinMocha.Yellow}▲ Avg ({avgLines}){CatppuccinMocha.Gray}{rightTrack}{CatppuccinMocha.Red}█ Max ({node.MaxLines}){CatppuccinMocha.Reset}";
                legendLine = $"  {CatppuccinMocha.Green}◆ Min {CatppuccinMocha.Gray}│{CatppuccinMocha.Yellow} ▲ Avg ({avgPct:P0} of scale){CatppuccinMocha.Gray} │{CatppuccinMocha.Red} █ Max{CatppuccinMocha.Reset}";
            }
            else
            {
                singleLineCurve = $"  {CatppuccinMocha.Green}Single File LoC: {CatppuccinMocha.Yellow}█ {node.MaxLines} lines{CatppuccinMocha.Reset}";
                legendLine = $"  {CatppuccinMocha.Gray}(No distribution variance - 1 file){CatppuccinMocha.Reset}";
            }

            lines.Add($"{CatppuccinMocha.Peach}📂 {(node.RelativePath == "" ? "Repository Statistics" : "Module Statistics: " + node.RelativePath)}{CatppuccinMocha.Reset}");
            lines.Add($"  {CatppuccinMocha.Text}Total Lines of Code:   {CatppuccinMocha.Yellow}{node.TotalLines:N0}{CatppuccinMocha.Text} lines{CatppuccinMocha.Reset}");
            lines.Add($"  {CatppuccinMocha.Text}Valid Code Files:      {CatppuccinMocha.Yellow}{node.FileCount:N0}{CatppuccinMocha.Text} files{CatppuccinMocha.Reset}");
            lines.Add("");
            lines.Add($"\x1b[1m{CatppuccinMocha.Pink}Lines of Code Distribution:{CatppuccinMocha.Reset}");
            lines.Add($"  {CatppuccinMocha.Gray}├─{CatppuccinMocha.Text} Minimum File LoC:   {CatppuccinMocha.Green}{node.MinLines:N0}{CatppuccinMocha.Text}  {CatppuccinMocha.Lavender}({minName}){CatppuccinMocha.Reset}");
            lines.Add($"  {CatppuccinMocha.Gray}├─{CatppuccinMocha.Text} Average File LoC:   {CatppuccinMocha.Green}{avgLines:N0}{CatppuccinMocha.Reset}");
            lines.Add($"  {CatppuccinMocha.Gray}└─{CatppuccinMocha.Text} Maximum File LoC:   {CatppuccinMocha.Green}{node.MaxLines:N0}{CatppuccinMocha.Text}  {CatppuccinMocha.Lavender}({maxName}){CatppuccinMocha.Reset}");
            lines.Add("");
            lines.Add($"\x1b[1m{CatppuccinMocha.Pink}LoC Size Distribution Curve:{CatppuccinMocha.Reset}");
            lines.Add(singleLineCurve);
            lines.Add(legendLine);
            lines.Add("");
            lines.Add(""); // Line 13

            if (node.RelativePath == "")
            {
                lines.Add($"\x1b[1m{CatppuccinMocha.Pink}Key Contributors:{CatppuccinMocha.Reset}");
                var contribs = GetDirectoryContributorsLines(node);
                lines.Add(contribs.Count > 0 ? $"{CatppuccinMocha.Text}{contribs[0]}{CatppuccinMocha.Reset}" : "");
                lines.Add(contribs.Count > 1 ? $"{CatppuccinMocha.Text}{contribs[1]}{CatppuccinMocha.Reset}" : "");
            }
            else
            {
                lines.Add($"\x1b[1m{CatppuccinMocha.Pink}Risk Factors:{CatppuccinMocha.Reset}");
                var highAttFile = node.FindHighestAttentionFile();
                string highAttName = highAttFile != null ? highAttFile.Name : "N/A";
                double highAttScore = highAttFile?.FileMetric?.AttentionScore ?? 0.0;
                string alertColor = highAttScore > 60.0 ? CatppuccinMocha.Red : highAttScore > 30.0 ? CatppuccinMocha.Yellow : CatppuccinMocha.Green;
                lines.Add($"  {CatppuccinMocha.Gray}└─{CatppuccinMocha.Text} Highest Attention Score:  {alertColor}{highAttScore:F1}{CatppuccinMocha.Text} {CatppuccinMocha.Lavender}({highAttName}){CatppuccinMocha.Reset}");
                lines.Add("");
            }
        }
        else
        {
            var metric = node.FileMetric;
            if (metric == null) return lines;

            lines.Add($"{CatppuccinMocha.Peach}📄 File Statistics: {node.Name}{CatppuccinMocha.Reset}");
            lines.Add($"  {CatppuccinMocha.Text}Lines of Code:         {CatppuccinMocha.Yellow}{node.TotalLines:N0}{CatppuccinMocha.Text} lines{CatppuccinMocha.Reset}");
            lines.Add($"  {CatppuccinMocha.Text}File Physical Size:     {CatppuccinMocha.Yellow}{FormatBytes(metric.Size ?? 0)}{CatppuccinMocha.Reset}");
            lines.Add($"  {CatppuccinMocha.Text}Max Line Width:        {CatppuccinMocha.Yellow}{node.MaxWidth:N0}{CatppuccinMocha.Text} characters{CatppuccinMocha.Reset}");
            lines.Add("");
            lines.Add($"\x1b[1m{CatppuccinMocha.Pink}Git Metrics:{CatppuccinMocha.Reset}");
            lines.Add($"  {CatppuccinMocha.Gray}├─{CatppuccinMocha.Text} Cumulative Touches:  {CatppuccinMocha.Green}{metric.Touches:N0}{CatppuccinMocha.Text} times{CatppuccinMocha.Reset}");
            lines.Add($"  {CatppuccinMocha.Gray}├─{CatppuccinMocha.Text} Cumulative Churn:    {CatppuccinMocha.Green}{metric.Churn:N0}{CatppuccinMocha.Text} lines changed{CatppuccinMocha.Reset}");
            lines.Add($"  {CatppuccinMocha.Gray}└─{CatppuccinMocha.Text} Last Touched:        {CatppuccinMocha.Green}{metric.LastTouched}{CatppuccinMocha.Text} {CatppuccinMocha.Lavender}({GetDaysAgoString(metric.LastTouched, referenceDate)}){CatppuccinMocha.Reset}");
            lines.Add("");
            lines.Add($"\x1b[1m{CatppuccinMocha.Pink}Hotspot Risk Profile:{CatppuccinMocha.Reset}");

            string attAlertColor = metric.AttentionScore > 60.0 ? CatppuccinMocha.Red : metric.AttentionScore > 30.0 ? CatppuccinMocha.Yellow : CatppuccinMocha.Green;
            string alertText = metric.AttentionScore > 60.0 ? "[⚠ High Attention]" : metric.AttentionScore > 30.0 ? "[■ Moderate Attention]" : "[Normal]";
            lines.Add($"  {CatppuccinMocha.Gray}├─{CatppuccinMocha.Text} Attention Score:     {attAlertColor}{metric.AttentionScore:F1}{CatppuccMainText(metric.AttentionScore)}{CatppuccinMocha.Reset}");

            string heatAlertColor = metric.HeatScore > 60.0 ? CatppuccinMocha.Red : metric.HeatScore > 30.0 ? CatppuccinMocha.Yellow : CatppuccinMocha.Green;
            string alertTextHeat = metric.HeatScore > 60.0 ? "[🔥 High Heat]" : metric.HeatScore > 30.0 ? "[■ Moderate Heat]" : "[Normal]";
            lines.Add($"  {CatppuccinMocha.Gray}└─{CatppuccinMocha.Text} Heat Score:          {heatAlertColor}{metric.HeatScore:F1}{CatppuccinMocha.Text}   {heatAlertColor}{alertTextHeat}{CatppuccinMocha.Reset}");

            lines.Add("");
            lines.Add($"\x1b[1m{CatppuccinMocha.Pink}Ownership Spread:{CatppuccinMocha.Reset}");

            var contribs = GetContributorsLines(metric);
            lines.Add(contribs.Count > 0 ? $"{CatppuccinMocha.Text}{contribs[0]}{CatppuccinMocha.Reset}" : "");
            lines.Add(contribs.Count > 1 ? $"{CatppuccinMocha.Text}{contribs[1]}{CatppuccinMocha.Reset}" : "");
        }
        return lines;
    }

    private static string CatppuccMainText(double score)
    {
        string text = score > 60.0 ? "[⚠ High Attention]" : score > 30.0 ? "[■ Moderate Attention]" : "[Normal]";
        return $"{CatppuccinMocha.Text}  {text}";
    }

    private static string GetDaysAgoString(string lastTouched, DateTime referenceDate)
    {
        if (string.IsNullOrEmpty(lastTouched)) return "N/A";
        if (DateTime.TryParse(lastTouched, out var date))
        {
            int days = (int)(referenceDate - date).TotalDays;
            return days <= 0 ? "today" : days == 1 ? "1 day ago" : $"{days} days ago";
        }
        return "N/A";
    }

    private static List<string> GetContributorsLines(FileMetric metric)
    {
        var result = new List<string>();
        if (metric.Contributors == null || metric.Contributors.Count == 0)
        {
            result.Add("  - No contributor record");
            return result;
        }

        var sorted = metric.Contributors.OrderByDescending(c => c.ActivityShare).Take(2).ToList();
        foreach (var c in sorted)
        {
            result.Add($"  - {c.Name}:                  {c.ActivityShare:P0} activity share");
        }
        return result;
    }

    private List<string> GetDirectoryContributorsLines(TuiNode node)
    {
        var result = new List<string>();
        var dict = new Dictionary<string, double>();
        AccumulateContributors(node, dict);
        if (dict.Count == 0)
        {
            result.Add("  - No contributor record");
            return result;
        }

        double total = dict.Values.Sum();
        if (total == 0) total = 1;

        var sorted = dict.OrderByDescending(kv => kv.Value).Take(2).ToList();
        foreach (var kv in sorted)
        {
            double share = kv.Value / total;
            result.Add($"  - {kv.Key} ({share:P0} share)");
        }
        return result;
    }

    private void AccumulateContributors(TuiNode node, Dictionary<string, double> dict)
    {
        if (!node.IsDirectory)
        {
            if (node.FileMetric?.Contributors != null)
            {
                foreach (var c in node.FileMetric.Contributors)
                {
                    if (dict.ContainsKey(c.Name))
                        dict[c.Name] += c.Activity;
                    else
                        dict[c.Name] = c.Activity;
                }
            }
            return;
        }
        foreach (var child in node.Children)
        {
            AccumulateContributors(child, dict);
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffix = { "B", "KB", "MB", "GB", "TB" };
        int i = 0;
        double dblBytes = bytes;
        while (dblBytes >= 1024 && i < suffix.Length - 1)
        {
            dblBytes /= 1024;
            i++;
        }
        return $"{dblBytes:F2} {suffix[i]}";
    }
}
