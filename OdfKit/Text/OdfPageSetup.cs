using System;
using System.Globalization;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Text;

/// <summary>
/// 表示文字文件的頁面設定。
/// </summary>
public partial class OdfPageSetup
{
    private readonly TextDocument _doc;
    private readonly string _masterPageName;
    private readonly string _pageLayoutName;

    /// <summary>
    /// 使用預設主頁面（Standard / Mpm1）初始化
    /// </summary>
    public OdfPageSetup(TextDocument doc) : this(doc, "Standard", "Mpm1") { }

    internal OdfPageSetup(TextDocument doc, string masterPageName, string pageLayoutName)
    {
        _doc = doc;
        _masterPageName = masterPageName;
        _pageLayoutName = pageLayoutName;
    }

    private OdfNode ContentDom => _doc.ContentDom;
    private OdfNode StylesDom => _doc.StylesDom;

    internal void EnsureNodes()
    {
        _ = FindOrCreatePageLayoutProperties();
        _ = FindOrCreateMasterPage();
    }

    /// <summary>
    /// 取得或設定頁面寬度（公分）。
    /// </summary>
    public double PageWidth
    {
        get
        {
            string? val = GetPageProp("page-width");
            if (TryParseCentimeterLength(val, out var d))
                return d;
            return 21.0;
        }
        set => SetPageProp("page-width", FormatCentimeterLength(value));
    }

    /// <summary>
    /// 取得或設定頁面高度（公分）。
    /// </summary>
    public double PageHeight
    {
        get
        {
            string? val = GetPageProp("page-height");
            if (TryParseCentimeterLength(val, out var d))
                return d;
            return 29.7;
        }
        set => SetPageProp("page-height", FormatCentimeterLength(value));
    }

    private static bool TryParseCentimeterLength(string? value, out double result)
    {
        result = 0;
        return value is not null &&
            value.EndsWith("cm", StringComparison.Ordinal) &&
            double.TryParse(
                value.Substring(0, value.Length - 2),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out result);
    }

    private static string FormatCentimeterLength(double value) => value.ToString(CultureInfo.InvariantCulture) + "cm";

    /// <summary>
    /// 取得或設定頁面使用方式。
    /// </summary>
    public OdfPageUsage PageUsage
    {
        get
        {
            var props = FindOrCreatePageLayoutProperties();
            return (props.GetAttribute("page-usage", OdfNamespaces.Style) ?? "all") switch
            {
                "left" => OdfPageUsage.Left,
                "right" => OdfPageUsage.Right,
                "mirrored" => OdfPageUsage.Mirrored,
                _ => OdfPageUsage.All,
            };
        }
        set
        {
            var props = FindOrCreatePageLayoutProperties();
            string str = value switch
            {
                OdfPageUsage.Left => "left",
                OdfPageUsage.Right => "right",
                OdfPageUsage.Mirrored => "mirrored",
                _ => "all",
            };
            props.SetAttribute("page-usage", OdfNamespaces.Style, str, "style");
        }
    }

    /// <summary>
    /// 取得或設定頁面的文字書寫模式。
    /// </summary>
    public OdfWritingMode WritingMode
    {
        get
        {
            var props = FindOrCreatePageLayoutProperties();
            return OdfWritingModeExtensions.FromOdfToken(props.GetAttribute("writing-mode", OdfNamespaces.Style));
        }
        set
        {
            var props = FindOrCreatePageLayoutProperties();
            props.SetAttribute("writing-mode", OdfNamespaces.Style, value.ToOdfToken(), "style");
        }
    }

    private string? GetPageStyleProp(string name)
    {
        var props = FindOrCreatePageLayoutProperties();
        return props.GetAttribute(name, OdfNamespaces.Style);
    }

    private void SetPageStyleProp(string name, string? val)
    {
        var props = FindOrCreatePageLayoutProperties();
        if (val is not null)
        {
            props.SetAttribute(name, OdfNamespaces.Style, val, "style");
        }
        else
        {
            props.RemoveAttribute(name, OdfNamespaces.Style);
        }
    }

    /// <summary>
    /// 取得或設定頁面版面配置網格的模式。
    /// </summary>
    public OdfLayoutGridMode LayoutGridMode
    {
        get
        {
            return (GetPageStyleProp("layout-grid-mode") ?? "none") switch
            {
                "line" => OdfLayoutGridMode.Line,
                "both" => OdfLayoutGridMode.Both,
                _ => OdfLayoutGridMode.None,
            };
        }
        set
        {
            string str = value switch
            {
                OdfLayoutGridMode.Line => "line",
                OdfLayoutGridMode.Both => "both",
                _ => "none",
            };
            SetPageStyleProp("layout-grid-mode", str);
        }
    }

    /// <summary>
    /// 取得或設定版面配置網格的基礎高度。
    /// </summary>
    public string? LayoutGridBaseHeight
    {
        get => GetPageStyleProp("layout-grid-base-height");
        set => SetPageStyleProp("layout-grid-base-height", value);
    }

    /// <summary>
    /// 取得或設定版面配置網格的基礎寬度。
    /// </summary>
    public string? LayoutGridBaseWidth
    {
        get => GetPageStyleProp("layout-grid-base-width");
        set => SetPageStyleProp("layout-grid-base-width", value);
    }

    /// <summary>
    /// 取得或設定版面配置網格的旁註標記（注音）高度。
    /// </summary>
    public string? LayoutGridRubyHeight
    {
        get => GetPageStyleProp("layout-grid-ruby-height");
        set => SetPageStyleProp("layout-grid-ruby-height", value);
    }

    /// <summary>
    /// 取得或設定版面配置網格的行數。
    /// </summary>
    public int? LayoutGridLines
    {
        get => int.TryParse(GetPageStyleProp("layout-grid-lines"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var val) ? val : null;
        set => SetPageStyleProp("layout-grid-lines", value?.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// 取得或設定版面配置網格的字數。
    /// </summary>
    public int? LayoutGridCharacters
    {
        get => int.TryParse(GetPageStyleProp("layout-grid-characters"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var val) ? val : null;
        set => SetPageStyleProp("layout-grid-characters", value?.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// 取得或設定一個值，指出是否顯示版面配置網格。
    /// </summary>
    public bool? LayoutGridDisplay
    {
        get => GetPageStyleProp("layout-grid-display") == "true" ? true : (GetPageStyleProp("layout-grid-display") == "false" ? false : null);
        set => SetPageStyleProp("layout-grid-display", value is null ? null : (value.Value ? "true" : "false"));
    }

    /// <summary>
    /// 取得或設定一個值，指出是否列印版面配置網格。
    /// </summary>
    public bool? LayoutGridPrint
    {
        get => GetPageStyleProp("layout-grid-print") == "true" ? true : (GetPageStyleProp("layout-grid-print") == "false" ? false : null);
        set => SetPageStyleProp("layout-grid-print", value is null ? null : (value.Value ? "true" : "false"));
    }

    /// <summary>
    /// 取得或設定頁首的文字內容。
    /// </summary>
    public string? HeaderText
    {
        get => GetHeaderFooterText("header");
        set => SetHeaderFooterText("header", value);
    }

    /// <summary>
    /// 取得或設定左頁首的文字內容。
    /// </summary>
    public string? HeaderLeftText
    {
        get => GetHeaderFooterText("header-left");
        set => SetHeaderFooterText("header-left", value);
    }

    /// <summary>
    /// 取得或設定頁尾的文字內容。
    /// </summary>
    public string? FooterText
    {
        get => GetHeaderFooterText("footer");
        set => SetHeaderFooterText("footer", value);
    }

    /// <summary>
    /// 取得或設定左頁尾的文字內容。
    /// </summary>
    public string? FooterLeftText
    {
        get => GetHeaderFooterText("footer-left");
        set => SetHeaderFooterText("footer-left", value);
    }
}
