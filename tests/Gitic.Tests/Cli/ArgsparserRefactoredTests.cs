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
    }
}
