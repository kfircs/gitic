using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Gitic
{
    public interface ICliCommand
    {
        Task<CliResult> ExecuteAsync(IConsoleReporter? reporter);
    }

    public static class CliCommandFactory
    {
        public static ICliCommand CreateCommand(ParsedArgs parsed)
        {
            if (string.Equals(parsed.Command, "help", StringComparison.OrdinalIgnoreCase))
            {
                return new HelpCommand();
            }

            if (string.Equals(parsed.Command, "config", StringComparison.OrdinalIgnoreCase))
            {
                return new ConfigCommand(parsed);
            }

            if (string.Equals(parsed.Command, "hotspots", StringComparison.OrdinalIgnoreCase))
            {
                return new HotspotsCommand(parsed);
            }

            if (string.Equals(parsed.Command, "areas", StringComparison.OrdinalIgnoreCase))
            {
                return new AreasCommand(parsed);
            }

            if (string.Equals(parsed.Command, "contributors", StringComparison.OrdinalIgnoreCase))
            {
                return new ContributorsCommand(parsed);
            }

            if (string.Equals(parsed.Command, "contributor", StringComparison.OrdinalIgnoreCase))
            {
                return new ContributorCommand(parsed);
            }

            if (string.Equals(parsed.Command, "report", StringComparison.OrdinalIgnoreCase))
            {
                return new ReportCommand(parsed);
            }

            throw new CommandLineParseError($"Unknown command: {parsed.Command}");
        }
    }

    public class HelpCommand : ICliCommand
    {
        public Task<CliResult> ExecuteAsync(IConsoleReporter? reporter)
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
            reporter?.Write(helpText);
            return Task.FromResult(Cli.CliSuccess(helpText));
        }
    }

    public class ConfigCommand : ICliCommand
    {
        private readonly ParsedArgs _parsed;

        public ConfigCommand(ParsedArgs parsed)
        {
            _parsed = parsed;
        }

        public Task<CliResult> ExecuteAsync(IConsoleReporter? reporter)
        {
            if (_parsed.ConfigAction != "init")
            {
                string errMsg = "config requires an action. Try: gitizer config init\n";
                reporter?.WriteError(errMsg);
                return Task.FromResult(Cli.CliFailure(errMsg));
            }

            string stdout = ConfigLoader.RenderStarterConfig();
            reporter?.Write(stdout);
            return Task.FromResult(Cli.CliSuccess(stdout));
        }
    }

    public abstract class BaseAnalysisCommand : ICliCommand
    {
        protected readonly ParsedArgs Parsed;

        protected BaseAnalysisCommand(ParsedArgs parsed)
        {
            Parsed = parsed;
        }

        protected abstract AnalysisCommand CommandType { get; }

        public async Task<CliResult> ExecuteAsync(IConsoleReporter? reporter)
        {
            var gitClient = new GitClient(Parsed.RepoPath);
            string? repoRoot = await gitClient.GetRepositoryRootAsync();
            if (repoRoot == null)
            {
                string errMsg = $"Path {Parsed.RepoPath} is not inside a Git repository.\n" +
                                "Run gitizer from a Git worktree or pass the path to one.\n";
                reporter?.WriteError(errMsg);
                return Cli.CliFailure(errMsg);
            }

            IConfigManager configManager = new ConfigManager();
            LoadedGitizerConfig loadedConfig;
            try
            {
                loadedConfig = await configManager.LoadGitizerConfigAsync(new LoadGitizerConfigOptions { RepoRoot = repoRoot });
            }
            catch (ConfigValidationError error)
            {
                string errMsg = $"Invalid Gitizer config:\n{string.Join("\n", error.Details)}\n";
                reporter?.WriteError(errMsg);
                return Cli.CliFailure(errMsg);
            }

            var input = new AnalyzeInput
            {
                RepoRoot = repoRoot,
                Command = CommandType,
                Settings = Parsed.Settings,
                Config = loadedConfig.Config,
                ContributorName = Parsed.ContributorName
            };

            IRepositoryAnalyzer analyzer = new RepositoryAnalyzer();
            AnalysisResult result = await analyzer.AnalyzeAsync(input);

            return await ProcessResultAsync(result, reporter);
        }

        protected abstract Task<CliResult> ProcessResultAsync(AnalysisResult result, IConsoleReporter? reporter);
    }

    public abstract class StandardRenderAnalysisCommand : BaseAnalysisCommand
    {
        protected StandardRenderAnalysisCommand(ParsedArgs parsed) : base(parsed) { }

        protected override async Task<CliResult> ProcessResultAsync(AnalysisResult result, IConsoleReporter? reporter)
        {
            if (Parsed.Settings.Anonymize)
            {
                IResultAnonymizer anonymizer = new ResultAnonymizer();
                result = anonymizer.Anonymize(result);
            }

            IReportRenderer renderer;
            if (Parsed.Settings.Json)
            {
                renderer = new JsonRenderer();
            }
            else
            {
                renderer = new CliTableRenderer(CommandType);
            }

            string output = await renderer.RenderAsync(result);
            reporter?.Write(output);
            return Cli.CliSuccess(output);
        }
    }

    public class HotspotsCommand : StandardRenderAnalysisCommand
    {
        public HotspotsCommand(ParsedArgs parsed) : base(parsed) { }
        protected override AnalysisCommand CommandType => AnalysisCommand.Hotspots;
    }

    public class AreasCommand : StandardRenderAnalysisCommand
    {
        public AreasCommand(ParsedArgs parsed) : base(parsed) { }
        protected override AnalysisCommand CommandType => AnalysisCommand.Areas;
    }

    public class ContributorsCommand : StandardRenderAnalysisCommand
    {
        public ContributorsCommand(ParsedArgs parsed) : base(parsed) { }
        protected override AnalysisCommand CommandType => AnalysisCommand.Contributors;
    }

    public class ContributorCommand : StandardRenderAnalysisCommand
    {
        public ContributorCommand(ParsedArgs parsed) : base(parsed) { }
        protected override AnalysisCommand CommandType => AnalysisCommand.Contributor;

        protected override async Task<CliResult> ProcessResultAsync(AnalysisResult result, IConsoleReporter? reporter)
        {
            IContributorLookupRegistry registry = new ContributorLookupRegistry(result.Contributors);
            try
            {
                var filtered = registry.Find(Parsed.ContributorName ?? "");
                result.Contributors = new List<ContributorMetric> { filtered };
            }
            catch (Exception ex) when (ex is ContributorNotFoundError || ex is AmbiguousContributorError)
            {
                reporter?.WriteError($"{ex.Message}\n");
                return Cli.CliFailure($"{ex.Message}\n");
            }

            return await base.ProcessResultAsync(result, reporter);
        }
    }

    public class ReportCommand : BaseAnalysisCommand
    {
        public ReportCommand(ParsedArgs parsed) : base(parsed) { }
        protected override AnalysisCommand CommandType => AnalysisCommand.Report;

        protected override async Task<CliResult> ProcessResultAsync(AnalysisResult result, IConsoleReporter? reporter)
        {
            if (Parsed.HtmlPath == null && Parsed.MdPath == null && Parsed.SvgPath == null)
            {
                string errMsg = "report requires --html <path>, --md <path>, or --svg <path>.\n";
                reporter?.WriteError(errMsg);
                return Cli.CliFailure(errMsg);
            }

            if (Parsed.Settings.Anonymize)
            {
                IResultAnonymizer anonymizer = new ResultAnonymizer();
                result = anonymizer.Anonymize(result);
            }

            var outputSb = new StringBuilder();
            if (Parsed.HtmlPath != null)
            {
                var htmlRenderer = new HtmlRenderer(Parsed.HtmlPath);
                string htmlResult = await htmlRenderer.RenderAsync(result);
                outputSb.Append(htmlResult);
            }
            if (Parsed.MdPath != null)
            {
                var mdRenderer = new MarkdownRenderer(Parsed.MdPath);
                string mdResult = await mdRenderer.RenderAsync(result);
                outputSb.Append(mdResult);
            }
            if (Parsed.SvgPath != null)
            {
                var svgRenderer = new SvgRenderer(Parsed.SvgPath);
                string svgResult = await svgRenderer.RenderAsync(result);
                outputSb.Append(svgResult);
            }

            string reportOutput = outputSb.ToString();
            reporter?.Write(reportOutput);
            return Cli.CliSuccess(reportOutput);
        }
    }
}
