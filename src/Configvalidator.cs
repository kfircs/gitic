using System;
using System.Collections.Generic;
using System.Linq;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Gitic.Tests")]

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

    internal interface IConfigValidator
    {
        void ValidateAttentionWeights(AttentionWeights attention, string source, List<string>? errors = null);
    }

    internal class ConfigValidator : IConfigValidator
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
    }
}
