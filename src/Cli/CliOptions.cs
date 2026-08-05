using System;
using System.CommandLine;

namespace Gitic;

/// <summary>
/// Houses and configures all CommandLine Option and Argument definitions,
/// separating options construction from the main argument parsing logic.
/// </summary>
public class CliOptions
{
    public Option<string> ConfigOption { get; } = new("--config") { Description = "Path to non-default configuration file", Recursive = true };
    public Option<string> UserConfigOption { get; } = new("--user-config") { Description = "Path to non-default global user configuration file", Recursive = true };
    public Option<bool> JsonOption { get; } = new("--json") { Description = "Output results in raw JSON format", Recursive = true };
    
    public Option<string> FormatOption { get; } = new("--format") 
    { 
        Description = "Output format: human, plain, json", 
        DefaultValueFactory = _ => "human",
        Recursive = true 
    };
    
    public Option<string> ColorOption { get; } = new("--color") 
    { 
        Description = "Color mode: auto, always, never", 
        DefaultValueFactory = _ => "auto",
        Recursive = true 
    };
    
    public Option<bool> AllTimeOption { get; } = new("--all-time") { Description = "Analyze all history (ignoring time window settings)", Recursive = true };
    public Option<bool> IncludeMergesOption { get; } = new("--include-merges") { Description = "Include merge commits in the analysis", Recursive = true };
    public Option<bool> IncludeDeletedOption { get; } = new("--include-deleted") { Description = "Include deleted files in stats", Recursive = true };
    public Option<bool> MergeByEmailOption { get; } = new("--merge-by-email") { Description = "Merge contributor identities by email", Recursive = true };
    public Option<bool> AnonymizeOption { get; } = new("--anonymize") { Description = "Anonymize contributor names/emails in output", Recursive = true };
    public Option<string> SinceOption { get; } = new("--since") { Description = "Filter commits since date (YYYY-MM-DD)", Recursive = true };
    public Option<string> UntilOption { get; } = new("--until") { Description = "Filter commits until date (YYYY-MM-DD)", Recursive = true };
    public Option<string> PathOption { get; } = new("--path") { Description = "Filter analysis to files matching glob pattern", Recursive = true };
    
    public Option<int> DepthOption { get; } = new("--depth") 
    { 
        Description = "Directory depth for areas analysis (1-10)", 
        DefaultValueFactory = _ => 2,
        Recursive = true 
    };

    public Option<string> HtmlOption { get; } = new("--html") { Description = "Output visual HTML report to path", Recursive = true, HelpName = "path" };
    public Option<string> MdOption { get; } = new("--md") { Description = "Output Markdown summary report to path", Recursive = true, HelpName = "path" };
    public Option<string> SvgOption { get; } = new("--svg") { Description = "Output SVG reports to path", Recursive = true, HelpName = "path" };

    public Option<int?> LimitOption { get; } = new("--limit") { Description = "Limit results to top N items", Recursive = true };
    public Option<string> SortOption { get; } = new("--sort") { Description = "Sort results by field", Recursive = true };
    public Option<string> ColumnsOption { get; } = new("--columns") { Description = "Select columns to show", Recursive = true };
    public Option<bool> QuietOption { get; } = new("--quiet") { Description = "Suppress non-critical warnings", Recursive = true };

    public Argument<string> RepoPathArg { get; } = new("repo_path") { Description = "Path to the repository", DefaultValueFactory = _ => "." };

    public CliOptions()
    {
        DepthOption.Validators.Add(result =>
        {
            try
            {
                var value = result.GetValue(DepthOption);
                if (value < 1 || value > 10)
                {
                    result.AddError("--depth must be an integer between 1 and 10.");
                }
            }
            catch
            {
                result.AddError("--depth must be an integer between 1 and 10.");
            }
        });
    }

    /// <summary>
    /// Registers all encapsulated options and arguments onto the given root command.
    /// </summary>
    public void RegisterOn(RootCommand rootCommand)
    {
        rootCommand.Options.Add(ConfigOption);
        rootCommand.Options.Add(UserConfigOption);
        rootCommand.Options.Add(JsonOption);
        rootCommand.Options.Add(FormatOption);
        rootCommand.Options.Add(ColorOption);
        rootCommand.Options.Add(AllTimeOption);
        rootCommand.Options.Add(IncludeMergesOption);
        rootCommand.Options.Add(IncludeDeletedOption);
        rootCommand.Options.Add(MergeByEmailOption);
        rootCommand.Options.Add(AnonymizeOption);
        rootCommand.Options.Add(SinceOption);
        rootCommand.Options.Add(UntilOption);
        rootCommand.Options.Add(PathOption);
        rootCommand.Options.Add(DepthOption);
        rootCommand.Options.Add(HtmlOption);
        rootCommand.Options.Add(MdOption);
        rootCommand.Options.Add(SvgOption);
        rootCommand.Options.Add(LimitOption);
        rootCommand.Options.Add(SortOption);
        rootCommand.Options.Add(ColumnsOption);
        rootCommand.Options.Add(QuietOption);
        rootCommand.Arguments.Add(RepoPathArg);
    }
}
