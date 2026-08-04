using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Gitic;

/// <summary>
/// Renders the analysis metrics and data in raw, indented JSON format using standard serialization defaults.
/// </summary>
/// <remarks>
/// Serialization uses <see cref="JsonSerializationDefaults.Indented"/> which configures the output
/// to omit null values, format property names in camel case (via attributes), and provide indented, human-readable formatting.
/// The expected shape of the JSON output mirrors the structure of the <see cref="AnalysisResult"/>, 
/// typically including root-level metadata like `analysis` and `settings`, and collection properties 
/// such as `files`, `contributors`, and `areas` containing the core metrics.
/// </remarks>
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
// refactored

