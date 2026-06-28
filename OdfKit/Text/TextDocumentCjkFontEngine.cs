namespace OdfKit.Text;

/// <summary>
/// 文字文件中日韓（CJK）字型遞補引擎（內部協作者）。
/// </summary>
internal static class TextDocumentCjkFontEngine
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
    /// 套用預設的中日韓字型遞補宣告。
    /// </summary>
    internal static void ApplyFontFallback(TextDocument.TextDocumentCoreCollaborators ctx)
    {
        foreach ((string name, string family, string genericFamily, string pitch) in DefaultFallbackFonts)
        {
            TextDocumentFontFaceEngine.AddFontFace(ctx, name, family, genericFamily, pitch);
        }
    }
}
