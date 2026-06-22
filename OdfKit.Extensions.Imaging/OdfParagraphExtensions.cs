using System;
using System.Linq;
using OdfKit.Compliance;
using OdfKit.DOM;
using OdfKit.Styles;
using OdfKit.Text;
namespace OdfKit.Extensions.Imaging;

/// <summary>
/// 提供針對 <see cref="OdfParagraph"/> 的進階版面度量與排版 Facade 擴充方法。
/// </summary>
public static class OdfParagraphExtensions
{
    /// <summary>
    /// 當段落文字長度超出指定的物理限制時，自動縮小字型大小（Auto-Shrink），確保單行文字不溢出物理範圍。
    /// </summary>
    /// <param name="paragraph">目標段落。</param>
    /// <param name="maxWidthInCentimeters">最大可允許之物理寬度（公分）。</param>
    /// <param name="fontName">量測採用的字型名稱。</param>
    /// <param name="initialFontSizePoints">起始初始字級大小（以點 Pt 為單位）。</param>
    /// <param name="writingMode">書寫模式（橫書或直書）。</param>
    public static void AutoShrinkToFit(this OdfParagraph paragraph, double maxWidthInCentimeters, string fontName, double initialFontSizePoints, OdfWritingMode writingMode = OdfWritingMode.LrTb)
    {
        if (paragraph is null)
            throw new ArgumentNullException(nameof(paragraph));
        if (maxWidthInCentimeters <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxWidthInCentimeters));
        if (string.IsNullOrEmpty(fontName))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfParagraphExtensions_FontCannotBeEmpty"), nameof(fontName));

        // 1. 強制進行裂變同步以取得 Runs 集合，不污染段落本身的預設樣式
        var runs = paragraph.Runs.ToList();
        string text = paragraph.TextContent;

        // 限制：僅適用於單行、無換行的標籤場景
        if (text.Contains('\n') || text.Contains('\r'))
            return;

        double fontSize = initialFontSizePoints;
        const int maxIterations = 50;
        int iteration = 0;

        while (fontSize > 4.0 && iteration < maxIterations)
        {
            var length = OdfTextMeasurer.MeasureWidth(text, fontName, fontSize, false, false, writingMode);
            if (length.Value <= maxWidthInCentimeters)
            {
                break;
            }
            fontSize -= 0.5;
            iteration++;
        }

        // 2. 將縮小後的字型大小套用至段落下的所有 runs，包含中日韓與複雜字型槽
        string fontSizeStr = $"{fontSize:F1}pt";
        foreach (var run in runs)
        {
            run.FontSize = fontSizeStr;
            run.FontSizeAsian = fontSizeStr;
            run.FontSizeComplex = fontSizeStr;
        }
    }
}
