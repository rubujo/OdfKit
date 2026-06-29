using System;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Represents a named range in a worksheet.
/// 表示工作表中的命名範圍。
/// </summary>
/// <param name="name">The named range name. / 命名範圍名稱。</param>
/// <param name="cellRangeAddress">The ODF cell range address. / ODF 儲存格範圍位址。</param>
/// <param name="baseCellAddress">The ODF base cell address. / ODF 基準儲存格位址。</param>
public sealed class OdfNamedRangeInfo(string name, string cellRangeAddress, string? baseCellAddress)
{
    /// <summary>
    /// Gets the named range name.
    /// 取得命名範圍名稱。
    /// </summary>
    public string Name { get; } = name ?? string.Empty;

    /// <summary>
    /// Gets the ODF cell range address.
    /// 取得 ODF 儲存格範圍位址。
    /// </summary>
    public string CellRangeAddress { get; } = cellRangeAddress ?? string.Empty;

    /// <summary>
    /// Gets the ODF base cell address.
    /// 取得 ODF 基準儲存格位址。
    /// </summary>
    public string? BaseCellAddress { get; } = baseCellAddress;
}
