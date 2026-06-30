using OdfKit.Styles;

namespace OdfKit.Text;

/// <summary>
/// 文字文件字型宣告引擎（內部協作者）。
/// </summary>
internal static class TextDocumentFontFaceEngine
{
    /// <summary>
    /// 在 content.xml 與 styles.xml 中新增或更新字型宣告。
    /// </summary>
    internal static void AddFontFace(
        TextDocument.TextDocumentCoreCollaborators ctx,
        string name,
        string fontFamily,
        string? genericFamily = null,
        string? pitch = null)
    {
        OdfFontFaceDeclarationEngine.AddToDom(ctx.ContentDom, name, fontFamily, genericFamily, pitch);
        if (ctx.StylesDom is not null)
        {
            OdfFontFaceDeclarationEngine.AddToDom(ctx.StylesDom, name, fontFamily, genericFamily, pitch);
        }
    }
}
