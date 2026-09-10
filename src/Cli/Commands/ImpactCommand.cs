using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Kfc.Cli.Core;

namespace Gitic;

public class ImpactCommandOptions
{
    public List<string> TargetFiles { get; set; } = new();
    public bool Staged { get; set; }
    public bool InstallHook { get; set; }
    public double WarnThreshold { get; set; } = 0.5;
    public bool Json { get; set; }
    public bool Quiet { get; set; }
    public string RepoPath { get; set; } = ".";
    public string Format { get; set; } = "human";
}

public class ImpactCommand : IGiticCommand
{
    private readonly ImpactCommandOptions _options;
    private readonly IImpactPredictor _predictor;
    private readonly IRepositoryAnalyzer _analyzer;
    private readonly IGitClient? _gitClient;

    public ImpactCommand(
        ParsedArgs parsed,
        IImpactPredictor? predictor = null,
        IRepositoryAnalyzer? analyzer = null,
        IGitClient? gitClient = null)
    {
        _predictor = predictor ?? new ImpactPredictor();
        _analyzer = analyzer ?? new RepositoryAnalyzer();
        _gitClient = gitClient;
        _options = ParseOptionsFromParsedArgs(parsed);
    }

    public ImpactCommand(
        string[] args,
        IImpactPredictor? predictor = null,
        IRepositoryAnalyzer? analyzer = null,
        IGitClient? gitClient = null)
    {
        _predictor = predictor ?? new ImpactPredictor();
        _analyzer = analyzer ?? new RepositoryAnalyzer();
        _gitClient = gitClient;
        _options = ParseOptionsFromArgs(args);
    }

    public Task<CliResult> ExecuteAsync(IConsoleReporter reporter)
    {
        return ExecuteAsync(reporter, CancellationToken.None);
    }

    public async Task<CliResult> ExecuteAsync(IConsoleReporter? reporter, CancellationToken cancellationToken = default)
    {
        if (_options.InstallHook)
        {
            return await ExecuteInstallHookAsync(reporter, cancellationToken);
        }

        string repoRoot = string.IsNullOrWhiteSpace(_options.RepoPath) ? "." : _options.RepoPath;
        var gitClient = _gitClient ?? new GitClient(repoRoot);

        string? resolvedRoot = null;
        try
        {
            resolvedRoot = await gitClient.GetRepositoryRootAsync(cancellationToken);
        }
        catch { }

        string actualRoot = !string.IsNullOrEmpty(resolvedRoot) ? resolvedRoot : Path.GetFullPath(repoRoot);

        List<string> targetFiles;
        if (_options.Staged)
        {
            targetFiles = await gitClient.GetStagedFilesAsync(cancellationToken);
            if (targetFiles.Count == 0)
            {
                if (_options.Json || string.Equals(_options.Format, "json", StringComparison.OrdinalIgnoreCase))
                {
                    var emptyReport = new ImpactPredictionReport
                    {
                        TargetFiles = new List<string>(),
                        WarnThreshold = _options.WarnThreshold
                    };
                    string jsonOutput = JsonSerializer.Serialize(emptyReport, JsonSerializationDefaults.Indented);
                    reporter?.Write(jsonOutput);
                    return Cli.CliSuccess(jsonOutput);
                }

                string msg = "No staged files detected in git repository.\n";
                reporter?.Write(msg);
                return Cli.CliSuccess(msg);
            }
        }
        else
        {
            targetFiles = _options.TargetFiles;
            if (targetFiles.Count == 0)
            {
                string errMsg = "No target files specified. Pass one or more files, or use --staged. Try: gitic impact --help\n";
                reporter?.WriteError(errMsg);
                return Cli.CliFailure(errMsg, exitCode: 2);
            }
        }

        try
        {
            var analyzeInput = new AnalyzeInput
            {
                RepoRoot = actualRoot,
                Command = AnalysisCommand.Impact,
                GitClient = gitClient
            };

            var analysisResult = await _analyzer.AnalyzeAsync(analyzeInput, cancellationToken);
            var report = _predictor.PredictImpact(analysisResult, targetFiles, _options.WarnThreshold);

            if (_options.Json || string.Equals(_options.Format, "json", StringComparison.OrdinalIgnoreCase))
            {
                string jsonOutput = JsonSerializer.Serialize(report, JsonSerializationDefaults.Indented);
                reporter?.Write(jsonOutput);
                return Cli.CliSuccess(jsonOutput);
            }

            if (_options.Quiet)
            {
                string quietSummary = report.RenderQuietSummary(_options.WarnThreshold);
                if (!string.IsNullOrWhiteSpace(quietSummary))
                {
                    reporter?.Write(quietSummary + "\n");
                }
                return Cli.CliSuccess(quietSummary);
            }

            bool plain = string.Equals(_options.Format, "plain", StringComparison.OrdinalIgnoreCase);
            string humanOutput = report.RenderHumanReport(plain);
            reporter?.Write(humanOutput);
            return Cli.CliSuccess(humanOutput);
        }
        catch (Exception ex)
        {
            string errMsg = $"Failed to execute impact prediction: {ex.Message}\n";
            reporter?.WriteError(errMsg);
            return Cli.CliFailure(errMsg, exitCode: 1);
        }
    }

    private async Task<CliResult> ExecuteInstallHookAsync(IConsoleReporter? reporter, CancellationToken cancellationToken)
    {
        string repoRoot = string.IsNullOrWhiteSpace(_options.RepoPath) ? "." : _options.RepoPath;
        var gitClient = _gitClient ?? new GitClient(repoRoot);

        string? resolvedRoot = null;
        try
        {
            resolvedRoot = await gitClient.GetRepositoryRootAsync(cancellationToken);
        }
        catch { }

        string actualRoot = !string.IsNullOrEmpty(resolvedRoot) ? resolvedRoot : Path.GetFullPath(repoRoot);
        string gitDir = Path.Combine(actualRoot, ".git");

        if (File.Exists(gitDir))
        {
            // Git worktree or submodule referencing git directory
            string gitFileContent = (await File.ReadAllTextAsync(gitDir, cancellationToken)).Trim();
            if (gitFileContent.StartsWith("gitdir:", StringComparison.OrdinalIgnoreCase))
            {
                string relOrAbsGitDir = gitFileContent.Substring("gitdir:".Length).Trim();
                gitDir = Path.IsPathRooted(relOrAbsGitDir)
                    ? relOrAbsGitDir
                    : Path.GetFullPath(Path.Combine(actualRoot, relOrAbsGitDir));
            }
        }

        string hooksDir = Path.Combine(gitDir, "hooks");
        Directory.CreateDirectory(hooksDir);

        string hookFilePath = Path.Combine(hooksDir, "pre-commit");
        string thresholdStr = _options.WarnThreshold.ToString("0.0#", CultureInfo.InvariantCulture);

        string hookScript =
$@"#!/bin/sh
# Gitic pre-commit hook: Change Impact Predictor
# Auto-generated by gitic impact --install-hook
exec gitic impact --staged --warn-threshold {thresholdStr}
";

        await File.WriteAllTextAsync(hookFilePath, hookScript, cancellationToken);

        if (!OperatingSystem.IsWindows())
        {
            try
            {
                File.SetUnixFileMode(hookFilePath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }
            catch { }
        }

        string msg = $"Successfully installed git pre-commit hook at: {hookFilePath}\n";
        reporter?.Write(msg);
        return Cli.CliSuccess(msg);
    }

    private static ImpactCommandOptions ParseOptionsFromParsedArgs(ParsedArgs parsed)
    {
        var options = new ImpactCommandOptions
        {
            Staged = parsed.ImpactStaged,
            InstallHook = parsed.ImpactInstallHook,
            WarnThreshold = parsed.ImpactWarnThreshold,
            TargetFiles = new List<string>(parsed.ImpactTargetFiles ?? Array.Empty<string>()),
            RepoPath = parsed.RepoPath,
            Json = parsed.Settings.Json,
            Quiet = parsed.Settings.Quiet,
            Format = parsed.Settings.Format
        };

        if (parsed.RawArgs != null && parsed.RawArgs.Count > 0)
        {
            var raw = ParseOptionsFromArgs(parsed.RawArgs.ToArray());
            if (!options.Staged && raw.Staged) options.Staged = true;
            if (!options.InstallHook && raw.InstallHook) options.InstallHook = true;
            if (options.WarnThreshold == 0.5 && raw.WarnThreshold != 0.5) options.WarnThreshold = raw.WarnThreshold;
            if (options.TargetFiles.Count == 0 && raw.TargetFiles.Count > 0) options.TargetFiles = raw.TargetFiles;
            if (options.RepoPath == "." && raw.RepoPath != ".") options.RepoPath = raw.RepoPath;
            if (!options.Json && raw.Json) options.Json = true;
            if (!options.Quiet && raw.Quiet) options.Quiet = true;
            if (options.Format == "human" && raw.Format != "human") options.Format = raw.Format;
        }

        return options;
    }

    public static ImpactCommandOptions ParseOptionsFromArgs(string[] args)
    {
        var options = new ImpactCommandOptions();
        if (args == null || args.Length == 0) return options;

        int startIndex = 0;
        if (string.Equals(args[0], "impact", StringComparison.OrdinalIgnoreCase))
        {
            startIndex = 1;
        }

        for (int i = startIndex; i < args.Length; i++)
        {
            string arg = args[i];

            if (string.Equals(arg, "--staged", StringComparison.OrdinalIgnoreCase))
            {
                options.Staged = true;
            }
            else if (string.Equals(arg, "--install-hook", StringComparison.OrdinalIgnoreCase))
            {
                options.InstallHook = true;
            }
            else if (string.Equals(arg, "--warn-threshold", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length && double.TryParse(args[++i], NumberStyles.Float, CultureInfo.InvariantCulture, out double threshold))
                {
                    options.WarnThreshold = threshold;
                }
            }
            else if (string.Equals(arg, "--json", StringComparison.OrdinalIgnoreCase))
            {
                options.Json = true;
            }
            else if (string.Equals(arg, "--quiet", StringComparison.OrdinalIgnoreCase) || string.Equals(arg, "-q", StringComparison.OrdinalIgnoreCase))
            {
                options.Quiet = true;
            }
            else if (string.Equals(arg, "--format", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length)
                {
                    options.Format = args[++i];
                    if (string.Equals(options.Format, "json", StringComparison.OrdinalIgnoreCase))
                    {
                        options.Json = true;
                    }
                }
            }
            else if (string.Equals(arg, "--repo", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length) options.RepoPath = args[++i];
            }
            else if (!arg.StartsWith("-"))
            {
                options.TargetFiles.Add(arg);
            }
        }

        return options;
    }
}
