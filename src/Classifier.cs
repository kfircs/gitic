using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Gitic
{
    public interface IClassifierStrategy
    {
        string Category { get; }
        bool Matches(string message);
    }

    public class CommitClassifier
    {
        private readonly List<IClassifierStrategy> _strategies;

        public CommitClassifier(List<IClassifierStrategy>? strategies = null)
        {
            _strategies = strategies ?? new List<IClassifierStrategy>
            {
                new BugfixStrategy(),
                new FeatureStrategy()
            };
        }

        public string Classify(string message)
        {
            foreach (var strategy in _strategies)
            {
                if (strategy.Matches(message))
                {
                    return strategy.Category;
                }
            }
            return "other";
        }
    }

    public class BugfixStrategy : IClassifierStrategy
    {
        public string Category => "bugfix";

        private static readonly Regex BugFixPattern = new(
            @"(?:bug|fix|revert|issue|crash|error|prevent|problem|fail|correct|leak)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex BugFixPrefixPattern = new(
            @"^(?:fix|revert)(?:\(.+\))?:",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public bool Matches(string message)
        {
            return BugFixPattern.IsMatch(message) || BugFixPrefixPattern.IsMatch(message);
        }
    }

    public class FeatureStrategy : IClassifierStrategy
    {
        public string Category => "feature";

        private static readonly Regex FeaturePattern = new(
            @"(?:feat|feature|add|implement|introduce)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex FeaturePrefixPattern = new(
            @"^feat(?:\(.+\))?:",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public bool Matches(string message)
        {
            return FeaturePattern.IsMatch(message) || FeaturePrefixPattern.IsMatch(message);
        }
    }
}
