using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Gitic;

public class JsonRenderer : IReportRenderer
{
    public Task<string> RenderAsync(AnalysisResult result, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        string json = JsonSerializer.Serialize(result, options) + "\n";
        return Task.FromResult(json);
    }
}
