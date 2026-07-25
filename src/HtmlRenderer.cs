using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Gitic
{
    public class HtmlRenderer : IReportRenderer
    {
        private readonly string _htmlPath;

        public HtmlRenderer(string htmlPath)
        {
            _htmlPath = htmlPath;
        }

        public async Task<string> RenderAsync(AnalysisResult result)
        {
            var options = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
            string rawJson = JsonSerializer.Serialize(result, options);
            string data = rawJson.Replace("</script", "<\\/script", StringComparison.OrdinalIgnoreCase);
            string html = ReportTemplateHelper.GetHtmlReportTemplate(data);

            string targetPath = _htmlPath;
            if (Directory.Exists(targetPath))
            {
                targetPath = Path.Combine(targetPath, "report.html");
            }

            await File.WriteAllTextAsync(targetPath, html);
            return $"Wrote HTML report to {targetPath}\n";
        }
    }
}
