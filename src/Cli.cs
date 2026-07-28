using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Gitic
{
    public class CliResult
    {
        public int ExitCode { get; init; }
        public string Stdout { get; init; } = string.Empty;
        public string Stderr { get; init; } = string.Empty;
    }

    public static class Cli
    {
        public static CliResult CliSuccess(string stdout, string stderr = "")
        {
            return new CliResult
            {
                ExitCode = 0,
                Stdout = stdout,
                Stderr = stderr
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

        public static async Task<CliResult> RunCliAsync(string[] args, IConsoleReporter? reporter = null)
        {
            ICommandLineParser parser = new CommandLineParser(args);
            ParsedArgs parsed;
            try
            {
                parsed = parser.Parse();
            }
            catch (CommandLineParseError error)
            {
                reporter?.WriteError($"{error.Message}\n");
                return CliFailure($"{error.Message}\n", exitCode: 2);
            }

            try
            {
                ICliCommand command = CliCommandFactory.CreateCommand(parsed);
                return await command.ExecuteAsync(reporter);
            }
            catch (Exception ex)
            {
                string errMsg = $"Error: {ex.Message}\n";
                reporter?.WriteError(errMsg);
                return CliFailure(errMsg);
            }
        }
    }
}
