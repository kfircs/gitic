using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gitic
{
    public class GitHistoryExtractorOptions
    {
        public bool IncludeMerges { get; set; }
        public bool AllTime { get; set; }
        public string? Since { get; set; }
    }

    public interface IGitExecutor
    {
        Task<string> RunAsync(string[] args, string cwd);
    }

    public class ExecFileGitExecutor : IGitExecutor
    {
        public async Task<string> RunAsync(string[] args, string cwd)
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
            try
            {
                process.Start();
                string stdout = await process.StandardOutput.ReadToEndAsync();
                string stderr = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

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
            catch (Exception ex)
            {
                if (ex.Message.Contains("does not have any commits yet") ||
                    ex.Message.Contains("Not a valid object name HEAD"))
                {
                    return "";
                }
                throw;
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

    public class GitClient
    {
        private readonly string _repoRoot;
        private readonly IGitExecutor _executor;

        public GitClient(string repoRoot, IGitExecutor? executor = null)
        {
            _repoRoot = repoRoot;
            _executor = executor ?? new ExecFileGitExecutor();
        }

        public async Task<string?> GetRepositoryRootAsync()
        {
            try
            {
                string stdout = await _executor.RunAsync(new[] { "rev-parse", "--show-toplevel" }, _repoRoot);
                string trimmed = stdout.Trim();
                return trimmed.Length > 0 ? trimmed : null;
            }
            catch
            {
                return null;
            }
        }

        public async Task<HashSet<string>> ListHeadFilesAsync()
        {
            string stdout = await _executor.RunAsync(new[] { "ls-tree", "-r", "--name-only", "HEAD" }, _repoRoot);
            return new HashSet<string>(
                stdout
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => PathUtils.NormalizeGitPath(line.Trim()))
                    .Where(line => line.Length > 0)
            );
        }

        public async Task<List<GitCommitRecord>> ExtractHistoryAsync(GitHistoryExtractorOptions? options = null)
        {
            var opt = options ?? new GitHistoryExtractorOptions();
            var args = new List<string>
            {
                "log",
                "--numstat",
                "-p",
                $"--format=format:{GitParser.CommitMarker}%n%H%n%aI%n%an%n%ae%n%P%n%B%n{GitParser.NumstatMarker}"
            };

            if (opt.IncludeMerges)
            {
                args.Add("--cc");
            }
            else
            {
                args.Add("--no-merges");
            }

            if (!opt.AllTime)
            {
                args.Add($"--since={opt.Since ?? GitUtils.DefaultSinceDate()}");
            }

            string stdout = await _executor.RunAsync(args.ToArray(), _repoRoot);
            return GitParser.ParseGitLog(stdout);
        }
    }
}
