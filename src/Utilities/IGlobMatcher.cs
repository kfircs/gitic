using System.Text.RegularExpressions;

namespace Gitic;

public interface IGlobMatcher
{
    bool MatchesPathPattern(string path, string pattern);
    bool MatchesTextPattern(string value, string pattern);
    Regex GlobToRegExp(string pattern);
}
