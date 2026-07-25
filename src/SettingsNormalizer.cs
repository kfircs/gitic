using System;

namespace Gitic
{
    public interface IAnalysisSettingsNormalizer
    {
        AnalysisSettings Normalize(AnalysisSettings settings);
    }

    public class AnalysisSettingsNormalizer : IAnalysisSettingsNormalizer
    {
        public AnalysisSettings Normalize(AnalysisSettings settings)
        {
            var defaults = DefaultAnalysisSettings.Create();
            return new AnalysisSettings
            {
                Json = settings.Json,
                AllTime = settings.AllTime,
                Since = settings.Since ?? defaults.Since,
                IncludeMerges = settings.IncludeMerges,
                IncludeDeleted = settings.IncludeDeleted,
                MergeByEmail = settings.MergeByEmail ?? defaults.MergeByEmail,
                Path = settings.Path ?? defaults.Path,
                Anonymize = settings.Anonymize,
                Depth = settings.Depth != 0 ? settings.Depth : defaults.Depth
            };
        }
    }
}
