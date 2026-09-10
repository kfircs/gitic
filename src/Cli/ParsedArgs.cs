using System;
using System.Collections.Generic;

namespace Gitic;

public class ParsedArgs
{
    public string Command { get; init; } = string.Empty;
    public string RepoPath { get; init; } = ".";
    public AnalysisSettings Settings { get; init; } = new();
    public string? ContributorName { get; init; }
    public string? HtmlPath { get; init; }
    public string? MdPath { get; init; }
    public string? SvgPath { get; init; }
    public string? ConfigAction { get; init; }
    public string? HelpText { get; init; }
    public string? BaselineSubcommand { get; init; }
    public string? BaselineTag { get; init; }
    public string? BaselineFrom { get; init; }
    public string? BaselineTo { get; init; }
    public string? BaselineMetric { get; init; }
    public string? BaselineArea { get; init; }
    public string? BaselineStorageDir { get; init; }
    public string? DepartureRiskDeveloper { get; init; }
    public bool ImpactStaged { get; init; }
    public bool ImpactInstallHook { get; init; }
    public double ImpactWarnThreshold { get; init; } = 0.5;
    public IReadOnlyList<string> ImpactTargetFiles { get; init; } = Array.Empty<string>();
    public string? GateBaseline { get; init; }
    public string? GateFormat { get; init; }
    public bool GateFailFast { get; init; }
    public string? GateConfig { get; init; }
    public string? GateStorageDir { get; init; }
    public string? SprintBaseline { get; init; }
    public string? SprintCurrent { get; init; }
    public string? SprintPeriod { get; init; }
    public string? SprintStorageDir { get; init; }
    public bool Trajectory { get; init; }
    public string? TrajectoryDeveloper { get; init; }
    public IReadOnlyList<string> RawArgs { get; init; } = Array.Empty<string>();
}
