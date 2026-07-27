using System;
using System.Collections.Generic;
using System.Linq;
using OdfKit.Compliance;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Represents a practical spreadsheet table backed by a database range.
/// 表示由資料庫範圍支援的實務試算表表格。
/// </summary>
public sealed class OdfSpreadsheetTable
{
    private readonly OdfDatabaseRange _databaseRange;
    private readonly SpreadsheetDocument _document;

    internal OdfSpreadsheetTable(OdfDatabaseRange databaseRange, SpreadsheetDocument document, bool firstRowAsHeader)
    {
        _databaseRange = databaseRange ?? throw new ArgumentNullException(nameof(databaseRange));
        _document = document ?? throw new ArgumentNullException(nameof(document));
        FirstRowAsHeader = firstRowAsHeader;
    }

    /// <summary>
    /// Gets or sets the table name.
    /// 取得或設定表格名稱。
    /// </summary>
    public string Name
    {
        get => _databaseRange.Name;
        set => _databaseRange.Name = value;
    }

    /// <summary>
    /// Gets whether the first row is treated as a header row.
    /// 取得首列是否視為標題列。
    /// </summary>
    public bool FirstRowAsHeader { get; }

    /// <summary>
    /// Gets or sets whether filter buttons are displayed.
    /// 取得或設定是否顯示篩選按鈕。
    /// </summary>
    public bool DisplayFilterButtons
    {
        get => _databaseRange.DisplayFilterButtons;
        set => _databaseRange.DisplayFilterButtons = value;
    }

    /// <summary>
    /// Gets the target range address.
    /// 取得目標範圍位址。
    /// </summary>
    public string TargetRangeAddress => _databaseRange.TargetRangeAddress;

    /// <summary>
    /// Gets the zero-based table field index for the specified header name.
    /// 依指定標題名稱取得 0 基準的表格欄位索引。
    /// </summary>
    /// <param name="columnName">The header name to resolve. / 要解析的標題名稱。</param>
    /// <returns>The zero-based field index, or -1 when not found. / 0 基準欄位索引；找不到時為 -1。</returns>
    public int GetColumnIndex(string columnName)
    {
        if (string.IsNullOrWhiteSpace(columnName) || !FirstRowAsHeader)
        {
            return -1;
        }

        if (!OdfCellRange.TryParse(TargetRangeAddress, out OdfCellRange range))
        {
            return -1;
        }

        string? sheetName = range.StartAddress.SheetName;
        if (string.IsNullOrEmpty(sheetName))
        {
            return -1;
        }

        OdfTableSheet? sheet = _document.FindSheet(sheetName!);
        if (sheet is null)
        {
            return -1;
        }

        int headerRow = Math.Min(range.StartAddress.Row, range.EndAddress.Row);
        int startColumn = Math.Min(range.StartAddress.Column, range.EndAddress.Column);
        int endColumn = Math.Max(range.StartAddress.Column, range.EndAddress.Column);
        for (int column = startColumn; column <= endColumn; column++)
        {
            if (string.Equals(sheet.GetCell(headerRow, column).DisplayText, columnName, StringComparison.OrdinalIgnoreCase))
            {
                return column - startColumn;
            }
        }

        return -1;
    }

    /// <summary>
    /// Resizes the table metadata to the specified range.
    /// 將表格 metadata 調整為指定範圍。
    /// </summary>
    /// <param name="range">The new cell range. / 新儲存格範圍。</param>
    public void Resize(OdfCellRange range) =>
        _databaseRange.TargetRangeAddress = range.ToOdfString(false);

    /// <summary>
    /// Applies filter conditions to the table metadata.
    /// 將篩選條件套用至表格 metadata。
    /// </summary>
    /// <param name="conditions">The filter conditions. / 篩選條件。</param>
    public void ApplyFilter(params OdfDatabaseFilterConditionInfo[] conditions)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(conditions, nameof(conditions));

        _databaseRange.SetFilter(conditions.Select(condition =>
            (condition.FieldNumber, condition.Operator, condition.Value)).ToArray());
    }

    /// <summary>
    /// Applies a filter condition by resolving a header name.
    /// 依標題名稱解析欄位並套用篩選條件。
    /// </summary>
    /// <param name="columnName">The header name. / 標題名稱。</param>
    /// <param name="op">The ODF filter operator. / ODF 篩選運算子。</param>
    /// <param name="value">The filter value. / 篩選值。</param>
    public void ApplyFilter(string columnName, string op, string value)
    {
        int index = GetColumnIndex(columnName);
        if (index < 0)
        {
            throw new KeyNotFoundException(OdfLocalizer.GetMessage("Err_OdfSpreadsheetTable_ColumnNotFound", columnName));
        }

        ApplyFilter(new OdfDatabaseFilterConditionInfo(index, op, value));
    }

    /// <summary>
    /// Removes filter conditions from the table metadata.
    /// 從表格 metadata 移除篩選條件。
    /// </summary>
    public void ClearFilter() =>
        _databaseRange.SetFilter(Array.Empty<(int fieldNumber, string op, string value)>());

    /// <summary>
    /// Applies sort rules to the table metadata.
    /// 將排序規則套用至表格 metadata。
    /// </summary>
    /// <param name="rules">The sort rules. / 排序規則。</param>
    public void ApplySort(params OdfDatabaseSortRuleInfo[] rules)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(rules, nameof(rules));

        _databaseRange.SetSort(rules.Select(rule => (rule.FieldNumber, rule.Ascending)).ToArray());
    }
    /// <summary>
    /// Short overload of ApplySort that accepts columnName; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 columnName；其餘可選參數使用預設值並轉呼叫最長 ApplySort 多載。
    /// </summary>
    public void ApplySort(string columnName) => ApplySort(columnName, true);


    /// <summary>
    /// Applies a sort rule by resolving a header name.
    /// 依標題名稱解析欄位並套用排序規則。
    /// </summary>
    /// <param name="columnName">The header name. / 標題名稱。</param>
    /// <param name="ascending">Whether the sort order is ascending. / 是否遞增排序。</param>
    public void ApplySort(string columnName, bool ascending)
    {
        int index = GetColumnIndex(columnName);
        if (index < 0)
        {
            throw new KeyNotFoundException(OdfLocalizer.GetMessage("Err_OdfSpreadsheetTable_ColumnNotFound", columnName));
        }

        ApplySort(new OdfDatabaseSortRuleInfo(index, ascending));
    }


    /// <summary>
    /// Removes sort rules from the table metadata.
    /// 從表格 metadata 移除排序規則。
    /// </summary>
    public void ClearSort() =>
        _databaseRange.SetSort(Array.Empty<(int fieldNumber, bool ascending)>());
}
