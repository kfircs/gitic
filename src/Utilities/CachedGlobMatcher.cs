using System;
using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;

namespace Gitic;

public class CachedGlobMatcher : IGlobMatcher
{
    private readonly ConcurrentDictionary<string, Regex> _regexCache = new();

    private static bool IsGlobPattern(string pattern) => pattern.Contains('*') || pattern.Contains('?');

    public bool MatchesPathPattern(string path, string pattern)
    {
        string normalizedPath = PathUtils.NormalizeGitPath(path);
        string normalizedPattern = PathUtils.NormalizeGitPath(pattern).Trim('/');
        if (string.IsNullOrEmpty(normalizedPattern))
        {
            return false;
        }
        if (IsGlobPattern(normalizedPattern))
        {
            return GlobToRegExp(normalizedPattern).IsMatch(normalizedPath);
        }
        return normalizedPath == normalizedPattern || normalizedPath.StartsWith(normalizedPattern + "/", StringComparison.Ordinal);
    }

    public bool MatchesTextPattern(string value, string pattern)
    {
        if (IsGlobPattern(pattern))
        {
            return GlobToRegExp(pattern).IsMatch(value);
        }
        return value.Contains(pattern);
    }

    public Regex GlobToRegExp(string pattern)
    {
        return _regexCache.GetOrAdd(pattern, p =>
        {
            string regexPattern = ConvertGlobToRegexPattern(p);
            return new(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        });
    }

    /// <summary>
    /// Converts a glob pattern (e.g., "src/**/*.cs" or "*.json") into an equivalent, compiled-ready regular expression pattern.
    /// </summary>
    /// <param name="pattern">The glob pattern to convert.</param>
    /// <returns>A regular expression string representation of the glob pattern.</returns>
    private static string ConvertGlobToRegexPattern(string pattern)
    {
        var sb = new StringBuilder("^");
        int index = 0;
        int length = pattern.Length;

        while (index < length)
        {
            char current = pattern[index];

            // Handle double wildcards (**), which match recursively across directories
            if (current == '*' && index + 1 < length && pattern[index + 1] == '*')
            {
                sb.Append(".*");
                index += 2;
            }
            else
            {
                // Handle single character wildcards, single directory wildcards, and literals
                string substitution = current switch
                {
                    '*' => "[^/]*",
                    '?' => "[^/]",
                    _ => Regex.Escape(current.ToString())
                };
                sb.Append(substitution);
                index++;
            }
        }

        sb.Append('$');
        return sb.ToString();
    }
}
