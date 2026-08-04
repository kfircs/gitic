using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Gitic;

public class VersionCommand : ICliCommand
{
    public Task<CliResult> ExecuteAsync(IConsoleReporter? reporter, CancellationToken cancellationToken = default)
    {
        var assembly = typeof(Cli).Assembly;
        string displayVersion = GetDisplayVersion(assembly);

        string versionText = $"gitic version {displayVersion}\n";
        reporter?.Write(versionText);
        return Task.FromResult(Cli.CliSuccess(versionText));
    }

    private string GetDisplayVersion(Assembly assembly)
    {
        var version = assembly.GetName().Version?.ToString(3) ?? "0.1.0";
        var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return string.IsNullOrEmpty(infoVersion) ? version : infoVersion;
    }
}
