using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Gitic
{
    public class HtmlRenderer : IReportRenderer
    {
        public HtmlRenderer()
        {
        }

        public Task<string> RenderAsync(AnalysisResult result)
        {
            var options = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
            string rawJson = JsonSerializer.Serialize(result, options);
            string data = rawJson.Replace("</script", "<\\/script", StringComparison.OrdinalIgnoreCase);
            string html = ReportTemplateHelper.GetHtmlReportTemplate(data);

            return Task.FromResult(html);
        }
    }
}
