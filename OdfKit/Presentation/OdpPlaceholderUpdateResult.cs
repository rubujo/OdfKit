using System.Collections.Generic;

namespace OdfKit.Presentation;

/// <summary>
/// Reports a slide placeholder update operation.
/// 回報投影片預留位置更新作業。
/// </summary>
public sealed class OdpPlaceholderUpdateResult
{
    /// <summary>
    /// Gets or sets the number of updated placeholders.
    /// 取得或設定已更新的預留位置數量。
    /// </summary>
    public int UpdatedCount { get; set; }

    /// <summary>
    /// Gets requested placeholder types that were not present.
    /// 取得要求處理但不存在的預留位置類型。
    /// </summary>
    public IList<OdfPlaceholderType> MissingPlaceholderTypes { get; } = new List<OdfPlaceholderType>();

    /// <summary>
    /// Gets placeholder types that matched more than one shape.
    /// 取得符合多個圖形的預留位置類型。
    /// </summary>
    public IList<OdfPlaceholderType> AmbiguousPlaceholderTypes { get; } = new List<OdfPlaceholderType>();
}
