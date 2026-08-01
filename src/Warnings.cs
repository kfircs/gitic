using System;
using System.Collections.Generic;
using System.Linq;

namespace Gitic
{
    public static class Warnings
    {
        public static int GetSeverityOrder(string severity)
        {
            if (severity == null) return 3;
            return severity.ToLowerInvariant() switch
            {
                "critical" or "error" or "failure" => 1,
                "warning" => 2,
                _ => 3
            };
        }
    }

    public class WarningContext
    {
        public List<EmailCollision>? EmailCollisions { get; set; }
        public int? AliasCount { get; set; }
        public int? ConfiguredBotCount { get; set; }
        public List<AutomationMetric>? AutomationMetrics { get; set; }
        public LeadTimesInfo? LeadTimes { get; set; }
        public TemporalCouplingResult? TemporalCoupling { get; set; }
        public List<FileMetric>? Files { get; set; }

        public int SafeConfiguredBotCount => ConfiguredBotCount ?? 0;
    }

    public interface IWarningRule
    {
        List<string> Collect(WarningContext context);
        List<Diagnostic> CollectDiagnostics(WarningContext context);
    }

    public abstract class WarningRuleBase : IWarningRule
    {
        public virtual List<string> Collect(WarningContext context)
        {
            return CollectDiagnostics(context).Select(d => d.ToString()).ToList();
        }

        public abstract List<Diagnostic> CollectDiagnostics(WarningContext context);
    }

    public class EmailCollisionWarningRule : WarningRuleBase
    {
        public override List<Diagnostic> CollectDiagnostics(WarningContext context)
        {
            if (context.AliasCount > 0)
            {
                return new List<Diagnostic>();
            }

            if (context.EmailCollisions == null || context.EmailCollisions.Count == 0)
            {
                return new List<Diagnostic>();
            }

            return context.EmailCollisions.Select(collision =>
            {
                string nameList = string.Join(", ", collision.Names);
                return new Diagnostic
                {
                    Code = "GITIC001",
                    Severity = "Warning",
                    Message = $"Contributors {nameList} share email {collision.Email} but no alias is configured; they may be the same person.",
                    Hint = "Add an alias in .gitic.yml (or legacy .gitizer.yml) or enable identity.merge_on_email to merge them."
                };
            }).ToList();
        }
    }

    public class BotConfigWarningRule : WarningRuleBase
    {
        public override List<Diagnostic> CollectDiagnostics(WarningContext context)
        {
            if (context.SafeConfiguredBotCount == 0 && (context.AutomationMetrics?.Count ?? 0) > 0)
            {
                return new List<Diagnostic>
                {
                    new Diagnostic
                    {
                        Code = "GITIC002",
                        Severity = "Warning",
                        Message = $"No bots are explicitly configured; {context.AutomationMetrics?.Count ?? 0} automation identities were detected using default heuristics.",
                        Hint = "Configure bots in .gitic.yml (or legacy .gitizer.yml) to control automation detection (e.g. workspace-specific agents like test harnesses)."
                    }
                };
            }
            return new List<Diagnostic>();
        }
    }

    public class LeadTimeWarningRule : WarningRuleBase
    {
        public override List<Diagnostic> CollectDiagnostics(WarningContext context)
        {
            if (context.LeadTimes == null || context.LeadTimes.Merges.Count == 0)
            {
                return new List<Diagnostic>
                {
                    new Diagnostic
                    {
                        Code = "GITIC003",
                        Severity = "Warning",
                        Message = "No merge commits in the analysis window; branch lead time is unmeasured.",
                        Hint = "Run with --include-merges or widen the window to measure lead time."
                    }
                };
            }
            return new List<Diagnostic>();
        }
    }

    public class NoBotsWarningRule : WarningRuleBase
    {
        public override List<Diagnostic> CollectDiagnostics(WarningContext context)
        {
            if (context.SafeConfiguredBotCount == 0 && (context.AutomationMetrics?.Count ?? 0) == 0)
            {
                return new List<Diagnostic>
                {
                    new Diagnostic
                    {
                        Code = "GITIC004",
                        Severity = "Warning",
                        Message = "No bots are configured and no automation identities were detected.",
                        Hint = "If this repository has CI or release bots, configure them in .gitic.yml (or legacy .gitizer.yml)."
                    }
                };
            }
            return new List<Diagnostic>();
        }
    }

    public class TemporalCouplingWarningRule : WarningRuleBase
    {
        public override List<Diagnostic> CollectDiagnostics(WarningContext context)
        {
            if (context.TemporalCoupling == null)
            {
                return new List<Diagnostic>();
            }
            if (context.TemporalCoupling.OversizedCommitCount > 0)
            {
                return new List<Diagnostic>
                {
                    new Diagnostic
                    {
                        Code = "GITIC005",
                        Severity = "Warning",
                        Message = $"{context.TemporalCoupling.OversizedCommitCount} commit(s) changed more than {context.TemporalCoupling.Limit} files (max observed: {context.TemporalCoupling.MaxObservedFiles}) and were excluded from temporal coupling analysis.",
                        Hint = "Configure metrics.temporal_coupling_max_commit_file_count in .gitic.yml (or legacy .gitizer.yml) to adjust."
                    }
                };
            }
            return new List<Diagnostic>();
        }
    }

    public class GeneratedFileWarningRule : WarningRuleBase
    {
        private const int SingleTouchCount = 1;
        private const int HighChurnThreshold = 200;
        private const int SingleContributorCount = 1;
        private const double HighActivityShareThreshold = 0.99;

        public override List<Diagnostic> CollectDiagnostics(WarningContext context)
        {
            if (context.Files == null)
            {
                return new List<Diagnostic>();
            }
            int suspiciousCount = context.Files.Count(IsSuspiciousFile);
            if (suspiciousCount > 0)
            {
                return new List<Diagnostic>
                {
                    new Diagnostic
                    {
                        Code = "GITIC006",
                        Severity = "Warning",
                        Message = $"{suspiciousCount} file(s) have single-touch high churn (>{HighChurnThreshold} lines) with a single author. These may be generated files or scaffolding.",
                        Hint = "Consider adding them to your .gitic.yml (or legacy .gitizer.yml) excludes."
                    }
                };
            }
            return new List<Diagnostic>();
        }

        private bool IsSuspiciousFile(FileMetric file)
        {
            return file.Touches == SingleTouchCount &&
                   file.Churn > HighChurnThreshold &&
                   file.Contributors.Count == SingleContributorCount &&
                   file.Contributors[0].ActivityShare >= HighActivityShareThreshold;
        }
    }

    public interface IWarningRuleProvider
    {
        List<IWarningRule> GetRules();
    }

    public class DefaultWarningRuleProvider : IWarningRuleProvider
    {
        public List<IWarningRule> GetRules()
        {
            return new List<IWarningRule>
            {
                new EmailCollisionWarningRule(),
                new BotConfigWarningRule(),
                new LeadTimeWarningRule(),
                new NoBotsWarningRule(),
                new TemporalCouplingWarningRule(),
                new GeneratedFileWarningRule()
            };
        }
    }

    public interface IWarningCollector
    {
        List<string> Collect(WarningContext context);
        List<string> Collect(WarningContext context, List<string>? existingWarnings);
        List<Diagnostic> CollectDiagnostics(WarningContext context);
        List<Diagnostic> CollectDiagnostics(WarningContext context, List<string>? existingWarnings);

        /// <summary>
        /// Collects diagnostics and immediately reports them via the provided console reporter, handling quiet filtering, sorting, and formatting.
        /// </summary>
        void CollectAndReport(WarningContext context, IConsoleReporter reporter, List<string>? existingWarnings = null, bool quiet = false)
        {
            if (reporter == null) return;
            var diagnostics = CollectDiagnostics(context, existingWarnings);
            reporter.WriteDiagnostics(diagnostics, quiet);
        }
    }

    public class WarningCollector : IWarningCollector
    {
        private readonly List<IWarningRule> _rules;

        public WarningCollector(IWarningRuleProvider? ruleProvider = null)
        {
            var provider = ruleProvider ?? new DefaultWarningRuleProvider();
            _rules = provider.GetRules();
        }

        public List<string> Collect(WarningContext context)
        {
            return Collect(context, null);
        }

        public List<string> Collect(WarningContext context, List<string>? existingWarnings)
        {
            return CollectDiagnostics(context, existingWarnings).Select(d => d.ToString()).ToList();
        }

        public List<Diagnostic> CollectDiagnostics(WarningContext context)
        {
            return CollectDiagnostics(context, null);
        }

        public List<Diagnostic> CollectDiagnostics(WarningContext context, List<string>? existingWarnings)
        {
            var list = new List<Diagnostic>();

            if (existingWarnings != null)
            {
                foreach (var warning in existingWarnings)
                {
                    list.Add(ParseOrWrapWarning(warning));
                }
            }

            foreach (var rule in _rules)
            {
                list.AddRange(rule.CollectDiagnostics(context));
            }

            return list
                .GroupBy(d => new { d.Code, d.Message })
                .Select(g => g.First())
                .OrderBy(d => Warnings.GetSeverityOrder(d.Severity))
                .ThenBy(d => d.Code)
                .ThenBy(d => d.Message)
                .ToList();
        }

        public void CollectAndReport(WarningContext context, IConsoleReporter reporter, List<string>? existingWarnings = null, bool quiet = false)
        {
            if (reporter == null) return;
            var diagnostics = CollectDiagnostics(context, existingWarnings);
            reporter.WriteDiagnostics(diagnostics, quiet);
        }

        private Diagnostic ParseOrWrapWarning(string warning)
        {
            if (string.IsNullOrEmpty(warning))
            {
                return new Diagnostic
                {
                    Code = "GITIC999",
                    Severity = "Warning",
                    Message = string.Empty
                };
            }

            if (warning.Contains("matched multiple configured areas"))
            {
                int idx = warning.IndexOf("; using ");
                if (idx >= 0)
                {
                    return new Diagnostic
                    {
                        Code = "GITIC007",
                        Severity = "Warning",
                        Message = warning.Substring(0, idx),
                        Hint = warning.Substring(idx + 2) // "using ..."
                    };
                }
                return new Diagnostic
                {
                    Code = "GITIC007",
                    Severity = "Warning",
                    Message = warning,
                    Hint = "Adjust area path patterns in .gitic.yml (or legacy .gitizer.yml) to avoid overlapping patterns."
                };
            }

            return new Diagnostic
            {
                Code = "GITIC999",
                Severity = "Warning",
                Message = warning
            };
        }
    }
}
