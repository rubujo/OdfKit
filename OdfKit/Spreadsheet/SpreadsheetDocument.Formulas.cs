using System;
using System.Collections.Generic;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Provides workbook-level formula query and batch update APIs.
/// 提供活頁簿層級的公式查詢與批次更新 API。
/// </summary>
public partial class SpreadsheetDocument
{
    /// <summary>
    /// Gets all used cells with formulas in the workbook.
    /// 取得活頁簿中所有含公式的已使用儲存格。
    /// </summary>
    /// <returns>The result. / 含公式儲存格資訊列舉</returns>
    public IEnumerable<OdfFormulaCellInfo> GetFormulaCells()
    {
        foreach (OdfTableSheet sheet in Worksheets)
        {
            foreach (OdfFormulaCellInfo formulaCell in sheet.GetFormulaCells())
            {
                yield return formulaCell;
            }
        }
    }

    /// <summary>
    /// Finds formula cells in the workbook that match the specified predicate.
    /// 尋找活頁簿中符合指定條件的公式儲存格。
    /// </summary>
    /// <param name="predicate">The delegate to invoke. / 用來篩選公式儲存格的條件委派</param>
    /// <returns>The result. / 符合條件的公式儲存格資訊列舉</returns>
    public IEnumerable<OdfFormulaCellInfo> FindFormulaCells(Func<OdfFormulaCellInfo, bool> predicate)
    {
        if (predicate is null)
            throw new ArgumentNullException(nameof(predicate));

        foreach (OdfFormulaCellInfo formulaCell in GetFormulaCells())
        {
            if (predicate(formulaCell))
            {
                yield return formulaCell;
            }
        }
    }

    /// <summary>
    /// Gets the formula for a cell in the specified worksheet.
    /// 取得指定工作表儲存格的公式。
    /// </summary>
    /// <param name="sheetName">The name or identifier. / 工作表名稱</param>
    /// <param name="address">The cell address. / 儲存格位址，例如 <c>A1</c></param>
    /// <returns>The result. / 指定儲存格的公式；若沒有公式則為空字串</returns>
    public string GetFormula(string sheetName, string address)
    {
        return Worksheets[sheetName].GetFormula(address);
    }

    /// <summary>
    /// Attempts to get the formula for a cell in the specified worksheet.
    /// 嘗試取得指定工作表儲存格的公式。
    /// </summary>
    /// <param name="sheetName">The name or identifier. / 工作表名稱</param>
    /// <param name="address">The cell address. / 儲存格位址，例如 <c>A1</c></param>
    /// <param name="formula">The value to use. / 取得成功時傳回公式文字</param>
    /// <returns>The result. / 若指定儲存格具有公式則為 <see langword="true"/>，否則為 <see langword="false"/></returns>
    public bool TryGetFormula(string sheetName, string address, out string formula)
    {
        return Worksheets[sheetName].TryGetFormula(address, out formula);
    }

    /// <summary>
    /// Sets the formula for a cell in the specified worksheet.
    /// 設定指定工作表儲存格的公式。
    /// </summary>
    /// <param name="sheetName">The name or identifier. / 工作表名稱</param>
    /// <param name="address">The cell address. / 儲存格位址，例如 <c>A1</c></param>
    /// <param name="formula">The value to use. / 要寫入的公式；傳入空字串會清除公式</param>
    /// <returns>The result. / 已更新的儲存格 facade</returns>
    public OdfCell SetFormula(string sheetName, string address, string formula)
    {
        return Worksheets[sheetName].SetFormula(address, formula);
    }

    /// <summary>
    /// Updates all formulas in the workbook.
    /// 更新活頁簿中的所有公式。
    /// </summary>
    /// <param name="updater">The delegate to invoke. / 接收目前公式資訊並傳回新公式的委派；傳回 <see langword="null"/> 表示不變更</param>
    /// <returns>The result. / 實際變更的公式數量</returns>
    public int UpdateFormulas(Func<OdfFormulaCellInfo, string?> updater)
    {
        if (updater is null)
            throw new ArgumentNullException(nameof(updater));

        int updatedCount = 0;
        foreach (OdfTableSheet sheet in Worksheets)
        {
            updatedCount += sheet.UpdateFormulas(updater);
        }

        return updatedCount;
    }

    /// <summary>
    /// Replaces the specified text in all formulas in the workbook.
    /// 取代活頁簿所有公式中的指定文字。
    /// </summary>
    /// <param name="oldValue">The value to use. / 要尋找的文字</param>
    /// <param name="newValue">The value to use. / 替換後的文字</param>
    /// <param name="comparisonType">The value to use. / 文字比對方式</param>
    /// <returns>The result. / 實際變更的公式數量</returns>
    public int ReplaceFormulaText(string oldValue, string newValue, StringComparison comparisonType = StringComparison.Ordinal)
    {
        int updatedCount = 0;
        foreach (OdfTableSheet sheet in Worksheets)
        {
            updatedCount += sheet.ReplaceFormulaText(oldValue, newValue, comparisonType);
        }

        return updatedCount;
    }
}
