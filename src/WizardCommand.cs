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
            // Interactive TUI Wizard
            Console.Clear();
            Console.WriteLine("=============================================");
            Console.WriteLine("🚀 Gitic Report Wizard");
            Console.WriteLine("=============================================\n");

            var reportType = PromptSingleSelection(
                "What type of report would you like to generate?",
                new[] { "Curated Report (All sections)", "Custom Report (Select specific sections)" }
            );

            List<string> selectedSections = new List<string>();
            if (reportType == 0) // Curated
            {
                selectedSections = new List<string> {
                    "Work Classification",
                    "Developer Onboarding",
                    "Code Rot",
                    "Review Collaboration",
                    "AI Code Strain"
                };
            }
            else // Custom
            {
                var availableSections = new[] {
                    "Work Classification",
                    "Developer Onboarding",
                    "Code Rot",
                    "Review Collaboration",
                    "AI Code Strain"
                };
                var selections = PromptMultiSelection("Select the sections to include (Space to select, Enter to confirm):", availableSections);
                if (selections.Count == 0)
                {
                    Console.WriteLine("\nNo sections selected. Exiting.");
                    return Cli.CliFailure("No sections selected.");
                }
                foreach (var index in selections)
                {
                    selectedSections.Add(availableSections[index]);
                }
            }

            var formatType = PromptSingleSelection(
                "\nWhich format do you prefer?",
                new[] { "Markdown (.md) with embedded SVGs", "HTML (.html) with embedded SVGs" }
            );

            Console.WriteLine("\nGenerating report with selected sections:");
            foreach (var sec in selectedSections) Console.WriteLine($" - {sec}");
            Console.WriteLine();

            // Run analysis
            var gitClient = new GitClient(_parsed.RepoPath);
            string? repoRoot = await gitClient.GetRepositoryRootAsync(cancellationToken);
            if (repoRoot == null)
            {
                return Cli.CliFailure($"Path {_parsed.RepoPath} is not inside a Git repository.");
            }

            var analyzer = new RepositoryAnalyzer();
            var input = new AnalyzeInput
            {
                RepoRoot = repoRoot,
                Command = AnalysisCommand.GeReport, // using GeReport to trigger all needed data gathering
                Settings = _parsed.Settings
            };
            input.Settings.IncludeMerges = true;

            var result = await analyzer.AnalyzeAsync(input, cancellationToken);

            string extension = formatType == 0 ? "md" : "html";
            string filename = $"gitic_report_{DateTime.Now:yyyyMMdd_HHmmss}.{extension}";
            string targetPath = Path.Combine(repoRoot, filename);

            // Filter rendering. For simplicity in the wizard right now, we can inject a quick markdown generator
            // specifically for the curated reports, or just append the sections to the main report based on selections.
            // Let's generate a dedicated Markdown/HTML containing just the selected sections!
            
            var content = formatType == 0 ? GenerateCustomMarkdown(result, selectedSections) : GenerateCustomHtml(result, selectedSections);
            
            await File.WriteAllTextAsync(targetPath, content, cancellationToken);

            Console.WriteLine($"\n✅ Report generated successfully at: {targetPath}");
            return Cli.CliSuccess($"Report generated at {targetPath}");
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
            sb.AppendLine("<h4>Codebase Ownership Treemap</h4>");
            sb.AppendLine("<div>" + SvgGeneratorHelper.GenerateGeTreemapSvg(result) + "</div>");
            sb.AppendLine("</body></html>");

            return sb.ToString();
        }

        private int PromptSingleSelection(string prompt, string[] options)
        {
            if (Console.IsInputRedirected)
            {
                Console.WriteLine(prompt);
                for (int i = 0; i < options.Length; i++) Console.WriteLine($"[{i}] {options[i]}");
                Console.Write("Enter selection (number): ");
                if (int.TryParse(Console.ReadLine(), out int val) && val >= 0 && val < options.Length) return val;
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

                Console.WriteLine(prompt);
                for (int i = 0; i < options.Length; i++)
                {
                    if (i == currentSelection)
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine($" > {options[i]}".PadRight(Console.WindowWidth));
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.WriteLine($"   {options[i]}".PadRight(Console.WindowWidth));
                    }
                }

                key = Console.ReadKey(true).Key;

                switch (key)
                {
                    case ConsoleKey.UpArrow:
                        currentSelection = (currentSelection == 0) ? options.Length - 1 : currentSelection - 1;
                        break;
                    case ConsoleKey.DownArrow:
                        currentSelection = (currentSelection == options.Length - 1) ? 0 : currentSelection + 1;
                        break;
                }
            } while (key != ConsoleKey.Enter);

            try { Console.CursorVisible = true; } catch { }
            return currentSelection;
        }

        private List<int> PromptMultiSelection(string prompt, string[] options)
        {
            if (Console.IsInputRedirected)
            {
                Console.WriteLine(prompt);
                for (int i = 0; i < options.Length; i++) Console.WriteLine($"[{i}] {options[i]}");
                Console.Write("Enter selections (comma separated numbers): ");
                var line = Console.ReadLine() ?? "";
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

                Console.WriteLine(prompt);
                for (int i = 0; i < options.Length; i++)
                {
                    string checkbox = selected.Contains(i) ? "[x]" : "[ ]";
                    if (i == currentSelection)
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine($" > {checkbox} {options[i]}".PadRight(Console.WindowWidth));
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.WriteLine($"   {checkbox} {options[i]}".PadRight(Console.WindowWidth));
                    }
                }

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
                }
            } while (key != ConsoleKey.Enter);

            try { Console.CursorVisible = true; } catch { }
            return selected.OrderBy(x => x).ToList();
        }
    }
}