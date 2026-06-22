using System;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 表示工作表中的具名運算式。
/// </summary>
/// <param name="name">具名運算式名稱</param>
/// <param name="expression">ODF 運算式</param>
/// <param name="baseCellAddress">ODF 基準儲存格位址</param>
public sealed class OdfNamedExpressionInfo(string name, string expression, string? baseCellAddress)
{
    /// <summary>
    /// 取得具名運算式名稱。
    /// </summary>
    public string Name { get; } = name ?? string.Empty;

    /// <summary>
    /// 取得 ODF 運算式。
    /// </summary>
    public string Expression { get; } = expression ?? string.Empty;

    /// <summary>
    /// 取得 ODF 基準儲存格位址。
    /// </summary>
    public string? BaseCellAddress { get; } = baseCellAddress;
}
