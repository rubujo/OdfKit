using System.Web;
using OdfKit.WebFonts.Hosting.SystemWeb;

IHttpHandler handler = new OdfWebFontHandler();
string markup = OdfWebFontHtml.StylesheetLink().ToHtmlString();
if (!handler.IsReusable || !markup.Contains("/_odf-fonts/webfonts.css"))
{
    return 1;
}

Console.WriteLine("PASS: System.Web handler and Web Forms stylesheet helper loaded.");
return 0;
