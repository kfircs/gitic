using System;
using Gitic;

var result = await Cli.RunCliAsync(args);

if (!string.IsNullOrEmpty(result.Stdout))
{
    Console.Write(result.Stdout);
}

if (!string.IsNullOrEmpty(result.Stderr))
{
    Console.Error.Write(result.Stderr);
}

Environment.Exit(result.ExitCode);
