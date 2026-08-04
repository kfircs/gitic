using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Gitic.Tests
{
    public class ConformanceAndPerformanceTests
    {
        private readonly string _dllPath;
        private readonly int _helpLimitMs;
        private readonly int _versionLimitMs;
        private readonly int _memoryLimitMb;

        public ConformanceAndPerformanceTests()
        {
            string baseDir = AppContext.BaseDirectory;
            _dllPath = Path.GetFullPath(Path.Combine(baseDir, "../../../../../bin/Debug/net10.0/Gitic.dll"));
            if (!File.Exists(_dllPath))
            {
                _dllPath = Path.Combine(baseDir, "Gitic.dll");
            }

            string baselinePath = Path.GetFullPath(Path.Combine(baseDir, "../../../Integration/conformance_baseline.json"));
            if (File.Exists(baselinePath))
            {
                var json = File.ReadAllText(baselinePath);
                using var doc = JsonDocument.Parse(json);
                _helpLimitMs = doc.RootElement.GetProperty("StartupHelpLimitMs").GetInt32();
                _versionLimitMs = doc.RootElement.GetProperty("StartupVersionLimitMs").GetInt32();
                _memoryLimitMb = doc.RootElement.GetProperty("MemoryBudgetMb").GetInt32();
            }
            else
            {
                _helpLimitMs = 300;
                _versionLimitMs = 300;
                _memoryLimitMb = 50;
            }
        }

        private async Task<(int ExitCode, string Stdout, string Stderr, long ElapsedMs)> RunGiticProcessAsync(
            string arguments, 
            Dictionary<string, string>? envVars = null)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"\"{_dllPath}\" {arguments}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            if (envVars != null)
            {
                foreach (var kv in envVars)
                {
                    psi.Environment[kv.Key] = kv.Value;
                }
            }

            using var process = new Process { StartInfo = psi };
            var sw = Stopwatch.StartNew();
            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await Task.WhenAll(stdoutTask, stderrTask);
            string stdout = stdoutTask.Result;
            string stderr = stderrTask.Result;
            await process.WaitForExitAsync();
            sw.Stop();

            return (process.ExitCode, stdout, stderr, sw.ElapsedMilliseconds);
        }

        [Fact]
        public async Task TestStartupPerformance_HelpAndVersion()
        {
            var helpRes = await RunGiticProcessAsync("--help");
            Assert.Equal(0, helpRes.ExitCode);
            Assert.NotEmpty(helpRes.Stdout);
            Assert.True(helpRes.ElapsedMs <= _helpLimitMs, 
                $"Help command startup took {helpRes.ElapsedMs}ms, exceeding budget of {_helpLimitMs}ms.");

            var versionRes = await RunGiticProcessAsync("--version");
            Assert.Equal(0, versionRes.ExitCode);
            Assert.NotEmpty(versionRes.Stdout);
            Assert.True(versionRes.ElapsedMs <= _versionLimitMs, 
                $"Version command startup took {versionRes.ElapsedMs}ms, exceeding budget of {_versionLimitMs}ms.");
        }

        [Fact]
        public async Task TestCLI_ContractConformance()
        {
            var invalidRes = await RunGiticProcessAsync("nonexistent-command extra-arg");
            Assert.Equal(2, invalidRes.ExitCode);
            Assert.NotEmpty(invalidRes.Stderr);

            // Test explicit no-color / contract execution on root via json output
            var jsonRes = await RunGiticProcessAsync(". --json");
            Assert.Equal(0, jsonRes.ExitCode);
            Assert.NotEmpty(jsonRes.Stdout);
            Assert.False(jsonRes.Stdout.Contains("\u001b"), "Stdout should not contain ANSI escape codes.");
        }

        [Fact]
        public void TestTerminalFormatter_DirectFallbackTests()
        {
            // Direct unit testing of capability checks in TerminalFormatter
            var settings = DefaultAnalysisSettings.Create();
            settings.Color = "auto";
            settings.Format = "plain";

            var formatter = new TerminalFormatter(settings);
            string formatted = formatter.FormatAttention(85.0, "high");
            Assert.False(formatted.Contains("\u001b"), "Formatted plain attention should not contain escape codes.");

            // Test auto formatting on TERM=dumb direct check simulation
            Environment.SetEnvironmentVariable("TERM", "dumb");
            try
            {
                var settingsAuto = DefaultAnalysisSettings.Create();
                settingsAuto.Color = "auto";
                var dumbFormatter = new TerminalFormatter(settingsAuto);
                string text = dumbFormatter.FormatAttention(85.0, "high");
                Assert.False(text.Contains("\u001b"), "TERM=dumb should not contain escape codes.");
            }
            finally
            {
                Environment.SetEnvironmentVariable("TERM", null);
            }

            // Test auto formatting on NO_COLOR direct check simulation
            Environment.SetEnvironmentVariable("NO_COLOR", "1");
            try
            {
                var settingsAuto = DefaultAnalysisSettings.Create();
                settingsAuto.Color = "auto";
                var noColorFormatter = new TerminalFormatter(settingsAuto);
                string text = noColorFormatter.FormatAttention(85.0, "high");
                Assert.False(text.Contains("\u001b"), "NO_COLOR should not contain escape codes.");
            }
            finally
            {
                Environment.SetEnvironmentVariable("NO_COLOR", null);
            }
        }

        [Fact]
        public void TestSchemaCompatibility_JsonStability()
        {
            var result = new AnalysisResult
            {
                SchemaVersion = "1.1",
                Tool = "gitic",
                Analysis = new AnalysisMetadata { RepoRoot = "/fake/root", CommitCount = 5 },
                Warnings = new List<string> { "Test Warning" },
                Diagnostics = new List<Diagnostic>
                {
                    new() { Code = "GITIC001", Severity = "Warning", Message = "Test Message", Hint = "Test Hint" }
                }
            };

            var options = Gitic.JsonSerializationDefaults.Indented;
            string json = JsonSerializer.Serialize(result, options);
            
            Assert.Contains("\"schema_version\": \"1.1\"", json);
            Assert.Contains("\"diagnostics\":", json);

            var deserialized = JsonSerializer.Deserialize<AnalysisResult>(json);
            Assert.NotNull(deserialized);
            Assert.Equal("1.1", deserialized.SchemaVersion);
            Assert.Single(deserialized.Diagnostics);
            Assert.Equal("GITIC001", deserialized.Diagnostics[0].Code);
            Assert.Equal("Warning", deserialized.Diagnostics[0].Severity);
        }
    }
}