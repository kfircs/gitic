using System;

namespace Gitic;

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
