using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Gitic;

/// <summary>
/// Renders analysis results as an interactive HTML visual dashboard.
/// Embeds serialized JSON data directly into the dashboard template for client-side rendering.
/// </summary>
public class HtmlRenderer : IReportRenderer
{
    // clean code refactor
    /// <summary>
    /// Asynchronously renders the analysis results into a complete HTML string.
    /// </summary>
    /// <param name="result">The analysis metrics and data result structure.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the HTML string result.</returns>
    public async Task<string> RenderAsync(AnalysisResult result, CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        await RenderToStreamAsync(result, ms, cancellationToken);
        ms.Position = 0;
        using var reader = new StreamReader(ms, System.Text.Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    /// <summary>
    /// Serializes the analysis result to JSON and writes the combined HTML dashboard template and data into the target stream.
    /// </summary>
    /// <param name="result">The analysis metrics and data.</param>
    /// <param name="output">The output stream where the HTML will be written.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous writing operation.</returns>
    public async Task RenderToStreamAsync(AnalysisResult result, Stream output, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string rawJson = JsonSerializer.Serialize(result, JsonSerializationDefaults.Compact);
        string data = rawJson.Replace("</script", "<\\/script", StringComparison.OrdinalIgnoreCase);

        await DashboardTemplateEngine.RenderToStreamAsync(data, output);
    }
}
// Refactored: Candidate 8
// Clean code review completed.
