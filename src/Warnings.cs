using System;
using System.Collections.Generic;
using System.Linq;

namespace Gitic
{
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
    }

    public class EmailCollisionWarningRule : IWarningRule
    {
        public List<string> Collect(WarningContext context)
        {
            if ((context.AliasCount ?? 0) > 0)
            {
                return new List<string>();
            }

            if (context.EmailCollisions == null || context.EmailCollisions.Count == 0)
            {
                return new List<string>();
            }

            return context.EmailCollisions.Select(collision =>
            {
                string nameList = string.Join(", ", collision.Names);
                return $"Contributors {nameList} share email {collision.Email} but no alias is configured; they may be the same person. Add an alias in .gitic.yml (or legacy .gitizer.yml) or enable identity.merge_on_email to merge them.";
            }).ToList();
        }
    }

    public class BotConfigWarningRule : IWarningRule
    {
        public List<string> Collect(WarningContext context)
        {
            if (context.SafeConfiguredBotCount == 0 && (context.AutomationMetrics?.Count ?? 0) > 0)
            {
                return new List<string>
                {
                    $"No bots are explicitly configured; {context.AutomationMetrics?.Count ?? 0} automation identities were detected using default heuristics. Configure bots in .gitic.yml (or legacy .gitizer.yml) to control automation detection (e.g. workspace-specific agents like test harnesses)."
                };
            }
            return new List<string>();
        }
    }

    public class LeadTimeWarningRule : IWarningRule
    {
        public List<string> Collect(WarningContext context)
        {
            if (context.LeadTimes == null || context.LeadTimes.Merges.Count == 0)
            {
                return new List<string>
                {
                    "No merge commits in the analysis window; branch lead time is unmeasured. Run with --include-merges or widen the window to measure lead time."
                };
            }
            return new List<string>();
        }
    }

    public class NoBotsWarningRule : IWarningRule
    {
        public List<string> Collect(WarningContext context)
        {
            if (context.SafeConfiguredBotCount == 0 && (context.AutomationMetrics?.Count ?? 0) == 0)
            {
                return new List<string>
                {
                    "No bots are configured and no automation identities were detected. If this repository has CI or release bots, configure them in .gitic.yml (or legacy .gitizer.yml)."
                };
            }
            return new List<string>();
        }
    }

    public class TemporalCouplingWarningRule : IWarningRule
    {
        public List<string> Collect(WarningContext context)
        {
            if (context.TemporalCoupling == null)
            {
                return new List<string>();
            }
            if (context.TemporalCoupling.OversizedCommitCount > 0)
            {
                return new List<string>
                {
                    $"{context.TemporalCoupling.OversizedCommitCount} commit(s) changed more than {context.TemporalCoupling.Limit} files (max observed: {context.TemporalCoupling.MaxObservedFiles}) and were excluded from temporal coupling analysis. Configure metrics.temporal_coupling_max_commit_file_count in .gitic.yml (or legacy .gitizer.yml) to adjust."
                };
            }
            return new List<string>();
        }
    }

    public class GeneratedFileWarningRule : IWarningRule
    {
        private const int SingleTouchCount = 1;
        private const int HighChurnThreshold = 200;
        private const int SingleContributorCount = 1;
        private const double HighActivityShareThreshold = 0.99;

        public List<string> Collect(WarningContext context)
        {
            if (context.Files == null)
            {
                return new List<string>();
            }
            int suspiciousCount = context.Files.Count(IsSuspiciousFile);
            if (suspiciousCount > 0)
            {
                return new List<string>
                {
                    $"{suspiciousCount} file(s) have single-touch high churn (>{HighChurnThreshold} lines) with a single author. These may be generated files or scaffolding. Consider adding them to your .gitic.yml (or legacy .gitizer.yml) excludes."
                };
            }
            return new List<string>();
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
            var baseWarnings = existingWarnings ?? Enumerable.Empty<string>();
            var ruleWarnings = _rules.SelectMany(rule => rule.Collect(context));
            return baseWarnings.Concat(ruleWarnings).Distinct().OrderBy(w => w, StringComparer.Ordinal).ToList();
        }
    }
}
