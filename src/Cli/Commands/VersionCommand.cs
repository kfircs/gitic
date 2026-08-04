using System.Threading;
using System.Threading.Tasks;

namespace Gitic;

public class VersionCommand : ICliCommand
{
    public Task<CliResult> ExecuteAsync(IConsoleReporter? reporter, CancellationToken cancellationToken = default)
    {
        string displayVersion = Cli.GetDisplayVersion();

        string versionText = $"gitic version {displayVersion}\n";
        reporter?.Write(versionText);
        return Task.FromResult(Cli.CliSuccess(versionText));
    }
}
