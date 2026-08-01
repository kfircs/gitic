using System;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Gitic
{
    public interface IGlobMatcher
    {
        bool MatchesPathPattern(string path, string pattern);
        bool MatchesTextPattern(string value, string pattern);
    }

    public class CachedGlobMatcher : IGlobMatcher
    {
        private readonly ConcurrentDictionary<string, Regex> _regexCache = new ConcurrentDictionary<string, Regex>();

        public bool MatchesPathPattern(string path, string pattern)
        {
            string normalizedPath = PathUtils.NormalizeGitPath(path);
            string normalizedPattern = PathUtils.NormalizeGitPath(pattern).TrimStart('/').TrimEnd('/');
            if (normalizedPattern.Length == 0)
            {
                return false;
            }
            if (normalizedPattern.Contains('*') || normalizedPattern.Contains('?'))
            {
                return GlobToRegExp(normalizedPattern).IsMatch(normalizedPath);
            }
            return normalizedPath == normalizedPattern || normalizedPath.StartsWith(normalizedPattern + "/");
        }

        public bool MatchesTextPattern(string value, string pattern)
        {
            if (pattern.Contains('*') || pattern.Contains('?'))
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
                return new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            });
        }

        private static string ConvertGlobToRegexPattern(string pattern)
        {
            string source = "^";
            for (int index = 0; index < pattern.Length; index += 1)
            {
                char c = pattern[index];
                if (c == '*' && index + 1 < pattern.Length && pattern[index + 1] == '*')
                {
                    source += ".*";
                    index += 1;
                    continue;
                }
                if (c == '*')
                {
                    source += "[^/]*";
                    continue;
                }
                if (c == '?')
                {
                    source += "[^/]";
                    continue;
                }
                source += Regex.Escape(c.ToString());
            }
            source += "$";
            return source;
        }
    }

    public static class PathUtils
    {
        private const int MinLengthForSplitting = 5;
        private const int EllipsisLength = 3;
        private const string Ellipsis = "...";

        private static IGlobMatcher _matcher = new CachedGlobMatcher();

        public static void SetMatcher(IGlobMatcher matcher)
        {
            _matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
        }

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
            if (_matcher is CachedGlobMatcher cached)
            {
                return cached.GlobToRegExp(pattern);
            }
            return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }

        public static string TruncatePath(string path, int maxLength)
        {
            if (string.IsNullOrEmpty(path) || path.Length <= maxLength)
            {
                return path;
            }

            if (maxLength <= MinLengthForSplitting)
            {
                return path.Substring(path.Length - maxLength);
            }

            int keepEnd = maxLength / 2;
            int keepStart = maxLength - keepEnd - EllipsisLength;

            if (keepStart <= 0)
            {
                return Ellipsis + path.Substring(path.Length - (maxLength - EllipsisLength));
            }

            return path.Substring(0, keepStart) + Ellipsis + path.Substring(path.Length - keepEnd);
        }
    }
}
