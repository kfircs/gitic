using System;
using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;

namespace Gitic;

public interface IGlobMatcher
{
    bool MatchesPathPattern(string path, string pattern);
    bool MatchesTextPattern(string value, string pattern);
    Regex GlobToRegExp(string pattern);
}

public class CachedGlobMatcher : IGlobMatcher
{
    private readonly ConcurrentDictionary<string, Regex> _regexCache = new();

    private static bool IsGlobPattern(string pattern) => pattern.Contains('*') || pattern.Contains('?');

    public bool MatchesPathPattern(string path, string pattern)
    {
        string normalizedPath = PathUtils.NormalizeGitPath(path);
        string normalizedPattern = PathUtils.NormalizeGitPath(pattern).Trim('/');
        if (normalizedPattern.Length == 0)
        {
            return false;
        }
        if (IsGlobPattern(normalizedPattern))
        {
            return GlobToRegExp(normalizedPattern).IsMatch(normalizedPath);
        }
        return normalizedPath == normalizedPattern || normalizedPath.StartsWith(normalizedPattern + "/");
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

public static class PathUtils
{
    /// <summary>
    /// The minimum length of a path (in characters) required to perform split-truncation.
    /// If the target maximum length is less than or equal to this threshold, we fallback 
    /// to simple suffix truncation (taking characters only from the end of the path) because 
    /// there is not enough space to show a start segment, an ellipsis, and an end segment.
    /// </summary>
    private const int MinLengthForSplitting = 5;

    /// <summary>
    /// The ellipsis string sequence appended or inserted to indicate path truncation.
    /// </summary>
    private const string Ellipsis = "...";

    /// <summary>
    /// The character length of the ellipsis sequence. This must match the actual length of <see cref="Ellipsis"/>.
    /// </summary>
    private const int EllipsisLength = 3;

    private static readonly IGlobMatcher _matcher = new CachedGlobMatcher();

    public static string NormalizeGitPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }

        string normalized = path.Replace('\\', '/');
        if (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }
        normalized = normalized.TrimStart('/');
        return normalized;
    }

    public static bool MatchesPathPattern(string path, string pattern)
    {
        return _matcher.MatchesPathPattern(path, pattern);
    }

    public static bool MatchesTextPattern(string value, string pattern)
    {
        return _matcher.MatchesTextPattern(value, pattern);
    }

    public static Regex GlobToRegExp(string pattern)
    {
        return _matcher.GlobToRegExp(pattern);
    }

    public static string TruncatePath(string path, int maxLength)
    {
        if (string.IsNullOrEmpty(path) || path.Length <= maxLength)
        {
            return path;
        }

        if (maxLength <= MinLengthForSplitting)
        {
            return path[^maxLength..];
        }

        int keepEnd = maxLength / 2;
        int keepStart = maxLength - keepEnd - EllipsisLength;

        if (keepStart <= 0)
        {
            return Ellipsis + path[^(maxLength - EllipsisLength)..];
        }

        return path[..keepStart] + Ellipsis + path[^keepEnd..];
    }
}
