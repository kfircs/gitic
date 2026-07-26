using System;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Gitic
{
    public static class PathUtils
    {
        private static readonly ConcurrentDictionary<string, Regex> _regexCache = new ConcurrentDictionary<string, Regex>();

        public static string NormalizeGitPath(string? path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            string normalized = path.Replace('\\', '/');
            if (normalized.StartsWith("./", StringComparison.Ordinal))
            {
                normalized = normalized.Substring(2);
            }
            normalized = normalized.TrimStart('/');
            return normalized;
        }

        public static bool MatchesPathPattern(string path, string pattern)
        {
            string normalizedPath = NormalizeGitPath(path);
            string normalizedPattern = NormalizeGitPath(pattern).TrimStart('/').TrimEnd('/');
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

        public static bool MatchesTextPattern(string value, string pattern)
        {
            if (pattern.Contains('*') || pattern.Contains('?'))
            {
                return GlobToRegExp(pattern).IsMatch(value);
            }
            return value.Contains(pattern);
        }

        public static Regex GlobToRegExp(string pattern)
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
}
