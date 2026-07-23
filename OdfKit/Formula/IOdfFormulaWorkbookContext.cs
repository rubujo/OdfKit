using System.Collections.Generic;
using OdfKit.Spreadsheet;

namespace OdfKit.Formula;

/// <summary>
/// Supplies workbook-level metadata and optional calculation services to formula evaluation.
/// 提供公式評估所需的活頁簿層級中繼資料與選配計算服務。
/// </summary>
public interface IOdfFormulaWorkbookContext : IEvaluationContext
{
    /// <summary>
    /// Gets worksheet names in document order.
    /// 依文件順序取得工作表名稱。
    /// </summary>
    IReadOnlyList<string> SheetNames { get; }

    /// <summary>
    /// Attempts to obtain a pivot-table value for a data field and field filters.
    /// 嘗試依資料欄位與欄位篩選條件取得樞紐分析表值。
    /// </summary>
    /// <param name="dataField">The pivot data-field name. / 樞紐分析表資料欄位名稱。</param>
    /// <param name="pivotAnchor">A cell in the pivot table. / 樞紐分析表內的儲存格。</param>
    /// <param name="filters">The field-name to item-value filters. / 欄位名稱至項目值的篩選條件。</param>
    /// <param name="result">The resolved value when available. / 可取得時解析出的值。</param>
    /// <returns><see langword="true"/> when a value is available; otherwise, <see langword="false"/>. / 可取得值時為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    bool TryGetPivotData(
        string dataField,
        OdfCellAddress pivotAnchor,
        IReadOnlyDictionary<string, object> filters,
        out object result);

    /// <summary>
    /// Attempts to evaluate an OpenFormula multiple-operations data table.
    /// 嘗試評估 OpenFormula 多重運算資料表。
    /// </summary>
    /// <param name="arguments">The evaluated function arguments. / 已評估的函式引數。</param>
    /// <param name="result">The calculated data-table result when available. / 可取得時計算出的資料表結果。</param>
    /// <returns><see langword="true"/> when the data table was evaluated; otherwise, <see langword="false"/>. / 已評估資料表時為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    bool TryEvaluateMultipleOperations(
        IReadOnlyList<object> arguments,
        out object result);
}
