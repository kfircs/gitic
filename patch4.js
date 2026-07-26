const fs = require('fs');

let cliFactory = fs.readFileSync('src/CliCommandFactory.cs', 'utf8');
cliFactory = cliFactory.replace(/string stdout = ConfigLoader\.RenderStarterConfig\(\);/, 'var engine = new ConfigurationEngine();\n            string stdout = engine.RenderStarterConfig();');
const oldExecute = `            IConfigManager configManager = new ConfigManager();
            LoadedGitizerConfig loadedConfig;
            try
            {
                loadedConfig = await configManager.LoadGitizerConfigAsync(new LoadGitizerConfigOptions { RepoRoot = repoRoot });
            }
            catch (ConfigValidationError error)
            {
                string errMsg = $"Invalid Gitizer config:\\n{string.Join("\\n", error.Details)}\\n";
                reporter?.WriteError(errMsg);
                return Cli.CliFailure(errMsg);
            }

            var input = new AnalyzeInput
            {
                RepoRoot = repoRoot,
                Command = CommandType,
                Settings = Parsed.Settings,
                Config = loadedConfig.Config,
                ContributorName = Parsed.ContributorName
            };

            IRepositoryAnalyzer analyzer = new RepositoryAnalyzer();
            AnalysisResult result = await analyzer.AnalyzeAsync(input);`;
const newExecute = `            var input = new AnalyzeInput
            {
                RepoRoot = repoRoot,
                Command = CommandType,
                Settings = Parsed.Settings,
                ContributorName = Parsed.ContributorName
            };

            IRepositoryAnalyzer analyzer = new RepositoryAnalyzer();
            AnalysisResult result;
            try
            {
                result = await analyzer.AnalyzeAsync(input);
            }
            catch (ConfigValidationError error)
            {
                string errMsg = $"Invalid Gitizer config:\\n{string.Join("\\n", error.Details)}\\n";
                reporter?.WriteError(errMsg);
                return Cli.CliFailure(errMsg);
            }`;
cliFactory = cliFactory.replace(oldExecute, newExecute);
fs.writeFileSync('src/CliCommandFactory.cs', cliFactory);

let analyzer = fs.readFileSync('src/Analyzer.cs', 'utf8');
analyzer = analyzer.replace(/        public IConfigurationResolver\? ConfigurationResolver \{ get; set; \} = null;\r?\n/, '');
const oldObsolete = `        [Obsolete("Use IConfigurationResolver instead.")]
        public static AnalysisSettings NormalizeSettings(AnalysisSettings settings)
        {
            return new AnalysisSettingsNormalizer().Normalize(settings);
        }

`;
analyzer = analyzer.replace(oldObsolete, '');
analyzer = analyzer.replace(/            var resolver = input\.ConfigurationResolver \?\? new ConfigurationResolver\(\);\r?\n            var resolvedConfig = resolver\.Resolve\(input\);/, 
  '            var configEngine = new ConfigurationEngine();\n            var resolvedConfig = await configEngine.LoadAndResolveAsync(input);');
fs.writeFileSync('src/Analyzer.cs', analyzer);

let config = fs.readFileSync('src/Config.cs', 'utf8');
config = config.replace(/        private static readonly IConfigManager _manager = new ConfigManager\(\);\r?\n/, '');
const configLoaderMatch = `    public static class ConfigLoader
    {

        public static string RenderStarterConfig() => _manager.RenderStarterConfig();

        public static Task<LoadedGitizerConfig> LoadGitizerConfigAsync(LoadGitizerConfigOptions? options = null) =>
            _manager.LoadGitizerConfigAsync(options);

        public static GitizerConfig ApplyConfigOverrides(
            GitizerConfig baseConfig,
            GitizerConfigOverrides overrides) =>
            _manager.ApplyConfigOverrides(baseConfig, overrides);

        public static GitizerConfig CloneConfig(GitizerConfig config) =>
            _manager.CloneConfig(config);

        public static GitizerConfig MergeConfig(
            GitizerConfig baseConfig,
            GitizerConfigOverrides? overrideConfig = null) =>
            _manager.MergeConfig(baseConfig, overrideConfig);
    }`;
config = config.replace(configLoaderMatch, '');
fs.writeFileSync('src/Config.cs', config);

let cli = fs.readFileSync('src/Cli.cs', 'utf8');
cli = cli.replace(/ConfigLoader\.RenderStarterConfig\(\)/, 'new ConfigurationEngine().RenderStarterConfig()');
fs.writeFileSync('src/Cli.cs', cli);

if (fs.existsSync('src/ConfigManager.cs')) fs.unlinkSync('src/ConfigManager.cs');
if (fs.existsSync('src/ConfigurationResolver.cs')) fs.unlinkSync('src/ConfigurationResolver.cs');
