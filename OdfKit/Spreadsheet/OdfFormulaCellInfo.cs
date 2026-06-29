namespace OdfKit.Spreadsheet;

/// <summary>
/// Represents a spreadsheet cell that contains a formula.
/// 表示試算表中一個含公式的儲存格。
/// </summary>
/// <param name="sheetName">The sheet name. / 工作表名稱。</param>
/// <param name="address">The cell address. / 儲存格位址。</param>
/// <param name="cell">The cell facade. / 儲存格 facade。</param>
/// <param name="formula">The current <c>table:formula</c> text. / 目前的 <c>table:formula</c> 文字。</param>
public sealed class OdfFormulaCellInfo(string sheetName, OdfCellAddress address, OdfCell cell, string formula)
{
    /// <summary>
    /// Gets the sheet name.
    /// 取得工作表名稱。
    /// </summary>
    public string SheetName { get; } = sheetName;

    /// <summary>
    /// Gets the cell address.
    /// 取得儲存格位址。
    /// </summary>
    public OdfCellAddress Address { get; } = address;

    /// <summary>
    /// Gets the cell facade.
    /// 取得儲存格 facade。
    /// </summary>
    public OdfCell Cell { get; } = cell;

    /// <summary>
    /// Gets the current <c>table:formula</c> text.
    /// 取得目前的 <c>table:formula</c> 文字。
    /// </summary>
    public string Formula { get; } = formula;

    /// <summary>
    /// Gets the Excel-style address with the sheet name.
    /// 取得含工作表名稱的 Excel 樣式位址。
    /// </summary>
    public string ExcelAddress => Address.ToExcelString();
}
