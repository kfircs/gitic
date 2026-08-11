using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Gitic;

/// <summary>
/// Defines the engine for loading, merging, and resolving Gitic configuration.
/// </summary>
public interface IConfigurationEngine
{
    string RenderStarterConfig();
    Task<ResolvedConfiguration> LoadAndResolveAsync(AnalyzeInput input, LoadGiticConfigOptions? options = null);
}

public class ResolvedConfiguration
{
    public AnalysisSettings Settings { get; init; } = new();
    public GiticConfig Config { get; init; } = GiticConfig.Default;
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
        var attention = GiticConfig.Default.Scoring.Attention;
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

    public async Task<ResolvedConfiguration> LoadAndResolveAsync(AnalyzeInput input, LoadGiticConfigOptions? options = null)
    {
        options ??= new LoadGiticConfigOptions { RepoRoot = input.RepoRoot };

        var loadedConfig = await LoadGiticConfigInternalAsync(options);
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

    private async Task<LoadedGiticConfig> LoadGiticConfigInternalAsync(LoadGiticConfigOptions options)
    {
        string userHome = options.UserHome ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        string userConfigPath = GetUserConfigPath(options, userHome);
        string? repoConfigPath = GetRepoConfigPath(options);

        string? userConfigRaw = await ReadOptionalUtf8Async(userConfigPath);
        string? repoConfigRaw = repoConfigPath == null ? null : await ReadOptionalUtf8Async(repoConfigPath);

        GiticConfigOverrides? userOverride = userConfigRaw == null
            ? null
            : ParseAndValidateOverride(userConfigRaw, $"user config ({userConfigPath})");

        GiticConfigOverrides? repoOverride = repoConfigRaw == null
            ? null
            : ParseAndValidateOverride(repoConfigRaw, $"repo config ({repoConfigPath})");

        var merged = _configMerger.MergeConfig(
            _configMerger.MergeConfig(_configMerger.CloneDefaultConfig(), userOverride),
            repoOverride
        );

        _validator.ValidateAttentionWeights(merged.Scoring.Attention, "effective config");

        return new LoadedGiticConfig
        {
            Config = merged,
            Sources = new ConfigSources
            {
                User = userConfigRaw == null ? null : userConfigPath,
                Repo = repoConfigRaw == null ? null : repoConfigPath
            }
        };
    }

    private static string GetUserConfigPath(LoadGiticConfigOptions options, string userHome)
    {
        if (options.UserConfigPath != null)
        {
            return options.UserConfigPath;
        }

        string preferredUserPath = Path.Combine(userHome, ".config", "gitic", "config.yml");
        string fallbackUserPath = Path.Combine(userHome, ".config", "gitizer", "config.yml");
        if (File.Exists(preferredUserPath))
        {
            return preferredUserPath;
        }
        if (File.Exists(fallbackUserPath))
        {
            Console.Error.WriteLine($"Warning: Using legacy user configuration file at '{fallbackUserPath}'. Please migrate to '{preferredUserPath}'.");
            return fallbackUserPath;
        }
        return preferredUserPath;
    }

    private static string? GetRepoConfigPath(LoadGiticConfigOptions options)
    {
        if (options.RepoConfigPath != null)
        {
            return options.RepoConfigPath;
        }
        if (options.RepoRoot == null)
        {
            return null;
        }

        string preferredRepoPath = Path.Combine(options.RepoRoot, ".gitic.yml");
        string fallbackRepoPath = Path.Combine(options.RepoRoot, ".gitizer.yml");
        if (File.Exists(preferredRepoPath))
        {
            return preferredRepoPath;
        }
        if (File.Exists(fallbackRepoPath))
        {
            Console.Error.WriteLine($"Warning: Using legacy repository configuration file at '{fallbackRepoPath}'. Please migrate to '{preferredRepoPath}'.");
            return fallbackRepoPath;
        }
        return preferredRepoPath;
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

    private GiticConfigOverrides ParseAndValidateOverride(string content, string source)
    {
        var parsed = _yamlParser.Parse(content, source);
        return _overridesNormalizer.NormalizeOverride(parsed, source);
    }
}
