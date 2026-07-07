using System;

namespace OdfKit.Core;

/// <summary>
/// Provides the OdfTemplateMetadata API.
/// 表示 ODF 範本中繼資料。
/// </summary>
public sealed class OdfTemplateMetadata
{
    /// <summary>
    /// Gets the Href value.
    /// 取得或設定範本的 URI。
    /// </summary>
    public string? Href { get; set; }

    /// <summary>
    /// Gets the Title value.
    /// 取得或設定範本的標題。
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Gets the Date value.
    /// 取得或設定套用此範本的日期時間。
    /// </summary>
    public DateTime? Date { get; set; }
}
