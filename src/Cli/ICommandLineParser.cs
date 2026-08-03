namespace Gitic;

public interface ICommandLineParser
{
    ParsedArgs Parse();
    ICliCommand ParseToCommand();
}

