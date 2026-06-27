using System;
using System.Collections.Generic;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 提供活頁簿層級的公式查詢與批次更新 API。
/// </summary>
public partial class SpreadsheetDocument
{
    /// <summary>
    /// 取得活頁簿中所有含公式的已使用儲存格。
    /// </summary>
    /// <returns>含公式儲存格資訊列舉</returns>
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
    /// 尋找活頁簿中符合指定條件的公式儲存格。
    /// </summary>
    /// <param name="predicate">用來篩選公式儲存格的條件委派</param>
    /// <returns>符合條件的公式儲存格資訊列舉</returns>
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
    /// 更新活頁簿中的所有公式。
    /// </summary>
    /// <param name="updater">接收目前公式資訊並傳回新公式的委派；傳回 <see langword="null"/> 表示不變更</param>
    /// <returns>實際變更的公式數量</returns>
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
    /// 取代活頁簿所有公式中的指定文字。
    /// </summary>
    /// <param name="oldValue">要尋找的文字</param>
    /// <param name="newValue">替換後的文字</param>
    /// <param name="comparisonType">文字比對方式</param>
    /// <returns>實際變更的公式數量</returns>
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
