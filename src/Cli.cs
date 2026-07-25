using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Gitic
{
    public class CliResult
    {
        public int ExitCode { get; set; }
        public string Stdout { get; set; } = string.Empty;
        public string Stderr { get; set; } = string.Empty;
    }

    public static class Cli
    {
        public static CliResult CliSuccess(string stdout)
        {
            return new CliResult
            {
                ExitCode = 0,
                Stdout = stdout,
                Stderr = ""
            };
        }

        public static CliResult CliFailure(string stderr, int exitCode = 1)
        {
            return new CliResult
            {
                ExitCode = exitCode,
                Stdout = "",
                Stderr = stderr
            };
        }

        public static async Task<CliResult> RunCliAsync(string[] args)
        {
            ICommandLineParser parser = new CommandLineParser(args);
            ParsedArgs parsed;
            try
            {
                parsed = parser.Parse();
            }
            catch (CommandLineParseError error)
            {
                return CliFailure($"{error.Message}\n");
            }

            try
            {
                if (parsed.Command == "help")
                {
                    return RunHelpCommand();
                }

                if (parsed.Command == "config")
                {
                    return RunConfigCommand(parsed);
                }

                var gitClient = new GitClient(parsed.RepoPath);
                string? repoRoot = await gitClient.GetRepositoryRootAsync();
                if (repoRoot == null)
                {
                    return CliFailure(
                        $"Path {parsed.RepoPath} is not inside a Git repository.\n" +
                        "Run gitizer from a Git worktree or pass the path to one.\n"
                    );
                }

                LoadedGitizerConfig loadedConfig;
                try
                {
                    loadedConfig = await ConfigLoader.LoadGitizerConfigAsync(new LoadGitizerConfigOptions { RepoRoot = repoRoot });
                }
                catch (ConfigValidationError error)
                {
                    return CliFailure($"Invalid Gitizer config:\n{string.Join("\n", error.Details)}\n");
                }

                var input = new AnalyzeInput
                {
                    RepoRoot = repoRoot,
                    Command = ParseCommand(parsed.Command),
                    Settings = parsed.Settings,
                    Config = loadedConfig.Config,
                    ContributorName = parsed.ContributorName
                };

                IRepositoryAnalyzer analyzer = new RepositoryAnalyzerImpl();
                AnalysisResult result = await analyzer.AnalyzeRepositoryAsync(input);

                if (parsed.Command == "contributor")
                {
                    IContributorLookupRegistry registry = new ContributorLookupRegistry(result.Contributors);
                    try
                    {
                        var filtered = registry.Find(parsed.ContributorName ?? "");
                        result.Contributors = new List<ContributorMetric> { filtered };
                    }
                    catch (Exception ex) when (ex is ContributorNotFoundError || ex is AmbiguousContributorError)
                    {
                        return CliFailure($"{ex.Message}\n");
                    }
                }

                if (parsed.Settings.Anonymize)
                {
                    IResultAnonymizer anonymizer = new ResultAnonymizer();
                    result = anonymizer.Anonymize(result);
                }

                if (parsed.Command == "report")
                {
                    if (parsed.HtmlPath == null && parsed.MdPath == null && parsed.SvgPath == null)
                    {
                        return CliFailure("report requires --html <path>, --md <path>, or --svg <path>.\n");
                    }

                    var outputSb = new System.Text.StringBuilder();
                    if (parsed.HtmlPath != null)
                    {
                        var htmlRenderer = new HtmlRenderer(parsed.HtmlPath);
                        string htmlResult = await htmlRenderer.RenderAsync(result);
                        outputSb.Append(htmlResult);
                    }
                    if (parsed.MdPath != null)
                    {
                        var mdRenderer = new MarkdownRenderer(parsed.MdPath);
                        string mdResult = await mdRenderer.RenderAsync(result);
                        outputSb.Append(mdResult);
                    }
                    if (parsed.SvgPath != null)
                    {
                        var svgRenderer = new SvgRenderer(parsed.SvgPath);
                        string svgResult = await svgRenderer.RenderAsync(result);
                        outputSb.Append(svgResult);
                    }

                    return CliSuccess(outputSb.ToString());
                }

                IReportRenderer renderer;
                if (parsed.Settings.Json)
                {
                    renderer = new JsonRenderer();
                }
                else
                {
                    renderer = new CliTableRenderer(ParseCommand(parsed.Command));
                }

                string output = await renderer.RenderAsync(result);
                return CliSuccess(output);
            }
            catch (Exception ex)
            {
                return CliFailure($"Error: {ex.Message}\n");
            }
        }

        private static CliResult RunHelpCommand()
        {
            string helpText = 
@"Gitic - Gitizer C# Port (v0.1.0)
A tool to analyze Git repositories and identify code hotspots, contributor ownership, areas, and temporal coupling.

Usage:
  gitic <command> [repo_path] [options]

Commands:
  hotspots [repo_path]                  Identify code hotspots with high complexity/churn
  areas [repo_path]                     Analyze code ownership and changes across directories
  contributors [repo_path]              Show contributor metrics and profiles
  contributor <name> [repo_path]        Analyze a specific contributor's details
  report [repo_path] [options]          Generate reports (visual HTML and/or Markdown summary)
  config init                           Generate a starter config file (.gitizer.yml)

Options:
  -h, --help                            Show this help menu
  --html <path>                         Output visual HTML report to path (for report command)
  --md <path>                           Output Markdown summary report to path (for report command)
  --json                                Output results in raw JSON format
  --all-time                            Analyze all history (ignoring time window settings)
  --since <date>                        Filter commits since date (YYYY-MM-DD)
  --path <pattern>                      Filter analysis to files matching glob pattern (e.g. 'src/**')
  --depth <num>                         Directory depth for areas analysis (1-10, default: 2)
  --include-merges                      Include merge commits in the analysis
  --include-deleted                     Include deleted files in stats
  --merge-by-email                      Merge contributor identities by email
  --anonymize                           Anonymize contributor names/emails in output
";
            return CliSuccess(helpText);
        }

        private static CliResult RunConfigCommand(ParsedArgs parsed)
        {
            if (parsed.ConfigAction != "init")
            {
                return CliFailure("config requires an action. Try: gitizer config init\n");
            }

            return CliSuccess(ConfigLoader.RenderStarterConfig());
        }

        private static AnalysisCommand ParseCommand(string cmd)
        {
            if (string.Equals(cmd, "hotspots", StringComparison.OrdinalIgnoreCase)) return AnalysisCommand.Hotspots;
            if (string.Equals(cmd, "areas", StringComparison.OrdinalIgnoreCase)) return AnalysisCommand.Areas;
            if (string.Equals(cmd, "contributors", StringComparison.OrdinalIgnoreCase)) return AnalysisCommand.Contributors;
            if (string.Equals(cmd, "contributor", StringComparison.OrdinalIgnoreCase)) return AnalysisCommand.Contributor;
            if (string.Equals(cmd, "report", StringComparison.OrdinalIgnoreCase)) return AnalysisCommand.Report;
            throw new CommandLineParseError($"Unknown command: {cmd}");
        }
    }
}
