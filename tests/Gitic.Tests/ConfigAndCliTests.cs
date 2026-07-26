using System;
using System.Collections.Generic;
using Xunit;

namespace Gitic.Tests
{
    public class ConfigAndCliTests
    {
        [Fact]
        public void TestCommitClassifier_DefaultStrategies()
        {
            var classifier = new CommitClassifier();

            Assert.Equal("bugfix", classifier.Classify("fix: resolve crash in thread loop"));
            Assert.Equal("bugfix", classifier.Classify("prevent memory leak in file loader"));
            Assert.Equal("bugfix", classifier.Classify("revert accidental commit"));
            
            Assert.Equal("feature", classifier.Classify("feat: implement new auth flow"));
            Assert.Equal("feature", classifier.Classify("introduce areas metric processor"));
            Assert.Equal("feature", classifier.Classify("add unit tests for config parser"));

            Assert.Equal("other", classifier.Classify("docs: update README with installation steps"));
            Assert.Equal("other", classifier.Classify("chore: bump version to 1.1.0"));
        }

        [Fact]
        public void TestYamlSubsetParser_BasicMappingsAndScalars()
        {
            string yaml = @"
# Simple config test
identity:
  merge_on_email: true
metrics:
  temporal_coupling_max_commit_file_count: 50
";
            var parsed = (Dictionary<string, object?>)YamlSubsetParserHelper.ParseYamlSubset(yaml, "test_source")!;
            
            Assert.NotNull(parsed);
            Assert.True(parsed.ContainsKey("identity"));
            
            var identity = (Dictionary<string, object?>)parsed["identity"]!;
            Assert.True((bool)identity["merge_on_email"]!);

            var metrics = (Dictionary<string, object?>)parsed["metrics"]!;
            Assert.Equal(50L, (long)metrics["temporal_coupling_max_commit_file_count"]!);
        }

        [Fact]
        public void TestYamlSubsetParser_Sequences()
        {
            string yaml = @"
excludes:
  - pattern: 'node_modules/**'
    category: dependency
  - pattern: 'dist/**'
    category: build
";
            var parsed = (Dictionary<string, object?>)YamlSubsetParserHelper.ParseYamlSubset(yaml, "test_source")!;
            Assert.NotNull(parsed);
            
            var excludes = (List<object?>)parsed["excludes"]!;
            Assert.Equal(2, excludes.Count);

            var entry1 = (Dictionary<string, object?>)excludes[0]!;
            Assert.Equal("node_modules/**", (string)entry1["pattern"]!);
            Assert.Equal("dependency", (string)entry1["category"]!);

            var entry2 = (Dictionary<string, object?>)excludes[1]!;
            Assert.Equal("dist/**", (string)entry2["pattern"]!);
            Assert.Equal("build", (string)entry2["category"]!);
        }

        [Fact]
        public void TestYamlSubsetParser_TabsNotSupported()
        {
            string yaml = "identity:\n\tmerge_on_email: true";
            Assert.Throws<ConfigValidationError>(() => YamlSubsetParserHelper.ParseYamlSubset(yaml, "test_source"));
        }

        [Fact]
        public void TestContributorLookupRegistry()
        {
            var contributors = new List<ContributorMetric>
            {
                new() { Name = "Alice Smith", Email = "alice@example.com" },
                new() { Name = "alice smith", Email = "alice-alt@example.com" },
                new() { Name = "Bob Jones", Email = "bob@example.com" }
            };

            var registry = new ContributorLookupRegistry(contributors);

            // Exact match (case-sensitive) takes precedence
            var exact = registry.Find("Alice Smith");
            Assert.Equal("alice@example.com", exact.Email);

            // Exact match for the lowercase version
            var exactAlt = registry.Find("alice smith");
            Assert.Equal("alice-alt@example.com", exactAlt.Email);

            // Normalized email match
            var emailMatch = registry.Find("bob@example.com");
            Assert.Equal("Bob Jones", emailMatch.Name);

            // Ambiguous match (two matching Alice Smiths by case-insensitive lookup)
            Assert.Throws<AmbiguousContributorError>(() => registry.Find("ALICE SMITH"));

            // Not found
            Assert.Throws<ContributorNotFoundError>(() => registry.Find("Charlie"));

            // Null or whitespace checks
            var nullEx = Assert.Throws<ArgumentException>(() => registry.Find(null!));
            Assert.Equal("lookup", nullEx.ParamName);

            var emptyEx = Assert.Throws<ArgumentException>(() => registry.Find(""));
            Assert.Equal("lookup", emptyEx.ParamName);

            var whitespaceEx = Assert.Throws<ArgumentException>(() => registry.Find("   "));
            Assert.Equal("lookup", whitespaceEx.ParamName);
        }

        private class FakeContributorLookupRegistry : IContributorLookupRegistry
        {
            private readonly ContributorMetric _metric;

            public FakeContributorLookupRegistry(ContributorMetric metric)
            {
                _metric = metric;
            }

            public ContributorMetric Find(string lookup)
            {
                return _metric;
            }
        }

        [Fact]
        public void TestIContributorLookupRegistryInterfaceAndMock()
        {
            var contributors = new List<ContributorMetric>
            {
                new() { Name = "Alice Smith", Email = "alice@example.com" }
            };
            IContributorLookupRegistry registry = new ContributorLookupRegistry(contributors);
            var result = registry.Find("Alice Smith");
            Assert.Equal("alice@example.com", result.Email);

            var preconfiguredMetric = new ContributorMetric { Name = "Mocked Contributor", Email = "mocked@example.com" };
            IContributorLookupRegistry fakeRegistry = new FakeContributorLookupRegistry(preconfiguredMetric);
            var mockResult = fakeRegistry.Find("Any Lookup");
            Assert.Equal("Mocked Contributor", mockResult.Name);
            Assert.Equal("mocked@example.com", mockResult.Email);
        }

        [Fact]
        public void TestCommandLineParser_Success()
        {
            string[] args = { "hotspots", "--json", "--depth", "5", "--all-time", "/path/to/repo" };
            var parser = new CommandLineParser(args);
            var parsed = parser.Parse();

            Assert.Equal("hotspots", parsed.Command);
            Assert.True(parsed.Settings.Json);
            Assert.Equal(5, parsed.Settings.Depth);
            Assert.True(parsed.Settings.AllTime);
            Assert.Equal("/path/to/repo", parsed.RepoPath);
        }

        [Fact]
        public void TestCommandLineParser_DepthValidation()
        {
            string[] args = { "hotspots", "--depth", "11" };
            var parser = new CommandLineParser(args);
            Assert.Throws<CommandLineParseError>(() => parser.Parse());
        }

        [Fact]
        public void TestCommandLineValidator_IsCommand()
        {
            // Valid commands (case-insensitive)
            new CommandLineParser(new[] { "hotspots" }).Parse();
            new CommandLineParser(new[] { "Hotspots" }).Parse();
            new CommandLineParser(new[] { "areas" }).Parse();
            new CommandLineParser(new[] { "contributors" }).Parse();
            new CommandLineParser(new[] { "contributor", "some_contributor" }).Parse();
            new CommandLineParser(new[] { "report" }).Parse();
            new CommandLineParser(new[] { "config" }).Parse();

            // Invalid commands
            Assert.Throws<CommandLineParseError>(() => new CommandLineParser(new[] { "unknown_command" }).Parse());
            Assert.Throws<CommandLineParseError>(() => new CommandLineParser(new string[] { "" }).Parse());
            Assert.Throws<CommandLineParseError>(() => new CommandLineParser(new string[] { null! }).Parse());
        }

        [Fact]
        public void TestCommandLineValidator_ValidateDepth()
        {
            // Valid depth values
            Assert.Equal(1, new CommandLineParser(new[] { "hotspots", "--depth", "1" }).Parse().Settings.Depth);
            Assert.Equal(5, new CommandLineParser(new[] { "hotspots", "--depth", "5" }).Parse().Settings.Depth);
            Assert.Equal(10, new CommandLineParser(new[] { "hotspots", "--depth", "10" }).Parse().Settings.Depth);

            // Invalid depth values
            var ex1 = Assert.Throws<CommandLineParseError>(() => new CommandLineParser(new[] { "hotspots", "--depth", "0" }).Parse());
            Assert.Equal("--depth must be an integer between 1 and 10.", ex1.Message);

            var ex2 = Assert.Throws<CommandLineParseError>(() => new CommandLineParser(new[] { "hotspots", "--depth", "-3" }).Parse());
            Assert.Equal("--depth must be an integer between 1 and 10.", ex2.Message);

            var ex3 = Assert.Throws<CommandLineParseError>(() => new CommandLineParser(new[] { "hotspots", "--depth", "notaninteger" }).Parse());
            Assert.Equal("--depth must be an integer between 1 and 10.", ex3.Message);

            var ex4 = Assert.Throws<CommandLineParseError>(() => new CommandLineParser(new[] { "hotspots", "--depth", "11" }).Parse());
            Assert.Equal("--depth must be an integer between 1 and 10.", ex4.Message);
        }

        [Fact]
        public void TestConfigValidator_ValidateAttentionWeightsSum()
        {
            var weights = new AttentionWeights
            {
                Churn = 0.5,
                Recency = 0.5,
                ContributorSpread = 0.0,
                LowFamiliarityConcentration = 0.1 // Sums to 1.1
            };

            Assert.Throws<ConfigValidationError>(() => new ConfigValidator().ValidateAttentionWeights(weights, "test"));
        }

        [Fact]
        public async Task TestHtmlRenderer_DirectoryPathResolution()
        {
            var renderer = new HtmlRenderer();
            var result = new AnalysisResult();

            string htmlContent = await renderer.RenderAsync(result);
            Assert.NotNull(htmlContent);
            Assert.Contains("<html", htmlContent);
        }

        [Fact]
        public async Task TestCli_RunCliAsync_HandlesExceptionGracefully()
        {
            string repoPath = "sessions-db";
            string current = Directory.GetCurrentDirectory();
            while (!string.IsNullOrEmpty(current))
            {
                string candidate = Path.Combine(current, "sessions-db");
                if (Directory.Exists(candidate) && Directory.Exists(Path.Combine(candidate, ".git")))
                {
                    repoPath = candidate;
                    break;
                }
                
                string? parent = Path.GetDirectoryName(current);
                if (parent == null || parent == current) break;
                
                string siblingCandidate = Path.Combine(parent, "sessions-db");
                if (Directory.Exists(siblingCandidate) && Directory.Exists(Path.Combine(siblingCandidate, ".git")))
                {
                    repoPath = siblingCandidate;
                    break;
                }
                
                current = parent;
            }

            string[] args = { "report", repoPath, "--html", "/nonexistent_directory_for_testing/report.html" };
            var result = await Cli.RunCliAsync(args);
            Assert.Equal(1, result.ExitCode);
            Assert.Contains("Error:", result.Stderr);
        }

        [Fact]
        public async Task TestCli_HelpSupport()
        {
            var helpArgs = new[] { "--help" };
            var parser = new CommandLineParser(helpArgs);
            var parsed = parser.Parse();
            Assert.Equal("help", parsed.Command);

            var hArgs = new[] { "-h" };
            var hParser = new CommandLineParser(hArgs);
            var hParsed = hParser.Parse();
            Assert.Equal("help", hParsed.Command);

            var runResult = await Cli.RunCliAsync(helpArgs);
            Assert.Equal(0, runResult.ExitCode);
            Assert.Contains("Usage:", runResult.Stdout);
            Assert.Contains("Commands:", runResult.Stdout);
            Assert.Contains("Options:", runResult.Stdout);
        }

        [Fact]
        public async Task TestMarkdownRenderer_DirectoryPathAndContents()
        {
            var renderer = new MarkdownRenderer();
            var result = new AnalysisResult
            {
                Files = new List<FileMetric>
                {
                    new() { Path = "src/Main.cs", Area = "src", Lines = 1200, Width = 120, Churn = 500, AttentionScore = 85.5, ReworkRate = 0.25 }
                },
                Contributors = new List<ContributorMetric>
                {
                    new() { Name = "Alice", Email = "alice@example.com", TotalActivity = 150 }
                },
                Areas = new List<AreaMetric>
                {
                    new() { Area = "src", FileCount = 1, Touches = 150, Churn = 500 }
                }
            };

            string content = await renderer.RenderAsync(result);

            Assert.Contains("# 📊 Gitic Analysis Report", content);
            Assert.Contains("Overview", content);
            Assert.Contains("Hotspots", content);
            Assert.Contains("Rework Alert:", content);
            Assert.Contains("File Length Alert:", content);
            Assert.Contains("Complexity Distribution (Min / Max / Avg)", content);
            Assert.Contains("Min: 1200", content);
            Assert.Contains("Min: 120", content);
        }

        [Fact]
        public async Task TestMarkdownRenderer_DeterministicTimestamp()
        {
            var renderer = new MarkdownRenderer();
            var resultWithValidTimestamp = new AnalysisResult
            {
                Analysis = new AnalysisMetadata
                {
                    GeneratedAt = "2026-07-25T12:34:56.000Z"
                }
            };

            string content = await renderer.RenderAsync(resultWithValidTimestamp);
            Assert.Contains("Generated on: 2026-07-25 12:34:56 UTC", content);

            var resultWithInvalidTimestamp = new AnalysisResult
            {
                Analysis = new AnalysisMetadata
                {
                    GeneratedAt = "Not a date string"
                }
            };

            content = await renderer.RenderAsync(resultWithInvalidTimestamp);
            Assert.Contains("Generated on: Not a date string", content);
        }

        [Fact]
        public async Task TestSvgRenderer_DirectoryPathAndContents()
        {
            var summaryRenderer = new SvgSummaryRenderer();
            var complexityRenderer = new SvgComplexityRenderer();
            var result = new AnalysisResult
            {
                Files = new List<FileMetric>
                {
                    new() { Path = "src/Main.cs", Area = "src", Lines = 1200, Width = 120, Churn = 500, AttentionScore = 85.5, ReworkRate = 0.25 }
                },
                Areas = new List<AreaMetric>
                {
                    new() { Area = "src", FileCount = 1, Touches = 150, Churn = 500 }
                }
            };

            string content = await summaryRenderer.RenderAsync(result);
            string complexityContent = await complexityRenderer.RenderAsync(result);

            Assert.Contains("<svg viewBox=\"0 0 800 450\"", content);
            Assert.Contains("<circle cx=", content);
            Assert.Contains("Volatile Hotspots", content);
            Assert.Contains("src/Main.cs", content);

            Assert.Contains("<svg viewBox=\"0 0 800 150\"", complexityContent);
            Assert.Contains("Complexity Distribution by App Module", complexityContent);
            Assert.Contains("FILE LENGTH (LINES)", complexityContent);
            Assert.Contains("MAX LINE WIDTH (CHARS)", complexityContent);
        }

        [Fact]
        public void TestGitignore_Exclusions()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            try
            {
                string gitignoreContent = 
@"# Compiled source
*.class
*.dll
bin/
/obj/
";
                File.WriteAllText(Path.Combine(tempDir, ".gitignore"), gitignoreContent);

                var rules = PathClassifier.LoadGitignoreRules(tempDir);
                Assert.Equal(7, rules.Count);
                Assert.Contains(rules, r => r.Pattern == "**/*.class" && r.Category == "gitignore");
                Assert.Contains(rules, r => r.Pattern == "*.class" && r.Category == "gitignore");
                Assert.Contains(rules, r => r.Pattern == "**/*.dll" && r.Category == "gitignore");
                Assert.Contains(rules, r => r.Pattern == "*.dll" && r.Category == "gitignore");
                Assert.Contains(rules, r => r.Pattern == "**/bin/**" && r.Category == "gitignore");
                Assert.Contains(rules, r => r.Pattern == "bin/**" && r.Category == "gitignore");
                Assert.Contains(rules, r => r.Pattern == "obj/**" && r.Category == "gitignore");

                var classifier = new PathClassifier(
                    new HashSet<string> { "src/Main.cs" },
                    rules,
                    true,
                    null
                );

                Assert.True(classifier.Check("src/Main.cs"));
                Assert.False(classifier.Check("bin/Debug/Gitic.dll"));
                Assert.False(classifier.Check("obj/Gitic.csproj.nuget.g.props"));
                Assert.False(classifier.Check("test.class"));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Fact]
        public void TestParseGitignoreLines()
        {
            var lines = new[]
            {
                "# Comment line",
                "",
                "*.class",
                "bin/",
                "/obj/",
                "custom-pattern"
            };

            var rules = PathClassifier.ParseGitignoreLines(lines, "custom-category");

            Assert.Contains(rules, r => r.Pattern == "**/*.class" && r.Category == "custom-category");
            Assert.Contains(rules, r => r.Pattern == "*.class" && r.Category == "custom-category");
            Assert.Contains(rules, r => r.Pattern == "**/bin/**" && r.Category == "custom-category");
            Assert.Contains(rules, r => r.Pattern == "bin/**" && r.Category == "custom-category");
            Assert.Contains(rules, r => r.Pattern == "obj/**" && r.Category == "custom-category");
            Assert.Contains(rules, r => r.Pattern == "**/custom-pattern" && r.Category == "custom-category");
            Assert.Contains(rules, r => r.Pattern == "custom-pattern" && r.Category == "custom-category");
        }

        public class MockConsoleReporter : IConsoleReporter
        {
            public List<string> Messages { get; } = new();
            public List<string> ErrorMessages { get; } = new();

            public void Write(string message) => Messages.Add(message);
            public void WriteLine(string message) => Messages.Add(message + "\n");
            public void WriteError(string message) => ErrorMessages.Add(message);
            public void WriteErrorLine(string message) => ErrorMessages.Add(message + "\n");
        }

        [Fact]
        public async Task TestCli_RunWithIConsoleReporter()
        {
            var reporter = new MockConsoleReporter();
            var runResult = await Cli.RunCliAsync(new[] { "--help" }, reporter);
            Assert.Equal(0, runResult.ExitCode);
            Assert.Contains(reporter.Messages, m => m.Contains("Usage:"));
        }

        [Fact]
        public void TestConsoleReporter_WritesToConsole()
        {
            var reporter = new ConsoleReporter();
            var oldOut = Console.Out;
            var oldError = Console.Error;
            using var swOut = new System.IO.StringWriter();
            using var swErr = new System.IO.StringWriter();
            try
            {
                Console.SetOut(swOut);
                Console.SetError(swErr);
                reporter.Write("hello");
                reporter.WriteLine(" world");
                reporter.WriteError("error");
                reporter.WriteErrorLine(" log");

                Assert.Equal("hello world" + Environment.NewLine, swOut.ToString());
                Assert.Equal("error log" + Environment.NewLine, swErr.ToString());
            }
            finally
            {
                Console.SetOut(oldOut);
                Console.SetError(oldError);
            }
        }
    }
}
