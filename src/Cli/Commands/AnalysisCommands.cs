using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Gitic;

public class HotspotsCommand : StandardRenderAnalysisCommand
{
    public HotspotsCommand(ParsedArgs parsed, IGitClient? gitClient = null, IRepositoryAnalyzer? analyzer = null) 
        : base(parsed, gitClient, analyzer) { }
    protected override AnalysisCommand CommandType => AnalysisCommand.Hotspots;
}

public class AreasCommand : StandardRenderAnalysisCommand
{
    public AreasCommand(ParsedArgs parsed, IGitClient? gitClient = null, IRepositoryAnalyzer? analyzer = null) 
        : base(parsed, gitClient, analyzer) { }
    protected override AnalysisCommand CommandType => AnalysisCommand.Areas;
}

public class ContributorsCommand : StandardRenderAnalysisCommand
{
    public ContributorsCommand(ParsedArgs parsed, IGitClient? gitClient = null, IRepositoryAnalyzer? analyzer = null) 
        : base(parsed, gitClient, analyzer) { }
    protected override AnalysisCommand CommandType => AnalysisCommand.Contributors;
}

public class TemporalCouplingCommand : StandardRenderAnalysisCommand
{
    public TemporalCouplingCommand(ParsedArgs parsed, IGitClient? gitClient = null, IRepositoryAnalyzer? analyzer = null) 
        : base(parsed, gitClient, analyzer) { }
    protected override AnalysisCommand CommandType => AnalysisCommand.TemporalCoupling;
}

public class LeadTimeCommand : StandardRenderAnalysisCommand
{
    public LeadTimeCommand(ParsedArgs parsed, IGitClient? gitClient = null, IRepositoryAnalyzer? analyzer = null) 
        : base(parsed, gitClient, analyzer) { }
    protected override AnalysisCommand CommandType => AnalysisCommand.LeadTime;
}

public class ContributorCommand : StandardRenderAnalysisCommand
{
    public ContributorCommand(ParsedArgs parsed, IGitClient? gitClient = null, IRepositoryAnalyzer? analyzer = null) 
        : base(parsed, gitClient, analyzer) { }
    protected override AnalysisCommand CommandType => AnalysisCommand.Contributor;

    protected override async Task<CliResult> ProcessResultAsync(AnalysisResult result, IConsoleReporter? reporter, CancellationToken cancellationToken = default)
    {
        IContributorLookupRegistry registry = new ContributorLookupRegistry(result.Contributors);
        try
        {
            var filtered = registry.Find(Parsed.ContributorName ?? "");
            result.Contributors = new List<ContributorMetric> { filtered };
        }
        catch (Exception ex) when (ex is ContributorNotFoundError || ex is AmbiguousContributorError)
        {
            reporter?.WriteError($"{ex.Message}\n");
            return Cli.CliFailure($"{ex.Message}\n");
        }

        return await base.ProcessResultAsync(result, reporter, cancellationToken);
    }
}

public class GeReportCommand : BaseAnalysisCommand
{
    public GeReportCommand(ParsedArgs parsed, IGitClient? gitClient = null, IRepositoryAnalyzer? analyzer = null) 
        : base(parsed, gitClient, analyzer) { }
    protected override AnalysisCommand CommandType => AnalysisCommand.GeReport;

    protected override async Task<CliResult> ProcessResultAsync(AnalysisResult result, IConsoleReporter? reporter, CancellationToken cancellationToken = default)
    {
        var geRenderer = new GeReportRenderer();
        string mdContent = await geRenderer.RenderAsync(result, cancellationToken);
        
        reporter?.Write(mdContent);
        return Cli.CliSuccess(mdContent);
    }
}
