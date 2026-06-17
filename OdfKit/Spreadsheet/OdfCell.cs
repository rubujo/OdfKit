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
/// <param name="sheetName">所在工作表名稱。</param>
public partial class OdfCell(OdfNode node, int row, int col, SpreadsheetDocument doc, string sheetName = "")
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
    private readonly string _sheetName = sheetName ?? string.Empty;

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
            OdfNode? previousSnapshot = _doc.TrackedChanges && !string.IsNullOrEmpty(_sheetName)
                ? Node.CloneNode(deep: true)
                : null;

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

            if (previousSnapshot is not null)
            {
                SpreadsheetDocumentTrackedChangesEngine.RecordCellContentChange(
                    _doc,
                    _sheetName,
                    Row,
                    Column,
                    previousSnapshot);
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

}
