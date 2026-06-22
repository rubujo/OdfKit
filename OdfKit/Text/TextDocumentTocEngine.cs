using System.Globalization;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// 文字文件目錄（TOC）引擎（內部協作者）。
/// </summary>
internal static class TextDocumentTocEngine
{
    /// <summary>
    /// 新增目錄至文件本文結尾。
    /// </summary>
    internal static OdfTableOfContents AddTableOfContents(
        TextDocument document,
        TextDocument.TextDocumentCoreCollaborators ctx,
        string title,
        int outlineLevel)
    {
        OdfNode tocNode = OdfNodeFactory.CreateElement("table-of-content", OdfNamespaces.Text, "text");
        tocNode.SetAttribute("name", OdfNamespaces.Text, title, "text");

        OdfNode sourceNode = OdfNodeFactory.CreateElement("table-of-content-source", OdfNamespaces.Text, "text");
        sourceNode.SetAttribute("outline-level", OdfNamespaces.Text, outlineLevel.ToString(CultureInfo.InvariantCulture), "text");

        // 產生目錄條目範本以支援超連結與頁碼顯示
        for (int i = 1; i <= outlineLevel; i++)
        {
            OdfNode entryTemplate = OdfNodeFactory.CreateElement("table-of-content-entry-template", OdfNamespaces.Text, "text");
            entryTemplate.SetAttribute("outline-level", OdfNamespaces.Text, i.ToString(CultureInfo.InvariantCulture), "text");
            entryTemplate.SetAttribute("style-name", OdfNamespaces.Text, $"Contents_{i}", "text");

            // 1. 超連結起始
            OdfNode linkStart = OdfNodeFactory.CreateElement("index-entry-link-start", OdfNamespaces.Text, "text");
            linkStart.SetAttribute("style-name", OdfNamespaces.Text, "Index_20_Link", "text");
            entryTemplate.AppendChild(linkStart);

            // 2. 目錄文字
            OdfNode indexText = OdfNodeFactory.CreateElement("index-entry-text", OdfNamespaces.Text, "text");
            entryTemplate.AppendChild(indexText);

            // 3. 定位點（置右且具有點狀引導符）
            OdfNode tabStop = OdfNodeFactory.CreateElement("index-entry-tab-stop", OdfNamespaces.Text, "text");
            tabStop.SetAttribute("type", OdfNamespaces.Style, "right", "style");
            tabStop.SetAttribute("leader-char", OdfNamespaces.Style, ".", "style");
            entryTemplate.AppendChild(tabStop);

            // 4. 頁碼
            OdfNode pageNum = OdfNodeFactory.CreateElement("index-entry-page-number", OdfNamespaces.Text, "text");
            entryTemplate.AppendChild(pageNum);

            // 5. 超連結結束
            OdfNode linkEnd = OdfNodeFactory.CreateElement("index-entry-link-end", OdfNamespaces.Text, "text");
            entryTemplate.AppendChild(linkEnd);

            sourceNode.AppendChild(entryTemplate);
        }

        tocNode.AppendChild(sourceNode);

        OdfNode bodyNode = OdfNodeFactory.CreateElement("index-body", OdfNamespaces.Text, "text");

        OdfNode titlePara = OdfNodeFactory.CreateElement("p", OdfNamespaces.Text, "text");
        titlePara.SetAttribute("style-name", OdfNamespaces.Text, "Contents_20_Heading", "text");
        titlePara.TextContent = title;
        bodyNode.AppendChild(titlePara);

        tocNode.AppendChild(bodyNode);
        ctx.BodyTextRoot.AppendChild(tocNode);

        TextDocumentSettingsEngine.SetUpdateFieldsWhenOpening(ctx, true);
        return new OdfTableOfContents(tocNode, document);
    }
}

