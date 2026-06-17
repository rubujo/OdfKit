using OdfKit.Core;
using OdfKit.DOM;

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
        AddToDom(ctx, ctx.ContentDom, name, fontFamily, genericFamily, pitch);
        if (ctx.StylesDom is not null)
            AddToDom(ctx, ctx.StylesDom, name, fontFamily, genericFamily, pitch);
    }

    private static void AddToDom(
        TextDocument.TextDocumentCoreCollaborators ctx,
        OdfNode domRoot,
        string name,
        string fontFamily,
        string? genericFamily,
        string? pitch)
    {
        OdfNode fontDecls = ctx.FindOrCreateChild(domRoot, "font-face-decls", OdfNamespaces.Office, "office");
        foreach (OdfNode child in fontDecls.Children)
        {
            if (child.LocalName == "font-face" && child.NamespaceUri == OdfNamespaces.Style && child.GetAttribute("name", OdfNamespaces.Style) == name)
            {
                child.SetAttribute("font-family", OdfNamespaces.Svg, fontFamily, "svg");
                if (genericFamily is not null)
                    child.SetAttribute("font-family-generic", OdfNamespaces.Style, genericFamily, "style");
                if (pitch is not null)
                    child.SetAttribute("font-pitch", OdfNamespaces.Style, pitch, "style");
                return;
            }
        }

        var fontFace = new OdfNode(OdfNodeType.Element, "font-face", OdfNamespaces.Style, "style");
        fontFace.SetAttribute("name", OdfNamespaces.Style, name, "style");
        fontFace.SetAttribute("font-family", OdfNamespaces.Svg, fontFamily, "svg");
        if (genericFamily is not null)
            fontFace.SetAttribute("font-family-generic", OdfNamespaces.Style, genericFamily, "style");
        if (pitch is not null)
            fontFace.SetAttribute("font-pitch", OdfNamespaces.Style, pitch, "style");
        fontDecls.AppendChild(fontFace);
    }
}
