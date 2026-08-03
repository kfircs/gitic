using System;
using System.Collections.Generic;
using System.Linq;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Gitic.Tests")]

namespace Gitic;
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
            ValidateAliases(aliasesVal, source, errors);
        }
        if (record.TryGetValue("bots", out var botsVal))
        {
            ValidateBots(botsVal, source, errors);
        }
        if (record.TryGetValue("excludes", out var excludesVal))
        {
            ValidateExcludes(excludesVal, source, errors);
        }
        if (record.TryGetValue("areas", out var areasVal))
        {
            ValidateAreas(areasVal, source, errors);
        }
        if (record.TryGetValue("scoring", out var scoringVal))
        {
            ValidateScoring(scoringVal, source, errors);
        }
        if (record.TryGetValue("identity", out var identityVal))
        {
            ValidateIdentityConfig(identityVal, source, errors);
        }
        if (record.TryGetValue("metrics", out var metricsVal))
        {
            ValidateMetricsConfig(metricsVal, source, errors);
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

    private static string? NormalizeNonEmptyString(object? value)
    {
        if (value is string s)
        {
            string trimmed = s.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }
        return null;
    }

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

    private static void ValidateAliases(object? value, string source, List<string> errors)
    {
        if (!RequireArray(value, $"{source}: aliases must be an array.", errors, out var array))
        {
            return;
        }

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
            ValidateIdentity(canonicalVal, $"{path}.canonical", source, errors);

            entryRecord.TryGetValue("identities", out var identitiesVal);
            if (!RequireNonEmptyArray(identitiesVal, $"{source}: {path}.identities must be a non-empty array.", errors, out var identitiesArr))
            {
                continue;
            }

            for (int j = 0; j < identitiesArr.Count; j++)
            {
                ValidateIdentity(identitiesArr[j], $"{path}.identities[{j}]", source, errors);
            }
        }
    }

    private static void ValidateIdentity(
        object? value,
        string path,
        string source,
        List<string> errors)
    {
        if (!RequireRecord(value, $"{source}: {path} must be an object with name and email.", errors, out var record))
        {
            return;
        }

        CheckUnknownKeys(record, new List<string> { "name", "email" }, path, source, errors);

        record.TryGetValue("name", out var nameVal);
        record.TryGetValue("email", out var emailVal);

        RequireNonEmptyString(nameVal, $"{source}: {path}.name", errors);
        RequireNonEmptyString(emailVal, $"{source}: {path}.email", errors);
    }

    private static void ValidateBots(object? value, string source, List<string> errors)
    {
        if (!RequireArray(value, $"{source}: bots must be an array.", errors, out var array))
        {
            return;
        }

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
            }
        }
    }

    private static void ValidateExcludes(object? value, string source, List<string> errors)
    {
        if (!RequireArray(value, $"{source}: excludes must be an array.", errors, out var array))
        {
            return;
        }

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

            RequireNonEmptyString(patternVal, $"{source}: {path}.pattern", errors);
            RequireNonEmptyString(categoryVal, $"{source}: {path}.category", errors);
        }
    }

    private static void ValidateAreas(object? value, string source, List<string> errors)
    {
        if (!RequireArray(value, $"{source}: areas must be an array.", errors, out var array))
        {
            return;
        }

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
            RequireNonEmptyString(nameVal, $"{source}: {path}.name", errors);

            record.TryGetValue("paths", out var pathsVal);
            if (!RequireNonEmptyArray(pathsVal, $"{source}: {path}.paths must be a non-empty array of strings.", errors, out var pathsArr))
            {
                continue;
            }

            for (int j = 0; j < pathsArr.Count; j++)
            {
                RequireNonEmptyString(pathsArr[j], $"{source}: {path}.paths[{j}]", errors);
            }
        }
    }

    private static void ValidateIdentityConfig(object? value, string source, List<string> errors)
    {
        if (!RequireRecord(value, $"{source}: identity must be an object.", errors, out var record))
        {
            return;
        }

        CheckUnknownKeys(record, new List<string> { "merge_on_email" }, "identity", source, errors);

        if (record.TryGetValue("merge_on_email", out var raw))
        {
            if (raw is not bool)
            {
                errors.Add($"{source}: identity.merge_on_email must be a boolean.");
            }
        }
    }

    private static void ValidateMetricsConfig(object? value, string source, List<string> errors)
    {
        if (!RequireRecord(value, $"{source}: metrics must be an object.", errors, out var record))
        {
            return;
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
        }
    }

    private void ValidateScoring(object? value, string source, List<string> errors)
    {
        if (!RequireRecord(value, $"{source}: scoring must be an object.", errors, out var record))
        {
            return;
        }

        CheckUnknownKeys(record, new List<string> { "attention" }, "scoring", source, errors);

        if (record.TryGetValue("attention", out var attentionVal))
        {
            if (attentionVal == null)
            {
                return;
            }

            if (!RequireRecord(attentionVal, $"{source}: scoring.attention must be an object.", errors, out var attentionRecord))
            {
                return;
            }

            CheckUnknownKeys(attentionRecord, AttentionWeightKeys, "scoring.attention", source, errors);

            double? churn = null;
            double? recency = null;
            double? contributorSpread = null;
            double? lowFamiliarityConcentration = null;

            if (attentionRecord.TryGetValue("churn", out var churnVal))
            {
                if (ValidateWeightObject(churnVal, "churn", source, errors))
                {
                    churn = ConfigUtils.ConvertToDouble(churnVal);
                }
            }
            if (attentionRecord.TryGetValue("recency", out var recencyVal))
            {
                if (ValidateWeightObject(recencyVal, "recency", source, errors))
                {
                    recency = ConfigUtils.ConvertToDouble(recencyVal);
                }
            }
            if (attentionRecord.TryGetValue("contributor_spread", out var csVal))
            {
                if (ValidateWeightObject(csVal, "contributor_spread", source, errors))
                {
                    contributorSpread = ConfigUtils.ConvertToDouble(csVal);
                }
            }
            if (attentionRecord.TryGetValue("low_familiarity_concentration", out var lfcVal))
            {
                if (ValidateWeightObject(lfcVal, "low_familiarity_concentration", source, errors))
                {
                    lowFamiliarityConcentration = ConfigUtils.ConvertToDouble(lfcVal);
                }
            }

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

    private static bool ValidateWeightObject(
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

        return ValidateWeightValue(numVal, key, source, errors);
    }
}

