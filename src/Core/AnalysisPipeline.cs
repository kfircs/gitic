using System;
using System.Collections.Generic;
using System.Linq;

namespace Gitic;

public interface IAnalysisPipeline
{
    AnalysisPipelineResult Run(
        List<GitCommitRecord> commits,
        HashSet<string> headFiles,
        GiticConfig config,
        AnalysisSettings settings,
        AnalysisCommand command,
        System.Threading.CancellationToken cancellationToken = default);

    AnalysisPipelineResult Run(
        List<GitCommitRecord> commits,
        GiticConfig? config = null,
        AnalysisSettings? settings = null,
        AnalysisCommand command = AnalysisCommand.Hotspots,
        System.Threading.CancellationToken cancellationToken = default);
}

public class AnalysisPipelineResult
{
    public List<FileMetric> Files { get; set; } = new();
    public List<AreaMetric> Areas { get; set; } = new();
    public List<ContributorMetric> Contributors { get; set; } = new();
    public List<AutomationMetric> Automation { get; set; } = new();
    public List<TemporalCoupling> TemporalCouplings { get; set; } = new();
    public LeadTimesInfo LeadTimes { get; set; } = new();
    public List<ExclusionSummary> Exclusions { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<Diagnostic> Diagnostics { get; set; } = new();
    public int IncludedFileChangeCount { get; set; }

    public int TotalWarningCount => Warnings?.Count ?? 0;
    public bool HasWarnings => TotalWarningCount > 0;
    public bool HasDiagnostics => (Diagnostics?.Count ?? 0) > 0;
}

public class PipelineDependencies
{
    public IMetricsEngine? MetricsEngine { get; set; }
    public IFamiliarityScoringEngine? ScoringEngine { get; set; }
    public IWarningCollector? WarningCollector { get; set; }
    public IIdentityRegistry? IdentityRegistry { get; set; }
    public IChangeAccumulator? Accumulator { get; set; }
    public Func<HashSet<string>, List<ExcludeRule>, bool, string?, IPathClassifier>? PathClassifierFactory { get; set; }
    public Func<GiticConfig, AnalysisSettings, IPathClassifier, IIdentityRegistry, IChangeAccumulator>? ChangeAccumulatorFactory { get; set; }
    public Func<GiticConfig, HashSet<string>, int, IFamiliarityScoringEngine>? ScoringEngineFactory { get; set; }
}

public class AnalysisPipeline : IAnalysisPipeline
{
    private readonly IMetricsEngine _metricsEngine;
    private readonly IFamiliarityScoringEngine? _scoringEngine;
    private readonly IWarningCollector _warningCollector;
    private readonly IIdentityRegistry? _identityRegistry;
    private readonly IChangeAccumulator? _accumulator;
    private readonly Func<HashSet<string>, List<ExcludeRule>, bool, string?, IPathClassifier> _pathClassifierFactory;
    private readonly Func<GiticConfig, AnalysisSettings, IPathClassifier, IIdentityRegistry, IChangeAccumulator> _changeAccumulatorFactory;
    private readonly Func<GiticConfig, HashSet<string>, int, IFamiliarityScoringEngine> _scoringEngineFactory;

    public AnalysisPipeline(PipelineDependencies? deps = null)
    {
        deps ??= new PipelineDependencies();
        _metricsEngine = deps.MetricsEngine ?? new MetricsEngine();
        _scoringEngine = deps.ScoringEngine;
        _warningCollector = deps.WarningCollector ?? new WarningCollector();
        _identityRegistry = deps.IdentityRegistry;
        _accumulator = deps.Accumulator;
        _pathClassifierFactory = deps.PathClassifierFactory ?? ((headFiles, excludes, includeDeleted, requestedPath) => new PathClassifier(headFiles, excludes, includeDeleted, requestedPath));
        _changeAccumulatorFactory = deps.ChangeAccumulatorFactory ?? ((config, settings, pathClassifier, identityRegistry) => new ChangeAccumulator(config, settings, pathClassifier, identityRegistry));
        _scoringEngineFactory = deps.ScoringEngineFactory ?? ((config, activeContributorKeys, depth) => new FamiliarityScoringEngine(config, activeContributorKeys, depth));
    }

    public AnalysisPipelineResult Run(
        List<GitCommitRecord> commits,
        GiticConfig? config = null,
        AnalysisSettings? settings = null,
        AnalysisCommand command = AnalysisCommand.Hotspots,
        System.Threading.CancellationToken cancellationToken = default)
    {
        var actualConfig = config ?? GiticConfig.Default;
        var actualSettings = settings ?? new AnalysisSettings();
        return Run(
            commits,
            new HashSet<string>(),
            actualConfig,
            actualSettings,
            command,
            cancellationToken);
    }

    public AnalysisPipelineResult Run(
        List<GitCommitRecord> commits,
        HashSet<string> headFiles,
        GiticConfig config,
        AnalysisSettings settings,
        AnalysisCommand command,
        System.Threading.CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IPathClassifier pathClassifier = _pathClassifierFactory(headFiles, config.Excludes, settings.IncludeDeleted, settings.Path);
        bool mergeByEmail = (config.Identity?.MergeOnEmail == true) || (settings.MergeByEmail == true);
        var actualIdentityRegistry = _identityRegistry ?? new IdentityRegistry(config.Aliases, config.Bots, mergeByEmail);
        IChangeAccumulator actualAccumulator = _accumulator ?? _changeAccumulatorFactory(config, settings, pathClassifier, actualIdentityRegistry);
        var rawIncludedCommits = actualAccumulator.ProcessCommits(commits);
        var allIncludedCommits = rawIncludedCommits.Select(files => new CommitFileSet { Files = files }).ToList();

        int temporalCouplingLimit = config.Metrics?.TemporalCouplingMaxCommitFileCount ?? 20;
        var actualMetricsEngine = _metricsEngine;

        var activeContributorKeys = actualMetricsEngine.GetActiveContributorKeys(commits);
        IFamiliarityScoringEngine actualScoringEngine = _scoringEngine ?? _scoringEngineFactory(config, activeContributorKeys, settings.Depth);

        var rawFileMetrics = actualScoringEngine.ScoreFiles(actualAccumulator.GetFiles().Values.ToList(), settings.Depth);
        var areaMetrics = actualScoringEngine.ScoreAreas(actualAccumulator.GetAreas().Values.ToList());

        var contributorMetrics = actualMetricsEngine.RenderContributors(actualAccumulator.GetContributors().Values.ToList());
        var automationMetrics = actualMetricsEngine.RenderAutomation(actualAccumulator.GetAutomation().Values.ToList());

        var couplingConfig = new TemporalCouplingConfig { MaxCommitFileCount = temporalCouplingLimit };
        var couplingResult = actualMetricsEngine.CalculateTemporalCoupling(allIncludedCommits, couplingConfig);
        var leadTimeConfig = new LeadTimeConfig();
        var leadTimes = actualMetricsEngine.CalculateLeadTimes(commits, leadTimeConfig);

        IWarningCollector actualWarningCollector = _warningCollector;
        var warningContext = new WarningContext
        {
            EmailCollisions = actualAccumulator.GetEmailCollisions(),
            AliasCount = config.Aliases.Count,
            ConfiguredBotCount = config.Bots.Count,
            AutomationMetrics = automationMetrics,
            LeadTimes = leadTimes,
            TemporalCoupling = couplingResult,
            Files = rawFileMetrics
        };
        var warnings = actualWarningCollector.Collect(warningContext, actualAccumulator.GetWarnings().ToList());
        var diagnostics = actualWarningCollector.CollectDiagnostics(warningContext, actualAccumulator.GetWarnings().ToList());

        return new AnalysisPipelineResult
        {
            Files = rawFileMetrics,
            Areas = areaMetrics,
            Contributors = contributorMetrics,
            Automation = automationMetrics,
            TemporalCouplings = couplingResult.Couplings,
            LeadTimes = leadTimes,
            Exclusions = actualAccumulator.GetExclusions(),
            Warnings = warnings,
            Diagnostics = diagnostics,
            IncludedFileChangeCount = actualAccumulator.GetIncludedFileChangeCount()
        };
    }
}
