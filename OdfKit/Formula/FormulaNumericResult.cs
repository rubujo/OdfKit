namespace OdfKit.Formula;

/// <summary>
/// 統一正規化公式計算結果，避免 IEEE 754 非有限值逸出為儲存格值。
/// </summary>
internal static class FormulaNumericResult
{
    internal static object Normalize(object result) =>
        result is double value && (double.IsNaN(value) || double.IsInfinity(value)) ||
        result is float singleValue && (float.IsNaN(singleValue) || float.IsInfinity(singleValue))
            ? OdfFormulaError.Num
            : result;
}
