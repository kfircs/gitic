using System.Threading;
using System.Threading.Tasks;
using Kfc.Cli.Core;

namespace Gitic;

public class VersionCommand : IGiticCommand
{
    public Task<CliResult> ExecuteAsync(IConsoleReporter reporter)
    {
        return ExecuteAsync(reporter, CancellationToken.None);
    }

    public Task<CliResult> ExecuteAsync(IConsoleReporter? reporter, CancellationToken cancellationToken = default)
    {
        string displayVersion = Cli.GetDisplayVersion();

        string versionText = $"gitic version {displayVersion}\n";
        reporter?.Write(versionText);
        return Task.FromResult(Cli.CliSuccess(versionText));
    }
}
