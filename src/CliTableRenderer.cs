using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Gitic
{
    public class CliReportFormatter
    {
        private readonly AnalysisResult _result;

        public CliReportFormatter(AnalysisResult result)
        {
            _result = result;
        }

        public string Format(string tableString, bool includeWarnings = false)
        {
            if (tableString.StartsWith("No contributor activity matched"))
            {
                return tableString;
            }

            string output = $"{tableString}\n";
            output += RenderExclusions(_result);
            if (includeWarnings)
            {
                output += RenderWarnings(_result);
            }
            return output;
        }

        private string RenderExclusions(AnalysisResult result)
        {
            if (result.Exclusions == null || result.Exclusions.Count == 0)
            {
                return "";
            }
            string summary = string.Join(", ", result.Exclusions
                .Select(exclusion => $"{exclusion.Category}:{exclusion.Count}"));
            return $"exclusions {summary}\n";
        }

        private string RenderWarnings(AnalysisResult result)
        {
            if (result.Warnings == null || result.Warnings.Count == 0)
            {
                return "";
            }
            return $"warnings {string.Join("; ", result.Warnings)}\n";
        }
    }

    public class CliTableRenderer : IReportRenderer
    {
        private readonly AnalysisCommand _command;

        public CliTableRenderer(AnalysisCommand command)
        {
            _command = command;
        }

        public Task<string> RenderAsync(AnalysisResult result)
        {
            if (result.Analysis.IncludedFileChangeCount == 0)
            {
                return Task.FromResult("No commits matched the selected analysis window. Try --all-time or a wider --since value.\n");
            }

            var formatter = new CliReportFormatter(result);

            if (_command == AnalysisCommand.Contributors)
            {
                string tableString = RenderContributorTable(result);
                return Task.FromResult(formatter.Format(tableString, includeWarnings: false));
            }
            if (_command == AnalysisCommand.Contributor)
            {
                string tableString = RenderSingleContributorTable(result);
                return Task.FromResult(formatter.Format(tableString, includeWarnings: false));
            }
            if (_command == AnalysisCommand.Areas)
            {
                string tableString = RenderAreaTable(result);
                return Task.FromResult(formatter.Format(tableString, includeWarnings: true));
            }

            // Default fallback to hotspots
            {
                string tableString = RenderHotspotTable(result);
                return Task.FromResult(formatter.Format(tableString, includeWarnings: true));
            }
        }

        private string RenderHotspotTable(AnalysisResult result)
        {
            IConsoleTableBuilder table = new ConsoleTableBuilder()
                .AddColumn("file", 28, "left")
                .AddColumn("attention", 9, "right")
                .AddColumn("heat", 5, "right")
                .AddColumn("churn", 6, "right")
                .AddColumn("contributors", 12, "right")
                .AddColumn("top activity share", 24, "left")
                .AddColumn("reasons");

            var filesToRender = result.Files.Take(20).ToList();
            foreach (var file in filesToRender)
            {
                table.AddRow(new List<string>
                {
                    file.Path,
                    file.AttentionScore.ToString(),
                    file.HeatScore.ToString(),
                    file.Churn.ToString(),
                    file.ContributorCount.ToString(),
                    TopContributor(file.Contributors),
                    ScoreReasons(file.ScoreBreakdown)
                });
            }

            return table.Render();
        }

        private string RenderAreaTable(AnalysisResult result)
        {
            IConsoleTableBuilder table = new ConsoleTableBuilder()
                .AddColumn("area", 28, "left")
                .AddColumn("heat", 5, "right")
                .AddColumn("attention", 9, "right")
                .AddColumn("churn", 6, "right")
                .AddColumn("contributors", 12, "right")
                .AddColumn("top activity share", 24, "left")
                .AddColumn("reasons");

            foreach (var area in result.Areas)
            {
                table.AddRow(new List<string>
                {
                    area.Area,
                    area.HeatScore.ToString(),
                    area.AttentionScore.ToString(),
                    area.Churn.ToString(),
                    area.ContributorCount.ToString(),
                    TopContributor(area.Contributors),
                    ScoreReasons(area.ScoreBreakdown)
                });
            }

            return table.Render();
        }

        private string RenderContributorTable(AnalysisResult result)
        {
            IConsoleTableBuilder table = new ConsoleTableBuilder()
                .AddColumn("contributor", 24, "left")
                .AddColumn("activity", 8, "right")
                .AddColumn("top area");

            foreach (var contributor in result.Contributors)
            {
                string topArea = contributor.Areas.Count > 0 ? contributor.Areas[0].Area : "";
                table.AddRow(new List<string>
                {
                    contributor.Name,
                    contributor.TotalActivity.ToString(),
                    topArea
                });
            }

            return table.Render();
        }

        private string RenderSingleContributorTable(AnalysisResult result)
        {
            if (result.Contributors == null || result.Contributors.Count == 0)
            {
                return "No contributor activity matched the selected analysis.\n";
            }

            var contributor = result.Contributors[0];
            IConsoleTableBuilder table = new ConsoleTableBuilder()
                .AddColumn("area", 28, "left")
                .AddColumn("familiarity", 11, "right")
                .AddColumn("activity", 8, "right")
                .AddColumn("share");

            foreach (var area in contributor.Areas)
            {
                table.AddRow(new List<string>
                {
                    area.Area,
                    area.FamiliarityScore.ToString(),
                    area.Activity.ToString(),
                    $"{Math.Round(area.ActivityShare * 100)}%"
                });
            }

            return $"{contributor.Name} <{contributor.Email}>\n{table.Render()}";
        }

        private string TopContributor(List<ContributorShare> contributors)
        {
            if (contributors == null || contributors.Count == 0)
            {
                return "";
            }
            var contributor = contributors[0];
            return $"{contributor.Name} {Math.Round(contributor.ActivityShare * 100)}%";
        }

        private string ScoreReasons(ScoreBreakdown breakdown)
        {
            var reasons = new List<Tuple<string, double>>
            {
                Tuple.Create("churn", breakdown.Churn),
                Tuple.Create("recent", breakdown.Recency),
                Tuple.Create("spread", breakdown.ContributorSpread),
                Tuple.Create("low familiarity", breakdown.LowFamiliarityConcentration)
            };

            var sortedReasons = reasons
                .OrderByDescending(r => r.Item2)
                .Take(2)
                .Where(r => r.Item2 > 0)
                .Select(r => $"{r.Item1} {Math.Round(r.Item2 * 100)}%")
                .ToList();

            return sortedReasons.Count == 0 ? "no activity" : string.Join(", ", sortedReasons);
        }
    }
}
