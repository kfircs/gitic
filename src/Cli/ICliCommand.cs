using System.Threading;
using System.Threading.Tasks;

namespace Gitic;

public interface ICliCommand
{
    Task<CliResult> ExecuteAsync(IConsoleReporter? reporter, CancellationToken cancellationToken = default);
}
