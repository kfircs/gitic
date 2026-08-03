using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Gitic;

public interface ICommitClassifier
{
    string Classify(string message);
}

public record ClassificationRule(string Category, Regex MainRegex, Regex PrefixRegex)
{
    public bool Matches(string message) =>
        message != null && (MainRegex.IsMatch(message) || PrefixRegex.IsMatch(message));
}

public class CommitClassifier : ICommitClassifier
{
    private static readonly List<ClassificationRule> DefaultRules = new List<ClassificationRule>
    {
        new ClassificationRule(
            "bugfix",
            new Regex(@"(?:bug|fix|revert|issue|crash|error|prevent|problem|fail|correct|leak)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"^(?:fix|revert)(?:\(.+\))?:", RegexOptions.IgnoreCase | RegexOptions.Compiled)
        ),
        new ClassificationRule(
            "feature",
            new Regex(@"(?:feat|feature|add|implement|introduce)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"^feat(?:\(.+\))?:", RegexOptions.IgnoreCase | RegexOptions.Compiled)
        )
    };

    private readonly List<ClassificationRule> _rules;

    public CommitClassifier() => _rules = DefaultRules;

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
