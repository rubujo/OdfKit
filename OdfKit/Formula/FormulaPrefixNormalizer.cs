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
            return formula.Substring(6);
        if (formula.StartsWith("of:=", StringComparison.OrdinalIgnoreCase))
            return formula.Substring(4);
        if (formula.StartsWith("="))
            return formula.Substring(1);
        return formula;
    }
}
