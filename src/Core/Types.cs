using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Gitic;

public interface IContributorIdentity
{
    string Name { get; set; }
    string Email { get; set; }
}

public class GitIdentity
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AnalysisCommand
{
    Unknown,
    Hotspots,
    Areas,
    Contributors,
    Contributor,
    Report,
    TemporalCoupling,
    LeadTime,
    GeReport,
    Wizard
}
public class AnalysisSettings
{
    [JsonPropertyName("json")]
    public bool Json { get; set; }

    [JsonPropertyName("all_time")]
    public bool AllTime { get; set; }

    [JsonPropertyName("since")]
    public string? Since { get; set; }

    [JsonPropertyName("include_merges")]
    public bool IncludeMerges { get; set; }

    [JsonPropertyName("include_deleted")]
    public bool IncludeDeleted { get; set; }

    [JsonPropertyName("merge_by_email")]
    public bool? MergeByEmail { get; set; }

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("anonymize")]
    public bool Anonymize { get; set; }

    [JsonPropertyName("depth")]
    public int Depth { get; set; }

    [JsonPropertyName("format")]
    public string Format { get; set; } = "human";

    [JsonPropertyName("color")]
    public string Color { get; set; } = "auto";

    [JsonPropertyName("limit")]
    public int? Limit { get; set; }

    [JsonPropertyName("sort")]
    public string? Sort { get; set; }

    [JsonPropertyName("columns")]
    public string? Columns { get; set; }

    [JsonPropertyName("quiet")]
    public bool Quiet { get; set; }

    public AnalysisSettings Clone()
    {
        return new AnalysisSettings
        {
            Json = Json,
            AllTime = AllTime,
            Since = Since,
            IncludeMerges = IncludeMerges,
            IncludeDeleted = IncludeDeleted,
            MergeByEmail = MergeByEmail,
            Path = Path,
            Anonymize = Anonymize,
            Depth = Depth,
            Format = Format,
            Color = Color,
            Limit = Limit,
            Sort = Sort,
            Columns = Columns,
            Quiet = Quiet
        };
    }
}

public class GitFileChange
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("added")]
    public int Added { get; set; }

    [JsonPropertyName("deleted")]
    public int Deleted { get; set; }

    [JsonPropertyName("symbols")]
    public List<string>? Symbols { get; set; }
}

public class GitCommitRecord
{
    [JsonPropertyName("hash")]
    public string Hash { get; set; } = string.Empty;

    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    [JsonPropertyName("author")]
    public GitIdentity Author { get; set; } = new();

    [JsonPropertyName("coAuthors")]
    public List<GitIdentity> CoAuthors { get; set; } = new();

    [JsonPropertyName("parentCount")]
    public int ParentCount { get; set; }

    [JsonPropertyName("parents")]
    public List<string> Parents { get; set; } = new();

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("files")]
    public List<GitFileChange> Files { get; set; } = new();
}

public class ExclusionSummary
{
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("pattern")]
    public string Pattern { get; set; } = string.Empty;

    [JsonPropertyName("count")]
    public int Count { get; set; }
}

public class ScoreBreakdown
{
    [JsonPropertyName("touches")]
    public double Touches { get; set; }

    [JsonPropertyName("churn")]
    public double Churn { get; set; }

    [JsonPropertyName("recency")]
    public double Recency { get; set; }

    [JsonPropertyName("contributor_spread")]
    public double ContributorSpread { get; set; }

    [JsonPropertyName("low_familiarity_concentration")]
    public double LowFamiliarityConcentration { get; set; }

    public ScoreBreakdown Clone() => new()
    {
        Touches = Touches,
        Churn = Churn,
        Recency = Recency,
        ContributorSpread = ContributorSpread,
        LowFamiliarityConcentration = LowFamiliarityConcentration
    };
}

public class ContributorShare : IContributorIdentity
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("activity")]
    public double Activity { get; set; }

    [JsonPropertyName("activity_share")]
    public double ActivityShare { get; set; }
}

public class InnerSymbolMetric
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("touches")]
    public int Touches { get; set; }
}

public class KnowledgeSiloMetric
{
    [JsonPropertyName("truck_factor")]
    public int TruckFactor { get; set; }

    [JsonPropertyName("top_owner_share")]
    public double TopOwnerShare { get; set; }

    [JsonPropertyName("is_silo")]
    public bool IsSilo { get; set; }

    [JsonPropertyName("abandoned")]
    public bool Abandoned { get; set; }
}

public class FileMetric
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("area")]
    public string Area { get; set; } = string.Empty;

    [JsonPropertyName("touches")]
    public int Touches { get; set; }

    [JsonPropertyName("added")]
    public int Added { get; set; }

    [JsonPropertyName("deleted")]
    public int Deleted { get; set; }

    [JsonPropertyName("churn")]
    public int Churn { get; set; }

    [JsonPropertyName("last_touched")]
    public string LastTouched { get; set; } = string.Empty;

    [JsonPropertyName("contributor_count")]
    public int ContributorCount { get; set; }

    [JsonPropertyName("contributors")]
    public List<ContributorShare> Contributors { get; set; } = new();

    [JsonPropertyName("heat_score")]
    public double HeatScore { get; set; }

    [JsonPropertyName("attention_score")]
    public double AttentionScore { get; set; }

    [JsonPropertyName("score_breakdown")]
    public ScoreBreakdown ScoreBreakdown { get; set; } = new();

    [JsonPropertyName("inner_symbols")]
    public List<InnerSymbolMetric>? InnerSymbols { get; set; }

    [JsonPropertyName("debt_volatility")]
    public double? DebtVolatility { get; set; }

    [JsonPropertyName("rework_rate")]
    public double? ReworkRate { get; set; }

    [JsonPropertyName("coordination_overlap")]
    public double? CoordinationOverlap { get; set; }

    [JsonPropertyName("knowledge_silo")]
    public KnowledgeSiloMetric? KnowledgeSilo { get; set; }

    [JsonPropertyName("size")]
    public long? Size { get; set; }

    [JsonPropertyName("width")]
    public int? Width { get; set; }

    [JsonPropertyName("lines")]
    public int? Lines { get; set; }

    [JsonPropertyName("lines_of_code")]
    public int? LinesOfCode
    {
        get => Lines;
        set => Lines = value;
    }

    [JsonPropertyName("work_classification")]
    public WorkClassificationMetrics WorkClassification { get; set; } = new();
}

public class AreaMetric
{
    [JsonPropertyName("area")]
    public string Area { get; set; } = string.Empty;

    [JsonPropertyName("touches")]
    public int Touches { get; set; }

    [JsonPropertyName("added")]
    public int Added { get; set; }

    [JsonPropertyName("deleted")]
    public int Deleted { get; set; }

    [JsonPropertyName("churn")]
    public int Churn { get; set; }

    [JsonPropertyName("file_count")]
    public int FileCount { get; set; }

    [JsonPropertyName("last_touched")]
    public string LastTouched { get; set; } = string.Empty;

    [JsonPropertyName("contributor_count")]
    public int ContributorCount { get; set; }

    [JsonPropertyName("contributors")]
    public List<ContributorShare> Contributors { get; set; } = new();

    [JsonPropertyName("heat_score")]
    public double HeatScore { get; set; }

    [JsonPropertyName("attention_score")]
    public double AttentionScore { get; set; }

    [JsonPropertyName("score_breakdown")]
    public ScoreBreakdown ScoreBreakdown { get; set; } = new();

    [JsonPropertyName("rework_rate")]
    public double? ReworkRate { get; set; }
}

public class ContributorAreaMetric
{
    [JsonPropertyName("area")]
    public string Area { get; set; } = string.Empty;

    [JsonPropertyName("activity")]
    public double Activity { get; set; }

    [JsonPropertyName("activity_share")]
    public double ActivityShare { get; set; }

    [JsonPropertyName("familiarity_score")]
    public double FamiliarityScore { get; set; }
}

public class ContributorMetric : IContributorIdentity
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("total_activity")]
    public double TotalActivity { get; set; }

    [JsonPropertyName("areas")]
    public List<ContributorAreaMetric> Areas { get; set; } = new();
}

public class AutomationMetric : IContributorIdentity
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("total_activity")]
    public double TotalActivity { get; set; }

    [JsonPropertyName("areas")]
    public List<ContributorAreaMetric> Areas { get; set; } = new();
}

public class TemporalCoupling
{
    [JsonPropertyName("fileA")]
    public string FileA { get; set; } = string.Empty;

    [JsonPropertyName("fileB")]
    public string FileB { get; set; } = string.Empty;

    [JsonPropertyName("shared_commits")]
    public int SharedCommits { get; set; }

    [JsonPropertyName("coupling_degree")]
    public double CouplingDegree { get; set; }
}

public class MergeLeadTimeRecord
{
    [JsonPropertyName("hash")]
    public string Hash { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;

    [JsonPropertyName("lead_time_hours")]
    public double LeadTimeHours { get; set; }

    [JsonPropertyName("file_count")]
    public int FileCount { get; set; }
}

public class LeadTimesInfo
{
    [JsonPropertyName("average_lead_time_hours")]
    public double AverageLeadTimeHours { get; set; }

    [JsonPropertyName("merges")]
    public List<MergeLeadTimeRecord> Merges { get; set; } = new();
}

public class Diagnostic
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("hint")]
    public string? Hint { get; set; }

    public override string ToString()
    {
        if (string.IsNullOrEmpty(Hint))
        {
            return Message;
        }
        return $"{Message} {Hint}";
    }
}

public class CuratedReports
{
    [JsonPropertyName("work_classification")]
    public WorkClassificationMetrics WorkClassification { get; set; } = new();

    [JsonPropertyName("onboarding")]
    public List<DeveloperOnboardingMetric> Onboarding { get; set; } = new();

    [JsonPropertyName("code_rot")]
    public CodeRotMetric CodeRot { get; set; } = new();

    [JsonPropertyName("review_collaboration")]
    public ReviewCollaborationMetric ReviewCollaboration { get; set; } = new();

    [JsonPropertyName("ai_code_strain")]
    public AiCodeStrainMetric AiCodeStrain { get; set; } = new();
}

public class WorkClassificationMetrics
{
    [JsonPropertyName("features")]
    public int Features { get; set; }

    [JsonPropertyName("bugs")]
    public int Bugs { get; set; }

    [JsonPropertyName("technical_debt")]
    public int TechnicalDebt { get; set; }

    [JsonPropertyName("chores")]
    public int Chores { get; set; }

    [JsonPropertyName("unclassified")]
    public int Unclassified { get; set; }
}

public class DeveloperOnboardingMetric
{
    [JsonPropertyName("developer")]
    public string Developer { get; set; } = string.Empty;

    [JsonPropertyName("first_commit_date")]
    public string FirstCommitDate { get; set; } = string.Empty;

    [JsonPropertyName("days_active")]
    public int DaysActive { get; set; }
}

public class CodeRotMetric
{
    [JsonPropertyName("zombie_files")]
    public int ZombieFileCount { get; set; }

    [JsonPropertyName("zombie_lines")]
    public int ZombieLines { get; set; }

    [JsonPropertyName("threshold_days")]
    public int ThresholdDays { get; set; } = 365;
}

public class ReviewCollaborationMetric
{
    [JsonPropertyName("reviewer_silos")]
    public int ReviewerSilos { get; set; }

    [JsonPropertyName("collaboration_pairs")]
    public List<ReviewPair> Pairs { get; set; } = new();
}

public class ReviewPair
{
    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    [JsonPropertyName("reviewer")]
    public string Reviewer { get; set; } = string.Empty;

    [JsonPropertyName("pr_count")]
    public int PrCount { get; set; }
}

public class AiCodeStrainMetric
{
    [JsonPropertyName("high_volume_commits")]
    public int HighVolumeCommits { get; set; }

    [JsonPropertyName("review_velocity_warning")]
    public bool ReviewVelocityWarning { get; set; }
}

public class AnalysisResult
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "1.1";

    [JsonPropertyName("tool")]
    public string Tool { get; set; } = "gitic";

    [JsonPropertyName("analysis")]
    public AnalysisMetadata Analysis { get; set; } = new();

    [JsonPropertyName("settings")]
    public AnalysisSettings Settings { get; set; } = new();

    [JsonPropertyName("exclusions")]
    public List<ExclusionSummary> Exclusions { get; set; } = new();

    [JsonPropertyName("areas")]
    public List<AreaMetric> Areas { get; set; } = new();

    [JsonPropertyName("files")]
    public List<FileMetric> Files { get; set; } = new();

    [JsonPropertyName("contributors")]
    public List<ContributorMetric> Contributors { get; set; } = new();

    [JsonPropertyName("automation")]
    public List<AutomationMetric> Automation { get; set; } = new();

    [JsonPropertyName("temporal_coupling")]
    public List<TemporalCoupling>? TemporalCoupling { get; set; }

    [JsonPropertyName("lead_times")]
    public LeadTimesInfo? LeadTimes { get; set; }

    [JsonPropertyName("curated_reports")]
    public CuratedReports? CuratedReports { get; set; }

    [JsonPropertyName("configuration")]
    public AnalysisConfiguration Configuration { get; set; } = new();

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = new();

    [JsonPropertyName("diagnostics")]
    public List<Diagnostic> Diagnostics { get; set; } = new();
}

public class AnalysisMetadata
{
    [JsonPropertyName("repo_root")]
    public string RepoRoot { get; set; } = string.Empty;

    [JsonPropertyName("command")]
    public AnalysisCommand Command { get; set; }

    [JsonPropertyName("generated_at")]
    public string GeneratedAt { get; set; } = string.Empty;

    [JsonPropertyName("commit_count")]
    public int CommitCount { get; set; }

    [JsonPropertyName("included_file_change_count")]
    public int IncludedFileChangeCount { get; set; }
}

public class AnalysisConfiguration
{
    [JsonPropertyName("scoring")]
    public ScoringConfiguration Scoring { get; set; } = new();

    [JsonPropertyName("configured_alias_count")]
    public int ConfiguredAliasCount { get; set; }

    [JsonPropertyName("configured_bot_count")]
    public int ConfiguredBotCount { get; set; }

    [JsonPropertyName("configured_exclude_count")]
    public int ConfiguredExcludeCount { get; set; }

    [JsonPropertyName("configured_area_count")]
    public int ConfiguredAreaCount { get; set; }

    [JsonPropertyName("identity")]
    public IdentityConfigInfo Identity { get; set; } = new();
}

public class ScoringConfiguration
{
    [JsonPropertyName("attention")]
    public AttentionWeights Attention { get; set; } = new();
}

public class IdentityConfigInfo
{
    [JsonPropertyName("merge_on_email")]
    public bool MergeOnEmail { get; set; }
}

public class ContributorCredit
{
    public GitIdentity Identity { get; set; } = new();
    public double Activity { get; set; }
}

public class ItemAccumulator
{
    public string Key { get; set; } = string.Empty;
    public int Touches { get; set; }
    public int Added { get; set; }
    public int Deleted { get; set; }
    public int Churn { get; set; }
    public long LastTouched { get; set; }
    public HashSet<string> Files { get; set; } = new();
    public Dictionary<string, ContributorCredit> ContributorCredits { get; set; } = new();
    public Dictionary<string, int> Symbols { get; set; } = new();
    public int BugFixTouches { get; set; }
    public int FeatureTouches { get; set; }
}

public class ContributorAccumulator
{
    public GitIdentity Identity { get; set; } = new();
    public double TotalActivity { get; set; }
    public Dictionary<string, double> Areas { get; set; } = new();
}

public class CommitFileSet
{
    public List<string> Files { get; set; } = new();
}

public class SubsystemHealthStats
{
    [JsonPropertyName("hotspot_density")]
    public double HotspotDensity { get; set; }

    [JsonPropertyName("single_owner_ratio")]
    public double SingleOwnerRatio { get; set; }

    [JsonPropertyName("rework_rate")]
    public double ReworkRate { get; set; }

    [JsonPropertyName("active_contributors")]
    public int ActiveContributors { get; set; }
}

public class SubsystemDeliveryStats
{
    [JsonPropertyName("median_lead_time_hours")]
    public double MedianLeadTimeHours { get; set; }

    [JsonPropertyName("tail_lead_time_hours")]
    public double TailLeadTimeHours { get; set; }

    [JsonPropertyName("stall_rate")]
    public double StallRate { get; set; }
}
