using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Styles;

/// <summary>
/// Provides shared CJK font fallback declarations.
/// 提供共用的中日韓字型遞補宣告。
/// </summary>
internal static class OdfCjkFontFallbackEngine
{
    internal static readonly OdfFontFaceInfo[] DefaultFallbackFonts =
    [
        new("TW-Kai-98_1", "TW-Kai-98_1", "system-serif", "variable"),
        new("TW-Kai-Ext-B-98_1", "TW-Kai-Ext-B-98_1", "system-serif", "variable"),
        new("TW-Kai-Plus-98_1", "TW-Kai-Plus-98_1", "system-serif", "variable"),
        new("TW-Song-98_1", "TW-Song-98_1", "system-serif", "variable"),
        new("TW-Song-Ext-B-98_1", "TW-Song-Ext-B-98_1", "system-serif", "variable"),
        new("TW-Song-Plus-98_1", "TW-Song-Plus-98_1", "system-serif", "variable"),
        new("PMingLiU", "PMingLiU", "system-serif", "variable"),
        new("Microsoft JhengHei", "Microsoft JhengHei", "system-sans-serif", "variable"),
        new("MS Mincho", "MS Mincho", "system-serif", "variable"),
        new("MS Gothic", "MS Gothic", "system-sans-serif", "variable"),
        new("SimSun", "SimSun", "system-serif", "variable"),
        new("Microsoft YaHei", "Microsoft YaHei", "system-sans-serif", "variable"),
        new("Malgun Gothic", "Malgun Gothic", "system-sans-serif", "variable")
    ];

    /// <summary>
    /// Applies the default CJK fallback font declarations.
    /// 套用預設的中日韓字型遞補宣告。
    /// </summary>
    /// <param name="document">The target ODF document. / 目標 ODF 文件。</param>
    internal static void ApplyFontFallback(OdfDocument document)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        ApplyFontFaces(document, DefaultFallbackFonts);
    }

    /// <summary>
    /// Applies the font-face declarations specified by fallback options.
    /// 套用字型遞補設定指定的 font-face 宣告。
    /// </summary>
    /// <param name="document">The target ODF document. / 目標 ODF 文件。</param>
    /// <param name="options">The font fallback options. / 字型遞補選項。</param>
    internal static void ApplyFontFallback(OdfDocument document, OdfTextFontFallbackOptions options)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        ApplyFontFaces(document, options.FontFaces);
    }

    /// <summary>
    /// Applies the default CJK fallback font declarations to DOM roots.
    /// 將預設的中日韓字型遞補宣告套用至 DOM 根節點。
    /// </summary>
    /// <param name="contentDom">The content DOM root. / content DOM 根節點。</param>
    /// <param name="stylesDom">The styles DOM root. / styles DOM 根節點。</param>
    internal static void ApplyFontFallback(OdfNode contentDom, OdfNode? stylesDom)
    {
        ApplyFontFaces(contentDom, stylesDom, DefaultFallbackFonts);
    }

    private static void ApplyFontFaces(OdfDocument document, IReadOnlyList<OdfFontFaceInfo> fontFaces)
    {
        foreach (OdfFontFaceInfo fontFace in fontFaces)
        {
            OdfFontFaceDeclarationEngine.AddFontFace(
                document,
                fontFace.Name,
                fontFace.Family,
                fontFace.GenericFamily,
                fontFace.Pitch);
        }
    }

    private static void ApplyFontFaces(OdfNode contentDom, OdfNode? stylesDom, IReadOnlyList<OdfFontFaceInfo> fontFaces)
    {
        foreach (OdfFontFaceInfo fontFace in fontFaces)
        {
            OdfFontFaceDeclarationEngine.AddToDom(
                contentDom,
                fontFace.Name,
                fontFace.Family,
                fontFace.GenericFamily,
                fontFace.Pitch);
            if (stylesDom is not null)
            {
                OdfFontFaceDeclarationEngine.AddToDom(
                    stylesDom,
                    fontFace.Name,
                    fontFace.Family,
                    fontFace.GenericFamily,
                    fontFace.Pitch);
            }
        }
    }
}
