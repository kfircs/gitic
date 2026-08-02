using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Gitic;

public class HtmlRenderer : IReportRenderer
{
    public HtmlRenderer()
    {
    }

    public async Task<string> RenderAsync(AnalysisResult result, CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        await RenderToStreamAsync(result, ms, cancellationToken);
        ms.Position = 0;
        using var reader = new StreamReader(ms, System.Text.Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    public async Task RenderToStreamAsync(AnalysisResult result, Stream output, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        string rawJson = JsonSerializer.Serialize(result, options);
        string data = rawJson.Replace("</script", "<\\/script", StringComparison.OrdinalIgnoreCase);

        await DashboardTemplateEngine.RenderToStreamAsync(data, output);
    }
}
