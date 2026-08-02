using System;

namespace Gitic;

public interface IAnalysisSettingsNormalizer
{
    AnalysisSettings Normalize(AnalysisSettings settings);
}

public class AnalysisSettingsNormalizer : IAnalysisSettingsNormalizer
{
    private const int UninitializedDepth = 0;

    public AnalysisSettings Normalize(AnalysisSettings settings)
    {
        var defaults = DefaultAnalysisSettings.Create();
        return new()
        {
            Json = settings.Json,
            AllTime = settings.AllTime,
            Since = settings.Since ?? defaults.Since,
            IncludeMerges = settings.IncludeMerges,
            IncludeDeleted = settings.IncludeDeleted,
            MergeByEmail = settings.MergeByEmail ?? defaults.MergeByEmail,
            Path = settings.Path ?? defaults.Path,
            Anonymize = settings.Anonymize,
            Depth = settings.Depth > UninitializedDepth ? settings.Depth : defaults.Depth,
            Format = string.IsNullOrEmpty(settings.Format) ? defaults.Format : settings.Format,
            Color = string.IsNullOrEmpty(settings.Color) ? defaults.Color : settings.Color
        };
    }
}
