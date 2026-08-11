using System;
using Kfc.Cli.Core;

namespace Gitic;

/// <summary>
/// Simple routing factory that instantiates the appropriate ICommand based on ParsedArgs.
/// </summary>
public interface ICliCommandFactory
{
    ICommand CreateCommand(ParsedArgs parsed);
}

public class CliCommandFactoryImpl : ICliCommandFactory
{
    public ICommand CreateCommand(ParsedArgs parsed)
    {
        if (parsed == null) throw new ArgumentNullException(nameof(parsed));

        return parsed.Command?.ToLowerInvariant() switch
        {
            "help" => new HelpCommand(parsed.HelpText),
            "version" => new VersionCommand(),
            "config" => new ConfigCommand(parsed),
            "hotspots" => new HotspotsCommand(parsed),
            "areas" => new AreasCommand(parsed),
            "contributors" => new ContributorsCommand(parsed),
            "contributor" => new ContributorCommand(parsed),
            "report" => new ReportCommand(parsed),
            "wizard" => new WizardCommand(parsed),
            "temporal-coupling" => new TemporalCouplingCommand(parsed),
            "lead-time" => new LeadTimeCommand(parsed),
            "ge-report" => new GeReportCommand(parsed),
            _ => throw new CommandLineParseError($"Unknown command: {parsed.Command}")
        };
    }
}
