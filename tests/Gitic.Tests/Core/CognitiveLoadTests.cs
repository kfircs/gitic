using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Gitic.Tests;

public class CognitiveLoadSequenceSwitchingTests
{
    private static GitCommitRecord CreateCommit(string author, string file, long timestamp = 0, string date = "")
    {
        return new GitCommitRecord
        {
            Author = new GitIdentity { Name = author, Email = $"{author.ToLowerInvariant()}@example.com" },
            Timestamp = timestamp,
            Date = date,
            Files = new List<GitFileChange>
            {
                new() { Path = file, Added = 10, Deleted = 2 }
            }
        };
    }

    [Fact]
    public void TestSequenceSwitching_SingleAreaCommits_ZeroSwitches()
    {
        var commits = new List<GitCommitRecord>
        {
            CreateCommit("Alice", "src/Core/Foo.cs", 1000),
            CreateCommit("Alice", "src/Core/Bar.cs", 2000),
            CreateCommit("Alice", "src/Core/Baz.cs", 3000),
            CreateCommit("Alice", "src/Core/Qux.cs", 4000)
        };

        var calculator = new CognitiveLoadCalculator();
        var profiles = calculator.CalculateProfiles(commits);

        Assert.Single(profiles);
        var profile = profiles[0];

        Assert.Equal("Alice", profile.Developer);
        Assert.Equal(0, profile.TotalSwitches);
        Assert.Equal(0.0, profile.EstimatedSwitchesPerWeek);
        Assert.Equal(0.0, profile.EstimatedOverheadHoursPerWeek);
        Assert.Equal(1, profile.AreaBreadth);
        Assert.Equal(4, profile.FileBreadth);
        Assert.Equal(4, profile.TotalCommits);
    }

    [Fact]
    public void TestSequenceSwitching_AlternatingAreas_MeasuresCorrectSwitchCount()
    {
        // Core -> Cli -> Core -> Cli -> Git
        // Transitions:
        // Core -> Cli (1)
        // Cli -> Core (2)
        // Core -> Cli (3)
        // Cli -> Git (4)
        var commits = new List<GitCommitRecord>
        {
            CreateCommit("Alice", "src/Core/Foo.cs", 1000),
            CreateCommit("Alice", "src/Cli/Bar.cs", 2000),
            CreateCommit("Alice", "src/Core/Baz.cs", 3000),
            CreateCommit("Alice", "src/Cli/Qux.cs", 4000),
            CreateCommit("Alice", "src/Git/Quux.cs", 5000)
        };

        var calculator = new CognitiveLoadCalculator();
        var profiles = calculator.CalculateProfiles(commits);

        Assert.Single(profiles);
        var profile = profiles[0];

        Assert.Equal(4, profile.TotalSwitches);
        Assert.Equal(3, profile.AreaBreadth);
        Assert.Equal(5, profile.FileBreadth);
    }

    [Fact]
    public void TestSequenceSwitching_ConsecutiveSameArea_DoesNotIncrementSwitches()
    {
        // Core -> Core -> Core -> Cli -> Cli -> Git -> Git
        // Transitions:
        // Core -> Core (0)
        // Core -> Core (0)
        // Core -> Cli (1)
        // Cli -> Cli (0)
        // Cli -> Git (2)
        // Git -> Git (0)
        var commits = new List<GitCommitRecord>
        {
            CreateCommit("Bob", "src/Core/A.cs", 1000),
            CreateCommit("Bob", "src/Core/B.cs", 2000),
            CreateCommit("Bob", "src/Core/C.cs", 3000),
            CreateCommit("Bob", "src/Cli/D.cs", 4000),
            CreateCommit("Bob", "src/Cli/E.cs", 5000),
            CreateCommit("Bob", "src/Git/F.cs", 6000),
            CreateCommit("Bob", "src/Git/G.cs", 7000)
        };

        var calculator = new CognitiveLoadCalculator();
        var profiles = calculator.CalculateProfiles(commits);

        Assert.Single(profiles);
        var profile = profiles[0];

        Assert.Equal(2, profile.TotalSwitches);
        Assert.Equal(3, profile.AreaBreadth);
        Assert.Equal(7, profile.FileBreadth);
    }

    [Fact]
    public void TestSequenceSwitching_EstimatedSwitchingOverhead_CalculatedAt15MinPerSwitch()
    {
        // 8 switches over 7 days (1 week)
        // 8 switches * 0.25 hours = 2.0 hours overhead
        long startMs = 1700000000000L;
        long dayMs = 86400000L;

        var commits = new List<GitCommitRecord>();
        for (int i = 0; i < 9; i++)
        {
            string area = (i % 2 == 0) ? "src/Core" : "src/Cli";
            commits.Add(CreateCommit("Carol", $"{area}/File{i}.cs", startMs + (i * dayMs * 7 / 9)));
        }

        var calculator = new CognitiveLoadCalculator();
        var profiles = calculator.CalculateProfiles(commits);

        Assert.Single(profiles);
        var profile = profiles[0];

        Assert.Equal(8, profile.TotalSwitches);
        Assert.Equal(8.0, profile.EstimatedSwitchesPerWeek);
        Assert.Equal(2.0, profile.EstimatedOverheadHoursPerWeek);
    }

    [Fact]
    public void TestSequenceSwitching_NormalizedOverMultiWeekSpan()
    {
        // 14 switches over 14 days (2 weeks)
        // switches per week = 14 / 2 = 7.0
        // overhead per week = 7.0 * 0.25 = 1.75 hours
        long startMs = 1700000000000L;
        long dayMs = 86400000L;

        var commits = new List<GitCommitRecord>();
        for (int i = 0; i < 15; i++)
        {
            string area = (i % 2 == 0) ? "src/Core" : "src/Cli";
            commits.Add(CreateCommit("Dave", $"{area}/File{i}.cs", startMs + (i * dayMs)));
        }

        var calculator = new CognitiveLoadCalculator();
        var profiles = calculator.CalculateProfiles(commits);

        Assert.Single(profiles);
        var profile = profiles[0];

        Assert.Equal(14, profile.TotalSwitches);
        Assert.Equal(7.0, profile.EstimatedSwitchesPerWeek);
        Assert.Equal(1.75, profile.EstimatedOverheadHoursPerWeek);
    }

    [Fact]
    public void TestSequenceSwitching_MultiDeveloperSequences_CalculatedIndependently()
    {
        // Alice: Core -> Cli -> Core -> Cli (3 switches)
        // Bob: Git -> Git -> Git (0 switches)
        var commits = new List<GitCommitRecord>
        {
            CreateCommit("Alice", "src/Core/1.cs", 1000),
            CreateCommit("Bob", "src/Git/1.cs", 1500),
            CreateCommit("Alice", "src/Cli/2.cs", 2000),
            CreateCommit("Bob", "src/Git/2.cs", 2500),
            CreateCommit("Alice", "src/Core/3.cs", 3000),
            CreateCommit("Bob", "src/Git/3.cs", 3500),
            CreateCommit("Alice", "src/Cli/4.cs", 4000)
        };

        var calculator = new CognitiveLoadCalculator();
        var profiles = calculator.CalculateProfiles(commits);

        Assert.Equal(2, profiles.Count);

        var alice = profiles.First(p => p.Developer == "Alice");
        var bob = profiles.First(p => p.Developer == "Bob");

        Assert.Equal(3, alice.TotalSwitches);
        Assert.Equal(2, alice.AreaBreadth);

        Assert.Equal(0, bob.TotalSwitches);
        Assert.Equal(1, bob.AreaBreadth);
        Assert.Equal("src/Git", bob.PrimaryArea);
    }

    [Fact]
    public void TestSequenceSwitching_EmptyOrSingleCommit_ZeroSwitches()
    {
        var calculator = new CognitiveLoadCalculator();

        var emptyProfiles = calculator.CalculateProfiles(new List<GitCommitRecord>());
        Assert.Empty(emptyProfiles);

        var singleCommit = new List<GitCommitRecord>
        {
            CreateCommit("SingleDev", "src/Core/Solo.cs", 1000)
        };
        var singleProfiles = calculator.CalculateProfiles(singleCommit);

        Assert.Single(singleProfiles);
        Assert.Equal(0, singleProfiles[0].TotalSwitches);
        Assert.Equal(0.0, singleProfiles[0].EstimatedSwitchesPerWeek);
        Assert.Equal(0.0, singleProfiles[0].EstimatedOverheadHoursPerWeek);
    }
}

public class CognitiveLoadEntropySpecializationTests
{
    private static GitCommitRecord CreateCommit(string author, string file)
    {
        return new GitCommitRecord
        {
            Author = new GitIdentity { Name = author, Email = $"{author.ToLowerInvariant()}@example.com" },
            Timestamp = 1000,
            Files = new List<GitFileChange>
            {
                new() { Path = file, Added = 10, Deleted = 2 }
            }
        };
    }

    [Fact]
    public void TestEntropy_PureSpecialist_ZeroEntropy_MaxSpecialization()
    {
        // Specialist working entirely within src/Core
        var commits = new List<GitCommitRecord>();
        for (int i = 0; i < 20; i++)
        {
            commits.Add(CreateCommit("SpecialistDev", $"src/Core/File{i}.cs"));
        }

        var areas = new List<AreaMetric>
        {
            new() { Area = "src/Core" },
            new() { Area = "src/Cli" },
            new() { Area = "src/Git" },
            new() { Area = "src/Reporting" }
        };

        var profiles = CognitiveLoadCalculator.Calculate(commits, areas);

        Assert.Single(profiles);
        var p = profiles[0];

        Assert.Equal(1, p.AreaBreadth);
        Assert.Equal(20, p.FileBreadth);
        Assert.Equal(0.0, p.AreaEntropy);
        Assert.Equal(1.0, p.SpecializationIndex);
        Assert.Equal(CognitiveLoadClassification.Specialist, p.LoadClassification);
        Assert.True(p.IsSpecialist);
    }

    [Fact]
    public void TestEntropy_UniformDistribution_MaxEntropy_ZeroSpecialization()
    {
        // Generalist working equally across 4 areas (5 files in each area)
        var areas = new List<AreaMetric>
        {
            new() { Area = "src/Core" },
            new() { Area = "src/Cli" },
            new() { Area = "src/Git" },
            new() { Area = "src/Reporting" }
        };

        var commits = new List<GitCommitRecord>();
        foreach (var area in areas)
        {
            for (int i = 0; i < 5; i++)
            {
                commits.Add(CreateCommit("GeneralistDev", $"{area.Area}/File{i}.cs"));
            }
        }

        var profiles = CognitiveLoadCalculator.Calculate(commits, areas);

        Assert.Single(profiles);
        var p = profiles[0];

        Assert.Equal(4, p.AreaBreadth);
        Assert.Equal(20, p.FileBreadth);
        // Shannon entropy of uniform 4-state distribution: log2(4) = 2.0
        Assert.Equal(2.0, p.AreaEntropy);
        // Specialization index should be 0.0 (1.0 - 2.0 / log2(4) = 0.0)
        Assert.Equal(0.0, p.SpecializationIndex);
    }

    [Fact]
    public void TestEntropy_SkewedDistribution_ModerateEntropy()
    {
        // 80% Core (16 files), 10% Cli (2 files), 10% Git (2 files)
        var areas = new List<AreaMetric>
        {
            new() { Area = "src/Core" },
            new() { Area = "src/Cli" },
            new() { Area = "src/Git" },
            new() { Area = "src/Reporting" }
        };

        var commits = new List<GitCommitRecord>();
        for (int i = 0; i < 16; i++) commits.Add(CreateCommit("FocusedDev", $"src/Core/File{i}.cs"));
        for (int i = 0; i < 2; i++) commits.Add(CreateCommit("FocusedDev", $"src/Cli/File{i}.cs"));
        for (int i = 0; i < 2; i++) commits.Add(CreateCommit("FocusedDev", $"src/Git/File{i}.cs"));

        var profiles = CognitiveLoadCalculator.Calculate(commits, areas);

        Assert.Single(profiles);
        var p = profiles[0];

        Assert.Equal(3, p.AreaBreadth);
        Assert.Equal("src/Core", p.PrimaryArea);
        // Entropy for 0.8, 0.1, 0.1 is ~0.92
        Assert.True(p.AreaEntropy > 0.5 && p.AreaEntropy < 1.2);
        // Specialization index is higher than generalist, lower than specialist
        Assert.True(p.SpecializationIndex > 0.4 && p.SpecializationIndex < 0.85);
    }

    [Fact]
    public void TestSpecializationIndex_DifferentiatesSpecialistFromScatteredGeneralist()
    {
        var areas = new List<AreaMetric>
        {
            new() { Area = "Area1" },
            new() { Area = "Area2" },
            new() { Area = "Area3" },
            new() { Area = "Area4" },
            new() { Area = "Area5" },
            new() { Area = "Area6" },
            new() { Area = "Area7" },
            new() { Area = "Area8" }
        };

        var commits = new List<GitCommitRecord>();

        // Specialist: 24 commits in Area1
        for (int i = 0; i < 24; i++)
        {
            commits.Add(CreateCommit("Specialist", $"Area1/File{i}.cs"));
        }

        // Scattered Generalist: 3 commits in each of 8 areas
        for (int a = 1; a <= 8; a++)
        {
            for (int i = 0; i < 3; i++)
            {
                commits.Add(CreateCommit("Generalist", $"Area{a}/File{i}.cs"));
            }
        }

        var profiles = CognitiveLoadCalculator.Calculate(commits, areas);

        var specialist = profiles.First(p => p.Developer == "Specialist");
        var generalist = profiles.First(p => p.Developer == "Generalist");

        Assert.True(specialist.SpecializationIndex >= 0.9, "Specialist should have index >= 0.9");
        Assert.True(generalist.SpecializationIndex <= 0.1, "Generalist should have index <= 0.1");
        Assert.True(generalist.AreaEntropy > specialist.AreaEntropy, "Generalist entropy must exceed specialist");
        Assert.True(generalist.AreaBreadth > specialist.AreaBreadth, "Generalist area breadth must exceed specialist");
    }

    [Fact]
    public void TestClassification_OverloadedVsFocusedVsSpecialist()
    {
        var (overloadedClass, _) = CognitiveLoadCalculator.ClassifyLoad(
            switchesPerWeek: 12.4,
            areaBreadth: 8,
            entropy: 2.8,
            specializationIndex: 0.23,
            primaryArea: "src/Core",
            developer: "Alice");

        Assert.Equal(CognitiveLoadClassification.Overloaded, overloadedClass);

        var (watchClass, _) = CognitiveLoadCalculator.ClassifyLoad(
            switchesPerWeek: 7.8,
            areaBreadth: 5,
            entropy: 1.8,
            specializationIndex: 0.45,
            primaryArea: "src/Cli",
            developer: "Carol");

        Assert.Equal(CognitiveLoadClassification.Watch, watchClass);

        var (focusedClass, _) = CognitiveLoadCalculator.ClassifyLoad(
            switchesPerWeek: 4.1,
            areaBreadth: 3,
            entropy: 1.2,
            specializationIndex: 0.65,
            primaryArea: "src/Core",
            developer: "Bob");

        Assert.Equal(CognitiveLoadClassification.Focused, focusedClass);

        var (specialistClass, _) = CognitiveLoadCalculator.ClassifyLoad(
            switchesPerWeek: 1.2,
            areaBreadth: 1,
            entropy: 0.0,
            specializationIndex: 1.0,
            primaryArea: "src/Git",
            developer: "Dave");

        Assert.Equal(CognitiveLoadClassification.Specialist, specialistClass);
    }
}

public class CognitiveLoadFormattingTests
{
    private static List<ContributorCognitiveProfile> SampleProfiles()
    {
        return new List<ContributorCognitiveProfile>
        {
            new()
            {
                Developer = "Alice",
                DeveloperEmail = "alice@example.com",
                AreaBreadth = 8,
                FileBreadth = 47,
                AreaEntropy = 2.80,
                EstimatedSwitchesPerWeek = 12.4,
                EstimatedOverheadHoursPerWeek = 3.1,
                SpecializationIndex = 0.23,
                LoadClassification = CognitiveLoadClassification.Overloaded,
                PrimaryArea = "src/Core",
                TotalCommits = 45,
                TotalSwitches = 25,
                Insight = "Alice carries high cognitive load across 8 areas."
            },
            new()
            {
                Developer = "Bob",
                DeveloperEmail = "bob@example.com",
                AreaBreadth = 3,
                FileBreadth = 15,
                AreaEntropy = 1.10,
                EstimatedSwitchesPerWeek = 4.1,
                EstimatedOverheadHoursPerWeek = 1.0,
                SpecializationIndex = 0.65,
                LoadClassification = CognitiveLoadClassification.Focused,
                PrimaryArea = "src/Git",
                TotalCommits = 20,
                TotalSwitches = 8,
                Insight = "Bob maintains healthy focus."
            },
            new()
            {
                Developer = "Dave",
                DeveloperEmail = "dave@example.com",
                AreaBreadth = 1,
                FileBreadth = 8,
                AreaEntropy = 0.0,
                EstimatedSwitchesPerWeek = 0.0,
                EstimatedOverheadHoursPerWeek = 0.0,
                SpecializationIndex = 1.0,
                LoadClassification = CognitiveLoadClassification.Specialist,
                PrimaryArea = "src/Core",
                TotalCommits = 14,
                TotalSwitches = 0,
                Insight = "Dave is a dedicated specialist."
            }
        };
    }

    [Fact]
    public void TestFormatting_SummaryTable_ContainsExpectedHeadersAndDeveloperRows()
    {
        var calculator = new CognitiveLoadCalculator();
        var profiles = SampleProfiles();

        string table = calculator.FormatSummaryTable(profiles);

        Assert.NotNull(table);
        Assert.Contains("Alice", table);
        Assert.Contains("Bob", table);
        Assert.Contains("Dave", table);
        Assert.Contains("Overloaded", table);
        Assert.Contains("Focused", table);
        Assert.Contains("Specialist", table);
        Assert.Contains("12.4", table);
        Assert.Contains("4.1", table);
    }

    [Fact]
    public void TestFormatting_Markdown_RendersValidMarkdownTableAndHeadings()
    {
        var calculator = new CognitiveLoadCalculator();
        var profiles = SampleProfiles();

        string markdown = calculator.FormatMarkdown(profiles);

        Assert.NotNull(markdown);
        Assert.Contains("## 🧠 Cognitive Load & Context-Switching Map", markdown);
        Assert.Contains("| Developer | Area Breadth | File Breadth | Area Entropy | Switches / Wk | Overhead (hrs/wk) | Specialization Index | Status |", markdown);
        Assert.Contains("| **Alice** |", markdown);
        Assert.Contains("| **Bob** |", markdown);
        Assert.Contains("| **Dave** |", markdown);
        Assert.Contains("⚠️ Overloaded", markdown);
        Assert.Contains("✅ Focused", markdown);
        Assert.Contains("✅ Specialist", markdown);
        Assert.Contains("### 📊 Contributor Cognitive Profiles & Insights", markdown);
    }

    [Fact]
    public void TestFormatting_EmptyProfiles_ReturnsGracefulFallback()
    {
        var calculator = new CognitiveLoadCalculator();
        var empty = new List<ContributorCognitiveProfile>();

        string table = calculator.FormatSummaryTable(empty);
        Assert.NotNull(table);
        Assert.Contains("No contributor cognitive profile data available", table);

        string md = calculator.FormatMarkdown(empty);
        Assert.NotNull(md);
        Assert.Contains("No cognitive load profiles available", md);
    }
}
