using System.Threading;
using System.Threading.Tasks;

namespace Gitic;

public class ConfigCommand : ICliCommand
{
    private readonly ParsedArgs _parsed;

    private bool IsInitAction => _parsed.ConfigAction == "init";

    public ConfigCommand(ParsedArgs parsed)
    {
        _parsed = parsed;
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
