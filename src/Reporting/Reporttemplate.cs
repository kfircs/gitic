using System;
using System.IO;
using System.Threading.Tasks;

namespace Gitic;

public static class ReportTemplateHelper
{
    // clean code refactor
    public static string GetHtmlReportTemplate(string resultJson)
    {
        return DashboardTemplateEngine.Generate(resultJson);
    }
}

public static class DashboardTemplateEngine
{
    public static string Generate(string resultJson)
    {
        var css = GetCssThemes();
        var clientScript = GetClientScript(resultJson);
        var body = GetHtmlBody();
        return GetHtmlLayout(body, css, clientScript);
    }

    public static async Task RenderToStreamAsync(string resultJson, Stream output)
    {
        using var writer = new StreamWriter(output, System.Text.Encoding.UTF8, bufferSize: 4096, leaveOpen: true);

        string layout = TemplateAssets.HtmlLayout;
        string css = GetCssThemes();
        string body = GetHtmlBody();
        string clientScriptTemplate = TemplateAssets.ClientScriptTemplate;

        // Write layout until __CSS__
        int cssIndex = layout.IndexOf("__CSS__", StringComparison.Ordinal);
        if (cssIndex == -1)
        {
            await writer.WriteAsync(layout);
            return;
        }
        await writer.WriteAsync(layout.AsMemory(0, cssIndex));

        // Write css content
        await writer.WriteAsync(css);

        // Write layout between __CSS__ and __BODY__
        int bodyIndex = layout.IndexOf("__BODY__", StringComparison.Ordinal);
        int afterCssIndex = cssIndex + "__CSS__".Length;
        if (bodyIndex == -1)
        {
            await writer.WriteAsync(layout.AsMemory(afterCssIndex));
            return;
        }
        await writer.WriteAsync(layout.AsMemory(afterCssIndex, bodyIndex - afterCssIndex));

        // Write body content
        await writer.WriteAsync(body);

        // Write layout between __BODY__ and __CLIENT_SCRIPT__
        int clientScriptIndex = layout.IndexOf("__CLIENT_SCRIPT__", StringComparison.Ordinal);
        int afterBodyIndex = bodyIndex + "__BODY__".Length;
        if (clientScriptIndex == -1)
        {
            await writer.WriteAsync(layout.AsMemory(afterBodyIndex));
            return;
        }
        await writer.WriteAsync(layout.AsMemory(afterBodyIndex, clientScriptIndex - afterBodyIndex));

        // Write ClientScriptTemplate split by __RESULT_JSON__
        int jsonIndex = clientScriptTemplate.IndexOf("__RESULT_JSON__", StringComparison.Ordinal);
        if (jsonIndex == -1)
        {
            await writer.WriteAsync(clientScriptTemplate);
        }
        else
        {
            await writer.WriteAsync(clientScriptTemplate.AsMemory(0, jsonIndex));
            await writer.WriteAsync(resultJson);
            int afterJsonIndex = jsonIndex + "__RESULT_JSON__".Length;
            await writer.WriteAsync(clientScriptTemplate.AsMemory(afterJsonIndex));
        }

        // Write remaining layout after __CLIENT_SCRIPT__
        int afterClientScriptIndex = clientScriptIndex + "__CLIENT_SCRIPT__".Length;
        await writer.WriteAsync(layout.AsMemory(afterClientScriptIndex));

        await writer.FlushAsync();
    }

    public static string GetCssThemes()
    {
        return TemplateAssets.CssThemes;
    }

    public static string GetHtmlBody()
    {
        return TemplateAssets.HtmlBody;
    }

    public static string GetClientScript(string resultJson)
    {
        return TemplateAssets.ClientScriptTemplate.Replace("__RESULT_JSON__", resultJson);
    }

    public static string GetHtmlLayout(string body, string css, string clientScript)
    {
        return TemplateAssets.HtmlLayout
            .Replace("__CSS__", css)
            .Replace("__BODY__", body)
            .Replace("__CLIENT_SCRIPT__", clientScript);
    }
}
// Refactored: Candidate 6
// Clean code review completed.

