using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gitic
{
    public class GitHistoryExtractorOptions
    {
        public bool IncludeMerges { get; init; }
        public bool AllTime { get; init; }
        public string? Since { get; init; }
    }

    public interface IGitExecutor
    {
        Task<string> RunAsync(string[] args, string cwd, CancellationToken cancellationToken = default);
    }

    public class ExecFileGitExecutor : IGitExecutor
    {
        public async Task<string> RunAsync(string[] args, string cwd, CancellationToken cancellationToken = default)
        {
            var allArgs = new List<string> { "-C", cwd };
            allArgs.AddRange(args);

            var psi = new ProcessStartInfo
            {
                FileName = "git",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var arg in allArgs)
            {
                psi.ArgumentList.Add(arg);
            }

            using var process = new Process { StartInfo = psi };
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
                string stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
                string stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
                await process.WaitForExitAsync(cancellationToken);

                if (process.ExitCode != 0)
                {
                    if (stderr.Contains("does not have any commits yet") ||
                        stderr.Contains("Not a valid object name HEAD"))
                    {
                        return "";
                    }
                    throw new Exception($"Git command failed with exit code {process.ExitCode}: {stderr}");
                }
                return stdout;
            }
            catch (Exception ex) when (ex.Message.Contains("does not have any commits yet") ||
                                       ex.Message.Contains("Not a valid object name HEAD") ||
                                       ex.InnerException?.Message.Contains("does not have any commits yet") == true ||
                                       ex.InnerException?.Message.Contains("Not a valid object name HEAD") == true)
            {
                return "";
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

    public interface IGitClient : ICommitStream
    {
        Task<string?> GetRepositoryRootAsync(CancellationToken cancellationToken = default);
        Task<HashSet<string>> ListHeadFilesAsync(CancellationToken cancellationToken = default);

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
        private readonly GitParser _parser;

        public GitClient(string repoRoot, IGitExecutor? executor = null)
        {
            _repoRoot = repoRoot;
            _executor = executor ?? new ExecFileGitExecutor();
            var patchParser = new GitPatchParser();
            _parser = new GitParser(patchParser);
        }

        public async Task<string?> GetRepositoryRootAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                string stdout = await _executor.RunAsync(new[] { "rev-parse", "--show-toplevel" }, _repoRoot, cancellationToken);
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
            string stdout = await _executor.RunAsync(new[] { "ls-tree", "-r", "--name-only", "HEAD" }, _repoRoot, cancellationToken);
            return new HashSet<string>(
                stdout
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => PathUtils.NormalizeGitPath(line.Trim()))
                    .Where(line => line.Length > 0)
            );
        }

        public async Task<List<GitCommitRecord>> ExtractHistoryAsync(GitHistoryExtractorOptions? options = null, CancellationToken cancellationToken = default)
        {
            var opt = options ?? new GitHistoryExtractorOptions();
            var args = _parser.BuildGitLogArguments(opt);

            string stdout = await _executor.RunAsync(args.ToArray(), _repoRoot, cancellationToken);
            return _parser.ParseGitLog(stdout);
        }
    }
}
