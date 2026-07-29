using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace OdfKit.Styles;

/// <summary>
/// Defines how an automatic layout operation determines physical sizes.
/// 定義自動版面配置作業決定實體尺寸的方式。
/// </summary>
public enum OdfAutoFitMode
{
    /// <summary>
    /// Uses ODF optimal-size properties and delegates layout to the reader.
    /// 使用 ODF 最佳尺寸屬性，並將排版交由閱讀器處理。
    /// </summary>
    Reader,

    /// <summary>
    /// Uses the managed, font-file-free estimator.
    /// 使用不讀取字型檔案的受控估算器。
    /// </summary>
    Fast,

    /// <summary>
    /// Uses the supplied precise text measurer.
    /// 使用呼叫端提供的精確文字量測器。
    /// </summary>
    Precise
}

/// <summary>
/// Describes one styled run in a text measurement request.
/// 描述文字量測要求中的單一具樣式片段。
/// </summary>
public sealed class OdfTextMeasureRun
{
    /// <summary>
    /// Gets or sets the run text.
    /// 取得或設定片段文字。
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the font family.
    /// 取得或設定字型家族。
    /// </summary>
    public string FontFamily { get; set; } = OdfFontContext.DefaultBaseFontFamily;

    /// <summary>
    /// Gets or sets the font size in points.
    /// 取得或設定以點為單位的字型大小。
    /// </summary>
    public double FontSizePoints { get; set; } = 10;

    /// <summary>
    /// Gets or sets whether the run is bold.
    /// 取得或設定片段是否為粗體。
    /// </summary>
    public bool IsBold { get; set; }

    /// <summary>
    /// Gets or sets whether the run is italic.
    /// 取得或設定片段是否為斜體。
    /// </summary>
    public bool IsItalic { get; set; }
}

/// <summary>
/// Describes a styled text block to be measured.
/// 描述要量測的具樣式文字區塊。
/// </summary>
public sealed class OdfTextMeasureRequest
{
    /// <summary>
    /// Gets optional styled runs that override the single-style text properties when non-empty.
    /// 取得選用的具樣式片段；非空時會取代單一樣式文字屬性。
    /// </summary>
    public IList<OdfTextMeasureRun> Runs { get; } = new List<OdfTextMeasureRun>();

    /// <summary>
    /// Gets or sets the text to measure.
    /// 取得或設定要量測的文字。
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the font family.
    /// 取得或設定字型家族。
    /// </summary>
    public string FontFamily { get; set; } = OdfFontContext.DefaultBaseFontFamily;

    /// <summary>
    /// Gets or sets the font size in points.
    /// 取得或設定以點為單位的字型大小。
    /// </summary>
    public double FontSizePoints { get; set; } = 10;

    /// <summary>
    /// Gets or sets whether the text is bold.
    /// 取得或設定文字是否為粗體。
    /// </summary>
    public bool IsBold { get; set; }

    /// <summary>
    /// Gets or sets whether the text is italic.
    /// 取得或設定文字是否為斜體。
    /// </summary>
    public bool IsItalic { get; set; }

    /// <summary>
    /// Gets or sets the writing mode.
    /// 取得或設定書寫模式。
    /// </summary>
    public OdfWritingMode WritingMode { get; set; } = OdfWritingMode.LrTb;

    /// <summary>
    /// Gets or sets the available width in centimeters; null disables wrapping.
    /// 取得或設定以公分為單位的可用寬度；null 表示不換行。
    /// </summary>
    public double? AvailableWidthCentimeters { get; set; }

    /// <summary>
    /// Gets or sets whether wrapping is enabled.
    /// 取得或設定是否啟用換行。
    /// </summary>
    public bool Wrap { get; set; }

    /// <summary>
    /// Gets or sets the clockwise text rotation in degrees.
    /// 取得或設定文字順時針旋轉角度。
    /// </summary>
    public double RotationDegrees { get; set; }

    /// <summary>
    /// Gets or sets the maximum text elements accepted by the measurer.
    /// 取得或設定量測器接受的最大文字元素數。
    /// </summary>
    public int MaximumTextElements { get; set; } = 1_000_000;

    /// <summary>
    /// Gets or sets the maximum number of styled runs accepted by the measurer.
    /// 取得或設定量測器接受的具樣式片段數上限。
    /// </summary>
    public int MaximumRuns { get; set; } = 16_384;
}

/// <summary>
/// Represents the physical result of text layout measurement.
/// 表示文字版面量測的實體結果。
/// </summary>
/// <param name="widthCentimeters">The measured width in centimeters. / 以公分為單位的量測寬度。</param>
/// <param name="heightCentimeters">The measured height in centimeters. / 以公分為單位的量測高度。</param>
/// <param name="lineCount">The measured line count. / 量測所得的行數。</param>
/// <param name="isExact">Whether font metrics were used. / 是否使用字型度量資料。</param>
public readonly struct OdfTextMeasureResult(
    double widthCentimeters,
    double heightCentimeters,
    int lineCount,
    bool isExact)
{
    /// <summary>
    /// Gets the measured width in centimeters.
    /// 取得以公分為單位的量測寬度。
    /// </summary>
    public double WidthCentimeters { get; } = widthCentimeters;

    /// <summary>
    /// Gets the measured height in centimeters.
    /// 取得以公分為單位的量測高度。
    /// </summary>
    public double HeightCentimeters { get; } = heightCentimeters;

    /// <summary>
    /// Gets the line count.
    /// 取得行數。
    /// </summary>
    public int LineCount { get; } = lineCount;

    /// <summary>
    /// Gets whether font metrics were used.
    /// 取得是否使用字型度量資料。
    /// </summary>
    public bool IsExact { get; } = isExact;
}

/// <summary>
/// Measures styled text without coupling the core package to a rendering engine.
/// 量測具樣式文字，同時避免核心套件耦合至渲染引擎。
/// </summary>
public interface IOdfTextLayoutMeasurer
{
    /// <summary>
    /// Measures a styled text block.
    /// 量測具樣式文字區塊。
    /// </summary>
    /// <param name="request">The measurement request. / 量測要求。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消權杖。</param>
    /// <returns>The physical measurement result. / 實體量測結果。</returns>
    OdfTextMeasureResult Measure(OdfTextMeasureRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Configures bounded automatic width and height calculations.
/// 設定具資源上限的自動寬度與高度計算。
/// </summary>
public sealed class OdfAutoFitOptions
{
    /// <summary>
    /// Gets or sets the measurement mode.
    /// 取得或設定量測模式。
    /// </summary>
    public OdfAutoFitMode Mode { get; set; } = OdfAutoFitMode.Fast;

    /// <summary>
    /// Gets or sets the precise measurer used by <see cref="OdfAutoFitMode.Precise"/>.
    /// 取得或設定 <see cref="OdfAutoFitMode.Precise"/> 使用的精確量測器。
    /// </summary>
    public IOdfTextLayoutMeasurer? TextMeasurer { get; set; }

    /// <summary>
    /// Gets or sets the minimum column width.
    /// 取得或設定最小欄寬。
    /// </summary>
    public OdfLength MinimumColumnWidth { get; set; } = OdfLength.FromCentimeters(1);

    /// <summary>
    /// Gets or sets the maximum column width.
    /// 取得或設定最大欄寬。
    /// </summary>
    public OdfLength MaximumColumnWidth { get; set; } = OdfLength.FromCentimeters(50);

    /// <summary>
    /// Gets or sets the minimum row height.
    /// 取得或設定最小列高。
    /// </summary>
    public OdfLength MinimumRowHeight { get; set; } = OdfLength.FromCentimeters(0.45);

    /// <summary>
    /// Gets or sets the maximum row height.
    /// 取得或設定最大列高。
    /// </summary>
    public OdfLength MaximumRowHeight { get; set; } = OdfLength.FromCentimeters(100);

    /// <summary>
    /// Gets or sets the fallback column width used while calculating wrapped row height.
    /// 取得或設定計算換行列高時使用的後備欄寬。
    /// </summary>
    public OdfLength DefaultColumnWidth { get; set; } = OdfLength.FromCentimeters(2.27);

    /// <summary>
    /// Gets or sets the fallback horizontal padding per cell.
    /// 取得或設定每個儲存格的後備水平留白。
    /// </summary>
    public OdfLength HorizontalPadding { get; set; } = OdfLength.FromCentimeters(0.12);

    /// <summary>
    /// Gets or sets the fallback vertical padding per cell.
    /// 取得或設定每個儲存格的後備垂直留白。
    /// </summary>
    public OdfLength VerticalPadding { get; set; } = OdfLength.FromCentimeters(0.08);

    /// <summary>
    /// Gets or sets the fallback font family.
    /// 取得或設定後備字型家族。
    /// </summary>
    public string DefaultFontFamily { get; set; } = OdfFontContext.DefaultBaseFontFamily;

    /// <summary>
    /// Gets or sets the fallback font size in points.
    /// 取得或設定後備字型大小（點）。
    /// </summary>
    public double DefaultFontSizePoints { get; set; } = 10;

    /// <summary>
    /// Gets or sets the maximum cells inspected by one operation.
    /// 取得或設定單次作業最多檢查的儲存格數。
    /// </summary>
    public int MaximumCells { get; set; } = 1_000_000;

    /// <summary>
    /// Gets or sets the maximum text elements inspected by one operation.
    /// 取得或設定單次作業最多檢查的文字元素數。
    /// </summary>
    public int MaximumTextElements { get; set; } = 10_000_000;

    /// <summary>
    /// Gets or sets the maximum text elements inspected in one text block.
    /// 取得或設定單一文字區塊最多檢查的文字元素數。
    /// </summary>
    public int MaximumTextElementsPerBlock { get; set; } = 1_000_000;

    /// <summary>
    /// Gets or sets the maximum operation-scoped measurement cache entries.
    /// 取得或設定單次作業量測快取的最大項目數。
    /// </summary>
    public int MaximumMeasurementCacheEntries { get; set; } = 4_096;

    /// <summary>
    /// Gets or sets the maximum rich-text runs inspected by one operation.
    /// 取得或設定單次作業最多檢查的富文字片段數。
    /// </summary>
    public int MaximumRichTextRuns { get; set; } = 100_000;

    /// <summary>
    /// Gets or sets whether precise layout registers fonts embedded in the document.
    /// 取得或設定精確排版是否註冊文件內嵌字型。
    /// </summary>
    /// <remarks>
    /// Use an isolated document font context; the process-wide default context is rejected.
    /// 請使用文件專屬字型情境；處理程序共用的預設情境會被拒絕。
    /// </remarks>
    public bool UseEmbeddedFonts { get; set; }

    /// <summary>
    /// Gets or sets the maximum embedded fonts registered by one layout operation.
    /// 取得或設定單次排版作業最多註冊的內嵌字型數。
    /// </summary>
    public int MaximumEmbeddedFonts { get; set; } = 32;

    /// <summary>
    /// Gets or sets the maximum total embedded font bytes registered by one layout operation.
    /// 取得或設定單次排版作業最多註冊的內嵌字型總位元組數。
    /// </summary>
    public long MaximumEmbeddedFontBytes { get; set; } = 128 * 1024 * 1024;

    /// <summary>
    /// Gets or sets whether automatic text-box layout may change its width.
    /// 取得或設定文字框自動排版是否可變更寬度。
    /// </summary>
    public bool ResizeTextBoxWidth { get; set; }

    /// <summary>
    /// Gets or sets whether automatic text-box layout may change its height.
    /// 取得或設定文字框自動排版是否可變更高度。
    /// </summary>
    public bool ResizeTextBoxHeight { get; set; } = true;
}

/// <summary>
/// Provides the safe managed text-layout estimator used by core automatic layout.
/// 提供核心自動版面配置使用的安全受控文字版面估算器。
/// </summary>
internal sealed class OdfFastTextLayoutMeasurer : IOdfTextLayoutMeasurer
{
    internal static OdfFastTextLayoutMeasurer Instance { get; } = new();

    public OdfTextMeasureResult Measure(OdfTextMeasureRequest request, CancellationToken cancellationToken)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(request, nameof(request));
        if (request.MaximumTextElements < 1)
            throw new ArgumentOutOfRangeException(nameof(request));
        if (request.MaximumRuns < 1 || request.Runs.Count > request.MaximumRuns)
            throw new ArgumentOutOfRangeException(nameof(request));

        double limit = request.Wrap &&
            request.AvailableWidthCentimeters is double available &&
            IsFinite(available) &&
            available > 0
                ? available
                : double.PositiveInfinity;

        double current = 0;
        double maximum = 0;
        double currentLineHeight = 0;
        double totalHeight = 0;
        int lineCount = 1;
        int elementCount = 0;
        IEnumerable<OdfTextMeasureRun> runs = request.Runs.Count == 0
            ? [new OdfTextMeasureRun
            {
                Text = request.Text ?? string.Empty,
                FontFamily = request.FontFamily,
                FontSizePoints = request.FontSizePoints,
                IsBold = request.IsBold,
                IsItalic = request.IsItalic,
            }]
            : request.Runs;
        foreach (OdfTextMeasureRun run in runs)
        {
            global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(run, nameof(request));
            double fontSize = IsFinite(run.FontSizePoints) && run.FontSizePoints > 0
                ? Math.Min(run.FontSizePoints, 1_000)
                : 10;
            double emCm = fontSize * (2.54 / 72);
            double lineHeight = emCm * 1.2;
            double styleFactor = (run.IsBold ? 1.05 : 1) * (run.IsItalic ? 1.02 : 1);
            currentLineHeight = Math.Max(currentLineHeight, lineHeight);
            TextElementEnumerator enumerator =
                StringInfo.GetTextElementEnumerator(run.Text ?? string.Empty);
            while (enumerator.MoveNext())
            {
                if ((elementCount++ & 0xff) == 0)
                    cancellationToken.ThrowIfCancellationRequested();
                if (elementCount > request.MaximumTextElements)
                    throw new InvalidOperationException();

                string element = enumerator.GetTextElement();
                if (element is "\r" or "\n" or "\r\n")
                {
                    maximum = Math.Max(maximum, current);
                    totalHeight += currentLineHeight;
                    current = 0;
                    currentLineHeight = lineHeight;
                    lineCount++;
                    continue;
                }

                double advance = GetAdvanceInEm(element) * emCm * styleFactor;
                if (current > 0 && current + advance > limit)
                {
                    maximum = Math.Max(maximum, current);
                    totalHeight += currentLineHeight;
                    current = advance;
                    currentLineHeight = lineHeight;
                    lineCount++;
                }
                else
                {
                    current += advance;
                    currentLineHeight = Math.Max(currentLineHeight, lineHeight);
                }
            }
        }

        maximum = Math.Max(maximum, current);
        totalHeight += currentLineHeight > 0
            ? currentLineHeight
            : Math.Min(Math.Max(request.FontSizePoints, 1), 1_000) * (2.54 / 72) * 1.2;
        if (IsFinite(limit))
            maximum = Math.Min(maximum, limit);

        bool vertical = request.WritingMode is OdfWritingMode.TbLr or OdfWritingMode.TbRl;
        OdfTextMeasureResult result = vertical
            ? new OdfTextMeasureResult(totalHeight, maximum, lineCount, false)
            : new OdfTextMeasureResult(maximum, totalHeight, lineCount, false);
        return Rotate(result, request.RotationDegrees);
    }

    private static OdfTextMeasureResult Rotate(OdfTextMeasureResult result, double rotationDegrees)
    {
        if (!IsFinite(rotationDegrees))
            return result;

        double normalized = rotationDegrees % 360;
        if (Math.Abs(normalized) < 0.000001)
            return result;

        double radians = normalized * (Math.PI / 180);
        double cos = Math.Abs(Math.Cos(radians));
        double sin = Math.Abs(Math.Sin(radians));
        double width = (result.WidthCentimeters * cos) + (result.HeightCentimeters * sin);
        double height = (result.WidthCentimeters * sin) + (result.HeightCentimeters * cos);
        return new OdfTextMeasureResult(width, height, result.LineCount, result.IsExact);
    }

    private static double GetAdvanceInEm(string element)
    {
        int codePoint = element.Length >= 2 && char.IsSurrogatePair(element, 0)
            ? char.ConvertToUtf32(element, 0)
            : element[0];
        UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(element, 0);
        if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.Format)
            return 0;
        if (char.IsWhiteSpace(element, 0))
            return 0.33;
        if (IsWide(codePoint))
            return 1;
        if (category is UnicodeCategory.DashPunctuation or UnicodeCategory.OpenPunctuation or
            UnicodeCategory.ClosePunctuation or UnicodeCategory.InitialQuotePunctuation or
            UnicodeCategory.FinalQuotePunctuation or UnicodeCategory.OtherPunctuation)
            return 0.5;
        if (category == UnicodeCategory.DecimalDigitNumber)
            return 0.56;
        if (category == UnicodeCategory.UppercaseLetter)
        {
            return codePoint is 'W' or 'M'
                ? 0.9
                : codePoint is 'I'
                    ? 0.35
                    : 0.62;
        }
        if (category == UnicodeCategory.LowercaseLetter)
        {
            return codePoint is 'm' or 'w'
                ? 0.82
                : codePoint is 'i' or 'l'
                    ? 0.28
                    : 0.55;
        }
        return 0.55;
    }

    private static bool IsWide(int codePoint) =>
        codePoint is >= 0x1100 and <= 0x115f or
            >= 0x2329 and <= 0x232a or
            >= 0x2e80 and <= 0xa4cf or
            >= 0xac00 and <= 0xd7a3 or
            >= 0xf900 and <= 0xfaff or
            >= 0xfe10 and <= 0xfe19 or
            >= 0xfe30 and <= 0xfe6f or
            >= 0xff00 and <= 0xff60 or
            >= 0xffe0 and <= 0xffe6 or
            >= 0x1f000 and <= 0x1faff or
            >= 0x20000 and <= 0x3fffd;

    private static bool IsFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value);
}
