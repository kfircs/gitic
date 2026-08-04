using System.Threading;
using System.Threading.Tasks;

namespace Gitic;

public interface IReportRenderer
{
    // clean code refactor
    Task<string> RenderAsync(AnalysisResult result, CancellationToken cancellationToken = default);
}
