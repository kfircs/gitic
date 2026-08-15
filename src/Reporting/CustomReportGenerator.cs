using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gitic;

public static class CustomReportGenerator
{
    public static string GenerateCustomMarkdown(AnalysisResult result, List<string> sections)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Gitic Custom Report");
        sb.AppendLine($"*Generated at {DateTime.Now}*");
        sb.AppendLine();

        if (result.CuratedReports == null) return sb.ToString();

        if (sections.Contains("Work Classification"))
        {
            sb.AppendLine("## 1. Work Classification");
            sb.AppendLine($"* **Features:** {result.CuratedReports.WorkClassification.Features}");
            sb.AppendLine($"* **Bugs:** {result.CuratedReports.WorkClassification.Bugs}");
            sb.AppendLine($"* **Technical Debt:** {result.CuratedReports.WorkClassification.TechnicalDebt}");
            sb.AppendLine($"* **Chores:** {result.CuratedReports.WorkClassification.Chores}");
            sb.AppendLine($"* **Unclassified:** {result.CuratedReports.WorkClassification.Unclassified}");
            sb.AppendLine();
        }

        if (sections.Contains("Developer Onboarding"))
        {
            sb.AppendLine("## 2. Developer Onboarding (TTFC)");
            foreach (var dev in result.CuratedReports.Onboarding.Take(10))
            {
                sb.AppendLine($"* **{dev.Developer}**: First commit on {dev.FirstCommitDate} ({dev.DaysActive} days active)");
            }
            sb.AppendLine();
        }

        if (sections.Contains("Code Rot"))
        {
            sb.AppendLine("## 3. Code Rot");
            sb.AppendLine($"* **Zombie Files (>1yr):** {result.CuratedReports.CodeRot.ZombieFileCount}");
            sb.AppendLine($"* **Zombie Lines (>1yr):** {result.CuratedReports.CodeRot.ZombieLines}");
            sb.AppendLine();
        }

        if (sections.Contains("Review Collaboration"))
        {
            sb.AppendLine("## 4. Review Collaboration & Silos");
            sb.AppendLine($"* **Reviewer Silos (Single-reviewer):** {result.CuratedReports.ReviewCollaboration.ReviewerSilos}");
            sb.AppendLine();
            sb.AppendLine("### Top Review Pairs");
            foreach (var pair in result.CuratedReports.ReviewCollaboration.Pairs.Take(5))
            {
                sb.AppendLine($"* {pair.Author} reviewed by {pair.Reviewer} ({pair.PrCount} PRs)");
            }
            sb.AppendLine();
        }

        if (sections.Contains("AI Code Strain"))
        {
            sb.AppendLine("## 5. AI Code Strain");
            sb.AppendLine($"* **High-Volume Commits (>20 files):** {result.CuratedReports.AiCodeStrain.HighVolumeCommits}");
            if (result.CuratedReports.AiCodeStrain.ReviewVelocityWarning)
            {
                sb.AppendLine("> ⚠️ **WARNING**: High proportion of large commits detected. Review capacity may be strained.");
            }
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine("### Repository Visualization");
        sb.AppendLine("#### Codebase Ownership Treemap");
        sb.AppendLine(SvgGeneratorHelper.GenerateGeTreemapSvg(result));
        sb.AppendLine();

        return sb.ToString();
    }

    public static string GenerateCustomHtml(AnalysisResult result, List<string> sections)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<html><head><style>body { font-family: sans-serif; padding: 20px; } h2 { color: #333; border-bottom: 1px solid #ccc; } svg { max-width: 100%; height: auto; }</style></head><body>");
        sb.AppendLine("<h1>Gitic Custom Report</h1>");
        sb.AppendLine($"<p><em>Generated at {DateTime.Now}</em></p>");

        if (result.CuratedReports != null)
        {
            if (sections.Contains("Work Classification"))
            {
                sb.AppendLine("<h2>1. Work Classification</h2><ul>");
                sb.AppendLine($"<li><strong>Features:</strong> {result.CuratedReports.WorkClassification.Features}</li>");
                sb.AppendLine($"<li><strong>Bugs:</strong> {result.CuratedReports.WorkClassification.Bugs}</li>");
                sb.AppendLine($"<li><strong>Technical Debt:</strong> {result.CuratedReports.WorkClassification.TechnicalDebt}</li>");
                sb.AppendLine($"<li><strong>Chores:</strong> {result.CuratedReports.WorkClassification.Chores}</li>");
                sb.AppendLine($"<li><strong>Unclassified:</strong> {result.CuratedReports.WorkClassification.Unclassified}</li>");
                sb.AppendLine("</ul>");
            }

            if (sections.Contains("Developer Onboarding"))
            {
                sb.AppendLine("<h2>2. Developer Onboarding (TTFC)</h2><ul>");
                foreach (var dev in result.CuratedReports.Onboarding.Take(10))
                {
                    sb.AppendLine($"<li><strong>{dev.Developer}</strong>: First commit on {dev.FirstCommitDate} ({dev.DaysActive} days active)</li>");
                }
                sb.AppendLine("</ul>");
            }

            if (sections.Contains("Code Rot"))
            {
                sb.AppendLine("<h2>3. Code Rot</h2><ul>");
                sb.AppendLine($"<li><strong>Zombie Files (&gt;1yr):</strong> {result.CuratedReports.CodeRot.ZombieFileCount}</li>");
                sb.AppendLine($"<li><strong>Zombie Lines (&gt;1yr):</strong> {result.CuratedReports.CodeRot.ZombieLines}</li>");
                sb.AppendLine("</ul>");
            }

            if (sections.Contains("Review Collaboration"))
            {
                sb.AppendLine("<h2>4. Review Collaboration & Silos</h2>");
                sb.AppendLine($"<p><strong>Reviewer Silos (Single-reviewer):</strong> {result.CuratedReports.ReviewCollaboration.ReviewerSilos}</p>");
                sb.AppendLine("<h3>Top Review Pairs</h3><ul>");
                foreach (var pair in result.CuratedReports.ReviewCollaboration.Pairs.Take(5))
                {
                    sb.AppendLine($"<li>{pair.Author} reviewed by {pair.Reviewer} ({pair.PrCount} PRs)</li>");
                }
                sb.AppendLine("</ul>");
            }

            if (sections.Contains("AI Code Strain"))
            {
                sb.AppendLine("<h2>5. AI Code Strain</h2><ul>");
                sb.AppendLine($"<li><strong>High-Volume Commits (&gt;20 files):</strong> {result.CuratedReports.AiCodeStrain.HighVolumeCommits}</li>");
                if (result.CuratedReports.AiCodeStrain.ReviewVelocityWarning)
                {
                    sb.AppendLine("<li style='color:red'><strong>⚠️ WARNING:</strong> High proportion of large commits detected. Review capacity may be strained.</li>");
                }
                sb.AppendLine("</ul>");
            }
        }

        sb.AppendLine("<hr/>");
        sb.AppendLine("<h3>Repository Visualization</h3>");
        sb.AppendLine("#### Codebase Ownership Treemap</h4>");
        sb.AppendLine("<div>" + SvgGeneratorHelper.GenerateGeTreemapSvg(result) + "</div>");
        sb.AppendLine("</body></html>");

        return sb.ToString();
    }
}
