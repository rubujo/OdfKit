using OdfKit.Styles;

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
        => OdfCjkFontFallbackEngine.ApplyFontFallback(ctx.ContentDom, ctx.StylesDom);
}
