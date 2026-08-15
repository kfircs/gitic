using System;
using System.Text.RegularExpressions;

namespace Gitic;

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
