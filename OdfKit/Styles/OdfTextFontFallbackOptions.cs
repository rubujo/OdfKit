using OdfKit.Compliance;

namespace OdfKit.Styles;

/// <summary>
/// Configures text segmentation and font fallback behavior.
/// 設定文字分段與字型遞補行為。
/// </summary>
public sealed class OdfTextFontFallbackOptions
{
    private const string DefaultBaseFont = "TW-Kai";
    /// <summary>
    /// Short overload of OdfTextFontFallbackOptions that uses default values for all optional parameters and forwards to the full overload.
    /// 便利多載：OdfTextFontFallbackOptions 的所有可選參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public OdfTextFontFallbackOptions() : this(DefaultBaseFont, true) { }

    /// <summary>
    /// Short overload of OdfTextFontFallbackOptions that accepts baseFont; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 baseFont；其餘可選參數使用預設值並轉呼叫最長 OdfTextFontFallbackOptions 多載。
    /// </summary>
    public OdfTextFontFallbackOptions(string? baseFont) : this(baseFont, true) { }


    /// <summary>
    /// Initializes a new instance of the <see cref="OdfTextFontFallbackOptions"/> class.
    /// 初始化 <see cref="OdfTextFontFallbackOptions"/> 類別的新執行個體。
    /// </summary>
    /// <param name="baseFont">The base CJK font family. / 基礎 CJK 字型家族。</param>
    /// <param name="declareDefaultCjkFallbackFonts">A value indicating whether default CJK font-face declarations are written. / 是否寫入預設 CJK font-face 宣告。</param>
    public OdfTextFontFallbackOptions(string? baseFont, bool declareDefaultCjkFallbackFonts)
        : this(
            baseFont,
            declareDefaultCjkFallbackFonts,
            declareDefaultCjkFallbackFonts ? OdfCjkFontFallbackEngine.DefaultFallbackFonts : [])
    {
    }

    private OdfTextFontFallbackOptions(string? baseFont, bool declareDefaultCjkFallbackFonts, IReadOnlyList<OdfFontFaceInfo> fontFaces)
    {
        BaseFont = NormalizeBaseFont(baseFont);
        DeclareDefaultCjkFallbackFonts = declareDefaultCjkFallbackFonts;
        FontFaces = fontFaces;
    }

    /// <summary>
    /// Gets the base font family used before fallback mapping is applied.
    /// 取得套用遞補對照前使用的基礎字型家族。
    /// </summary>
    public string BaseFont { get; }

    /// <summary>
    /// Gets a value indicating whether the default CJK font-face declarations are written.
    /// 取得是否寫入預設 CJK font-face 宣告。
    /// </summary>
    public bool DeclareDefaultCjkFallbackFonts { get; }

    /// <summary>
    /// Gets the font context used for text segmentation; null uses <see cref="OdfFontContext.Default"/>.
    /// 取得文字分段所用的字型情境；為 null 時使用 <see cref="OdfFontContext.Default"/>。
    /// </summary>
    public OdfFontContext? FontContext { get; init; }

    internal OdfFontContext EffectiveFontContext => FontContext ?? OdfFontContext.Default;

    internal IReadOnlyList<OdfFontFaceInfo> FontFaces { get; }
    /// <summary>
    /// Short overload of Cns11643 that uses default values for all optional parameters and forwards to the full overload.
    /// 便利多載：Cns11643 的所有可選參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public static OdfTextFontFallbackOptions Cns11643() => Cns11643(DefaultBaseFont);


    /// <summary>
    /// Creates options for CNS 11643 full-font-library fallback.
    /// 建立 CNS 11643 全字庫情境的字型遞補設定。
    /// </summary>
    /// <param name="baseFont">The base CJK font family. / 基礎 CJK 字型家族。</param>
    /// <returns>The configured fallback options. / 已設定的遞補選項。</returns>
    public static OdfTextFontFallbackOptions Cns11643(string? baseFont)
    {
        return new OdfTextFontFallbackOptions(baseFont, declareDefaultCjkFallbackFonts: true, OdfCjkFontFallbackEngine.DefaultFallbackFonts);
    }


    /// <summary>
    /// Creates options for Hanazono Mincho fallback.
    /// 建立花園明朝字型遞補設定。
    /// </summary>
    /// <returns>The configured fallback options. / 已設定的遞補選項。</returns>
    public static OdfTextFontFallbackOptions HanaMin()
    {
        return new OdfTextFontFallbackOptions(
            "HanaMinA",
            declareDefaultCjkFallbackFonts: true,
            [
                new OdfFontFaceInfo("HanaMinA", "HanaMinA", "system-serif", "variable"),
                new OdfFontFaceInfo("HanaMinB", "HanaMinB", "system-serif", "variable")
            ]);
    }

    /// <summary>
    /// Creates options that declare caller-supplied font faces for custom rare-glyph fonts.
    /// 建立宣告呼叫端自訂 font-face 的遞補選項，供自訂罕字字型使用。
    /// </summary>
    /// <remarks>
    /// Combine with <see cref="OdfFontSegmenter.RegisterSupplementaryPlaneFontMapping"/> so text segmentation
    /// routes supplementary-plane characters to the declared fonts.
    /// 可搭配 <see cref="OdfFontSegmenter.RegisterSupplementaryPlaneFontMapping"/> 使用，讓文字分段將增補平面字元導向所宣告的字型。
    /// </remarks>
    /// <param name="baseFont">The base CJK font family. / 基礎 CJK 字型家族。</param>
    /// <param name="fontFaces">The font-face declarations to write into the document. / 要寫入文件的 font-face 宣告集合。</param>
    /// <returns>The configured fallback options. / 已設定的遞補選項。</returns>
    /// <exception cref="ArgumentNullException">當 <paramref name="fontFaces"/> 為 <see langword="null"/> 時擲出</exception>
    /// <exception cref="ArgumentException">當任一 font-face 宣告的名稱或字型家族為空白時擲出</exception>
    public static OdfTextFontFallbackOptions Custom(string? baseFont, IReadOnlyList<OdfFontFaceInfo> fontFaces)
    {
        if (fontFaces is null)
        {
            throw new ArgumentNullException(nameof(fontFaces));
        }

        // 防禦性複製：呼叫端後續修改原集合不得影響本選項；同時驗證每筆宣告的必要欄位。
        var copy = new OdfFontFaceInfo[fontFaces.Count];
        for (int i = 0; i < copy.Length; i++)
        {
            OdfFontFaceInfo? fontFace = fontFaces[i];
            if (fontFace is null || string.IsNullOrWhiteSpace(fontFace.Name) || string.IsNullOrWhiteSpace(fontFace.Family))
            {
                throw new ArgumentException(
                    OdfLocalizer.GetMessage("Err_OdfTextFontFallbackOptions_FontFaceEmpty"),
                    nameof(fontFaces));
            }

            copy[i] = fontFace;
        }

        return new OdfTextFontFallbackOptions(baseFont, declareDefaultCjkFallbackFonts: true, copy);
    }

    /// <summary>
    /// Creates options for Jigmo fallback.
    /// 建立字雲字型遞補設定。
    /// </summary>
    /// <returns>The configured fallback options. / 已設定的遞補選項。</returns>
    public static OdfTextFontFallbackOptions Jigmo()
    {
        return new OdfTextFontFallbackOptions(
            "Jigmo",
            declareDefaultCjkFallbackFonts: true,
            [
                new OdfFontFaceInfo("Jigmo", "Jigmo", "system-serif", "variable"),
                new OdfFontFaceInfo("Jigmo2", "Jigmo2", "system-serif", "variable"),
                new OdfFontFaceInfo("Jigmo3", "Jigmo3", "system-serif", "variable")
            ]);
    }

    private static string NormalizeBaseFont(string? baseFont)
    {
        string candidate = baseFont ?? string.Empty;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return DefaultBaseFont;
        }

        return candidate;
    }
}
