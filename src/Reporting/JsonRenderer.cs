using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Gitic;

public class JsonRenderer : IReportRenderer
{
    public Task<string> RenderAsync(AnalysisResult result, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string json = $"{JsonSerializer.Serialize(result, JsonSerializationDefaults.Indented)}\n";
        return Task.FromResult(json);
    }
}
