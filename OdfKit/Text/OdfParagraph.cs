using System;
using System.Collections;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Text;

/// <summary>
/// 表示文字文件中的段落。
/// </summary>
public partial class OdfParagraph
{
    internal OdfParagraph(OdfNode node, TextDocument doc)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
        Doc = doc ?? throw new ArgumentNullException(nameof(doc));
    }

    /// <summary>
    /// 取得與此段落相關聯的 OdfNode 節點。
    /// </summary>
    internal OdfNode Node { get; }

    /// <summary>
    /// 取得所屬的文字文件。
    /// </summary>
    protected readonly TextDocument Doc;

    /// <summary>
    /// 取得或設定段落的文字內容。
    /// </summary>
    public string TextContent
    {
        get => Node.TextContent;
        set => Node.TextContent = value;
    }

    /// <summary>
    /// 取得或設定段落的樣式名稱。
    /// </summary>
    public string? StyleName
    {
        get => Node.GetAttribute("style-name", OdfNamespaces.Text);
        set
        {
            if (Doc.TrackedChanges)
            {
                Doc.TrackFormatChange(Node, "paragraph");
            }
            Node.SetAttribute("style-name", OdfNamespaces.Text, value ?? string.Empty, "text");
        }
    }

    internal TextDocument DocProperty => Doc;
    internal OdfStyleEngine StyleEngine => Doc.StyleEngine;

    private OdfKit.Styles.OdfParagraphStyleProxy? _styleProxy;

    /// <summary>
    /// 取得此段落的高階樣式設定代理 Facade。
    /// </summary>
    public OdfKit.Styles.OdfParagraphStyleProxy Style => _styleProxy ??= new OdfKit.Styles.OdfParagraphStyleProxy(this);

    /// <summary>
    /// 取得或設定段落的水平對齊方式。
    /// </summary>
    public string? HorizontalAlignment
    {
        get => Doc.StyleEngine.GetStyleProperty(StyleName ?? string.Empty, "text-align", OdfNamespaces.Fo, "paragraph");
        set => Doc.StyleEngine.SetLocalStyleProperty(Node, "paragraph", "paragraph-properties", "text-align", OdfNamespaces.Fo, value ?? string.Empty, "fo");
    }

    /// <summary>
    /// 取得或設定段落的書寫模式。
    /// </summary>
    public OdfWritingMode WritingMode
    {
        get => OdfWritingModeExtensions.FromOdfToken(Doc.StyleEngine.GetStyleProperty(StyleName ?? string.Empty, "writing-mode", OdfNamespaces.Style, "paragraph"));
        set => Doc.StyleEngine.SetLocalStyleProperty(Node, "paragraph", "paragraph-properties", "writing-mode", OdfNamespaces.Style, value.ToOdfToken(), "style");
    }

    /// <summary>
    /// 取得或設定段落的西文字型名稱。
    /// </summary>
    public string? FontName
    {
        get => Doc.StyleEngine.GetStyleProperty(StyleName ?? string.Empty, "font-name", OdfNamespaces.Style, "paragraph");
        set => Doc.StyleEngine.SetLocalStyleProperty(Node, "paragraph", "paragraph-properties", "font-name", OdfNamespaces.Style, value ?? string.Empty, "style");
    }

    /// <summary>
    /// 取得或設定段落的東亞（中日韓）字型名稱。
    /// </summary>
    public string? FontNameAsian
    {
        get => Doc.StyleEngine.GetStyleProperty(StyleName ?? string.Empty, "font-name-asian", OdfNamespaces.Style, "paragraph");
        set => Doc.StyleEngine.SetLocalStyleProperty(Node, "paragraph", "paragraph-properties", "font-name-asian", OdfNamespaces.Style, value ?? string.Empty, "style");
    }

    /// <summary>
    /// 取得或設定段落的複雜文字字型名稱。
    /// </summary>
    public string? FontNameComplex
    {
        get => Doc.StyleEngine.GetStyleProperty(StyleName ?? string.Empty, "font-name-complex", OdfNamespaces.Style, "paragraph");
        set => Doc.StyleEngine.SetLocalStyleProperty(Node, "paragraph", "paragraph-properties", "font-name-complex", OdfNamespaces.Style, value ?? string.Empty, "style");
    }

    /// <summary>
    /// 取得或設定段落的西文字型大小。
    /// </summary>
    public string? FontSize
    {
        get => Doc.StyleEngine.GetStyleProperty(StyleName ?? string.Empty, "font-size", OdfNamespaces.Fo, "paragraph");
        set => Doc.StyleEngine.SetLocalStyleProperty(Node, "paragraph", "paragraph-properties", "font-size", OdfNamespaces.Fo, value ?? string.Empty, "fo");
    }

    /// <summary>
    /// 取得或設定段落的東亞（中日韓）字型大小。
    /// </summary>
    public string? FontSizeAsian
    {
        get => Doc.StyleEngine.GetStyleProperty(StyleName ?? string.Empty, "font-size-asian", OdfNamespaces.Fo, "paragraph");
        set => Doc.StyleEngine.SetLocalStyleProperty(Node, "paragraph", "paragraph-properties", "font-size-asian", OdfNamespaces.Fo, value ?? string.Empty, "fo");
    }

    /// <summary>
    /// 取得或設定段落的複雜文字字型大小。
    /// </summary>
    public string? FontSizeComplex
    {
        get => Doc.StyleEngine.GetStyleProperty(StyleName ?? string.Empty, "font-size-complex", OdfNamespaces.Fo, "paragraph");
        set => Doc.StyleEngine.SetLocalStyleProperty(Node, "paragraph", "paragraph-properties", "font-size-complex", OdfNamespaces.Fo, value ?? string.Empty, "fo");
    }

    /// <summary>
    /// 設定段落的字型名稱。
    /// </summary>
    /// <param name="westernFont">西文字型名稱</param>
    /// <param name="asianFont">東亞（中日韓）字型名稱</param>
    /// <param name="complexFont">複雜文字字型名稱</param>
    public void SetFont(string westernFont, string? asianFont = null, string? complexFont = null)
    {
        FontName = westernFont;
        FontNameAsian = asianFont ?? westernFont;
        FontNameComplex = complexFont ?? westernFont;
    }

    /// <summary>
    /// 設定段落的字型大小。
    /// </summary>
    /// <param name="westernSize">西文字型大小</param>
    /// <param name="asianSize">東亞字型大小</param>
    /// <param name="complexSize">複雜文字字型大小</param>
    public void SetFontSize(string westernSize, string? asianSize = null, string? complexSize = null)
    {
        FontSize = westernSize;
        FontSizeAsian = asianSize ?? westernSize;
        FontSizeComplex = complexSize ?? westernSize;
    }

    /// <summary>
    /// 在段落結尾新增一個文字片段。
    /// </summary>
    /// <param name="text">要新增的文字內容</param>
    /// <returns>建立的文字片段物件</returns>
    public OdfTextRun AddTextRun(string text)
    {
        var spanNode = OdfNodeFactory.CreateElement("span", OdfNamespaces.Text, "text");
        spanNode.TextContent = text;
        if (Doc.TrackedChanges)
        {
            string changeId = Doc.RecordTrackedChange("insertion");
            var startNode = new OdfNode(OdfNodeType.Element, "change-start", OdfNamespaces.Text, "text");
            startNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");
            var endNode = new OdfNode(OdfNodeType.Element, "change-end", OdfNamespaces.Text, "text");
            endNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");

            Node.AppendChild(startNode);
            Node.AppendChild(spanNode);
            Node.AppendChild(endNode);
        }
        else
        {
            Node.AppendChild(spanNode);
        }
        return new OdfTextRun(spanNode, Doc);
    }

    /// <summary>
    /// 在段落中新增軟分頁符號。
    /// </summary>
    public void AddSoftPageBreak()
    {
        var node = OdfNodeFactory.CreateElement("soft-page-break", OdfNamespaces.Text, "text");
        Node.AppendChild(node);
    }

    /// <summary>
    /// 在段落中新增定位點（Tab）字元。
    /// </summary>
    public void AddTab()
    {
        var node = OdfNodeFactory.CreateElement("tab", OdfNamespaces.Text, "text");
        Node.AppendChild(node);
    }

    /// <summary>
    /// 在段落中新增換行符號。
    /// </summary>
    public void AddLineBreak()
    {
        var node = OdfNodeFactory.CreateElement("line-break", OdfNamespaces.Text, "text");
        Node.AppendChild(node);
    }

    /// <summary>
    /// 在段落中新增指定數量的空格項目。
    /// </summary>
    /// <param name="count">空格數量</param>
    public void AddSpace(int count = 1)
    {
        var node = OdfNodeFactory.CreateElement("s", OdfNamespaces.Text, "text");
        if (count > 1)
        {
            node.SetAttribute("c", OdfNamespaces.Text, count.ToString(), "text");
        }
        Node.AppendChild(node);
    }

    /// <summary>
    /// 刪除此段落。
    /// </summary>
    public void Delete()
    {
        Doc.DeleteNode(Node);
    }

    /// <summary>
    /// 取得此段落的所有文字片段（Runs）。存取時會自動將直屬的文字節點分裂包裝為 span 節點。
    /// </summary>
    public System.Collections.Generic.IEnumerable<OdfTextRun> Runs
    {
        get
        {
            // 裂變同步：若存在直屬的 text 節點，則先將其包裝到一個全新的 <text:span> 中
            var children = new System.Collections.Generic.List<OdfNode>(Node.Children);
            foreach (var child in children)
            {
                if (child.NodeType == OdfNodeType.Text && !string.IsNullOrEmpty(child.TextContent))
                {
                    var spanNode = OdfNodeFactory.CreateElement("span", OdfNamespaces.Text, "text");
                    Node.InsertBefore(spanNode, child);
                    spanNode.AppendChild(child);
                }
            }

            // 回傳所有的 text:span 節點包裝
            foreach (var child in Node.Children)
            {
                if (child.NodeType == OdfNodeType.Element &&
                    child.LocalName == "span" &&
                    child.NamespaceUri == OdfNamespaces.Text)
                {
                    yield return new OdfTextRun(child, Doc);
                }
            }
        }
    }

    /// <summary>
    /// 清除此段落內的所有文字片段（移除所有的 span 節點）。
    /// </summary>
    public void ClearRuns()
    {
        var spans = new System.Collections.Generic.List<OdfNode>();
        foreach (var child in Node.Children)
        {
            if (child.NodeType == OdfNodeType.Element &&
                child.LocalName == "span" &&
                child.NamespaceUri == OdfNamespaces.Text)
            {
                spans.Add(child);
            }
        }
        foreach (var span in spans)
        {
            Node.RemoveChild(span);
        }
    }
}
