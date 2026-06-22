using System;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 表示工作表中的命名範圍。
/// </summary>
/// <param name="name">命名範圍名稱</param>
/// <param name="cellRangeAddress">ODF 儲存格範圍位址</param>
/// <param name="baseCellAddress">ODF 基準儲存格位址</param>
public sealed class OdfNamedRangeInfo(string name, string cellRangeAddress, string? baseCellAddress)
{
    /// <summary>
    /// 取得命名範圍名稱。
    /// </summary>
    public string Name { get; } = name ?? string.Empty;

    /// <summary>
    /// 取得 ODF 儲存格範圍位址。
    /// </summary>
    public string CellRangeAddress { get; } = cellRangeAddress ?? string.Empty;

    /// <summary>
    /// 取得 ODF 基準儲存格位址。
    /// </summary>
    public string? BaseCellAddress { get; } = baseCellAddress;
}
