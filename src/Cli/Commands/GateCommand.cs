using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Kfc.Cli.Core;

namespace Gitic;

public class GateCommandOptions
{
    public string? Baseline { get; set; }
    public string Format { get; set; } = "human";
    public bool FailFast { get; set; }
    public string? ConfigPath { get; set; }
    public string? StorageDir { get; set; }
    public string RepoPath { get; set; } = ".";
    public bool Json { get; set; }
}

public class GateCommand : IGiticCommand
{
    private readonly GateCommandOptions _options;
    private readonly IQualityGateEngine _gateEngine;
    private readonly IBaselineEngine _baselineEngine;
    private readonly IRepositoryAnalyzer _analyzer;
    private readonly IGitClient? _gitClient;

    public GateCommand(
        ParsedArgs parsed,
        IQualityGateEngine? gateEngine = null,
        IBaselineEngine? baselineEngine = null,
        IRepositoryAnalyzer? analyzer = null,
        IGitClient? gitClient = null)
    {
        _gateEngine = gateEngine ?? new QualityGateEngine();
        _baselineEngine = baselineEngine ?? new BaselineEngine();
        _analyzer = analyzer ?? new RepositoryAnalyzer();
        _gitClient = gitClient;
        _options = ParseOptionsFromParsedArgs(parsed);
    }

    public GateCommand(
        string[] args,
        IQualityGateEngine? gateEngine = null,
        IBaselineEngine? baselineEngine = null,
        IRepositoryAnalyzer? analyzer = null,
        IGitClient? gitClient = null)
    {
        _gateEngine = gateEngine ?? new QualityGateEngine();
        _baselineEngine = baselineEngine ?? new BaselineEngine();
        _analyzer = analyzer ?? new RepositoryAnalyzer();
        _gitClient = gitClient;
        _options = ParseOptionsFromArgs(args);
    }

    public GateCommand(
        GateCommandOptions options,
        IQualityGateEngine? gateEngine = null,
        IBaselineEngine? baselineEngine = null,
        IRepositoryAnalyzer? analyzer = null,
        IGitClient? gitClient = null)
    {
        _gateEngine = gateEngine ?? new QualityGateEngine();
        _baselineEngine = baselineEngine ?? new BaselineEngine();
        _analyzer = analyzer ?? new RepositoryAnalyzer();
        _gitClient = gitClient;
        _options = options ?? new GateCommandOptions();
    }

    public Task<CliResult> ExecuteAsync(IConsoleReporter reporter)
    {
        return ExecuteAsync(reporter, CancellationToken.None);
    }

    public async Task<CliResult> ExecuteAsync(IConsoleReporter? reporter, CancellationToken cancellationToken = default)
    {
        string repoRoot = string.IsNullOrWhiteSpace(_options.RepoPath) ? "." : _options.RepoPath;
        var gitClient = _gitClient ?? new GitClient(repoRoot);

        string? resolvedRoot = null;
        try
        {
            resolvedRoot = await gitClient.GetRepositoryRootAsync(cancellationToken);
        }
        catch
        {
            // Ignore git lookup error if not in git repo
        }

        string actualRoot = !string.IsNullOrEmpty(resolvedRoot) ? resolvedRoot : Path.GetFullPath(repoRoot);

        // 1. Resolve baseline snapshot if requested
        BaselineSnapshot? baselineSnapshot = null;
        if (!string.IsNullOrWhiteSpace(_options.Baseline))
        {
            try
            {
                if (string.Equals(_options.Baseline, "latest", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(_options.Baseline, "latest.json", StringComparison.OrdinalIgnoreCase))
                {
                    string storageDir = ResolveStorageDir(actualRoot);
                    var baselines = _baselineEngine.ListBaselines(storageDir);
                    if (baselines == null || baselines.Count == 0)
                    {
                        string errMsg = $"Operational error: No baseline snapshots found in storage directory '{storageDir}' for '--baseline latest'.\n";
                        reporter?.WriteError(errMsg);
                        return Cli.CliFailure(errMsg, exitCode: 2);
                    }
                    baselineSnapshot = baselines[^1];
                }
                else
                {
                    string baselinePath = _options.Baseline;
                    if (!File.Exists(baselinePath))
                    {
                        string storageDir = ResolveStorageDir(actualRoot);
                        string candidateInStorage = Path.Combine(storageDir, baselinePath);
                        if (File.Exists(candidateInStorage))
                        {
                            baselinePath = candidateInStorage;
                        }
                    }

                    if (!File.Exists(baselinePath) && !Directory.Exists(baselinePath))
                    {
                        string errMsg = $"Operational error: Baseline snapshot file not found at '{_options.Baseline}'.\n";
                        reporter?.WriteError(errMsg);
                        return Cli.CliFailure(errMsg, exitCode: 2);
                    }

                    baselineSnapshot = _baselineEngine.LoadBaseline(baselinePath);
                }
            }
            catch (Exception ex)
            {
                string errMsg = $"Operational error loading baseline: {ex.Message}\n";
                reporter?.WriteError(errMsg);
                return Cli.CliFailure(errMsg, exitCode: 2);
            }
        }

        // 2. Load custom rules if config specified or present
        List<QualityGateRule>? customRules = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(_options.ConfigPath))
            {
                if (!File.Exists(_options.ConfigPath))
                {
                    string errMsg = $"Operational error: Configuration file not found at '{_options.ConfigPath}'.\n";
                    reporter?.WriteError(errMsg);
                    return Cli.CliFailure(errMsg, exitCode: 2);
                }
                customRules = _gateEngine.LoadRulesFromConfig(_options.ConfigPath);
            }
            else
            {
                string defaultYml = Path.Combine(actualRoot, ".gitic.yml");
                string defaultYaml = Path.Combine(actualRoot, ".gitic.yaml");
                if (File.Exists(defaultYml))
                {
                    var loaded = _gateEngine.LoadRulesFromConfig(defaultYml);
                    if (loaded != null && loaded.Count > 0)
                    {
                        customRules = loaded;
                    }
                }
                else if (File.Exists(defaultYaml))
                {
                    var loaded = _gateEngine.LoadRulesFromConfig(defaultYaml);
                    if (loaded != null && loaded.Count > 0)
                    {
                        customRules = loaded;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            string errMsg = $"Operational error loading quality gate configuration: {ex.Message}\n";
            reporter?.WriteError(errMsg);
            return Cli.CliFailure(errMsg, exitCode: 2);
        }

        // 3. Analyze repository
        AnalysisResult analysisResult;
        try
        {
            var analyzeInput = new AnalyzeInput
            {
                RepoRoot = actualRoot,
                Command = AnalysisCommand.Gate,
                GitClient = gitClient
            };

            analysisResult = await _analyzer.AnalyzeAsync(analyzeInput, cancellationToken);
        }
        catch (Exception ex)
        {
            string errMsg = $"Operational error during analysis: {ex.Message}\n";
            reporter?.WriteError(errMsg);
            return Cli.CliFailure(errMsg, exitCode: 2);
        }

        // 4. Evaluate gates
        QualityGateEvaluationResult evaluationResult;
        try
        {
            evaluationResult = _gateEngine.EvaluateGates(analysisResult, baselineSnapshot, customRules, _options.FailFast);
        }
        catch (Exception ex)
        {
            string errMsg = $"Operational error evaluating quality gates: {ex.Message}\n";
            reporter?.WriteError(errMsg);
            return Cli.CliFailure(errMsg, exitCode: 2);
        }

        // 5. Output format handling
        if (string.Equals(_options.Format, "github-annotations", StringComparison.OrdinalIgnoreCase))
        {
            string annotations = evaluationResult.FormatGitHubAnnotations();
            if (!string.IsNullOrWhiteSpace(annotations))
            {
                reporter?.Write(annotations + "\n");
            }
            else
            {
                reporter?.Write("::notice title=Quality Gate::All quality gate assertions passed successfully.\n");
            }

            return evaluationResult.IsPassed
                ? Cli.CliSuccess(annotations)
                : Cli.CliFailure(annotations, exitCode: 1);
        }

        if (_options.Json || string.Equals(_options.Format, "json", StringComparison.OrdinalIgnoreCase))
        {
            string jsonOutput = JsonSerializer.Serialize(evaluationResult, JsonSerializationDefaults.Indented);
            reporter?.Write(jsonOutput);

            return evaluationResult.IsPassed
                ? Cli.CliSuccess(jsonOutput)
                : Cli.CliFailure(jsonOutput, exitCode: 1);
        }

        // Default human format
        string terminalSummary = evaluationResult.FormatTerminalSummary();
        reporter?.Write(terminalSummary);

        return evaluationResult.IsPassed
            ? Cli.CliSuccess(terminalSummary)
            : Cli.CliFailure(terminalSummary, exitCode: 1);
    }

    private string ResolveStorageDir(string repoRoot)
    {
        if (!string.IsNullOrWhiteSpace(_options.StorageDir))
        {
            return _options.StorageDir;
        }

        return Path.Combine(repoRoot, ".gitic", "baselines");
    }

    private static GateCommandOptions ParseOptionsFromParsedArgs(ParsedArgs parsed)
    {
        var options = new GateCommandOptions
        {
            Baseline = parsed.GateBaseline ?? parsed.BaselineFrom,
            Format = !string.IsNullOrEmpty(parsed.GateFormat) ? parsed.GateFormat : parsed.Settings.Format,
            FailFast = parsed.GateFailFast,
            ConfigPath = parsed.GateConfig,
            StorageDir = parsed.GateStorageDir ?? parsed.BaselineStorageDir,
            RepoPath = parsed.RepoPath,
            Json = parsed.Settings.Json
        };

        if (parsed.RawArgs != null && parsed.RawArgs.Count > 0)
        {
            var raw = ParseOptionsFromArgs(parsed.RawArgs.ToArray());
            if (string.IsNullOrEmpty(options.Baseline)) options.Baseline = raw.Baseline;
            if (options.Format == "human" && raw.Format != "human") options.Format = raw.Format;
            if (!options.FailFast && raw.FailFast) options.FailFast = true;
            if (string.IsNullOrEmpty(options.ConfigPath)) options.ConfigPath = raw.ConfigPath;
            if (string.IsNullOrEmpty(options.StorageDir)) options.StorageDir = raw.StorageDir;
            if (options.RepoPath == "." && raw.RepoPath != ".") options.RepoPath = raw.RepoPath;
            if (!options.Json && raw.Json) options.Json = true;
        }

        return options;
    }

    public static GateCommandOptions ParseOptionsFromArgs(string[] args)
    {
        var options = new GateCommandOptions();
        if (args == null || args.Length == 0) return options;

        int startIndex = 0;
        if (string.Equals(args[0], "gate", StringComparison.OrdinalIgnoreCase))
        {
            startIndex = 1;
        }

        for (int i = startIndex; i < args.Length; i++)
        {
            string arg = args[i];

            if (string.Equals(arg, "--baseline", StringComparison.OrdinalIgnoreCase) || string.Equals(arg, "-b", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length) options.Baseline = args[++i];
            }
            else if (string.Equals(arg, "--format", StringComparison.OrdinalIgnoreCase) || string.Equals(arg, "-f", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length) options.Format = args[++i];
            }
            else if (string.Equals(arg, "--fail-fast", StringComparison.OrdinalIgnoreCase))
            {
                options.FailFast = true;
            }
            else if (string.Equals(arg, "--config", StringComparison.OrdinalIgnoreCase) || string.Equals(arg, "-c", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length) options.ConfigPath = args[++i];
            }
            else if (string.Equals(arg, "--storage", StringComparison.OrdinalIgnoreCase) || string.Equals(arg, "--dir", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length) options.StorageDir = args[++i];
            }
            else if (string.Equals(arg, "--repo", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length) options.RepoPath = args[++i];
            }
            else if (string.Equals(arg, "--json", StringComparison.OrdinalIgnoreCase))
            {
                options.Json = true;
            }
            else if (!arg.StartsWith("-"))
            {
                options.RepoPath = arg;
            }
        }

        return options;
    }
}
