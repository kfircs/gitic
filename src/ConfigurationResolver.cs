using System;

namespace Gitic
{
    public interface IConfigurationResolver
    {
        ResolvedConfiguration Resolve(AnalyzeInput input);
    }

    public class ResolvedConfiguration
    {
        public AnalysisSettings Settings { get; set; } = new();
        public GitizerConfig Config { get; set; } = GitizerConfig.Default;
    }

    public class ConfigurationResolver : IConfigurationResolver
    {
        public ResolvedConfiguration Resolve(AnalyzeInput input)
        {
            var normalizer = new AnalysisSettingsNormalizer();
            var settings = normalizer.Normalize(input.Settings ?? new AnalysisSettings());
            var config = input.Config ?? GitizerConfig.Default;

            return new ResolvedConfiguration
            {
                Settings = settings,
                Config = config
            };
        }
    }
}
