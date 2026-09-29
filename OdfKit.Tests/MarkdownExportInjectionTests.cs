using System;
using OdfKit.Export;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 回歸測試：不受信任的 ODF 超連結匯出為 Markdown 後，不得成為 HTML 注入或 XSS 載體。
/// 以 Markdig（預設允許原始 HTML）轉成 HTML 驗證，模擬常見的下游轉換。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Boundary)]
public sealed class MarkdownExportInjectionTests
{
    private static (string Markdown, string Html) Export(string href)
    {
        using TextDocument document = TextDocument.Create();
        OdfParagraph paragraph = document.AddParagraph("before ");
        paragraph.AddHyperlink(href, "click");
        document.AddParagraph("after");

        string markdown = document.ToMarkdown();
        return (markdown, Markdig.Markdown.ToHtml(markdown));
    }

    [Theory]
    [InlineData("http://x/a\n\n<img src=x onerror=alert(1) x=")]
    [InlineData("http://x/a\r\n\r\n<script>alert(1)</script>")]
    [InlineData("http://x/a>\n\n<img src=x onerror=alert(1) x=")]
    public void Href_WithLineBreaksAndMarkup_CannotBreakOutOfLink(string href)
    {
        (string markdown, string html) = Export(href);

        Assert.DoesNotContain("<img", html);
        Assert.DoesNotContain("<script", html);
        Assert.Contains("<a href=", html);
        Assert.DoesNotContain("<", markdown.Replace("(<http", string.Empty));
    }

    [Theory]
    [InlineData("http://x/a\\")]
    [InlineData("http://x/<a")]
    public void Href_WithBackslashOrAngleBracket_RemainsAWellFormedLink(string href)
    {
        (_, string html) = Export(href);

        Assert.Contains("<a href=\"http://x/", html);
        Assert.DoesNotContain("[click]", html);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("JaVaScRiPt:alert(1)")]
    [InlineData("  javascript:alert(1)")]
    [InlineData("java\tscript:alert(1)")]
    [InlineData("java\nscript:alert(1)")]
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==")]
    public void Href_WithScriptableScheme_IsNotEmittedAsExecutableLink(string href)
    {
        (string markdown, string html) = Export(href);

        Assert.DoesNotContain("javascript", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("vbscript", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data:text", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=\"javascript", html, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("https://example.com/a?b=1&c=2")]
    [InlineData("mailto:someone@example.com")]
    [InlineData("#bookmark")]
    [InlineData("data:image/png;base64,iVBORw0KGgo=")]
    public void Href_WithBenignTarget_IsPreserved(string href)
    {
        (string markdown, string html) = Export(href);

        Assert.Contains("[click](<" + href + ">)", markdown);
        Assert.Contains("<a href=", html);
    }
}
