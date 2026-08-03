using System;
using Gitic;

IConsoleReporter reporter = new ConsoleReporter();
var result = await Cli.RunCliAsync(args, reporter);

Environment.Exit(result.ExitCode);
