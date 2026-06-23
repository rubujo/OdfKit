using OdfKit.Spreadsheet;

namespace OdfKit.Formula;

/// <summary>
/// 提供儲存格空白狀態檢查介面，避免在公式評估時使用反射。
/// </summary>
internal interface IOdfBlankCheckableContext
{
    /// <summary>
    /// 檢查指定儲存格是否為空白（既無值也無公式）。
    /// </summary>
    /// <param name="address">儲存格位址</param>
    /// <returns>若為空白則傳回 true，否則傳回 false</returns>
    bool IsBlank(OdfCellAddress address);
}
