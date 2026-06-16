using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Core;

public abstract partial class OdfDocument
{
    #region Statistics & Document Structure Diagnostics


    /// <summary>
    /// 更新文件統計中繼資料。
    /// </summary>
    protected virtual void UpdateDocumentStatistics()
    {
        int wordCount = 0;
        int charCount = 0;
        int paragraphCount = 0;
        int tableCount = 0;
        int imageCount = 0;

        TraverseForStats(ContentDom, ref wordCount, ref charCount, ref paragraphCount, ref tableCount, ref imageCount);

        var metaRoot = FindOrCreateMetaRoot();
        OdfNode? statNode = null;
        foreach (var child in metaRoot.Children)
        {
            if (child.LocalName == "document-statistic" && child.NamespaceUri == OdfNamespaces.Meta)
            {
                statNode = child;
                break;
            }
        }

        if (statNode == null)
        {
            statNode = new OdfNode(OdfNodeType.Element, "document-statistic", OdfNamespaces.Meta, "meta");
            metaRoot.AppendChild(statNode);
        }

        statNode.SetAttribute("word-count", OdfNamespaces.Meta, wordCount.ToString(), "meta");
        statNode.SetAttribute("character-count", OdfNamespaces.Meta, charCount.ToString(), "meta");
        statNode.SetAttribute("paragraph-count", OdfNamespaces.Meta, paragraphCount.ToString(), "meta");
        statNode.SetAttribute("table-count", OdfNamespaces.Meta, tableCount.ToString(), "meta");
        statNode.SetAttribute("image-count", OdfNamespaces.Meta, imageCount.ToString(), "meta");
        statNode.SetAttribute("page-count", OdfNamespaces.Meta, "1", "meta"); // Layout engine placeholder
    }

    private void TraverseForStats(OdfNode node, ref int words, ref int chars, ref int paragraphs, ref int tables, ref int images)
    {
        if (node.NodeType == OdfNodeType.Text)
        {
            string text = node.TextContent;
            chars += text.Length;

            string[] parts = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            words += parts.Length;
            return;
        }

        if (node.LocalName == "p" && node.NamespaceUri == OdfNamespaces.Text)
            paragraphs++;
        else if (node.LocalName == "table" && node.NamespaceUri == OdfNamespaces.Table)
            tables++;
        else if (node.LocalName == "image" && node.NamespaceUri == OdfNamespaces.Draw)
            images++;

        foreach (var child in node.Children)
        {
            TraverseForStats(child, ref words, ref chars, ref paragraphs, ref tables, ref images);
        }
    }


    #endregion
}
