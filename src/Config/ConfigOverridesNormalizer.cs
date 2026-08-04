using System;
using System.Collections.Generic;
using System.Linq;

namespace Gitic;

/// <summary>Defines a contract for normalizing untyped configuration override inputs.</summary>
public interface IConfigOverridesNormalizer
{
    GiticConfigOverrides NormalizeOverride(object? input, string source);
}

public class ConfigOverridesNormalizer : IConfigOverridesNormalizer
{
    private readonly IConfigValidator _validator;

    public ConfigOverridesNormalizer(IConfigValidator validator)
    {
        _validator = validator;
    }

    public GiticConfigOverrides NormalizeOverride(object? input, string source)
    {
        // Cleanly delegate all validation rules to the validation layer
        _validator.ValidateOverride(input, source);

        // Since the input is guaranteed to be structurally and semantically valid,
        // we can normalize it directly with absolute safety.
        var record = (Dictionary<string, object?>)input!;
        var @override = new GiticConfigOverrides();

        if (record.TryGetValue("aliases", out var aliasesVal) && aliasesVal is List<object?> aliasesList)
        {
            @override.Aliases = NormalizeAliases(aliasesList);
        }
        if (record.TryGetValue("bots", out var botsVal) && botsVal is List<object?> botsList)
        {
            @override.Bots = NormalizeBots(botsList);
        }
        if (record.TryGetValue("excludes", out var excludesVal) && excludesVal is List<object?> excludesList)
        {
            @override.Excludes = NormalizeExcludes(excludesList);
        }
        if (record.TryGetValue("areas", out var areasVal) && areasVal is List<object?> areasList)
        {
            @override.Areas = NormalizeAreas(areasList);
        }
        if (record.TryGetValue("scoring", out var scoringVal) && scoringVal is Dictionary<string, object?> scoringRecord)
        {
            @override.Scoring = NormalizeScoring(scoringRecord);
        }
        if (record.TryGetValue("identity", out var identityVal) && identityVal is Dictionary<string, object?> identityRecord)
        {
            @override.Identity = NormalizeIdentityConfig(identityRecord);
        }
        if (record.TryGetValue("metrics", out var metricsVal) && metricsVal is Dictionary<string, object?> metricsRecord)
        {
            @override.Metrics = NormalizeMetricsConfig(metricsRecord);
        }

        return @override;
    }

    private static List<AliasRule> NormalizeAliases(List<object?> array)
    {
        return array.Select(entry =>
        {
            var entryRecord = (Dictionary<string, object?>)entry!;
            
            entryRecord.TryGetValue("canonical", out var canonicalVal);
            GitIdentity canonical = NormalizeIdentity((Dictionary<string, object?>)canonicalVal!);

            entryRecord.TryGetValue("identities", out var identitiesVal);
            var identitiesArr = (List<object?>)identitiesVal!;
            var identities = identitiesArr.Select(idVal => NormalizeIdentity((Dictionary<string, object?>)idVal!)).ToList();

            return new AliasRule
            {
                Canonical = canonical,
                Identities = identities
            };
        }).ToList();
    }

    private static GitIdentity NormalizeIdentity(Dictionary<string, object?> record)
    {
        record.TryGetValue("name", out var nameVal);
        record.TryGetValue("email", out var emailVal);

        return new GitIdentity 
        { 
            Name = NormalizeNonEmptyString(nameVal)!, 
            Email = NormalizeNonEmptyString(emailVal)! 
        };
    }

    private static List<BotRule> NormalizeBots(List<object?> array)
    {
        return array.Select(entry =>
        {
            var record = (Dictionary<string, object?>)entry!;

            record.TryGetValue("name", out var nameVal);
            record.TryGetValue("email", out var emailVal);
            record.TryGetValue("pattern", out var patternVal);

            return new BotRule
            {
                Name = NormalizeNonEmptyString(nameVal),
                Email = NormalizeNonEmptyString(emailVal),
                Pattern = NormalizeNonEmptyString(patternVal)
            };
        }).ToList();
    }

    private static List<ExcludeRule> NormalizeExcludes(List<object?> array)
    {
        return array.Select(entry =>
        {
            var record = (Dictionary<string, object?>)entry!;

            record.TryGetValue("pattern", out var patternVal);
            record.TryGetValue("category", out var categoryVal);

            return new ExcludeRule 
            { 
                Pattern = NormalizeNonEmptyString(patternVal)!, 
                Category = NormalizeNonEmptyString(categoryVal)! 
            };
        }).ToList();
    }

    private static List<NamedArea> NormalizeAreas(List<object?> array)
    {
        return array.Select(entry =>
        {
            var record = (Dictionary<string, object?>)entry!;

            record.TryGetValue("name", out var nameVal);
            record.TryGetValue("paths", out var pathsVal);
            var pathsArr = (List<object?>)pathsVal!;
            var paths = pathsArr.Select(pathVal => NormalizeNonEmptyString(pathVal)!).ToList();

            return new NamedArea 
            { 
                Name = NormalizeNonEmptyString(nameVal)!, 
                Paths = paths 
            };
        }).ToList();
    }

    private static IdentityConfigOverrides NormalizeIdentityConfig(Dictionary<string, object?> record)
    {
        return new IdentityConfigOverrides
        {
            MergeOnEmail = record.TryGetValue("merge_on_email", out var raw) && raw is bool b && b
        };
    }

    private static MetricsConfigOverrides NormalizeMetricsConfig(Dictionary<string, object?> record)
    {
        int count = 20;
        if (record.TryGetValue("temporal_coupling_max_commit_file_count", out var raw))
        {
            count = raw switch
            {
                int i => i,
                long l => (int)l,
                _ => 20
            };
        }
        return new MetricsConfigOverrides { TemporalCouplingMaxCommitFileCount = count };
    }

    private static ScoringConfigOverrides NormalizeScoring(Dictionary<string, object?> record)
    {
        var result = new ScoringConfigOverrides();
        if (record.TryGetValue("attention", out var attentionVal) && attentionVal is Dictionary<string, object?> attentionRecord)
        {
            result.Attention = new AttentionWeightsOverrides
            {
                Churn = GetDoubleValue(attentionRecord, "churn"),
                Recency = GetDoubleValue(attentionRecord, "recency"),
                ContributorSpread = GetDoubleValue(attentionRecord, "contributor_spread"),
                LowFamiliarityConcentration = GetDoubleValue(attentionRecord, "low_familiarity_concentration")
            };
        }
        return result;
    }

    private static double? GetDoubleValue(Dictionary<string, object?> record, string key)
    {
        return record.TryGetValue(key, out var val) && val != null ? ConfigUtils.ConvertToDouble(val) : null;
    }

    private static string? NormalizeNonEmptyString(object? value)
    {
        if (value is string s)
        {
            string trimmed = s.Trim();
            return trimmed.Length > 0 ? trimmed : null;
        }
        return null;
    }
}
