using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Gitic;

/// <summary>
/// Renders the analysis metrics and data in raw, indented JSON format using standard serialization defaults.
/// </summary>
public class JsonRenderer : IReportRenderer
{
    /// <summary>
    /// Asynchronously renders the analysis results into an indented JSON string.
    /// </summary>
    /// <param name="result">The analysis metrics and data.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A string task returning the formatted JSON string.</returns>
    public Task<string> RenderAsync(AnalysisResult result, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string json = $"{JsonSerializer.Serialize(result, JsonSerializationDefaults.Indented)}\n";
        return Task.FromResult(json);
    }
}
// Refactored: Candidate 11
// Clean code review completed.

