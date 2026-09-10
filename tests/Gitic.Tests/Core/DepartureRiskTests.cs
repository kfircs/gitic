using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Kfc.Cli.Core;
using Xunit;

namespace Gitic.Tests;

public class DepartureRiskDominantFilesTests
{
    [Fact]
    public void TestDominantFiles_SoleContributor_IsCriticalRisk()
    {
        var result = new AnalysisResult
        {
            Files = new List<FileMetric>
            {
                new()
                {
                    Path = "src/Git/Gitgraph.cs",
                    Area = "Git",
                    Touches = 15,
                    Lines = 450,
                    Contributors = new List<ContributorShare>
                    {
                        new() { Name = "Bob", Email = "bob@example.com", Activity = 15, ActivityShare = 1.0 }
                    }
                }
            },
            Contributors = new List<ContributorMetric>
            {
                new() { Name = "Bob", Email = "bob@example.com", TotalActivity = 15 }
            }
        };

        var analyzer = new DepartureRiskAnalyzer();
        var report = analyzer.AnalyzeDepartureRisk(result, "Bob");

        Assert.Single(report.CriticalRiskFiles);
        Assert.Empty(report.ModerateRiskFiles);
        Assert.Equal("src/Git/Gitgraph.cs", report.CriticalRiskFiles[0].Path);
        Assert.Equal(DepartureRiskLevel.Critical, report.CriticalRiskFiles[0].RiskLevel);
        Assert.True(report.CriticalRiskFiles[0].IsSoleOwner);
        Assert.Equal(1.0, report.CriticalRiskFiles[0].OwnershipShare);
        Assert.Equal(1, report.TotalAffectedFiles);
        Assert.Equal(1, report.TotalAffectedAreas);
        Assert.Equal(450, report.TotalAffectedLines);
    }

    [Fact]
    public void TestDominantFiles_HighShareGreaterThan70_IsCriticalRisk()
    {
        var result = new AnalysisResult
        {
            Files = new List<FileMetric>
            {
                new()
                {
                    Path = "src/Git/GitPatchParser.cs",
                    Area = "Git",
                    Touches = 20,
                    Lines = 300,
                    Contributors = new List<ContributorShare>
                    {
                        new() { Name = "Bob", Email = "bob@example.com", Activity = 16, ActivityShare = 0.80 },
                        new() { Name = "Alice", Email = "alice@example.com", Activity = 4, ActivityShare = 0.20 }
                    }
                }
            },
            Contributors = new List<ContributorMetric>
            {
                new() { Name = "Bob", Email = "bob@example.com", TotalActivity = 16 },
                new() { Name = "Alice", Email = "alice@example.com", TotalActivity = 4 }
            }
        };

        var analyzer = new DepartureRiskAnalyzer();
        var report = analyzer.AnalyzeDepartureRisk(result, "Bob");

        Assert.Single(report.CriticalRiskFiles);
        Assert.Empty(report.ModerateRiskFiles);
        Assert.Equal("src/Git/GitPatchParser.cs", report.CriticalRiskFiles[0].Path);
        Assert.Equal(DepartureRiskLevel.Critical, report.CriticalRiskFiles[0].RiskLevel);
        Assert.False(report.CriticalRiskFiles[0].IsSoleOwner);
        Assert.Equal(0.80, report.CriticalRiskFiles[0].OwnershipShare);
    }

    [Fact]
    public void TestDominantFiles_ModerateShareBetween50And70_IsModerateRisk()
    {
        var result = new AnalysisResult
        {
            Files = new List<FileMetric>
            {
                new()
                {
                    Path = "src/Git/Git.cs",
                    Area = "Git",
                    Touches = 25,
                    Lines = 600,
                    Contributors = new List<ContributorShare>
                    {
                        new() { Name = "Bob", Email = "bob@example.com", Activity = 15, ActivityShare = 0.60 },
                        new() { Name = "Alice", Email = "alice@example.com", Activity = 6, ActivityShare = 0.24 },
                        new() { Name = "Carol", Email = "carol@example.com", Activity = 4, ActivityShare = 0.16 }
                    }
                }
            },
            Contributors = new List<ContributorMetric>
            {
                new() { Name = "Bob", Email = "bob@example.com", TotalActivity = 15 },
                new() { Name = "Alice", Email = "alice@example.com", TotalActivity = 6 },
                new() { Name = "Carol", Email = "carol@example.com", TotalActivity = 4 }
            }
        };

        var analyzer = new DepartureRiskAnalyzer();
        var report = analyzer.AnalyzeDepartureRisk(result, "Bob");

        Assert.Empty(report.CriticalRiskFiles);
        Assert.Single(report.ModerateRiskFiles);
        Assert.Equal("src/Git/Git.cs", report.ModerateRiskFiles[0].Path);
        Assert.Equal(DepartureRiskLevel.Moderate, report.ModerateRiskFiles[0].RiskLevel);
        Assert.Equal(0.60, report.ModerateRiskFiles[0].OwnershipShare);
        Assert.Equal(2, report.ModerateRiskFiles[0].SecondaryContributors.Count);
    }

    [Fact]
    public void TestDominantFiles_ShareUnder50Percent_IsNotAtRisk()
    {
        var result = new AnalysisResult
        {
            Files = new List<FileMetric>
            {
                new()
                {
                    Path = "src/Shared/Utils.cs",
                    Area = "Shared",
                    Touches = 30,
                    Lines = 200,
                    Contributors = new List<ContributorShare>
                    {
                        new() { Name = "Alice", Email = "alice@example.com", Activity = 18, ActivityShare = 0.60 },
                        new() { Name = "Bob", Email = "bob@example.com", Activity = 12, ActivityShare = 0.40 }
                    }
                }
            },
            Contributors = new List<ContributorMetric>
            {
                new() { Name = "Alice", Email = "alice@example.com", TotalActivity = 18 },
                new() { Name = "Bob", Email = "bob@example.com", TotalActivity = 12 }
            }
        };

        var analyzer = new DepartureRiskAnalyzer();
        var report = analyzer.AnalyzeDepartureRisk(result, "Bob");

        Assert.Empty(report.CriticalRiskFiles);
        Assert.Empty(report.ModerateRiskFiles);
        Assert.Equal(0, report.TotalAffectedFiles);
    }

    [Fact]
    public void TestDominantFiles_TargetHasNoActivity_IsNotAtRisk()
    {
        var result = new AnalysisResult
        {
            Files = new List<FileMetric>
            {
                new()
                {
                    Path = "src/Other.cs",
                    Area = "Other",
                    Touches = 10,
                    Lines = 100,
                    Contributors = new List<ContributorShare>
                    {
                        new() { Name = "Alice", Email = "alice@example.com", Activity = 10, ActivityShare = 1.0 }
                    }
                }
            }
        };

        var analyzer = new DepartureRiskAnalyzer();
        var report = analyzer.AnalyzeDepartureRisk(result, "Bob");

        Assert.Empty(report.CriticalRiskFiles);
        Assert.Empty(report.ModerateRiskFiles);
        Assert.Equal(0, report.TotalAffectedFiles);
    }

    [Fact]
    public void TestDominantFiles_MultipleFiles_CategorizesCorrectlyAndAggregatesTotals()
    {
        var result = new AnalysisResult
        {
            Files = new List<FileMetric>
            {
                new()
                {
                    Path = "src/Git/Gitgraph.cs",
                    Area = "Git",
                    Touches = 15,
                    Lines = 500,
                    Contributors = new List<ContributorShare>
                    {
                        new() { Name = "Bob", Email = "bob@example.com", Activity = 15, ActivityShare = 1.0 }
                    }
                },
                new()
                {
                    Path = "src/Git/GitPatchParser.cs",
                    Area = "Git",
                    Touches = 12,
                    Lines = 300,
                    Contributors = new List<ContributorShare>
                    {
                        new() { Name = "Bob", Email = "bob@example.com", Activity = 10, ActivityShare = 0.83 },
                        new() { Name = "Alice", Email = "alice@example.com", Activity = 2, ActivityShare = 0.17 }
                    }
                },
                new()
                {
                    Path = "src/Core/Identity.cs",
                    Area = "Core",
                    Touches = 9,
                    Lines = 200,
                    Contributors = new List<ContributorShare>
                    {
                        new() { Name = "Bob", Email = "bob@example.com", Activity = 6, ActivityShare = 0.67 },
                        new() { Name = "Alice", Email = "alice@example.com", Activity = 3, ActivityShare = 0.33 }
                    }
                },
                new()
                {
                    Path = "src/Cli/Cli.cs",
                    Area = "Cli",
                    Touches = 10,
                    Lines = 150,
                    Contributors = new List<ContributorShare>
                    {
                        new() { Name = "Alice", Email = "alice@example.com", Activity = 6, ActivityShare = 0.60 },
                        new() { Name = "Bob", Email = "bob@example.com", Activity = 4, ActivityShare = 0.40 }
                    }
                }
            },
            Contributors = new List<ContributorMetric>
            {
                new() { Name = "Bob", Email = "bob@example.com", TotalActivity = 35 },
                new() { Name = "Alice", Email = "alice@example.com", TotalActivity = 11 }
            }
        };

        var analyzer = new DepartureRiskAnalyzer();
        var report = analyzer.AnalyzeDepartureRisk(result, "Bob");

        Assert.Equal(2, report.CriticalRiskFiles.Count);
        Assert.Single(report.ModerateRiskFiles);
        Assert.Equal(3, report.TotalAffectedFiles);
        Assert.Equal(2, report.TotalAffectedAreas); // Git and Core
        Assert.Equal(1000, report.TotalAffectedLines); // 500 + 300 + 200
        Assert.Equal(3, report.TransferPlan.Count);
        Assert.True(report.OverallRiskScore > 0);
    }

    [Fact]
    public void TestDominantFiles_TargetMatching_MatchesByNameAndEmailCaseInsensitively()
    {
        var result = new AnalysisResult
        {
            Files = new List<FileMetric>
            {
                new()
                {
                    Path = "src/Git/Gitgraph.cs",
                    Area = "Git",
                    Touches = 10,
                    Lines = 250,
                    Contributors = new List<ContributorShare>
                    {
                        new() { Name = "Bob Smith", Email = "bob.smith@example.com", Activity = 10, ActivityShare = 1.0 }
                    }
                }
            },
            Contributors = new List<ContributorMetric>
            {
                new() { Name = "Bob Smith", Email = "bob.smith@example.com", TotalActivity = 10 }
            }
        };

        var analyzer = new DepartureRiskAnalyzer();

        var reportByEmail = analyzer.AnalyzeDepartureRisk(result, "bob.smith@example.com");
        Assert.Single(reportByEmail.CriticalRiskFiles);
        Assert.Equal("Bob Smith", reportByEmail.TargetDeveloper);

        var reportByName = analyzer.AnalyzeDepartureRisk(result, "bob smith");
        Assert.Single(reportByName.CriticalRiskFiles);

        var reportByUpper = analyzer.AnalyzeDepartureRisk(result, "BOB.SMITH@EXAMPLE.COM");
        Assert.Single(reportByUpper.CriticalRiskFiles);
    }

    [Fact]
    public void TestDominantFiles_NullOrWhitespaceTarget_ThrowsArgumentException()
    {
        var analyzer = new DepartureRiskAnalyzer();
        var result = new AnalysisResult();

        Assert.Throws<ArgumentException>(() => analyzer.AnalyzeDepartureRisk(result, ""));
        Assert.Throws<ArgumentException>(() => analyzer.AnalyzeDepartureRisk(result, "   "));
    }
}

public class KnowledgeTransferCandidateRankingTests
{
    [Fact]
    public void TestCandidateRanking_SecondaryOnFile_HasLowestContextDistance()
    {
        var file = new FileMetric
        {
            Path = "src/Git/Git.cs",
            Area = "Git",
            Touches = 20,
            Lines = 300,
            Contributors = new List<ContributorShare>
            {
                new() { Name = "Bob", Email = "bob@example.com", Activity = 16, ActivityShare = 0.80 },
                new() { Name = "Alice", Email = "alice@example.com", Activity = 4, ActivityShare = 0.20 }
            }
        };

        var result = new AnalysisResult
        {
            Files = new List<FileMetric> { file },
            Contributors = new List<ContributorMetric>
            {
                new()
                {
                    Name = "Alice",
                    Email = "alice@example.com",
                    TotalActivity = 50,
                    Areas = new List<ContributorAreaMetric>
                    {
                        new() { Area = "Git", Activity = 20, ActivityShare = 0.4, FamiliarityScore = 40 }
                    }
                }
            }
        };

        var analyzer = new DepartureRiskAnalyzer();
        var candidates = analyzer.RankCandidates(file, result, "Bob");
        var best = analyzer.SelectBestCandidate(file, result, "Bob");

        Assert.NotEmpty(candidates);
        Assert.Equal("Alice", best.CandidateName);
        Assert.True(best.ContextDistance < 0.6); // Direct file familiarity
        Assert.Contains("file familiarity", best.Reason);
    }

    [Fact]
    public void TestCandidateRanking_MultipleSecondariesOnFile_RanksHighestShareFirst()
    {
        var file = new FileMetric
        {
            Path = "src/Git/Git.cs",
            Area = "Git",
            Touches = 30,
            Lines = 500,
            Contributors = new List<ContributorShare>
            {
                new() { Name = "Bob", Email = "bob@example.com", Activity = 18, ActivityShare = 0.60 },
                new() { Name = "Carol", Email = "carol@example.com", Activity = 4, ActivityShare = 0.13 },
                new() { Name = "Alice", Email = "alice@example.com", Activity = 8, ActivityShare = 0.27 }
            }
        };

        var result = new AnalysisResult
        {
            Files = new List<FileMetric> { file },
            Contributors = new List<ContributorMetric>
            {
                new() { Name = "Alice", Email = "alice@example.com", TotalActivity = 30 },
                new() { Name = "Carol", Email = "carol@example.com", TotalActivity = 20 }
            }
        };

        var analyzer = new DepartureRiskAnalyzer();
        var ranked = analyzer.RankCandidates(file, result, "Bob");

        Assert.Equal(2, ranked.Count);
        Assert.Equal("Alice", ranked[0].CandidateName); // 27% > 13%
        Assert.Equal("Carol", ranked[1].CandidateName);
        Assert.True(ranked[0].ContextDistance < ranked[1].ContextDistance);
    }

    [Fact]
    public void TestCandidateRanking_SoleOwner_SelectsBestCandidateFromSameArea()
    {
        var file = new FileMetric
        {
            Path = "src/Core/Identity.cs",
            Area = "Core",
            Touches = 15,
            Lines = 350,
            Contributors = new List<ContributorShare>
            {
                new() { Name = "Bob", Email = "bob@example.com", Activity = 15, ActivityShare = 1.0 }
            }
        };

        var result = new AnalysisResult
        {
            Files = new List<FileMetric> { file },
            Contributors = new List<ContributorMetric>
            {
                new()
                {
                    Name = "Carol",
                    Email = "carol@example.com",
                    TotalActivity = 20,
                    Areas = new List<ContributorAreaMetric>
                    {
                        new() { Area = "Core", Activity = 5, ActivityShare = 0.10, FamiliarityScore = 12.0 }
                    }
                },
                new()
                {
                    Name = "Alice",
                    Email = "alice@example.com",
                    TotalActivity = 40,
                    Areas = new List<ContributorAreaMetric>
                    {
                        new() { Area = "Core", Activity = 15, ActivityShare = 0.30, FamiliarityScore = 35.0 }
                    }
                }
            }
        };

        var analyzer = new DepartureRiskAnalyzer();
        var best = analyzer.SelectBestCandidate(file, result, "Bob");
        var ranked = analyzer.RankCandidates(file, result, "Bob");

        Assert.Equal("Alice", best.CandidateName);
        Assert.True(best.ContextDistance >= 1.0 && best.ContextDistance <= 1.5);
        Assert.Contains("area familiarity", best.Reason);
        Assert.Equal("Alice", ranked[0].CandidateName);
        Assert.Equal("Carol", ranked[1].CandidateName);
    }

    [Fact]
    public void TestCandidateRanking_DirectFileBeatsAreaFamiliarity()
    {
        var file = new FileMetric
        {
            Path = "src/Core/Engine.cs",
            Area = "Core",
            Touches = 20,
            Lines = 400,
            Contributors = new List<ContributorShare>
            {
                new() { Name = "Bob", Email = "bob@example.com", Activity = 14, ActivityShare = 0.70 },
                new() { Name = "Dave", Email = "dave@example.com", Activity = 6, ActivityShare = 0.30 }
            }
        };

        var result = new AnalysisResult
        {
            Files = new List<FileMetric> { file },
            Contributors = new List<ContributorMetric>
            {
                new()
                {
                    Name = "Alice",
                    Email = "alice@example.com",
                    TotalActivity = 100,
                    Areas = new List<ContributorAreaMetric>
                    {
                        new() { Area = "Core", Activity = 80, ActivityShare = 0.80, FamiliarityScore = 90.0 }
                    }
                },
                new()
                {
                    Name = "Dave",
                    Email = "dave@example.com",
                    TotalActivity = 10
                }
            }
        };

        var analyzer = new DepartureRiskAnalyzer();
        var best = analyzer.SelectBestCandidate(file, result, "Bob");

        // Dave has on-file direct experience, so lower context distance than Alice who only has area experience
        Assert.Equal("dave@example.com", best.CandidateEmail);
        Assert.True(best.ContextDistance < 1.0);
    }

    [Fact]
    public void TestCandidateRanking_NoAreaFamiliarity_FallsBackToGeneralRepoActivity()
    {
        var file = new FileMetric
        {
            Path = "src/NewArea/Widget.cs",
            Area = "NewArea",
            Touches = 10,
            Lines = 150,
            Contributors = new List<ContributorShare>
            {
                new() { Name = "Bob", Email = "bob@example.com", Activity = 10, ActivityShare = 1.0 }
            }
        };

        var result = new AnalysisResult
        {
            Files = new List<FileMetric> { file },
            Contributors = new List<ContributorMetric>
            {
                new()
                {
                    Name = "Carol",
                    Email = "carol@example.com",
                    TotalActivity = 50,
                    Areas = new List<ContributorAreaMetric>
                    {
                        new() { Area = "OtherArea", Activity = 50, ActivityShare = 1.0, FamiliarityScore = 80.0 }
                    }
                },
                new()
                {
                    Name = "Alice",
                    Email = "alice@example.com",
                    TotalActivity = 200,
                    Areas = new List<ContributorAreaMetric>
                    {
                        new() { Area = "OtherArea", Activity = 150, ActivityShare = 1.0, FamiliarityScore = 90.0 }
                    }
                }
            }
        };

        var analyzer = new DepartureRiskAnalyzer();
        var best = analyzer.SelectBestCandidate(file, result, "Bob");

        Assert.Equal("Alice", best.CandidateName); // Alice has higher total activity (200 > 50)
        Assert.True(best.ContextDistance >= 2.0);
        Assert.Contains("Codebase familiarity", best.Reason);
    }

    [Fact]
    public void TestCandidateRanking_NoOtherContributorsInRepo_ReturnsUnassigned()
    {
        var file = new FileMetric
        {
            Path = "src/Solo.cs",
            Area = "Solo",
            Touches = 10,
            Lines = 100,
            Contributors = new List<ContributorShare>
            {
                new() { Name = "Bob", Email = "bob@example.com", Activity = 10, ActivityShare = 1.0 }
            }
        };

        var result = new AnalysisResult
        {
            Files = new List<FileMetric> { file },
            Contributors = new List<ContributorMetric>
            {
                new() { Name = "Bob", Email = "bob@example.com", TotalActivity = 10 }
            }
        };

        var analyzer = new DepartureRiskAnalyzer();
        var best = analyzer.SelectBestCandidate(file, result, "Bob");

        Assert.Equal("Unassigned", best.CandidateName);
        Assert.Equal(3.0, best.ContextDistance);
    }

    [Fact]
    public void TestTransferComplexity_HighLinesTouchesCoupling_CalculatesHighComplexity()
    {
        var file = new FileMetric
        {
            Path = "src/Core/GodFile.cs",
            Touches = 35,
            Lines = 1200
        };

        var result = new AnalysisResult
        {
            TemporalCoupling = new List<TemporalCoupling>
            {
                new() { FileA = "src/Core/GodFile.cs", FileB = "src/Core/Pipeline.cs", SharedCommits = 20, CouplingDegree = 0.85 }
            }
        };

        var (level, effort) = DepartureRiskAnalyzer.CalculateTransferComplexity(file, result);

        Assert.Equal("High", level);
        Assert.Equal("1-2 sprints", effort);
    }

    [Fact]
    public void TestTransferComplexity_LowLinesTouchesCoupling_CalculatesLowComplexity()
    {
        var file = new FileMetric
        {
            Path = "src/Core/SmallUtil.cs",
            Touches = 2,
            Lines = 45
        };

        var result = new AnalysisResult();

        var (level, effort) = DepartureRiskAnalyzer.CalculateTransferComplexity(file, result);

        Assert.Equal("Low", level);
        Assert.Equal("1-2 days", effort);
    }
}

public class DepartureRiskCliExecutionTests
{
    private class MockConsoleReporter : IConsoleReporter
    {
        public List<string> Messages { get; } = new();
        public List<string> ErrorMessages { get; } = new();

        public void Write(string message) => Messages.Add(message);
        public void WriteLine(string message = "") => Messages.Add(message + "\n");
        public void WriteError(string message) => ErrorMessages.Add(message);
        public void WriteErrorLine(string message) => ErrorMessages.Add(message + "\n");
        public void WriteWarning(string message) => Messages.Add("Warning: " + message + "\n");

        public string Output => string.Join("", Messages);
        public string Error => string.Join("", ErrorMessages);
    }

    private class FakeRepositoryAnalyzerForDepartureRisk : IRepositoryAnalyzer
    {
        public Task<AnalysisResult> AnalyzeAsync(AnalyzeInput input, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AnalysisResult
            {
                Analysis = new AnalysisMetadata
                {
                    RepoRoot = input.RepoRoot,
                    CommitCount = 100,
                    GeneratedAt = DateTime.UtcNow.ToString("o")
                },
                Files = new List<FileMetric>
                {
                    new()
                    {
                        Path = "src/Git/Gitgraph.cs",
                        Area = "Git",
                        Touches = 15,
                        Lines = 450,
                        Contributors = new List<ContributorShare>
                        {
                            new() { Name = "Bob", Email = "bob@example.com", Activity = 15, ActivityShare = 1.0 }
                        }
                    },
                    new()
                    {
                        Path = "src/Git/Git.cs",
                        Area = "Git",
                        Touches = 20,
                        Lines = 600,
                        Contributors = new List<ContributorShare>
                        {
                            new() { Name = "Bob", Email = "bob@example.com", Activity = 12, ActivityShare = 0.60 },
                            new() { Name = "Alice", Email = "alice@example.com", Activity = 8, ActivityShare = 0.40 }
                        }
                    }
                },
                Contributors = new List<ContributorMetric>
                {
                    new()
                    {
                        Name = "Bob",
                        Email = "bob@example.com",
                        TotalActivity = 27,
                        Areas = new List<ContributorAreaMetric>
                        {
                            new() { Area = "Git", Activity = 27, ActivityShare = 0.77, FamiliarityScore = 80 }
                        }
                    },
                    new()
                    {
                        Name = "Alice",
                        Email = "alice@example.com",
                        TotalActivity = 8,
                        Areas = new List<ContributorAreaMetric>
                        {
                            new() { Area = "Git", Activity = 8, ActivityShare = 0.23, FamiliarityScore = 20 }
                        }
                    }
                }
            });
        }
    }

    [Fact]
    public async Task TestCliExecution_TerminalTableOutput_RendersStructuredSections()
    {
        var parsed = new ParsedArgs
        {
            Command = "departure-risk",
            DepartureRiskDeveloper = "Bob",
            ContributorName = "Bob",
            RepoPath = "."
        };

        var command = new DepartureRiskCommand(parsed, analyzer: new FakeRepositoryAnalyzerForDepartureRisk());
        var reporter = new MockConsoleReporter();

        var result = await command.ExecuteAsync(reporter);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Departure Risk Analysis: Bob", reporter.Output);
        Assert.Contains("CRITICAL RISK", reporter.Output);
        Assert.Contains("src/Git/Gitgraph.cs", reporter.Output);
        Assert.Contains("MODERATE RISK", reporter.Output);
        Assert.Contains("src/Git/Git.cs", reporter.Output);
        Assert.Contains("TOTAL AFFECTED: 1 areas, 2 files", reporter.Output);
        Assert.Contains("RECOMMENDED KNOWLEDGE TRANSFER PLAN", reporter.Output);
        Assert.Contains("ESTIMATED ONBOARDING TIME", reporter.Output);
    }

    [Fact]
    public async Task TestCliExecution_JsonOutput_OutputsValidJson()
    {
        var parsed = new ParsedArgs
        {
            Command = "departure-risk",
            DepartureRiskDeveloper = "Bob",
            ContributorName = "Bob",
            RepoPath = ".",
            Settings = new AnalysisSettings { Json = true, Format = "json" }
        };

        var command = new DepartureRiskCommand(parsed, analyzer: new FakeRepositoryAnalyzerForDepartureRisk());
        var reporter = new MockConsoleReporter();

        var result = await command.ExecuteAsync(reporter);

        Assert.Equal(0, result.ExitCode);

        var report = JsonSerializer.Deserialize<DepartureRiskReport>(reporter.Output, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(report);
        Assert.Equal("Bob", report.TargetDeveloper);
        Assert.Single(report.CriticalRiskFiles);
        Assert.Single(report.ModerateRiskFiles);
        Assert.Equal(2, report.TotalAffectedFiles);
        Assert.Equal(2, report.TransferPlan.Count);
    }

    [Fact]
    public async Task TestCliExecution_MissingDeveloper_ReturnsExitCode2AndError()
    {
        var parsed = new ParsedArgs
        {
            Command = "departure-risk",
            RepoPath = "."
        };

        var command = new DepartureRiskCommand(parsed, analyzer: new FakeRepositoryAnalyzerForDepartureRisk());
        var reporter = new MockConsoleReporter();

        var result = await command.ExecuteAsync(reporter);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Departure risk analysis requires a target developer", reporter.Error);
    }

    [Fact]
    public void TestCliExecution_CommandLineParser_ParsesDepartureRiskSubcommand()
    {
        var parser = new CommandLineParser(new[] { "departure-risk", "--developer", "Alice" });
        var parsed = parser.Parse();

        Assert.Equal("departure-risk", parsed.Command);
        Assert.Equal("Alice", parsed.DepartureRiskDeveloper);
    }

    [Fact]
    public void TestCliExecution_CommandLineParser_ParsesShortDeveloperFlag()
    {
        var parser = new CommandLineParser(new[] { "departure-risk", "-d", "Carol", "--json" });
        var parsed = parser.Parse();

        Assert.Equal("departure-risk", parsed.Command);
        Assert.Equal("Carol", parsed.DepartureRiskDeveloper);
        Assert.True(parsed.Settings.Json);
    }

    [Fact]
    public void TestCliExecution_CliCommandFactory_CreatesDepartureRiskCommand()
    {
        var parsed = new ParsedArgs
        {
            Command = "departure-risk",
            DepartureRiskDeveloper = "Bob"
        };

        var factory = new CliCommandFactoryImpl();
        var command = factory.CreateCommand(parsed);

        Assert.IsType<DepartureRiskCommand>(command);
    }

    [Fact]
    public async Task TestCliExecution_StringArgsConstructor_ExecutesSuccessfully()
    {
        var command = new DepartureRiskCommand(
            new[] { "departure-risk", "--developer", "Bob" },
            analyzer: new FakeRepositoryAnalyzerForDepartureRisk());

        var reporter = new MockConsoleReporter();
        var result = await command.ExecuteAsync(reporter);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Departure Risk Analysis: Bob", reporter.Output);
    }
}
