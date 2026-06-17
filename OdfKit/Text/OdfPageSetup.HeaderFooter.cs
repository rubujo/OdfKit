namespace OdfKit.Text;

public partial class OdfPageSetup
{
    /// <summary>
    /// 取得此頁面設定對應的主頁面樣式名稱。
    /// </summary>
    public string MasterPageName => _masterPageName;

    /// <summary>
    /// 取得預設頁首區域。
    /// </summary>
    public OdfPageHeaderFooter Header => new(_doc, this, "header");

    /// <summary>
    /// 取得左頁首區域（鏡像版面）。
    /// </summary>
    public OdfPageHeaderFooter HeaderLeft => new(_doc, this, "header-left");

    /// <summary>
    /// 取得首頁專用頁首區域。
    /// </summary>
    public OdfPageHeaderFooter HeaderFirst => new(_doc, this, "header-first");

    /// <summary>
    /// 取得預設頁尾區域。
    /// </summary>
    public OdfPageHeaderFooter Footer => new(_doc, this, "footer");

    /// <summary>
    /// 取得左頁尾區域（鏡像版面）。
    /// </summary>
    public OdfPageHeaderFooter FooterLeft => new(_doc, this, "footer-left");

    /// <summary>
    /// 取得首頁專用頁尾區域。
    /// </summary>
    public OdfPageHeaderFooter FooterFirst => new(_doc, this, "footer-first");

    /// <summary>
    /// 取得或設定頁首區域最小高度（例如 <c>1.5cm</c>）。
    /// </summary>
    public string? HeaderMinHeight
    {
        get => GetHeaderFooterLayoutProp("header-style", "min-height");
        set => SetHeaderFooterLayoutProp("header-style", "min-height", value);
    }

    /// <summary>
    /// 取得或設定頁尾區域最小高度（例如 <c>1cm</c>）。
    /// </summary>
    public string? FooterMinHeight
    {
        get => GetHeaderFooterLayoutProp("footer-style", "min-height");
        set => SetHeaderFooterLayoutProp("footer-style", "min-height", value);
    }

    /// <summary>
    /// 取得或設定頁首是否採用動態間距。
    /// </summary>
    public bool? HeaderDynamicSpacing
    {
        get => GetHeaderFooterDynamicSpacing("header-style");
        set => SetHeaderFooterLayoutProp("header-style", "dynamic-spacing", value is null ? null : (value.Value ? "true" : "false"));
    }

    /// <summary>
    /// 取得或設定頁尾是否採用動態間距。
    /// </summary>
    public bool? FooterDynamicSpacing
    {
        get => GetHeaderFooterDynamicSpacing("footer-style");
        set => SetHeaderFooterLayoutProp("footer-style", "dynamic-spacing", value is null ? null : (value.Value ? "true" : "false"));
    }
}
