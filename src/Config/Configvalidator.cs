using System;
using System.Collections.Generic;
using System.Linq;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Gitic.Tests")]

namespace Gitic;

/// <summary>Defines validation methods for Gitic configuration settings.</summary>
public interface IConfigValidator
{
    void ValidateOverride(object? input, string source);
    void ValidateAttentionWeights(AttentionWeights attention, string source, List<string>? errors = null);
}

public class ConfigValidator : IConfigValidator
{
    public static readonly List<string> TopLevelKeys = [
        "aliases",
        "bots",
        "excludes",
        "areas",
        "scoring",
        "identity",
        "metrics"
    ];

    public static readonly List<string> AttentionWeightKeys = [
        "churn",
        "recency",
        "contributor_spread",
        "low_familiarity_concentration"
    ];

    public void ValidateOverride(object? input, string source)
    {
        var errors = new List<string>();
        if (!IsRecord(input, out var record))
        {
            throw new ConfigValidationError(new List<string> { $"{source}: expected a top-level mapping object." });
        }

        CheckUnknownKeys(record, TopLevelKeys, "", source, errors);

        if (record.TryGetValue("aliases", out var aliasesVal))
        {
            AliasValidator.ValidateAliases(aliasesVal, source, errors);
        }
        if (record.TryGetValue("bots", out var botsVal))
        {
            BotValidator.ValidateBots(botsVal, source, errors);
        }
        if (record.TryGetValue("excludes", out var excludesVal))
        {
            ExcludeValidator.ValidateExcludes(excludesVal, source, errors);
        }
        if (record.TryGetValue("areas", out var areasVal))
        {
            AreaValidator.ValidateAreas(areasVal, source, errors);
        }
        if (record.TryGetValue("scoring", out var scoringVal))
        {
            ScoringValidator.ValidateScoring(scoringVal, source, errors);
        }
        if (record.TryGetValue("identity", out var identityVal))
        {
            IdentityConfigValidator.ValidateIdentityConfig(identityVal, source, errors);
        }
        if (record.TryGetValue("metrics", out var metricsVal))
        {
            MetricsConfigValidator.ValidateMetricsConfig(metricsVal, source, errors);
        }

        if (errors.Count > 0)
        {
            throw new ConfigValidationError(errors);
        }
    }

    public void ValidateAttentionWeights(
        AttentionWeights attention,
        string source,
        List<string>? errors = null)
    {
        ScoringValidator.ValidateAttentionWeights(attention, source, errors);
    }

    internal static bool IsRecord(object? value, out Dictionary<string, object?> record)
    {
        if (value is Dictionary<string, object?> dict)
        {
            record = dict;
            return true;
        }
        record = [];
        return false;
    }

    internal static bool RequireRecord(
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

    internal static bool RequireArray(
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
        array = [];
        return false;
    }

    internal static bool RequireNonEmptyArray(
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
        array = [];
        return false;
    }

    internal static string? RequireNonEmptyString(
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

    internal static string? NormalizeNonEmptyString(object? value)
    {
        if (value is string s)
        {
            string trimmed = s.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }
        return null;
    }

    internal static string? NormalizeOptionalString(
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

    internal static void CheckUnknownKeys(
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
}

public class AliasValidator
{
    public static void ValidateAliases(object? value, string source, List<string> errors)
    {
        if (!ConfigValidator.RequireArray(value, $"{source}: aliases must be an array.", errors, out var array))
        {
            return;
        }

        for (int i = 0; i < array.Count; i++)
        {
            object? entry = array[i];
            string path = $"aliases[{i}]";
            if (!ConfigValidator.RequireRecord(entry, $"{source}: {path} must be an object.", errors, out var entryRecord))
            {
                continue;
            }

            ConfigValidator.CheckUnknownKeys(entryRecord, new List<string> { "canonical", "identities" }, path, source, errors);

            entryRecord.TryGetValue("canonical", out var canonicalVal);
            ValidateIdentity(canonicalVal, $"{path}.canonical", source, errors);

            entryRecord.TryGetValue("identities", out var identitiesVal);
            if (!ConfigValidator.RequireNonEmptyArray(identitiesVal, $"{source}: {path}.identities must be a non-empty array.", errors, out var identitiesArr))
            {
                continue;
            }

            for (int j = 0; j < identitiesArr.Count; j++)
            {
                ValidateIdentity(identitiesArr[j], $"{path}.identities[{j}]", source, errors);
            }
        }
    }

    public static void ValidateIdentity(
        object? value,
        string path,
        string source,
        List<string> errors)
    {
        if (!ConfigValidator.RequireRecord(value, $"{source}: {path} must be an object with name and email.", errors, out var record))
        {
            return;
        }

        ConfigValidator.CheckUnknownKeys(record, new List<string> { "name", "email" }, path, source, errors);

        record.TryGetValue("name", out var nameVal);
        record.TryGetValue("email", out var emailVal);

        ConfigValidator.RequireNonEmptyString(nameVal, $"{source}: {path}.name", errors);
        ConfigValidator.RequireNonEmptyString(emailVal, $"{source}: {path}.email", errors);
    }
}

public class BotValidator
{
    public static void ValidateBots(object? value, string source, List<string> errors)
    {
        if (!ConfigValidator.RequireArray(value, $"{source}: bots must be an array.", errors, out var array))
        {
            return;
        }

        for (int i = 0; i < array.Count; i++)
        {
            object? entry = array[i];
            string path = $"bots[{i}]";
            if (!ConfigValidator.RequireRecord(entry, $"{source}: {path} must be an object.", errors, out var record))
            {
                continue;
            }

            ConfigValidator.CheckUnknownKeys(record, new List<string> { "name", "email", "pattern" }, path, source, errors);

            record.TryGetValue("name", out var nameVal);
            record.TryGetValue("email", out var emailVal);
            record.TryGetValue("pattern", out var patternVal);

            string? name = ConfigValidator.NormalizeOptionalString(nameVal, $"{source}: {path}.name", errors);
            string? email = ConfigValidator.NormalizeOptionalString(emailVal, $"{source}: {path}.email", errors);
            string? pattern = ConfigValidator.NormalizeOptionalString(patternVal, $"{source}: {path}.pattern", errors);

            if (name == null && email == null && pattern == null)
            {
                errors.Add($"{source}: {path} must define at least one of name, email, or pattern.");
            }
        }
    }
}

public class ExcludeValidator
{
    public static void ValidateExcludes(object? value, string source, List<string> errors)
    {
        if (!ConfigValidator.RequireArray(value, $"{source}: excludes must be an array.", errors, out var array))
        {
            return;
        }

        for (int i = 0; i < array.Count; i++)
        {
            object? entry = array[i];
            string path = $"excludes[{i}]";
            if (!ConfigValidator.RequireRecord(entry, $"{source}: {path} must be an object.", errors, out var record))
            {
                continue;
            }

            ConfigValidator.CheckUnknownKeys(record, new List<string> { "pattern", "category" }, path, source, errors);

            record.TryGetValue("pattern", out var patternVal);
            record.TryGetValue("category", out var categoryVal);

            ConfigValidator.RequireNonEmptyString(patternVal, $"{source}: {path}.pattern", errors);
            ConfigValidator.RequireNonEmptyString(categoryVal, $"{source}: {path}.category", errors);
        }
    }
}

public class AreaValidator
{
    public static void ValidateAreas(object? value, string source, List<string> errors)
    {
        if (!ConfigValidator.RequireArray(value, $"{source}: areas must be an array.", errors, out var array))
        {
            return;
        }

        for (int i = 0; i < array.Count; i++)
        {
            object? entry = array[i];
            string path = $"areas[{i}]";
            if (!ConfigValidator.RequireRecord(entry, $"{source}: {path} must be an object.", errors, out var record))
            {
                continue;
            }

            ConfigValidator.CheckUnknownKeys(record, new List<string> { "name", "paths" }, path, source, errors);

            record.TryGetValue("name", out var nameVal);
            ConfigValidator.RequireNonEmptyString(nameVal, $"{source}: {path}.name", errors);

            record.TryGetValue("paths", out var pathsVal);
            if (!ConfigValidator.RequireNonEmptyArray(pathsVal, $"{source}: {path}.paths must be a non-empty array of strings.", errors, out var pathsArr))
            {
                continue;
            }

            for (int j = 0; j < pathsArr.Count; j++)
            {
                ConfigValidator.RequireNonEmptyString(pathsArr[j], $"{source}: {path}.paths[{j}]", errors);
            }
        }
    }
}

public class ScoringValidator
{
    public static void ValidateScoring(object? value, string source, List<string> errors)
    {
        if (!ConfigValidator.RequireRecord(value, $"{source}: scoring must be an object.", errors, out var record))
        {
            return;
        }

        ConfigValidator.CheckUnknownKeys(record, new List<string> { "attention" }, "scoring", source, errors);

        if (record.TryGetValue("attention", out var attentionVal))
        {
            if (attentionVal == null)
            {
                return;
            }

            if (!ConfigValidator.RequireRecord(attentionVal, $"{source}: scoring.attention must be an object.", errors, out var attentionRecord))
            {
                return;
            }

            ConfigValidator.CheckUnknownKeys(attentionRecord, ConfigValidator.AttentionWeightKeys, "scoring.attention", source, errors);

            double? churn = ExtractAttentionWeight(attentionRecord, "churn", source, errors);
            double? recency = ExtractAttentionWeight(attentionRecord, "recency", source, errors);
            double? contributorSpread = ExtractAttentionWeight(attentionRecord, "contributor_spread", source, errors);
            double? lowFamiliarityConcentration = ExtractAttentionWeight(attentionRecord, "low_familiarity_concentration", source, errors);

            var attention = new AttentionWeights
            {
                Churn = churn ?? DefaultAttentionWeights.Churn,
                Recency = recency ?? DefaultAttentionWeights.Recency,
                ContributorSpread = contributorSpread ?? DefaultAttentionWeights.ContributorSpread,
                LowFamiliarityConcentration = lowFamiliarityConcentration ?? DefaultAttentionWeights.LowFamiliarityConcentration
            };

            ValidateWeightValue(attention.Churn, "churn", source, errors);
            ValidateWeightValue(attention.Recency, "recency", source, errors);
            ValidateWeightValue(attention.ContributorSpread, "contributor_spread", source, errors);
            ValidateWeightValue(attention.LowFamiliarityConcentration, "low_familiarity_concentration", source, errors);

            double sum = attention.Churn + attention.Recency + attention.ContributorSpread + attention.LowFamiliarityConcentration;
            if (Math.Abs(sum - 1.0) > 0.0001)
            {
                errors.Add($"{source}: scoring.attention weights must sum to 1. Current sum: {sum.ToString("F6")}.");
            }
        }
    }

    public static void ValidateAttentionWeights(
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
            errors.Add($"{source}: scoring.attention weights must sum to 1. Current sum: {sum:F6}.");
        }

        if (errors.Count > 0)
        {
            throw new ConfigValidationError(errors);
        }
    }

    internal static bool ValidateWeightValue(
        double value,
        string key,
        string source,
        List<string> errors)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            errors.Add($"{source}: scoring.attention.{key} must be a finite number.");
            return false;
        }

        if (value < 0.0 || value > 1.0)
        {
            errors.Add($"{source}: scoring.attention.{key} must be between 0 and 1.");
            return false;
        }

        return true;
    }

    private static double? ExtractAttentionWeight(Dictionary<string, object?> attentionRecord, string key, string source, List<string> errors)
    {
        if (attentionRecord.TryGetValue(key, out var val))
        {
            if (val is double d)
            {
                return d;
            }
            if (val is int i)
            {
                return (double)i;
            }
            if (val is long l)
            {
                return (double)l;
            }
            if (val is float f)
            {
                return (double)f;
            }
            if (val is decimal dec)
            {
                return (double)dec;
            }

            errors.Add($"{source}: scoring.attention.{key} must be a finite number.");
        }
        return null;
    }
}

public class IdentityConfigValidator
{
    public static void ValidateIdentityConfig(object? value, string source, List<string> errors)
    {
        if (!ConfigValidator.RequireRecord(value, $"{source}: identity must be an object.", errors, out var record))
        {
            return;
        }

        ConfigValidator.CheckUnknownKeys(record, new List<string> { "merge_on_email" }, "identity", source, errors);

        if (record.TryGetValue("merge_on_email", out var raw))
        {
            if (raw is not bool)
            {
                errors.Add($"{source}: identity.merge_on_email must be a boolean.");
            }
        }
    }
}

public class MetricsConfigValidator
{
    public static void ValidateMetricsConfig(object? value, string source, List<string> errors)
    {
        if (!ConfigValidator.RequireRecord(value, $"{source}: metrics must be an object.", errors, out var record))
        {
            return;
        }

        ConfigValidator.CheckUnknownKeys(record, new List<string> { "temporal_coupling_max_commit_file_count" }, "metrics", source, errors);

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
        }
    }
}
