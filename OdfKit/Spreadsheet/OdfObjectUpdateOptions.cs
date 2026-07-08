using System;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Configures key-based object updates for spreadsheet tables.
/// 設定試算表資料表的 key-based 物件更新。
/// </summary>
public sealed class OdfObjectUpdateOptions : OdfObjectBindingOptions
{
    /// <summary>
    /// Gets or sets the mapped header or property name used as the row key.
    /// 取得或設定作為資料列 key 的對應標題或屬性名稱。
    /// </summary>
    public string? KeyColumn { get; set; }

    /// <summary>
    /// Gets or sets the comparer used for key matching.
    /// 取得或設定 key 比對使用的比較器。
    /// </summary>
    public StringComparer KeyComparer { get; set; } = StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// Gets or sets how missing object keys are handled.
    /// 取得或設定缺少物件 key 時的處理方式。
    /// </summary>
    public OdfObjectMissingKeyPolicy MissingKeyPolicy { get; set; }

    /// <summary>
    /// Gets or sets whether cells not mapped to object properties are preserved.
    /// 取得或設定是否保留未對應至物件屬性的儲存格。
    /// </summary>
    public bool PreserveUnmappedCells { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the practical table metadata is resized after inserting rows.
    /// 取得或設定新增資料列後是否調整 practical table metadata。
    /// </summary>
    public bool ResizeTable { get; set; } = true;

    /// <summary>
    /// Gets or sets whether existing data cell styles are preserved during updates.
    /// 取得或設定更新時是否保留既有資料儲存格樣式。
    /// </summary>
    public bool PreserveDataStyles { get; set; } = true;

    /// <summary>
    /// Gets or sets whether inserted rows copy styles from the template row.
    /// 取得或設定新增資料列是否從模板列複製樣式。
    /// </summary>
    public bool CopyStylesFromTemplateRow { get; set; } = true;

    /// <summary>
    /// Gets or sets whether inserted rows copy formulas from the template row.
    /// 取得或設定新增資料列是否從模板列複製公式。
    /// </summary>
    public bool FillFormulasFromTemplateRow { get; set; } = true;

    /// <summary>
    /// Gets or sets how formulas are copied from the template row.
    /// 取得或設定如何從模板列複製公式。
    /// </summary>
    public OdfFormulaCopyMode FormulaCopyMode { get; set; } = OdfFormulaCopyMode.ShiftRelativeReferences;
}
