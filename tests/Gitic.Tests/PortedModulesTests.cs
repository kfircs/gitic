using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace Gitic.Tests
{
    public class PortedModulesTests
    {
        [Fact]
        public void TestMatchesPathPattern()
        {
            Assert.True(PathUtils.MatchesPathPattern("src/main.cs", "src/*.cs"));
            Assert.True(PathUtils.MatchesPathPattern("src/main.cs", "src/**"));
            Assert.False(PathUtils.MatchesPathPattern("src/main.js", "src/*.cs"));
            Assert.True(PathUtils.MatchesPathPattern("src/components/button.cs", "src/**/*.cs"));
        }

        [Fact]
        public void TestAreaForPath()
        {
            var namedAreas = new List<NamedArea>
            {
                new() { Name = "Frontend", Paths = new List<string> { "src/ui/**", "public/**" } },
                new() { Name = "Backend", Paths = new List<string> { "src/api/**" } }
            };

            Assert.Equal("Frontend", Exclusions.AreaForPath("src/ui/components/button.cs", 2, namedAreas));
            Assert.Equal("Backend", Exclusions.AreaForPath("src/api/controllers/user.cs", 2, namedAreas));
            Assert.Equal("src/db", Exclusions.AreaForPath("src/db/migrations/init.cs", 2, namedAreas));
            Assert.Equal("src", Exclusions.AreaForPath("src/db/migrations/init.cs", 1, namedAreas));
        }

        [Fact]
        public void TestPathClassifier()
        {
            var headFiles = new HashSet<string> { "src/main.cs", "package.json" };
            var excludes = new List<ExcludeRule>
            {
                new() { Pattern = "tests/**", Category = "test" }
            };

            var classifier = new PathClassifier(headFiles, excludes, includeDeleted: false, requestedPath: null);

            // Included file
            Assert.True(classifier.Check("src/main.cs"));

            // Excluded by default lockfile
            Assert.False(classifier.Check("package-lock.json"));
            
            // Excluded by custom exclude rule
            Assert.False(classifier.Check("tests/unit/test.cs"));

            // Excluded because missing from HEAD (includeDeleted is false)
            Assert.False(classifier.Check("src/deleted.cs"));

            var exclusions = classifier.GetExclusions();
            Assert.Contains(exclusions, e => e.Category == "lockfile" && e.Pattern == "lockfiles");
            Assert.Contains(exclusions, e => e.Category == "test" && e.Pattern == "tests/**");
            Assert.Contains(exclusions, e => e.Category == "deleted" && e.Pattern == "missing from HEAD");
        }

        [Fact]
        public void TestGitGraph()
        {
            var commit1 = new GitCommitRecord { Hash = "C1", Parents = new List<string>() };
            var commit2 = new GitCommitRecord { Hash = "C2", Parents = new List<string> { "C1" } };
            var commit3 = new GitCommitRecord { Hash = "C3", Parents = new List<string> { "C1" } };
            var commit4 = new GitCommitRecord { Hash = "C4", Parents = new List<string> { "C2", "C3" } };

            var commitMap = new Dictionary<string, GitCommitRecord>
            {
                { "C1", commit1 },
                { "C2", commit2 },
                { "C3", commit3 },
                { "C4", commit4 }
            };

            var ancestors = GitGraph.GetAncestors("C4", commitMap, 10);
            Assert.Contains("C4", ancestors);
            Assert.Contains("C2", ancestors);
            Assert.Contains("C3", ancestors);
            Assert.Contains("C1", ancestors);

            var branchCommits = GitGraph.GetBranchCommits("C3", new HashSet<string> { "C2", "C1" }, commitMap, 10);
            Assert.Single(branchCommits);
            Assert.Equal("C3", branchCommits[0].Hash);
        }

        [Fact]
        public void TestFileStatsIsBinary()
        {
            byte[] textData = Encoding.UTF8.GetBytes("Hello, world! This is a test.");
            byte[] binaryData = new byte[] { 72, 101, 108, 108, 111, 0, 119, 111, 114, 108, 100 }; // contains null byte

            Assert.False(FileStats.IsBinaryFile(textData));
            Assert.True(FileStats.IsBinaryFile(binaryData));
        }

        // --- ADDED PORTED MODULE TESTS ---

        [Fact]
        public void TestPathUtils_NormalizeGitPath()
        {
            Assert.Equal("src/main.cs", PathUtils.NormalizeGitPath("src\\main.cs"));
            Assert.Equal("src/main.cs", PathUtils.NormalizeGitPath("./src/main.cs"));
            Assert.Equal("src/main.cs", PathUtils.NormalizeGitPath("/src/main.cs"));
            Assert.Equal("src/main.cs", PathUtils.NormalizeGitPath("src/main.cs"));
        }

        [Fact]
        public void TestPathUtils_MatchesTextPattern()
        {
            Assert.True(PathUtils.MatchesTextPattern("some-bot", "*bot*"));
            Assert.True(PathUtils.MatchesTextPattern("ci-worker", "ci-*"));
            Assert.True(PathUtils.MatchesTextPattern("some-text", "text"));
            Assert.False(PathUtils.MatchesTextPattern("some-text", "other"));
        }

        [Fact]
        public void TestIdentity_IdentityKey()
        {
            var identity = new GitIdentity { Name = "Alice Smith", Email = "Alice@Example.com" };
            Assert.Equal("alice smith <alice@example.com>", IdentityUtils.IdentityKey(identity));
        }

        [Fact]
        public void TestIdentity_SameIdentity()
        {
            var left = new GitIdentity { Name = "Alice Smith", Email = "alice@example.com" };
            var right = new GitIdentity { Name = "ALICE SMITH", Email = "ALICE@EXAMPLE.COM" };
            Assert.True(IdentityUtils.SameIdentity(left, right));

            var diff = new GitIdentity { Name = "Bob", Email = "bob@example.com" };
            Assert.False(IdentityUtils.SameIdentity(left, diff));
        }

        [Fact]
        public void TestIdentity_ResolveAlias()
        {
            var identity = new GitIdentity { Name = "Alice S", Email = "alices@example.com" };
            var canonical = new GitIdentity { Name = "Alice Smith", Email = "alice@example.com" };
            var aliasRule = new AliasRule
            {
                Canonical = canonical,
                Identities = new List<GitIdentity> { new() { Name = "Alice S", Email = "alices@example.com" } }
            };

            var resolved = IdentityUtils.ResolveAlias(identity, new List<AliasRule> { aliasRule });
            Assert.Equal("Alice Smith", resolved.Name);
            Assert.Equal("alice@example.com", resolved.Email);

            var unresolved = IdentityUtils.ResolveAlias(new GitIdentity { Name = "Bob", Email = "bob@example.com" }, new List<AliasRule> { aliasRule });
            Assert.Equal("Bob", unresolved.Name);
        }

        [Fact]
        public void TestIdentity_IsBotIdentity()
        {
            Assert.True(IdentityUtils.IsBotIdentity(new GitIdentity { Name = "Dependabot", Email = "dependabot@github.com" }, new List<BotRule>()));
            Assert.True(IdentityUtils.IsBotIdentity(new GitIdentity { Name = "My Bot", Email = "my-bot@example.com" }, new List<BotRule>()));
            Assert.True(IdentityUtils.IsBotIdentity(new GitIdentity { Name = "gemini cli", Email = "someone@example.com" }, new List<BotRule>()));
            Assert.True(IdentityUtils.IsBotIdentity(new GitIdentity { Name = "Some Bot", Email = "bot@ampcode.com" }, new List<BotRule>()));

            var botRules = new List<BotRule>
            {
                new() { Name = "MyConfiguredBot" },
                new() { Email = "custom-bot@test.org" },
                new() { Pattern = "ci-worker-*" }
            };

            Assert.True(IdentityUtils.IsBotIdentity(new GitIdentity { Name = "myconfiguredbot", Email = "some@email.com" }, botRules));
            Assert.True(IdentityUtils.IsBotIdentity(new GitIdentity { Name = "Some User", Email = "custom-bot@test.org" }, botRules));
            Assert.True(IdentityUtils.IsBotIdentity(new GitIdentity { Name = "CI-Worker-01", Email = "worker@test.org" }, botRules));
            Assert.False(IdentityUtils.IsBotIdentity(new GitIdentity { Name = "Normal User", Email = "normal@example.com" }, botRules));
        }

        [Fact]
        public void TestIdentity_IdentityRegistry()
        {
            var aliases = new List<AliasRule>
            {
                new()
                {
                    Canonical = new GitIdentity { Name = "Alice Smith", Email = "alice@example.com" },
                    Identities = new List<GitIdentity> { new() { Name = "Alice S", Email = "alices@example.com" } }
                }
            };
            var bots = new List<BotRule> { new() { Name = "SpecialBot" } };
            var registry = new IdentityRegistry(aliases, bots, mergeOnEmail: true);

            // Resolve aliased identity
            var aliceS = new GitIdentity { Name = "Alice S", Email = "alices@example.com" };
            var resolvedAlice = registry.Resolve(aliceS);
            Assert.Equal("Alice Smith", resolvedAlice.Name);
            Assert.Equal("alice@example.com", resolvedAlice.Email);

            // Register real identity to nameToRealCanonical map
            registry.RegisterRealIdentity(new GitIdentity { Name = "bob", Email = "bob@real.com" });

            // Resolve real identity first so it maps to the canonical list
            registry.Resolve(new GitIdentity { Name = "bob", Email = "bob@real.com" });

            // Github noreply merging to real identity email
            var noreplyEmail = "12345+bob@users.noreply.github.com";
            var resolvedNoreply = registry.Resolve(new GitIdentity { Name = "bob", Email = noreplyEmail });
            Assert.Equal("bob", resolvedNoreply.Name);
            Assert.Equal("bob@real.com", resolvedNoreply.Email);

            // Bot checking
            Assert.True(registry.IsBot(new GitIdentity { Name = "SpecialBot", Email = "bot@example.com" }));

            // Collision tracking
            registry.Resolve(new GitIdentity { Name = "Charlie A", Email = "charlie@shared.com" });
            registry.Resolve(new GitIdentity { Name = "Charlie B", Email = "charlie@shared.com" });
            var collisions = registry.GetEmailCollisions();
            Assert.Single(collisions);
            Assert.Equal("charlie@shared.com", collisions[0].Email);
            Assert.Equal(2, collisions[0].Names.Count);
            Assert.Contains("Charlie A", collisions[0].Names);
            Assert.Contains("Charlie B", collisions[0].Names);
        }

        [Fact]
        public void TestGitParser_CleanSymbol()
        {
            Assert.Equal("", GitParser.CleanSymbol("@decorator"));
            Assert.Equal("", GitParser.CleanSymbol("import { something } from 'somewhere'"));
            Assert.Equal("", GitParser.CleanSymbol("using System;"));
            Assert.Equal("myFunc()", GitParser.CleanSymbol("myFunc();"));
            Assert.Equal("class MyClass", GitParser.CleanSymbol("class MyClass {  "));
            Assert.Equal("method(param)", GitParser.CleanSymbol("method(param)"));
            Assert.Equal("a_very_long_symbol_string_that_exceeds_sixty_characters_shou...", GitParser.CleanSymbol("a_very_long_symbol_string_that_exceeds_sixty_characters_should_be_truncated_with_ellipses"));
        }

        [Fact]
        public void TestGitParser_ParseCoAuthors()
        {
            string message = "Commit message here\n\nCo-authored-by: Alice <alice@example.com>\nCo-authored-by: Bob <bob@example.com>\nCo-authored-by: Alice <alice@example.com>";
            var coAuthors = GitParser.ParseCoAuthors(message);
            Assert.Equal(2, coAuthors.Count);
            Assert.Equal("Alice", coAuthors[0].Name);
            Assert.Equal("alice@example.com", coAuthors[0].Email);
            Assert.Equal("Bob", coAuthors[1].Name);
            Assert.Equal("bob@example.com", coAuthors[1].Email);
        }

        [Fact]
        public void TestGitParser_NormalizeNumstatPath()
        {
            Assert.Equal("src/main.cs", GitParser.NormalizeNumstatPath("src/{utils => main}.cs"));
            Assert.Equal("src/main.cs", GitParser.NormalizeNumstatPath("src/main.cs"));
        }

        [Fact]
        public void TestGitParser_ParseGitLog()
        {
            string logOutput = $@"__GITIZER_COMMIT__
hash1
2026-06-01T12:00:00Z
Author Name
author@email.com
parent_hash

This is a commit message.
__GITIZER_NUMSTAT__
10	5	src/main.cs
2	0	src/helper.cs
";
            var records = GitParser.ParseGitLog(logOutput);
            Assert.Single(records);
            var record = records[0];
            Assert.Equal("hash1", record.Hash);
            Assert.Equal("Author Name", record.Author.Name);
            Assert.Equal("author@email.com", record.Author.Email);
            Assert.Equal("This is a commit message.", record.Message);
            Assert.Equal(2, record.Files.Count);
            Assert.Equal("src/main.cs", record.Files[0].Path);
            Assert.Equal(10, record.Files[0].Added);
            Assert.Equal(5, record.Files[0].Deleted);
        }

        [Fact]
        public void TestMetricProcessors()
        {
            var accums = new List<ContributorAccumulator>
            {
                new()
                {
                    Identity = new GitIdentity { Name = "Alice", Email = "alice@example.com" },
                    TotalActivity = 100.0,
                    Areas = new Dictionary<string, double> { { "Frontend", 80.0 }, { "Backend", 20.0 } }
                },
                new()
                {
                    Identity = new GitIdentity { Name = "Bob", Email = "bob@example.com" },
                    TotalActivity = 50.0,
                    Areas = new Dictionary<string, double> { { "Backend", 50.0 } }
                }
            };

            var contributors = MetricProcessors.RenderContributors(accums);
            Assert.Equal(2, contributors.Count);
            Assert.Equal("Alice", contributors[0].Name);
            Assert.Equal(100.0, contributors[0].TotalActivity);
            Assert.Equal(2, contributors[0].Areas.Count);
            Assert.Equal("Frontend", contributors[0].Areas[0].Area);
            Assert.Equal(80.0, contributors[0].Areas[0].Activity);
            Assert.Equal(0.8, contributors[0].Areas[0].ActivityShare);

            var automation = MetricProcessors.RenderAutomation(accums);
            Assert.Equal(2, automation.Count);
            Assert.Equal("Alice", automation[0].Name);

            var files = new List<FileMetric>
            {
                new() { Path = "f1", HeatScore = 10, AttentionScore = 5 },
                new() { Path = "f2", HeatScore = 5, AttentionScore = 20 }
            };

            var sortedFilesAreas = MetricProcessors.SortFilesForCommand(files, AnalysisCommand.Areas);
            Assert.Equal("f1", sortedFilesAreas[0].Path);

            var sortedFilesHotspots = MetricProcessors.SortFilesForCommand(files, AnalysisCommand.Hotspots);
            Assert.Equal("f2", sortedFilesHotspots[0].Path);
        }

        [Fact]
        public void TestTemporalCouplingEngine()
        {
            var engine = new TemporalCouplingEngine(10);

            engine.TrackCommitFiles(new List<string> { "fileA.ts", "fileB.ts" });
            engine.TrackCommitFiles(new List<string> { "fileA.ts", "fileB.ts" });
            engine.TrackCommitFiles(new List<string> { "fileA.ts", "fileB.ts" });

            var couplings = engine.CalculateTemporalCoupling();
            Assert.Single(couplings);
            Assert.Equal("fileA.ts", couplings[0].FileA);
            Assert.Equal("fileB.ts", couplings[0].FileB);
            Assert.Equal(3, couplings[0].SharedCommits);
            Assert.Equal(1.0, couplings[0].CouplingDegree);

            var info = engine.GetOversizedCommitInfo();
            Assert.Equal(0, info.count);
        }

        [Fact]
        public void TestTemporalCouplingEngine_FiltersAndOversized()
        {
            var engine = new TemporalCouplingEngine(2);

            // too few shared commits
            engine.TrackCommitFiles(new List<string> { "fileA.ts", "fileB.ts" });
            engine.TrackCommitFiles(new List<string> { "fileA.ts", "fileB.ts" });

            var couplings = engine.CalculateTemporalCoupling();
            Assert.Empty(couplings);

            // Track an oversized commit
            engine.TrackCommitFiles(new List<string> { "fileA.ts", "fileB.ts", "fileC.ts" });

            var info = engine.GetOversizedCommitInfo();
            Assert.Equal(1, info.count);
            Assert.Equal(3, info.maxObserved);
            Assert.Equal(2, info.limit);
        }

        [Fact]
        public void TestLeadTimeEngine()
        {
            var engine = new LeadTimeEngine();
            var alice = new GitIdentity { Name = "Alice", Email = "alice@example.com" };

            var commits = new List<GitCommitRecord>
            {
                new()
                {
                    Hash = "C1",
                    Date = "2026-06-01T12:00:00Z",
                    Timestamp = 1780324800000,
                    Author = alice,
                    ParentCount = 0,
                    Parents = new List<string>(),
                    Message = "Initial commit",
                    Files = new List<GitFileChange> { new() { Path = "file1.ts", Added = 10, Deleted = 0 } }
                },
                new()
                {
                    Hash = "C2",
                    Date = "2026-06-02T12:00:00Z",
                    Timestamp = 1780411200000,
                    Author = alice,
                    ParentCount = 1,
                    Parents = new List<string> { "C1" },
                    Message = "Work on main",
                    Files = new List<GitFileChange> { new() { Path = "file1.ts", Added = 5, Deleted = 0 } }
                },
                new()
                {
                    Hash = "C3",
                    Date = "2026-06-03T12:00:00Z",
                    Timestamp = 1780497600000,
                    Author = alice,
                    ParentCount = 1,
                    Parents = new List<string> { "C1" },
                    Message = "Feature commit",
                    Files = new List<GitFileChange> { new() { Path = "file2.ts", Added = 8, Deleted = 0 } }
                },
                new()
                {
                    Hash = "M1",
                    Date = "2026-06-04T12:00:00Z",
                    Timestamp = 1780584000000,
                    Author = alice,
                    ParentCount = 2,
                    Parents = new List<string> { "C2", "C3" },
                    Message = "Merge pull request #1",
                    Files = new List<GitFileChange>()
                }
            };

            var result = engine.CalculateLeadTimes(commits);
            Assert.Single(result.Merges);
            Assert.Equal("M1", result.Merges[0].Hash);
            Assert.Equal("Merge pull request #1", result.Merges[0].Message);
            Assert.Equal("Alice", result.Merges[0].Author);
            Assert.Equal("2026-06-04T12:00:00Z", result.Merges[0].Date);
            Assert.Equal(24.0, result.Merges[0].LeadTimeHours);
            Assert.Equal(1, result.Merges[0].FileCount);
            Assert.Equal(24.0, result.AverageLeadTimeHours);
        }

        [Fact]
        public void TestConsoleTableBuilder()
        {
            var builder = new ConsoleTableBuilder();
            builder.AddColumn("Name", 10, "left");
            builder.AddColumn("Age", 5, "right");
            builder.AddColumn("Role");

            builder.AddRow(new List<string> { "Alice", "30", "Engineer" });
            builder.AddRow(new List<string> { "Bob", "25", "Designer" });

            var expected = "Name         Age Role\nAlice         30 Engineer\nBob           25 Designer";
            Assert.Equal(expected, builder.Render());
        }

        [Fact]
        public void TestConsoleTableBuilder_EmptyRowsAndDefaultAlign()
        {
            var builder = new ConsoleTableBuilder();
            builder.AddColumn("Col1", 8);
            builder.AddColumn("Col2", 8, "right");

            builder.AddRow(new List<string> { "val1", "val2" });

            var expected = "Col1         Col2\nval1         val2";
            Assert.Equal(expected, builder.Render());
        }

        [Fact]
        public void TestScoring_ConcentrationTier()
        {
            Assert.Equal("healthy", ScoringUtils.ConcentrationTier(0.49));
            Assert.Equal("watch", ScoringUtils.ConcentrationTier(0.5));
            Assert.Equal("watch", ScoringUtils.ConcentrationTier(0.69));
            Assert.Equal("silo", ScoringUtils.ConcentrationTier(0.7));
            Assert.Equal("silo", ScoringUtils.ConcentrationTier(1.0));
        }

        [Fact]
        public void TestScoring_HeatScoreCalculator()
        {
            var calculator = new HeatScoreCalculator();
            var breakdown = new ScoreBreakdown
            {
                Touches = 0.5,
                Churn = 0.3,
                Recency = 0.8,
                ContributorSpread = 0,
                LowFamiliarityConcentration = 0
            };
            // Expected math: (0.5 * 0.45 + 0.3 * 0.45 + 0.8 * 0.1) * 100
            // = (0.225 + 0.135 + 0.08) * 100 = 0.44 * 100 = 44
            Assert.Equal(44, calculator.Calculate(breakdown));
        }

        [Fact]
        public void TestScoring_AttentionScoreCalculator()
        {
            var weights = new AttentionWeights
            {
                Churn = 0.25,
                Recency = 0.25,
                ContributorSpread = 0.25,
                LowFamiliarityConcentration = 0.25
            };
            var calculator = new AttentionScoreCalculator(weights);
            var breakdown = new ScoreBreakdown
            {
                Touches = 0,
                Churn = 0.8,
                Recency = 0.4,
                ContributorSpread = 0.6,
                LowFamiliarityConcentration = 0.2
            };
            // Expected math: (0.8 * 0.25 + 0.4 * 0.25 + 0.6 * 0.25 + 0.2 * 0.25) * 100
            // = (0.2 + 0.1 + 0.15 + 0.05) * 100 = 0.50 * 100 = 50
            Assert.Equal(50, calculator.Calculate(breakdown));
        }

        [Fact]
        public void TestCliReportFormatter_Format()
        {
            var result = new AnalysisResult
            {
                Exclusions = new List<ExclusionSummary>
                {
                    new() { Category = "test-exclude", Pattern = "*.log", Count = 5 }
                },
                Warnings = new List<string> { "warning-one" }
            };
            var formatter = new CliReportFormatter(result);

            // TableString start early return
            var tableStringEarly = "No contributor activity matched the selected analysis.\n";
            Assert.Equal(tableStringEarly, formatter.Format(tableStringEarly));

            // Standard formatting with exclusions and warnings
            var tableString = "Col1 Col2\nVal1 Val2";
            var formatted = formatter.Format(tableString, includeWarnings: true);
            Assert.Equal("Col1 Col2\nVal1 Val2\nexclusions test-exclude:5\nwarnings warning-one\n", formatted);

            // Standard formatting without warnings
            var formattedNoWarnings = formatter.Format(tableString, includeWarnings: false);
            Assert.Equal("Col1 Col2\nVal1 Val2\nexclusions test-exclude:5\n", formattedNoWarnings);
        }

        [Fact]
        public void TestCliTableRenderer_EmptyState()
        {
            var renderer = new CliTableRenderer(AnalysisCommand.Hotspots);
            var result = new AnalysisResult
            {
                Analysis = new AnalysisMetadata
                {
                    RepoRoot = "/root",
                    Command = AnalysisCommand.Hotspots,
                    GeneratedAt = "2026-07-24T00:00:00.000Z",
                    CommitCount = 0,
                    IncludedFileChangeCount = 0
                }
            };
            var formatted = renderer.RenderAsync(result).GetAwaiter().GetResult();
            Assert.Contains("No commits matched the selected analysis window", formatted);
        }

        [Fact]
        public void TestWarningCollection_LegacyFunctions()
        {
            // EmailCollisionWarnings
            var collisions = new List<EmailCollision>
            {
                new() { Email = "test@example.com", Names = new List<string> { "Alice", "Bob" } }
            };
            var warnings1 = WarningCollector.CollectEmailCollisionWarnings(collisions, 0);
            Assert.Single(warnings1);
            Assert.Contains("Contributors Alice, Bob share email test@example.com", warnings1[0]);

            var warnings1Empty = WarningCollector.CollectEmailCollisionWarnings(collisions, 1);
            Assert.Empty(warnings1Empty);

            // BotConfigWarning
            var automationMetrics = new List<AutomationMetric>
            {
                new() { Name = "bot", Email = "bot@example.com", TotalActivity = 10, Areas = new() }
            };
            var warnings2 = WarningCollector.CollectBotConfigWarning(0, automationMetrics);
            Assert.Single(warnings2);
            Assert.Contains("No bots are explicitly configured", warnings2[0]);

            var warnings2Empty = WarningCollector.CollectBotConfigWarning(1, automationMetrics);
            Assert.Empty(warnings2Empty);

            // LeadTimeWarning
            var warnings3Null = WarningCollector.CollectLeadTimeWarning(null);
            Assert.Single(warnings3Null);
            Assert.Contains("No merge commits in the analysis window", warnings3Null[0]);

            var warnings3EmptyList = WarningCollector.CollectLeadTimeWarning(new LeadTimesInfo { Merges = new() });
            Assert.Single(warnings3EmptyList);

            var warnings3HasMerges = WarningCollector.CollectLeadTimeWarning(new LeadTimesInfo
            {
                Merges = new List<MergeLeadTimeRecord> { new() { LeadTimeHours = 1 } }
            });
            Assert.Empty(warnings3HasMerges);

            // NoBotsWarning
            var warnings4 = WarningCollector.CollectNoBotsWarning(0, new());
            Assert.Single(warnings4);
            Assert.Contains("No bots are configured and no automation identities were detected", warnings4[0]);

            var warnings4HasConfigured = WarningCollector.CollectNoBotsWarning(1, new());
            Assert.Empty(warnings4HasConfigured);

            var warnings4HasDetected = WarningCollector.CollectNoBotsWarning(0, automationMetrics);
            Assert.Empty(warnings4HasDetected);

            // TemporalCouplingWarning
            var engine = new TemporalCouplingEngine(1);
            engine.TrackCommitFiles(new List<string> { "file1.ts", "file2.ts" });
            var warnings5 = WarningCollector.CollectTemporalCouplingWarning(engine);
            Assert.Single(warnings5);
            Assert.Contains("1 commit(s) changed more than 1 files", warnings5[0]);

            var normalEngine = new TemporalCouplingEngine(5);
            normalEngine.TrackCommitFiles(new List<string> { "file1.ts", "file2.ts" });
            var warnings5Empty = WarningCollector.CollectTemporalCouplingWarning(normalEngine);
            Assert.Empty(warnings5Empty);
        }

        [Fact]
        public void TestWarningCollector_CollectAndAggregate()
        {
            var engine = new TemporalCouplingEngine(1);
            engine.TrackCommitFiles(new List<string> { "file1.ts", "file2.ts" });

            var context = new WarningContext
            {
                EmailCollisions = new List<EmailCollision>
                {
                    new() { Email = "test@example.com", Names = new List<string> { "Alice", "Bob" } }
                },
                AliasCount = 0,
                ConfiguredBotCount = 0,
                AutomationMetrics = new(),
                LeadTimes = null,
                TemporalCouplingEngine = engine
            };

            var collector = new WarningCollector();
            var warnings = collector.Collect(context);

            Assert.Equal(4, warnings.Count);
            Assert.Contains(warnings, w => w.Contains("share email test@example.com"));
            Assert.Contains(warnings, w => w.Contains("No bots are configured"));
            Assert.Contains(warnings, w => w.Contains("No merge commits in the analysis window"));
            Assert.Contains(warnings, w => w.Contains("changed more than 1 files"));
        }

        [Fact]
        public void TestGeneratedFileWarningRule()
        {
            var rule = new GeneratedFileWarningRule();
            var contextEmpty = new WarningContext { Files = new() };
            Assert.Empty(rule.Collect(contextEmpty));

            var contextSuspicious = new WarningContext
            {
                Files = new List<FileMetric>
                {
                    new()
                    {
                        Path = "scaffolding.cs",
                        Touches = 1,
                        Churn = 250,
                        Contributors = new List<ContributorShare>
                        {
                            new() { ActivityShare = 0.995 }
                        }
                    }
                }
            };

            var warnings = rule.Collect(contextSuspicious);
            Assert.Single(warnings);
            Assert.Contains("1 file(s) have single-touch high churn (>200 lines) with a single author", warnings[0]);

            var contextNormal = new WarningContext
            {
                Files = new List<FileMetric>
                {
                    new()
                    {
                        Path = "normal.cs",
                        Touches = 5,
                        Churn = 250,
                        Contributors = new List<ContributorShare>
                        {
                            new() { ActivityShare = 0.5 }
                        }
                    }
                }
            };
            Assert.Empty(rule.Collect(contextNormal));
        }

        [Fact]
        public void TestWarningCollector_DeduplicationAndSorting()
        {
            var rule = new MockWarningRule();
            var collector = new WarningCollector(new List<IWarningRule> { rule });

            var existing = new List<string>
            {
                "Charlie warning",
                "Alpha warning", // duplicate with rule output
                "Delta warning"
            };

            var result = collector.Collect(new WarningContext(), existing);
            Assert.Equal(4, result.Count);
            Assert.Equal("Alpha warning", result[0]);
            Assert.Equal("Beta warning", result[1]);
            Assert.Equal("Charlie warning", result[2]);
            Assert.Equal("Delta warning", result[3]);
        }

        private class MockWarningRule : IWarningRule
        {
            public List<string> Collect(WarningContext context)
            {
                return new List<string> { "Beta warning", "Alpha warning" };
            }
        }
    }
}
