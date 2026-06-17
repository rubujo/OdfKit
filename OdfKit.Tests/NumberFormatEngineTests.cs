using System;
using OdfKit.DOM;
using OdfKit.Core;
using OdfKit.Styles;
using OdfKit.Spreadsheet;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定 N-1 ODF 數字格式引擎的單元與整合測試。
/// </summary>
public class NumberFormatEngineTests
{
    // ── 輔助：以程式碼建立格式定義節點 ──────────────────────────

    private static OdfNode NumberStyleNode(
        int decimalPlaces = 2, int minInt = 1, bool grouping = false)
    {
        var root = new OdfNode(OdfNodeType.Element, "number-style", OdfNamespaces.Number, "number");
        root.SetAttribute("name", OdfNamespaces.Style, "N1", "style");

        var num = new OdfNode(OdfNodeType.Element, "number", OdfNamespaces.Number, "number");
        num.SetAttribute("decimal-places", OdfNamespaces.Number, decimalPlaces.ToString(), "number");
        num.SetAttribute("min-integer-digits", OdfNamespaces.Number, minInt.ToString(), "number");
        num.SetAttribute("grouping", OdfNamespaces.Number, grouping ? "true" : "false", "number");
        root.AppendChild(num);
        return root;
    }

    private static OdfNode PercentStyleNode(int decimalPlaces = 2)
    {
        var root = new OdfNode(OdfNodeType.Element, "percentage-style", OdfNamespaces.Number, "number");
        root.SetAttribute("name", OdfNamespaces.Style, "N2", "style");

        var num = new OdfNode(OdfNodeType.Element, "number", OdfNamespaces.Number, "number");
        num.SetAttribute("decimal-places", OdfNamespaces.Number, decimalPlaces.ToString(), "number");
        num.SetAttribute("min-integer-digits", OdfNamespaces.Number, "1", "number");
        root.AppendChild(num);

        var text = new OdfNode(OdfNodeType.Element, "text", OdfNamespaces.Number, "number");
        text.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = "%" });
        root.AppendChild(text);
        return root;
    }

    private static OdfNode BoolStyleNode()
    {
        var root = new OdfNode(OdfNodeType.Element, "boolean-style", OdfNamespaces.Number, "number");
        root.SetAttribute("name", OdfNamespaces.Style, "N3", "style");
        root.AppendChild(new OdfNode(OdfNodeType.Element, "boolean", OdfNamespaces.Number, "number"));
        return root;
    }

    private static OdfNode CurrencyStyleNode()
    {
        var root = new OdfNode(OdfNodeType.Element, "currency-style", OdfNamespaces.Number, "number");
        root.SetAttribute("name", OdfNamespaces.Style, "N4", "style");

        var sym = new OdfNode(OdfNodeType.Element, "currency-symbol", OdfNamespaces.Number, "number");
        sym.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = "$" });
        root.AppendChild(sym);

        var num = new OdfNode(OdfNodeType.Element, "number", OdfNamespaces.Number, "number");
        num.SetAttribute("decimal-places", OdfNamespaces.Number, "2", "number");
        num.SetAttribute("min-integer-digits", OdfNamespaces.Number, "1", "number");
        num.SetAttribute("grouping", OdfNamespaces.Number, "true", "number");
        root.AppendChild(num);
        return root;
    }

    private static OdfNode DateStyleNode()
    {
        var root = new OdfNode(OdfNodeType.Element, "date-style", OdfNamespaces.Number, "number");
        root.SetAttribute("name", OdfNamespaces.Style, "N5", "style");

        var year = new OdfNode(OdfNodeType.Element, "year", OdfNamespaces.Number, "number");
        year.SetAttribute("style", OdfNamespaces.Number, "long", "number");
        root.AppendChild(year);

        AddText(root, "-");

        var month = new OdfNode(OdfNodeType.Element, "month", OdfNamespaces.Number, "number");
        month.SetAttribute("style", OdfNamespaces.Number, "long", "number");
        root.AppendChild(month);

        AddText(root, "-");

        var day = new OdfNode(OdfNodeType.Element, "day", OdfNamespaces.Number, "number");
        day.SetAttribute("style", OdfNamespaces.Number, "long", "number");
        root.AppendChild(day);

        return root;
    }

    private static OdfNode TimeStyleNode()
    {
        var root = new OdfNode(OdfNodeType.Element, "time-style", OdfNamespaces.Number, "number");
        root.SetAttribute("name", OdfNamespaces.Style, "N6", "style");

        var h = new OdfNode(OdfNodeType.Element, "hours", OdfNamespaces.Number, "number");
        h.SetAttribute("style", OdfNamespaces.Number, "long", "number");
        root.AppendChild(h);

        AddText(root, ":");

        var m = new OdfNode(OdfNodeType.Element, "minutes", OdfNamespaces.Number, "number");
        m.SetAttribute("style", OdfNamespaces.Number, "long", "number");
        root.AppendChild(m);

        AddText(root, ":");

        var s = new OdfNode(OdfNodeType.Element, "seconds", OdfNamespaces.Number, "number");
        s.SetAttribute("style", OdfNamespaces.Number, "long", "number");
        root.AppendChild(s);

        return root;
    }

    private static void AddText(OdfNode parent, string content)
    {
        var t = new OdfNode(OdfNodeType.Element, "text", OdfNamespaces.Number, "number");
        t.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = content });
        parent.AppendChild(t);
    }

    // ── number:number-style ────────────────────────────────────

    /// <summary>
    /// 驗證基本整數格式化（無小數點）。
    /// </summary>
    [Fact]
    public void Format_NumberStyle_NoDecimals_ReturnsInteger()
    {
        var node = NumberStyleNode(decimalPlaces: 0, minInt: 1);
        string result = OdfNumberFormatEngine.Format(1234.0, node);
        Assert.Equal("1234", result);
    }

    /// <summary>
    /// 驗證兩位小數格式化。
    /// </summary>
    [Fact]
    public void Format_NumberStyle_TwoDecimals_ReturnsFormatted()
    {
        var node = NumberStyleNode(decimalPlaces: 2, minInt: 1);
        string result = OdfNumberFormatEngine.Format(1234.5, node);
        Assert.Equal("1234.50", result);
    }

    /// <summary>
    /// 驗證千分位分隔符號。
    /// </summary>
    [Fact]
    public void Format_NumberStyle_Grouping_ReturnsThousandsSeparated()
    {
        var node = NumberStyleNode(decimalPlaces: 2, minInt: 1, grouping: true);
        string result = OdfNumberFormatEngine.Format(1234567.89, node);
        Assert.Equal("1,234,567.89", result);
    }

    /// <summary>
    /// 驗證最小整數位數補零（minInt=4）。
    /// </summary>
    [Fact]
    public void Format_NumberStyle_MinIntDigits_PadsWithZeros()
    {
        var node = NumberStyleNode(decimalPlaces: 0, minInt: 4);
        string result = OdfNumberFormatEngine.Format(42.0, node);
        Assert.Equal("0042", result);
    }

    // ── number:percentage-style ───────────────────────────────

    /// <summary>
    /// 驗證百分比格式化（值乘以 100 後加 %）。
    /// </summary>
    [Fact]
    public void Format_PercentageStyle_MultipliesBy100AndAppendsPercent()
    {
        var node = PercentStyleNode(2);
        string result = OdfNumberFormatEngine.Format(0.1234, node);
        Assert.Equal("12.34%", result);
    }

    /// <summary>
    /// 驗證百分比零小數格式。
    /// </summary>
    [Fact]
    public void Format_PercentageStyle_ZeroDecimals_ReturnsWholePercent()
    {
        var node = PercentStyleNode(0);
        string result = OdfNumberFormatEngine.Format(0.5, node);
        Assert.Equal("50%", result);
    }

    // ── number:boolean-style ──────────────────────────────────

    /// <summary>
    /// 驗證布林格式化 — 非零回傳 TRUE。
    /// </summary>
    [Fact]
    public void Format_BooleanStyle_NonZero_ReturnsTrue()
    {
        var node = BoolStyleNode();
        Assert.Equal("TRUE", OdfNumberFormatEngine.Format(1.0, node));
    }

    /// <summary>
    /// 驗證布林格式化 — 零回傳 FALSE。
    /// </summary>
    [Fact]
    public void Format_BooleanStyle_Zero_ReturnsFalse()
    {
        var node = BoolStyleNode();
        Assert.Equal("FALSE", OdfNumberFormatEngine.Format(0.0, node));
    }

    // ── number:currency-style ─────────────────────────────────

    /// <summary>
    /// 驗證貨幣格式含貨幣符號與千分位。
    /// </summary>
    [Fact]
    public void Format_CurrencyStyle_IncludesCurrencySymbol()
    {
        var node = CurrencyStyleNode();
        string result = OdfNumberFormatEngine.Format(1234.5, node);
        Assert.Contains("$", result);
        Assert.Contains("1,234.50", result);
    }

    // ── number:date-style ─────────────────────────────────────

    /// <summary>
    /// 驗證日期格式化 yyyy-MM-dd。
    /// </summary>
    [Fact]
    public void Format_DateStyle_ReturnsFormattedDate()
    {
        var node = DateStyleNode();
        var dt = new DateTime(2026, 6, 16);
        string result = OdfNumberFormatEngine.Format(dt, node);
        Assert.Equal("2026-06-16", result);
    }

    /// <summary>
    /// 驗證日期格式化 — 月份補零。
    /// </summary>
    [Fact]
    public void Format_DateStyle_PadsMonthWithZero()
    {
        var node = DateStyleNode();
        var dt = new DateTime(2026, 3, 5);
        string result = OdfNumberFormatEngine.Format(dt, node);
        Assert.Equal("2026-03-05", result);
    }

    // ── number:time-style ─────────────────────────────────────

    /// <summary>
    /// 驗證時間格式化 HH:mm:ss。
    /// </summary>
    [Fact]
    public void Format_TimeStyle_ReturnsFormattedTime()
    {
        var node = TimeStyleNode();
        var dt = new DateTime(2026, 1, 1, 9, 5, 3);
        string result = OdfNumberFormatEngine.Format(dt, node);
        Assert.Equal("09:05:03", result);
    }

    // ── FindFormatNode ────────────────────────────────────────

    /// <summary>
    /// 驗證 FindFormatNode 能從巢狀 DOM 找到指定名稱的格式節點。
    /// </summary>
    [Fact]
    public void FindFormatNode_FoundByName()
    {
        var dom = new OdfNode(OdfNodeType.Element, "document-content", OdfNamespaces.Office, "office");
        var autoStyles = new OdfNode(OdfNodeType.Element, "automatic-styles", OdfNamespaces.Office, "office");
        dom.AppendChild(autoStyles);

        var numStyle = new OdfNode(OdfNodeType.Element, "number-style", OdfNamespaces.Number, "number");
        numStyle.SetAttribute("name", OdfNamespaces.Style, "N10", "style");
        var numPart = new OdfNode(OdfNodeType.Element, "number", OdfNamespaces.Number, "number");
        numPart.SetAttribute("decimal-places", OdfNamespaces.Number, "0", "number");
        numStyle.AppendChild(numPart);
        autoStyles.AppendChild(numStyle);

        var found = OdfNumberFormatEngine.FindFormatNode(dom, "N10");
        Assert.NotNull(found);
        Assert.Equal("number-style", found!.LocalName);
    }

    /// <summary>
    /// 驗證 FindFormatNode 對不存在的名稱回傳 null。
    /// </summary>
    [Fact]
    public void FindFormatNode_NotFound_ReturnsNull()
    {
        var dom = new OdfNode(OdfNodeType.Element, "root", string.Empty, string.Empty);
        var found = OdfNumberFormatEngine.FindFormatNode(dom, "N99");
        Assert.Null(found);
    }

    // ── Format(object, styleName, stylesDom) ─────────────────

    /// <summary>
    /// 驗證三參數多載能依樣式名稱格式化。
    /// </summary>
    [Fact]
    public void Format_ByStyleName_FormatsCorrectly()
    {
        var dom = new OdfNode(OdfNodeType.Element, "document-content", OdfNamespaces.Office, "office");
        var autoStyles = new OdfNode(OdfNodeType.Element, "automatic-styles", OdfNamespaces.Office, "office");
        dom.AppendChild(autoStyles);

        var numStyle = new OdfNode(OdfNodeType.Element, "number-style", OdfNamespaces.Number, "number");
        numStyle.SetAttribute("name", OdfNamespaces.Style, "PctLike", "style");
        var numPart = new OdfNode(OdfNodeType.Element, "number", OdfNamespaces.Number, "number");
        numPart.SetAttribute("decimal-places", OdfNamespaces.Number, "1", "number");
        numPart.SetAttribute("min-integer-digits", OdfNamespaces.Number, "1", "number");
        numStyle.AppendChild(numPart);
        AddText(numStyle, "%");
        autoStyles.AppendChild(numStyle);

        string result = OdfNumberFormatEngine.Format((object)42.5, "PctLike", dom);
        Assert.Contains("42.5", result);
        Assert.Contains("%", result);
    }

    /// <summary>
    /// 驗證三參數多載對 null 值回傳空字串。
    /// </summary>
    [Fact]
    public void Format_ByStyleName_NullValue_ReturnsEmpty()
    {
        var dom = new OdfNode(OdfNodeType.Element, "root", string.Empty, string.Empty);
        string result = OdfNumberFormatEngine.Format(null, "N1", dom);
        Assert.Equal(string.Empty, result);
    }

    // ── OdfCell.FormattedValue 整合測試 ───────────────────────

    /// <summary>
    /// 驗證無樣式的儲存格 FormattedValue 回傳 DisplayText。
    /// </summary>
    [Fact]
    public void OdfCell_FormattedValue_NoStyle_ReturnsDisplayText()
    {
        using var doc = SpreadsheetDocument.Create();
        var sheet = doc.Worksheets.Add("Sheet1");
        var cell = sheet.Cells["A1"];
        cell.SetValue("Hello");
        Assert.Equal("Hello", cell.FormattedValue);
    }

    /// <summary>
    /// 驗證整合場景：儲存格帶數字格式樣式，FormattedValue 依格式輸出。
    /// </summary>
    [Fact]
    public void OdfCell_FormattedValue_WithNumberStyle_ReturnsFormatted()
    {
        using var doc = SpreadsheetDocument.Create();

        // 向 ContentDom 的 automatic-styles 注入 number:number-style
        var autoStyles = FindOrCreateAutoStyles(doc.ContentDom);

        var numStyle = new OdfNode(OdfNodeType.Element, "number-style", OdfNamespaces.Number, "number");
        numStyle.SetAttribute("name", OdfNamespaces.Style, "N_TEST", "style");
        var numPart = new OdfNode(OdfNodeType.Element, "number", OdfNamespaces.Number, "number");
        numPart.SetAttribute("decimal-places", OdfNamespaces.Number, "2", "number");
        numPart.SetAttribute("min-integer-digits", OdfNamespaces.Number, "1", "number");
        numStyle.AppendChild(numPart);
        autoStyles.AppendChild(numStyle);

        // 注入對應的 cell style，關聯 data-style-name
        var cellStyle = new OdfNode(OdfNodeType.Element, "style", OdfNamespaces.Style, "style");
        cellStyle.SetAttribute("name", OdfNamespaces.Style, "ce_test", "style");
        cellStyle.SetAttribute("family", OdfNamespaces.Style, "table-cell", "style");
        cellStyle.SetAttribute("data-style-name", OdfNamespaces.Style, "N_TEST", "style");
        autoStyles.AppendChild(cellStyle);

        var sheet = doc.Worksheets.Add("Sheet1");
        var cell = sheet.Cells["A1"];
        cell.SetValue(3.14159);
        cell.StyleName = "ce_test";

        Assert.Equal("3.14", cell.FormattedValue);
    }

    private static OdfNode FindOrCreateAutoStyles(OdfNode contentDom)
    {
        foreach (var child in contentDom.Children)
        {
            if (child.LocalName == "automatic-styles" && child.NamespaceUri == OdfNamespaces.Office)
                return child;
        }
        var node = new OdfNode(OdfNodeType.Element, "automatic-styles", OdfNamespaces.Office, "office");
        contentDom.AppendChild(node);
        return node;
    }
}
