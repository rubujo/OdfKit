using System;
using System.IO;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Export;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證 OTH 網頁範本文件（<see cref="TextWebDocument"/>）的雙向轉換工作流，
/// 以及與 HTML 匯出工作流的相容性。
/// </summary>
public class TextWebDocumentTests
{
    /// <summary>
    /// 驗證 <see cref="TextWebDocument.CreateFromDocument(TextDocument)"/> 與
    /// <see cref="TextDocument.CreateFromWebDocument(TextWebDocument)"/> 形成的雙向轉換，
    /// 內容與 MIME 類型正確且可往返還原。
    /// </summary>
    [Fact]
    public void TextWebDocument_CreateFromDocument_RoundTripsBackToOdt()
    {
        using var original = TextDocument.Create();
        original.Title = "網頁範本往返測試標題";
        original.AddParagraph("既有 ODT 文件內容");

        using var web = TextWebDocument.CreateFromDocument(original);
        Assert.Equal("application/vnd.oasis.opendocument.text-web", web.Package.MimeType);
        Assert.Equal(OdfDocumentKind.TextWeb, web.DocumentKind);
        Assert.Equal("既有 ODT 文件內容", web.BodyTextRoot.Children[0].TextContent);

        using var restored = TextDocument.CreateFromWebDocument(web);
        Assert.Equal("application/vnd.oasis.opendocument.text", restored.Package.MimeType);
        Assert.Equal(OdfDocumentKind.Text, restored.DocumentKind);
        Assert.Equal("網頁範本往返測試標題", restored.Title);
        Assert.Equal("既有 ODT 文件內容", restored.BodyTextRoot.Children[0].TextContent);
    }

    /// <summary>
    /// 驗證 <see cref="TextWebDocument"/> 可直接以繼承自 <see cref="TextDocument"/> 的高階 API
    /// 編輯內容（不需下沉 DOM），且儲存／載入後保留。
    /// </summary>
    [Fact]
    public void TextWebDocument_SupportsHighLevelEditingApiWithoutDom()
    {
        using var web = TextWebDocument.Create();
        web.AddHeading("網頁範本標題", 1);
        web.AddParagraph("直接以高階 API 編輯的網頁範本內容");

        using var ms = new MemoryStream();
        web.SaveToStream(ms);
        ms.Position = 0;

        using var reloaded = TextWebDocument.Load(ms);
        Assert.Equal(OdfDocumentKind.TextWeb, reloaded.DocumentKind);
        Assert.Equal("直接以高階 API 編輯的網頁範本內容", reloaded.BodyTextRoot.Children[1].TextContent);
    }

    /// <summary>
    /// 驗證 <see cref="OdfHtmlExporter.Export(OdfKit.Text.TextDocument, OdfHtmlExportOptions?)"/>
    /// 因 <see cref="TextWebDocument"/> 繼承自 <see cref="TextDocument"/>，可直接接受 OTH 網頁範本文件，
    /// 不需任何額外轉接層，證明 OTH 與 HTML 匯出工作流的一致性。
    /// </summary>
    [Fact]
    public void OdfHtmlExporter_AcceptsTextWebDocumentDirectly()
    {
        using var web = TextWebDocument.Create();
        web.AddHeading("網頁範本標題", 1);
        web.AddParagraph("網頁範本段落內容");

        string html = OdfHtmlExporter.Export(web);

        Assert.Contains("<h1>", html, StringComparison.Ordinal);
        Assert.Contains("網頁範本標題", html, StringComparison.Ordinal);
        Assert.Contains("網頁範本段落內容", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// 驗證 <see cref="TextWebDocument.CreateFromDocument(TextDocument)"/> 與
    /// <see cref="TextDocument.CreateFromWebDocument(TextWebDocument)"/> 的邊界案例：
    /// 傳入 <see langword="null"/> 來源文件時皆擲出 <see cref="ArgumentNullException"/>。
    /// </summary>
    [Fact]
    public void CreateWebDocument_NullSource_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => TextWebDocument.CreateFromDocument(null!));
        Assert.Throws<ArgumentNullException>(() => TextDocument.CreateFromWebDocument(null!));
    }
}
