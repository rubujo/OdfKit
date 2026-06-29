namespace OdfKit.Text;

public partial class OdfPageSetup
{
    /// <summary>
    /// Gets the master page style name corresponding to this page setup.
    /// 取得此頁面設定對應的主頁面樣式名稱。
    /// </summary>
    public string MasterPageName => _masterPageName;

    /// <summary>
    /// Gets the default header region.
    /// 取得預設頁首區域。
    /// </summary>
    public OdfPageHeaderFooter Header => new(_doc, this, "header");

    /// <summary>
    /// Gets the left-page header region (mirrored layout).
    /// 取得左頁首區域（鏡像版面）。
    /// </summary>
    public OdfPageHeaderFooter HeaderLeft => new(_doc, this, "header-left");

    /// <summary>
    /// Gets the first-page-only header region.
    /// 取得首頁專用頁首區域。
    /// </summary>
    public OdfPageHeaderFooter HeaderFirst => new(_doc, this, "header-first");

    /// <summary>
    /// Gets the default footer region.
    /// 取得預設頁尾區域。
    /// </summary>
    public OdfPageHeaderFooter Footer => new(_doc, this, "footer");

    /// <summary>
    /// Gets the left-page footer region (mirrored layout).
    /// 取得左頁尾區域（鏡像版面）。
    /// </summary>
    public OdfPageHeaderFooter FooterLeft => new(_doc, this, "footer-left");

    /// <summary>
    /// Gets the first-page-only footer region.
    /// 取得首頁專用頁尾區域。
    /// </summary>
    public OdfPageHeaderFooter FooterFirst => new(_doc, this, "footer-first");

    /// <summary>
    /// Gets or sets the minimum height of the header region (e.g. <c>1.5cm</c>).
    /// 取得或設定頁首區域最小高度（例如 <c>1.5cm</c>）。
    /// </summary>
    public string? HeaderMinHeight
    {
        get => GetHeaderFooterLayoutProp("header-style", "min-height");
        set => SetHeaderFooterLayoutProp("header-style", "min-height", value);
    }

    /// <summary>
    /// Gets or sets the minimum height of the footer region (e.g. <c>1cm</c>).
    /// 取得或設定頁尾區域最小高度（例如 <c>1cm</c>）。
    /// </summary>
    public string? FooterMinHeight
    {
        get => GetHeaderFooterLayoutProp("footer-style", "min-height");
        set => SetHeaderFooterLayoutProp("footer-style", "min-height", value);
    }

    /// <summary>
    /// Gets or sets whether the header uses dynamic spacing.
    /// 取得或設定頁首是否採用動態間距。
    /// </summary>
    public bool? HeaderDynamicSpacing
    {
        get => GetHeaderFooterDynamicSpacing("header-style");
        set => SetHeaderFooterLayoutProp("header-style", "dynamic-spacing", value is null ? null : (value.Value ? "true" : "false"));
    }

    /// <summary>
    /// Gets or sets whether the footer uses dynamic spacing.
    /// 取得或設定頁尾是否採用動態間距。
    /// </summary>
    public bool? FooterDynamicSpacing
    {
        get => GetHeaderFooterDynamicSpacing("footer-style");
        set => SetHeaderFooterLayoutProp("footer-style", "dynamic-spacing", value is null ? null : (value.Value ? "true" : "false"));
    }
}
