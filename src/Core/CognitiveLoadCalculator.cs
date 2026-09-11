using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using Kfc.Cli.Terminal;

namespace Gitic;

/// <summary>
/// Qualitative cognitive load classification for developers.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CognitiveLoadClassification
{
    Specialist,
    Focused,
    Moderate,
    Watch,
    Overloaded
}

/// <summary>
/// Represents the quantified cognitive load, context-switching overhead, area breadth,
/// and Shannon entropy profile for an individual developer.
/// </summary>
public class ContributorCognitiveProfile
{
    [JsonPropertyName("developer")]
    public string Developer { get; set; } = string.Empty;

    [JsonPropertyName("developer_email")]
    public string DeveloperEmail { get; set; } = string.Empty;

    [JsonIgnore]
    public string Email
    {
        get => DeveloperEmail;
        set => DeveloperEmail = value;
    }

    [JsonPropertyName("area_breadth")]
    public int AreaBreadth { get; set; }

    [JsonPropertyName("file_breadth")]
    public int FileBreadth { get; set; }

    [JsonPropertyName("area_entropy")]
    public double AreaEntropy { get; set; }

    [JsonPropertyName("estimated_switches_per_week")]
    public double EstimatedSwitchesPerWeek { get; set; }

    [JsonPropertyName("estimated_overhead_hours_per_week")]
    public double EstimatedOverheadHoursPerWeek { get; set; }

    [JsonPropertyName("specialization_index")]
    public double SpecializationIndex { get; set; }

    [JsonPropertyName("load_classification")]
    public CognitiveLoadClassification LoadClassification { get; set; }

    [JsonPropertyName("total_commits")]
    public int TotalCommits { get; set; }

    [JsonPropertyName("total_switches")]
    public int TotalSwitches { get; set; }

    [JsonPropertyName("primary_area")]
    public string PrimaryArea { get; set; } = string.Empty;

    [JsonPropertyName("rework_rate_vs_team_avg")]
    public double? ReworkRateVsTeamAvg { get; set; }

    [JsonPropertyName("insight")]
    public string Insight { get; set; } = string.Empty;

    [JsonPropertyName("area_distribution")]
    public Dictionary<string, int> AreaDistribution { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonIgnore]
    public string Status => LoadClassification.ToString();

    [JsonIgnore]
    public bool IsOverloaded => LoadClassification == CognitiveLoadClassification.Overloaded;

    [JsonIgnore]
    public bool IsSpecialist => LoadClassification == CognitiveLoadClassification.Specialist || SpecializationIndex >= 0.85;

    [JsonIgnore]
    public bool IsFocused => LoadClassification == CognitiveLoadClassification.Focused;
}

public partial class AnalysisResult
{
    [JsonPropertyName("cognitive_profiles")]
    public List<ContributorCognitiveProfile>? CognitiveProfiles { get; set; }
}

/// <summary>
/// Interface for measuring contributor cognitive load and context-switching overhead.
/// </summary>
public interface ICognitiveLoadCalculator
{
    List<ContributorCognitiveProfile> CalculateProfiles(
        List<GitCommitRecord> commits,
        List<AreaMetric>? areas = null,
        IAreaMapper? mapper = null);

    string FormatSummaryTable(IEnumerable<ContributorCognitiveProfile> profiles);
    string FormatMarkdown(IEnumerable<ContributorCognitiveProfile> profiles);
}

/// <summary>
/// Calculates per-developer context-switching frequency, area breadth, and Shannon entropy
/// from commit sequences to quantify cognitive overhead.
/// </summary>
public class CognitiveLoadCalculator : ICognitiveLoadCalculator
{
    private const double SwitchOverheadHours = 0.25; // 15 minutes = 0.25 hours
    private const double MsPerDay = 86400000.0;
    private const double DaysPerWeek = 7.0;

    /// <summary>
    /// Static convenience method to compute cognitive profiles for all contributors.
    /// </summary>
    public static List<ContributorCognitiveProfile> Calculate(
        List<GitCommitRecord> commits,
        List<AreaMetric>? areas = null,
        IAreaMapper? mapper = null)
    {
        return new CognitiveLoadCalculator().CalculateProfiles(commits, areas, mapper);
    }

    /// <summary>
    /// Computes Shannon entropy (H = -sum(p * log2(p))) from an activity distribution.
    /// </summary>
    public static double CalculateShannonEntropy(IEnumerable<int> distribution)
    {
        var counts = distribution?.Where(c => c > 0).ToList() ?? new List<int>();
        if (counts.Count <= 1)
        {
            return 0.0;
        }

        double total = counts.Sum();
        if (total <= 0)
        {
            return 0.0;
        }

        double entropy = 0.0;
        foreach (int count in counts)
        {
            double p = count / total;
            if (p > 0.0)
            {
                entropy -= p * Math.Log2(p);
            }
        }

        return Math.Round(Math.Max(0.0, entropy), 2);
    }

    /// <summary>
    /// Computes Shannon entropy (H = -sum(p * log2(p))) from probability values.
    /// </summary>
    public static double CalculateShannonEntropy(IEnumerable<double> probabilities)
    {
        var probs = probabilities?.Where(p => p > 0.0).ToList() ?? new List<double>();
        if (probs.Count <= 1)
        {
            return 0.0;
        }

        double total = probs.Sum();
        double entropy = 0.0;
        foreach (double rawP in probs)
        {
            double p = total > 0.0 ? rawP / total : rawP;
            if (p > 0.0)
            {
                entropy -= p * Math.Log2(p);
            }
        }

        return Math.Round(Math.Max(0.0, entropy), 2);
    }

    /// <summary>
    /// Computes specialization index (0 = pure generalist, 1 = pure specialist) from Shannon entropy.
    /// </summary>
    public static double CalculateSpecializationIndex(double entropy, int totalAreas)
    {
        if (totalAreas <= 1 || entropy <= 0.0)
        {
            return 1.0;
        }

        double maxEntropy = Math.Log2(Math.Max(2, totalAreas));
        if (maxEntropy <= 0.0)
        {
            return 1.0;
        }

        double normalizedEntropy = Math.Clamp(entropy / maxEntropy, 0.0, 1.0);
        return Math.Round(1.0 - normalizedEntropy, 2);
    }

    /// <summary>
    /// Classifies contributor cognitive load and generates a narrative recommendation.
    /// </summary>
    public static (CognitiveLoadClassification Classification, string Insight) ClassifyLoad(
        double switchesPerWeek,
        int areaBreadth,
        double entropy,
        double specializationIndex,
        string primaryArea,
        string developer)
    {
        CognitiveLoadClassification classification;

        if (switchesPerWeek >= 10.0 || areaBreadth >= 7 || entropy >= 2.5)
        {
            classification = CognitiveLoadClassification.Overloaded;
        }
        else if (switchesPerWeek >= 5.0 || areaBreadth >= 4 || entropy >= 1.6)
        {
            classification = CognitiveLoadClassification.Watch;
        }
        else if (areaBreadth <= 1 || (specializationIndex >= 0.85 && switchesPerWeek <= 2.0))
        {
            classification = CognitiveLoadClassification.Specialist;
        }
        else
        {
            classification = CognitiveLoadClassification.Focused;
        }

        string primary = string.IsNullOrEmpty(primaryArea) ? "primary module" : primaryArea;
        string insight = classification switch
        {
            CognitiveLoadClassification.Overloaded =>
                $"{developer}'s high context-switching ({switchesPerWeek:F1}/wk across {areaBreadth} areas) indicates cognitive overload. Consider consolidating focus to {primary} and redistributing peripheral tasks.",
            CognitiveLoadClassification.Watch =>
                $"{developer} is active across {areaBreadth} areas with {switchesPerWeek:F1} switches/wk. Monitor workload to prevent further context fragmentation.",
            CognitiveLoadClassification.Moderate =>
                $"{developer} handles moderate context-switching ({switchesPerWeek:F1}/wk across {areaBreadth} areas). Workload distribution is manageable.",
            CognitiveLoadClassification.Specialist =>
                $"{developer} is dedicated to {primary} with minimal context-switching overhead ({switchesPerWeek:F1} switches/wk, specialization index {specializationIndex:F2}).",
            _ =>
                $"{developer} maintains healthy focus across {areaBreadth} areas with low context-switching overhead ({switchesPerWeek:F1} switches/wk)."
        };

        return (classification, insight);
    }

    public List<ContributorCognitiveProfile> CalculateProfiles(
        List<GitCommitRecord> commits,
        List<AreaMetric>? areas = null,
        IAreaMapper? mapper = null)
    {
        return CalculateProfiles((IEnumerable<GitCommitRecord>)commits, areas, mapper);
    }

    public List<ContributorCognitiveProfile> CalculateProfiles(
        IEnumerable<GitCommitRecord> commits,
        IEnumerable<AreaMetric>? areas = null,
        IAreaMapper? mapper = null)
    {
        if (commits == null)
        {
            return new List<ContributorCognitiveProfile>();
        }

        var commitList = commits.ToList();
        if (commitList.Count == 0)
        {
            return new List<ContributorCognitiveProfile>();
        }

        var areaList = areas?.ToList() ?? new List<AreaMetric>();
        var areaMapper = mapper ?? new AreaMapper();

        // Build named area patterns from passed AreaMetrics
        var namedAreas = areaList
            .Where(a => !string.IsNullOrWhiteSpace(a.Area))
            .Select(a => new NamedArea
            {
                Name = a.Area,
                Paths = new List<string> { a.Area, $"{a.Area}/**", $"{a.Area}/*", $"**/{a.Area}/**", $"**/{a.Area}/*" }
            })
            .ToList();

        // Helper to map file path to area
        string GetAreaForPath(string path)
        {
            string normPath = PathUtils.NormalizeGitPath(path);
            return areaMapper.AreaForPath(normPath, 2, namedAreas);
        }

        // Collect all distinct areas across all commits/areas
        var allDiscoveredAreas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in areaList)
        {
            if (!string.IsNullOrWhiteSpace(a.Area))
            {
                allDiscoveredAreas.Add(a.Area);
            }
        }

        foreach (var commit in commitList)
        {
            foreach (var file in commit.Files)
            {
                string area = GetAreaForPath(file.Path);
                if (!string.IsNullOrEmpty(area))
                {
                    allDiscoveredAreas.Add(area);
                }
            }
        }

        int totalRepoAreas = Math.Max(allDiscoveredAreas.Count, areaList.Count);

        // Group commits by author identity
        var authorGroups = commitList
            .GroupBy(c => ResolveAuthorKey(c.Author), StringComparer.OrdinalIgnoreCase)
            .ToList();

        var profiles = new List<ContributorCognitiveProfile>();

        foreach (var group in authorGroups)
        {
            var authorCommits = group.ToList();
            var primaryIdentity = authorCommits
                .Select(c => c.Author)
                .FirstOrDefault(a => !string.IsNullOrWhiteSpace(a?.Name) || !string.IsNullOrWhiteSpace(a?.Email))
                ?? new GitIdentity();

            string devName = !string.IsNullOrWhiteSpace(primaryIdentity.Name)
                ? primaryIdentity.Name
                : (!string.IsNullOrWhiteSpace(primaryIdentity.Email) ? primaryIdentity.Email : group.Key);
            string devEmail = primaryIdentity.Email ?? string.Empty;

            // Sort author commits chronologically
            var sortedCommits = authorCommits
                .OrderBy(c => c.Timestamp > 0 ? c.Timestamp : ParseTimestamp(c.Date))
                .ToList();

            var distinctFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var areaDistribution = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            int totalSwitches = 0;
            string? previousArea = null;

            foreach (var commit in sortedCommits)
            {
                if (commit.Files == null || commit.Files.Count == 0)
                {
                    continue;
                }

                // Determine commit's areas and primary area
                var commitAreaCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var file in commit.Files)
                {
                    string normPath = PathUtils.NormalizeGitPath(file.Path);
                    distinctFiles.Add(normPath);

                    string area = GetAreaForPath(normPath);
                    if (string.IsNullOrEmpty(area))
                    {
                        area = ".";
                    }

                    commitAreaCounts[area] = commitAreaCounts.GetValueOrDefault(area) + 1;
                    areaDistribution[area] = areaDistribution.GetValueOrDefault(area) + 1;
                }

                string currentCommitArea = commitAreaCounts
                    .OrderByDescending(kv => kv.Value)
                    .Select(kv => kv.Key)
                    .FirstOrDefault() ?? ".";

                if (previousArea != null && !string.Equals(previousArea, currentCommitArea, StringComparison.OrdinalIgnoreCase))
                {
                    totalSwitches++;
                }

                previousArea = currentCommitArea;
            }

            int areaBreadth = areaDistribution.Count;
            int fileBreadth = distinctFiles.Count;

            // Calculate active time span
            long minTimestamp = sortedCommits
                .Select(c => c.Timestamp > 0 ? c.Timestamp : ParseTimestamp(c.Date))
                .Where(t => t > 0)
                .DefaultIfEmpty(0)
                .Min();

            long maxTimestamp = sortedCommits
                .Select(c => c.Timestamp > 0 ? c.Timestamp : ParseTimestamp(c.Date))
                .Where(t => t > 0)
                .DefaultIfEmpty(0)
                .Max();

            double spanDays = 0.0;
            if (maxTimestamp > minTimestamp)
            {
                spanDays = (maxTimestamp - minTimestamp) / MsPerDay;
            }

            double weeks = spanDays <= 0.0 ? 1.0 : Math.Max(1.0, spanDays / DaysPerWeek);
            double switchesPerWeek = Math.Round(totalSwitches / weeks, 1);
            double overheadHoursPerWeek = Math.Round(switchesPerWeek * SwitchOverheadHours, 2);

            double entropy = CalculateShannonEntropy(areaDistribution.Values);
            double specializationIndex = CalculateSpecializationIndex(entropy, totalRepoAreas);

            string primaryArea = areaDistribution
                .OrderByDescending(kv => kv.Value)
                .Select(kv => kv.Key)
                .FirstOrDefault() ?? string.Empty;

            var (classification, insight) = ClassifyLoad(
                switchesPerWeek,
                areaBreadth,
                entropy,
                specializationIndex,
                primaryArea,
                devName);

            profiles.Add(new ContributorCognitiveProfile
            {
                Developer = devName,
                DeveloperEmail = devEmail,
                AreaBreadth = areaBreadth,
                FileBreadth = fileBreadth,
                AreaEntropy = entropy,
                EstimatedSwitchesPerWeek = switchesPerWeek,
                EstimatedOverheadHoursPerWeek = overheadHoursPerWeek,
                SpecializationIndex = specializationIndex,
                LoadClassification = classification,
                TotalCommits = sortedCommits.Count,
                TotalSwitches = totalSwitches,
                PrimaryArea = primaryArea,
                Insight = insight,
                AreaDistribution = areaDistribution
            });
        }

        // Rank profiles: Overloaded first, then highest switches/week, then largest area breadth
        return profiles
            .OrderByDescending(p => p.LoadClassification == CognitiveLoadClassification.Overloaded)
            .ThenByDescending(p => p.EstimatedSwitchesPerWeek)
            .ThenByDescending(p => p.AreaBreadth)
            .ThenBy(p => p.Developer, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public string FormatSummaryTable(IEnumerable<ContributorCognitiveProfile> profiles)
    {
        return FormatCliTable(profiles);
    }

    public string FormatCliTable(IEnumerable<ContributorCognitiveProfile> profiles)
    {
        var list = profiles?.ToList() ?? new List<ContributorCognitiveProfile>();
        if (list.Count == 0)
        {
            return "No contributor cognitive profile data available.\n";
        }

        int consoleWidth = ConsoleUtils.GetBoundedConsoleWidth(null);
        var visibleColumns = consoleWidth < 90
            ? new List<string> { "developer", "areas", "files", "switches_wk", "classification" }
            : consoleWidth < 120
                ? new List<string> { "developer", "areas", "files", "entropy", "switches_wk", "overhead", "classification" }
                : new List<string> { "developer", "areas", "files", "entropy", "switches_wk", "overhead", "specialization", "classification" };

        var table = new ConsoleTableBuilder()
            .WithConsoleWidth(consoleWidth)
            .WithVisibleColumns(visibleColumns)
            .WithBorders(true, true, true)
            .AddColumnEx("developer", align: "left", widthPolicy: WidthPolicy.Stretch, truncation: TruncationStyle.Standard, stretchRatio: 0.35, minWidth: 14)
            .AddColumnEx("areas", width: 8, align: "right")
            .AddColumnEx("files", width: 8, align: "right")
            .AddColumnEx("entropy", width: 10, align: "right")
            .AddColumnEx("switches_wk", width: 14, align: "right")
            .AddColumnEx("overhead", width: 16, align: "right")
            .AddColumnEx("specialization", width: 16, align: "right")
            .AddColumnEx("classification", width: 16, align: "center");

        foreach (var p in list)
        {
            table.AddRow(new Dictionary<string, string>
            {
                { "developer", p.Developer },
                { "areas", p.AreaBreadth.ToString() },
                { "files", p.FileBreadth.ToString() },
                { "entropy", p.AreaEntropy.ToString("F2") },
                { "switches_wk", p.EstimatedSwitchesPerWeek.ToString("F1") },
                { "overhead", $"{p.EstimatedOverheadHoursPerWeek:F1} hrs/wk" },
                { "specialization", p.SpecializationIndex.ToString("F2") },
                { "classification", p.LoadClassification.ToString() }
            });
        }

        return table.Render();
    }

    public string FormatMarkdown(IEnumerable<ContributorCognitiveProfile> profiles)
    {
        var list = profiles?.ToList() ?? new List<ContributorCognitiveProfile>();
        var sb = new StringBuilder();

        sb.AppendLine("## 🧠 Cognitive Load & Context-Switching Map");
        sb.AppendLine();

        if (list.Count == 0)
        {
            sb.AppendLine("No cognitive load profiles available for the analyzed commit history.");
            sb.AppendLine();
            return sb.ToString();
        }

        sb.AppendLine("Quantifies developer context-switching frequency, area breadth, and Shannon entropy across codebase subsystems.");
        sb.AppendLine();

        // Summary table
        sb.AppendLine("| Developer | Area Breadth | File Breadth | Area Entropy | Switches / Wk | Overhead (hrs/wk) | Specialization Index | Status |");
        sb.AppendLine("| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :--- |");

        foreach (var p in list)
        {
            string statusEmoji = p.LoadClassification switch
            {
                CognitiveLoadClassification.Overloaded => "⚠️ Overloaded",
                CognitiveLoadClassification.Watch => "👀 Watch",
                CognitiveLoadClassification.Moderate => "👀 Moderate",
                CognitiveLoadClassification.Specialist => "✅ Specialist",
                _ => "✅ Focused"
            };

            sb.AppendLine($"| **{p.Developer}** | {p.AreaBreadth} | {p.FileBreadth} | {p.AreaEntropy:F2} | {p.EstimatedSwitchesPerWeek:F1} | {p.EstimatedOverheadHoursPerWeek:F1}h | {p.SpecializationIndex:F2} | {statusEmoji} |");
        }
        sb.AppendLine();

        // Visual Distribution & Narrative Insights
        sb.AppendLine("### 📊 Contributor Cognitive Profiles & Insights");
        sb.AppendLine();

        foreach (var p in list)
        {
            int barLength = Math.Clamp((int)Math.Round(p.EstimatedSwitchesPerWeek * 1.5), 2, 24);
            string bar = new string('█', barLength);

            sb.AppendLine($"#### {p.Developer}");
            sb.AppendLine($"- **Load Indicator:** `{bar}` ({p.AreaBreadth} areas, {p.EstimatedSwitchesPerWeek:F1} switches/wk — **{p.LoadClassification}**)");
            sb.AppendLine($"- **Shannon Entropy:** {p.AreaEntropy:F2} (Specialization Index: {p.SpecializationIndex:F2})");
            sb.AppendLine($"- **Estimated Switching Overhead:** {p.EstimatedOverheadHoursPerWeek:F1} hours/week (at 15 min per switch)");
            if (!string.IsNullOrEmpty(p.PrimaryArea))
            {
                sb.AppendLine($"- **Primary Area:** `{p.PrimaryArea}`");
            }
            if (!string.IsNullOrEmpty(p.Insight))
            {
                sb.AppendLine($"> **Insight:** {p.Insight}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string ResolveAuthorKey(GitIdentity identity)
    {
        if (identity == null) return "unknown";
        if (!string.IsNullOrWhiteSpace(identity.Name)) return identity.Name.Trim();
        if (!string.IsNullOrWhiteSpace(identity.Email)) return identity.Email.Trim();
        return "unknown";
    }

    private static long ParseTimestamp(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr)) return 0;
        if (DateTimeOffset.TryParse(dateStr.Trim(), out var dt))
        {
            return dt.ToUnixTimeMilliseconds();
        }
        return 0;
    }
}
