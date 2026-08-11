using System;
using Gitic;
using Kfc.Cli.Terminal;

Kfc.Cli.Core.IConsoleReporter reporter = new ConsoleReporter();
var result = await Cli.RunCliAsync(args, reporter);

Environment.Exit(result.ExitCode);
