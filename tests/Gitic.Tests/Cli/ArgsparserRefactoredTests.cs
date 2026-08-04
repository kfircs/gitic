using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Gitic.Tests
{
    public class ArgsparserRefactoredTests
    {
        [Fact]
        public void TestCommandLineParser_HelpText_IsPopulated()
        {
            var helpArgs = new[] { "--help" };
            var parser = new CommandLineParser(helpArgs);
            var parsed = parser.Parse();

            Assert.Equal("help", parsed.Command);
            Assert.NotNull(parsed.HelpText);
            Assert.Null(parsed.HtmlPath);
            Assert.Contains("Gitic Strategic Codebase Analysis", parsed.HelpText);
        }

        [Fact]
        public void TestCommandLineParser_HelpShort_IsPopulated()
        {
            var helpArgs = new[] { "-h" };
            var parser = new CommandLineParser(helpArgs);
            var parsed = parser.Parse();

            Assert.Equal("help", parsed.Command);
            Assert.NotNull(parsed.HelpText);
            Assert.Null(parsed.HtmlPath);
            Assert.Contains("Gitic Strategic Codebase Analysis", parsed.HelpText);
        }

        [Fact]
        public async Task TestCliCommandFactory_CreatesHelpCommandWithHelpText()
        {
            var helpArgs = new[] { "--help" };
            var parser = new CommandLineParser(helpArgs);
            var parsed = parser.Parse();

            var command = new CliCommandFactoryImpl().CreateCommand(parsed);
            Assert.IsType<HelpCommand>(command);

            var reporter = new MockConsoleReporter();
            var result = await command.ExecuteAsync(reporter);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Gitic Strategic Codebase Analysis", reporter.Stdout);
        }

        private class MockConsoleReporter : IConsoleReporter
        {
            public string Stdout { get; set; } = string.Empty;
            public string Stderr { get; set; } = string.Empty;

            public void Write(string message) => Stdout += message;
            public void WriteLine(string message) => Stdout += message + "\n";
            public void WriteError(string message) => Stderr += message;
            public void WriteErrorLine(string message) => Stderr += message + "\n";
        }

        [Fact]
        public async Task TestBaseAnalysisCommand_InjectsDependencies()
        {
            var parsed = new ParsedArgs
            {
                RepoPath = "/fake/root",
                Settings = DefaultAnalysisSettings.Create()
            };
            parsed.Settings.Format = "json";

            var fakeGit = new FakeGitClientForTesting();
            var fakeAnalyzer = new FakeRepositoryAnalyzerForTesting();

            var command = new HotspotsCommand(parsed, fakeGit, fakeAnalyzer);
            var reporter = new MockConsoleReporter();

            var result = await command.ExecuteAsync(reporter);

            Assert.True(fakeAnalyzer.AnalyzeAsyncCalled);
            Assert.Same(fakeGit, fakeAnalyzer.ReceivedInput?.GitClient);
            Assert.Equal("/fake/root", fakeAnalyzer.ReceivedInput?.RepoRoot);
            Assert.Equal(0, result.ExitCode);
        }

        private class FakeGitClientForTesting : IGitClient
        {
            public Task<string?> GetRepositoryRootAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>("/fake/root");
            public Task<HashSet<string>> ListHeadFilesAsync(CancellationToken cancellationToken = default) => Task.FromResult(new HashSet<string>());
            public Task<System.Collections.Generic.List<GitCommitRecord>> ExtractHistoryAsync(GitHistoryExtractorOptions? options = null, CancellationToken cancellationToken = default) => Task.FromResult(new System.Collections.Generic.List<GitCommitRecord>());
        }

        private class FakeRepositoryAnalyzerForTesting : IRepositoryAnalyzer
        {
            public bool AnalyzeAsyncCalled { get; private set; } = false;
            public AnalyzeInput? ReceivedInput { get; private set; }

            public Task<AnalysisResult> AnalyzeAsync(AnalyzeInput input, CancellationToken cancellationToken = default)
            {
                AnalyzeAsyncCalled = true;
                ReceivedInput = input;
                return Task.FromResult(new AnalysisResult
                {
                    Analysis = new AnalysisMetadata { IncludedFileChangeCount = 1 },
                    Files = new System.Collections.Generic.List<FileMetric>(),
                    Diagnostics = new System.Collections.Generic.List<Diagnostic>()
                });
            }
        }
    }
}
