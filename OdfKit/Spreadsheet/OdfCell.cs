using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 表示 ODF 工作表中的一個儲存格。
/// </summary>
/// <remarks>
/// 初始化 <see cref="OdfCell"/> 類別的新執行個體。
/// </remarks>
/// <param name="node">儲存格 XML 節點</param>
/// <param name="row">以 0 為基準的列索引</param>
/// <param name="col">以 0 為基準的欄索引</param>
/// <param name="doc">試算表文件</param>
public class OdfCell(OdfNode node, int row, int col, SpreadsheetDocument doc)
{
    /// <summary>
    /// 取得代表儲存格的 XML 節點。
    /// </summary>
    internal OdfNode Node { get; } = node;

    /// <summary>
    /// 取得以 0 為基準的列索引。
    /// </summary>
    public int Row { get; } = row;

    /// <summary>
    /// 取得以 0 為基準的欄索引。
    /// </summary>
    public int Column { get; } = col;

    private readonly SpreadsheetDocument _doc = doc;

    internal SpreadsheetDocument Document => _doc;

    /// <summary>
    /// 取得或設定儲存格資料值的型態。
    /// </summary>
    public string ValueType
    {
        get => Node.GetAttribute("value-type", OdfNamespaces.Office) ?? string.Empty;
        set => Node.SetAttribute("value-type", OdfNamespaces.Office, value, "office");
    }

    /// <summary>
    /// 取得或設定儲存格的原始數值（office:value 屬性，字串格式）。
    /// </summary>
    public string RawValue
    {
        get => Node.GetAttribute("value", OdfNamespaces.Office) ?? string.Empty;
        set => Node.SetAttribute("value", OdfNamespaces.Office, value, "office");
    }

    /// <summary>
    /// 取得或設定儲存格的常用型別值。
    /// </summary>
    public object? CellValue
    {
        get
        {
            return ValueType switch
            {
                "float" => double.TryParse(RawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
                    ? number
                    : null,
                "boolean" => bool.TryParse(Node.GetAttribute("boolean-value", OdfNamespaces.Office), out bool flag)
                    ? flag
                    : null,
                "date" => Node.GetAttribute("date-value", OdfNamespaces.Office),
                "string" => DisplayText,
                _ => string.IsNullOrEmpty(DisplayText) ? null : DisplayText
            };
        }
        set
        {
            switch (value)
            {
                case null:
                    ClearValue();
                    break;
                case string text:
                    SetValue(text);
                    break;
                case bool flag:
                    SetValue(flag);
                    break;
                case DateTime date:
                    SetValue(date);
                    break;
                case byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal:
                    SetValue(Convert.ToDouble(value, CultureInfo.InvariantCulture));
                    break;
                default:
                    SetValue(value.ToString() ?? string.Empty);
                    break;
            }
        }
    }

    /// <summary>
    /// 取得或設定儲存格套用的表格樣式名稱。
    /// </summary>
    public string? StyleName
    {
        get => Node.GetAttribute("style-name", OdfNamespaces.Table);
        set
        {
            if (string.IsNullOrEmpty(value))
            {
                Node.RemoveAttribute("style-name", OdfNamespaces.Table);
            }
            else
            {
                Node.SetAttribute("style-name", OdfNamespaces.Table, value!, "table");
            }
        }
    }

    private OdfKit.Styles.OdfCellStyleProxy? _styleProxy;

    /// <summary>
    /// 取得此儲存格的高階樣式設定代理 Facade。
    /// </summary>
    public OdfKit.Styles.OdfCellStyleProxy Style => _styleProxy ??= new OdfKit.Styles.OdfCellStyleProxy(this);

    /// <summary>
    /// 取得或設定儲存格顯示的文字內容（text:p 子節點的純文字）。
    /// </summary>
    public string DisplayText
    {
        get
        {
            foreach (var child in Node.Children)
            {
                if (child.LocalName == "p" && child.NamespaceUri == OdfNamespaces.Text)
                {
                    return child.TextContent;
                }
            }
            return Node.TextContent;
        }
        set
        {
            SetCellTextContent(value);
        }
    }

    /// <summary>
    /// 以指定型別 <typeparamref name="T"/> 取得儲存格值；轉換失敗時回傳預設值。
    /// </summary>
    public T? GetValue<T>()
    {
        object? val = CellValue;
        if (val is null)
            return default;
        if (val is T typed)
            return typed;
        try
        { return (T)Convert.ChangeType(val, typeof(T), CultureInfo.InvariantCulture); }
        catch { return default; }
    }

    /// <summary>
    /// 取得或設定儲存格的公式。
    /// </summary>
    public string Formula
    {
        get => Node.GetAttribute("formula", OdfNamespaces.Table) ?? string.Empty;
        set => Node.SetAttribute("formula", OdfNamespaces.Table, value, "table");
    }

    /// <summary>
    /// 設定儲存格的數值。
    /// </summary>
    /// <param name="val">數值</param>
    public void SetValue(double val)
    {
        ValueType = "float";
        RawValue = val.ToString(System.Globalization.CultureInfo.InvariantCulture);
        DisplayText = val.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 設定儲存格的布林值。
    /// </summary>
    /// <param name="val">布林值</param>
    public void SetValue(bool val)
    {
        ValueType = "boolean";
        Node.SetAttribute("boolean-value", OdfNamespaces.Office, val ? "true" : "false", "office");
        DisplayText = val ? "TRUE" : "FALSE";
    }

    /// <summary>
    /// 設定儲存格的日期時間值。
    /// </summary>
    /// <param name="date">日期時間</param>
    /// <param name="useTimezoneNaive">是否忽略時區轉換，使用本地時間格式</param>
    public void SetValue(DateTime date, bool useTimezoneNaive = false)
    {
        ValueType = "date";
        string isoDate;
        if (date == DateTime.MinValue || date == DateTime.MaxValue)
        {
            isoDate = useTimezoneNaive
                ? date.ToString("yyyy-MM-ddTHH:mm:ss")
                : date.ToString("yyyy-MM-ddTHH:mm:ss") + "Z";
        }
        else
        {
            isoDate = useTimezoneNaive
                ? date.ToString("yyyy-MM-ddTHH:mm:ss")
                : date.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
        }
        Node.SetAttribute("date-value", OdfNamespaces.Office, isoDate, "office");
        DisplayText = isoDate;
    }

    /// <summary>
    /// 設定儲存格的文字內容。
    /// </summary>
    /// <param name="text">文字字串</param>
    public void SetValue(string text)
    {
        ValueType = "string";
        DisplayText = text;
    }

    private void ClearValue()
    {
        Node.RemoveAttribute("value-type", OdfNamespaces.Office);
        Node.RemoveAttribute("value", OdfNamespaces.Office);
        Node.RemoveAttribute("boolean-value", OdfNamespaces.Office);
        Node.RemoveAttribute("date-value", OdfNamespaces.Office);
        DisplayText = string.Empty;
    }

    private void SetCellTextContent(string text)
    {
        var toRemove = new List<OdfNode>();
        foreach (var child in Node.Children)
        {
            if (child.NamespaceUri == OdfNamespaces.Text)
                toRemove.Add(child);
        }
        foreach (var child in toRemove)
        {
            Node.RemoveChild(child);
        }

        var pNode = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
        bool needsWrap = false;

        int i = 0;
        while (i < text.Length)
        {
            if (text[i] == '\n')
            {
                pNode.AppendChild(new OdfNode(OdfNodeType.Element, "line-break", OdfNamespaces.Text, "text"));
                needsWrap = true;
                i++;
            }
            else if (text[i] == '\t')
            {
                pNode.AppendChild(new OdfNode(OdfNodeType.Element, "tab", OdfNamespaces.Text, "text"));
                i++;
            }
            else if (text[i] == ' ')
            {
                int spaceCount = 0;
                while (i < text.Length && text[i] == ' ')
                {
                    spaceCount++;
                    i++;
                }

                if (spaceCount == 1)
                {
                    pNode.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = " " });
                }
                else
                {
                    pNode.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = " " });
                    var sNode = new OdfNode(OdfNodeType.Element, "s", OdfNamespaces.Text, "text");
                    sNode.SetAttribute("c", OdfNamespaces.Text, (spaceCount - 1).ToString(), "text");
                    pNode.AppendChild(sNode);
                }
            }
            else
            {
                int start = i;
                while (i < text.Length && text[i] != '\n' && text[i] != '\t' && text[i] != ' ')
                {
                    i++;
                }
                string segment = text.Substring(start, i - start);
                pNode.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = segment });
            }
        }

        Node.AppendChild(pNode);

        if (needsWrap)
        {
            SetStyleProperty("table-cell-properties", "wrap-option", OdfNamespaces.Fo, "wrap", "fo");
        }
    }

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
            }
        }
        return richText;
    }

    /// <summary>
    /// 設定儲存格的富文字內容，取代現有文字。
    /// </summary>
    public void SetRichText(OdfRichText richText)
    {
        var toRemove = new List<OdfNode>();
        foreach (var child in Node.Children)
            if (child.LocalName == "p" && child.NamespaceUri == OdfNamespaces.Text)
                toRemove.Add(child);
        foreach (var child in toRemove)
            Node.RemoveChild(child);

        var pNode = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
        foreach (var run in richText.Runs)
        {
            bool hasFormatting = run.Bold || run.Italic || run.Underline || run.Color.HasValue || !string.IsNullOrEmpty(run.FontFamily);
            if (hasFormatting)
            {
                string styleName = _doc.GetOrCreateCharacterStyle(run.Bold, run.Italic, run.Underline, run.Color, run.FontFamily);
                var span = new OdfNode(OdfNodeType.Element, "span", OdfNamespaces.Text, "text");
                span.SetAttribute("style-name", OdfNamespaces.Text, styleName, "text");
                span.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = run.Text });
                pNode.AppendChild(span);
            }
            else
            {
                pNode.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = run.Text });
            }
        }
        Node.AppendChild(pNode);
        ValueType = "string";
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
                    if (DateTime.TryParse(inner.TextContent, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime dt))
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

    /// <summary>
    /// 設定此儲存格的四面框線樣式。
    /// </summary>
    /// <param name="top">上框線</param>
    /// <param name="bottom">下框線</param>
    /// <param name="left">左框線</param>
    /// <param name="right">右框線</param>
    public void SetBorders(OdfBorder? top, OdfBorder? bottom, OdfBorder? left, OdfBorder? right)
    {
        if (top.HasValue)
            SetStyleProperty("table-cell-properties", "border-top", OdfNamespaces.Fo, top.Value.ToString(), "fo");
        if (bottom.HasValue)
            SetStyleProperty("table-cell-properties", "border-bottom", OdfNamespaces.Fo, bottom.Value.ToString(), "fo");
        if (left.HasValue)
            SetStyleProperty("table-cell-properties", "border-left", OdfNamespaces.Fo, left.Value.ToString(), "fo");
        if (right.HasValue)
            SetStyleProperty("table-cell-properties", "border-right", OdfNamespaces.Fo, right.Value.ToString(), "fo");
    }

    /// <summary>
    /// 新增條件格式對應規則。
    /// </summary>
    /// <param name="condition">條件值（例如 "cell-content()=1"）</param>
    /// <param name="applyStyleName">要套用的格式樣式名稱</param>
    /// <param name="baseCell">基準儲存格位址</param>
    public void AddConditionalFormatMap(string condition, string applyStyleName, OdfCellAddress? baseCell = null)
    {
        var styleNode = _doc.StyleEngine.GetOrCreateLocalStyle(Node, "table-cell");
        var mapNode = new OdfNode(OdfNodeType.Element, "map", OdfNamespaces.Style, "style");
        mapNode.SetAttribute("condition", OdfNamespaces.Style, condition, "style");
        mapNode.SetAttribute("apply-style-name", OdfNamespaces.Style, applyStyleName, "style");
        if (baseCell.HasValue)
        {
            mapNode.SetAttribute("base-cell-address", OdfNamespaces.Style, baseCell.Value.ToOdfString(false), "style");
        }
        styleNode.AppendChild(mapNode);
    }

    /// <summary>
    /// 取得依套用數字格式樣式格式化後的顯示值；若無樣式定義則回傳 <see cref="DisplayText"/>。
    /// </summary>
    public string FormattedValue
    {
        get
        {
            string? cellStyleName = StyleName;
            if (string.IsNullOrEmpty(cellStyleName))
                return DisplayText;

            string? dataStyleName = FindDataStyleName(cellStyleName!);
            if (string.IsNullOrEmpty(dataStyleName))
                return DisplayText;

            OdfNode? formatNode = OdfKit.Styles.OdfNumberFormatEngine.FindFormatNode(_doc.ContentDom, dataStyleName!)
                ?? OdfKit.Styles.OdfNumberFormatEngine.FindFormatNode(_doc.StylesDom, dataStyleName!);
            if (formatNode is null)
                return DisplayText;

            return ValueType switch
            {
                "float" when double.TryParse(RawValue, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double dbl)
                    => OdfKit.Styles.OdfNumberFormatEngine.Format(dbl, formatNode),
                "date" when DateTime.TryParse(
                    Node.GetAttribute("date-value", OdfNamespaces.Office),
                    null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime dt)
                    => OdfKit.Styles.OdfNumberFormatEngine.Format(dt, formatNode),
                "boolean" when bool.TryParse(
                    Node.GetAttribute("boolean-value", OdfNamespaces.Office), out bool flag)
                    => flag ? "TRUE" : "FALSE",
                _ => DisplayText
            };
        }
    }

    private string? FindDataStyleName(string cellStyle)
    {
        return SearchForDataStyle(_doc.ContentDom, cellStyle)
            ?? SearchForDataStyle(_doc.StylesDom, cellStyle);
    }

    private static string? SearchForDataStyle(OdfNode root, string cellStyle)
    {
        foreach (var child in root.Children)
        {
            if (child.NamespaceUri == OdfNamespaces.Office
                && (child.LocalName == "automatic-styles" || child.LocalName == "styles"))
            {
                foreach (var style in child.Children)
                {
                    if (style.NamespaceUri == OdfNamespaces.Style
                        && style.LocalName == "style"
                        && style.GetAttribute("name", OdfNamespaces.Style) == cellStyle)
                    {
                        return style.GetAttribute("data-style-name", OdfNamespaces.Style);
                    }
                }
            }
        }
        return null;
    }

    private void SetStyleProperty(string propertiesElement, string propertyAttr, string propertyNs, string value, string propertyPrefix)
    {
        _doc.StyleEngine.SetLocalStyleProperty(Node, "table-cell", propertiesElement, propertyAttr, propertyNs, value, propertyPrefix);
    }
}
