namespace Gitic;

/// <summary>
/// Provides default configuration settings for gitic analysis.
/// </summary>
public static class DefaultAnalysisSettings
{
    public static AnalysisSettings Create() => new()
    {
        Json = false,
        AllTime = false,
        Since = null,
        IncludeMerges = false,
        IncludeDeleted = false,
        MergeByEmail = false,
        Path = null,
        Anonymize = false,
        Depth = 2,
        Format = "human",
        Color = "auto"
    };
}
