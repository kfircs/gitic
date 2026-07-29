using System;
using System.Collections.Generic;
using System.Text;

namespace Gitic
{
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

            var diagnosticsToShow = new List<Diagnostic>();
            foreach (var d in diagnostics)
            {
                if (quiet)
                {
                    if (string.Equals(d.Severity, "Critical", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(d.Severity, "Error", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(d.Severity, "Failure", StringComparison.OrdinalIgnoreCase))
                    {
                        diagnosticsToShow.Add(d);
                    }
                }
                else
                {
                    diagnosticsToShow.Add(d);
                }
            }

            if (diagnosticsToShow.Count == 0) return;

            // Group by Severity
            var groups = new Dictionary<string, List<Diagnostic>>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in diagnosticsToShow)
            {
                string severity = (d.Severity ?? "WARNING").ToUpperInvariant();
                if (!groups.ContainsKey(severity))
                {
                    groups[severity] = new List<Diagnostic>();
                }
                groups[severity].Add(d);
            }

            // Sort the groups by severity order: Critical/Error/Failure first, then Warning, then others.
            var orderedKeys = new List<string>(groups.Keys);
            orderedKeys.Sort((a, b) => GetSeverityOrder(a).CompareTo(GetSeverityOrder(b)));

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
            var list = new List<ExclusionSummary>();
            foreach (var e in exclusions)
            {
                if (e != null) list.Add(e);
            }
            if (list.Count == 0) return;

            var sb = new StringBuilder();
            sb.Append("exclusions ");
            var parts = new List<string>();
            foreach (var e in list)
            {
                parts.Add($"{e.Category}:{e.Count}");
            }
            sb.Append(string.Join(", ", parts));
            sb.AppendLine();

            WriteError(sb.ToString());
        }

        private static int GetSeverityOrder(string severity)
        {
            if (string.Equals(severity, "Critical", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(severity, "Error", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(severity, "Failure", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }
            if (string.Equals(severity, "Warning", StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }
            return 3;
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

        public void WriteDiagnostics(IEnumerable<Diagnostic> diagnostics, bool quiet = false)
        {
            if (diagnostics == null) return;

            var diagnosticsToShow = new List<Diagnostic>();
            foreach (var d in diagnostics)
            {
                if (quiet)
                {
                    if (string.Equals(d.Severity, "Critical", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(d.Severity, "Error", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(d.Severity, "Failure", StringComparison.OrdinalIgnoreCase))
                    {
                        diagnosticsToShow.Add(d);
                    }
                }
                else
                {
                    diagnosticsToShow.Add(d);
                }
            }

            if (diagnosticsToShow.Count == 0) return;

            var groups = new Dictionary<string, List<Diagnostic>>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in diagnosticsToShow)
            {
                string severity = (d.Severity ?? "WARNING").ToUpperInvariant();
                if (!groups.ContainsKey(severity))
                {
                    groups[severity] = new List<Diagnostic>();
                }
                groups[severity].Add(d);
            }

            var orderedKeys = new List<string>(groups.Keys);
            orderedKeys.Sort((a, b) => GetSeverityOrder(a).CompareTo(GetSeverityOrder(b)));

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

        public void WriteExclusions(IEnumerable<ExclusionSummary> exclusions)
        {
            if (exclusions == null) return;
            var list = new List<ExclusionSummary>();
            foreach (var e in exclusions)
            {
                if (e != null) list.Add(e);
            }
            if (list.Count == 0) return;

            var sb = new StringBuilder();
            sb.Append("exclusions ");
            var parts = new List<string>();
            foreach (var e in list)
            {
                parts.Add($"{e.Category}:{e.Count}");
            }
            sb.Append(string.Join(", ", parts));
            sb.AppendLine();

            WriteError(sb.ToString());
        }

        private static int GetSeverityOrder(string severity)
        {
            if (string.Equals(severity, "Critical", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(severity, "Error", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(severity, "Failure", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }
            if (string.Equals(severity, "Warning", StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }
            return 3;
        }
    }
}
