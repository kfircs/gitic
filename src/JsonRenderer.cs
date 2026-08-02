using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Gitic;

public class JsonRenderer : IReportRenderer
{
    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public Task<string> RenderAsync(AnalysisResult result, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string json = $"{JsonSerializer.Serialize(result, Options)}\n";
        return Task.FromResult(json);
    }
}
