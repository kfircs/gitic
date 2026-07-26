using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Gitic
{
    public interface IConfigurationEngine
    {
        string RenderStarterConfig();
        Task<ResolvedConfiguration> LoadAndResolveAsync(AnalyzeInput input, LoadGitizerConfigOptions? options = null);
    }

    public class ResolvedConfiguration
    {
        public AnalysisSettings Settings { get; init; } = new();
        public GitizerConfig Config { get; init; } = GitizerConfig.Default;
    }

    public class ConfigurationEngine : IConfigurationEngine
    {
        private readonly IConfigValidator _validator;
        private readonly IYamlParser _yamlParser;
        private readonly AnalysisSettingsNormalizer _normalizer;
        private readonly IConfigMerger _configMerger;
        private readonly IConfigOverridesNormalizer _overridesNormalizer;

        public ConfigurationEngine(IYamlParser? yamlParser = null, IConfigMerger? configMerger = null)
        {
            _validator = new ConfigValidator();
            _yamlParser = yamlParser ?? new YamlSubsetParserImpl();
            _normalizer = new AnalysisSettingsNormalizer();
            _configMerger = configMerger ?? new ConfigMerger();
            _overridesNormalizer = new ConfigOverridesNormalizer(_validator);
        }

        public string RenderStarterConfig()
        {
            var attention = GitizerConfig.Default.Scoring.Attention;
            return
                "aliases: []\n" +
                "bots: []\n" +
                "excludes: []\n" +
                "areas: []\n" +
                "scoring:\n" +
                "  attention:\n" +
                $"    churn: {attention.Churn.ToString("F2")}\n" +
                $"    recency: {attention.Recency.ToString("F2")}\n" +
                $"    contributor_spread: {attention.ContributorSpread.ToString("F2")}\n" +
                $"    low_familiarity_concentration: {attention.LowFamiliarityConcentration.ToString("F2")}\n" +
                "identity:\n" +
                "  # merge_on_email: merge contributor identities sharing an email (case-insensitive).\n" +
                "  # Default false (raw by default, PRD US-16). Set true to collapse same-person identities.\n" +
                "  merge_on_email: false\n" +
                "metrics:\n" +
                "  # temporal_coupling_max_commit_file_count: max files a commit may touch before being\n" +
                "  # skipped for temporal coupling analysis. Commits above this limit trigger a warning.\n" +
                "  temporal_coupling_max_commit_file_count: 20\n";
        }

        public async Task<ResolvedConfiguration> LoadAndResolveAsync(AnalyzeInput input, LoadGitizerConfigOptions? options = null)
        {
            options ??= new LoadGitizerConfigOptions { RepoRoot = input.RepoRoot };
            
            var loadedConfig = await LoadGitizerConfigInternalAsync(options);
            var mergedConfig = input.Config != null 
                ? _configMerger.MergeConfig(loadedConfig.Config, _configMerger.ConvertToOverrides(input.Config)) 
                : loadedConfig.Config;

            var settings = _normalizer.Normalize(input.Settings ?? new AnalysisSettings());

            return new ResolvedConfiguration
            {
                Settings = settings,
                Config = mergedConfig
            };
        }

        private async Task<LoadedGitizerConfig> LoadGitizerConfigInternalAsync(LoadGitizerConfigOptions options)
        {
            string userHome = options.UserHome ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string userConfigPath = options.UserConfigPath ?? Path.Combine(userHome, ".config", "gitizer", "config.yml");
            string? repoConfigPath = options.RepoConfigPath ?? (options.RepoRoot == null ? null : Path.Combine(options.RepoRoot, ".gitizer.yml"));

            string? userConfigRaw = await ReadOptionalUtf8Async(userConfigPath);
            string? repoConfigRaw = repoConfigPath == null ? null : await ReadOptionalUtf8Async(repoConfigPath);

            GitizerConfigOverrides? userOverride = userConfigRaw == null
                ? null
                : ParseAndValidateOverride(userConfigRaw, $"user config ({userConfigPath})");

            GitizerConfigOverrides? repoOverride = repoConfigRaw == null
                ? null
                : ParseAndValidateOverride(repoConfigRaw, $"repo config ({repoConfigPath})");

            var merged = _configMerger.MergeConfig(
                _configMerger.MergeConfig(_configMerger.CloneDefaultConfig(), userOverride),
                repoOverride
            );

            _validator.ValidateAttentionWeights(merged.Scoring.Attention, "effective config");

            return new LoadedGitizerConfig
            {
                Config = merged,
                Sources = new ConfigSources
                {
                    User = userConfigRaw == null ? null : userConfigPath,
                    Repo = repoConfigRaw == null ? null : repoConfigPath
                }
            };
        }

        private static async Task<string?> ReadOptionalUtf8Async(string path)
        {
            try
            {
                return await File.ReadAllTextAsync(path);
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            catch (DirectoryNotFoundException)
            {
                return null;
            }
        }

        private GitizerConfigOverrides ParseAndValidateOverride(string content, string source)
        {
            var parsed = _yamlParser.Parse(content, source);
            return _overridesNormalizer.NormalizeOverride(parsed, source);
        }
    }
}
