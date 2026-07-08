using OdfKit.Styles;

namespace OdfKit.Presentation;

/// <summary>
/// Describes a high-level shape update request for presentation or drawing documents.
/// 描述簡報或繪圖文件的高階圖形更新要求。
/// </summary>
public sealed class OdfShapeUpdateRequest
{
    /// <summary>
    /// Gets or sets the shape id or name to update.
    /// 取得或設定要更新的圖形識別碼或名稱。
    /// </summary>
    public string Name { get; set; } = string.Empty;

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

    /// <summary>
    /// Gets or sets the optional layer name.
    /// 取得或設定選用的圖層名稱。
    /// </summary>
    public string? LayerName { get; set; }

    /// <summary>
    /// Gets or sets the optional fill color.
    /// 取得或設定選用的填滿色。
    /// </summary>
    public string? FillColor { get; set; }

    /// <summary>
    /// Gets or sets the optional stroke color.
    /// 取得或設定選用的筆觸色。
    /// </summary>
    public string? StrokeColor { get; set; }

    /// <summary>
    /// Gets or sets the optional z-index.
    /// 取得或設定選用的 z-index。
    /// </summary>
    public int? ZIndex { get; set; }
}
