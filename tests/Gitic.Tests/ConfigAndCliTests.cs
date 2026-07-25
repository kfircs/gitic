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
        public void TestConfigValidator_ValidateAttentionWeightsSum()
        {
            var weights = new AttentionWeights
            {
                Churn = 0.5,
                Recency = 0.5,
                ContributorSpread = 0.0,
                LowFamiliarityConcentration = 0.1 // Sums to 1.1
            };

            Assert.Throws<ConfigValidationError>(() => ConfigValidator.ValidateAttentionWeights(weights, "test"));
        }

        [Fact]
        public async Task TestHtmlRenderer_DirectoryPathResolution()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            try
            {
                var renderer = new HtmlRenderer(tempDir);
                var result = new AnalysisResult();

                string msg = await renderer.RenderAsync(result);
                string expectedFile = Path.Combine(tempDir, "report.html");

                Assert.True(File.Exists(expectedFile));
                Assert.Contains("Wrote HTML report to", msg);
                Assert.Contains("report.html", msg);
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
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            try
            {
                var renderer = new MarkdownRenderer(tempDir);
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

                string msg = await renderer.RenderAsync(result);
                string expectedFile = Path.Combine(tempDir, "report.md");

                Assert.True(File.Exists(expectedFile));
                Assert.Contains("Wrote Markdown report to", msg);
                Assert.Contains("report.md", msg);

                string content = await File.ReadAllTextAsync(expectedFile);
                Assert.Contains("# 📊 Gitic Analysis Report", content);
                Assert.Contains("Overview", content);
                Assert.Contains("Hotspots", content);
                Assert.Contains("Rework Alert:", content);
                Assert.Contains("File Length Alert:", content);
                Assert.Contains("Complexity Distribution (Min / Max / Avg)", content);
                Assert.Contains("Min: 1200", content);
                Assert.Contains("Min: 120", content);
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
        public async Task TestSvgRenderer_DirectoryPathAndContents()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            try
            {
                var renderer = new SvgRenderer(tempDir);
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

                string msg = await renderer.RenderAsync(result);
                string expectedFile = Path.Combine(tempDir, "report.svg");
                string expectedComplexityFile = Path.Combine(tempDir, "report-complexity.svg");

                Assert.True(File.Exists(expectedFile));
                Assert.True(File.Exists(expectedComplexityFile));
                Assert.Contains("Wrote SVG report to", msg);
                Assert.Contains("report.svg", msg);
                Assert.Contains("Wrote Svg Complexity report to", msg);
                Assert.Contains("report-complexity.svg", msg);

                string content = await File.ReadAllTextAsync(expectedFile);
                Assert.Contains("<svg viewBox=\"0 0 800 450\"", content);
                Assert.Contains("<circle cx=", content);
                Assert.Contains("Volatile Hotspots", content);
                Assert.Contains("src/Main.cs", content);

                string complexityContent = await File.ReadAllTextAsync(expectedComplexityFile);
                Assert.Contains("<svg viewBox=\"0 0 800 150\"", complexityContent);
                Assert.Contains("Complexity Distribution by App Module", complexityContent);
                Assert.Contains("FILE LENGTH (LINES)", complexityContent);
                Assert.Contains("MAX LINE WIDTH (CHARS)", complexityContent);
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
    }
}
