using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Gitic.Tests
{
    public class CliTableRendererRefactoredTests
    {
        [Fact]
        public async Task TestRefactored_HotspotsTable()
        {
            var result = new AnalysisResult
            {
                Analysis = new AnalysisMetadata { IncludedFileChangeCount = 5 },
                Files = new List<FileMetric>
                {
                    new() { Path = "src/A.cs", AttentionScore = 85.0, HeatScore = 90.0, Churn = 100, ContributorCount = 2, ScoreBreakdown = new ScoreBreakdown() },
                    new() { Path = "src/B.cs", AttentionScore = 40.0, HeatScore = 30.0, Churn = 50, ContributorCount = 1, ScoreBreakdown = new ScoreBreakdown() }
                }
            };

            var settings = new AnalysisSettings { Format = "plain", Limit = 1 };
            var renderer = new CliTableRenderer(AnalysisCommand.Hotspots, settings);
            string output = await renderer.RenderAsync(result);

            Assert.Contains("src/A.cs", output);
            Assert.Contains("[!]", output); // plain format uses ASCII "[!]" for high attention
            Assert.DoesNotContain("src/B.cs", output); // limited to 1 row
        }

        [Fact]
        public async Task TestRefactored_TemporalCouplingTable()
        {
            var result = new AnalysisResult
            {
                Analysis = new AnalysisMetadata { IncludedFileChangeCount = 10 },
                TemporalCoupling = new List<TemporalCoupling>
                {
                    new() { FileA = "src/Main.cs", FileB = "src/Helper.cs", SharedCommits = 8, CouplingDegree = 0.85 }
                }
            };

            var settings = new AnalysisSettings { Format = "plain" };
            var renderer = new CliTableRenderer(AnalysisCommand.TemporalCoupling, settings);
            string output = await renderer.RenderAsync(result);

            Assert.Contains("src/Main.cs", output);
            Assert.Contains("src/Helper.cs", output);
            Assert.Contains("85%", output); // coupling degree formatting
        }

        [Fact]
        public async Task TestRefactored_LeadTimeTable()
        {
            var result = new AnalysisResult
            {
                Analysis = new AnalysisMetadata { IncludedFileChangeCount = 10 },
                LeadTimes = new LeadTimesInfo
                {
                    AverageLeadTimeHours = 12.5,
                    Merges = new List<MergeLeadTimeRecord>
                    {
                        new() { Hash = "abc1234", Date = "2026-07-28T12:00:00Z", LeadTimeHours = 12.5, Author = "Alice", FileCount = 1, Message = "Merge A" }
                    }
                }
            };

            var settings = new AnalysisSettings { Format = "plain" };
            var renderer = new CliTableRenderer(AnalysisCommand.LeadTime, settings);
            string output = await renderer.RenderAsync(result);

            Assert.Contains("Average Lead Time: 12.5 hours", output);
            Assert.Contains("abc1234", output);
            Assert.Contains("12.5 hours", output);
        }

        [Fact]
        public async Task TestRefactored_AreaTable()
        {
            var result = new AnalysisResult
            {
                Analysis = new AnalysisMetadata { IncludedFileChangeCount = 10 },
                Areas = new List<AreaMetric>
                {
                    new() { Area = "src/Auth", AttentionScore = 60.0, HeatScore = 50.0, ContributorCount = 3, ScoreBreakdown = new ScoreBreakdown() }
                }
            };

            var settings = new AnalysisSettings { Format = "plain" };
            var renderer = new CliTableRenderer(AnalysisCommand.Areas, settings);
            string output = await renderer.RenderAsync(result);

            Assert.Contains("src/Auth", output);
            Assert.Contains("60.0", output);
        }

        [Fact]
        public async Task TestRefactored_ContributorTable()
        {
            var result = new AnalysisResult
            {
                Analysis = new AnalysisMetadata { IncludedFileChangeCount = 10 },
                Contributors = new List<ContributorMetric>
                {
                    new() { Name = "Alice", TotalActivity = 40, Areas = new List<ContributorAreaMetric>() }
                },
                Automation = new List<AutomationMetric>
                {
                    new() { Name = "Bot-Builder", TotalActivity = 10, Areas = new List<ContributorAreaMetric>() }
                }
            };

            var settings = new AnalysisSettings { Format = "plain", Sort = "activity" };
            var renderer = new CliTableRenderer(AnalysisCommand.Contributors, settings);
            string output = await renderer.RenderAsync(result);

            Assert.Contains("Alice", output);
            Assert.Contains("Bot-Builder", output);
            Assert.Contains("human", output);
            Assert.Contains("bot", output);
        }

        [Fact]
        public async Task TestRefactored_HotspotsTable_WithBordersAndBanner()
        {
            var result = new AnalysisResult
            {
                Analysis = new AnalysisMetadata 
                { 
                    IncludedFileChangeCount = 5,
                    RepoRoot = "/test/repo",
                    CommitCount = 10,
                    Command = AnalysisCommand.Hotspots
                },
                Settings = new AnalysisSettings { Format = "human", Color = "always" },
                Files = new List<FileMetric>
                {
                    new() { Path = "src/A.cs", AttentionScore = 85.0, HeatScore = 90.0, Churn = 100, ContributorCount = 2, ScoreBreakdown = new ScoreBreakdown() }
                }
            };

            var renderer = new CliTableRenderer(AnalysisCommand.Hotspots, result.Settings);
            string output = await renderer.RenderAsync(result);

            // 1. Verify ASCII art banner and header details exist
            Assert.Contains("___ _ _", output);
            Assert.Contains("Strategic Codebase Analysis", output);
            Assert.Contains("Repository: /test/repo", output);
            Assert.Contains("Commits: 10 | Files: 5", output);

            // 2. Verify Box-Drawing borders exist in human format
            Assert.Contains("┌", output);
            Assert.Contains("─", output);
            Assert.Contains("│", output);
            Assert.Contains("└", output);

            // 3. Verify ANSI colors are utilized for headers in color always mode
            Assert.Contains("\x1b[1;36m", output); // cyan header coloring
        }
    }
}