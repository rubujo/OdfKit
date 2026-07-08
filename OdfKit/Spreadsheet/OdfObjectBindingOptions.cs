using System;
using System.Globalization;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Configures object-to-spreadsheet binding.
/// 設定物件寫入試算表的繫結行為。
/// </summary>
public class OdfObjectBindingOptions
{
    /// <summary>
    /// Gets or sets whether a header row is written before data rows.
    /// 取得或設定是否在資料列前寫入標題列。
    /// </summary>
    public bool IncludeHeader { get; set; } = true;

    /// <summary>
    /// Gets or sets the zero-based start column used by append operations.
    /// 取得或設定附加操作使用的 0 基準起始欄。
    /// </summary>
    public int StartColumn { get; set; }

    /// <summary>
    /// Gets or sets the optional table name to create around the written range.
    /// 取得或設定要包覆寫入範圍建立的選用表格名稱。
    /// </summary>
    public string? CreateTableName { get; set; }

    /// <summary>
    /// Gets or sets the null value write policy.
    /// 取得或設定 null 值寫入原則。
    /// </summary>
    public OdfObjectNullValuePolicy NullValuePolicy { get; set; } = OdfObjectNullValuePolicy.EmptyCell;

    /// <summary>
    /// Gets or sets a selector that can rewrite resolved header names.
    /// 取得或設定可改寫已解析標題名稱的選取器。
    /// </summary>
    public Func<string, string>? HeaderNameSelector { get; set; }

    /// <summary>
    /// Gets or sets the explicit property-to-column map.
    /// 取得或設定明確的屬性與欄位對應。
    /// </summary>
    public OdfObjectColumnMap? ColumnMap { get; set; }

    /// <summary>
    /// Gets or sets the culture used when creating number formats.
    /// 取得或設定建立數字格式時使用的文化特性。
    /// </summary>
    public CultureInfo? CultureInfo { get; set; }
}
