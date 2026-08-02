using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gitic;

public interface IConsoleReporter
{
    void Write(string message);
    void WriteLine(string message);
    void WriteError(string message);
    void WriteErrorLine(string message);

    /// <summary>
    /// Writes a collection of diagnostics directly to standard error, automatically handling grouping, sorting, and quiet filtering.
    /// </summary>
    void WriteDiagnostics(IEnumerable<Diagnostic> diagnostics, bool quiet = false)
    {
        if (diagnostics == null) return;

        var diagnosticsToShow = quiet
            ? diagnostics.Where(d => Warnings.GetSeverityOrder(d.Severity) == 1).ToList()
            : diagnostics.ToList();

        if (diagnosticsToShow.Count == 0) return;

        // Group by Severity
        Dictionary<string, List<Diagnostic>> groups = new(StringComparer.OrdinalIgnoreCase);
        foreach (var d in diagnosticsToShow)
        {
            string severity = (d.Severity ?? "WARNING").ToUpperInvariant();
            if (!groups.ContainsKey(severity))
            {
                groups[severity] = [];
            }
            groups[severity].Add(d);
        }

        // Sort the groups by severity order: Critical/Error/Failure first, then Warning, then others.
        List<string> orderedKeys = new(groups.Keys);
        orderedKeys.Sort((a, b) => Warnings.GetSeverityOrder(a).CompareTo(Warnings.GetSeverityOrder(b)));

        var sb = new StringBuilder();
        foreach (var key in orderedKeys)
        {
            sb.AppendLine($"[{key}]");
            foreach (var diag in groups[key])
            {
                sb.AppendLine($"  {diag.Code}: {diag.Message}");
                if (!string.IsNullOrEmpty(diag.Hint))
                {
                    sb.AppendLine($"  Hint: {diag.Hint}");
                }
            }
        }

        WriteError(sb.ToString());
    }

    /// <summary>
    /// Formats and writes an exclusion summary to standard error.
    /// </summary>
    void WriteExclusions(IEnumerable<ExclusionSummary> exclusions)
    {
        if (exclusions == null) return;
        List<ExclusionSummary> list = [];
        foreach (var e in exclusions)
        {
            if (e != null) list.Add(e);
        }
        if (list.Count == 0) return;

        var sb = new StringBuilder();
        sb.Append("exclusions ");
        List<string> parts = [];
        foreach (var e in list)
        {
            parts.Add($"{e.Category}:{e.Count}");
        }
        sb.Append(string.Join(", ", parts));
        sb.AppendLine();

        WriteError(sb.ToString());
    }
}

public class ConsoleReporter : IConsoleReporter
{
    public void Write(string message)
    {
        Console.Write(message);
    }

    public void WriteLine(string message)
    {
        Console.WriteLine(message);
    }

    public void WriteError(string message)
    {
        Console.Error.Write(message);
    }

    public void WriteErrorLine(string message)
    {
        Console.Error.WriteLine(message);
    }
}
