namespace OdfKit.Styles;

/// <summary>
/// Configures text segmentation and font fallback behavior.
/// 設定文字分段與字型遞補行為。
/// </summary>
public sealed class OdfTextFontFallbackOptions
{
    private const string DefaultBaseFont = "TW-Kai";

    /// <summary>
    /// Initializes a new instance of the <see cref="OdfTextFontFallbackOptions"/> class.
    /// 初始化 <see cref="OdfTextFontFallbackOptions"/> 類別的新執行個體。
    /// </summary>
    /// <param name="baseFont">The base CJK font family. / 基礎 CJK 字型家族。</param>
    /// <param name="declareDefaultCjkFallbackFonts">A value indicating whether default CJK font-face declarations are written. / 是否寫入預設 CJK font-face 宣告。</param>
    public OdfTextFontFallbackOptions(string? baseFont = DefaultBaseFont, bool declareDefaultCjkFallbackFonts = true)
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

    internal IReadOnlyList<OdfFontFaceInfo> FontFaces { get; }

    /// <summary>
    /// Creates options for CNS 11643 full-font-library fallback.
    /// 建立 CNS 11643 全字庫情境的字型遞補設定。
    /// </summary>
    /// <param name="baseFont">The base CJK font family. / 基礎 CJK 字型家族。</param>
    /// <returns>The configured fallback options. / 已設定的遞補選項。</returns>
    public static OdfTextFontFallbackOptions Cns11643(string? baseFont = DefaultBaseFont)
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
