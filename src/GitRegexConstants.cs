using System.Text.RegularExpressions;

namespace Gitic
{
    /// <summary>
    /// Centralized regular expressions used across the Git parsing module.
    /// Consolidated to support robust extraction while keeping patterns cleanly organized.
    /// </summary>
    public static class GitRegexConstants
    {
        /// <summary>
        /// Matches target paths in unified diff headers (e.g., ' b/src/main.cs').
        /// </summary>
        public static readonly Regex DiffGitRegex = new(@" b/(.*)$", RegexOptions.Compiled);

        /// <summary>
        /// Matches unified diff hunk headers to extract context symbol information.
        /// </summary>
        public static readonly Regex HunkHeaderRegex = new(@"^@@\s+-\d+(?:,\d+)?\s+\+\d+(?:,\d+)?\s+@@\s*(.*)$", RegexOptions.Compiled);

        /// <summary>
        /// Matches typical module/namespace imports or requirements that should be excluded from extracted symbols.
        /// </summary>
        public static readonly Regex ImportExcludeRegex = new(@"^(import|require|using|export\s+\*\s+from)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Matches semicolons and trailing whitespace at the end of a line.
        /// </summary>
        public static readonly Regex SemicolonSuffixRegex = new(@";\s*$", RegexOptions.Compiled);

        /// <summary>
        /// Matches brackets, parentheses, or brace characters at the end of a line.
        /// </summary>
        public static readonly Regex BracketsSuffixRegex = new(@"\s*[\{\(\[]\s*$", RegexOptions.Compiled);

        /// <summary>
        /// Matches 'Co-authored-by: Name <email@example.com>' git trailer lines.
        /// </summary>
        public static readonly Regex CoAuthoredByRegex = new(@"^Co-authored-by:\s*(.*?)\s*<([^>]+)>", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

        /// <summary>
        /// Matches braced git rename syntax in file paths (e.g., 'src/{utils => main}.cs').
        /// </summary>
        public static readonly Regex BraceRenameRegex = new(@"\{.*? => (.*?)\}", RegexOptions.Compiled);
    }
}
