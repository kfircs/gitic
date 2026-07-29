using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace Gitic
{
    public interface IConfigMerger
    {
        GiticConfig CloneDefaultConfig();
        GiticConfigOverrides ConvertToOverrides(GiticConfig config);
        GiticConfig CloneConfig(GiticConfig config);
        GiticConfig MergeConfig(GiticConfig baseConfig, GiticConfigOverrides? overrideConfig = null);
        
        // Deep Interface Overloads
        GiticConfig MergeConfig(GiticConfig baseConfig, GiticConfig? overrideConfig);
        GiticConfig MergeMultiple(GiticConfig baseConfig, params GiticConfigOverrides?[] overrides);
        GiticConfig MergeMultiple(GiticConfig baseConfig, params object?[] overridesOrConfigs);
    }

    public interface IConfigurationService
    {
        GiticConfig Merge(GiticConfig baseConfig, params object?[] overridesOrConfigs);
        AnalysisSettings NormalizeSettings(AnalysisSettings? settings);
        GiticConfigOverrides NormalizeOverrides(object? rawYamlDict, string source);
    }

    public class ConfigMerger : IConfigMerger
    {
        public GiticConfig CloneDefaultConfig()
        {
            return CloneConfig(GiticConfig.Default);
        }

        public GiticConfigOverrides ConvertToOverrides(GiticConfig config)
        {
            if (config == null) return new GiticConfigOverrides();
            return MapTo<GiticConfigOverrides>(config);
        }

        public GiticConfig CloneConfig(GiticConfig config)
        {
            if (config == null) return new GiticConfig();
            var json = JsonSerializer.Serialize(config);
            return JsonSerializer.Deserialize<GiticConfig>(json) ?? new GiticConfig();
        }

        public GiticConfig MergeConfig(GiticConfig baseConfig, GiticConfigOverrides? overrideConfig = null)
        {
            var cloned = CloneConfig(baseConfig);
            if (overrideConfig == null)
            {
                return cloned;
            }

            MergeObjects(cloned, overrideConfig);
            return cloned;
        }

        // Deep Interface Implementation: Overload that accepts GiticConfig directly, hiding manual override mapping
        public GiticConfig MergeConfig(GiticConfig baseConfig, GiticConfig? overrideConfig)
        {
            if (overrideConfig == null) return CloneConfig(baseConfig);
            return MergeConfig(baseConfig, ConvertToOverrides(overrideConfig));
        }

        // Deep Interface Implementation: Overload that handles chaining multiple typed overrides
        public GiticConfig MergeMultiple(GiticConfig baseConfig, params GiticConfigOverrides?[] overrides)
        {
            var cloned = CloneConfig(baseConfig);
            if (overrides == null) return cloned;

            foreach (var @override in overrides)
            {
                if (@override != null)
                {
                    MergeObjects(cloned, @override);
                }
            }
            return cloned;
        }

        // Deep Interface Implementation: Overload that handles both GiticConfig and GiticConfigOverrides polymorphically
        public GiticConfig MergeMultiple(GiticConfig baseConfig, params object?[] overridesOrConfigs)
        {
            var cloned = CloneConfig(baseConfig);
            if (overridesOrConfigs == null) return cloned;

            foreach (var item in overridesOrConfigs)
            {
                if (item == null) continue;

                if (item is GiticConfig configItem)
                {
                    MergeObjects(cloned, ConvertToOverrides(configItem));
                }
                else if (item is GiticConfigOverrides overridesItem)
                {
                    MergeObjects(cloned, overridesItem);
                }
            }
            return cloned;
        }

        private static TTarget MapTo<TTarget>(object source) where TTarget : class, new()
        {
            var target = new TTarget();
            MapProperties(source, target);
            return target;
        }

        private static void MapProperties(object source, object target)
        {
            if (source == null || target == null) return;

            var sourceProps = source.GetType().GetProperties();
            var targetProps = target.GetType().GetProperties();

            foreach (var sourceProp in sourceProps)
            {
                var targetProp = targetProps.FirstOrDefault(p => p.Name == sourceProp.Name);
                if (targetProp == null || !targetProp.CanWrite) continue;

                var sourceVal = sourceProp.GetValue(source);
                if (sourceVal == null) continue;

                // Case 1: Simple assignment if types match (e.g., lists like List<AliasRule>)
                if (targetProp.PropertyType.IsAssignableFrom(sourceProp.PropertyType))
                {
                    targetProp.SetValue(target, sourceVal);
                }
                // Case 2: Target is Nullable version of Source type (e.g., bool? and bool, double? and double, int? and int)
                else if (Nullable.GetUnderlyingType(targetProp.PropertyType) == sourceProp.PropertyType)
                {
                    targetProp.SetValue(target, sourceVal);
                }
                // Case 3: Nested objects (e.g., ScoringConfigOverrides and ScoringConfig)
                else if (targetProp.PropertyType.IsClass && sourceProp.PropertyType.IsClass &&
                         targetProp.PropertyType != typeof(string) && !typeof(IEnumerable).IsAssignableFrom(targetProp.PropertyType))
                {
                    var targetVal = targetProp.GetValue(target);
                    if (targetVal == null)
                    {
                        targetVal = Activator.CreateInstance(targetProp.PropertyType);
                        targetProp.SetValue(target, targetVal);
                    }
                    if (targetVal != null)
                    {
                        MapProperties(sourceVal, targetVal);
                    }
                }
            }
        }

        private static void MergeObjects(object target, object source)
        {
            if (target == null || source == null) return;

            var targetProperties = target.GetType().GetProperties();
            var sourceProperties = source.GetType().GetProperties();

            foreach (var sourceProp in sourceProperties)
            {
                var val = sourceProp.GetValue(source);
                if (val == null) continue;

                var targetProp = targetProperties.FirstOrDefault(p => p.Name == sourceProp.Name);
                if (targetProp == null || !targetProp.CanWrite) continue;

                // Case 1: Collection
                if (typeof(IList).IsAssignableFrom(targetProp.PropertyType) &&
                    val is IEnumerable sourceCollection)
                {
                    var targetList = targetProp.GetValue(target) as IList;
                    if (targetList == null)
                    {
                        targetList = Activator.CreateInstance(targetProp.PropertyType) as IList;
                        targetProp.SetValue(target, targetList);
                    }
                    if (targetList != null)
                    {
                        foreach (var item in sourceCollection)
                        {
                            targetList.Add(item);
                        }
                    }
                }
                // Case 2: Nullable primitive on source, non-nullable primitive on target
                else if (Nullable.GetUnderlyingType(sourceProp.PropertyType) != null)
                {
                    targetProp.SetValue(target, val);
                }
                // Case 3: Nested object
                else if (targetProp.PropertyType.IsClass && sourceProp.PropertyType.IsClass &&
                         targetProp.PropertyType != typeof(string))
                {
                    var targetVal = targetProp.GetValue(target);
                    if (targetVal != null)
                    {
                        MergeObjects(targetVal, val);
                    }
                }
            }
        }
    }

    public class ConfigurationService : IConfigurationService
    {
        private readonly IConfigMerger _merger;
        private readonly IConfigOverridesNormalizer _overridesNormalizer;
        private readonly IAnalysisSettingsNormalizer _settingsNormalizer;

        public ConfigurationService(IConfigMerger? merger = null, IConfigOverridesNormalizer? overridesNormalizer = null, IAnalysisSettingsNormalizer? settingsNormalizer = null)
        {
            _merger = merger ?? new ConfigMerger();
            _overridesNormalizer = overridesNormalizer ?? new ConfigOverridesNormalizer(new ConfigValidator());
            _settingsNormalizer = settingsNormalizer ?? new AnalysisSettingsNormalizer();
        }

        public GiticConfig Merge(GiticConfig baseConfig, params object?[] overridesOrConfigs)
        {
            return _merger.MergeMultiple(baseConfig, overridesOrConfigs);
        }

        public AnalysisSettings NormalizeSettings(AnalysisSettings? settings)
        {
            return _settingsNormalizer.Normalize(settings ?? new AnalysisSettings());
        }

        public GiticConfigOverrides NormalizeOverrides(object? rawYamlDict, string source)
        {
            return _overridesNormalizer.NormalizeOverride(rawYamlDict, source);
        }
    }
}
