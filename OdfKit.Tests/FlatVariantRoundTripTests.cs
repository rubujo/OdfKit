using System;
using System.IO;
using System.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Drawing;
using OdfKit.Presentation;
using OdfKit.Spreadsheet;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證四主格式 Flat XML（FODT／FODS／FODP／FODG）與 ZIP 封裝之間型別化雙向轉換工作流的
/// 等價性 Round-trip 測試。
/// </summary>
public class FlatVariantRoundTripTests
{
    /// <summary>
    /// 驗證 <see cref="FlatTextDocument.CreateFromDocument(TextDocument)"/> 與
    /// <see cref="TextDocument.CreateFromFlatDocument(FlatTextDocument)"/> 形成的雙向轉換，
    /// 內容與 Flat XML 形態正確且可往返還原。
    /// </summary>
    [Fact]
    public void TextDocument_CreateFlatDocument_RoundTripsBackToZip()
    {
        using var original = TextDocument.Create();
        original.Title = "Flat 往返測試標題";
        original.AddParagraph("既有 ZIP 文件內容");

        using var flat = FlatTextDocument.CreateFromDocument(original);
        Assert.True(flat.IsFlatXml);
        Assert.Equal(OdfDocumentKind.FlatText, flat.DocumentKind);
        Assert.Equal("既有 ZIP 文件內容", flat.BodyTextRoot.Children.Single().TextContent);

        using var ms = new MemoryStream();
        flat.SaveToStream(ms);
        ms.Position = 0;
        string flatXml = new StreamReader(ms).ReadToEnd();
        Assert.StartsWith("<?xml", flatXml.TrimStart());
        Assert.Contains("<office:document", flatXml, StringComparison.Ordinal);

        using var restored = TextDocument.CreateFromFlatDocument(flat);
        Assert.False(restored.IsFlatXml);
        Assert.Equal(OdfDocumentKind.Text, restored.DocumentKind);
        Assert.Equal("Flat 往返測試標題", restored.Title);
        Assert.Equal("既有 ZIP 文件內容", restored.BodyTextRoot.Children.Single().TextContent);
    }

    /// <summary>
    /// 驗證 <see cref="FlatTextDocument"/> 可直接以繼承自 <see cref="TextDocument"/> 的高階 API
    /// 編輯內容（不需下沉 DOM），證明 Flat XML 與 ZIP 封裝的高階編輯工作流等價。
    /// </summary>
    [Fact]
    public void FlatTextDocument_SupportsHighLevelEditingApiWithoutDom()
    {
        using var flat = FlatTextDocument.Create();
        flat.AddHeading("Flat 文件標題", 1);
        flat.AddParagraph("直接以高階 API 編輯的 Flat 文件");

        using var ms = new MemoryStream();
        flat.SaveToStream(ms);
        ms.Position = 0;

        using var reloaded = FlatTextDocument.Load(ms);
        Assert.True(reloaded.IsFlatXml);
        Assert.Equal("直接以高階 API 編輯的 Flat 文件", reloaded.BodyTextRoot.Children.Last().TextContent);
    }

    /// <summary>
    /// 驗證 <see cref="FlatSpreadsheetDocument.CreateFromDocument(SpreadsheetDocument)"/> 與
    /// <see cref="SpreadsheetDocument.CreateFromFlatDocument(FlatSpreadsheetDocument)"/> 的雙向轉換，
    /// 完整保留工作表資料。
    /// </summary>
    [Fact]
    public void SpreadsheetDocument_CreateFlatDocument_RoundTripsBackToZip()
    {
        using var original = SpreadsheetDocument.Create();
        var sheet = original.Worksheets.Add("Sheet1");
        sheet.Cells["A1"].CellValue = "既有資料";

        using var flat = FlatSpreadsheetDocument.CreateFromDocument(original);
        Assert.True(flat.IsFlatXml);
        Assert.Equal("既有資料", flat.Worksheets["Sheet1"].Cells["A1"].CellValue?.ToString());

        using var restored = SpreadsheetDocument.CreateFromFlatDocument(flat);
        Assert.False(restored.IsFlatXml);
        Assert.Equal("既有資料", restored.Worksheets["Sheet1"].Cells["A1"].CellValue?.ToString());
    }

    /// <summary>
    /// 驗證 <see cref="FlatPresentationDocument.CreateFromDocument(PresentationDocument)"/> 與
    /// <see cref="PresentationDocument.CreateFromFlatDocument(FlatPresentationDocument)"/> 的雙向轉換，
    /// 完整保留母片頁面。
    /// </summary>
    [Fact]
    public void PresentationDocument_CreateFlatDocument_RoundTripsBackToZip()
    {
        using var original = PresentationDocument.Create();
        original.AddMasterPage("OriginalFlatMaster");

        using var flat = FlatPresentationDocument.CreateFromDocument(original);
        Assert.True(flat.IsFlatXml);
        Assert.NotNull(flat.GetMasterPages().FirstOrDefault(m => m.Name == "OriginalFlatMaster"));

        using var restored = PresentationDocument.CreateFromFlatDocument(flat);
        Assert.False(restored.IsFlatXml);
        Assert.NotNull(restored.GetMasterPages().FirstOrDefault(m => m.Name == "OriginalFlatMaster"));
    }

    /// <summary>
    /// 驗證 <see cref="FlatGraphicsDocument.CreateFromDocument(DrawingDocument)"/> 與
    /// <see cref="DrawingDocument.CreateFromFlatDocument(FlatGraphicsDocument)"/> 的雙向轉換，
    /// 完整保留母片頁面。
    /// </summary>
    [Fact]
    public void DrawingDocument_CreateFlatDocument_RoundTripsBackToZip()
    {
        using var original = DrawingDocument.Create();
        original.AddMasterPage("OriginalFlatDrawingMaster");

        using var flat = FlatGraphicsDocument.CreateFromDocument(original);
        Assert.True(flat.IsFlatXml);
        Assert.NotNull(flat.GetMasterPages().FirstOrDefault(m => m.Name == "OriginalFlatDrawingMaster"));

        using var restored = DrawingDocument.CreateFromFlatDocument(flat);
        Assert.False(restored.IsFlatXml);
        Assert.NotNull(restored.GetMasterPages().FirstOrDefault(m => m.Name == "OriginalFlatDrawingMaster"));
    }

    /// <summary>
    /// 驗證八個 Flat↔ZIP 雙向轉換工作流方法的邊界案例：
    /// 傳入 <see langword="null"/> 來源文件時皆擲出 <see cref="ArgumentNullException"/>。
    /// </summary>
    [Fact]
    public void CreateFlatVariant_NullSource_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => FlatTextDocument.CreateFromDocument(null!));
        Assert.Throws<ArgumentNullException>(() => TextDocument.CreateFromFlatDocument(null!));
        Assert.Throws<ArgumentNullException>(() => FlatSpreadsheetDocument.CreateFromDocument(null!));
        Assert.Throws<ArgumentNullException>(() => SpreadsheetDocument.CreateFromFlatDocument(null!));
        Assert.Throws<ArgumentNullException>(() => FlatPresentationDocument.CreateFromDocument(null!));
        Assert.Throws<ArgumentNullException>(() => PresentationDocument.CreateFromFlatDocument(null!));
        Assert.Throws<ArgumentNullException>(() => FlatGraphicsDocument.CreateFromDocument(null!));
        Assert.Throws<ArgumentNullException>(() => DrawingDocument.CreateFromFlatDocument(null!));
    }
}
