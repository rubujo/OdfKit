using OdfKit.Styles;

namespace OdfKit.Presentation;

/// <summary>
/// Describes a high-level picture update request for presentation or drawing documents.
/// 描述簡報或繪圖文件的高階圖片更新要求。
/// </summary>
public sealed class OdfPictureUpdateRequest
{
    /// <summary>
    /// Gets or sets the picture id or name to update.
    /// 取得或設定要更新的圖片識別碼或名稱。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional alternative text.
    /// 取得或設定選用的替代文字。
    /// </summary>
    public string? AltText { get; set; }

    /// <summary>
    /// Gets or sets the optional X-axis position.
    /// 取得或設定選用的 X 軸位置。
    /// </summary>
    public OdfLength? X { get; set; }

    /// <summary>
    /// Gets or sets the optional Y-axis position.
    /// 取得或設定選用的 Y 軸位置。
    /// </summary>
    public OdfLength? Y { get; set; }

    /// <summary>
    /// Gets or sets the optional width.
    /// 取得或設定選用的寬度。
    /// </summary>
    public OdfLength? Width { get; set; }

    /// <summary>
    /// Gets or sets the optional height.
    /// 取得或設定選用的高度。
    /// </summary>
    public OdfLength? Height { get; set; }
}
