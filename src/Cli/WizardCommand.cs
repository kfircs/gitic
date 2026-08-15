using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kfc.Cli.Core;

namespace Gitic
{
    public class WizardCommand : IGiticCommand
    {
        private readonly ParsedArgs _parsed;

        public WizardCommand(ParsedArgs parsed)
        {
            _parsed = parsed;
        }

        public Task<CliResult> ExecuteAsync(IConsoleReporter reporter)
        {
            return ExecuteAsync(reporter, CancellationToken.None);
        }

        public async Task<CliResult> ExecuteAsync(IConsoleReporter? reporter, CancellationToken cancellationToken = default)
        {
            bool isTestMock = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.FullName?.StartsWith("xunit", StringComparison.OrdinalIgnoreCase) == true);
            bool isInteractiveTest = isTestMock && Environment.GetEnvironmentVariable("GITIC_INTERACTIVE_TEST") == "1";
            if ((Console.IsInputRedirected || Console.IsOutputRedirected || Console.IsErrorRedirected) && !isInteractiveTest)
            {
                return await ExecuteNonInteractiveAsync(reporter, cancellationToken);
            }

            bool exit = false;
            while (!exit && !cancellationToken.IsCancellationRequested)
            {
                Console.Clear();
                int winWidth = 80;
                try { if (!Console.IsOutputRedirected && Console.WindowWidth > 0) winWidth = Console.WindowWidth; } catch { }
                int boxWidth = Math.Max(50, Math.Min(80, winWidth));
                string topBorder = "\x1b[38;2;203;166;247m┌" + new string('─', boxWidth - 2) + "┐\x1b[0m";
                string botBorder = "\x1b[38;2;203;166;247m└" + new string('─', boxWidth - 2) + "┘\x1b[0m";
                string midBorder = "\x1b[38;2;203;166;247m├" + new string('─', boxWidth - 2) + "┤\x1b[0m";
                Console.WriteLine(topBorder);
                Console.WriteLine($"\x1b[38;2;203;166;247m│\x1b[0m {ConsoleUtils.PadRightAnsi("\x1b[1m󰚩 Gitic Strategic Codebase Analysis Dashboard\x1b[0m", boxWidth - 4)} \x1b[38;2;203;166;247m│\x1b[0m");
                Console.WriteLine(midBorder);
                Console.WriteLine($"\x1b[38;2;203;166;247m│\x1b[0m {ConsoleUtils.PadRightAnsi($"\x1b[38;2;166;227;161mTarget:\x1b[0m {Path.GetFullPath(_parsed.RepoPath)}", boxWidth - 4)} \x1b[38;2;203;166;247m│\x1b[0m");
                Console.WriteLine(botBorder);
                Console.WriteLine();

                var mainChoice = TuiPrompter.PromptSingleSelection(
                    "Select an analysis view or action:",
                    new[] {
                        "🖥️  Interactive TUI Codebase Explorer (Structure & Reports)",
                        "📊 Generate Curated Report (HTML/Markdown/SVG)",
                        // "🔥 Run Code Hotspots Analysis",
                        // "📂 Run Code Ownership & Areas Analysis",
                        // "👥 Run Contributor Profiles & Metrics",
                        // "👤 Analyze Specific Contributor",
                        // "🔄 Run Temporal Coupling Analysis",
                        // "⏱️ Run Lead-Time Metrics Analysis",
                        // "🌐 Generate Gemini Enterprise (GE) Report",
                        // "🛠️ Generate Starter Config File (.gitic.yml)",
                        "ℹ️ Show Version Information",
                        "❌ Exit"
                    }
                );

                if (mainChoice == 11 || mainChoice == -1) // Exit or EOF/redirected input exhaustion
                {
                    exit = true;
                    break;
                }

                Console.Clear();

                try
                {
                    switch (mainChoice)
                    {
                        case 0: // Interactive TUI Codebase Explorer
                            {
                                var gitClient = new GitClient(_parsed.RepoPath);
                                string? repoRoot = await gitClient.GetRepositoryRootAsync(cancellationToken);
                                if (repoRoot == null)
                                {
                                    Console.WriteLine($"Path {_parsed.RepoPath} is not inside a Git repository.");
                                }
                                else
                                {
                                    Console.WriteLine("Analyzing repository for TUI Explorer...");
                                    var result = await ExecuteAnalysisAsync(repoRoot, cancellationToken);
                                    var explorer = new TuiExplorer();
                                    await explorer.LaunchAsync(result);
                                }
                            }
                            break;
                        case 1: // Generate Curated Report
                            await GenerateCuratedReportAsync(reporter, cancellationToken);
                            break;
                        // case 2: // Run Code Hotspots Analysis
                        //     await new HotspotsCommand(_parsed).ExecuteAsync(reporter, cancellationToken);
                        //     break;
                        // case 3: // Run Code Ownership & Areas Analysis
                        //     await new AreasCommand(_parsed).ExecuteAsync(reporter, cancellationToken);
                        //     break;
                        // case 4: // Run Contributor Profiles & Metrics
                        //     await new ContributorsCommand(_parsed).ExecuteAsync(reporter, cancellationToken);
                        //     break;
                        // case 5: // Analyze Specific Contributor
                        //     Console.Write("Enter contributor name to analyze: ");
                        //     string? name = Console.ReadLine();
                        //     if (string.IsNullOrWhiteSpace(name))
                        //     {
                        //         Console.WriteLine("Invalid contributor name.");
                        //     }
                        //     else
                        //     {
                        //         var parsedContributor = new ParsedArgs
                        //         {
                        //             Command = "contributor",
                        //             RepoPath = _parsed.RepoPath,
                        //             Settings = _parsed.Settings,
                        //             ContributorName = name
                        //         };
                        //         await new ContributorCommand(parsedContributor).ExecuteAsync(reporter, cancellationToken);
                        //     }
                        //     break;
                        // case 6: // Run Temporal Coupling Analysis
                        //     await new TemporalCouplingCommand(_parsed).ExecuteAsync(reporter, cancellationToken);
                        //     break;
                        // case 7: // Run Lead-Time Metrics Analysis
                        //     await new LeadTimeCommand(_parsed).ExecuteAsync(reporter, cancellationToken);
                        //     break;
                        // case 8: // Generate Gemini Enterprise (GE) Report
                        //     await new GeReportCommand(_parsed).ExecuteAsync(reporter, cancellationToken);
                        //     break;
                        // case 9: // Generate Starter Config File (.gitic.yml)
                        //     await new ConfigCommand(new ParsedArgs { Command = "config", ConfigAction = "init" }).ExecuteAsync(reporter, cancellationToken);
                        //     break;
                        case 10: // Show Version Information
                            await new VersionCommand().ExecuteAsync(reporter, cancellationToken);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\x1b[38;2;243;139;168m");
                    Console.WriteLine($"\nError executing command: {ex.Message}");
                    Console.Write("\x1b[0m");
                }

                if (!exit)
                {
                    if (Console.IsInputRedirected || Console.IsOutputRedirected || Console.IsErrorRedirected)
                    {
                        // In redirected environments, read a line if available, but do not block on ReadKey
                        Console.ReadLine();
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine("\nPress any key to return to the main menu...");
                        Console.Write("\x1b[0m");
                        try { Console.ReadKey(true); } catch { }
                    }
                }
            }

            return Cli.CliSuccess("TUI session ended successfully.");
        }

        private async Task<CliResult> ExecuteNonInteractiveAsync(IConsoleReporter? reporter, CancellationToken cancellationToken)
        {
            // In non-interactive mode, if they passed reporting options like --html or --md, we can run the report generation directly!
            if (!string.IsNullOrEmpty(_parsed.HtmlPath) || !string.IsNullOrEmpty(_parsed.MdPath) || !string.IsNullOrEmpty(_parsed.SvgPath))
            {
                var cmd = new ReportCommand(_parsed);
                return await cmd.ExecuteAsync(reporter, cancellationToken);
            }

            // If they passed --json, we can output the raw JSON format of the full analysis
            if (_parsed.Settings.Json || string.Equals(_parsed.Settings.Format, "json", StringComparison.OrdinalIgnoreCase))
            {
                var gitClient = new GitClient(_parsed.RepoPath);
                string? repoRoot = await gitClient.GetRepositoryRootAsync(cancellationToken);
                if (repoRoot == null)
                {
                    string nonRepoMsg = $"Path {_parsed.RepoPath} is not inside a Git repository.\n";
                    reporter?.WriteError(nonRepoMsg);
                    return Cli.CliFailure(nonRepoMsg);
                }
                var result = await ExecuteAnalysisAsync(repoRoot, cancellationToken);
                var jsonRenderer = new JsonRenderer();
                string jsonOutput = await jsonRenderer.RenderAsync(result, cancellationToken);
                reporter?.Write(jsonOutput);
                return Cli.CliSuccess(jsonOutput);
            }

            // Otherwise, print error and exit gracefully or with 2
            string errMsg = "Interactive TUI cannot be run because standard input/output is redirected. Run 'gitic --help' for non-interactive options.\n";
            reporter?.WriteError(errMsg);
            return Cli.CliFailure(errMsg, exitCode: 2);
        }

        private async Task GenerateCuratedReportAsync(IConsoleReporter? reporter, CancellationToken cancellationToken)
        {
            int step = 0;
            int reportType = 0;
            List<string> selectedSections = new List<string>();
            int formatType = 0;

            while (step >= 0 && step <= 2)
            {
                if (cancellationToken.IsCancellationRequested) return;

                // Clear console and print header for each selection step
                Console.Clear();
                int winWidth = 80;
                try { if (!Console.IsOutputRedirected && Console.WindowWidth > 0) winWidth = Console.WindowWidth; } catch { }
                int boxWidth = Math.Max(50, Math.Min(80, winWidth));
                Console.WriteLine($"\x1b[38;2;203;166;247m┌{new string('─', boxWidth - 2)}┐\x1b[0m");
                Console.WriteLine($"\x1b[38;2;203;166;247m│\x1b[0m {ConsoleUtils.PadRightAnsi("\x1b[1m📊 Gitic Report Wizard\x1b[0m", boxWidth - 4)} \x1b[38;2;203;166;247m│\x1b[0m");
                Console.WriteLine($"\x1b[38;2;203;166;247m└{new string('─', boxWidth - 2)}┘\x1b[0m");
                Console.WriteLine();

                if (step == 0)
                {
                    reportType = TuiPrompter.PromptSingleSelection(
                        "What type of report would you like to generate?",
                        new[] {
                            "Developer Onboarding & Collaboration Profile",
                            "Engineering Health & Technical Debt Profile",
                            "Copilot / AI Code Strain Assessment Profile",
                            "Comprehensive Repository Diagnostic (Full)",
                            "Custom Report (Select specific sections)"
                        }
                    );

                    if (reportType == -1)
                    {
                        return; // Exit GenerateCuratedReportAsync back to main menu
                    }

                    if (reportType == 4) // Custom selection
                    {
                        step = 1;
                    }
                    else
                    {
                        // Predefined profiles
                        if (reportType == 0) // Onboarding & Collaboration
                        {
                            selectedSections = new List<string> {
                                "Developer Onboarding",
                                "Review Collaboration"
                            };
                        }
                        else if (reportType == 1) // Health & Tech Debt
                        {
                            selectedSections = new List<string> {
                                "Work Classification",
                                "Code Rot"
                            };
                        }
                        else if (reportType == 2) // AI Code Strain
                        {
                            selectedSections = new List<string> {
                                "Work Classification",
                                "Review Collaboration",
                                "AI Code Strain"
                            };
                        }
                        else if (reportType == 3) // Full Curated
                        {
                            selectedSections = new List<string> {
                                "Work Classification",
                                "Developer Onboarding",
                                "Code Rot",
                                "Review Collaboration",
                                "AI Code Strain"
                            };
                        }
                        step = 2; // skip step 1 (custom selection)
                    }
                }
                else if (step == 1)
                {
                    var availableSections = new[] {
                        "Work Classification",
                        "Developer Onboarding",
                        "Code Rot",
                        "Review Collaboration",
                        "AI Code Strain"
                    };
                    var selections = TuiPrompter.PromptMultiSelection("Select the sections to include (Space to select, Enter to confirm):", availableSections);
                    if (selections.Contains(-1))
                    {
                        step = 0; // go back to choosing report type
                        continue;
                    }
                    if (selections.Count == 0)
                    {
                        Console.WriteLine("\nNo sections selected. Please select at least one section.");
                        // Let's pause briefly so they see it
                        await Task.Delay(1000, cancellationToken);
                        continue;
                    }
                    selectedSections = new List<string>();
                    foreach (var index in selections)
                    {
                        selectedSections.Add(availableSections[index]);
                    }
                    step = 2;
                }
                else if (step == 2)
                {
                    formatType = TuiPrompter.PromptSingleSelection(
                        "Which format do you prefer?",
                        new[] { "Markdown (.md) with embedded SVGs", "HTML (.html) with embedded SVGs" }
                    );

                    if (formatType == -1)
                    {
                        if (reportType == 4)
                        {
                            step = 1; // go back to custom sections selection
                        }
                        else
                        {
                            step = 0; // go back to choosing report type
                        }
                        continue;
                    }

                    // Done with selections, proceed to generate
                    step = 3;
                }
            }

            if (step != 3) return; // cancelled or exited

            Console.WriteLine("\nGenerating report with selected sections:");
            foreach (var sec in selectedSections) Console.WriteLine($" - {sec}");
            Console.WriteLine();

            // Run analysis
            var gitClient = new GitClient(_parsed.RepoPath);
            string? repoRoot = await gitClient.GetRepositoryRootAsync(cancellationToken);
            if (repoRoot == null)
            {
                Console.WriteLine($"Path {_parsed.RepoPath} is not inside a Git repository.");
                return;
            }

            var result = await ExecuteAnalysisAsync(repoRoot, cancellationToken);

            string extension = formatType == 0 ? "md" : "html";
            string filename = $"gitic_report_{DateTime.Now:yyyyMMdd_HHmmss}.{extension}";
            string targetDir = Path.Combine(repoRoot, ".test-report");
            Directory.CreateDirectory(targetDir);
            string targetPath = Path.Combine(targetDir, filename);

            var content = formatType == 0 ? CustomReportGenerator.GenerateCustomMarkdown(result, selectedSections) : CustomReportGenerator.GenerateCustomHtml(result, selectedSections);

            await File.WriteAllTextAsync(targetPath, content, cancellationToken);

            Console.WriteLine($"\n✅ Report generated successfully at: {targetPath}");
        }

        private async Task<AnalysisResult> ExecuteAnalysisAsync(string repoRoot, CancellationToken cancellationToken)
        {
            var analyzer = new RepositoryAnalyzer();
            var input = new AnalyzeInput
            {
                RepoRoot = repoRoot,
                Command = AnalysisCommand.GeReport, // using GeReport to trigger all needed data gathering
                Settings = _parsed.Settings
            };
            input.Settings.IncludeMerges = true;

            return await analyzer.AnalyzeAsync(input, cancellationToken);
        }

    }
}
