using System;
using OdfKit.Extensions.Imaging;
using Xunit;
using SkiaSharp;

namespace OdfKit.Tests;

/// <summary>
/// 以 Chrome 的 canvas measureText 為基準的真實量測結果：量測器曾經完全忽略粗體（Arial 粗體少 4–7%），
/// 且字型缺字時不使用備援字型（拉丁字型量中文少 22–45%）。
/// </summary>
public sealed class TextMeasurerFidelityTests
{
    private static bool HasFamily(string family)
    {
        using SKTypeface? typeface = SKTypeface.FromFamilyName(family);
        return typeface is not null && string.Equals(typeface.FamilyName, family, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 粗體文字比正常字重寬（Arial 粗體在 Chrome 為 7.5% 以上）。
    /// </summary>
    [Fact]
    public void BoldTextIsMeasuredWithTheBoldFace()
    {
        if (!HasFamily("Arial"))
            return;

        double regular = OdfTextMeasurer.MeasureWidth("The quick brown fox", "Arial", 12).ToCentimeters();
        double bold = OdfTextMeasurer.MeasureWidth("The quick brown fox", "Arial", 12, isBold: true).ToCentimeters();

        Assert.True(bold > regular * 1.04, $"粗體 {bold} 應明顯寬於正常字重 {regular}");
    }

    /// <summary>
    /// 拉丁字型沒有中文字形時，以備援字型的寬度（約 1 em）量測，而不是 .notdef 的寬度。
    /// </summary>
    [Fact]
    public void CjkTextInALatinFontUsesFallbackGlyphWidths()
    {
        if (!HasFamily("Arial") || SKFontManager.Default.MatchCharacter('中') is null)
            return;

        double width = OdfTextMeasurer.MeasureWidth("中華民國臺灣", "Arial", 12).ToCentimeters();
        double sixEm = 6 * 12 * 2.54 / 72;

        Assert.InRange(width, sixEm * 0.9, sixEm * 1.1);
    }
}
