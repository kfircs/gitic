using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Gitic.Tests;

public class CodeOwnershipTests
{
    private sealed class AllowAllPathClassifier : IPathClassifier
    {
        public bool Check(string path) => true;
        public List<ExclusionSummary> GetExclusions() => new();
    }

    private static GitIdentity Identity(string name) =>
        new() { Name = name, Email = $"{name.ToLowerInvariant()}@example.com" };

    private static GitCommitRecord Commit(GitIdentity author, params GitFileChange[] files) =>
        new() { Author = author, Files = files.ToList() };

    private static GitFileChange Change(string path, int added, int deleted) =>
        new() { Path = path, Added = added, Deleted = deleted };

    private static ChangeAccumulator Accumulate(params GitCommitRecord[] commits)
    {
        var accumulator = new ChangeAccumulator(
            GiticConfig.Default, new AnalysisSettings { Depth = 1 }, new AllowAllPathClassifier(), new IdentityRegistry());
        accumulator.ProcessCommits(commits.ToList());
        return accumulator;
    }

    [Fact]
    public void Commit_TouchingSeveralFilesInOneArea_IsCountedOncePerScope()
    {
        var alice = Identity("Alice");
        var accumulator = Accumulate(
            Commit(alice, Change("src/a.cs", 10, 2), Change("src/b.cs", 6, 0)));

        var repoWide = accumulator.GetContributors().Values.Single();
        var inArea = accumulator.GetAreas()["src"].ContributorCredits.Values.Single();
        var inFile = accumulator.GetFiles()["src/a.cs"].ContributorCredits.Values.Single();

        Assert.Equal(1, repoWide.Commits);
        Assert.Equal(1, inArea.Commits);
        Assert.Equal(1, inFile.Commits);
        Assert.Equal(16, repoWide.Added);
        Assert.Equal(2, repoWide.Deleted);
    }

    [Fact]
    public void Commit_TouchingTwoAreas_CountsOncePerAreaAndOnceRepoWide()
    {
        var accumulator = Accumulate(
            Commit(Identity("Alice"), Change("src/a.cs", 1, 0), Change("tests/a.cs", 1, 0)));

        Assert.Equal(1, accumulator.GetContributors().Values.Single().Commits);
        Assert.Equal(1, accumulator.GetAreas()["src"].ContributorCredits.Values.Single().Commits);
        Assert.Equal(1, accumulator.GetAreas()["tests"].ContributorCredits.Values.Single().Commits);
    }

    [Fact]
    public void CoAuthoredCommit_CountsForBothAuthorsAndSplitsLinesEqually()
    {
        var commit = Commit(Identity("Alice"), Change("src/a.cs", 10, 4));
        commit.CoAuthors = new List<GitIdentity> { Identity("Bob") };
        var accumulator = Accumulate(commit);

        var contributors = accumulator.GetContributors().Values.ToDictionary(c => c.Identity.Name);

        Assert.Equal(1, contributors["Alice"].Commits);
        Assert.Equal(1, contributors["Bob"].Commits);
        Assert.Equal(5, contributors["Alice"].Added, precision: 6);
        Assert.Equal(5, contributors["Bob"].Added, precision: 6);
        Assert.Equal(2, contributors["Bob"].Deleted, precision: 6);
    }

    [Fact]
    public void RenderContributors_ComputesPercentageSharesForEveryMeasure()
    {
        var accumulator = Accumulate(
            Commit(Identity("Alice"), Change("src/a.cs", 30, 10)),
            Commit(Identity("Alice"), Change("src/a.cs", 30, 10)),
            Commit(Identity("Alice"), Change("src/a.cs", 30, 10)),
            Commit(Identity("Bob"), Change("src/b.cs", 10, 10)));

        var metrics = new MetricsEngine()
            .RenderContributors(accumulator.GetContributors().Values.ToList())
            .ToDictionary(c => c.Name);

        Assert.Equal(0.75, metrics["Alice"].CommitShare, precision: 4);
        Assert.Equal(0.25, metrics["Bob"].CommitShare, precision: 4);
        Assert.Equal(0.9, metrics["Alice"].AddedShare, precision: 4);
        Assert.Equal(0.75, metrics["Alice"].DeletedShare, precision: 4);
        Assert.Equal(0.75, metrics["Alice"].ActivityShare, precision: 4);
        Assert.Equal(90, metrics["Alice"].Added);
        Assert.Equal(3, metrics["Alice"].Commits);
    }

    [Fact]
    public void RenderContributors_WithNoLineChanges_ReportsZeroLineSharesInsteadOfDividingByZero()
    {
        var accumulator = Accumulate(Commit(Identity("Alice"), Change("src/a.cs", 0, 0)));

        var alice = new MetricsEngine().RenderContributors(accumulator.GetContributors().Values.ToList()).Single();

        Assert.Equal(0.0, alice.AddedShare);
        Assert.Equal(0.0, alice.DeletedShare);
        Assert.Equal(1.0, alice.CommitShare);
    }

    [Fact]
    public void CodeOwnersPerspective_ForRepositoryRoot_ListsOwnersWithPercentages()
    {
        var result = new AnalysisResult
        {
            Contributors = new List<ContributorMetric>
            {
                new() { Name = "Alice", Email = "alice@example.com", CommitShare = 0.75, AddedShare = 0.9, DeletedShare = 0.5, ActivityShare = 0.6 },
                new() { Name = "Bob", Email = "bob@example.com", CommitShare = 0.25, AddedShare = 0.1, DeletedShare = 0.5, ActivityShare = 0.4 }
            }
        };
        var root = new TuiNode { Name = "Root", RelativePath = "", IsDirectory = true };

        var text = string.Join("\n", new CodeOwnersPerspective().GetRightSidebarLines(root, 60, result));

        Assert.Contains("Repository-wide", text);
        Assert.Contains("Alice", text);
        Assert.Contains("75%", text);
        Assert.Contains("90%", text);
        Assert.True(text.IndexOf("Alice") < text.IndexOf("Bob"));
    }

    [Fact]
    public void CodeOwnersPerspective_ForDirectoryMatchingAnArea_UsesThatAreasOwners()
    {
        var result = new AnalysisResult
        {
            Areas = new List<AreaMetric>
            {
                new()
                {
                    Area = "src/Core",
                    Contributors = new List<ContributorShare> { new() { Name = "Carol", CommitShare = 1.0 } }
                },
                new()
                {
                    Area = "tests",
                    Contributors = new List<ContributorShare> { new() { Name = "Dave", CommitShare = 1.0 } }
                }
            }
        };
        var node = new TuiNode { Name = "Core", RelativePath = "src/Core", IsDirectory = true };

        var text = string.Join("\n", new CodeOwnersPerspective().GetRightSidebarLines(node, 60, result));

        Assert.Contains("Area: src/Core", text);
        Assert.Contains("Carol", text);
        Assert.DoesNotContain("Dave", text);
    }

    [Fact]
    public void CodeOwnersPerspective_ForDirectoryAboveSeveralAreas_CombinesTheirCounts()
    {
        var result = new AnalysisResult
        {
            Areas = new List<AreaMetric>
            {
                new()
                {
                    Area = "src/Core",
                    Contributors = new List<ContributorShare> { new() { Name = "Carol", Email = "c@x", Commits = 3, Added = 30 } }
                },
                new()
                {
                    Area = "src/Cli",
                    Contributors = new List<ContributorShare>
                    {
                        new() { Name = "Carol", Email = "c@x", Commits = 1, Added = 10 },
                        new() { Name = "Dave", Email = "d@x", Commits = 4, Added = 60 }
                    }
                }
            }
        };
        var node = new TuiNode { Name = "src", RelativePath = "src", IsDirectory = true };

        var text = string.Join("\n", new CodeOwnersPerspective().GetRightSidebarLines(node, 60, result));

        Assert.Contains("2 areas combined", text);
        Assert.True(text.IndexOf("Carol") < text.IndexOf("Dave"), "Both have 50% of commits, so ties sort by name");
        Assert.Contains("50%", text);
        Assert.Contains("60%", text);
    }
}
