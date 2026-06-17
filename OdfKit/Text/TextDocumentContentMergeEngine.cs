using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// 文字文件內容合併引擎（內部協作者）。
/// </summary>
internal static class TextDocumentContentMergeEngine
{
    /// <summary>
    /// 合併來源文字文件本文節點至目標文件。
    /// </summary>
    internal static void MergeContentNodes(
        TextDocument.TextDocumentCoreCollaborators dest,
        OdfDocument sourceDoc,
        Dictionary<string, string> renameMap)
    {
        var srcText = sourceDoc as TextDocument ?? throw new ArgumentException("Source document must be a TextDocument.");

        foreach (OdfNode child in srcText.BodyTextRoot.Children)
        {
            if (child.NodeType == OdfNodeType.Element)
            {
                OdfNode imported = OdfNode.ImportNode(child, srcText.Package, dest.Package);
                dest.RemapStylesInNodes(imported, renameMap);
                dest.BodyTextRoot.AppendChild(imported);
            }
        }
    }
}
