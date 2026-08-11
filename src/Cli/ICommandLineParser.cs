using Kfc.Cli.Core;

namespace Gitic;

public interface ICommandLineParser
{
    ParsedArgs Parse();
    ICommand ParseToCommand();
}

