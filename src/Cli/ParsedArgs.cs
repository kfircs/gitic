namespace Gitic;

public class ParsedArgs
{
    public string Command { get; init; } = string.Empty;
    public string RepoPath { get; init; } = ".";
    public AnalysisSettings Settings { get; init; } = new();
    public string? ContributorName { get; init; }
    public string? HtmlPath { get; init; }
    public string? MdPath { get; init; }
    public string? SvgPath { get; init; }
    public string? ConfigAction { get; init; }
    public string? HelpText { get; init; }
}
