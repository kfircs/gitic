using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Threading;

namespace Gitic;

public record CliResult(int ExitCode, string Stdout, string Stderr);

public static class Cli
{
    public static CliResult CliSuccess(string stdout, string stderr = "")
    {
        return new CliResult(0, stdout, stderr);
    }

    public static CliResult CliFailure(string stderr, int exitCode = 1)
    {
        return new CliResult(exitCode, "", stderr);
    }

    public static async Task<CliResult> RunCliAsync(string[] args, IConsoleReporter? reporter = null, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        ConsoleCancelEventHandler cancelHandler = (sender, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        Console.CancelKeyPress += cancelHandler;

        try
        {
            ICliCommand? command = ParseCommand(args, reporter, out CliResult? errorResult);
            if (errorResult != null)
            {
                return errorResult;
            }

            return await ExecuteCommandAsync(command!, reporter, cts.Token);
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }

    private static ICliCommand? ParseCommand(string[] args, IConsoleReporter? reporter, out CliResult? errorResult)
    {
        errorResult = null;
        ICommandLineParser parser = new CommandLineParser(args);
        try
        {
            return parser.ParseToCommand();
        }
        catch (CommandLineParseError error)
        {
            reporter?.WriteError($"{error.Message}\n");
            errorResult = CliFailure($"{error.Message}\n", exitCode: 2);
            return null;
        }
    }

    private static async Task<CliResult> ExecuteCommandAsync(ICliCommand command, IConsoleReporter? reporter, CancellationToken cancellationToken)
    {
        try
        {
            return await command.ExecuteAsync(reporter, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            string errMsg = "Operation cancelled.\n";
            reporter?.WriteError(errMsg);
            return CliFailure(errMsg, exitCode: 130);
        }
        catch (Exception ex)
        {
            string errMsg = $"Error: {ex.Message}\n";
            reporter?.WriteError(errMsg);
            return CliFailure(errMsg);
        }
    }
}
