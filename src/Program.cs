using System;
using Gitic;
using Kfc.Cli.Terminal;

if (args.Length == 1 && (args[0] == "--version" || args[0] == "-v" || args[0] == "version"))
{
    Console.WriteLine(Cli.GetDisplayVersion());
    Environment.Exit(0);
}
if (args.Length == 1 && (args[0] == "--help" || args[0] == "-h" || args[0] == "help"))
{
    Console.Write(string.Format(HelpCommand.HelpTemplate, Cli.GetDisplayVersion()));
    Environment.Exit(0);
}

Kfc.Cli.Core.IConsoleReporter reporter = new ConsoleReporter();
var result = await Cli.RunCliAsync(args, reporter);

Environment.Exit(result.ExitCode);
