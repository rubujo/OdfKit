using System.Globalization;
using System;
using System.Collections.Generic;
using System.Drawing;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Spreadsheet;

public partial class OdfCell
{
    #region Hyperlink, Rich Text & Annotation

    /// <summary>
    /// 設定儲存格的超連結。
    /// </summary>
    /// <param name="url">超連結 URL</param>
    /// <param name="displayText">連結顯示文字；為 null 時使用現有文字內容或 URL 本身</param>
    public void SetHyperlink(string url, string? displayText = null)
    {
        string text = displayText ?? (string.IsNullOrEmpty(DisplayText) ? url : DisplayText);

        var toRemove = new List<OdfNode>();
        foreach (var child in Node.Children)
            if (child.LocalName == "p" && child.NamespaceUri == OdfNamespaces.Text)
                toRemove.Add(child);
        foreach (var child in toRemove)
            Node.RemoveChild(child);

        var aNode = new OdfNode(OdfNodeType.Element, "a", OdfNamespaces.Text, "text");
        aNode.SetAttribute("type", OdfNamespaces.XLink, "simple", "xlink");
        aNode.SetAttribute("href", OdfNamespaces.XLink, url, "xlink");
        aNode.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = text });

        var pNode = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
        pNode.AppendChild(aNode);
        Node.AppendChild(pNode);
        ValueType = "string";
    }

    /// <summary>
    /// 取得儲存格的超連結 URL；若無超連結則回傳 null。
    /// </summary>
    public string? GetHyperlinkUrl()
    {
        foreach (var child in Node.Children)
        {
            if (child.LocalName != "p" || child.NamespaceUri != OdfNamespaces.Text)
                continue;
            foreach (var inner in child.Children)
            {
                if (inner.LocalName == "a" && inner.NamespaceUri == OdfNamespaces.Text)
                    return inner.GetAttribute("href", OdfNamespaces.XLink);
            }
        }
        return null;
    }

    /// <summary>
    /// 移除儲存格的超連結，保留顯示文字。
    /// </summary>
    public void RemoveHyperlink()
    {
        foreach (var child in Node.Children)
        {
            if (child.LocalName != "p" || child.NamespaceUri != OdfNamespaces.Text)
                continue;
            var toUnwrap = new List<OdfNode>();
            foreach (var inner in child.Children)
                if (inner.LocalName == "a" && inner.NamespaceUri == OdfNamespaces.Text)
                    toUnwrap.Add(inner);
            foreach (var aNode in toUnwrap)
            {
                string linkText = aNode.TextContent;
                child.InsertBefore(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = linkText }, aNode);
                child.RemoveChild(aNode);
            }
            break;
        }
    }

    /// <summary>
    /// 取得儲存格的富文字內容；若為純文字或空白則回傳 null。
    /// </summary>
    public OdfRichText? GetRichText()
    {
        OdfRichText? richText = null;
        foreach (var child in Node.Children)
        {
            if (child.LocalName != "p" || child.NamespaceUri != OdfNamespaces.Text)
                continue;
            bool hasSpans = false;
            foreach (var inner in child.Children)
            {
                if (inner.LocalName == "span" && inner.NamespaceUri == OdfNamespaces.Text)
                { hasSpans = true; break; }
            }
            if (!hasSpans)
                continue;

            richText ??= new OdfRichText();
            foreach (var inner in child.Children)
            {
                if (inner.LocalName == "span" && inner.NamespaceUri == OdfNamespaces.Text)
                {
                    string styleName = inner.GetAttribute("style-name", OdfNamespaces.Text) ?? string.Empty;
                    bool bold = _doc.StyleEngine.GetStyleProperty(styleName, "font-weight", OdfNamespaces.Fo, "text") == "bold";
                    bool italic = _doc.StyleEngine.GetStyleProperty(styleName, "font-style", OdfNamespaces.Fo, "text") == "italic";
                    bool underline = _doc.StyleEngine.GetStyleProperty(styleName, "text-underline-style", OdfNamespaces.Style, "text") != null;
                    string? colorVal = _doc.StyleEngine.GetStyleProperty(styleName, "color", OdfNamespaces.Fo, "text");
                    OdfColor? color = colorVal != null && OdfColor.TryParse(colorVal, out OdfColor c) ? c : (OdfColor?)null;
                    string? fontName = _doc.StyleEngine.GetStyleProperty(styleName, "font-name", OdfNamespaces.Style, "text");
                    richText.AddRun(inner.TextContent, bold, italic, color, fontName, underline);
                }
                else if (inner.NodeType == OdfNodeType.Text && !string.IsNullOrEmpty(inner.TextContent))
                {
                    richText.AddRun(inner.TextContent);
                }
                else if (inner.LocalName == "line-break" && inner.NamespaceUri == OdfNamespaces.Text)
                {
                    richText.AddLineBreak();
                }
            }
        }
        return richText;
    }

    /// <summary>
    /// 設定儲存格的富文字內容，取代現有文字。
    /// </summary>
    /// <param name="richText">要寫入儲存格的富文字內容</param>
    public void SetRichText(OdfRichText richText)
    {
        if (richText is null)
        {
            throw new ArgumentNullException(nameof(richText));
        }

        var toRemove = new List<OdfNode>();
        foreach (var child in Node.Children)
            if (child.LocalName == "p" && child.NamespaceUri == OdfNamespaces.Text)
                toRemove.Add(child);
        foreach (var child in toRemove)
            Node.RemoveChild(child);

        var pNode = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
        bool needsWrap = false;
        foreach (var run in richText.Runs)
        {
            bool hasFormatting = run.Bold || run.Italic || run.Underline || run.Color.HasValue || !string.IsNullOrEmpty(run.FontFamily);
            if (hasFormatting)
            {
                string styleName = _doc.GetOrCreateCharacterStyle(run.Bold, run.Italic, run.Underline, run.Color, run.FontFamily);
                var span = new OdfNode(OdfNodeType.Element, "span", OdfNamespaces.Text, "text");
                span.SetAttribute("style-name", OdfNamespaces.Text, styleName, "text");
                AppendTextContent(span, run.Text, ref needsWrap);
                pNode.AppendChild(span);
            }
            else
            {
                AppendTextContent(pNode, run.Text, ref needsWrap);
            }
        }
        Node.AppendChild(pNode);
        ValueType = "string";
        if (needsWrap)
        {
            SetStyleProperty("table-cell-properties", "wrap-option", OdfNamespaces.Fo, "wrap", "fo");
        }
    }

    /// <summary>
    /// 取得儲存格的批注；若無批注則回傳 null。
    /// </summary>
    public OdfCellAnnotation? GetAnnotation()
    {
        foreach (var child in Node.Children)
        {
            if (child.LocalName != "annotation" || child.NamespaceUri != OdfNamespaces.Office)
                continue;
            string text = string.Empty;
            string? author = null;
            DateTime? date = null;
            bool visible = child.GetAttribute("display", OdfNamespaces.Office) == "true";

            foreach (var inner in child.Children)
            {
                if (inner.LocalName == "creator" && inner.NamespaceUri == OdfNamespaces.Dc)
                    author = inner.TextContent;
                else if (inner.LocalName == "date" && inner.NamespaceUri == OdfNamespaces.Dc)
                {
                    if (DateTime.TryParse(inner.TextContent, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime dt))
                        date = dt;
                }
                else if (inner.LocalName == "p" && inner.NamespaceUri == OdfNamespaces.Text)
                    text = inner.TextContent;
            }
            return new OdfCellAnnotation { Text = text, Author = author, Date = date, Visible = visible };
        }
        return null;
    }

    /// <summary>
    /// 設定儲存格的批注。若已有批注則覆蓋。
    /// </summary>
    /// <param name="text">批注內容</param>
    /// <param name="author">作者名稱</param>
    /// <param name="visible">是否顯示（預設為 false）</param>
    public void SetAnnotation(string text, string? author = null, bool visible = false)
    {
        RemoveAnnotation();
        var ann = new OdfNode(OdfNodeType.Element, "annotation", OdfNamespaces.Office, "office");
        ann.SetAttribute("display", OdfNamespaces.Office, visible ? "true" : "false", "office");

        if (!string.IsNullOrEmpty(author))
        {
            var creator = new OdfNode(OdfNodeType.Element, "creator", OdfNamespaces.Dc, "dc");
            creator.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = author! });
            ann.AppendChild(creator);
        }

        var dateNode = new OdfNode(OdfNodeType.Element, "date", OdfNamespaces.Dc, "dc");
        dateNode.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty)
        { TextContent = DateTime.UtcNow.ToString("O") });
        ann.AppendChild(dateNode);

        var pNode = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
        pNode.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = text });
        ann.AppendChild(pNode);

        Node.AppendChild(ann);
    }

    /// <summary>
    /// 移除儲存格的批注。
    /// </summary>
    public void RemoveAnnotation()
    {
        var toRemove = new List<OdfNode>();
        foreach (var child in Node.Children)
            if (child.LocalName == "annotation" && child.NamespaceUri == OdfNamespaces.Office)
                toRemove.Add(child);
        foreach (var child in toRemove)
            Node.RemoveChild(child);
    }

    #endregion
}
