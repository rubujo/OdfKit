namespace OdfKit.Text;

/// <summary>
/// 文字文件中日韓（CJK）字型遞補引擎（內部協作者）。
/// </summary>
internal static class TextDocumentCjkFontEngine
{
    /// <summary>
    /// 套用預設的中日韓字型遞補宣告。
    /// </summary>
    internal static void ApplyFontFallback(TextDocument.TextDocumentCoreCollaborators ctx)
    {
        TextDocumentFontFaceEngine.AddFontFace(ctx, "PMingLiU", "PMingLiU", "system-serif", "variable");
        TextDocumentFontFaceEngine.AddFontFace(ctx, "Microsoft JhengHei", "Microsoft JhengHei", "system-sans-serif", "variable");
        TextDocumentFontFaceEngine.AddFontFace(ctx, "MS Mincho", "MS Mincho", "system-serif", "variable");
        TextDocumentFontFaceEngine.AddFontFace(ctx, "MS Gothic", "MS Gothic", "system-sans-serif", "variable");
        TextDocumentFontFaceEngine.AddFontFace(ctx, "SimSun", "SimSun", "system-serif", "variable");
        TextDocumentFontFaceEngine.AddFontFace(ctx, "Microsoft YaHei", "Microsoft YaHei", "system-sans-serif", "variable");
        TextDocumentFontFaceEngine.AddFontFace(ctx, "Malgun Gothic", "Malgun Gothic", "system-sans-serif", "variable");
    }
}
