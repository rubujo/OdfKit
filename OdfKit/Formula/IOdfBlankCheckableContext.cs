using OdfKit.Spreadsheet;

namespace OdfKit.Formula;

/// <summary>
/// Checks cell blank states without reflection during formula evaluation.
/// 提供儲存格空白狀態檢查介面，避免在公式評估時使用反射。
/// </summary>
internal interface IOdfBlankCheckableContext
{
    /// <summary>
    /// Checks whether the specified cell is blank, with neither value nor formula.
    /// 檢查指定儲存格是否為空白（既無值也無公式）。
    /// </summary>
    /// <param name="address">The cell address. / 儲存格位址。</param>
    /// <returns><see langword="true"/> if the cell is blank; otherwise, <see langword="false"/>. / 若為空白則傳回 <see langword="true"/>，否則傳回 <see langword="false"/>。</returns>
    bool IsBlank(OdfCellAddress address);
}
