using System;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Represents data for an ODS cell annotation (<c>office:annotation</c>).
/// 表示 ODS 儲存格批注（office:annotation）的資料。
/// </summary>
public sealed class OdfCellAnnotation
{
    /// <summary>
    /// Gets the plain text content of the annotation.
    /// 批注的純文字內容。
    /// </summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// Gets the annotation author.
    /// 批注作者。
    /// </summary>
    public string? Author { get; init; }

    /// <summary>
    /// Gets the annotation creation date and time in UTC.
    /// 批注的建立日期時間（UTC）。
    /// </summary>
    public DateTime? Date { get; init; }

    /// <summary>
    /// Gets a value indicating whether the annotation is displayed.
    /// 批注是否顯示。
    /// </summary>
    public bool Visible { get; init; }
}
