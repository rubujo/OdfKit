using System;
using System.Collections.Generic;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Represents summary information for a data validation rule in a spreadsheet.
/// 表示試算表中一筆資料驗證規則的摘要資訊。
/// </summary>
/// <param name="name">The rule name from <c>table:name</c>. / 規則名稱（<c>table:name</c>）。</param>
/// <param name="conditionExpression">The original condition expression from <c>table:condition</c>. / 條件運算式原文（<c>table:condition</c>）。</param>
/// <param name="errorMessage">The error message content. / 錯誤訊息內容。</param>
/// <param name="errorTitle">The error message title. / 錯誤訊息標題。</param>
/// <param name="alertStyle">The alert style, such as <c>stop</c>, <c>warning</c>, or <c>information</c>. / 警告樣式（<c>stop</c>、<c>warning</c>、<c>information</c>）。</param>
/// <param name="appliedRanges">The list of cell ranges to which this rule applies. / 套用此規則的儲存格範圍清單。</param>
public sealed class OdfDataValidationInfo(
    string name,
    string conditionExpression,
    string? errorMessage,
    string? errorTitle,
    string? alertStyle,
    IReadOnlyList<OdfCellRange> appliedRanges)
{
    /// <summary>
    /// Gets the rule name.
    /// 取得規則名稱。
    /// </summary>
    public string Name { get; } = name ?? string.Empty;

    /// <summary>
    /// Gets the original condition expression.
    /// 取得條件運算式原文。
    /// </summary>
    public string ConditionExpression { get; } = conditionExpression ?? string.Empty;

    /// <summary>
    /// Gets the error message content.
    /// 取得錯誤訊息內容。
    /// </summary>
    public string? ErrorMessage { get; } = errorMessage;

    /// <summary>
    /// Gets the error message title.
    /// 取得錯誤訊息標題。
    /// </summary>
    public string? ErrorTitle { get; } = errorTitle;

    /// <summary>
    /// Gets the alert style.
    /// 取得警告樣式。
    /// </summary>
    public string? AlertStyle { get; } = alertStyle;

    /// <summary>
    /// Gets the list of cell ranges to which this rule applies.
    /// 取得套用此規則的儲存格範圍清單。
    /// </summary>
    public IReadOnlyList<OdfCellRange> AppliedRanges { get; } = appliedRanges ?? [];

    /// <summary>
    /// Attempts to map <see cref="ConditionExpression"/> to <see cref="OdfValidationCondition"/>.
    /// 嘗試將 <see cref="ConditionExpression"/> 對應至 <see cref="OdfValidationCondition"/>。
    /// </summary>
    /// <param name="condition">The condition type returned when parsing succeeds. / 解析成功時傳回的條件類型。</param>
    /// <returns><see langword="true"/> if the condition can be recognized. / 若可辨識則為 <see langword="true"/>。</returns>
    public bool TryGetCondition(out OdfValidationCondition condition)
    {
        if (ConditionExpression.Contains("cell-content-is-decimal-number()", StringComparison.Ordinal))
        {
            condition = OdfValidationCondition.DecimalBetween;
            return true;
        }

        if (ConditionExpression.Contains("cell-content-text-length-is-between(", StringComparison.Ordinal))
        {
            condition = OdfValidationCondition.TextLengthBetween;
            return true;
        }

        if (ConditionExpression.Contains("cell-content-is-whole-number()", StringComparison.Ordinal))
        {
            condition = OdfValidationCondition.IntegerBetween;
            return true;
        }

        condition = default;
        return false;
    }
}
