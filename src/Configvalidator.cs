using System;
using System.Collections.Generic;
using System.Linq;

namespace Gitic
{
    public static class DefaultAttentionWeights
    {
        public const double Churn = 0.35;
        public const double Recency = 0.30;
        public const double ContributorSpread = 0.20;
        public const double LowFamiliarityConcentration = 0.15;

        public static AttentionWeights Create() => new()
        {
            Churn = Churn,
            Recency = Recency,
            ContributorSpread = ContributorSpread,
            LowFamiliarityConcentration = LowFamiliarityConcentration
        };
    }

    public interface IConfigValidator
    {
        void ValidateAttentionWeights(AttentionWeights attention, string source, List<string>? errors = null);
        GitizerConfigOverrides NormalizeOverride(object? input, string source);
    }

    public class ConfigValidator : IConfigValidator
    {
        public static readonly List<string> TopLevelKeys = new()
        {
            "aliases",
            "bots",
            "excludes",
            "areas",
            "scoring",
            "identity",
            "metrics"
        };

        public static readonly List<string> AttentionWeightKeys = new()
        {
            "churn",
            "recency",
            "contributor_spread",
            "low_familiarity_concentration"
        };

        public void ValidateAttentionWeights(
            AttentionWeights attention,
            string source,
            List<string>? errors = null)
        {
            errors ??= new List<string>();

            ValidateWeightValue(attention.Churn, "churn", source, errors);
            ValidateWeightValue(attention.Recency, "recency", source, errors);
            ValidateWeightValue(attention.ContributorSpread, "contributor_spread", source, errors);
            ValidateWeightValue(attention.LowFamiliarityConcentration, "low_familiarity_concentration", source, errors);

            double sum = attention.Churn + attention.Recency + attention.ContributorSpread + attention.LowFamiliarityConcentration;
            if (Math.Abs(sum - 1.0) > 0.0001)
            {
                errors.Add($"{source}: scoring.attention weights must sum to 1. Current sum: {sum.ToString("F6")}.");
            }

            if (errors.Count > 0)
            {
                throw new ConfigValidationError(errors);
            }
        }

        public GitizerConfigOverrides NormalizeOverride(object? input, string source)
        {
            var errors = new List<string>();
            if (!IsRecord(input, out var record))
            {
                throw new ConfigValidationError(new List<string> { $"{source}: expected a top-level mapping object." });
            }

            CheckUnknownKeys(record, TopLevelKeys, "", source, errors);

            var @override = new GitizerConfigOverrides();

            if (record.TryGetValue("aliases", out var aliasesVal))
            {
                @override.Aliases = NormalizeAliases(aliasesVal, source, errors);
            }
            if (record.TryGetValue("bots", out var botsVal))
            {
                @override.Bots = NormalizeBots(botsVal, source, errors);
            }
            if (record.TryGetValue("excludes", out var excludesVal))
            {
                @override.Excludes = NormalizeExcludes(excludesVal, source, errors);
            }
            if (record.TryGetValue("areas", out var areasVal))
            {
                @override.Areas = NormalizeAreas(areasVal, source, errors);
            }
            if (record.TryGetValue("scoring", out var scoringVal))
            {
                @override.Scoring = NormalizeScoring(scoringVal, source, errors);
            }
            if (record.TryGetValue("identity", out var identityVal))
            {
                @override.Identity = NormalizeIdentityConfig(identityVal, source, errors);
            }
            if (record.TryGetValue("metrics", out var metricsVal))
            {
                @override.Metrics = NormalizeMetricsConfig(metricsVal, source, errors);
            }

            if (errors.Count > 0)
            {
                throw new ConfigValidationError(errors);
            }

            return @override;
        }

        private static bool RequireRecord(
            object? value,
            string errorMsg,
            List<string> errors,
            out Dictionary<string, object?> record)
        {
            if (!IsRecord(value, out record))
            {
                errors.Add(errorMsg);
                return false;
            }
            return true;
        }

        private static bool RequireArray(
            object? value,
            string errorMsg,
            List<string> errors,
            out List<object?> array)
        {
            if (value is List<object?> list)
            {
                array = list;
                return true;
            }
            errors.Add(errorMsg);
            array = new List<object?>();
            return false;
        }

        private static bool RequireNonEmptyArray(
            object? value,
            string errorMsg,
            List<string> errors,
            out List<object?> array)
        {
            if (value is List<object?> list && list.Count > 0)
            {
                array = list;
                return true;
            }
            errors.Add(errorMsg);
            array = new List<object?>();
            return false;
        }

        private static string? RequireNonEmptyString(
            object? value,
            string path,
            List<string> errors)
        {
            string? result = NormalizeNonEmptyString(value);
            if (result == null)
            {
                errors.Add($"{path} must be a non-empty string.");
            }
            return result;
        }

        private static void CheckUnknownKeys(
            Dictionary<string, object?> entry,
            List<string> allowed,
            string path,
            string source,
            List<string> errors)
        {
            var unknown = entry.Keys.Where(k => !allowed.Contains(k)).ToList();
            foreach (var key in unknown)
            {
                if (string.IsNullOrEmpty(path))
                {
                    errors.Add($"{source}: unknown top-level key \"{key}\". Allowed keys: {string.Join(", ", allowed)}.");
                }
                else
                {
                    errors.Add($"{source}: {path} has unknown key \"{key}\".");
                }
            }
        }

        private static bool ValidateWeightValue(
            object? value,
            string key,
            string source,
            List<string> errors)
        {
            double numVal;
            if (value is double d)
            {
                numVal = d;
            }
            else if (value is long l)
            {
                numVal = l;
            }
            else if (value is int i)
            {
                numVal = i;
            }
            else
            {
                errors.Add($"{source}: scoring.attention.{key} must be a finite number.");
                return false;
            }

            if (double.IsNaN(numVal) || double.IsInfinity(numVal))
            {
                errors.Add($"{source}: scoring.attention.{key} must be a finite number.");
                return false;
            }

            if (numVal < 0.0 || numVal > 1.0)
            {
                errors.Add($"{source}: scoring.attention.{key} must be between 0 and 1.");
                return false;
            }

            return true;
        }

        private static List<AliasRule> NormalizeAliases(object? value, string source, List<string> errors)
        {
            if (!RequireArray(value, $"{source}: aliases must be an array.", errors, out var array))
            {
                return new List<AliasRule>();
            }

            var aliases = new List<AliasRule>();
            for (int i = 0; i < array.Count; i++)
            {
                object? entry = array[i];
                string path = $"aliases[{i}]";
                if (!RequireRecord(entry, $"{source}: {path} must be an object.", errors, out var entryRecord))
                {
                    continue;
                }

                CheckUnknownKeys(entryRecord, new List<string> { "canonical", "identities" }, path, source, errors);

                entryRecord.TryGetValue("canonical", out var canonicalVal);
                GitIdentity? canonical = NormalizeIdentity(canonicalVal, $"{path}.canonical", source, errors);

                entryRecord.TryGetValue("identities", out var identitiesVal);
                if (!RequireNonEmptyArray(identitiesVal, $"{source}: {path}.identities must be a non-empty array.", errors, out var identitiesArr))
                {
                    continue;
                }

                var identities = new List<GitIdentity>();
                for (int j = 0; j < identitiesArr.Count; j++)
                {
                    var id = NormalizeIdentity(identitiesArr[j], $"{path}.identities[{j}]", source, errors);
                    if (id != null)
                    {
                        identities.Add(id);
                    }
                }

                if (canonical != null && identities.Count == identitiesArr.Count)
                {
                    aliases.Add(new AliasRule
                    {
                        Canonical = canonical,
                        Identities = identities
                    });
                }
            }

            return aliases;
        }

        private static GitIdentity? NormalizeIdentity(
            object? value,
            string path,
            string source,
            List<string> errors)
        {
            if (!RequireRecord(value, $"{source}: {path} must be an object with name and email.", errors, out var record))
            {
                return null;
            }

            CheckUnknownKeys(record, new List<string> { "name", "email" }, path, source, errors);

            record.TryGetValue("name", out var nameVal);
            record.TryGetValue("email", out var emailVal);

            string? name = RequireNonEmptyString(nameVal, $"{source}: {path}.name", errors);
            string? email = RequireNonEmptyString(emailVal, $"{source}: {path}.email", errors);

            if (name == null || email == null)
            {
                return null;
            }

            return new GitIdentity { Name = name, Email = email };
        }

        private static List<BotRule> NormalizeBots(object? value, string source, List<string> errors)
        {
            if (!RequireArray(value, $"{source}: bots must be an array.", errors, out var array))
            {
                return new List<BotRule>();
            }

            var bots = new List<BotRule>();
            for (int i = 0; i < array.Count; i++)
            {
                object? entry = array[i];
                string path = $"bots[{i}]";
                if (!RequireRecord(entry, $"{source}: {path} must be an object.", errors, out var record))
                {
                    continue;
                }

                CheckUnknownKeys(record, new List<string> { "name", "email", "pattern" }, path, source, errors);

                record.TryGetValue("name", out var nameVal);
                record.TryGetValue("email", out var emailVal);
                record.TryGetValue("pattern", out var patternVal);

                string? name = NormalizeOptionalString(nameVal, $"{source}: {path}.name", errors);
                string? email = NormalizeOptionalString(emailVal, $"{source}: {path}.email", errors);
                string? pattern = NormalizeOptionalString(patternVal, $"{source}: {path}.pattern", errors);

                if (name == null && email == null && pattern == null)
                {
                    errors.Add($"{source}: {path} must define at least one of name, email, or pattern.");
                    continue;
                }

                bots.Add(new BotRule
                {
                    Name = name,
                    Email = email,
                    Pattern = pattern
                });
            }

            return bots;
        }

        private static List<ExcludeRule> NormalizeExcludes(
            object? value,
            string source,
            List<string> errors)
        {
            if (!RequireArray(value, $"{source}: excludes must be an array.", errors, out var array))
            {
                return new List<ExcludeRule>();
            }

            var excludes = new List<ExcludeRule>();
            for (int i = 0; i < array.Count; i++)
            {
                object? entry = array[i];
                string path = $"excludes[{i}]";
                if (!RequireRecord(entry, $"{source}: {path} must be an object.", errors, out var record))
                {
                    continue;
                }

                CheckUnknownKeys(record, new List<string> { "pattern", "category" }, path, source, errors);

                record.TryGetValue("pattern", out var patternVal);
                record.TryGetValue("category", out var categoryVal);

                string? pattern = RequireNonEmptyString(patternVal, $"{source}: {path}.pattern", errors);
                string? category = RequireNonEmptyString(categoryVal, $"{source}: {path}.category", errors);

                if (pattern != null && category != null)
                {
                    excludes.Add(new ExcludeRule { Pattern = pattern, Category = category });
                }
            }

            return excludes;
        }

        private static List<NamedArea> NormalizeAreas(object? value, string source, List<string> errors)
        {
            if (!RequireArray(value, $"{source}: areas must be an array.", errors, out var array))
            {
                return new List<NamedArea>();
            }

            var areas = new List<NamedArea>();
            for (int i = 0; i < array.Count; i++)
            {
                object? entry = array[i];
                string path = $"areas[{i}]";
                if (!RequireRecord(entry, $"{source}: {path} must be an object.", errors, out var record))
                {
                    continue;
                }

                CheckUnknownKeys(record, new List<string> { "name", "paths" }, path, source, errors);

                record.TryGetValue("name", out var nameVal);
                string? name = RequireNonEmptyString(nameVal, $"{source}: {path}.name", errors);

                record.TryGetValue("paths", out var pathsVal);
                if (!RequireNonEmptyArray(pathsVal, $"{source}: {path}.paths must be a non-empty array of strings.", errors, out var pathsArr))
                {
                    continue;
                }

                var paths = new List<string>();
                for (int j = 0; j < pathsArr.Count; j++)
                {
                    string? candidate = RequireNonEmptyString(pathsArr[j], $"{source}: {path}.paths[{j}]", errors);
                    if (candidate != null)
                    {
                        paths.Add(candidate);
                    }
                }

                if (name != null && paths.Count == pathsArr.Count)
                {
                    areas.Add(new NamedArea { Name = name, Paths = paths });
                }
            }

            return areas;
        }

        private static IdentityConfigOverrides NormalizeIdentityConfig(
            object? value,
            string source,
            List<string> errors)
        {
            var result = new IdentityConfigOverrides { MergeOnEmail = false };
            if (!RequireRecord(value, $"{source}: identity must be an object.", errors, out var record))
            {
                return result;
            }

            CheckUnknownKeys(record, new List<string> { "merge_on_email" }, "identity", source, errors);

            if (record.TryGetValue("merge_on_email", out var raw))
            {
                if (raw is bool b)
                {
                    result.MergeOnEmail = b;
                }
                else
                {
                    errors.Add($"{source}: identity.merge_on_email must be a boolean.");
                }
            }

            return result;
        }

        private static MetricsConfigOverrides NormalizeMetricsConfig(
            object? value,
            string source,
            List<string> errors)
        {
            var result = new MetricsConfigOverrides { TemporalCouplingMaxCommitFileCount = 20 };
            if (!RequireRecord(value, $"{source}: metrics must be an object.", errors, out var record))
            {
                return result;
            }

            CheckUnknownKeys(record, new List<string> { "temporal_coupling_max_commit_file_count" }, "metrics", source, errors);

            if (record.TryGetValue("temporal_coupling_max_commit_file_count", out var raw))
            {
                int val = 20;
                bool valid = false;
                if (raw is int i)
                {
                    val = i;
                    valid = true;
                }
                else if (raw is long l)
                {
                    val = (int)l;
                    valid = true;
                }

                if (!valid || val < 1)
                {
                    errors.Add($"{source}: metrics.temporal_coupling_max_commit_file_count must be a positive integer.");
                }
                else
                {
                    result.TemporalCouplingMaxCommitFileCount = val;
                }
            }

            return result;
        }

        private ScoringConfigOverrides NormalizeScoring(
            object? value,
            string source,
            List<string> errors)
        {
            var result = new ScoringConfigOverrides();
            if (!RequireRecord(value, $"{source}: scoring must be an object.", errors, out var record))
            {
                return result;
            }

            CheckUnknownKeys(record, new List<string> { "attention" }, "scoring", source, errors);

            if (record.TryGetValue("attention", out var attentionVal))
            {
                if (attentionVal == null)
                {
                    return result;
                }

                if (!RequireRecord(attentionVal, $"{source}: scoring.attention must be an object.", errors, out var attentionRecord))
                {
                    return result;
                }

                CheckUnknownKeys(attentionRecord, AttentionWeightKeys, "scoring.attention", source, errors);

                var attention = new AttentionWeightsOverrides();

                if (attentionRecord.TryGetValue("churn", out var churnVal))
                {
                    if (ValidateWeightValue(churnVal, "churn", source, errors))
                    {
                        attention.Churn = ConvertToDouble(churnVal);
                    }
                }
                if (attentionRecord.TryGetValue("recency", out var recencyVal))
                {
                    if (ValidateWeightValue(recencyVal, "recency", source, errors))
                    {
                        attention.Recency = ConvertToDouble(recencyVal);
                    }
                }
                if (attentionRecord.TryGetValue("contributor_spread", out var csVal))
                {
                    if (ValidateWeightValue(csVal, "contributor_spread", source, errors))
                    {
                        attention.ContributorSpread = ConvertToDouble(csVal);
                    }
                }
                if (attentionRecord.TryGetValue("low_familiarity_concentration", out var lfcVal))
                {
                    if (ValidateWeightValue(lfcVal, "low_familiarity_concentration", source, errors))
                    {
                        attention.LowFamiliarityConcentration = ConvertToDouble(lfcVal);
                    }
                }

                // Merge for validation
                var mergedForValidation = new AttentionWeights
                {
                    Churn = attention.Churn ?? DefaultAttentionWeights.Churn,
                    Recency = attention.Recency ?? DefaultAttentionWeights.Recency,
                    ContributorSpread = attention.ContributorSpread ?? DefaultAttentionWeights.ContributorSpread,
                    LowFamiliarityConcentration = attention.LowFamiliarityConcentration ?? DefaultAttentionWeights.LowFamiliarityConcentration
                };

                ValidateAttentionWeights(mergedForValidation, source, errors);

                result.Attention = attention;
            }

            return result;
        }

        private static double ConvertToDouble(object? val) => val switch
        {
            null => 0.0,
            double d => d,
            float f => f,
            long l => l,
            int i => i,
            _ => Convert.ToDouble(val)
        };

        private static string? NormalizeOptionalString(
            object? value,
            string path,
            List<string> errors)
        {
            if (value == null)
            {
                return null;
            }
            string? normalized = NormalizeNonEmptyString(value);
            if (normalized == null)
            {
                errors.Add($"{path} must be a non-empty string when provided.");
            }
            return normalized;
        }

        private static string? NormalizeNonEmptyString(object? value)
        {
            if (value is string s)
            {
                string trimmed = s.Trim();
                return trimmed.Length == 0 ? null : trimmed;
            }
            return null;
        }

        private static bool IsRecord(object? value, out Dictionary<string, object?> record)
        {
            if (value is Dictionary<string, object?> dict)
            {
                record = dict;
                return true;
            }
            record = new Dictionary<string, object?>();
            return false;
        }
    }
}
