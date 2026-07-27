using System;

namespace OdfKit.Formula;

/// <summary>
/// 公式字串前綴正規化工具（內部協作者）。
/// </summary>
internal static class FormulaPrefixNormalizer
{
    /// <summary>
    /// 移除 ODF／Excel 公式前綴（<c>oooc:=</c>、<c>of:=</c>、<c>=</c>）。
    /// </summary>
    internal static string RemovePrefix(string formula)
    {
        if (formula.StartsWith("oooc:=", StringComparison.OrdinalIgnoreCase))
            formula = formula.Substring(6);
        else if (formula.StartsWith("of:=", StringComparison.OrdinalIgnoreCase))
            formula = formula.Substring(4);
        else if (global::OdfKit.Internal.OdfStringHelper.StartsWith(formula, '='))
            formula = formula.Substring(1);

        if (global::OdfKit.Internal.OdfStringHelper.StartsWith(formula, '='))
            return formula.Substring(1);
        return formula;
    }
}
