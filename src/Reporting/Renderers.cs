using System.Threading;
using System.Threading.Tasks;

namespace Gitic;

/// <summary>
/// Defines the interface contract for report renderers.
/// All format-specific report renderers (HTML, Markdown, SVG, JSON) must implement this.
/// </summary>
public interface IReportRenderer
{
    // clean code refactor
    
    /// <summary>
    /// Asynchronously renders the analysis result into a string payload.
    /// </summary>
    /// <param name="result">The analysis metrics and data result structure.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous render operation, returning the rendered string content.</returns>
    Task<string> RenderAsync(AnalysisResult result, CancellationToken cancellationToken = default);
}

// Refactored: Candidate 1

