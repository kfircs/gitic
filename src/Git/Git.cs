using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gitic;

public class GitHistoryExtractorOptions
{
    public bool IncludeMerges { get; init; }
    public bool AllTime { get; init; }
    public string? Since { get; init; }
    public string? Path { get; set; }
}

public interface IGitExecutor
{
    IAsyncEnumerable<string> RunAsync(string[] args, string cwd, CancellationToken cancellationToken = default);
}

public class ExecFileGitExecutor : IGitExecutor
{
    public async IAsyncEnumerable<string> RunAsync(string[] args, string cwd, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        string fullCwd = Path.GetFullPath(cwd);
        List<string> allArgs = ["-C", fullCwd, .. args];

        ProcessStartInfo psi = new()
        {
            FileName = "git",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = fullCwd
        };
        foreach (var arg in allArgs)
        {
            psi.ArgumentList.Add(arg);
        }

        using Process process = new() { StartInfo = psi };
        using var registration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch { /* Ignore */ }
        });

        try
        {
            process.Start();
        }
        catch (Exception ex) when (IsGitNoCommitsException(ex))
        {
            yield break;
        }

        while (true)
        {
            string? line;
            try
            {
                line = await process.StandardOutput.ReadLineAsync(cancellationToken);
            }
            catch (Exception ex) when (IsGitNoCommitsException(ex))
            {
                yield break;
            }

            if (line == null)
            {
                break;
            }

            yield return line;
        }

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            string stderrOutput = await process.StandardError.ReadToEndAsync(cancellationToken);
            if (!stderrOutput.Contains("does not have any commits yet") &&
                !stderrOutput.Contains("Not a valid object name HEAD"))
            {
                throw new Exception($"Git command failed with exit code {process.ExitCode}: {stderrOutput}");
            }
        }

        bool IsGitNoCommitsException(Exception ex)
        {
            return ex.Message.Contains("does not have any commits yet") ||
                   ex.Message.Contains("Not a valid object name HEAD") ||
                   ex.InnerException?.Message.Contains("does not have any commits yet") == true ||
                   ex.InnerException?.Message.Contains("Not a valid object name HEAD") == true;
        }
    }
}

public static class GitUtils
{
    public static string DefaultSinceDate(DateTime? now = null)
    {
        var baseTime = now ?? DateTime.UtcNow;
        var since = baseTime.AddDays(-180);
        return since.ToString("yyyy-MM-ddTHH:mm:ssZ");
    }
}

public interface IGitClient
{
    Task<string?> GetRepositoryRootAsync(CancellationToken cancellationToken = default);
    Task<HashSet<string>> ListHeadFilesAsync(CancellationToken cancellationToken = default);
    Task<List<GitCommitRecord>> ExtractHistoryAsync(GitHistoryExtractorOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deeper interface addition that retrieves and constructs a complete, self-contained GitCommitGraph.
    /// Existing classes/mocks (like FakeGitClient in tests) get this automatically via default interface implementation,
    /// avoiding build-breaking changes in other files while offering a high-leverage entry point for new callers.
    /// </summary>
    async Task<GitCommitGraph> GetCommitGraphAsync(GitHistoryExtractorOptions? options = null, CancellationToken cancellationToken = default)
    {
        var history = await ExtractHistoryAsync(options, cancellationToken);
        return new GitCommitGraph(history);
    }
}

public class GitClient : IGitClient
{
    private readonly string _repoRoot;
    private readonly IGitExecutor _executor;
    private readonly IGitParser _parser;

    public GitClient(string repoRoot, IGitExecutor? executor = null, IGitParser? parser = null)
    {
        _repoRoot = string.IsNullOrEmpty(repoRoot) ? Directory.GetCurrentDirectory() : Path.GetFullPath(repoRoot);
        _executor = executor ?? new ExecFileGitExecutor();
        _parser = parser ?? new GitParser(new GitPatchParser());
    }

    private async Task<string> ExecuteAndAggregateStdoutAsync(string[] args, CancellationToken cancellationToken)
    {
        StringBuilder sb = new();
        await foreach (var line in _executor.RunAsync(args, _repoRoot, cancellationToken))
        {
            sb.AppendLine(line);
        }
        return sb.ToString();
    }

    public async Task<string?> GetRepositoryRootAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            string stdout = await ExecuteAndAggregateStdoutAsync(["rev-parse", "--show-toplevel"], cancellationToken);
            string trimmed = stdout.Trim();
            return trimmed.Length > 0 ? trimmed : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<HashSet<string>> ListHeadFilesAsync(CancellationToken cancellationToken = default)
    {
        HashSet<string> files = [];
        await foreach (var line in _executor.RunAsync(["ls-tree", "-r", "--name-only", "HEAD"], _repoRoot, cancellationToken))
        {
            string trimmed = PathUtils.NormalizeGitPath(line.Trim());
            if (trimmed.Length > 0)
            {
                files.Add(trimmed);
            }
        }
        return files;
    }

    public async Task<List<GitCommitRecord>> ExtractHistoryAsync(GitHistoryExtractorOptions? options = null, CancellationToken cancellationToken = default)
    {
        var opt = options ?? new();
        var args = _parser.BuildGitLogArguments(opt);

        string stdout = await ExecuteAndAggregateStdoutAsync(args.ToArray(), cancellationToken);
        return _parser.ParseGitLog(stdout);
    }
}
