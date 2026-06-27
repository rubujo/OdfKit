namespace OdfKit.Spreadsheet;

/// <summary>
/// 表示試算表中一個含公式的儲存格。
/// </summary>
/// <param name="sheetName">工作表名稱</param>
/// <param name="address">儲存格位址</param>
/// <param name="cell">儲存格 facade</param>
/// <param name="formula">目前的 <c>table:formula</c> 文字</param>
public sealed class OdfFormulaCellInfo(string sheetName, OdfCellAddress address, OdfCell cell, string formula)
{
    /// <summary>
    /// 取得工作表名稱。
    /// </summary>
    public string SheetName { get; } = sheetName;

    /// <summary>
    /// 取得儲存格位址。
    /// </summary>
    public OdfCellAddress Address { get; } = address;

    /// <summary>
    /// 取得儲存格 facade。
    /// </summary>
    public OdfCell Cell { get; } = cell;

    /// <summary>
    /// 取得目前的 <c>table:formula</c> 文字。
    /// </summary>
    public string Formula { get; } = formula;

    /// <summary>
    /// 取得含工作表名稱的 Excel 樣式位址。
    /// </summary>
    public string ExcelAddress => Address.ToExcelString();
}
