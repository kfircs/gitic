using System.Threading.Tasks;

namespace Gitic
{
    public interface IReportRenderer
    {
        Task<string> RenderAsync(AnalysisResult result);
    }
}
