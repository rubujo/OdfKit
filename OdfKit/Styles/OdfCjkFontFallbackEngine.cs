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
    private static readonly (string Name, string Family, string GenericFamily, string Pitch)[] DefaultFallbackFonts =
    [
        ("TW-Kai-98_1", "TW-Kai-98_1", "system-serif", "variable"),
        ("TW-Kai-Ext-B-98_1", "TW-Kai-Ext-B-98_1", "system-serif", "variable"),
        ("TW-Kai-Plus-98_1", "TW-Kai-Plus-98_1", "system-serif", "variable"),
        ("TW-Song-98_1", "TW-Song-98_1", "system-serif", "variable"),
        ("TW-Song-Ext-B-98_1", "TW-Song-Ext-B-98_1", "system-serif", "variable"),
        ("TW-Song-Plus-98_1", "TW-Song-Plus-98_1", "system-serif", "variable"),
        ("PMingLiU", "PMingLiU", "system-serif", "variable"),
        ("Microsoft JhengHei", "Microsoft JhengHei", "system-sans-serif", "variable"),
        ("MS Mincho", "MS Mincho", "system-serif", "variable"),
        ("MS Gothic", "MS Gothic", "system-sans-serif", "variable"),
        ("SimSun", "SimSun", "system-serif", "variable"),
        ("Microsoft YaHei", "Microsoft YaHei", "system-sans-serif", "variable"),
        ("Malgun Gothic", "Malgun Gothic", "system-sans-serif", "variable")
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

        foreach ((string name, string family, string genericFamily, string pitch) in DefaultFallbackFonts)
        {
            OdfFontFaceDeclarationEngine.AddFontFace(document, name, family, genericFamily, pitch);
        }
    }

    /// <summary>
    /// Applies the default CJK fallback font declarations to DOM roots.
    /// 將預設的中日韓字型遞補宣告套用至 DOM 根節點。
    /// </summary>
    /// <param name="contentDom">The content DOM root. / content DOM 根節點。</param>
    /// <param name="stylesDom">The styles DOM root. / styles DOM 根節點。</param>
    internal static void ApplyFontFallback(OdfNode contentDom, OdfNode? stylesDom)
    {
        foreach ((string name, string family, string genericFamily, string pitch) in DefaultFallbackFonts)
        {
            OdfFontFaceDeclarationEngine.AddToDom(contentDom, name, family, genericFamily, pitch);
            if (stylesDom is not null)
            {
                OdfFontFaceDeclarationEngine.AddToDom(stylesDom, name, family, genericFamily, pitch);
            }
        }
    }
}
