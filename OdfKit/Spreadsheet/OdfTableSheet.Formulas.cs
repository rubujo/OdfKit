using System;
using System.Collections.Generic;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 提供工作表公式查詢與批次更新 API。
/// </summary>
public partial class OdfTableSheet
{
    /// <summary>
    /// 取得此工作表中所有含公式的已使用儲存格。
    /// </summary>
    /// <returns>含公式儲存格資訊列舉</returns>
    public IEnumerable<OdfFormulaCellInfo> GetFormulaCells()
    {
        foreach (OdfCell cell in GetUsedCells())
        {
            string formula = cell.Formula;
            if (!string.IsNullOrEmpty(formula))
            {
                yield return CreateFormulaCellInfo(cell, formula);
            }
        }
    }

    /// <summary>
    /// 尋找此工作表中符合指定條件的公式儲存格。
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
    /// 取得指定儲存格的公式。
    /// </summary>
    /// <param name="address">儲存格位址，例如 <c>A1</c></param>
    /// <returns>指定儲存格的公式；若沒有公式則為空字串</returns>
    public string GetFormula(string address)
    {
        return Cells[address].Formula;
    }

    /// <summary>
    /// 嘗試取得指定儲存格的公式。
    /// </summary>
    /// <param name="address">儲存格位址，例如 <c>A1</c></param>
    /// <param name="formula">取得成功時傳回公式文字</param>
    /// <returns>若指定儲存格具有公式則為 <see langword="true"/>，否則為 <see langword="false"/></returns>
    public bool TryGetFormula(string address, out string formula)
    {
        formula = GetFormula(address);
        return !string.IsNullOrEmpty(formula);
    }

    /// <summary>
    /// 設定指定儲存格的公式。
    /// </summary>
    /// <param name="address">儲存格位址，例如 <c>A1</c></param>
    /// <param name="formula">要寫入的公式；傳入空字串會清除公式</param>
    /// <returns>已更新的儲存格 facade</returns>
    public OdfCell SetFormula(string address, string formula)
    {
        OdfCell cell = Cells[address];
        cell.Formula = formula;
        return cell;
    }

    /// <summary>
    /// 更新此工作表中的公式。
    /// </summary>
    /// <param name="updater">接收目前公式資訊並傳回新公式的委派；傳回 <see langword="null"/> 表示不變更</param>
    /// <returns>實際變更的公式數量</returns>
    public int UpdateFormulas(Func<OdfFormulaCellInfo, string?> updater)
    {
        if (updater is null)
            throw new ArgumentNullException(nameof(updater));

        int updatedCount = 0;
        foreach (OdfFormulaCellInfo formulaCell in GetFormulaCells())
        {
            string? newFormula = updater(formulaCell);
            if (newFormula is null || string.Equals(formulaCell.Formula, newFormula, StringComparison.Ordinal))
            {
                continue;
            }

            formulaCell.Cell.Formula = newFormula;
            updatedCount++;
        }

        return updatedCount;
    }

    /// <summary>
    /// 取代此工作表所有公式中的指定文字。
    /// </summary>
    /// <param name="oldValue">要尋找的文字</param>
    /// <param name="newValue">替換後的文字</param>
    /// <param name="comparisonType">文字比對方式</param>
    /// <returns>實際變更的公式數量</returns>
    public int ReplaceFormulaText(string oldValue, string newValue, StringComparison comparisonType = StringComparison.Ordinal)
    {
        if (oldValue is null)
            throw new ArgumentNullException(nameof(oldValue));
        if (newValue is null)
            throw new ArgumentNullException(nameof(newValue));

        return UpdateFormulas(formulaCell =>
            formulaCell.Formula.IndexOf(oldValue, comparisonType) >= 0
                ? ReplaceText(formulaCell.Formula, oldValue, newValue, comparisonType)
                : null);
    }

    private static string ReplaceText(string text, string oldValue, string newValue, StringComparison comparisonType)
    {
        if (oldValue.Length == 0)
        {
            return text;
        }

        int startIndex = 0;
        int matchIndex = text.IndexOf(oldValue, startIndex, comparisonType);
        if (matchIndex < 0)
        {
            return text;
        }

        var builder = new System.Text.StringBuilder(text.Length);
        while (matchIndex >= 0)
        {
            builder.Append(text, startIndex, matchIndex - startIndex);
            builder.Append(newValue);
            startIndex = matchIndex + oldValue.Length;
            matchIndex = text.IndexOf(oldValue, startIndex, comparisonType);
        }

        builder.Append(text, startIndex, text.Length - startIndex);
        return builder.ToString();
    }

    private OdfFormulaCellInfo CreateFormulaCellInfo(OdfCell cell, string formula)
    {
        return new OdfFormulaCellInfo(
            Name,
            new OdfCellAddress(cell.Row, cell.Column, Name),
            cell,
            formula);
    }
}
