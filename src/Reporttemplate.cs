namespace Gitic;

public static class ReportTemplateHelper
{
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
