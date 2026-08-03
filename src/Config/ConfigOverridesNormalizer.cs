using System;
using System.Collections.Generic;
using System.Linq;

namespace Gitic;

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
        var aliases = new List<AliasRule>();
        foreach (var entry in array)
        {
            var entryRecord = (Dictionary<string, object?>)entry!;
            
            entryRecord.TryGetValue("canonical", out var canonicalVal);
            GitIdentity canonical = NormalizeIdentity((Dictionary<string, object?>)canonicalVal!);

            entryRecord.TryGetValue("identities", out var identitiesVal);
            var identitiesArr = (List<object?>)identitiesVal!;

            var identities = new List<GitIdentity>();
            foreach (var idVal in identitiesArr)
            {
                identities.Add(NormalizeIdentity((Dictionary<string, object?>)idVal!));
            }

            aliases.Add(new AliasRule
            {
                Canonical = canonical,
                Identities = identities
            });
        }
        return aliases;
    }

    private static GitIdentity NormalizeIdentity(Dictionary<string, object?> record)
    {
        record.TryGetValue("name", out var nameVal);
        record.TryGetValue("email", out var emailVal);

        return new GitIdentity 
        { 
            Name = NormalizeNonEmptyString(nameVal!)!, 
            Email = NormalizeNonEmptyString(emailVal!)! 
        };
    }

    private static List<BotRule> NormalizeBots(List<object?> array)
    {
        var bots = new List<BotRule>();
        foreach (var entry in array)
        {
            var record = (Dictionary<string, object?>)entry!;

            record.TryGetValue("name", out var nameVal);
            record.TryGetValue("email", out var emailVal);
            record.TryGetValue("pattern", out var patternVal);

            bots.Add(new BotRule
            {
                Name = NormalizeNonEmptyString(nameVal),
                Email = NormalizeNonEmptyString(emailVal),
                Pattern = NormalizeNonEmptyString(patternVal)
            });
        }
        return bots;
    }

    private static List<ExcludeRule> NormalizeExcludes(List<object?> array)
    {
        var excludes = new List<ExcludeRule>();
        foreach (var entry in array)
        {
            var record = (Dictionary<string, object?>)entry!;

            record.TryGetValue("pattern", out var patternVal);
            record.TryGetValue("category", out var categoryVal);

            excludes.Add(new ExcludeRule 
            { 
                Pattern = NormalizeNonEmptyString(patternVal!)!, 
                Category = NormalizeNonEmptyString(categoryVal!)! 
            });
        }
        return excludes;
    }

    private static List<NamedArea> NormalizeAreas(List<object?> array)
    {
        var areas = new List<NamedArea>();
        foreach (var entry in array)
        {
            var record = (Dictionary<string, object?>)entry!;

            record.TryGetValue("name", out var nameVal);
            record.TryGetValue("paths", out var pathsVal);
            var pathsArr = (List<object?>)pathsVal!;

            var paths = new List<string>();
            foreach (var pathVal in pathsArr)
            {
                paths.Add(NormalizeNonEmptyString(pathVal!)!);
            }

            areas.Add(new NamedArea 
            { 
                Name = NormalizeNonEmptyString(nameVal!)!, 
                Paths = paths 
            });
        }
        return areas;
    }

    private static IdentityConfigOverrides NormalizeIdentityConfig(Dictionary<string, object?> record)
    {
        var result = new IdentityConfigOverrides { MergeOnEmail = false };
        if (record.TryGetValue("merge_on_email", out var raw) && raw is bool b)
        {
            result.MergeOnEmail = b;
        }
        return result;
    }

    private static MetricsConfigOverrides NormalizeMetricsConfig(Dictionary<string, object?> record)
    {
        var result = new MetricsConfigOverrides { TemporalCouplingMaxCommitFileCount = 20 };
        if (record.TryGetValue("temporal_coupling_max_commit_file_count", out var raw))
        {
            if (raw is int i)
            {
                result.TemporalCouplingMaxCommitFileCount = i;
            }
            else if (raw is long l)
            {
                result.TemporalCouplingMaxCommitFileCount = (int)l;
            }
        }
        return result;
    }

    private static ScoringConfigOverrides NormalizeScoring(Dictionary<string, object?> record)
    {
        var result = new ScoringConfigOverrides();
        if (record.TryGetValue("attention", out var attentionVal) && attentionVal is Dictionary<string, object?> attentionRecord)
        {
            var attention = new AttentionWeightsOverrides();

            if (attentionRecord.TryGetValue("churn", out var churnVal) && churnVal != null)
            {
                attention.Churn = ConfigUtils.ConvertToDouble(churnVal);
            }
            if (attentionRecord.TryGetValue("recency", out var recencyVal) && recencyVal != null)
            {
                attention.Recency = ConfigUtils.ConvertToDouble(recencyVal);
            }
            if (attentionRecord.TryGetValue("contributor_spread", out var csVal) && csVal != null)
            {
                attention.ContributorSpread = ConfigUtils.ConvertToDouble(csVal);
            }
            if (attentionRecord.TryGetValue("low_familiarity_concentration", out var lfcVal) && lfcVal != null)
            {
                attention.LowFamiliarityConcentration = ConfigUtils.ConvertToDouble(lfcVal);
            }

            result.Attention = attention;
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
}
