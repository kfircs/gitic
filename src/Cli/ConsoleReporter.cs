using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kfc.Cli.Core;

namespace Gitic;

public static class ConsoleReporterExtensions
{
    public static void WriteErrorLine(this IConsoleReporter reporter, string message)
    {
        reporter.WriteError(message);
    }

    /// <summary>
    /// Writes a collection of diagnostics directly to standard error, automatically handling grouping, sorting, and quiet filtering.
    /// </summary>
    public static void WriteDiagnostics(this IConsoleReporter reporter, IEnumerable<Diagnostic> diagnostics, bool quiet = false)
    {
        if (diagnostics == null) return;

        var diagnosticsToShow = quiet
            ? diagnostics.Where(d => Warnings.GetSeverityOrder(d.Severity) == 1).ToList()
            : diagnostics.ToList();

        if (diagnosticsToShow.Count == 0) return;

        // Group by Severity using LINQ
        var groups = diagnosticsToShow
            .GroupBy(d => (d.Severity ?? "WARNING").ToUpperInvariant(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        // Sort the groups by severity order: Critical/Error/Failure first, then Warning, then others.
        List<string> orderedKeys = new(groups.Keys);
        orderedKeys.Sort((a, b) => Warnings.GetSeverityOrder(a).CompareTo(Warnings.GetSeverityOrder(b)));

        var sb = new StringBuilder();
        foreach (var key in orderedKeys)
        {
            string color = key == "ERROR" || key == "CRITICAL" ? "\x1b[38;2;243;139;168m" : "\x1b[38;2;249;226;175m";
            string icon = key == "ERROR" || key == "CRITICAL" ? "❌" : "⚠️";
            sb.AppendLine($"{color}[{key}] {icon}\x1b[0m");
            foreach (var diag in groups[key])
            {
                sb.AppendLine($"  {color}{diag.Code}\x1b[0m: {diag.Message}");
                if (!string.IsNullOrEmpty(diag.Hint))
                {
                    sb.AppendLine($"  \x1b[38;2;108;112;147m󰌑 Hint: {diag.Hint}\x1b[0m");
                }
            }
        }

        reporter.WriteError(sb.ToString());
    }

    /// <summary>
    /// Formats and writes an exclusion summary to standard error.
    /// </summary>
    public static void WriteExclusions(this IConsoleReporter reporter, IEnumerable<ExclusionSummary> exclusions)
    {
        if (exclusions == null) return;
        var list = exclusions.Where(e => e != null).ToList();
        if (list.Count == 0) return;

        var sb = new StringBuilder();
        sb.Append("\x1b[38;2;108;112;147m󰆧 exclusions ");
        var parts = list.Select(e => $"{e.Category}:{e.Count}");
        sb.Append(string.Join(", ", parts));
        sb.AppendLine("\x1b[0m");

        reporter.WriteError(sb.ToString());
    }
}
