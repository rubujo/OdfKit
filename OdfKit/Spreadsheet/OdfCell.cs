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
    /// Gets or sets the normalized ISO 4217 currency code stored in <c>office:currency</c>.
    /// 取得或設定儲存在 <c>office:currency</c> 的正規化 ISO 4217 貨幣代碼。
    /// </summary>
    /// <remarks>
    /// Writing validates the ISO 4217 <i>shape</i> only — exactly three ASCII letters — and normalizes to
    /// upper case. No list of currently assigned codes is enforced, because such a list would reject
    /// historical codes, the <c>XTS</c> test code, and codes assigned after this release. Reading is
    /// deliberately lenient: values already present in a loaded document are normalized but never rejected,
    /// so documents written by other producers stay readable.
    /// 寫入時只驗證 ISO 4217 的<b>形狀</b>——恰好三個 ASCII 字母——並正規化為大寫；不比對現行代碼清單，
    /// 因為那會拒絕歷史代碼、<c>XTS</c> 測試代碼以及本版發佈後才配發的代碼。讀取刻意寬容：已存在於文件中的
    /// 值只做正規化而不拒絕，確保其他產生器寫出的文件仍可讀取。
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when the value is not three ASCII letters. / 當值不是三個 ASCII 字母時擲出。</exception>
    public string? CurrencyCode
    {
        get
        {
            string? currency = Node.GetAttribute("currency", OdfNamespaces.Office);
            if (string.IsNullOrWhiteSpace(currency))
            {
                return null;
            }

            return currency!.Trim().ToUpperInvariant();
        }
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                Node.RemoveAttribute("currency", OdfNamespaces.Office);
            }
            else
            {
                Node.SetAttribute("currency", OdfNamespaces.Office, NormalizeCurrencyCode(value!, nameof(value)), "office");
            }
        }
    }

    /// <summary>
    /// 驗證 ISO 4217 形狀並正規化為大寫；不比對現行代碼清單。
    /// </summary>
    private static string NormalizeCurrencyCode(string currencyCode, string parameterName)
    {
        string trimmed = currencyCode.Trim();
        if (trimmed.Length != 3)
        {
            throw new ArgumentException(null, parameterName);
        }

        foreach (char c in trimmed)
        {
            // 不使用 char.IsAsciiLetter：該多載自 .NET 7 起才提供，netstandard2.0 目標無法編譯。
            bool isAsciiLetter = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
            if (!isAsciiLetter)
            {
                throw new ArgumentException(null, parameterName);
            }
        }

        return trimmed.ToUpperInvariant();
    }

    /// <summary>
    /// Gets or sets the commonly typed cell value.
    /// 取得或設定儲存格的常用型別值。
    /// </summary>
    /// <remarks>
    /// Date cells return <see cref="DateTime"/> when <c>office:date-value</c> can be parsed with
    /// round-trip semantics; otherwise this accessor preserves the raw date string.
    /// 日期儲存格若可將 <c>office:date-value</c> 依 round-trip 語意解析，會回傳
    /// <see cref="DateTime"/>；否則保留原始日期字串。
    /// </remarks>
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
                "date" => DateTime.TryParse(
                    Node.GetAttribute("date-value", OdfNamespaces.Office),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out DateTime date)
                    ? date
                    : Node.GetAttribute("date-value", OdfNamespaces.Office),
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
            NotifyFormulaCellChanged(formulaChanged: false);
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
    /// <remarks>
    /// When a date cell can round-trip parse <c>office:date-value</c>, <see cref="GetValue{T}"/> first reuses
    /// the parsed <see cref="DateTime"/> from <see cref="CellValue"/>. Therefore <c>GetValue&lt;DateTime&gt;()</c>
    /// preserves the parsed instant, while <c>GetValue&lt;string&gt;()</c> converts that
    /// <see cref="DateTime"/> with <see cref="CultureInfo.InvariantCulture"/> instead of returning the raw ISO text.
    /// 若日期儲存格的 <c>office:date-value</c> 可依 round-trip 語意解析，<see cref="GetValue{T}"/>
    /// 會先重用 <see cref="CellValue"/> 產生的 <see cref="DateTime"/>；因此 <c>GetValue&lt;DateTime&gt;()</c>
    /// 會保留解析後的時間點，而 <c>GetValue&lt;string&gt;()</c> 會以
    /// <see cref="CultureInfo.InvariantCulture"/> 將該 <see cref="DateTime"/> 轉成字串，而非回傳原始 ISO 文字。
    /// </remarks>
    /// <remarks>
    /// <c>Kind</c> is preserved only for the <c>Z</c> (UTC) and offset-less forms. XSD <c>dateTime</c> also
    /// permits an explicit offset such as <c>+05:30</c>; <see cref="DateTimeStyles.RoundtripKind"/> cannot
    /// represent that in a <see cref="DateTime"/>, so those values are converted to local time with
    /// <see cref="DateTimeKind.Local"/> — the instant is preserved, but the result varies with the machine
    /// time zone and the original offset is lost. Use <see cref="RawValue"/> or read
    /// <c>office:date-value</c> directly when the original lexical form matters.
    /// 只有 <c>Z</c>（UTC）與無時區形式會保留 <c>Kind</c>。XSD <c>dateTime</c> 也允許 <c>+05:30</c> 這類明確
    /// 位移，而 <see cref="DateTimeStyles.RoundtripKind"/> 無法在 <see cref="DateTime"/> 中表示，因此那些值
    /// 會被轉為當地時間並標記為 <see cref="DateTimeKind.Local"/>——時間點不變，但結果隨機器時區而異，
    /// 且原始位移遺失。若需要原始字面形式，請改用 <see cref="RawValue"/> 或直接讀取 <c>office:date-value</c>。
    /// </remarks>
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
            {
                Node.SetAttribute("formula", OdfNamespaces.Table, normalized, "table");
                if (string.IsNullOrEmpty(ValueType))
                {
                    ValueType = "float";
                }
            }

            PublishTrackingSnapshot(previousSnapshot);
            NotifyFormulaCellChanged(formulaChanged: true);
        }
    }

    /// <summary>
    /// Sets this cell's formula and cached display value.
    /// 設定此儲存格的公式與快取顯示值。
    /// </summary>
    /// <param name="formula">The ODF formula text; an empty value clears the formula. / ODF 公式文字；空值會清除公式。</param>
    /// <param name="cachedValue">The cached value stored with the formula. / 隨公式儲存的快取值。</param>
    public void SetFormula(string formula, object? cachedValue)
    {
        Formula = formula;
        if (cachedValue is not null)
        {
            CellValue = cachedValue;
        }
    }

    /// <summary>
    /// Sets the numeric value of the cell.
    /// 設定儲存格的數值。
    /// </summary>
    /// <param name="val">The numeric value. / 數值。</param>
    public void SetValue(double val)
    {
        ClearCachedValueAttributes();
        ValueType = "float";
        RawValue = val.ToString(CultureInfo.InvariantCulture);
        DisplayText = val.ToString(CultureInfo.InvariantCulture);
        NotifyFormulaCellChanged(formulaChanged: false);
    }

    /// <summary>
    /// Sets the currency value of the cell.
    /// 設定儲存格的貨幣值。
    /// </summary>
    /// <param name="amount">The numeric amount stored in <c>office:value</c>. / 儲存在 <c>office:value</c> 的數值金額。</param>
    /// <param name="currencyCode">The ISO 4217 currency code. / ISO 4217 貨幣代碼。</param>
    public void SetCurrencyValue(decimal amount, string currencyCode)
        => SetCurrencyValue(amount, currencyCode, amount.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// Sets the currency value and display text of the cell.
    /// 設定儲存格的貨幣值與顯示文字。
    /// </summary>
    /// <param name="amount">The numeric amount stored in <c>office:value</c>. / 儲存在 <c>office:value</c> 的數值金額。</param>
    /// <param name="currencyCode">The ISO 4217 currency code. / ISO 4217 貨幣代碼。</param>
    /// <param name="displayText">The plain-text display value written into <c>text:p</c>. / 寫入 <c>text:p</c> 的純文字顯示值。</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="currencyCode"/> is not three ASCII letters. / 當 <paramref name="currencyCode"/> 不是三個 ASCII 字母時擲出。</exception>
    public void SetCurrencyValue(decimal amount, string currencyCode, string displayText)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(currencyCode, nameof(currencyCode));
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(displayText, nameof(displayText));

        string normalizedCurrencyCode = NormalizeCurrencyCode(currencyCode, nameof(currencyCode));

        ClearCachedValueAttributes();
        ValueType = "currency";
        RawValue = amount.ToString(CultureInfo.InvariantCulture);
        CurrencyCode = normalizedCurrencyCode;
        DisplayText = displayText;
        NotifyFormulaCellChanged(formulaChanged: false);
    }

    /// <summary>
    /// Sets the Boolean value of the cell.
    /// 設定儲存格的布林值。
    /// </summary>
    /// <param name="val">The Boolean value. / 布林值。</param>
    public void SetValue(bool val)
    {
        ClearCachedValueAttributes();
        ValueType = "boolean";
        Node.SetAttribute("boolean-value", OdfNamespaces.Office, val ? "true" : "false", "office");
        DisplayText = val ? "TRUE" : "FALSE";
        NotifyFormulaCellChanged(formulaChanged: false);
    }
    /// <summary>
    /// Short overload of SetValue that accepts date; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 date；其餘可選參數使用預設值並轉呼叫最長 SetValue 多載。
    /// </summary>
    public void SetValue(DateTime date) => SetValue(date, false);


    /// <summary>
    /// Sets the date and time value of the cell.
    /// 設定儲存格的日期時間值。
    /// </summary>
    /// <param name="date">The date and time value. / 日期時間。</param>
    /// <param name="useTimezoneNaive">Whether to ignore time zone conversion and use local time formatting. / 是否忽略時區轉換，使用當地時間格式。</param>
    public void SetValue(DateTime date, bool useTimezoneNaive)
    {
        ClearCachedValueAttributes();
        ValueType = "date";
        string isoDate;
        if (date == DateTime.MinValue || date == DateTime.MaxValue)
        {
            isoDate = useTimezoneNaive
                ? date.ToString("yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)
                : date.ToString("yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + "Z";
        }
        else
        {
            isoDate = useTimezoneNaive
                ? date.ToString("yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)
                : date.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
        }
        Node.SetAttribute("date-value", OdfNamespaces.Office, isoDate, "office");
        DisplayText = isoDate;
        NotifyFormulaCellChanged(formulaChanged: false);
    }


    /// <summary>
    /// Sets the text content of the cell.
    /// 設定儲存格的文字內容。
    /// </summary>
    /// <param name="text">The text string. / 文字字串。</param>
    public void SetValue(string text)
    {
        ClearCachedValueAttributes();
        ValueType = "string";
        DisplayText = text;
        NotifyFormulaCellChanged(formulaChanged: false);
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
        ClearCachedValueAttributes();
        DisplayText = string.Empty;
        NotifyFormulaCellChanged(formulaChanged: false);
    }

    private void ClearCachedValueAttributes()
    {
        Node.RemoveAttribute("value", OdfNamespaces.Office);
        Node.RemoveAttribute("string-value", OdfNamespaces.Office);
        Node.RemoveAttribute("boolean-value", OdfNamespaces.Office);
        Node.RemoveAttribute("currency", OdfNamespaces.Office);
        Node.RemoveAttribute("date-value", OdfNamespaces.Office);
        Node.RemoveAttribute("time-value", OdfNamespaces.Office);
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

    private void NotifyFormulaCellChanged(bool formulaChanged) =>
        _doc.NotifyFormulaCellChanged(
            new OdfCellAddress(Row, Column, _sheetName),
            Node,
            formulaChanged);

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
