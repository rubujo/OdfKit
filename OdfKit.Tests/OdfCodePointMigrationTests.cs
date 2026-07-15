using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證 ODF 文件文字碼位遷移功能。
/// </summary>
public class OdfCodePointMigrationTests
{
    /// <summary>
    /// 驗證內容樹與樣式樹都會被遞迴走訪，並產生正確報告。
    /// </summary>
    [Fact]
    public void MigrateTextCodePoints_ContentAndStylesTrees_ReportsReplacements()
    {
        using TextDocument document = TextDocument.Create();
        OdfNode contentText = CreateTextNode("內文\uE000\uE000");
        document.ContentDom.AppendChild(contentText);

        var masterPage = new OdfNode(OdfNodeType.Element, "master-page", OdfNamespaces.Style, "style");
        OdfNode headerText = CreateTextNode("頁首\uE000");
        masterPage.AppendChild(headerText);
        document.StylesDom.AppendChild(masterPage);

        OdfCodePointMigrationReport report = document.MigrateTextCodePoints(
            new Dictionary<int, int> { [0xE000] = 0x4E00 });

        Assert.Equal("內文一一", contentText.TextContent);
        Assert.Equal("頁首一", headerText.TextContent);
        Assert.Equal(3, report.TotalReplacements);
        Assert.Equal(2, report.AffectedTextNodes);
        Assert.Equal(3, report.ReplacementsByCodePoint[0xE000]);
    }

    /// <summary>
    /// 驗證 BMP 與增補平面碼位可雙向替換。
    /// </summary>
    [Fact]
    public void MigrateTextCodePoints_BmpAndSupplementaryCodePoints_ReplacesBothDirections()
    {
        using TextDocument document = TextDocument.Create();
        OdfNode text = CreateTextNode("\uE000\U0002000B");
        document.ContentDom.AppendChild(text);

        OdfCodePointMigrationReport report = document.MigrateTextCodePoints(new Dictionary<int, int>
        {
            [0xE000] = 0x2000B,
            [0x2000B] = 0x4E00
        });

        Assert.Equal("\U0002000B一", text.TextContent);
        Assert.Equal(2, report.TotalReplacements);
        Assert.Equal(1, report.AffectedTextNodes);
    }

    /// <summary>
    /// 驗證沒有命中時不會改寫文字，並回傳全零報告。
    /// </summary>
    [Fact]
    public void MigrateTextCodePoints_NoMatch_ReturnsZeroReportWithoutChangingText()
    {
        using TextDocument document = TextDocument.Create();
        string original = "沒有命中的文字";
        OdfNode text = CreateTextNode(original);
        document.ContentDom.AppendChild(text);

        OdfCodePointMigrationReport report = document.MigrateTextCodePoints(
            new Dictionary<int, int> { [0xE000] = 0x4E00 });

        Assert.Same(original, text.TextContent);
        Assert.Equal(0, report.TotalReplacements);
        Assert.Equal(0, report.AffectedTextNodes);
        Assert.Empty(report.ReplacementsByCodePoint);
    }

    /// <summary>
    /// 驗證相同來源與目標的對應項目會被略過。
    /// </summary>
    [Fact]
    public void MigrateTextCodePoints_IdentityMapping_IsIgnored()
    {
        using TextDocument document = TextDocument.Create();
        string original = "一一";
        OdfNode text = CreateTextNode(original);
        document.ContentDom.AppendChild(text);

        OdfCodePointMigrationReport report = document.MigrateTextCodePoints(
            new Dictionary<int, int> { [0x4E00] = 0x4E00 });

        Assert.Same(original, text.TextContent);
        Assert.Equal(0, report.TotalReplacements);
        Assert.Equal(0, report.AffectedTextNodes);
        Assert.Empty(report.ReplacementsByCodePoint);
    }

    /// <summary>
    /// 驗證空值、代理字元碼位與超界碼位會被拒絕。
    /// </summary>
    [Fact]
    public void MigrateTextCodePoints_InvalidMapping_ThrowsExpectedException()
    {
        using TextDocument document = TextDocument.Create();

        Assert.Throws<ArgumentNullException>(() => document.MigrateTextCodePoints(null!));
        Assert.Throws<ArgumentException>(() => document.MigrateTextCodePoints(
            new Dictionary<int, int> { [0xD800] = 0x4E00 }));
        Assert.Throws<ArgumentException>(() => document.MigrateTextCodePoints(
            new Dictionary<int, int> { [0x4E00] = 0x110000 }));
    }

    /// <summary>
    /// 驗證同一碼位多次出現時會逐次統計。
    /// </summary>
    [Fact]
    public void MigrateTextCodePoints_RepeatedCodePoint_CountsEveryOccurrence()
    {
        using TextDocument document = TextDocument.Create();
        OdfNode text = CreateTextNode("\uE000甲\uE000\uE000");
        document.ContentDom.AppendChild(text);

        OdfCodePointMigrationReport report = document.MigrateTextCodePoints(
            new Dictionary<int, int> { [0xE000] = 0x4E00 });

        Assert.Equal(3, report.TotalReplacements);
        Assert.Equal(3, report.ReplacementsByCodePoint[0xE000]);
    }

    private static OdfNode CreateTextNode(string value) =>
        new(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = value };
}
