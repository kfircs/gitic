using System.Threading;
using System.Threading.Tasks;
using Kfc.Cli.Core;

namespace Gitic;

public interface IGiticCommand : ICommand
{
    Task<CliResult> ExecuteAsync(IConsoleReporter? reporter, CancellationToken cancellationToken = default);
}
