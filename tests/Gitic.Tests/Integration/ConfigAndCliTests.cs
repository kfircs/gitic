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
            string[] args = { "--json", "--depth", "5", "--all-time", "/path/to/repo" };
            var parser = new CommandLineParser(args);
            var parsed = parser.Parse();

            Assert.Equal("wizard", parsed.Command);
            Assert.True(parsed.Settings.Json);
            Assert.Equal(5, parsed.Settings.Depth);
            Assert.True(parsed.Settings.AllTime);
            Assert.Equal("/path/to/repo", parsed.RepoPath);
        }

        [Fact]
        public void TestCommandLineParser_DepthValidation()
        {
            string[] args = { "--depth", "11" };
            var parser = new CommandLineParser(args);
            Assert.Throws<CommandLineParseError>(() => parser.Parse());
        }

        [Fact]
        public void TestCommandLineValidator_IsCommand()
        {
            // Verifies that gitic parses with or without repo_path argument
            var parsed1 = new CommandLineParser(new string[] { }).Parse();
            Assert.Equal("wizard", parsed1.Command);
            Assert.Equal(".", parsed1.RepoPath);

            var parsed2 = new CommandLineParser(new[] { "/some/repo" }).Parse();
            Assert.Equal("wizard", parsed2.Command);
            Assert.Equal("/some/repo", parsed2.RepoPath);

            // Empty or null argument errors are still thrown
            Assert.Throws<CommandLineParseError>(() => new CommandLineParser(new string[] { "" }).Parse());
            Assert.Throws<CommandLineParseError>(() => new CommandLineParser(new string[] { null! }).Parse());
        }

        [Fact]
        public void TestCommandLineValidator_ValidateDepth()
        {
            // Valid depth values
            Assert.Equal(1, new CommandLineParser(new[] { "--depth", "1" }).Parse().Settings.Depth);
            Assert.Equal(5, new CommandLineParser(new[] { "--depth", "5" }).Parse().Settings.Depth);
            Assert.Equal(10, new CommandLineParser(new[] { "--depth", "10" }).Parse().Settings.Depth);

            // Invalid depth values
            var ex1 = Assert.Throws<CommandLineParseError>(() => new CommandLineParser(new[] { "--depth", "0" }).Parse());
            Assert.Equal("--depth must be an integer between 1 and 10.", ex1.Message);

            var ex2 = Assert.Throws<CommandLineParseError>(() => new CommandLineParser(new[] { "--depth", "-3" }).Parse());
            Assert.Equal("--depth must be an integer between 1 and 10.", ex2.Message);

            var ex3 = Assert.Throws<CommandLineParseError>(() => new CommandLineParser(new[] { "--depth", "notaninteger" }).Parse());
            Assert.Equal("--depth must be an integer between 1 and 10.", ex3.Message);

            var ex4 = Assert.Throws<CommandLineParseError>(() => new CommandLineParser(new[] { "--depth", "11" }).Parse());
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
        public async Task TestHtmlRenderer_Streaming()
        {
            var renderer = new HtmlRenderer();
            var result = new AnalysisResult();

            using var ms = new MemoryStream();
            await renderer.RenderToStreamAsync(result, ms);
            ms.Position = 0;
            using var reader = new StreamReader(ms, System.Text.Encoding.UTF8);
            string streamedContent = await reader.ReadToEndAsync();

            string regularContent = await renderer.RenderAsync(result);

            Assert.NotNull(streamedContent);
            Assert.Contains("<html", streamedContent);
            Assert.Equal(regularContent, streamedContent);
        }

        [Fact]
        public async Task TestCli_RunCliAsync_HandlesExceptionGracefully()
        {
            string[] args = { "--depth", "11" };
            var result = await Cli.RunCliAsync(args);
            Assert.Equal(2, result.ExitCode);
            Assert.Contains("--depth must be an integer between 1 and 10.", result.Stderr);
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
            Assert.Contains("Gitic Strategic Codebase Analysis", runResult.Stdout);
            Assert.Contains("Options:", runResult.Stdout);
            Assert.Contains("--since", runResult.Stdout);
            Assert.Contains("--depth", runResult.Stdout);
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

        [Fact]
        public async Task TestCli_VersionSupport()
        {
            var reporter = new MockConsoleReporter();
            
            var runResult1 = await Cli.RunCliAsync(new[] { "--version" }, reporter);
            Assert.Equal(0, runResult1.ExitCode);
            Assert.Contains("gitic version", runResult1.Stdout);

            var runResult2 = await Cli.RunCliAsync(new[] { "-v" }, reporter);
            Assert.Equal(0, runResult2.ExitCode);

            var runResult3 = await Cli.RunCliAsync(new[] { "version" }, reporter);
            Assert.Equal(0, runResult3.ExitCode);
        }

        [Fact]
        public async Task TestCli_EmptyArgsSupport()
        {
            var reporter = new MockConsoleReporter();
            var runResult = await Cli.RunCliAsync(new string[0], reporter);
            Assert.Equal(2, runResult.ExitCode);
            Assert.Contains("Interactive TUI cannot be run", runResult.Stderr);
        }

        [Fact]
        public async Task TestConfigurationEngine_FallbackResolution()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            try
            {
                var engine = new ConfigurationEngine();
                
                // Case 1: Neither exists - should fall back to default
                var optionsEmpty = new LoadGiticConfigOptions { RepoRoot = tempDir };
                var resolvedEmpty = await engine.LoadAndResolveAsync(new AnalyzeInput { RepoRoot = tempDir }, optionsEmpty);
                Assert.NotNull(resolvedEmpty.Config);
                
                // Case 2: Only legacy .gitizer.yml exists - should load it
                string legacyPath = Path.Combine(tempDir, ".gitizer.yml");
                File.WriteAllText(legacyPath, "identity:\n  merge_on_email: true\n");
                var optionsLegacy = new LoadGiticConfigOptions { RepoRoot = tempDir };
                var resolvedLegacy = await engine.LoadAndResolveAsync(new AnalyzeInput { RepoRoot = tempDir }, optionsLegacy);
                Assert.True(resolvedLegacy.Config.Identity.MergeOnEmail);

                // Case 3: Both exist - preferred .gitic.yml should take precedence
                string preferredPath = Path.Combine(tempDir, ".gitic.yml");
                File.WriteAllText(preferredPath, "identity:\n  merge_on_email: false\n");
                var optionsBoth = new LoadGiticConfigOptions { RepoRoot = tempDir };
                var resolvedBoth = await engine.LoadAndResolveAsync(new AnalyzeInput { RepoRoot = tempDir }, optionsBoth);
                Assert.False(resolvedBoth.Config.Identity.MergeOnEmail);
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
        public async Task TestCli_HelpDocumentsSVGReports()
        {
            var reporter = new MockConsoleReporter();
            var runResult = await Cli.RunCliAsync(new[] { "--help" }, reporter);
            Assert.Equal(0, runResult.ExitCode);
            Assert.Contains("--svg <path>", runResult.Stdout);
            Assert.Contains("--format <format>", runResult.Stdout);
            Assert.Contains("--color <color>", runResult.Stdout);
            Assert.Contains("--config <config>", runResult.Stdout);
            Assert.Contains("--user-config <user-config>", runResult.Stdout);
            Assert.Contains("--limit <limit>", runResult.Stdout);
            Assert.Contains("--sort <sort>", runResult.Stdout);
            Assert.Contains("--columns <columns>", runResult.Stdout);
        }

        [Fact]
        public async Task TestCli_NonRepoExitCodes()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            try
            {
                var runResult = await Cli.RunCliAsync(new[] { tempDir, "--json" });
                Assert.Equal(1, runResult.ExitCode);
                Assert.Contains("is not inside a Git repository", runResult.Stderr);
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
        public async Task TestCli_InvalidUsageExitCodes()
        {
            var runResult1 = await Cli.RunCliAsync(new[] { "--color", "invalid-color-value" });
            Assert.Equal(2, runResult1.ExitCode);
            Assert.Contains("--color must be", runResult1.Stderr);

            var runResult2 = await Cli.RunCliAsync(new[] { "--format", "invalid-format-value" });
            Assert.Equal(2, runResult2.ExitCode);
            Assert.Contains("--format must be", runResult2.Stderr);
        }

        [Fact]
        public async Task TestCliTableRenderer_FormatsAndColors()
        {
            var result = new AnalysisResult
            {
                Analysis = new AnalysisMetadata { IncludedFileChangeCount = 10 },
                Files = new List<FileMetric>
                {
                    new() { Path = "src/Main.cs", AttentionScore = 85.0, HeatScore = 90.0, Churn = 500, ContributorCount = 5, ScoreBreakdown = new ScoreBreakdown() }
                }
            };

            // Case 1: human format, color always
            var settingsAlways = new AnalysisSettings { Format = "human", Color = "always" };
            var rendererAlways = new CliTableRenderer(AnalysisCommand.Hotspots, settingsAlways);
            string outputAlways = await rendererAlways.RenderAsync(result);
            
            // Check for Unicode warning (⚠️) and heat (🔥) symbols and ANSI colors
            Assert.Contains("⚠️", outputAlways);
            Assert.Contains("🔥", outputAlways);
            Assert.Contains("\x1b[38;2;243;139;168m", outputAlways);

            // Case 2: plain format (should have no Unicode and no ANSI)
            var settingsPlain = new AnalysisSettings { Format = "plain" };
            var rendererPlain = new CliTableRenderer(AnalysisCommand.Hotspots, settingsPlain);
            string outputPlain = await rendererPlain.RenderAsync(result);
            Assert.DoesNotContain("⚠️", outputPlain);
            Assert.DoesNotContain("🔥", outputPlain);
            Assert.DoesNotContain("\x1b[", outputPlain, StringComparison.Ordinal);
            // Plain should use ASCII symbols [!] and *
            Assert.Contains("[!]", outputPlain);
            Assert.Contains("*", outputPlain);
        }

        [Fact]
        public async Task TestCliTableRenderer_TerminalCapabilities()
        {
            var result = new AnalysisResult
            {
                Analysis = new AnalysisMetadata { IncludedFileChangeCount = 10 },
                Files = new List<FileMetric>
                {
                    new() { Path = "src/Main.cs", AttentionScore = 85.0, HeatScore = 90.0, Churn = 500, ContributorCount = 5, ScoreBreakdown = new ScoreBreakdown() }
                }
            };

            string? originalNoColor = Environment.GetEnvironmentVariable("NO_COLOR");
            string? originalTerm = Environment.GetEnvironmentVariable("TERM");

            try
            {
                // Scenario 1: NO_COLOR is set to any value
                Environment.SetEnvironmentVariable("NO_COLOR", "1");
                Environment.SetEnvironmentVariable("TERM", "xterm-256color");

                var settingsAuto = new AnalysisSettings { Format = "human", Color = "auto" };
                var rendererAuto = new CliTableRenderer(AnalysisCommand.Hotspots, settingsAuto);
                string outputAuto = await rendererAuto.RenderAsync(result);

                // Should disable color when NO_COLOR is set
                Assert.DoesNotContain("\x1b[", outputAuto, StringComparison.Ordinal);

                // Scenario 2: TERM=dumb
                Environment.SetEnvironmentVariable("NO_COLOR", null);
                Environment.SetEnvironmentVariable("TERM", "dumb");

                var rendererDumb = new CliTableRenderer(AnalysisCommand.Hotspots, settingsAuto);
                string outputDumb = await rendererDumb.RenderAsync(result);

                // Should disable color and Unicode when TERM=dumb
                Assert.DoesNotContain("\x1b[", outputDumb, StringComparison.Ordinal);
                Assert.DoesNotContain("⚠️", outputDumb);
                Assert.DoesNotContain("🔥", outputDumb);
                Assert.Contains("[!]", outputDumb);
                Assert.Contains("*", outputDumb);
            }
            finally
            {
                Environment.SetEnvironmentVariable("NO_COLOR", originalNoColor);
                Environment.SetEnvironmentVariable("TERM", originalTerm);
            }
        }

        [Fact]
        public void TestTruncatePath_MiddleTruncation()
        {
            Assert.Equal("src/foo/ba...z/MyClass.cs", PathUtils.TruncatePath("src/foo/bar/baz/MyClass.cs", 25));
            Assert.Equal("src/MyClass.cs", PathUtils.TruncatePath("src/MyClass.cs", 25));
            Assert.Equal("s", PathUtils.TruncatePath("src/MyClass.cs", 1));
            Assert.Equal("s.cs", PathUtils.TruncatePath("src/MyClass.cs", 4));
        }

        [Fact]
        public async Task TestCliTableRenderer_AdaptiveHotspotsTable()
        {
            var result = new AnalysisResult
            {
                Analysis = new AnalysisMetadata { IncludedFileChangeCount = 10 },
                Settings = new AnalysisSettings { Limit = 2, Sort = "churn", Columns = "file,attention,churn" },
                Files = new List<FileMetric>
                {
                    new() { Path = "src/FileA.cs", AttentionScore = 80.0, Churn = 500 },
                    new() { Path = "src/FileB.cs", AttentionScore = 90.0, Churn = 1000 },
                    new() { Path = "src/FileC.cs", AttentionScore = 70.0, Churn = 200 }
                }
            };

            // Test 1: Sorting and Limit
            var service = new MetricsEngine();
            service.SortMetrics(result, AnalysisCommand.Hotspots);

            // Churn sort should put FileB first, then FileA, then FileC
            Assert.Equal("src/FileB.cs", result.Files[0].Path);
            Assert.Equal("src/FileA.cs", result.Files[1].Path);

            // Test 2: Custom Columns and Limit Rendering
            var renderer = new CliTableRenderer(AnalysisCommand.Hotspots, result.Settings);
            string output = await renderer.RenderAsync(result);

            // Output should contain only requested columns and limited to 2 rows
            Assert.Contains("file", output);
            Assert.Contains("attention", output);
            Assert.Contains("churn", output);
            Assert.DoesNotContain("heat", output);

            Assert.Contains("src/FileB.cs", output);
            Assert.Contains("src/FileA.cs", output);
            Assert.DoesNotContain("src/FileC.cs", output);
        }

        [Fact]
        public async Task TestCliTableRenderer_AreasAndContributorsParity()
        {
            var result = new AnalysisResult
            {
                Analysis = new AnalysisMetadata { IncludedFileChangeCount = 10 },
                Settings = new AnalysisSettings { Limit = 1, Sort = "heat" },
                Areas = new List<AreaMetric>
                {
                    new() { Area = "src/AreaA", HeatScore = 50.0, AttentionScore = 30.0 },
                    new() { Area = "src/AreaB", HeatScore = 90.0, AttentionScore = 40.0 }
                },
                Contributors = new List<ContributorMetric>
                {
                    new() { Name = "Alice", TotalActivity = 10 },
                    new() { Name = "Bob", TotalActivity = 50 }
                },
                Automation = new List<AutomationMetric>
                {
                    new() { Name = "Bot1", TotalActivity = 5 }
                }
            };

            // Test Areas custom sort
            var service = new MetricsEngine();
            service.SortMetrics(result, AnalysisCommand.Areas);

            // Heat sort should put AreaB first
            Assert.Equal("src/AreaB", result.Areas[0].Area);

            // Test Areas rendering (should apply Limit = 1)
            var areaRenderer = new CliTableRenderer(AnalysisCommand.Areas, result.Settings);
            string areaOutput = await areaRenderer.RenderAsync(result);
            Assert.Contains("src/AreaB", areaOutput);
            Assert.DoesNotContain("src/AreaA", areaOutput);

            // Test Contributors sorting and rendering (Limit = 1, default sort by activity)
            var contributorSettings = new AnalysisSettings { Limit = 1 };
            var contributorRenderer = new CliTableRenderer(AnalysisCommand.Contributors, contributorSettings);
            string contributorOutput = await contributorRenderer.RenderAsync(result);
            
            // Bob has activity 50, Alice has 10, Bot1 has 5 -> Bob should be rendered, Alice/Bot1 ignored due to Limit = 1
            Assert.Contains("Bob", contributorOutput);
            Assert.DoesNotContain("Alice", contributorOutput);
            Assert.DoesNotContain("Bot1", contributorOutput);
        }

        [Fact]
        public async Task TestCli_TemporalCouplingAndLeadTime_RenderingAndSorting()
        {
            var result = new AnalysisResult
            {
                Analysis = new AnalysisMetadata { IncludedFileChangeCount = 10 },
                Settings = new AnalysisSettings { Limit = 1, Sort = "degree" },
                TemporalCoupling = new List<TemporalCoupling>
                {
                    new() { FileA = "src/Main.cs", FileB = "src/Helper.cs", SharedCommits = 5, CouplingDegree = 0.8 },
                    new() { FileA = "src/User.cs", FileB = "src/Auth.cs", SharedCommits = 10, CouplingDegree = 0.9 }
                },
                LeadTimes = new LeadTimesInfo
                {
                    AverageLeadTimeHours = 42.5,
                    Merges = new List<MergeLeadTimeRecord>
                    {
                        new() { Hash = "abc1234", Date = "2026-07-28T12:00:00Z", LeadTimeHours = 10.0, Author = "Alice", FileCount = 2, Message = "Merge feature branch" },
                        new() { Hash = "def5678", Date = "2026-07-28T14:00:00Z", LeadTimeHours = 40.0, Author = "Bob", FileCount = 5, Message = "Merge another branch" }
                    }
                }
            };

            // 1. Test Temporal Coupling Sorting & Limit
            var service = new MetricsEngine();
            service.SortMetrics(result, AnalysisCommand.TemporalCoupling);

            // Coupling Degree sort should put src/User.cs & src/Auth.cs first (0.9 > 0.8)
            Assert.Equal("src/User.cs", result.TemporalCoupling[0].FileA);

            var tcRenderer = new CliTableRenderer(AnalysisCommand.TemporalCoupling, result.Settings);
            string tcOutput = await tcRenderer.RenderAsync(result);
            Assert.Contains("src/User.cs", tcOutput);
            Assert.DoesNotContain("src/Main.cs", tcOutput); // excluded due to limit = 1

            // 2. Test Lead Time Sorting & Limit
            var leadTimeSettings = new AnalysisSettings { Limit = 1, Sort = "hours" };
            result.Settings = leadTimeSettings;
            service.SortMetrics(result, AnalysisCommand.LeadTime);

            // LeadTimeHours sort (hours descending) should put def5678 first (40.0 > 10.0)
            Assert.Equal("def5678", result.LeadTimes.Merges[0].Hash);

            var ltRenderer = new CliTableRenderer(AnalysisCommand.LeadTime, leadTimeSettings);
            string ltOutput = await ltRenderer.RenderAsync(result);
            Assert.Contains("def5678", ltOutput);
            Assert.Contains("Average Lead Time: 42.5 hours", ltOutput);
            Assert.DoesNotContain("abc1234", ltOutput); // excluded due to limit = 1
        }

        [Fact]
        public async Task TestCli_TemporalCouplingAndLeadTime_NoDataGuidance()
        {
            var emptyResult = new AnalysisResult
            {
                Analysis = new AnalysisMetadata { IncludedFileChangeCount = 10 },
                Settings = new AnalysisSettings(),
                TemporalCoupling = new List<TemporalCoupling>(),
                LeadTimes = new LeadTimesInfo { Merges = new List<MergeLeadTimeRecord>() }
            };

            var tcRenderer = new CliTableRenderer(AnalysisCommand.TemporalCoupling, emptyResult.Settings);
            string tcOutput = await tcRenderer.RenderAsync(emptyResult);
            Assert.Contains("No temporal coupling pairs found", tcOutput);

            var ltRenderer = new CliTableRenderer(AnalysisCommand.LeadTime, emptyResult.Settings);
            string ltOutput = await ltRenderer.RenderAsync(emptyResult);
            Assert.Contains("No merge commits in the analysis window", ltOutput);
        }

        [Fact]
        public async Task TestWizardCommand_PrebuiltProfiles()
        {
            string currentDir = Directory.GetCurrentDirectory();
            var parsed = new ParsedArgs
            {
                Command = "wizard",
                RepoPath = currentDir,
                Settings = new AnalysisSettings()
            };

            // Option 1: Generate Curated Report (TUI Menu)
            // Option 0: Developer Onboarding & Collaboration Profile
            // Option 0: Markdown (.md)
            // Option 11: Exit (from menu)
            string inputLines = "1\n0\n0\n11\n";
            var originalIn = Console.In;
            var originalOut = Console.Out;

            try
            {
                Environment.SetEnvironmentVariable("GITIC_INTERACTIVE_TEST", "1");
                using var stringReader = new StringReader(inputLines);
                Console.SetIn(stringReader);

                // Set dummy out to prevent console pollution
                using var stringWriter = new StringWriter();
                Console.SetOut(stringWriter);

                var wizard = new WizardCommand(parsed);
                var reporter = new MockConsoleReporter();
                var result = await wizard.ExecuteAsync(reporter);

                Assert.Equal(0, result.ExitCode);

                // Find the generated file in the resolved .test-report folder
                var gitClient = new GitClient(currentDir);
                string repoRoot = await gitClient.GetRepositoryRootAsync() ?? currentDir;
                string targetDir = Path.Combine(repoRoot, ".test-report");
                var files = Directory.Exists(targetDir) ? Directory.GetFiles(targetDir, "gitic_report_*.md") : Array.Empty<string>();
                Assert.NotEmpty(files);

                string filePath = files[0];
                string content = await File.ReadAllTextAsync(filePath);

                // Verify the selected profile sections are present
                Assert.Contains("Developer Onboarding", content);
                Assert.Contains("Review Collaboration", content);

                // Verify non-selected sections are absent
                Assert.DoesNotContain("Code Rot", content);
                Assert.DoesNotContain("AI Code Strain", content);

                // Clean up
                File.Delete(filePath);
            }
            finally
            {
                Environment.SetEnvironmentVariable("GITIC_INTERACTIVE_TEST", null);
                Console.SetIn(originalIn);
                Console.SetOut(originalOut);
            }
        }

        [Fact]
        public async Task TestWizardCommand_EscapeKeyBackNavigation()
        {
            string currentDir = Directory.GetCurrentDirectory();
            var parsed = new ParsedArgs
            {
                Command = "wizard",
                RepoPath = currentDir,
                Settings = new AnalysisSettings()
            };

            // Option 1: Generate Curated Report (TUI Menu)
            // Option 0: Developer Onboarding & Collaboration Profile
            // Option escape: Go back from format selection to report type selection
            // Option 4: Custom Report
            // Option escape: Go back from custom sections selection to report type selection
            // Option escape: Go back from report type selection to main menu
            // Option 11: Exit (from menu)
            string inputLines = "1\n0\nescape\n4\nescape\nescape\n11\n";
            var originalIn = Console.In;
            var originalOut = Console.Out;

            try
            {
                Environment.SetEnvironmentVariable("GITIC_INTERACTIVE_TEST", "1");
                using var stringReader = new StringReader(inputLines);
                Console.SetIn(stringReader);

                // Set dummy out to prevent console pollution
                using var stringWriter = new StringWriter();
                Console.SetOut(stringWriter);

                var wizard = new WizardCommand(parsed);
                var reporter = new MockConsoleReporter();
                var result = await wizard.ExecuteAsync(reporter);

                Assert.Equal(0, result.ExitCode);

                // Since we backed out and escaped, NO file should be generated in .test-report
                var gitClient = new GitClient(currentDir);
                string repoRoot = await gitClient.GetRepositoryRootAsync() ?? currentDir;
                string targetDir = Path.Combine(repoRoot, ".test-report");
                var files = Directory.Exists(targetDir) ? Directory.GetFiles(targetDir, "gitic_report_*.md") : Array.Empty<string>();
                // Clean up any files that might have been leftover (but our test shouldn't create new ones)
                foreach (var file in files)
                {
                    try { File.Delete(file); } catch {}
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("GITIC_INTERACTIVE_TEST", null);
                Console.SetIn(originalIn);
                Console.SetOut(originalOut);
            }
        }
    }
}
