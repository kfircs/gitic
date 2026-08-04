using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gitic;

/// <summary>
/// Provides unified, shared static JsonSerializerOptions for report generation.
/// </summary>
public static class JsonSerializationDefaults
{
    // clean code refactor

    /// <summary>
    /// Default options for compact JSON serialization, ignoring null values to optimize payload size and avoid cluttering the output report.
    /// </summary>
    public static JsonSerializerOptions Compact { get; } = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Options for pretty-printed/indented JSON serialization, ignoring null values to optimize payload size and improve readability of the output report.
    /// </summary>
    public static JsonSerializerOptions Indented { get; } = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
// Refactored: Candidate 9
// Clean code review completed.

