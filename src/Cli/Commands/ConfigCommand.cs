using System.Threading;
using System.Threading.Tasks;
using Kfc.Cli.Core;

namespace Gitic;

public class ConfigCommand : ICommand
{
    private readonly ParsedArgs _parsed;

    /// <summary>
    /// Gets a value indicating whether the config action is "init".
    /// </summary>
    private bool IsInitAction => string.Equals(_parsed.ConfigAction, "init", System.StringComparison.Ordinal);

    public ConfigCommand(ParsedArgs parsed)
    {
        _parsed = parsed;
    }

    public Task<CliResult> ExecuteAsync(IConsoleReporter reporter)
    {
        return ExecuteAsync(reporter, CancellationToken.None);
    }

    public Task<CliResult> ExecuteAsync(IConsoleReporter? reporter, CancellationToken cancellationToken = default)
    {
        if (!IsInitAction)
        {
            string errMsg = "config requires an action. Try: gitic config init\n";
            reporter?.WriteError(errMsg);
            return Task.FromResult(Cli.CliFailure(errMsg, exitCode: 2));
        }

        var engine = new ConfigurationEngine();
        string stdout = engine.RenderStarterConfig();
        reporter?.Write(stdout);
        return Task.FromResult(Cli.CliSuccess(stdout));
    }
}
