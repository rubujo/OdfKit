using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// 文字文件書籤讀取引擎（內部協作者）。
/// </summary>
internal static class TextDocumentBookmarkReadEngine
{
    internal static IReadOnlyList<OdfBookmarkInfo> GetBookmarks(OdfNode bodyTextRoot)
    {
        List<OdfBookmarkInfo> bookmarks = [];
        ScanBookmarks(bodyTextRoot, bookmarks);
        return bookmarks.AsReadOnly();
    }

    private static void ScanBookmarks(OdfNode node, List<OdfBookmarkInfo> bookmarks)
    {
        if (node.NodeType is OdfNodeType.Element && node.NamespaceUri == OdfNamespaces.Text)
        {
            switch (node.LocalName)
            {
                case "bookmark":
                    TryAddBookmark(node, OdfBookmarkKind.Inline, bookmarks);
                    break;
                case "bookmark-start":
                    TryAddBookmark(node, OdfBookmarkKind.RangeStart, bookmarks);
                    break;
                case "bookmark-end":
                    TryAddBookmark(node, OdfBookmarkKind.RangeEnd, bookmarks);
                    break;
            }
        }

        foreach (OdfNode child in node.Children)
            ScanBookmarks(child, bookmarks);
    }

    private static void TryAddBookmark(OdfNode node, OdfBookmarkKind kind, List<OdfBookmarkInfo> bookmarks)
    {
        string? name = node.GetAttribute("name", OdfNamespaces.Text);
        if (string.IsNullOrEmpty(name))
            return;

        bookmarks.Add(new OdfBookmarkInfo(name!, kind));
    }
}
