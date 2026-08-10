using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Gitic.Tests
{
    public class ScoringRefactoredTests
    {
        [Fact]
        public void TestScoreFiles_WithEmptyItems_ReturnsEmptyList()
        {
            var config = new GiticConfig();
            var engine = new FamiliarityScoringEngine(config);
            var items = new List<ItemAccumulator>();

            var files = engine.ScoreFiles(items, 2);

            Assert.NotNull(files);
            Assert.Empty(files);
        }

        [Fact]
        public void TestScoreAreas_WithEmptyItems_ReturnsEmptyList()
        {
            var config = new GiticConfig();
            var engine = new FamiliarityScoringEngine(config);
            var items = new List<ItemAccumulator>();

            var areas = engine.ScoreAreas(items);

            Assert.NotNull(areas);
            Assert.Empty(areas);
        }

        [Fact]
        public void TestScoreFiles_AndScoreAreas_HaveConsistentCalculation()
        {
            var config = new GiticConfig();
            var engine = new FamiliarityScoringEngine(config);

            var items = new List<ItemAccumulator>
            {
                new ItemAccumulator
                {
                    Key = "src/Scoring.cs",
                    Touches = 10,
                    Added = 100,
                    Deleted = 50,
                    Churn = 150,
                    LastTouched = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Files = new HashSet<string> { "src/Scoring.cs" },
                    ContributorCredits = new Dictionary<string, ContributorCredit>
                    {
                        { "alice", new ContributorCredit { Identity = new GitIdentity { Name = "Alice", Email = "alice@example.com" }, Activity = 10.0 } }
                    }
                }
            };

            var files = engine.ScoreFiles(items, 2);
            var areas = engine.ScoreAreas(items);

            Assert.Single(files);
            Assert.Single(areas);

            var fileMetric = files[0];
            var areaMetric = areas[0];

            Assert.Equal("src/Scoring.cs", fileMetric.Path);
            Assert.Equal("src/Scoring.cs", areaMetric.Area);
            Assert.Equal(10, fileMetric.Touches);
            Assert.Equal(10, areaMetric.Touches);
            Assert.Equal(150, fileMetric.Churn);
            Assert.Equal(150, areaMetric.Churn);
            Assert.Equal(fileMetric.HeatScore, areaMetric.HeatScore);
            Assert.Equal(82, fileMetric.AttentionScore);
            Assert.Equal(67, areaMetric.AttentionScore);
            Assert.Equal(fileMetric.LastTouched, areaMetric.LastTouched);
            Assert.Equal(fileMetric.Contributors.Count, areaMetric.Contributors.Count);
            Assert.Equal(fileMetric.Contributors[0].Name, areaMetric.Contributors[0].Name);
        }

        private class MockSiloCalculator : IKnowledgeSiloCalculator
        {
            public bool Called { get; private set; }

            public KnowledgeSiloMetric CalculateKnowledgeSilo(
                List<ContributorShare> contributors,
                HashSet<string> activeContributorKeys)
            {
                Called = true;
                return new KnowledgeSiloMetric
                {
                    TruckFactor = 3,
                    TopOwnerShare = 0.85,
                    IsSilo = true,
                    Abandoned = false
                };
            }
        }

        private class MockScoringUtility : IScoringUtilityService
        {
            public double CalculateRecencyScore(long timestamp, DateTimeOffset? referenceDate = null) => 0.9;
            public double CalculateDebtVolatility(ItemAccumulator item, double maxChurn, double maxNetLines) => 42.0;
            public double CalculateCoordinationOverlap(List<ContributorShare> contributors, int itemTouches) => 88.0;
        }

        [Fact]
        public void TestScoringEngine_UsesDIAndInjectionsCorrectly()
        {
            var config = new GiticConfig();
            var mockSilo = new MockSiloCalculator();
            var mockUtil = new MockScoringUtility();

            var engine = new FamiliarityScoringEngine(
                config,
                activeContributorKeys: new HashSet<string> { "alice" },
                depth: 2,
                siloCalculator: mockSilo,
                scoringUtilityService: mockUtil
            );

            var items = new List<ItemAccumulator>
            {
                new ItemAccumulator
                {
                    Key = "src/Scoring.cs",
                    Touches = 5,
                    Added = 50,
                    Deleted = 10,
                    Churn = 60,
                    LastTouched = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    ContributorCredits = new Dictionary<string, ContributorCredit>
                    {
                        { "alice", new ContributorCredit { Identity = new GitIdentity { Name = "Alice", Email = "alice@example.com" }, Activity = 10.0 } }
                    }
                }
            };

            var files = engine.ScoreFiles(items, 2);

            Assert.True(mockSilo.Called);
            var file = Assert.Single(files);
            Assert.Equal(42.0, file.DebtVolatility);
            Assert.Equal(88.0, file.CoordinationOverlap);
            Assert.NotNull(file.KnowledgeSilo);
            Assert.Equal(3, file.KnowledgeSilo.TruckFactor);
            Assert.True(file.KnowledgeSilo.IsSilo);
        }
    }
}
