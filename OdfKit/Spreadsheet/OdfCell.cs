using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Represents a cell in an ODF spreadsheet.
/// 表示 ODF 工作表中的一個儲存格。
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="OdfCell"/> class.
/// 初始化 <see cref="OdfCell"/> 類別的新執行個體。
/// </remarks>
/// <param name="node">The cell XML node. / 儲存格 XML 節點。</param>
/// <param name="row">The zero-based row index. / 採 0 為基準的列索引。</param>
/// <param name="col">The zero-based column index. / 採 0 為基準的欄索引。</param>
/// <param name="doc">The spreadsheet document. / 試算表文件。</param>
/// <param name="sheetName">The containing sheet name. / 所在工作表名稱。</param>
public partial class OdfCell(OdfNode node, int row, int col, SpreadsheetDocument doc, string sheetName = "")
{
    /// <summary>
    /// 取得代表儲存格的 XML 節點。
    /// </summary>
    internal OdfNode Node { get; } = node;

    /// <summary>
    /// Gets the zero-based row index.
    /// 取得以 0 為基準的列索引。
    /// </summary>
    public int Row { get; } = row;

    /// <summary>
    /// Gets the zero-based column index.
    /// 取得以 0 為基準的欄索引。
    /// </summary>
    public int Column { get; } = col;

    private readonly SpreadsheetDocument _doc = doc;
    private readonly string _sheetName = sheetName ?? string.Empty;

    internal SpreadsheetDocument Document => _doc;

    /// <summary>
    /// Gets or sets the type of the cell data value.
    /// 取得或設定儲存格資料值的型態。
    /// </summary>
    public string ValueType
    {
        get => Node.GetAttribute("value-type", OdfNamespaces.Office) ?? string.Empty;
        set => Node.SetAttribute("value-type", OdfNamespaces.Office, value, "office");
    }

    /// <summary>
    /// Gets or sets the raw numeric cell value as the <c>office:value</c> attribute string.
    /// 取得或設定儲存格的原始數值（office:value 屬性，字串格式）。
    /// </summary>
    public string RawValue
    {
        get => Node.GetAttribute("value", OdfNamespaces.Office) ?? string.Empty;
        set => Node.SetAttribute("value", OdfNamespaces.Office, value, "office");
    }

    /// <summary>
    /// Gets or sets the commonly typed cell value.
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
            OdfNode? previousSnapshot = CaptureTrackingSnapshot();

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

            PublishTrackingSnapshot(previousSnapshot);
            _doc.NotifyFormulaRecalculationRequested();
        }
    }

    /// <summary>
    /// Gets or sets the table style name applied to the cell.
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
    /// Gets the high-level style configuration facade for this cell.
    /// 取得此儲存格的高階樣式設定代理 Facade。
    /// </summary>
    public OdfKit.Styles.OdfCellStyleProxy Style => _styleProxy ??= new OdfKit.Styles.OdfCellStyleProxy(this);

    /// <summary>
    /// Gets the fluent rich text builder for this cell.
    /// 取得此儲存格的富文字鏈式建構器。
    /// </summary>
    public OdfCellRichTextBuilder RichText => new(this);

    /// <summary>
    /// Gets or sets the displayed text content of the cell as plain text from <c>text:p</c> child nodes.
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
    /// Gets the cell value as the specified type <typeparamref name="T"/>, returning the default value when conversion fails.
    /// 以指定型別 <typeparamref name="T"/> 取得儲存格值；轉換失敗時回傳預設值。
    /// </summary>
    /// <typeparam name="T">The target value type. / 目標值型別。</typeparam>
    /// <returns>The converted cell value, or the default value when conversion fails. / 轉換後的儲存格值；轉換失敗時為預設值。</returns>
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
    /// Gets or sets the cell formula.
    /// 取得或設定儲存格的公式。
    /// </summary>
    public string Formula
    {
        get => Node.GetAttribute("formula", OdfNamespaces.Table) ?? string.Empty;
        set
        {
            string normalized = value ?? string.Empty;
            if (string.Equals(Formula, normalized, StringComparison.Ordinal))
                return;

            OdfNode? previousSnapshot = CaptureTrackingSnapshot();
            if (string.IsNullOrEmpty(normalized))
                Node.RemoveAttribute("formula", OdfNamespaces.Table);
            else
                Node.SetAttribute("formula", OdfNamespaces.Table, normalized, "table");

            PublishTrackingSnapshot(previousSnapshot);
            _doc.NotifyFormulaRecalculationRequested();
        }
    }

    /// <summary>
    /// Sets the numeric value of the cell.
    /// 設定儲存格的數值。
    /// </summary>
    /// <param name="val">The numeric value. / 數值。</param>
    public void SetValue(double val)
    {
        ValueType = "float";
        RawValue = val.ToString(CultureInfo.InvariantCulture);
        DisplayText = val.ToString(CultureInfo.InvariantCulture);
        _doc.NotifyFormulaRecalculationRequested();
    }

    /// <summary>
    /// Sets the Boolean value of the cell.
    /// 設定儲存格的布林值。
    /// </summary>
    /// <param name="val">The Boolean value. / 布林值。</param>
    public void SetValue(bool val)
    {
        ValueType = "boolean";
        Node.SetAttribute("boolean-value", OdfNamespaces.Office, val ? "true" : "false", "office");
        DisplayText = val ? "TRUE" : "FALSE";
        _doc.NotifyFormulaRecalculationRequested();
    }

    /// <summary>
    /// Sets the date and time value of the cell.
    /// 設定儲存格的日期時間值。
    /// </summary>
    /// <param name="date">The date and time value. / 日期時間。</param>
    /// <param name="useTimezoneNaive">Whether to ignore time zone conversion and use local time formatting. / 是否忽略時區轉換，使用本地時間格式。</param>
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
        _doc.NotifyFormulaRecalculationRequested();
    }

    /// <summary>
    /// Sets the text content of the cell.
    /// 設定儲存格的文字內容。
    /// </summary>
    /// <param name="text">The text string. / 文字字串。</param>
    public void SetValue(string text)
    {
        ValueType = "string";
        DisplayText = text;
        _doc.NotifyFormulaRecalculationRequested();
    }

    private OdfNode? CaptureTrackingSnapshot() =>
        _doc.TrackedChanges && !string.IsNullOrEmpty(_sheetName)
            ? Node.CloneNode(deep: true)
            : null;

    private void PublishTrackingSnapshot(OdfNode? previousSnapshot)
    {
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

    private void ClearValue()
    {
        Node.RemoveAttribute("value-type", OdfNamespaces.Office);
        Node.RemoveAttribute("value", OdfNamespaces.Office);
        Node.RemoveAttribute("boolean-value", OdfNamespaces.Office);
        Node.RemoveAttribute("date-value", OdfNamespaces.Office);
        DisplayText = string.Empty;
        _doc.NotifyFormulaRecalculationRequested();
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

        AppendTextContent(pNode, text, ref needsWrap);

        Node.AppendChild(pNode);

        if (needsWrap)
        {
            SetStyleProperty("table-cell-properties", "wrap-option", OdfNamespaces.Fo, "wrap", "fo");
        }
    }

    private static void AppendTextContent(OdfNode parentNode, string text, ref bool needsWrap)
    {
        int i = 0;
        while (i < text.Length)
        {
            if (text[i] == '\n')
            {
                parentNode.AppendChild(new OdfNode(OdfNodeType.Element, "line-break", OdfNamespaces.Text, "text"));
                needsWrap = true;
                i++;
            }
            else if (text[i] == '\t')
            {
                parentNode.AppendChild(new OdfNode(OdfNodeType.Element, "tab", OdfNamespaces.Text, "text"));
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
                    parentNode.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = " " });
                }
                else
                {
                    parentNode.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = " " });
                    var sNode = new OdfNode(OdfNodeType.Element, "s", OdfNamespaces.Text, "text");
                    sNode.SetAttribute("c", OdfNamespaces.Text, (spaceCount - 1).ToString(CultureInfo.InvariantCulture), "text");
                    parentNode.AppendChild(sNode);
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
                parentNode.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = segment });
            }
        }
    }

}
