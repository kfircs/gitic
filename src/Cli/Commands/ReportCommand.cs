using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gitic;

public class ReportCommand : BaseAnalysisCommand
{
    public ReportCommand(ParsedArgs parsed, IGitClient? gitClient = null, IRepositoryAnalyzer? analyzer = null) 
        : base(parsed, gitClient, analyzer) { }
    protected override AnalysisCommand CommandType => AnalysisCommand.Report;

    protected override async Task<CliResult> ProcessResultAsync(AnalysisResult result, IConsoleReporter? reporter, CancellationToken cancellationToken = default)
    {
        if (Parsed.HtmlPath == null && Parsed.MdPath == null && Parsed.SvgPath == null)
        {
            string errMsg = "report requires --html <path>, --md <path>, or --svg <path>.\n";
            reporter?.WriteError(errMsg);
            return Cli.CliFailure(errMsg, exitCode: 2);
        }

        var outputSb = new StringBuilder();
        var tempFiles = new List<(string TempPath, string TargetPath)>();
        try
        {
            if (Parsed.HtmlPath != null)
            {
                var htmlRenderer = new HtmlRenderer();
                string targetPath = ResolveTargetPath(Parsed.HtmlPath, "report.html");
                
                string dir = Path.GetDirectoryName(targetPath) ?? ".";
                string tempPath = Path.Combine(dir, $".report.html.{Path.GetRandomFileName()}.tmp");
                
                tempFiles.Add((tempPath, targetPath));
                using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
                {
                    await htmlRenderer.RenderToStreamAsync(result, fs, cancellationToken);
                }
                outputSb.Append($"Wrote HTML report to {targetPath}\n");
            }
            if (Parsed.MdPath != null)
            {
                var mdRenderer = new MarkdownRenderer();
                string mdContent = await mdRenderer.RenderAsync(result, cancellationToken);
                string targetPath = ResolveTargetPath(Parsed.MdPath, "report.md");
                
                string dir = Path.GetDirectoryName(targetPath) ?? ".";
                string tempPath = Path.Combine(dir, $".report.md.{Path.GetRandomFileName()}.tmp");
                
                await File.WriteAllTextAsync(tempPath, mdContent, cancellationToken);
                tempFiles.Add((tempPath, targetPath));
                outputSb.Append($"Wrote Markdown report to {targetPath}\n");
            }
            if (Parsed.SvgPath != null)
            {
                var svgSummaryRenderer = new SvgSummaryRenderer();
                var svgComplexityRenderer = new SvgComplexityRenderer();
                string svgContent = await svgSummaryRenderer.RenderAsync(result, cancellationToken);
                string complexitySvgContent = await svgComplexityRenderer.RenderAsync(result, cancellationToken);
                
                string targetPath = ResolveTargetPath(Parsed.SvgPath, "report.svg");
                string targetComplexityPath = Parsed.SvgPath;
                if (Directory.Exists(Parsed.SvgPath))
                {
                    targetComplexityPath = Path.Combine(targetComplexityPath, "report-complexity.svg");
                }
                else
                {
                    string dir = Path.GetDirectoryName(Parsed.SvgPath) ?? ".";
                    string name = Path.GetFileNameWithoutExtension(Parsed.SvgPath);
                    targetComplexityPath = Path.Combine(dir, $"{name}-complexity.svg");
                }
                
                string dirSvg = Path.GetDirectoryName(targetPath) ?? ".";
                string tempPath = Path.Combine(dirSvg, $".report.svg.{Path.GetRandomFileName()}.tmp");

                string dirComp = Path.GetDirectoryName(targetComplexityPath) ?? ".";
                string tempComplexityPath = Path.Combine(dirComp, $".report-complexity.svg.{Path.GetRandomFileName()}.tmp");

                await File.WriteAllTextAsync(tempPath, svgContent, cancellationToken);
                tempFiles.Add((tempPath, targetPath));

                await File.WriteAllTextAsync(tempComplexityPath, complexitySvgContent, cancellationToken);
                tempFiles.Add((tempComplexityPath, targetComplexityPath));

                outputSb.Append($"Wrote SVG report to {targetPath}\nWrote Svg Complexity report to {targetComplexityPath}\n");
            }

            // Move all temporary files into place atomically
            foreach (var pair in tempFiles)
            {
                File.Move(pair.TempPath, pair.TargetPath, overwrite: true);
            }
        }
        catch
        {
            // Clean up any temp files we created
            foreach (var pair in tempFiles)
            {
                try
                {
                    if (File.Exists(pair.TempPath))
                    {
                        File.Delete(pair.TempPath);
                    }
                }
                catch { /* Ignore cleanup errors */ }
            }
            throw;
        }

        string reportOutput = outputSb.ToString();
        reporter?.Write(reportOutput);
        return Cli.CliSuccess(reportOutput);
    }

    private static string ResolveTargetPath(string inputPath, string defaultFileName)
    {
        if (Directory.Exists(inputPath))
        {
            return Path.Combine(inputPath, defaultFileName);
        }
        return inputPath;
    }
}
