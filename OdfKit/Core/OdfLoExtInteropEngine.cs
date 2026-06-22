using System;
using OdfKit.DOM;

namespace OdfKit.Core;

/// <summary>
/// LibreOffice <c>loext</c> 擴充屬性與 ODF 標準屬性之互通正規化引擎（內部協作者）。
/// </summary>
internal static class OdfLoExtInteropEngine
{
    /// <summary>
    /// 於文件載入後正規化 <c>content.xml</c> 與 <c>styles.xml</c> 中的 LibreOffice 擴充屬性。
    /// </summary>
    /// <param name="contentDom">內容 DOM 根節點</param>
    /// <param name="stylesDom">樣式 DOM 根節點</param>
    internal static void NormalizeLoadedDocument(OdfNode contentDom, OdfNode stylesDom)
    {
        NormalizeDecorativeAttributes(contentDom);
        NormalizeDecorativeAttributes(stylesDom);
    }

    /// <summary>
    /// 判斷繪圖節點是否標記為裝飾性（支援標準 <c>draw:decorative</c> 與 LibreOffice <c>loext:decorative</c>）。
    /// </summary>
    /// <param name="node">繪圖外框或圖形節點</param>
    /// <returns>若標記為裝飾性則為 <see langword="true"/></returns>
    internal static bool IsDecorative(OdfNode node)
    {
        if (IsTruthyDecorative(node.GetAttribute("decorative", OdfNamespaces.Draw)))
            return true;

        return IsTruthyDecorative(node.GetAttribute("decorative", OdfNamespaces.LoExt));
    }

    private static void NormalizeDecorativeAttributes(OdfNode node)
    {
        if (node.NodeType is OdfNodeType.Element &&
            node.NamespaceUri == OdfNamespaces.Draw &&
            node.GetAttribute("decorative", OdfNamespaces.LoExt) is string loextValue)
        {
            if (IsTruthyDecorative(loextValue) &&
                !IsTruthyDecorative(node.GetAttribute("decorative", OdfNamespaces.Draw)))
            {
                node.SetAttribute("decorative", OdfNamespaces.Draw, "true", "draw");
            }

            node.RemoveAttribute("decorative", OdfNamespaces.LoExt);
        }

        foreach (OdfNode child in node.Children)
            NormalizeDecorativeAttributes(child);
    }

    private static bool IsTruthyDecorative(string? value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
}
