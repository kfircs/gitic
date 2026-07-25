using System.Text.RegularExpressions;

namespace Gitic
{
    public static class GitRegexConstants
    {
        public static readonly Regex DiffGitRegex = new(@" b/(.*)$", RegexOptions.Compiled);
        public static readonly Regex HunkHeaderRegex = new(@"^@@\s+-\d+(?:,\d+)?\s+\+\d+(?:,\d+)?\s+@@\s*(.*)$", RegexOptions.Compiled);
        public static readonly Regex ImportExcludeRegex = new(@"^(import|require|using|export\s+\*\s+from)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        public static readonly Regex SemicolonSuffixRegex = new(@";\s*$", RegexOptions.Compiled);
        public static readonly Regex BracketsSuffixRegex = new(@"\s*[\{\(\[]\s*$", RegexOptions.Compiled);
        public static readonly Regex CoAuthoredByRegex = new(@"^Co-authored-by:\s*(.*?)\s*<([^>]+)>", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);
    }
}
