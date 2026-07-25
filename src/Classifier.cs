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

    public interface ICommitClassifier
    {
        string Classify(string message);
    }

    public class CommitClassifier : ICommitClassifier
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

        private readonly Regex _bugFixPattern;
        private readonly Regex _bugFixPrefixPattern;

        public BugfixStrategy(string? bugFixPattern = null, string? bugFixPrefixPattern = null)
        {
            _bugFixPattern = new Regex(
                bugFixPattern ?? @"(?:bug|fix|revert|issue|crash|error|prevent|problem|fail|correct|leak)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            _bugFixPrefixPattern = new Regex(
                bugFixPrefixPattern ?? @"^(?:fix|revert)(?:\(.+\))?:",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }

        public bool Matches(string message)
        {
            return _bugFixPattern.IsMatch(message) || _bugFixPrefixPattern.IsMatch(message);
        }
    }

    public class FeatureStrategy : IClassifierStrategy
    {
        public string Category => "feature";

        private readonly Regex _featurePattern;
        private readonly Regex _featurePrefixPattern;

        public FeatureStrategy(string? featurePattern = null, string? featurePrefixPattern = null)
        {
            _featurePattern = new Regex(
                featurePattern ?? @"(?:feat|feature|add|implement|introduce)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            _featurePrefixPattern = new Regex(
                featurePrefixPattern ?? @"^feat(?:\(.+\))?:",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }

        public bool Matches(string message)
        {
            return _featurePattern.IsMatch(message) || _featurePrefixPattern.IsMatch(message);
        }
    }
}
