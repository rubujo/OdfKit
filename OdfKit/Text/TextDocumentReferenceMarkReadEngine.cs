using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// 文字文件參考標記讀取引擎（內部協作者）。
/// </summary>
internal static class TextDocumentReferenceMarkReadEngine
{
    internal static IReadOnlyList<OdfReferenceMarkInfo> GetReferenceMarks(OdfNode bodyTextRoot)
    {
        List<OdfReferenceMarkInfo> marks = [];
        ScanReferenceMarks(bodyTextRoot, marks);
        return marks.AsReadOnly();
    }

    private static void ScanReferenceMarks(OdfNode node, List<OdfReferenceMarkInfo> marks)
    {
        if (node.NodeType is OdfNodeType.Element &&
            node.LocalName is "reference-mark" &&
            node.NamespaceUri == OdfNamespaces.Text)
        {
            string? name = node.GetAttribute("name", OdfNamespaces.Text);
            if (!string.IsNullOrEmpty(name))
                marks.Add(new OdfReferenceMarkInfo(name!));
        }

        foreach (OdfNode child in node.Children)
            ScanReferenceMarks(child, marks);
    }
}
