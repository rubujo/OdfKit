using OdfKit.Styles;

namespace OdfKit.Export;

/// <summary>
/// SVG 匯出的選項設定。
/// </summary>
public sealed class OdfSvgExportOptions
{
    /// <summary>
    /// Gets or sets page index.
    /// 取得或設定要匯出的繪圖頁面索引，預設為 0。
    /// </summary>
    public int PageIndex { get; init; }

    /// <summary>
    /// Gets or sets include xml declaration.
    /// 取得或設定是否輸出 XML 宣告，預設為 false。
    /// </summary>
    public bool IncludeXmlDeclaration { get; init; }

    /// <summary>
    /// Gets or sets from centimeters.
    /// 取得或設定沒有可推估邊界時使用的預設寬度。
    /// </summary>
    public OdfLength DefaultWidth { get; init; } = OdfLength.FromCentimeters(29.7);

    /// <summary>
    /// Gets or sets from centimeters.
    /// 取得或設定沒有可推估邊界時使用的預設高度。
    /// </summary>
    public OdfLength DefaultHeight { get; init; } = OdfLength.FromCentimeters(21.0);

    /// <summary>
    /// Gets or sets preserve ids.
    /// 取得或設定是否將 ODF 圖形識別碼保留到 SVG <c>id</c> 屬性，預設為 true。
    /// </summary>
    public bool PreserveIds { get; init; } = true;

    /// <summary>
    /// Gets or sets embed package images as data uri.
    /// 取得或設定是否將 ODF 封裝內圖片內嵌為 SVG data URI，預設為 true。
    /// </summary>
    public bool EmbedPackageImagesAsDataUri { get; init; } = true;
}
