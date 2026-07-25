using System;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Gitic
{
    public static class PathUtils
    {
        private static readonly ConcurrentDictionary<string, Regex> _regexCache = new ConcurrentDictionary<string, Regex>();

        public static string NormalizeGitPath(string path)
        {
            string normalized = path.Replace("\\", "/");
            if (normalized.StartsWith("./"))
            {
                normalized = normalized.Substring(2);
            }
            while (normalized.StartsWith("/"))
            {
                normalized = normalized.Substring(1);
            }
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
                string source = "^";
                for (int index = 0; index < p.Length; index += 1)
                {
                    char c = p[index];
                    if (c == '*' && index + 1 < p.Length && p[index + 1] == '*')
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
                return new Regex(source, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            });
        }
    }
}
