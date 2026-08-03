using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gitic
{
    public class WizardCommand : ICliCommand
    {
        private readonly ParsedArgs _parsed;

        public WizardCommand(ParsedArgs parsed)
        {
            _parsed = parsed;
        }

        public async Task<CliResult> ExecuteAsync(IConsoleReporter? reporter, CancellationToken cancellationToken = default)
        {
            bool isTestMock = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.FullName?.StartsWith("xunit", StringComparison.OrdinalIgnoreCase) == true);
            bool isInteractiveTest = isTestMock && Environment.GetEnvironmentVariable("GITIC_INTERACTIVE_TEST") == "1";
            if ((Console.IsInputRedirected || Console.IsOutputRedirected || Console.IsErrorRedirected) && !isInteractiveTest)
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

            bool exit = false;
            while (!exit && !cancellationToken.IsCancellationRequested)
            {
                Console.Clear();
                int winWidth = 80;
                try { if (!Console.IsOutputRedirected && Console.WindowWidth > 0) winWidth = Console.WindowWidth; } catch {}
                int boxWidth = Math.Max(50, Math.Min(80, winWidth));
                string topBorder = "\x1b[38;2;203;166;247m┌" + new string('─', boxWidth - 2) + "┐\x1b[0m";
                string botBorder = "\x1b[38;2;203;166;247m└" + new string('─', boxWidth - 2) + "┘\x1b[0m";
                string midBorder = "\x1b[38;2;203;166;247m├" + new string('─', boxWidth - 2) + "┤\x1b[0m";
                Console.WriteLine(topBorder);
                Console.WriteLine($"\x1b[38;2;203;166;247m│\x1b[0m {PadRightAnsi("\x1b[1m󰚩 Gitic Strategic Codebase Analysis Dashboard\x1b[0m", boxWidth - 4)} \x1b[38;2;203;166;247m│\x1b[0m");
                Console.WriteLine(midBorder);
                Console.WriteLine($"\x1b[38;2;203;166;247m│\x1b[0m {PadRightAnsi($"\x1b[38;2;166;227;161mTarget:\x1b[0m {Path.GetFullPath(_parsed.RepoPath)}", boxWidth - 4)} \x1b[38;2;203;166;247m│\x1b[0m");
                Console.WriteLine(botBorder);
                Console.WriteLine();

                var mainChoice = PromptSingleSelection(
                    "Select an analysis view or action:",
                    new[] {
                        "📊 Generate Curated Report (HTML/Markdown/SVG)",
                        "🔥 Run Code Hotspots Analysis",
                        "📂 Run Code Ownership & Areas Analysis",
                        "👥 Run Contributor Profiles & Metrics",
                        "👤 Analyze Specific Contributor",
                        "🔄 Run Temporal Coupling Analysis",
                        "⏱️ Run Lead-Time Metrics Analysis",
                        "🌐 Generate Gemini Enterprise (GE) Report",
                        "🛠️ Generate Starter Config File (.gitic.yml)",
                        "ℹ️ Show Version Information",
                        "❌ Exit"
                    }
                );

                if (mainChoice == 10 || mainChoice == -1) // Exit or EOF/redirected input exhaustion
                {
                    exit = true;
                    break;
                }

                Console.Clear();

                try
                {
                    switch (mainChoice)
                    {
                        case 0: // Generate Curated Report
                            await GenerateCuratedReportAsync(reporter, cancellationToken);
                            break;
                        case 1: // Run Code Hotspots Analysis
                            await new HotspotsCommand(_parsed).ExecuteAsync(reporter, cancellationToken);
                            break;
                        case 2: // Run Code Ownership & Areas Analysis
                            await new AreasCommand(_parsed).ExecuteAsync(reporter, cancellationToken);
                            break;
                        case 3: // Run Contributor Profiles & Metrics
                            await new ContributorsCommand(_parsed).ExecuteAsync(reporter, cancellationToken);
                            break;
                        case 4: // Analyze Specific Contributor
                            Console.Write("Enter contributor name to analyze: ");
                            string? name = Console.ReadLine();
                            if (string.IsNullOrWhiteSpace(name))
                            {
                                Console.WriteLine("Invalid contributor name.");
                            }
                            else
                            {
                                var parsedContributor = new ParsedArgs
                                {
                                    Command = "contributor",
                                    RepoPath = _parsed.RepoPath,
                                    Settings = _parsed.Settings,
                                    ContributorName = name
                                };
                                await new ContributorCommand(parsedContributor).ExecuteAsync(reporter, cancellationToken);
                            }
                            break;
                        case 5: // Run Temporal Coupling Analysis
                            await new TemporalCouplingCommand(_parsed).ExecuteAsync(reporter, cancellationToken);
                            break;
                        case 6: // Run Lead-Time Metrics Analysis
                            await new LeadTimeCommand(_parsed).ExecuteAsync(reporter, cancellationToken);
                            break;
                        case 7: // Generate Gemini Enterprise (GE) Report
                            await new GeReportCommand(_parsed).ExecuteAsync(reporter, cancellationToken);
                            break;
                        case 8: // Generate Starter Config File (.gitic.yml)
                            await new ConfigCommand(new ParsedArgs { Command = "config", ConfigAction = "init" }).ExecuteAsync(reporter, cancellationToken);
                            break;
                        case 9: // Show Version Information
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
                try { if (!Console.IsOutputRedirected && Console.WindowWidth > 0) winWidth = Console.WindowWidth; } catch {}
                int boxWidth = Math.Max(50, Math.Min(80, winWidth));
                Console.WriteLine($"\x1b[38;2;203;166;247m┌{new string('─', boxWidth - 2)}┐\x1b[0m");
                Console.WriteLine($"\x1b[38;2;203;166;247m│\x1b[0m {PadRightAnsi("\x1b[1m📊 Gitic Report Wizard\x1b[0m", boxWidth - 4)} \x1b[38;2;203;166;247m│\x1b[0m");
                Console.WriteLine($"\x1b[38;2;203;166;247m└{new string('─', boxWidth - 2)}┘\x1b[0m");
                Console.WriteLine();

                if (step == 0)
                {
                    reportType = PromptSingleSelection(
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
                    var selections = PromptMultiSelection("Select the sections to include (Space to select, Enter to confirm):", availableSections);
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
                    formatType = PromptSingleSelection(
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

            var content = formatType == 0 ? GenerateCustomMarkdown(result, selectedSections) : GenerateCustomHtml(result, selectedSections);
            
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

        private string GenerateCustomMarkdown(AnalysisResult result, List<string> sections)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Gitic Custom Report");
            sb.AppendLine($"*Generated at {DateTime.Now}*");
            sb.AppendLine();
            
            if (result.CuratedReports == null) return sb.ToString();

            if (sections.Contains("Work Classification"))
            {
                sb.AppendLine("## 1. Work Classification");
                sb.AppendLine($"* **Features:** {result.CuratedReports.WorkClassification.Features}");
                sb.AppendLine($"* **Bugs:** {result.CuratedReports.WorkClassification.Bugs}");
                sb.AppendLine($"* **Technical Debt:** {result.CuratedReports.WorkClassification.TechnicalDebt}");
                sb.AppendLine($"* **Chores:** {result.CuratedReports.WorkClassification.Chores}");
                sb.AppendLine($"* **Unclassified:** {result.CuratedReports.WorkClassification.Unclassified}");
                sb.AppendLine();
            }

            if (sections.Contains("Developer Onboarding"))
            {
                sb.AppendLine("## 2. Developer Onboarding (TTFC)");
                foreach (var dev in result.CuratedReports.Onboarding.Take(10))
                {
                    sb.AppendLine($"* **{dev.Developer}**: First commit on {dev.FirstCommitDate} ({dev.DaysActive} days active)");
                }
                sb.AppendLine();
            }

            if (sections.Contains("Code Rot"))
            {
                sb.AppendLine("## 3. Code Rot");
                sb.AppendLine($"* **Zombie Files (>1yr):** {result.CuratedReports.CodeRot.ZombieFileCount}");
                sb.AppendLine($"* **Zombie Lines (>1yr):** {result.CuratedReports.CodeRot.ZombieLines}");
                sb.AppendLine();
            }

            if (sections.Contains("Review Collaboration"))
            {
                sb.AppendLine("## 4. Review Collaboration & Silos");
                sb.AppendLine($"* **Reviewer Silos (Single-reviewer):** {result.CuratedReports.ReviewCollaboration.ReviewerSilos}");
                sb.AppendLine();
                sb.AppendLine("### Top Review Pairs");
                foreach (var pair in result.CuratedReports.ReviewCollaboration.Pairs.Take(5))
                {
                    sb.AppendLine($"* {pair.Author} reviewed by {pair.Reviewer} ({pair.PrCount} PRs)");
                }
                sb.AppendLine();
            }

            if (sections.Contains("AI Code Strain"))
            {
                sb.AppendLine("## 5. AI Code Strain");
                sb.AppendLine($"* **High-Volume Commits (>20 files):** {result.CuratedReports.AiCodeStrain.HighVolumeCommits}");
                if (result.CuratedReports.AiCodeStrain.ReviewVelocityWarning)
                {
                    sb.AppendLine("> ⚠️ **WARNING**: High proportion of large commits detected. Review capacity may be strained.");
                }
                sb.AppendLine();
            }

            sb.AppendLine("---");
            sb.AppendLine("### Repository Visualization");
            sb.AppendLine("#### Codebase Ownership Treemap");
            sb.AppendLine(SvgGeneratorHelper.GenerateGeTreemapSvg(result));
            sb.AppendLine();

            return sb.ToString();
        }

        private string GenerateCustomHtml(AnalysisResult result, List<string> sections)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<html><head><style>body { font-family: sans-serif; padding: 20px; } h2 { color: #333; border-bottom: 1px solid #ccc; } svg { max-width: 100%; height: auto; }</style></head><body>");
            sb.AppendLine("<h1>Gitic Custom Report</h1>");
            sb.AppendLine($"<p><em>Generated at {DateTime.Now}</em></p>");
            
            if (result.CuratedReports != null)
            {
                if (sections.Contains("Work Classification"))
                {
                    sb.AppendLine("<h2>1. Work Classification</h2><ul>");
                    sb.AppendLine($"<li><strong>Features:</strong> {result.CuratedReports.WorkClassification.Features}</li>");
                    sb.AppendLine($"<li><strong>Bugs:</strong> {result.CuratedReports.WorkClassification.Bugs}</li>");
                    sb.AppendLine($"<li><strong>Technical Debt:</strong> {result.CuratedReports.WorkClassification.TechnicalDebt}</li>");
                    sb.AppendLine($"<li><strong>Chores:</strong> {result.CuratedReports.WorkClassification.Chores}</li>");
                    sb.AppendLine($"<li><strong>Unclassified:</strong> {result.CuratedReports.WorkClassification.Unclassified}</li>");
                    sb.AppendLine("</ul>");
                }

                if (sections.Contains("Developer Onboarding"))
                {
                    sb.AppendLine("<h2>2. Developer Onboarding (TTFC)</h2><ul>");
                    foreach (var dev in result.CuratedReports.Onboarding.Take(10))
                    {
                        sb.AppendLine($"<li><strong>{dev.Developer}</strong>: First commit on {dev.FirstCommitDate} ({dev.DaysActive} days active)</li>");
                    }
                    sb.AppendLine("</ul>");
                }

                if (sections.Contains("Code Rot"))
                {
                    sb.AppendLine("<h2>3. Code Rot</h2><ul>");
                    sb.AppendLine($"<li><strong>Zombie Files (&gt;1yr):</strong> {result.CuratedReports.CodeRot.ZombieFileCount}</li>");
                    sb.AppendLine($"<li><strong>Zombie Lines (&gt;1yr):</strong> {result.CuratedReports.CodeRot.ZombieLines}</li>");
                    sb.AppendLine("</ul>");
                }

                if (sections.Contains("Review Collaboration"))
                {
                    sb.AppendLine("<h2>4. Review Collaboration & Silos</h2>");
                    sb.AppendLine($"<p><strong>Reviewer Silos (Single-reviewer):</strong> {result.CuratedReports.ReviewCollaboration.ReviewerSilos}</p>");
                    sb.AppendLine("<h3>Top Review Pairs</h3><ul>");
                    foreach (var pair in result.CuratedReports.ReviewCollaboration.Pairs.Take(5))
                    {
                        sb.AppendLine($"<li>{pair.Author} reviewed by {pair.Reviewer} ({pair.PrCount} PRs)</li>");
                    }
                    sb.AppendLine("</ul>");
                }

                if (sections.Contains("AI Code Strain"))
                {
                    sb.AppendLine("<h2>5. AI Code Strain</h2><ul>");
                    sb.AppendLine($"<li><strong>High-Volume Commits (&gt;20 files):</strong> {result.CuratedReports.AiCodeStrain.HighVolumeCommits}</li>");
                    if (result.CuratedReports.AiCodeStrain.ReviewVelocityWarning)
                    {
                        sb.AppendLine("<li style='color:red'><strong>⚠️ WARNING:</strong> High proportion of large commits detected. Review capacity may be strained.</li>");
                    }
                    sb.AppendLine("</ul>");
                }
            }

            sb.AppendLine("<hr/>");
            sb.AppendLine("<h3>Repository Visualization</h3>");
            sb.AppendLine("#### Codebase Ownership Treemap</h4>");
            sb.AppendLine("<div>" + SvgGeneratorHelper.GenerateGeTreemapSvg(result) + "</div>");
            sb.AppendLine("</body></html>");

            return sb.ToString();
        }

        
        private static string PadRightAnsi(string text, int totalWidth)
        {
            int visibleLength = 0;
            bool inEscape = false;
            foreach (char c in text)
            {
                if (c == '') inEscape = true;
                else if (inEscape && c == 'm') inEscape = false;
                else if (!inEscape) visibleLength++;
            }
            int paddingCount = totalWidth - visibleLength;
            if (paddingCount > 0)
            {
                return text + new string(' ', paddingCount);
            }
            return text;
        }

        private int PromptSingleSelection(string prompt, string[] options)
        {
            if (Console.IsInputRedirected || Console.IsOutputRedirected || Console.IsErrorRedirected)
            {
                Console.WriteLine(prompt);
                for (int i = 0; i < options.Length; i++) Console.WriteLine($"[{i}] {options[i]}");
                Console.Write("Enter selection (number): ");
                string? line = Console.ReadLine();
                if (line == null || line == "back" || line == "escape") return -1;
                if (int.TryParse(line, out int val) && val >= 0 && val < options.Length) return val;
                return 0;
            }

            int currentSelection = 0;
            ConsoleKey key;
            int startTop = Console.CursorTop;
            bool firstDraw = true;

            try { Console.CursorVisible = false; } catch { }

            do
            {
                if (!firstDraw)
                {
                    try { Console.SetCursorPosition(0, startTop); } catch { }
                }
                firstDraw = false;

                int winW = 80;
                try { if (!Console.IsOutputRedirected && Console.WindowWidth > 0) winW = Console.WindowWidth; } catch {}
                int bWidth = Math.Max(50, Math.Min(80, winW));
                Console.WriteLine($"\x1b[38;2;249;226;175m┌{new string('─', bWidth - 2)}┐\x1b[0m");
                Console.WriteLine($"\x1b[38;2;249;226;175m│\x1b[0m {PadRightAnsi($"\x1b[1m󰜎 {prompt}\x1b[0m", bWidth - 4)} \x1b[38;2;249;226;175m│\x1b[0m");
                Console.WriteLine($"\x1b[38;2;249;226;175m├{new string('─', bWidth - 2)}┤\x1b[0m");
                for (int i = 0; i < options.Length; i++)
                {
                    if (i == currentSelection)
                    {
                        Console.WriteLine($"\x1b[38;2;249;226;175m│\x1b[0m {PadRightAnsi($"\x1b[38;2;137;180;250m󰅂\x1b[0m \x1b[1;38;2;137;180;250m{options[i]}\x1b[0m", bWidth - 4)} \x1b[38;2;249;226;175m│\x1b[0m");
                    }
                    else
                    {
                        Console.WriteLine($"\x1b[38;2;249;226;175m│\x1b[0m {PadRightAnsi($"  {options[i]}", bWidth - 4)} \x1b[38;2;249;226;175m│\x1b[0m");
                    }
                }
                Console.WriteLine($"\x1b[38;2;249;226;175m├{new string('─', bWidth - 2)}┤\x1b[0m");
                Console.WriteLine($"\x1b[38;2;249;226;175m│\x1b[0m {PadRightAnsi("\x1b[38;2;108;112;147m󰌑 Up/Down: Navigate │ Enter: Select\x1b[0m", bWidth - 4)} \x1b[38;2;249;226;175m│\x1b[0m");
                Console.WriteLine($"\x1b[38;2;249;226;175m└{new string('─', bWidth - 2)}┘\x1b[0m");

                key = Console.ReadKey(true).Key;

                switch (key)
                {
                    case ConsoleKey.UpArrow:
                        currentSelection = (currentSelection == 0) ? options.Length - 1 : currentSelection - 1;
                        break;
                    case ConsoleKey.DownArrow:
                        currentSelection = (currentSelection == options.Length - 1) ? 0 : currentSelection + 1;
                        break;
                    case ConsoleKey.Escape:
                        try { Console.CursorVisible = true; } catch { }
                        return -1;
                }
            } while (key != ConsoleKey.Enter);

            try { Console.CursorVisible = true; } catch { }
            return currentSelection;
        }

        private List<int> PromptMultiSelection(string prompt, string[] options)
        {
            if (Console.IsInputRedirected || Console.IsOutputRedirected || Console.IsErrorRedirected)
            {
                Console.WriteLine(prompt);
                for (int i = 0; i < options.Length; i++) Console.WriteLine($"[{i}] {options[i]}");
                Console.Write("Enter selections (comma separated numbers): ");
                var line = Console.ReadLine();
                if (line == null || line == "back" || line == "escape") return new List<int> { -1 };
                var parts = line.Split(',', StringSplitOptions.RemoveEmptyEntries);
                var sel = parts.Select(p => int.TryParse(p, out int v) ? v : -1).Where(v => v >= 0 && v < options.Length).ToList();
                return sel.Count > 0 ? sel : new List<int> { 0 };
            }

            int currentSelection = 0;
            HashSet<int> selected = new HashSet<int>();
            ConsoleKey key;
            int startTop = Console.CursorTop;
            bool firstDraw = true;

            try { Console.CursorVisible = false; } catch { }

            do
            {
                if (!firstDraw)
                {
                    try { Console.SetCursorPosition(0, startTop); } catch { }
                }
                firstDraw = false;

                int winW = 80;
                try { if (!Console.IsOutputRedirected && Console.WindowWidth > 0) winW = Console.WindowWidth; } catch {}
                int bWidth = Math.Max(50, Math.Min(80, winW));
                Console.WriteLine($"\x1b[38;2;249;226;175m┌{new string('─', bWidth - 2)}┐\x1b[0m");
                Console.WriteLine($"\x1b[38;2;249;226;175m│\x1b[0m {PadRightAnsi($"\x1b[1m󰜎 {prompt}\x1b[0m", bWidth - 4)} \x1b[38;2;249;226;175m│\x1b[0m");
                Console.WriteLine($"\x1b[38;2;249;226;175m├{new string('─', bWidth - 2)}┤\x1b[0m");
                for (int i = 0; i < options.Length; i++)
                {
                    string checkbox = selected.Contains(i) ? "󰄲" : "󰄱";
                    if (i == currentSelection)
                    {
                        Console.WriteLine($"\x1b[38;2;249;226;175m│\x1b[0m {PadRightAnsi($"\x1b[38;2;137;180;250m󰅂\x1b[0m {checkbox} \x1b[1;38;2;137;180;250m{options[i]}\x1b[0m", bWidth - 4)} \x1b[38;2;249;226;175m│\x1b[0m");
                    }
                    else
                    {
                        Console.WriteLine($"\x1b[38;2;249;226;175m│\x1b[0m {PadRightAnsi($"  {checkbox} {options[i]}", bWidth - 4)} \x1b[38;2;249;226;175m│\x1b[0m");
                    }
                }
                Console.WriteLine($"\x1b[38;2;249;226;175m├{new string('─', bWidth - 2)}┤\x1b[0m");
                Console.WriteLine($"\x1b[38;2;249;226;175m│\x1b[0m {PadRightAnsi("\x1b[38;2;108;112;147m󰌑 Up/Down: Navigate │ Space: Toggle │ Enter: Select\x1b[0m", bWidth - 4)} \x1b[38;2;249;226;175m│\x1b[0m");
                Console.WriteLine($"\x1b[38;2;249;226;175m└{new string('─', bWidth - 2)}┘\x1b[0m");

                key = Console.ReadKey(true).Key;

                switch (key)
                {
                    case ConsoleKey.UpArrow:
                        currentSelection = (currentSelection == 0) ? options.Length - 1 : currentSelection - 1;
                        break;
                    case ConsoleKey.DownArrow:
                        currentSelection = (currentSelection == options.Length - 1) ? 0 : currentSelection + 1;
                        break;
                    case ConsoleKey.Spacebar:
                        if (selected.Contains(currentSelection))
                            selected.Remove(currentSelection);
                        else
                            selected.Add(currentSelection);
                        break;
                    case ConsoleKey.Escape:
                        try { Console.CursorVisible = true; } catch { }
                        return new List<int> { -1 };
                }
            } while (key != ConsoleKey.Enter);

            try { Console.CursorVisible = true; } catch { }
            return selected.OrderBy(x => x).ToList();
        }
    }
}
