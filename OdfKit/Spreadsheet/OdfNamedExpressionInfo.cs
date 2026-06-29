using System;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Represents a named expression in a worksheet.
/// 表示工作表中的具名運算式。
/// </summary>
/// <param name="name">The named expression name. / 具名運算式名稱。</param>
/// <param name="expression">The ODF expression. / ODF 運算式。</param>
/// <param name="baseCellAddress">The ODF base cell address. / ODF 基準儲存格位址。</param>
public sealed class OdfNamedExpressionInfo(string name, string expression, string? baseCellAddress)
{
    /// <summary>
    /// Gets the named expression name.
    /// 取得具名運算式名稱。
    /// </summary>
    public string Name { get; } = name ?? string.Empty;

    /// <summary>
    /// Gets the ODF expression.
    /// 取得 ODF 運算式。
    /// </summary>
    public string Expression { get; } = expression ?? string.Empty;

    /// <summary>
    /// Gets the ODF base cell address.
    /// 取得 ODF 基準儲存格位址。
    /// </summary>
    public string? BaseCellAddress { get; } = baseCellAddress;
}
