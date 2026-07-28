using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

            var mapper = new AreaMapper();
            Assert.Equal("Frontend", mapper.AreaForPath("src/ui/components/button.cs", 2, namedAreas));
            Assert.Equal("Backend", mapper.AreaForPath("src/api/controllers/user.cs", 2, namedAreas));
            Assert.Equal("src/db", mapper.AreaForPath("src/db/migrations/init.cs", 2, namedAreas));
            Assert.Equal("src", mapper.AreaForPath("src/db/migrations/init.cs", 1, namedAreas));
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

            // Excluded because it's a binary image
            Assert.False(classifier.Check("images/logo.png"));
            Assert.False(classifier.Check("photo.jpg"));

            // Excluded because they are non-code files
            Assert.False(classifier.Check("README.md"));
            Assert.False(classifier.Check("assets/chart.svg"));
            Assert.False(classifier.Check("docs/manual.pdf"));
            Assert.False(classifier.Check("data/report.xlsx"));

            var exclusions = classifier.GetExclusions();
            Assert.Contains(exclusions, e => e.Category == "lockfile" && e.Pattern == "lockfiles");
            Assert.Contains(exclusions, e => e.Category == "test" && e.Pattern == "tests/**");
            Assert.Contains(exclusions, e => e.Category == "deleted" && e.Pattern == "missing from HEAD");
            Assert.Contains(exclusions, e => e.Category == "binary" && e.Pattern == "image files");
            Assert.Contains(exclusions, e => e.Category == "non_code" && e.Pattern == "non-code files");
        }

        public class MockPathClassifier : IPathClassifier
        {
            public bool Check(string path) => !path.Contains("exclude");
            public List<ExclusionSummary> GetExclusions() => new() { new ExclusionSummary { Category = "test", Pattern = "exclude", Count = 1 } };
        }

        [Fact]
        public void TestChangeAccumulator_WithPathClassifierMock()
        {
            var config = GiticConfig.Default;
            var settings = new AnalysisSettings();
            var mockFilter = new MockPathClassifier();
            var identityRegistry = new IdentityRegistry();
            IChangeAccumulator accumulator = new ChangeAccumulator(config, settings, mockFilter, identityRegistry);

            var list = accumulator.GetExclusions();
            Assert.Single(list);
            Assert.Equal("test", list[0].Category);
        }

        public class MockIdentityRegistry : IIdentityRegistry
        {
            public bool IsBotCalled { get; set; }
            public bool ResolveCalled { get; set; }
            public bool RegisterRealIdentityCalled { get; set; }
            public bool GetEmailCollisionsCalled { get; set; }

            public void RegisterRealIdentity(GitIdentity identity)
            {
                RegisterRealIdentityCalled = true;
            }

            public GitIdentity Resolve(GitIdentity identity)
            {
                ResolveCalled = true;
                return identity;
            }

            public List<EmailCollision> GetEmailCollisions()
            {
                GetEmailCollisionsCalled = true;
                return new List<EmailCollision>();
            }

            public bool IsBot(GitIdentity identity)
            {
                IsBotCalled = true;
                return false;
            }

            public string IdentityKey(GitIdentity identity) => IdentityUtils.IdentityKey(identity);
            public string IdentityKey(string name, string email) => IdentityUtils.IdentityKey(name, email);
            public GitIdentity ResolveAlias(GitIdentity identity) => identity;
            public bool IsGithubNoreply(string email) => IdentityUtils.IsGithubNoreply(email);
            public string ParseNoreplyUsername(string email) => IdentityUtils.ParseNoreplyUsername(email);
            public bool SameIdentity(GitIdentity left, GitIdentity right) => IdentityUtils.SameIdentity(left, right);
        }

        [Fact]
        public void TestChangeAccumulator_WithDecoupledIdentityRegistry()
        {
            var config = GiticConfig.Default;
            var settings = new AnalysisSettings();
            var mockFilter = new MockPathClassifier();
            var mockIdentityRegistry = new MockIdentityRegistry();
            IChangeAccumulator accumulator = new ChangeAccumulator(config, settings, mockFilter, mockIdentityRegistry);

            var commit = new GitCommitRecord
            {
                Author = new GitIdentity { Name = "John Doe", Email = "john@example.com" },
                CoAuthors = new List<GitIdentity> { new GitIdentity { Name = "Co Author", Email = "co@example.com" } },
                Files = new List<GitFileChange> { new GitFileChange { Path = "src/main.cs", Added = 10, Deleted = 5 } }
            };
            accumulator.PrepareIdentityMerging(new List<GitCommitRecord> { commit });
            Assert.True(mockIdentityRegistry.RegisterRealIdentityCalled);

            accumulator.GetEmailCollisions();
            Assert.True(mockIdentityRegistry.GetEmailCollisionsCalled);

            var filesInCommit = new List<string>();
            accumulator.AddCommit(commit, filesInCommit);
            Assert.True(mockIdentityRegistry.IsBotCalled);
            Assert.True(mockIdentityRegistry.ResolveCalled);
        }

        public class MockConfigValidator : IConfigValidator
        {
            public bool ValidateCalled { get; set; }
            public bool NormalizeCalled { get; set; }

            public void ValidateAttentionWeights(AttentionWeights attention, string source, List<string>? errors = null)
            {
                ValidateCalled = true;
            }

            public GiticConfigOverrides NormalizeOverride(object? input, string source)
            {
                NormalizeCalled = true;
                return new GiticConfigOverrides();
            }
        }

        [Fact]
        public void TestConfigValidator_Decoupled()
        {
            IConfigValidator validator = new ConfigValidator();
            var weights = new AttentionWeights
            {
                Churn = 0.35,
                Recency = 0.30,
                ContributorSpread = 0.20,
                LowFamiliarityConcentration = 0.15
            };
            validator.ValidateAttentionWeights(weights, "test"); // should not throw
            
            var mock = new MockConfigValidator();
            mock.ValidateAttentionWeights(weights, "test");
            Assert.True(mock.ValidateCalled);
        }

        public class MockWarningCollector : IWarningCollector
        {
            public bool CollectCalled { get; set; }

            public List<string> Collect(WarningContext context)
            {
                CollectCalled = true;
                return new List<string> { "mock_warning" };
            }

            public List<string> Collect(WarningContext context, List<string>? existingWarnings)
            {
                CollectCalled = true;
                return new List<string> { "mock_warning" };
            }

            public List<Diagnostic> CollectDiagnostics(WarningContext context)
            {
                CollectCalled = true;
                return new List<Diagnostic>
                {
                    new Diagnostic { Code = "GITIC999", Severity = "Warning", Message = "mock_warning" }
                };
            }

            public List<Diagnostic> CollectDiagnostics(WarningContext context, List<string>? existingWarnings)
            {
                CollectCalled = true;
                return new List<Diagnostic>
                {
                    new Diagnostic { Code = "GITIC999", Severity = "Warning", Message = "mock_warning" }
                };
            }
        }

        [Fact]
        public void TestWarningCollector_Decoupled()
        {
            IWarningCollector collector = new WarningCollector();
            var context = new WarningContext();
            var warnings = collector.Collect(context);
            Assert.NotNull(warnings);

            var mock = new MockWarningCollector();
            var mockWarnings = mock.Collect(context);
            Assert.True(mock.CollectCalled);
            Assert.Contains("mock_warning", mockWarnings);
        }

        [Fact]
        public void TestWarningRuleProviderSeam()
        {
            var defaultProvider = new DefaultWarningRuleProvider();
            var rules = defaultProvider.GetRules();
            Assert.NotNull(rules);
            Assert.Equal(6, rules.Count);
            Assert.Contains(rules, r => r is EmailCollisionWarningRule);
            Assert.Contains(rules, r => r is BotConfigWarningRule);
            Assert.Contains(rules, r => r is LeadTimeWarningRule);
            Assert.Contains(rules, r => r is NoBotsWarningRule);
            Assert.Contains(rules, r => r is TemporalCouplingWarningRule);
            Assert.Contains(rules, r => r is GeneratedFileWarningRule);

            // WarningCollector uses DefaultWarningRuleProvider by default when null is passed
            var collectorDefault = new WarningCollector();
            var context = new WarningContext();
            var warnings = collectorDefault.Collect(context);
            // Verify default rules are evaluated: e.g. LeadTimeWarningRule should generate a warning
            Assert.Contains(warnings, w => w.Contains("No merge commits in the analysis window"));

            // Custom provider implementation
            var customRule = new MockWarningRule();
            var customProvider = new MockWarningRuleProvider(new List<IWarningRule> { customRule });
            var collectorCustom = new WarningCollector(customProvider);
            var customWarnings = collectorCustom.Collect(context);
            Assert.Equal(2, customWarnings.Count);
            Assert.Equal("Alpha warning", customWarnings[0]);
            Assert.Equal("Beta warning", customWarnings[1]);
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

            IGitGraph gitGraph = new GitGraphCalculator();
            var ancestors = gitGraph.GetAncestors("C4", commitMap, 10);
            Assert.Contains("C4", ancestors);
            Assert.Contains("C2", ancestors);
            Assert.Contains("C3", ancestors);
            Assert.Contains("C1", ancestors);

            var branchCommits = gitGraph.GetBranchCommits("C3", new HashSet<string> { "C2", "C1" }, commitMap, 10);
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
            Assert.Equal(string.Empty, PathUtils.NormalizeGitPath(null));
            Assert.Equal(string.Empty, PathUtils.NormalizeGitPath(string.Empty));
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
        public void TestPathUtils_GlobToRegExp_Caching()
        {
            var rx1 = PathUtils.GlobToRegExp("some-pattern*");
            var rx2 = PathUtils.GlobToRegExp("some-pattern*");
            Assert.Same(rx1, rx2);

            var rx3 = PathUtils.GlobToRegExp("different-pattern*");
            Assert.NotSame(rx1, rx3);
        }

        [Fact]
        public void TestIdentity_IdentityKey()
        {
            var identity = new GitIdentity { Name = "Alice Smith", Email = "Alice@Example.com" };
            Assert.Equal("alice smith <alice@example.com>", IdentityUtils.IdentityKey(identity));
        }

        [Fact]
        public void TestIdentity_IdentityKey_Defensive()
        {
            Assert.Equal(string.Empty, IdentityUtils.IdentityKey(null!));

            var identityNullName = new GitIdentity { Name = null!, Email = "Alice@Example.com" };
            Assert.Equal(" <alice@example.com>", IdentityUtils.IdentityKey(identityNullName));

            var identityNullEmail = new GitIdentity { Name = "Alice Smith", Email = null! };
            Assert.Equal("alice smith <>", IdentityUtils.IdentityKey(identityNullEmail));

            var identityBothNull = new GitIdentity { Name = null!, Email = null! };
            Assert.Equal(" <>", IdentityUtils.IdentityKey(identityBothNull));
        }

        private class CustomKeyGenerator : IIdentityKeyGenerator
        {
            public string IdentityKey(GitIdentity identity)
            {
                return identity.Name.ToLowerInvariant();
            }
        }

        [Fact]
        public void TestCustomIdentityKeyGenerator()
        {
            var identity = new GitIdentity { Name = "Alice Smith", Email = "Alice@Example.com" };
            var customGenerator = new CustomKeyGenerator();

            // Verify that the custom generator computes keys as expected
            Assert.Equal("alice smith", customGenerator.IdentityKey(identity));

            // Verify that we can instantiate IdentityRegistry with custom key generator
            var registry = new IdentityRegistry(keyGenerator: customGenerator);
            Assert.NotNull(registry);
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
        public void TestGitParserImplAndInterface()
        {
            IGitParser parser = new GitParser(new GitPatchParser());
            Assert.Equal("__GITIC_COMMIT__", parser.CommitMarker);
            Assert.Equal("__GITIC_NUMSTAT__", parser.NumstatMarker);

            // Test parsing with a minimal valid log containing one commit
            string sampleOutput = 
                "__GITIC_COMMIT__\n" +
                "abc1234\n" +
                "2026-07-25T08:34:07Z\n" +
                "Alice Smith\n" +
                "alice@example.com\n" +
                "parent123\n" +
                "Implement IGitParser interface\n" +
                "__GITIC_NUMSTAT__\n" +
                "5\t3\tsrc/Gitparser.cs\n" +
                "diff --git a/src/Gitparser.cs b/src/Gitparser.cs\n" +
                "@@ -1,3 +1,15 @@\n";

            var commits = parser.ParseGitLog(sampleOutput);
            Assert.Single(commits);

            var commit = commits[0];
            Assert.Equal("abc1234", commit.Hash);
            Assert.Equal("Alice Smith", commit.Author.Name);
            Assert.Equal("alice@example.com", commit.Author.Email);
            Assert.Equal("Implement IGitParser interface", commit.Message);
            Assert.Equal(1, commit.ParentCount);
            
            Assert.Single(commit.Files);
            var file = commit.Files[0];
            Assert.Equal("src/Gitparser.cs", file.Path);
            Assert.Equal(5, file.Added);
            Assert.Equal(3, file.Deleted);
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
        public void TestIdentity_ParseNoreplyUsername()
        {
            Assert.Equal("bob", IdentityUtils.ParseNoreplyUsername("12345+bob@users.noreply.github.com"));
            Assert.Equal("bob", IdentityUtils.ParseNoreplyUsername("bob@users.noreply.github.com"));
            Assert.Equal("bob", IdentityUtils.ParseNoreplyUsername("12345+bob"));
            Assert.Equal("bob", IdentityUtils.ParseNoreplyUsername("12345+BoB@users.noreply.github.com"));
            Assert.Equal(string.Empty, IdentityUtils.ParseNoreplyUsername(null!));
            Assert.Equal(string.Empty, IdentityUtils.ParseNoreplyUsername(""));
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
            var parser = new GitPatchParser();
            Assert.Equal("", parser.CleanSymbol("@decorator"));
            Assert.Equal("", parser.CleanSymbol("import { something } from 'somewhere'"));
            Assert.Equal("", parser.CleanSymbol("using System;"));
            Assert.Equal("myFunc()", parser.CleanSymbol("myFunc();"));
            Assert.Equal("class MyClass", parser.CleanSymbol("class MyClass {  "));
            Assert.Equal("method(param)", parser.CleanSymbol("method(param)"));
            Assert.Equal("a_very_long_symbol_string_that_exceeds_sixty_characters_shou...", parser.CleanSymbol("a_very_long_symbol_string_that_exceeds_sixty_characters_should_be_truncated_with_ellipses"));
        }

        [Fact]
        public void TestGitParser_ParseCoAuthors()
        {
            var parser = new GitParser(new GitPatchParser());
            string message = "Commit message here\n\nCo-authored-by: Alice <alice@example.com>\nCo-authored-by: Bob <bob@example.com>\nCo-authored-by: Alice <alice@example.com>";
            var coAuthors = parser.ParseCoAuthors(message);
            Assert.Equal(2, coAuthors.Count);
            Assert.Equal("Alice", coAuthors[0].Name);
            Assert.Equal("alice@example.com", coAuthors[0].Email);
            Assert.Equal("Bob", coAuthors[1].Name);
            Assert.Equal("bob@example.com", coAuthors[1].Email);
        }

        [Fact]
        public void TestGitParser_NormalizeNumstatPath()
        {
            var parser = new GitPatchParser();
            Assert.Equal("src/main.cs", parser.NormalizeNumstatPath("src/{utils => main}.cs"));
            Assert.Equal("src/main.cs", parser.NormalizeNumstatPath("src/main.cs"));
            Assert.Equal("new_main.cs", parser.NormalizeNumstatPath("old_main.cs => new_main.cs"));
        }

        [Fact]
        public void TestGitParser_ParseNumstatAndPatches_BinaryAndInvalid()
        {
            var parser = new GitPatchParser();
            string numstatText = "-\t-\tbin/logo.png\nxyz\t999\tsrc/weird.cs\n";
            var files = parser.ParseNumstatAndPatches(numstatText);
            Assert.Equal(2, files.Count);

            var logo = files[0];
            Assert.Equal("bin/logo.png", logo.Path);
            Assert.Equal(0, logo.Added);
            Assert.Equal(0, logo.Deleted);

            var weird = files[1];
            Assert.Equal("src/weird.cs", weird.Path);
            Assert.Equal(0, weird.Added);
            Assert.Equal(999, weird.Deleted);
        }

        [Fact]
        public void TestGitParser_ParseGitLog()
        {
            var parser = new GitParser(new GitPatchParser());
            string logOutput = $@"__GITIC_COMMIT__
hash1
2026-06-01T12:00:00Z
Author Name
author@email.com
parent_hash

This is a commit message.
__GITIC_NUMSTAT__
10	5	src/main.cs
2	0	src/helper.cs
";
            var records = parser.ParseGitLog(logOutput);
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
        public void TestGitParser_BuildGitLogArguments()
        {
            var parser = new GitParser(new GitPatchParser());
            
            // Case 1: AllTime=true, IncludeMerges=true
            var options1 = new GitHistoryExtractorOptions
            {
                AllTime = true,
                IncludeMerges = true
            };
            var args1 = parser.BuildGitLogArguments(options1);
            Assert.Contains("log", args1);
            Assert.Contains("--numstat", args1);
            Assert.Contains("-p", args1);
            Assert.Contains("--cc", args1);
            Assert.DoesNotContain("--no-merges", args1);
            Assert.All(args1, arg => Assert.False(arg.StartsWith("--since=")));

            // Case 2: AllTime=false, IncludeMerges=false, Since="2026-01-01T00:00:00Z"
            var options2 = new GitHistoryExtractorOptions
            {
                AllTime = false,
                IncludeMerges = false,
                Since = "2026-01-01T00:00:00Z"
            };
            var args2 = parser.BuildGitLogArguments(options2);
            Assert.Contains("--no-merges", args2);
            Assert.DoesNotContain("--cc", args2);
            Assert.Contains("--since=2026-01-01T00:00:00Z", args2);
        }

        [Fact]
        public void TestMetricProcessors()
        {
            var service = new MetricProcessorService();
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

            var contributors = service.RenderContributors(accums);
            Assert.Equal(2, contributors.Count);
            Assert.Equal("Alice", contributors[0].Name);
            Assert.Equal(100.0, contributors[0].TotalActivity);
            Assert.Equal(2, contributors[0].Areas.Count);
            Assert.Equal("Frontend", contributors[0].Areas[0].Area);
            Assert.Equal(80.0, contributors[0].Areas[0].Activity);
            Assert.Equal(0.8, contributors[0].Areas[0].ActivityShare);

            var automation = service.RenderAutomation(accums);
            Assert.Equal(2, automation.Count);
            Assert.Equal("Alice", automation[0].Name);

            var files = new List<FileMetric>
            {
                new() { Path = "f1", HeatScore = 10, AttentionScore = 5 },
                new() { Path = "f2", HeatScore = 5, AttentionScore = 20 }
            };

            var sortedFilesAreas = service.SortFilesForCommand(files, AnalysisCommand.Areas);
            Assert.Equal("f1", sortedFilesAreas[0].Path);

            var sortedFilesHotspots = service.SortFilesForCommand(files, AnalysisCommand.Hotspots);
            Assert.Equal("f2", sortedFilesHotspots[0].Path);

            // Test sorting by Lines and Size (ch count)
            var filesWithSize = new List<FileMetric>
            {
                new() { Path = "smallLinesLargeSize", Lines = 5, Size = 1000, AttentionScore = 10 },
                new() { Path = "largeLinesSmallSize", Lines = 20, Size = 100, AttentionScore = 5 },
                new() { Path = "sameLinesLargerSize", Lines = 20, Size = 500, AttentionScore = 1 }
            };
            var sortedFilesBySize = service.SortFilesForCommand(filesWithSize, AnalysisCommand.Hotspots);
            Assert.Equal("sameLinesLargerSize", sortedFilesBySize[0].Path); // 20 lines, 500 chars (larger than 100 chars)
            Assert.Equal("largeLinesSmallSize", sortedFilesBySize[1].Path); // 20 lines, 100 chars
            Assert.Equal("smallLinesLargeSize", sortedFilesBySize[2].Path); // 5 lines, 1000 chars
        }

        [Fact]
        public void TestTemporalCouplingEngine()
        {
            var engine = new TemporalCouplingEngine(10);

            var result = engine.CalculateTemporalCoupling(new List<List<string>> {
                new List<string> { "fileA.ts", "fileB.ts" },
                new List<string> { "fileA.ts", "fileB.ts" },
                new List<string> { "fileA.ts", "fileB.ts" }
            });
            Assert.Single(result.Couplings);
            Assert.Equal("fileA.ts", result.Couplings[0].FileA);
            Assert.Equal("fileB.ts", result.Couplings[0].FileB);
            Assert.Equal(3, result.Couplings[0].SharedCommits);
            Assert.Equal(1.0, result.Couplings[0].CouplingDegree);

            Assert.Equal(0, result.OversizedCommitCount);
        }

        [Fact]
        public void TestTemporalCouplingEngine_FiltersAndOversized()
        {
            var engine = new TemporalCouplingEngine(2);

            // too few shared commits, plus an oversized commit in a single calculation
            var result = engine.CalculateTemporalCoupling(new List<List<string>> {
                new List<string> { "fileA.ts", "fileB.ts" },
                new List<string> { "fileA.ts", "fileB.ts" },
                new List<string> { "fileA.ts", "fileB.ts", "fileC.ts" }
            });
            Assert.Empty(result.Couplings);
            Assert.Equal(1, result.OversizedCommitCount);
            Assert.Equal(3, result.MaxObservedFiles);
            Assert.Equal(2, result.Limit);
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
        public void TestIConsoleTableBuilderInterface()
        {
            IConsoleTableBuilder builder = new ConsoleTableBuilder()
                .AddColumn("Col1", 8)
                .AddColumn("Col2", 8, "right")
                .AddRow(new List<string> { "val1", "val2" });

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
            Assert.Equal(44, ScoringUtils.CalculateHeatScore(breakdown));
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
            Assert.Equal(50, ScoringUtils.CalculateAttentionScore(breakdown, weights));
        }

        [Fact]
        public void TestScoring_CalculateDebtVolatility()
        {
            var item = new ItemAccumulator
            {
                Added = 100,
                Deleted = 20,
                Churn = 120
            };

            // Non-zero max values should work as expected
            double score = ScoringUtils.CalculateDebtVolatility(item, 200.0, 100.0);
            Assert.True(score > 0.0);

            // Zero or negative maxChurn or maxNetLines should return 0.0
            Assert.Equal(0.0, ScoringUtils.CalculateDebtVolatility(item, 0.0, 100.0));
            Assert.Equal(0.0, ScoringUtils.CalculateDebtVolatility(item, 200.0, 0.0));
            Assert.Equal(0.0, ScoringUtils.CalculateDebtVolatility(item, -10.0, 100.0));
            Assert.Equal(0.0, ScoringUtils.CalculateDebtVolatility(item, 200.0, -5.0));
        }

        [Fact]
        public void TestScoringUtilityService_DirectInstanceMethods()
        {
            IScoringUtilityService service = new ScoringUtilityService();
            var item = new ItemAccumulator
            {
                Added = 100,
                Deleted = 20,
                Churn = 120
            };

            double instanceDebtVolatility = service.CalculateDebtVolatility(item, 200.0, 100.0);
            double staticDebtVolatility = ScoringUtils.CalculateDebtVolatility(item, 200.0, 100.0);
            Assert.Equal(staticDebtVolatility, instanceDebtVolatility);
            Assert.True(instanceDebtVolatility > 0.0);

            var contributors = new List<ContributorShare>
            {
                new ContributorShare { Name = "Alice", ActivityShare = 0.6 },
                new ContributorShare { Name = "Bob", ActivityShare = 0.4 }
            };
            double instanceOverlap = service.CalculateCoordinationOverlap(contributors, 5);
            double staticOverlap = ScoringUtils.CalculateCoordinationOverlap(contributors, 5);
            Assert.Equal(staticOverlap, instanceOverlap);

            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            double instanceRecency = service.CalculateRecencyScore(nowMs);
            double staticRecency = ScoringUtils.CalculateRecencyScore(nowMs);
            Assert.Equal(staticRecency, instanceRecency, 4);
        }

        [Fact]
        public void TestScoreBreakdown_Clone()
        {
            var breakdown = new ScoreBreakdown
            {
                Touches = 1.2,
                Churn = 3.4,
                Recency = 5.6,
                ContributorSpread = 7.8,
                LowFamiliarityConcentration = 9.0
            };

            var clone = breakdown.Clone();

            Assert.NotSame(breakdown, clone);
            Assert.Equal(breakdown.Touches, clone.Touches);
            Assert.Equal(breakdown.Churn, clone.Churn);
            Assert.Equal(breakdown.Recency, clone.Recency);
            Assert.Equal(breakdown.ContributorSpread, clone.ContributorSpread);
            Assert.Equal(breakdown.LowFamiliarityConcentration, clone.LowFamiliarityConcentration);
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
        public void TestWarningCollection_ModernRules()
        {
            // EmailCollisionWarnings
            var collisions = new List<EmailCollision>
            {
                new() { Email = "test@example.com", Names = new List<string> { "Alice", "Bob" } }
            };
            var rule1 = new EmailCollisionWarningRule();
            var warnings1 = rule1.Collect(new WarningContext { EmailCollisions = collisions, AliasCount = 0 });
            Assert.Single(warnings1);
            Assert.Contains("Contributors Alice, Bob share email test@example.com", warnings1[0]);

            var warnings1Empty = rule1.Collect(new WarningContext { EmailCollisions = collisions, AliasCount = 1 });
            Assert.Empty(warnings1Empty);

            var warnings1Null = rule1.Collect(new WarningContext { EmailCollisions = null, AliasCount = 0 });
            Assert.Empty(warnings1Null);

            // BotConfigWarning
            var automationMetrics = new List<AutomationMetric>
            {
                new() { Name = "bot", Email = "bot@example.com", TotalActivity = 10, Areas = new() }
            };
            var rule2 = new BotConfigWarningRule();
            var warnings2 = rule2.Collect(new WarningContext { ConfiguredBotCount = 0, AutomationMetrics = automationMetrics });
            Assert.Single(warnings2);
            Assert.Contains("No bots are explicitly configured", warnings2[0]);

            var warnings2Empty = rule2.Collect(new WarningContext { ConfiguredBotCount = 1, AutomationMetrics = automationMetrics });
            Assert.Empty(warnings2Empty);

            var warnings2Null = rule2.Collect(new WarningContext { ConfiguredBotCount = 0, AutomationMetrics = null });
            Assert.Empty(warnings2Null);

            // LeadTimeWarning
            var rule3 = new LeadTimeWarningRule();
            var warnings3Null = rule3.Collect(new WarningContext { LeadTimes = null });
            Assert.Single(warnings3Null);
            Assert.Contains("No merge commits in the analysis window", warnings3Null[0]);

            var warnings3EmptyList = rule3.Collect(new WarningContext { LeadTimes = new LeadTimesInfo { Merges = new() } });
            Assert.Single(warnings3EmptyList);

            var warnings3HasMerges = rule3.Collect(new WarningContext
            {
                LeadTimes = new LeadTimesInfo
                {
                    Merges = new List<MergeLeadTimeRecord> { new() { LeadTimeHours = 1 } }
                }
            });
            Assert.Empty(warnings3HasMerges);

            // NoBotsWarning
            var rule4 = new NoBotsWarningRule();
            var warnings4 = rule4.Collect(new WarningContext { ConfiguredBotCount = 0, AutomationMetrics = new() });
            Assert.Single(warnings4);
            Assert.Contains("No bots are configured and no automation identities were detected", warnings4[0]);

            var warnings4HasConfigured = rule4.Collect(new WarningContext { ConfiguredBotCount = 1, AutomationMetrics = new() });
            Assert.Empty(warnings4HasConfigured);

            var warnings4HasDetected = rule4.Collect(new WarningContext { ConfiguredBotCount = 0, AutomationMetrics = automationMetrics });
            Assert.Empty(warnings4HasDetected);

            var warnings4Null = rule4.Collect(new WarningContext { ConfiguredBotCount = 0, AutomationMetrics = null });
            Assert.Single(warnings4Null);
            Assert.Contains("No bots are configured and no automation identities were detected", warnings4Null[0]);

            // TemporalCouplingWarning
            var rule5 = new TemporalCouplingWarningRule();
            var warnings5 = rule5.Collect(new WarningContext
            {
                TemporalCoupling = new TemporalCouplingResult
                {
                    OversizedCommitCount = 1,
                    Limit = 1,
                    MaxObservedFiles = 2
                }
            });
            Assert.Single(warnings5);
            Assert.Contains("1 commit(s) changed more than 1 files", warnings5[0]);

            var warnings5Empty = rule5.Collect(new WarningContext
            {
                TemporalCoupling = new TemporalCouplingResult
                {
                    OversizedCommitCount = 0,
                    Limit = 5,
                    MaxObservedFiles = 2
                }
            });
            Assert.Empty(warnings5Empty);
        }

        [Fact]
        public void TestWarningCollector_CollectAndAggregate()
        {
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
                TemporalCoupling = new TemporalCouplingResult
                {
                    OversizedCommitCount = 1,
                    Limit = 1,
                    MaxObservedFiles = 2
                }
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
            var collector = new WarningCollector(new MockWarningRuleProvider(new List<IWarningRule> { rule }));

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

        [Fact]
        public void TestDiagnostics_WarningRulesProduceStableDiagnostics()
        {
            // 1. EmailCollisionWarningRule
            var emailRule = new EmailCollisionWarningRule();
            var emailContext = new WarningContext
            {
                EmailCollisions = new List<EmailCollision>
                {
                    new() { Email = "test@example.com", Names = new List<string> { "Alice", "Bob" } }
                },
                AliasCount = 0
            };
            var emailDiags = emailRule.CollectDiagnostics(emailContext);
            Assert.Single(emailDiags);
            Assert.Equal("GITIC001", emailDiags[0].Code);
            Assert.Equal("Warning", emailDiags[0].Severity);
            Assert.Contains("share email test@example.com", emailDiags[0].Message);
            Assert.Contains("Add an alias in .gitic.yml", emailDiags[0].Hint);

            // 2. BotConfigWarningRule
            var botRule = new BotConfigWarningRule();
            var botContext = new WarningContext
            {
                ConfiguredBotCount = 0,
                AutomationMetrics = new List<AutomationMetric> { new() { Name = "bot", Email = "bot@ci.com", TotalActivity = 10 } }
            };
            var botDiags = botRule.CollectDiagnostics(botContext);
            Assert.Single(botDiags);
            Assert.Equal("GITIC002", botDiags[0].Code);
            Assert.Contains("No bots are explicitly configured", botDiags[0].Message);

            // 3. LeadTimeWarningRule
            var leadRule = new LeadTimeWarningRule();
            var leadContext = new WarningContext
            {
                LeadTimes = new LeadTimesInfo { Merges = new List<MergeLeadTimeRecord>() }
            };
            var leadDiags = leadRule.CollectDiagnostics(leadContext);
            Assert.Single(leadDiags);
            Assert.Equal("GITIC003", leadDiags[0].Code);
            Assert.Contains("No merge commits", leadDiags[0].Message);

            // 4. NoBotsWarningRule
            var noBotsRule = new NoBotsWarningRule();
            var noBotsContext = new WarningContext
            {
                ConfiguredBotCount = 0,
                AutomationMetrics = new List<AutomationMetric>()
            };
            var noBotsDiags = noBotsRule.CollectDiagnostics(noBotsContext);
            Assert.Single(noBotsDiags);
            Assert.Equal("GITIC004", noBotsDiags[0].Code);
            Assert.Contains("No bots are configured and no automation identities", noBotsDiags[0].Message);

            // 5. TemporalCouplingWarningRule
            var couplingRule = new TemporalCouplingWarningRule();
            var couplingContext = new WarningContext
            {
                TemporalCoupling = new TemporalCouplingResult
                {
                    OversizedCommitCount = 3,
                    Limit = 50,
                    MaxObservedFiles = 75
                }
            };
            var couplingDiags = couplingRule.CollectDiagnostics(couplingContext);
            Assert.Single(couplingDiags);
            Assert.Equal("GITIC005", couplingDiags[0].Code);
            Assert.Contains("3 commit(s) changed more than 50 files", couplingDiags[0].Message);

            // 6. GeneratedFileWarningRule
            var genRule = new GeneratedFileWarningRule();
            var genContext = new WarningContext
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
            var genDiags = genRule.CollectDiagnostics(genContext);
            Assert.Single(genDiags);
            Assert.Equal("GITIC006", genDiags[0].Code);
            Assert.Contains("single-touch high churn", genDiags[0].Message);
        }

        [Fact]
        public void TestWarningCollector_SeverityOrderingAndDeduplication()
        {
            var collector = new WarningCollector();
            var context = new WarningContext();
            var existing = new List<string>
            {
                "Path test.cs matched multiple configured areas (Area1, Area2); using Area1."
            };

            var diags = collector.CollectDiagnostics(context, existing);
            Assert.NotEmpty(diags);
            Assert.Contains(diags, d => d.Code == "GITIC007");

            var customCollector = new WarningCollector(new MockWarningRuleProvider(new List<IWarningRule>
            {
                new MockSeverityRule()
            }));

            var customDiags = customCollector.CollectDiagnostics(new WarningContext());
            Assert.Equal(3, customDiags.Count);
            Assert.Equal("Critical", customDiags[0].Severity);
            Assert.Equal("Warning", customDiags[1].Severity);
            Assert.Equal("Info", customDiags[2].Severity);
        }

        private class MockSeverityRule : IWarningRule
        {
            public List<string> Collect(WarningContext context) => new();
            public List<Diagnostic> CollectDiagnostics(WarningContext context)
            {
                return new List<Diagnostic>
                {
                    new Diagnostic { Code = "W100", Severity = "Info", Message = "Low priority Info" },
                    new Diagnostic { Code = "W200", Severity = "Critical", Message = "High priority Failure" },
                    new Diagnostic { Code = "W150", Severity = "Warning", Message = "Medium priority Warning" }
                };
            }
        }

        private class MockConsoleReporter : IConsoleReporter
        {
            public string Stdout { get; set; } = string.Empty;
            public string Stderr { get; set; } = string.Empty;

            public void Write(string message) => Stdout += message;
            public void WriteLine(string message) => Stdout += message + "\n";
            public void WriteError(string message) => Stderr += message;
            public void WriteErrorLine(string message) => Stderr += message + "\n";
        }

        private class TestStandardCommand : StandardRenderAnalysisCommand
        {
            public TestStandardCommand(ParsedArgs parsed) : base(parsed) { }
            protected override AnalysisCommand CommandType => AnalysisCommand.Hotspots;

            public Task<CliResult> TestProcessResultAsync(AnalysisResult result, IConsoleReporter reporter)
            {
                return ProcessResultAsync(result, reporter);
            }
        }

        [Fact]
        public async Task TestStreamRouting_And_QuietMode()
        {
            var settings = DefaultAnalysisSettings.Create();
            settings.Quiet = false;
            var parsed = new ParsedArgs { Settings = settings };
            var command = new TestStandardCommand(parsed);

            var result = new AnalysisResult
            {
                Analysis = new AnalysisMetadata { IncludedFileChangeCount = 1 },
                Diagnostics = new List<Diagnostic>
                {
                    new() { Code = "W1", Severity = "Critical", Message = "Critical Error!" },
                    new() { Code = "W2", Severity = "Warning", Message = "A warning." }
                }
            };

            var reporter = new MockConsoleReporter();
            await command.TestProcessResultAsync(result, reporter);

            Assert.NotEmpty(reporter.Stdout);

            Assert.Contains("[CRITICAL]", reporter.Stderr);
            Assert.Contains("  W1: Critical Error!", reporter.Stderr);
            Assert.Contains("[WARNING]", reporter.Stderr);
            Assert.Contains("  W2: A warning.", reporter.Stderr);

            var quietSettings = DefaultAnalysisSettings.Create();
            quietSettings.Quiet = true;
            var quietParsed = new ParsedArgs { Settings = quietSettings };
            var quietCommand = new TestStandardCommand(quietParsed);

            var quietReporter = new MockConsoleReporter();
            await quietCommand.TestProcessResultAsync(result, quietReporter);

            Assert.Contains("[CRITICAL]", quietReporter.Stderr);
            Assert.Contains("  W1: Critical Error!", quietReporter.Stderr);
            Assert.DoesNotContain("[WARNING]", quietReporter.Stderr);
            Assert.DoesNotContain("  W2: A warning.", quietReporter.Stderr);
        }

        [Fact]
        public async Task TestJSONCompatibility_AdditiveDiagnostics()
        {
            var settings = DefaultAnalysisSettings.Create();
            settings.Json = true;
            var parsed = new ParsedArgs { Settings = settings };
            var command = new TestStandardCommand(parsed);

            var result = new AnalysisResult
            {
                Analysis = new AnalysisMetadata { IncludedFileChangeCount = 1 },
                Warnings = new List<string> { "Old warning text" },
                Diagnostics = new List<Diagnostic>
                {
                    new() { Code = "W1", Severity = "Warning", Message = "Old warning text", Hint = "Do X" }
                }
            };

            var reporter = new MockConsoleReporter();
            await command.TestProcessResultAsync(result, reporter);

            string json = reporter.Stdout;
            Assert.Contains("\"schema_version\": \"1.1\"", json);
            Assert.Contains("\"warnings\":", json);
            Assert.Contains("Old warning text", json);
            Assert.Contains("\"diagnostics\":", json);
            Assert.Contains("\"code\": \"W1\"", json);
            Assert.Contains("\"hint\": \"Do X\"", json);

            Assert.Empty(reporter.Stderr);
        }

        private class MockWarningRuleProvider : IWarningRuleProvider
        {
            private readonly List<IWarningRule> _rules;
            public MockWarningRuleProvider(List<IWarningRule> rules) => _rules = rules;
            public List<IWarningRule> GetRules() => _rules;
        }

        private class MockWarningRule : IWarningRule
        {
            public List<string> Collect(WarningContext context)
            {
                return new List<string> { "Beta warning", "Alpha warning" };
            }

            public List<Diagnostic> CollectDiagnostics(WarningContext context)
            {
                return new List<Diagnostic>
                {
                    new Diagnostic { Code = "GITIC999", Severity = "Warning", Message = "Beta warning" },
                    new Diagnostic { Code = "GITIC999", Severity = "Warning", Message = "Alpha warning" }
                };
            }
        }

        [Fact]
        public async Task TestGitClient_WithMockExecutor_GetRepositoryRoot()
        {
            var mockExecutor = new MockGitExecutor();
            mockExecutor.Setup(new[] { "rev-parse", "--show-toplevel" }, "/path/to/repo\n");

            var client = new GitClient("/path/to/repo", mockExecutor);
            var root = await client.GetRepositoryRootAsync();

            Assert.Equal("/path/to/repo", root);
            Assert.Single(mockExecutor.Calls);
            Assert.Equal("rev-parse", mockExecutor.Calls[0][0]);
            Assert.Equal("--show-toplevel", mockExecutor.Calls[0][1]);
        }

        [Fact]
        public async Task TestGitClient_WithMockExecutor_ListHeadFiles()
        {
            var mockExecutor = new MockGitExecutor();
            mockExecutor.Setup(new[] { "ls-tree", "-r", "--name-only", "HEAD" }, "src/main.cs\npackage.json\n");

            var client = new GitClient("/path/to/repo", mockExecutor);
            var files = await client.ListHeadFilesAsync();

            Assert.Equal(2, files.Count);
            Assert.Contains("src/main.cs", files);
            Assert.Contains("package.json", files);
        }

        [Fact]
        public async Task TestGitClient_WithMockExecutor_ExtractHistory()
        {
            var mockExecutor = new MockGitExecutor();
            
            var options = new GitHistoryExtractorOptions
            {
                IncludeMerges = false,
                AllTime = false,
                Since = "2026-06-01T12:00:00Z"
            };

            string logOutput = $@"__GITIC_COMMIT__
hash1
2026-06-01T12:00:00Z
Author Name
author@email.com
parent_hash

This is a commit message.
__GITIC_NUMSTAT__
10	5	src/main.cs
";

            mockExecutor.Setup(new[]
            {
                "log",
                "--numstat",
                "-p",
                $"--format=format:{new GitParser(new GitPatchParser()).CommitMarker}%n%H%n%aI%n%an%n%ae%n%P%n%B%n{new GitParser(new GitPatchParser()).NumstatMarker}",
                "--no-merges",
                "--since=2026-06-01T12:00:00Z"
            }, logOutput);

            var client = new GitClient("/path/to/repo", mockExecutor);
            var records = await client.ExtractHistoryAsync(options);

            Assert.Single(records);
            var record = records[0];
            Assert.Equal("hash1", record.Hash);
            Assert.Equal("Author Name", record.Author.Name);
            Assert.Equal("author@email.com", record.Author.Email);
            Assert.Equal("This is a commit message.", record.Message);
            Assert.Single(record.Files);
            Assert.Equal("src/main.cs", record.Files[0].Path);
            }

            [Fact]
            public async Task TestRepositoryAnalyzer_WithFakeFileStatsProvider()
            {
                var fakeProvider = new FakeFileStatsProvider();
                fakeProvider.DummyResults["src/main.cs"] = new FileStatResult { Size = 1234, Width = 88, Lines = 99 };

                var input = new AnalyzeInput
                {
                    RepoRoot = "/fake/root",
                    Command = AnalysisCommand.Hotspots,
                    Settings = new AnalysisSettings { Depth = 1 },
                    FileStatsProvider = fakeProvider,
                    GitClient = new FakeGitClient()
                };

                var result = await RepositoryAnalyzer.AnalyzeRepositoryAsync(input);

                Assert.NotNull(result);
                if (result.Files.Any())
                {
                    var mainFile = result.Files.FirstOrDefault(f => f.Path == "src/main.cs");
                    if (mainFile != null)
                    {
                        Assert.Equal(1234, mainFile.Size);
                        Assert.Equal(88, mainFile.Width);
                        Assert.Equal(99, mainFile.Lines);
                    }

                    var otherFile = result.Files.FirstOrDefault(f => f.Path != "src/main.cs");
                    if (otherFile != null)
                    {
                        Assert.Equal(100, otherFile.Size);
                        Assert.Equal(10, otherFile.Width);
                        Assert.Equal(5, otherFile.Lines);
                    }
                }
            }

            [Fact]
            public async Task TestRepositoryAnalyzer()
            {
                var fakeProvider = new FakeFileStatsProvider();
                fakeProvider.DummyResults["src/main.cs"] = new FileStatResult { Size = 1234, Width = 88, Lines = 99 };

                var input = new AnalyzeInput
                {
                    RepoRoot = "/fake/root",
                    Command = AnalysisCommand.Hotspots,
                    Settings = new AnalysisSettings { Depth = 1 },
                    FileStatsProvider = fakeProvider,
                    GitClient = new FakeGitClient()
                };

                var result = await RepositoryAnalyzer.AnalyzeRepositoryAsync(input);

                Assert.NotNull(result);
                Assert.Equal("/fake/root", result.Analysis.RepoRoot);
            }

            [Fact]
            public async Task TestRepositoryAnalyzer_InterfaceUsage()
            {
                var fakeProvider = new FakeFileStatsProvider();
                fakeProvider.DummyResults["src/main.cs"] = new FileStatResult { Size = 1234, Width = 88, Lines = 99 };

                var input = new AnalyzeInput
                {
                    RepoRoot = "/fake/root",
                    Command = AnalysisCommand.Hotspots,
                    Settings = new AnalysisSettings { Depth = 1 },
                    FileStatsProvider = fakeProvider,
                    GitClient = new FakeGitClient()
                };

                IRepositoryAnalyzer analyzer = new RepositoryAnalyzer();
                var result = await analyzer.AnalyzeAsync(input);

                Assert.NotNull(result);
                Assert.Equal("/fake/root", result.Analysis.RepoRoot);
            }

            private class MockConfigurationEngine : IConfigurationEngine
            {
                public bool LoadAndResolveCalled { get; set; }
                public bool RenderStarterConfigCalled { get; set; }

                public string RenderStarterConfig()
                {
                    RenderStarterConfigCalled = true;
                    return "";
                }

                public Task<ResolvedConfiguration> LoadAndResolveAsync(AnalyzeInput input, LoadGiticConfigOptions? options = null)
                {
                    LoadAndResolveCalled = true;
                    return Task.FromResult(new ResolvedConfiguration
                    {
                        Settings = new AnalysisSettings(),
                        Config = GiticConfig.Default
                    });
                }
            }

            [Fact]
            public async Task TestRepositoryAnalyzer_WithMockConfigurationEngine()
            {
                var fakeProvider = new FakeFileStatsProvider();
                var mockConfigEngine = new MockConfigurationEngine();

                var input = new AnalyzeInput
                {
                    RepoRoot = "/fake/root",
                    Command = AnalysisCommand.Hotspots,
                    Settings = new AnalysisSettings { Depth = 1 },
                    FileStatsProvider = fakeProvider,
                    GitClient = new FakeGitClient()
                };

                var analyzer = new RepositoryAnalyzer(mockConfigEngine);
                var result = await analyzer.AnalyzeAsync(input);

                Assert.NotNull(result);
                Assert.True(mockConfigEngine.LoadAndResolveCalled);
            }

            private class MockAnalysisPipeline : IAnalysisPipeline
            {
                public bool RunCalled { get; set; }
                public List<GitCommitRecord>? ReceivedCommits { get; set; }
                public HashSet<string>? ReceivedHeadFiles { get; set; }

                public AnalysisPipelineResult Run(
                    List<GitCommitRecord> commits,
                    HashSet<string> headFiles,
                    GiticConfig config,
                    AnalysisSettings settings,
                    AnalysisCommand command)
                {
                    RunCalled = true;
                    ReceivedCommits = commits;
                    ReceivedHeadFiles = headFiles;

                    return new AnalysisPipelineResult
                    {
                        Files = new List<FileMetric> { new FileMetric { Path = "src/main.cs" } },
                        Warnings = new List<string> { "mock-pipeline-warning" },
                        IncludedFileChangeCount = 42
                    };
                }
            }

            [Fact]
            public async Task TestRepositoryAnalyzer_WithMockAnalysisPipeline()
            {
                var fakeProvider = new FakeFileStatsProvider();
                var mockPipeline = new MockAnalysisPipeline();

                var input = new AnalyzeInput
                {
                    RepoRoot = "/fake/root",
                    Command = AnalysisCommand.Hotspots,
                    Settings = new AnalysisSettings { Depth = 1 },
                    FileStatsProvider = fakeProvider,
                    GitClient = new FakeGitClient()
                };

                var analyzer = new RepositoryAnalyzer(pipeline: mockPipeline);
                var result = await analyzer.AnalyzeAsync(input);

                Assert.NotNull(result);
                Assert.True(mockPipeline.RunCalled);
                Assert.NotNull(mockPipeline.ReceivedCommits);
                Assert.Single(mockPipeline.ReceivedCommits);
                Assert.Equal("hash1", mockPipeline.ReceivedCommits[0].Hash);
                Assert.Contains("mock-pipeline-warning", result.Warnings);
                Assert.Equal(42, result.Analysis.IncludedFileChangeCount);
            }

            private class MockResultAnonymizer : IResultAnonymizer
            {
                public bool AnonymizeCalled { get; set; }
                public AnalysisResult Anonymize(AnalysisResult result)
                {
                    AnonymizeCalled = true;
                    return result;
                }
            }

            [Fact]
            public async Task TestRepositoryAnalyzer_WithResultAnonymizer()
            {
                var fakeProvider = new FakeFileStatsProvider();
                var mockAnonymizer = new MockResultAnonymizer();

                var input = new AnalyzeInput
                {
                    RepoRoot = "/fake/root",
                    Command = AnalysisCommand.Hotspots,
                    Settings = new AnalysisSettings { Depth = 1, Anonymize = true },
                    FileStatsProvider = fakeProvider,
                    GitClient = new FakeGitClient()
                };

                var analyzer = new RepositoryAnalyzer(anonymizer: mockAnonymizer);
                var result = await analyzer.AnalyzeAsync(input);

                Assert.NotNull(result);
                Assert.True(mockAnonymizer.AnonymizeCalled);

                // Now test when Anonymize is false
                var mockAnonymizerFalse = new MockResultAnonymizer();
                var inputFalse = new AnalyzeInput
                {
                    RepoRoot = "/fake/root",
                    Command = AnalysisCommand.Hotspots,
                    Settings = new AnalysisSettings { Depth = 1, Anonymize = false },
                    FileStatsProvider = fakeProvider,
                    GitClient = new FakeGitClient()
                };

                var analyzerFalse = new RepositoryAnalyzer(anonymizer: mockAnonymizerFalse);
                var resultFalse = await analyzerFalse.AnalyzeAsync(inputFalse);

                Assert.NotNull(resultFalse);
                Assert.False(mockAnonymizerFalse.AnonymizeCalled);
            }

            private class MockChangeAccumulator : IChangeAccumulator
            {
                public bool PrepareIdentityMergingCalled { get; set; }
                public bool AddCommitCalled { get; set; }
                public bool GetFilesCalled { get; set; }
                public bool GetAreasCalled { get; set; }
                public bool GetContributorsCalled { get; set; }
                public bool GetAutomationCalled { get; set; }

                public void PrepareIdentityMerging(List<GitCommitRecord> commits) => PrepareIdentityMergingCalled = true;
                public List<EmailCollision> GetEmailCollisions() => new();
                public void AddCommit(GitCommitRecord commit, List<string> filesInCommit) => AddCommitCalled = true;
                public IReadOnlyDictionary<string, ItemAccumulator> GetFiles() { GetFilesCalled = true; return new Dictionary<string, ItemAccumulator>(); }
                public IReadOnlyDictionary<string, ItemAccumulator> GetAreas() { GetAreasCalled = true; return new Dictionary<string, ItemAccumulator>(); }
                public IReadOnlyDictionary<string, ContributorAccumulator> GetContributors() { GetContributorsCalled = true; return new Dictionary<string, ContributorAccumulator>(); }
                public IReadOnlyDictionary<string, ContributorAccumulator> GetAutomation() { GetAutomationCalled = true; return new Dictionary<string, ContributorAccumulator>(); }
                public List<ExclusionSummary> GetExclusions() => new();
                public HashSet<string> GetWarnings() => new();
                public int GetIncludedFileChangeCount() => 0;
            }

            [Fact]
            public void TestAnalysisPipeline_UsesInjectedAccumulator()
            {
                var commits = new List<GitCommitRecord>
                {
                    new GitCommitRecord
                    {
                        Hash = "123456",
                        Author = new GitIdentity { Name = "John", Email = "john@example.com" },
                        Files = new List<GitFileChange>()
                    }
                };
                var headFiles = new HashSet<string>();
                var config = GiticConfig.Default;
                var settings = new AnalysisSettings();
                var command = AnalysisCommand.Hotspots;

                var mockAccumulator = new MockChangeAccumulator();
                var pipeline = new AnalysisPipeline(accumulator: mockAccumulator);

                var result = pipeline.Run(
                    commits,
                    headFiles,
                    config,
                    settings,
                    command
                );

                Assert.NotNull(result);
                Assert.True(mockAccumulator.PrepareIdentityMergingCalled);
                Assert.True(mockAccumulator.AddCommitCalled);
                Assert.True(mockAccumulator.GetFilesCalled);
                Assert.True(mockAccumulator.GetAreasCalled);
                Assert.True(mockAccumulator.GetContributorsCalled);
                Assert.True(mockAccumulator.GetAutomationCalled);
            }


            [Fact]
            public void TestAnalysisSettingsNormalizer_DefaultNormalization()
            {
                var normalizer = new AnalysisSettingsNormalizer();
                var original = new AnalysisSettings();
                var normalized = normalizer.Normalize(original);

                var defaults = DefaultAnalysisSettings.Create();
                Assert.Equal(defaults.Since, normalized.Since);
                Assert.Equal(defaults.Depth, normalized.Depth);
                Assert.Equal(defaults.MergeByEmail, normalized.MergeByEmail);
                Assert.Equal(defaults.Path, normalized.Path);
            }

            [Fact]
            public void TestAnalysisSettingsNormalizer_PreservesValues()
            {
                var normalizer = new AnalysisSettingsNormalizer();
                var original = new AnalysisSettings
                {
                    Depth = 5,
                    Since = "2 weeks ago",
                    Json = true,
                    AllTime = true,
                    IncludeMerges = true,
                    IncludeDeleted = true,
                    MergeByEmail = true,
                    Path = "custom/path",
                    Anonymize = true
                };
                var normalized = normalizer.Normalize(original);

                Assert.Equal(5, normalized.Depth);
                Assert.Equal("2 weeks ago", normalized.Since);
                Assert.True(normalized.Json);
                Assert.True(normalized.AllTime);
                Assert.True(normalized.IncludeMerges);
                Assert.True(normalized.IncludeDeleted);
                Assert.True(normalized.MergeByEmail);
                Assert.Equal("custom/path", normalized.Path);
                Assert.True(normalized.Anonymize);
            }

            [Fact]
            public void TestAnalysisSettings_Clone_ReturnsCorrectCopy()
            {
                var original = new AnalysisSettings
                {
                    Depth = 5,
                    Since = "2 weeks ago",
                    Json = true,
                    AllTime = true,
                    IncludeMerges = true,
                    IncludeDeleted = true,
                    MergeByEmail = true,
                    Path = "custom/path",
                    Anonymize = true
                };

                var cloned = original.Clone();

                Assert.NotSame(original, cloned);
                Assert.Equal(original.Depth, cloned.Depth);
                Assert.Equal(original.Since, cloned.Since);
                Assert.Equal(original.Json, cloned.Json);
                Assert.Equal(original.AllTime, cloned.AllTime);
                Assert.Equal(original.IncludeMerges, cloned.IncludeMerges);
                Assert.Equal(original.IncludeDeleted, cloned.IncludeDeleted);
                Assert.Equal(original.MergeByEmail, cloned.MergeByEmail);
                Assert.Equal(original.Path, cloned.Path);
                Assert.Equal(original.Anonymize, cloned.Anonymize);
            }


            [Fact]
            public async Task TestDiskFileStatsProvider_ComputesStats()
            {
                var provider = new DiskFileStatsProvider();
                string tempFile = Path.GetTempFileName();
                try
                {
                    await File.WriteAllTextAsync(tempFile, "line 1\nline 2\nlongest line here");
                    var relativePath = Path.GetFileName(tempFile);
                    var repoRoot = Path.GetDirectoryName(tempFile)!;

                    var stats = await provider.ComputeFileStatsAsync(repoRoot, new List<string> { relativePath });
                    Assert.NotNull(stats);
                    Assert.True(stats.ContainsKey(relativePath));
                    var fileStat = stats[relativePath];
                    Assert.True(fileStat.Size > 0);
                    Assert.Equal(3, fileStat.Lines);
                    Assert.Equal("longest line here".Length, fileStat.Width);
                }
                finally
                {
                    if (File.Exists(tempFile))
                    {
                        File.Delete(tempFile);
                    }
                }
            }

            [Fact]
            public async Task TestDiskFileStatsProvider_EdgeCases()
            {
                var provider = new DiskFileStatsProvider();
                string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);

                try
                {
                    // 1. Empty file
                    string emptyFile = Path.Combine(tempDir, "empty.txt");
                    await File.WriteAllBytesAsync(emptyFile, Array.Empty<byte>());

                    // 2. File with trailing newline
                    string trailingNewlineFile = Path.Combine(tempDir, "trailing.txt");
                    await File.WriteAllTextAsync(trailingNewlineFile, "line 1\nline 2\n");

                    // 3. File with CR LF and trailing newline
                    string crlfFile = Path.Combine(tempDir, "crlf.txt");
                    await File.WriteAllTextAsync(crlfFile, "line 1\r\nline 2\r\n");

                    // 4. Binary file
                    string binaryFile = Path.Combine(tempDir, "binary.bin");
                    await File.WriteAllBytesAsync(binaryFile, new byte[] { 1, 2, 0, 4, 5 });

                    // Compute stats
                    var files = new List<string> { "empty.txt", "trailing.txt", "crlf.txt", "binary.bin", "nonexistent.txt" };
                    var stats = await provider.ComputeFileStatsAsync(tempDir, files);

                    Assert.NotNull(stats);

                    // Assert empty file
                    Assert.True(stats.ContainsKey("empty.txt"));
                    Assert.Equal(0, stats["empty.txt"].Size);
                    Assert.Equal(1, stats["empty.txt"].Lines); // matching split behavior
                    Assert.Equal(0, stats["empty.txt"].Width);

                    // Assert trailing newline file
                    Assert.True(stats.ContainsKey("trailing.txt"));
                    Assert.Equal(3, stats["trailing.txt"].Lines); // "line 1", "line 2", ""
                    Assert.Equal(6, stats["trailing.txt"].Width);

                    // Assert CRLF trailing newline file
                    Assert.True(stats.ContainsKey("crlf.txt"));
                    Assert.Equal(3, stats["crlf.txt"].Lines); // "line 1", "line 2", ""
                    Assert.Equal(6, stats["crlf.txt"].Width);

                    // Assert binary file
                    Assert.True(stats.ContainsKey("binary.bin"));
                    Assert.Equal(5, stats["binary.bin"].Size);
                    Assert.Equal(0, stats["binary.bin"].Lines);
                    Assert.Equal(0, stats["binary.bin"].Width);

                    // Assert nonexistent file
                    Assert.True(stats.ContainsKey("nonexistent.txt"));
                    Assert.Equal(0, stats["nonexistent.txt"].Size);
                    Assert.Equal(0, stats["nonexistent.txt"].Lines);
                    Assert.Equal(0, stats["nonexistent.txt"].Width);
                }
                finally
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
            }

            private class FakeFileStatsProvider : IFileStatsProvider
            {
                public Dictionary<string, FileStatResult> DummyResults { get; set; } = new();

                public Task<Dictionary<string, FileStatResult>> ComputeFileStatsAsync(
                    string repoRoot,
                    List<string> files,
                    int concurrency = 20)
                {
                    var results = new Dictionary<string, FileStatResult>();
                    foreach (var file in files)
                    {
                        if (DummyResults.TryGetValue(file, out var stats))
                        {
                            results[file] = stats;
                        }
                        else
                        {
                            results[file] = new FileStatResult { Size = 100, Width = 10, Lines = 5 };
                        }
                    }
                    return Task.FromResult(results);
                }

                public async Task<List<FileMetric>> EnrichFileMetricsAsync(
                    string repoRoot,
                    List<FileMetric> metrics,
                    int concurrency = 20)
                {
                    var files = metrics.Select(m => m.Path).ToList();
                    var fileStats = await ComputeFileStatsAsync(repoRoot, files, concurrency);
                    foreach (var f in metrics)
                    {
                        if (fileStats.TryGetValue(f.Path, out var stats))
                        {
                            f.Size = stats.Size;
                            f.Width = stats.Width;
                            f.Lines = stats.Lines;
                        }
                        else
                        {
                            f.Size = 0;
                            f.Width = 0;
                            f.Lines = 0;
                        }
                    }
                    return metrics;
                }
            }

            private class FakeGitClient : IGitClient
            {
                public Task<string?> GetRepositoryRootAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>("/fake/root");
                public Task<HashSet<string>> ListHeadFilesAsync(CancellationToken cancellationToken = default) => Task.FromResult(new HashSet<string> { "src/main.cs" });
                public Task<List<GitCommitRecord>> ExtractHistoryAsync(GitHistoryExtractorOptions? options = null, CancellationToken cancellationToken = default)
                {
                    return Task.FromResult(new List<GitCommitRecord>
                    {
                        new GitCommitRecord
                        {
                            Hash = "hash1",
                            Author = new GitIdentity { Name = "Author", Email = "author@email.com" },
                            Date = "2026-07-25T12:00:00Z",
                            Timestamp = 1785000000000,
                            Message = "feat: add main file",
                            Files = new List<GitFileChange>
                            {
                                new GitFileChange { Path = "src/main.cs", Added = 10, Deleted = 5 }
                            }
                        }
                    });
                }
            }

            [Fact]
            public void TestCustomCommitClassifierStrategies()
            {
                // Test 1: Verify ClassificationRule with custom patterns
                var customBugfixRule = new ClassificationRule(
                    "bugfix",
                    @"(?:defect|patch|hotfix)",
                    @"^hotfix(?:\(.+\))?:"
                );

                // Should match custom patterns
                Assert.True(customBugfixRule.Matches("defect: fixed issue in login"));
                Assert.True(customBugfixRule.Matches("hotfix(auth): fix credentials leak"));

                // Should NOT match original/default patterns unless they happen to match custom patterns
                Assert.False(customBugfixRule.Matches("revert accidental commit")); // default bugfix has "revert", custom does not
                Assert.False(customBugfixRule.Matches("fix: resolve crash")); // default bugfix prefix has "fix", custom does not

                // Test 2: Verify ClassificationRule with custom patterns
                var customFeatureRule = new ClassificationRule(
                    "feature",
                    @"(?:new-feature|impl|create)",
                    @"^new(?:\(.+\))?:"
                );

                // Should match custom patterns
                Assert.True(customFeatureRule.Matches("create database index"));
                Assert.True(customFeatureRule.Matches("new(db): add migrations"));

                // Should NOT match original/default patterns unless they happen to match custom patterns
                Assert.False(customFeatureRule.Matches("feat: refactor login")); // default feature prefix has "feat", custom does not

                // Test 3: Verify they work via CommitClassifier when injected
                var customRules = new List<ClassificationRule> { customBugfixRule, customFeatureRule };
                ICommitClassifier classifier = new CommitClassifier(customRules);

                Assert.Equal("bugfix", classifier.Classify("defect: fixed issue in login"));
                Assert.Equal("feature", classifier.Classify("create database index"));
                Assert.Equal("other", classifier.Classify("feat: refactor login"));

                // Test 4: Verify default/omitted constructor parameters retain standard behavior
                var defaultClassifier = new CommitClassifier();
                Assert.Equal("bugfix", defaultClassifier.Classify("fix: resolve crash"));
                Assert.Equal("feature", defaultClassifier.Classify("feat: implement new auth flow"));
            }

            [Fact]
            public void TestResultAnonymizerInterface()
            {
                var original = new AnalysisResult();
                IResultAnonymizer anonymizer = new ResultAnonymizer();
                var result = anonymizer.Anonymize(original);
                Assert.NotNull(result);

                IResultAnonymizer noOpAnonymizer = new NoOpResultAnonymizer();
                var unchanged = noOpAnonymizer.Anonymize(original);
                Assert.Same(original, unchanged);
            }

            [Fact]
            public void TestResultAnonymizerConsolidatedBehavior()
            {
                var original = new AnalysisResult
                {
                    Contributors = new List<ContributorMetric>
                    {
                        new ContributorMetric { Name = "Alice Smith", Email = "alice@example.com", TotalActivity = 10, Areas = new List<ContributorAreaMetric>() },
                        new ContributorMetric { Name = "Bob Jones", Email = "bob@example.com", TotalActivity = 5, Areas = new List<ContributorAreaMetric>() },
                        new ContributorMetric { Name = "Alice Smith", Email = "alice@example.com", TotalActivity = 12, Areas = new List<ContributorAreaMetric>() }
                    },
                    Automation = new List<AutomationMetric>
                    {
                        new AutomationMetric { Name = "BuildBot", Email = "bot@example.com", TotalActivity = 100, Areas = new List<ContributorAreaMetric>() }
                    }
                };

                var anonymizer = new ResultAnonymizer();
                var result = anonymizer.Anonymize(original);

                Assert.NotNull(result);
                Assert.Equal(3, result.Contributors.Count);
                Assert.Single(result.Automation);

                // Alice Smith (first unique human) -> Contributor 1
                Assert.Equal("Contributor 1", result.Contributors[0].Name);
                Assert.Equal("contributor-1@anonymous.local", result.Contributors[0].Email);

                // Bob Jones (second unique human) -> Contributor 2
                Assert.Equal("Contributor 2", result.Contributors[1].Name);
                Assert.Equal("contributor-2@anonymous.local", result.Contributors[1].Email);

                // Same Alice Smith (repeats key) -> Contributor 1
                Assert.Equal("Contributor 1", result.Contributors[2].Name);
                Assert.Equal("contributor-1@anonymous.local", result.Contributors[2].Email);

                // BuildBot -> Automation 1
                Assert.Equal("Automation 1", result.Automation[0].Name);
                Assert.Equal("automation-1@anonymous.local", result.Automation[0].Email);
            }

            [Fact]
            public void TestResultAnonymizerIsStatelessAndThreadSafe()
            {
                var original1 = new AnalysisResult
                {
                    Contributors = new List<ContributorMetric>
                    {
                        new ContributorMetric { Name = "Alice Smith", Email = "alice@example.com", TotalActivity = 10, Areas = new List<ContributorAreaMetric>() }
                    },
                    Automation = new List<AutomationMetric>
                    {
                        new AutomationMetric { Name = "BuildBot", Email = "bot@example.com", TotalActivity = 100, Areas = new List<ContributorAreaMetric>() }
                    }
                };

                var original2 = new AnalysisResult
                {
                    Contributors = new List<ContributorMetric>
                    {
                        new ContributorMetric { Name = "Charlie Brown", Email = "charlie@example.com", TotalActivity = 10, Areas = new List<ContributorAreaMetric>() }
                    },
                    Automation = new List<AutomationMetric>
                    {
                        new AutomationMetric { Name = "DeployBot", Email = "deploy@example.com", TotalActivity = 100, Areas = new List<ContributorAreaMetric>() }
                    }
                };

                var anonymizer = new ResultAnonymizer();

                // Run anonymizer on the first result
                var result1 = anonymizer.Anonymize(original1);
                Assert.Equal("Contributor 1", result1.Contributors[0].Name);
                Assert.Equal("contributor-1@anonymous.local", result1.Contributors[0].Email);
                Assert.Equal("Automation 1", result1.Automation[0].Name);
                Assert.Equal("automation-1@anonymous.local", result1.Automation[0].Email);

                // Run anonymizer on the second result, using the exact SAME anonymizer instance.
                // Since the anonymizer is stateless, it must reset mappings for this new run and map to Contributor 1 and Automation 1 again.
                var result2 = anonymizer.Anonymize(original2);
                Assert.Equal("Contributor 1", result2.Contributors[0].Name);
                Assert.Equal("contributor-1@anonymous.local", result2.Contributors[0].Email);
                Assert.Equal("Automation 1", result2.Automation[0].Name);
                Assert.Equal("automation-1@anonymous.local", result2.Automation[0].Email);
            }

            [Fact]
            public void TestICommandLineParser_InterfaceAndImplementation()
            {
                string[] args = new[] { "hotspots", "--json", "--since", "2026-01-01" };
                ICommandLineParser parser = new CommandLineParser(args);
                ParsedArgs parsed = parser.Parse();

                Assert.Equal("hotspots", parsed.Command);
                Assert.True(parsed.Settings.Json);
                Assert.Equal("2026-01-01", parsed.Settings.Since);
            }

            [Fact]
            public void TestIYamlParserAndYamlSubsetParserImpl()
            {
                IYamlParser parser = new YamlSubsetParserImpl();
                string yaml = "key: value\nnumber: 42\nbool: true";
                var parsed = parser.Parse(yaml, "test_source") as Dictionary<string, object?>;
                Assert.NotNull(parsed);
                Assert.Equal("value", parsed["key"]);
                Assert.Equal(42L, parsed["number"]);
                Assert.Equal(true, parsed["bool"]);
            }

            private class MockYamlTokenizer : IYamlTokenizer
            {
                public List<YamlLine> Tokenize(string content)
                {
                    return new List<YamlLine>
                    {
                        new YamlLine { Indent = 0, Text = "mockKey: mockValue", LineNumber = 1 }
                    };
                }
            }

            [Fact]
            public void TestYamlSubsetParser_WithMockTokenizer()
            {
                var mockTokenizer = new MockYamlTokenizer();
                var parser = new YamlSubsetParser("ignored content", "test_source", mockTokenizer);
                var parsed = parser.Parse() as Dictionary<string, object?>;
                Assert.NotNull(parsed);
                Assert.Equal("mockValue", parsed["mockKey"]);
            }

            private class NoOpResultAnonymizer : IResultAnonymizer
            {
                public AnalysisResult Anonymize(AnalysisResult result)
                {
                    return result;
                }
            }

            [Fact]
            public void TestIFamiliarityScoringEngine_InterfaceAndImplementation()
            {
                var config = new GiticConfig();
                IFamiliarityScoringEngine engine = new FamiliarityScoringEngine(config);
                var items = new List<ItemAccumulator>();
                var files = engine.ScoreFiles(items, 2);
                var areas = engine.ScoreAreas(items);

                Assert.NotNull(files);
                Assert.Empty(files);
                Assert.NotNull(areas);
                Assert.Empty(areas);
            }

            private class MockKnowledgeSiloCalculator : IKnowledgeSiloCalculator
            {
                public bool Called { get; private set; }
                public List<ContributorShare>? PassedContributors { get; private set; }
                public HashSet<string>? PassedActiveContributorKeys { get; private set; }

                public KnowledgeSiloMetric CalculateKnowledgeSilo(
                    List<ContributorShare> contributors,
                    HashSet<string> activeContributorKeys)
                {
                    Called = true;
                    PassedContributors = contributors;
                    PassedActiveContributorKeys = activeContributorKeys;
                    return new KnowledgeSiloMetric
                    {
                        TruckFactor = 99,
                        TopOwnerShare = 0.99,
                        IsSilo = true,
                        Abandoned = true
                    };
                }
            }

            [Fact]
            public void TestFamiliarityScoringEngine_UsesInjectedKnowledgeSiloCalculator()
            {
                var config = new GiticConfig();
                var mockCalculator = new MockKnowledgeSiloCalculator();
                var activeKeys = new HashSet<string> { "active" };
                IFamiliarityScoringEngine engine = new FamiliarityScoringEngine(
                    config,
                    activeKeys,
                    depth: 2,
                    siloCalculator: mockCalculator);

                var items = new List<ItemAccumulator>
                {
                    new ItemAccumulator
                    {
                        Key = "test.txt",
                        Touches = 1,
                        Added = 10,
                        Deleted = 5,
                        Churn = 15,
                        LastTouched = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        ContributorCredits = new Dictionary<string, ContributorCredit>
                        {
                            { "contributor1", new ContributorCredit { Identity = new GitIdentity { Name = "User1", Email = "user1@example.com" }, Activity = 10.0 } }
                        }
                    }
                };

                var files = engine.ScoreFiles(items, 2);

                Assert.True(mockCalculator.Called);
                Assert.NotNull(mockCalculator.PassedContributors);
                Assert.Single(mockCalculator.PassedContributors);
                Assert.Equal("User1", mockCalculator.PassedContributors[0].Name);
                Assert.Same(activeKeys, mockCalculator.PassedActiveContributorKeys);

                Assert.Single(files);
                Assert.NotNull(files[0].KnowledgeSilo);
                Assert.Equal(99, files[0].KnowledgeSilo!.TruckFactor);
                Assert.Equal(0.99, files[0].KnowledgeSilo!.TopOwnerShare);
                Assert.True(files[0].KnowledgeSilo!.IsSilo);
                Assert.True(files[0].KnowledgeSilo!.Abandoned);
            }

            private class MockScoringUtilityService : IScoringUtilityService
            {
                public bool CalculateRecencyScoreCalled { get; set; }
                public bool CalculateDebtVolatilityCalled { get; set; }
                public bool CalculateCoordinationOverlapCalled { get; set; }

                public double CalculateRecencyScore(long timestamp)
                {
                    CalculateRecencyScoreCalled = true;
                    return 0.5;
                }

                public double CalculateDebtVolatility(ItemAccumulator item, double maxChurn, double maxNetLines)
                {
                    CalculateDebtVolatilityCalled = true;
                    return 42.0;
                }

                public double CalculateCoordinationOverlap(List<ContributorShare> contributors, int itemTouches)
                {
                    CalculateCoordinationOverlapCalled = true;
                    return 77.0;
                }
            }

            [Fact]
            public void TestFamiliarityScoringEngine_UsesInjectedScoringUtilityService()
            {
                var config = new GiticConfig();
                var mockUtility = new MockScoringUtilityService();
                IFamiliarityScoringEngine engine = new FamiliarityScoringEngine(
                    config,
                    scoringUtilityService: mockUtility);

                var items = new List<ItemAccumulator>
                {
                    new ItemAccumulator
                    {
                        Key = "test.txt",
                        Touches = 1,
                        Added = 10,
                        Deleted = 5,
                        Churn = 15,
                        LastTouched = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        ContributorCredits = new Dictionary<string, ContributorCredit>
                        {
                            { "author", new ContributorCredit { Identity = new GitIdentity { Name = "author", Email = "author@example.com" }, Activity = 10.0 } }
                        }
                    }
                };

                var files = engine.ScoreFiles(items, 2);

                Assert.True(mockUtility.CalculateRecencyScoreCalled);
                Assert.True(mockUtility.CalculateDebtVolatilityCalled);
                Assert.True(mockUtility.CalculateCoordinationOverlapCalled);

                var fileMetric = Assert.Single(files);
                Assert.Equal(42.0, fileMetric.DebtVolatility);
                Assert.Equal(77.0, fileMetric.CoordinationOverlap);
            }


            public class MockFileSystem : IFileSystem
            {
                public Dictionary<string, byte[]> Files { get; } = new();

                public bool FileExists(string path)
                {
                    return Files.ContainsKey(path);
                }

                public long GetFileSize(string path)
                {
                    if (Files.TryGetValue(path, out var data))
                    {
                        return data.Length;
                    }
                    throw new FileNotFoundException();
                }

                public Stream OpenRead(string path)
                {
                    if (Files.TryGetValue(path, out var data))
                    {
                        return new MemoryStream(data);
                    }
                    throw new FileNotFoundException();
                }
            }

            [Fact]
            public async Task TestDiskFileStatsProvider_WithMockFileSystem()
            {
                var mockFs = new MockFileSystem();
                string repoRoot = "/mock/root";
                string relativePath = "testfile.txt";
                string fullPath = Path.Combine(repoRoot, relativePath);

                string content = "line 1\nline 2\nlongest line here";
                mockFs.Files[fullPath] = Encoding.UTF8.GetBytes(content);

                var provider = new DiskFileStatsProvider(mockFs);
                var stats = await provider.ComputeFileStatsAsync(repoRoot, new List<string> { relativePath });

                Assert.NotNull(stats);
                Assert.True(stats.ContainsKey(relativePath));
                var fileStat = stats[relativePath];
                Assert.Equal(content.Length, fileStat.Size);
                Assert.Equal(3, fileStat.Lines);
                Assert.Equal("longest line here".Length, fileStat.Width);
            }

            [Fact]
            public async Task TestDiskFileStatsProvider_WithMockFileSystem_BinaryAndNonexistent()
            {
                var mockFs = new MockFileSystem();
                string repoRoot = "/mock/root";
                string binaryRelPath = "binary.bin";
                string nonexistentRelPath = "missing.txt";

                string fullBinaryPath = Path.Combine(repoRoot, binaryRelPath);
                mockFs.Files[fullBinaryPath] = new byte[] { 1, 2, 0, 4, 5 };

                var provider = new DiskFileStatsProvider(mockFs);
                var stats = await provider.ComputeFileStatsAsync(repoRoot, new List<string> { binaryRelPath, nonexistentRelPath });

                Assert.NotNull(stats);

                Assert.True(stats.ContainsKey(binaryRelPath));
                Assert.Equal(5, stats[binaryRelPath].Size);
                Assert.Equal(0, stats[binaryRelPath].Lines);
                Assert.Equal(0, stats[binaryRelPath].Width);

                Assert.True(stats.ContainsKey(nonexistentRelPath));
                Assert.Equal(0, stats[nonexistentRelPath].Size);
                Assert.Equal(0, stats[nonexistentRelPath].Lines);
                Assert.Equal(0, stats[nonexistentRelPath].Width);
            }

            private class MockFamiliarityScoringEngine : IFamiliarityScoringEngine
            {
                public bool ScoreFilesCalled { get; set; }
                public bool ScoreAreasCalled { get; set; }

                public List<FileMetric> ScoreFiles(List<ItemAccumulator> items, int depth)
                {
                    ScoreFilesCalled = true;
                    return new List<FileMetric> { new FileMetric { Path = "mockfile.cs", HeatScore = 99 } };
                }

                public List<AreaMetric> ScoreAreas(List<ItemAccumulator> items)
                {
                    ScoreAreasCalled = true;
                    return new List<AreaMetric> { new AreaMetric { Area = "mockarea", HeatScore = 88 } };
                }
            }

            [Fact]
            public void TestAnalysisPipeline_UsesInjectedScoringEngine()
            {
                var mockScoring = new MockFamiliarityScoringEngine();
                var pipeline = new AnalysisPipeline(scoringEngine: mockScoring);
                
                var commits = new List<GitCommitRecord>
                {
                    new GitCommitRecord
                    {
                        Hash = "123",
                        Author = new GitIdentity { Name = "test", Email = "test@example.com" },
                        Files = new List<GitFileChange> { new GitFileChange { Path = "mockfile.cs", Added = 10, Deleted = 5 } }
                    }
                };
                var headFiles = new HashSet<string> { "mockfile.cs" };
                var config = new GiticConfig();
                var settings = new AnalysisSettings();

                var result = pipeline.Run(commits, headFiles, config, settings, AnalysisCommand.Hotspots);

                Assert.True(mockScoring.ScoreFilesCalled);
                Assert.True(mockScoring.ScoreAreasCalled);
                Assert.Single(result.Files);
                Assert.Equal("mockfile.cs", result.Files[0].Path);
                Assert.Single(result.Areas);
                Assert.Equal("mockarea", result.Areas[0].Area);
            }

            [Fact]
            public void TestAnalysisPipeline_UsesInjectedWarningCollector()
            {
                var mockWarningCollector = new MockWarningCollector();
                var pipeline = new AnalysisPipeline(warningCollector: mockWarningCollector);
                
                var commits = new List<GitCommitRecord>
                {
                    new GitCommitRecord
                    {
                        Hash = "123",
                        Author = new GitIdentity { Name = "test", Email = "test@example.com" },
                        Files = new List<GitFileChange> { new GitFileChange { Path = "mockfile.cs", Added = 10, Deleted = 5 } }
                    }
                };
                var headFiles = new HashSet<string> { "mockfile.cs" };
                var config = new GiticConfig();
                var settings = new AnalysisSettings();

                var result = pipeline.Run(commits, headFiles, config, settings, AnalysisCommand.Hotspots);

                Assert.True(mockWarningCollector.CollectCalled);
                Assert.Contains("mock_warning", result.Warnings);
            }

            [Fact]
            public void TestAnalysisPipeline_UsesInjectedIdentityRegistry()
            {
                var mockRegistry = new MockIdentityRegistry();
                var pipeline = new AnalysisPipeline(identityRegistry: mockRegistry);
                
                var commits = new List<GitCommitRecord>
                {
                    new GitCommitRecord
                    {
                        Hash = "123",
                        Author = new GitIdentity { Name = "test", Email = "test@example.com" },
                        Files = new List<GitFileChange> { new GitFileChange { Path = "mockfile.cs", Added = 10, Deleted = 5 } }
                    }
                };
                var headFiles = new HashSet<string> { "mockfile.cs" };
                var config = new GiticConfig();
                var settings = new AnalysisSettings();

                var result = pipeline.Run(commits, headFiles, config, settings, AnalysisCommand.Hotspots);

                Assert.True(mockRegistry.RegisterRealIdentityCalled);
            }

        }
    }
