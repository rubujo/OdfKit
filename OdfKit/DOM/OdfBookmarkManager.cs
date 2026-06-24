using System;
using System.Collections.Generic;
using System.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Text;

namespace OdfKit.DOM;

/// <summary>
/// 提供安全讀寫與操作 ODF 文件中書籤的高階管理器。
/// </summary>
public sealed class OdfBookmarkManager
{
    private readonly OdfDocument _doc;

    /// <summary>
    /// 初始化 <see cref="OdfBookmarkManager"/> 類別的新執行個體。
    /// </summary>
    /// <param name="doc">所屬的 ODF 文件</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="doc"/> 為 <see langword="null"/> 時擲出</exception>
    public OdfBookmarkManager(OdfDocument doc)
    {
        _doc = doc ?? throw new ArgumentNullException(nameof(doc));
    }

    /// <summary>
    /// 取得指定名稱的書籤操作介面。
    /// </summary>
    /// <param name="name">書籤名稱</param>
    /// <returns>書籤操作執行個體</returns>
    public OdfBookmark this[string name] => new(_doc, name);

    /// <summary>
    /// 取得目前文件中所有書籤的名稱集合。
    /// </summary>
    public IEnumerable<string> Names
    {
        get
        {
            if (_doc.ContentRoot == null)
            {
                return Enumerable.Empty<string>();
            }

            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in _doc.ContentRoot.Descendants())
            {
                if (node.NodeType == OdfNodeType.Element && node.NamespaceUri == OdfNamespaces.Text)
                {
                    if (node.LocalName is "bookmark" or "bookmark-start" or "bookmark-end")
                    {
                        string? name = node.GetAttribute("name", OdfNamespaces.Text);
                        if (!string.IsNullOrEmpty(name))
                        {
                            names.Add(name!);
                        }
                    }
                }
            }
            return names;
        }
    }
}

/// <summary>
/// 代表一個 ODF 文件中的書籤，支援讀取與替換其文字值。
/// </summary>
public sealed class OdfBookmark
{
    private readonly OdfDocument _doc;

    /// <summary>
    /// 取得書籤名稱。
    /// </summary>
    public string Name { get; }

    internal OdfBookmark(OdfDocument doc, string name)
    {
        _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>
    /// 取得或設定書籤的文字內容。
    /// </summary>
    /// <exception cref="KeyNotFoundException">當在文件中找不到指定名稱的書籤節點時擲出</exception>
    public string Value
    {
        get
        {
            var nodes = FindBookmarkNodes();
            if (nodes.Start == null && nodes.Inline == null)
            {
                throw new KeyNotFoundException(OdfLocalizer.GetMessage("Err_Bookmark_NotFound") ?? $"找不到名稱為 '{Name}' 的書籤。");
            }

            if (nodes.Inline != null)
            {
                return string.Empty;
            }

            if (nodes.Start != null && nodes.End != null)
            {
                var descendants = _doc.ContentRoot.Descendants().ToList();
                int startIdx = descendants.IndexOf(nodes.Start);
                int endIdx = descendants.IndexOf(nodes.End);

                if (startIdx >= 0 && endIdx > startIdx)
                {
                    var sb = new System.Text.StringBuilder();
                    for (int i = startIdx + 1; i < endIdx; i++)
                    {
                        var node = descendants[i];
                        if (node.NodeType == OdfNodeType.Text)
                        {
                            sb.Append(node.TextContent);
                        }
                    }
                    return sb.ToString();
                }
            }

            return string.Empty;
        }
        set
        {
            var nodes = FindBookmarkNodes();
            if (nodes.Start == null && nodes.Inline == null)
            {
                throw new KeyNotFoundException(OdfLocalizer.GetMessage("Err_Bookmark_NotFound") ?? $"找不到名稱為 '{Name}' 的書籤。");
            }

            var textVal = value ?? string.Empty;

            if (nodes.Inline != null)
            {
                // 行內書籤：直接在該節點後面插入新文字
                var newText = new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = textVal };
                nodes.Inline.Parent?.InsertAfter(newText, nodes.Inline);
                return;
            }

            var startNode = nodes.Start!;
            if (nodes.End == null)
            {
                // 只有 start 沒有 end：在 start 之後插入新文字
                var newText = new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = textVal };
                startNode.Parent?.InsertAfter(newText, startNode);
                return;
            }

            var endNode = nodes.End!;

            if (startNode.Parent == endNode.Parent && startNode.Parent != null)
            {
                // 正常同階層情況：移除 start 和 end 之間的所有節點
                var parent = startNode.Parent;
                var curr = startNode.NextSibling;
                var toRemove = new List<OdfNode>();
                while (curr != null && curr != endNode)
                {
                    toRemove.Add(curr);
                    curr = curr.NextSibling;
                }
                foreach (var node in toRemove)
                {
                    parent.RemoveChild(node);
                }

                var newText = new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = textVal };
                parent.InsertAfter(newText, startNode);
            }
            else
            {
                // 跨階層情況：尋找 start 和 end 之間的所有 Text 節點並移除
                var descendants = _doc.ContentRoot.Descendants().ToList();
                int startIdx = descendants.IndexOf(startNode);
                int endIdx = descendants.IndexOf(endNode);

                if (startIdx >= 0 && endIdx > startIdx)
                {
                    for (int i = startIdx + 1; i < endIdx; i++)
                    {
                        var node = descendants[i];
                        if (node.NodeType == OdfNodeType.Text)
                        {
                            node.Parent?.RemoveChild(node);
                        }
                    }
                }

                var newText = new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = textVal };
                startNode.Parent?.InsertAfter(newText, startNode);
            }
        }
    }

    private (OdfNode? Start, OdfNode? End, OdfNode? Inline) FindBookmarkNodes()
    {
        if (_doc.ContentRoot == null)
        {
            return (null, null, null);
        }

        OdfNode? start = null;
        OdfNode? end = null;
        OdfNode? inline = null;

        foreach (var node in _doc.ContentRoot.Descendants())
        {
            if (node.NodeType == OdfNodeType.Element && node.NamespaceUri == OdfNamespaces.Text)
            {
                if (node.LocalName == "bookmark-start" && node.GetAttribute("name", OdfNamespaces.Text) == Name)
                {
                    start = node;
                }
                else if (node.LocalName == "bookmark-end" && node.GetAttribute("name", OdfNamespaces.Text) == Name)
                {
                    end = node;
                }
                else if (node.LocalName == "bookmark" && node.GetAttribute("name", OdfNamespaces.Text) == Name)
                {
                    inline = node;
                }
            }
        }

        return (start, end, inline);
    }
}
