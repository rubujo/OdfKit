using System;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 表示 ODS 儲存格批注（office:annotation）的資料。
/// </summary>
public sealed class OdfCellAnnotation
{
    /// <summary>批注的純文字內容。</summary>
    public string Text { get; init; } = string.Empty;
    /// <summary>批注作者。</summary>
    public string? Author { get; init; }
    /// <summary>批注的建立日期時間（UTC）。</summary>
    public DateTime? Date { get; init; }
    /// <summary>批注是否顯示。</summary>
    public bool Visible { get; init; }
}
