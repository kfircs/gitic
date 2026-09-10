using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gitic;

/// <summary>
/// A list of glob patterns that supports implicit conversion from a single string glob,
/// an array of globs, or a list of globs, with custom JSON serialization.
/// </summary>
[JsonConverter(typeof(GlobListJsonConverter))]
public class GlobList : List<string>
{
    public GlobList() { }

    public GlobList(IEnumerable<string> collection) : base(collection) { }

    public GlobList(params string[] items) : base(items) { }

    public static implicit operator GlobList(string? single) =>
        string.IsNullOrWhiteSpace(single) ? new GlobList() : new GlobList { single.Trim() };

    public static implicit operator GlobList(string[]? array) =>
        array == null ? new GlobList() : new GlobList(array);
}

/// <summary>
/// JSON converter for <see cref="GlobList"/> supporting both single string and array representations.
/// </summary>
public class GlobListJsonConverter : JsonConverter<GlobList>
{
    public override GlobList? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();
            return string.IsNullOrEmpty(str) ? new GlobList() : new GlobList { str };
        }

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var list = new GlobList();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType == JsonTokenType.String)
                {
                    var s = reader.GetString();
                    if (!string.IsNullOrEmpty(s)) list.Add(s);
                }
            }
            return list;
        }

        return new GlobList();
    }

    public override void Write(Utf8JsonWriter writer, GlobList value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value)
        {
            writer.WriteStringValue(item);
        }
        writer.WriteEndArray();
    }
}

/// <summary>
/// Represents a user-declared architectural boundary rule defined in .gitic.yml.
/// </summary>
public class BoundaryRule
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("forbidden_coupling")]
    public GlobList ForbiddenCoupling { get; set; } = new();

    [JsonPropertyName("allowed_coupling")]
    public GlobList AllowedCoupling { get; set; } = new();

    [JsonPropertyName("threshold")]
    public double Threshold { get; set; } = 0.2;

    public BoundaryRule() { }

    public BoundaryRule(
        string name,
        string source,
        GlobList forbiddenCoupling,
        GlobList? allowedCoupling = null,
        double threshold = 0.2)
    {
        Name = name ?? string.Empty;
        Source = source ?? string.Empty;
        ForbiddenCoupling = forbiddenCoupling ?? new GlobList();
        AllowedCoupling = allowedCoupling ?? new GlobList();
        Threshold = threshold;
    }

    public BoundaryRule(
        string name,
        string source,
        string forbiddenCoupling,
        double threshold = 0.2)
        : this(name, source, new GlobList { forbiddenCoupling }, null, threshold)
    {
    }
}

/// <summary>
/// Helper class for parsing and validating architectural boundary configurations.
/// </summary>
public static class BoundaryConfig
{
    /// <summary>
    /// Parses architectural boundary rules from a YAML configuration string.
    /// </summary>
    public static List<BoundaryRule> LoadRulesFromYaml(string yamlContent, string source = "boundary_config")
    {
        return ParseRules(yamlContent, source);
    }

    /// <summary>
    /// Parses architectural boundary rules from a YAML string, supporting either a root mapping with a "boundaries" key
    /// or a top-level list of boundary rules.
    /// </summary>
    public static List<BoundaryRule> ParseRules(string yamlContent, string source = "boundary_config")
    {
        if (string.IsNullOrWhiteSpace(yamlContent))
        {
            return new List<BoundaryRule>();
        }

        var parsed = YamlSubsetParserHelper.ParseYamlSubset(yamlContent, source);
        if (parsed == null)
        {
            return new List<BoundaryRule>();
        }

        var validator = new ConfigValidator();

        if (parsed is Dictionary<string, object?> dict)
        {
            if (dict.TryGetValue("boundaries", out var boundariesVal) && boundariesVal is List<object?>)
            {
                validator.ValidateOverride(dict, source);
                var normalizer = new ConfigOverridesNormalizer(validator);
                var overrides = normalizer.NormalizeOverride(dict, source);
                return overrides.Boundaries ?? new List<BoundaryRule>();
            }

            // In case it's a mapping that has boundary rule keys directly
            if (dict.ContainsKey("source") && (dict.ContainsKey("forbidden_coupling") || dict.ContainsKey("allowed_coupling")))
            {
                var wrapper = new Dictionary<string, object?> { ["boundaries"] = new List<object?> { dict } };
                validator.ValidateOverride(wrapper, source);
                var normalizer = new ConfigOverridesNormalizer(validator);
                var overrides = normalizer.NormalizeOverride(wrapper, source);
                return overrides.Boundaries ?? new List<BoundaryRule>();
            }
        }
        else if (parsed is List<object?> rootList)
        {
            var wrapper = new Dictionary<string, object?> { ["boundaries"] = rootList };
            validator.ValidateOverride(wrapper, source);
            var normalizer = new ConfigOverridesNormalizer(validator);
            var overrides = normalizer.NormalizeOverride(wrapper, source);
            return overrides.Boundaries ?? new List<BoundaryRule>();
        }

        return new List<BoundaryRule>();
    }
}
