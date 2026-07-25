using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Gitic
{
    public interface ICommitClassifier
    {
        string Classify(string message);
    }

    public class CommitClassifier : ICommitClassifier
    {
        public class ClassificationRule
        {
            public string Category { get; }
            public Regex MainRegex { get; }
            public Regex PrefixRegex { get; }

            public ClassificationRule(string category, string mainPattern, string prefixPattern)
            {
                Category = category ?? throw new ArgumentNullException(nameof(category));
                MainRegex = new Regex(mainPattern ?? throw new ArgumentNullException(nameof(mainPattern)), RegexOptions.IgnoreCase | RegexOptions.Compiled);
                PrefixRegex = new Regex(prefixPattern ?? throw new ArgumentNullException(nameof(prefixPattern)), RegexOptions.IgnoreCase | RegexOptions.Compiled);
            }

            public ClassificationRule(string category, Regex mainRegex, Regex prefixRegex)
            {
                Category = category ?? throw new ArgumentNullException(nameof(category));
                MainRegex = mainRegex ?? throw new ArgumentNullException(nameof(mainRegex));
                PrefixRegex = prefixRegex ?? throw new ArgumentNullException(nameof(prefixRegex));
            }

            public bool Matches(string message)
            {
                if (message == null) return false;
                return MainRegex.IsMatch(message) || PrefixRegex.IsMatch(message);
            }
        }

        private static readonly List<ClassificationRule> DefaultRules = new List<ClassificationRule>
        {
            new ClassificationRule(
                "bugfix",
                @"(?:bug|fix|revert|issue|crash|error|prevent|problem|fail|correct|leak)",
                @"^(?:fix|revert)(?:\(.+\))?:"
            ),
            new ClassificationRule(
                "feature",
                @"(?:feat|feature|add|implement|introduce)",
                @"^feat(?:\(.+\))?:"
            )
        };

        private readonly List<ClassificationRule> _rules;

        public CommitClassifier()
        {
            _rules = DefaultRules;
        }

        public CommitClassifier(IEnumerable<ClassificationRule> rules)
        {
            _rules = new List<ClassificationRule>(rules ?? throw new ArgumentNullException(nameof(rules)));
        }

        public string Classify(string message)
        {
            if (message == null) return "other";
            foreach (var rule in _rules)
            {
                if (rule.Matches(message))
                {
                    return rule.Category;
                }
            }
            return "other";
        }
    }
}
