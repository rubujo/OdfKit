using System;
using System.Collections.Generic;
using OdfKit.Compliance;

namespace OdfKit.Spreadsheet;

public partial class SpreadsheetDocument
{
    /// <summary>
    /// Sets a rectangular block of values in the specified worksheet.
    /// 在指定工作表設定一個矩形資料區塊。
    /// </summary>
    /// <param name="sheetName">The worksheet name. / 工作表名稱。</param>
    /// <param name="startAddress">The top-left target cell. / 左上角目標儲存格。</param>
    /// <param name="values">The two-dimensional values to write. / 要寫入的二維資料。</param>
    /// <returns>The updated range. / 已更新的範圍。</returns>
    public OdfCellRange SetValues(string sheetName, OdfCellAddress startAddress, object?[,] values) =>
        RequireSheet(sheetName).SetValues(startAddress, values);

    /// <summary>
    /// Sets a rectangular block of values in the specified worksheet and returns a write report.
    /// 在指定工作表設定矩形資料區塊並傳回寫入報告。
    /// </summary>
    /// <param name="sheetName">The worksheet name. / 工作表名稱。</param>
    /// <param name="startAddress">The top-left target cell. / 左上角目標儲存格。</param>
    /// <param name="values">The two-dimensional values to write. / 要寫入的二維資料。</param>
    /// <param name="options">The range write options. / 範圍寫入選項。</param>
    /// <returns>The range write report. / 範圍寫入報告。</returns>
    public OdfRangeWriteReport SetValues(string sheetName, OdfCellAddress startAddress, object?[,] values, OdfRangeWriteOptions? options) =>
        RequireSheet(sheetName).SetValues(startAddress, values, options);

    /// <summary>
    /// Sets rows of values in the specified worksheet.
    /// 在指定工作表逐列設定資料。
    /// </summary>
    /// <param name="sheetName">The worksheet name. / 工作表名稱。</param>
    /// <param name="startAddress">The top-left target cell. / 左上角目標儲存格。</param>
    /// <param name="rows">The row values to write. / 要寫入的資料列。</param>
    /// <returns>The updated range. / 已更新的範圍。</returns>
    public OdfCellRange SetValues(string sheetName, OdfCellAddress startAddress, IEnumerable<IEnumerable<object?>> rows) =>
        RequireSheet(sheetName).SetValues(startAddress, rows);

    /// <summary>
    /// Sets rows of values in the specified worksheet and returns a write report.
    /// 在指定工作表逐列設定資料並傳回寫入報告。
    /// </summary>
    /// <param name="sheetName">The worksheet name. / 工作表名稱。</param>
    /// <param name="startAddress">The top-left target cell. / 左上角目標儲存格。</param>
    /// <param name="rows">The row values to write. / 要寫入的資料列。</param>
    /// <param name="options">The range write options. / 範圍寫入選項。</param>
    /// <returns>The range write report. / 範圍寫入報告。</returns>
    public OdfRangeWriteReport SetValues(string sheetName, OdfCellAddress startAddress, IEnumerable<IEnumerable<object?>> rows, OdfRangeWriteOptions? options) =>
        RequireSheet(sheetName).SetValues(startAddress, rows, options);

    /// <summary>
    /// Appends rows after the current used range in the specified worksheet.
    /// 將資料列附加到指定工作表目前已使用範圍之後。
    /// </summary>
    /// <param name="sheetName">The worksheet name. / 工作表名稱。</param>
    /// <param name="rows">The row values to append. / 要附加的資料列。</param>
    /// <param name="startColumn">The zero-based start column. / 以 0 為基準的起始欄。</param>
    /// <returns>The appended range. / 已附加的範圍。</returns>
    public OdfCellRange AppendRows(string sheetName, IEnumerable<IEnumerable<object?>> rows, int startColumn = 0) =>
        RequireSheet(sheetName).AppendRows(rows, startColumn);

    /// <summary>
    /// Appends rows after the current used range in the specified worksheet and returns a write report.
    /// 將資料列附加到指定工作表目前已使用範圍之後並傳回寫入報告。
    /// </summary>
    /// <param name="sheetName">The worksheet name. / 工作表名稱。</param>
    /// <param name="rows">The row values to append. / 要附加的資料列。</param>
    /// <param name="startColumn">The zero-based start column. / 以 0 為基準的起始欄。</param>
    /// <param name="options">The range write options. / 範圍寫入選項。</param>
    /// <returns>The range write report. / 範圍寫入報告。</returns>
    public OdfRangeWriteReport AppendRows(string sheetName, IEnumerable<IEnumerable<object?>> rows, int startColumn, OdfRangeWriteOptions? options) =>
        RequireSheet(sheetName).AppendRows(rows, startColumn, options);

    /// <summary>
    /// Gets the used range of the specified worksheet.
    /// 取得指定工作表的已使用範圍。
    /// </summary>
    /// <param name="sheetName">The worksheet name. / 工作表名稱。</param>
    /// <returns>The used range, or <see langword="null"/> when the sheet is empty. / 已使用範圍；工作表為空時傳回 <see langword="null"/>。</returns>
    public OdfCellRange? GetUsedRange(string sheetName) =>
        RequireSheet(sheetName).GetUsedRange();

    private OdfTableSheet RequireSheet(string sheetName)
    {
        if (string.IsNullOrEmpty(sheetName))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_SpreadsheetDocument_WorksheetCannotBeEmpty_2"), nameof(sheetName));
        }

        OdfTableSheet? sheet = FindSheet(sheetName);
        return sheet ?? throw new KeyNotFoundException(OdfLocalizer.GetMessage("Err_SpreadsheetDocument_SheetNamedCannotFound_2", sheetName));
    }
}
