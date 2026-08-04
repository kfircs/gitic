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
    /// Default options for compact JSON serialization.
    /// Null values are intentionally ignored during serialization to minimize payload/file size of the generated reports, 
    /// and to prevent unpopulated or irrelevant metrics from cluttering the output JSON, making it clean and efficient to transmit or store.
    /// </summary>
    public static JsonSerializerOptions Compact { get; } = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Options for pretty-printed/indented JSON serialization.
    /// Null values are intentionally ignored to keep the output size minimal and improve overall readability of the report. 
    /// By excluding unpopulated properties, we avoid unnecessary clutter and visual noise, making the human-readable 
    /// formatted JSON significantly easier to inspect and debug.
    /// </summary>
    public static JsonSerializerOptions Indented { get; } = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
// Refactored: Candidate 9
// Clean code review completed.
// refactored


