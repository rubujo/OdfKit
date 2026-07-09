using System;
using System.Collections.Generic;
using System.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Styles;

namespace OdfKit.Spreadsheet;

public partial class OdfTableSheet
{
    /// <summary>
    /// Sets a rectangular block of values starting at the specified cell.
    /// 從指定儲存格開始設定一個矩形資料區塊。
    /// </summary>
    /// <returns>The updated range. / 已更新的範圍。</returns>
    public OdfCellRange SetValues(OdfCellAddress startAddress, object?[,] values) =>
        SetValues(startAddress, values, OdfRangeWriteOptions.Default).Range;

    /// <summary>
    /// Sets a rectangular block of values and returns a write report.
    /// 設定一個矩形資料區塊並傳回寫入報告。
    /// </summary>
    /// <returns>The range write report. / 範圍寫入報告。</returns>
    public OdfRangeWriteReport SetValues(OdfCellAddress startAddress, object?[,] values, OdfRangeWriteOptions? options)
    {
        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        return SetValues(startAddress, ToRows(values), options);
    }

    /// <summary>
    /// Sets rows of values starting at the specified cell.
    /// 從指定儲存格開始逐列設定資料。
    /// </summary>
    /// <param name="startAddress">The top-left target cell. / 左上角目標儲存格。</param>
    /// <param name="rows">The row values to write. / 要寫入的資料列。</param>
    /// <returns>The updated range. / 已更新的範圍。</returns>
    public OdfCellRange SetValues(OdfCellAddress startAddress, IEnumerable<IEnumerable<object?>> rows) =>
        SetValues(startAddress, rows, OdfRangeWriteOptions.Default).Range;

    /// <summary>
    /// Sets rows of values starting at the specified cell and returns a write report.
    /// 從指定儲存格開始逐列設定資料並傳回寫入報告。
    /// </summary>
    /// <param name="startAddress">The top-left target cell. / 左上角目標儲存格。</param>
    /// <param name="rows">The row values to write. / 要寫入的資料列。</param>
    /// <param name="options">The range write options. / 範圍寫入選項。</param>
    /// <returns>The range write report. / 範圍寫入報告。</returns>
    public OdfRangeWriteReport SetValues(OdfCellAddress startAddress, IEnumerable<IEnumerable<object?>> rows, OdfRangeWriteOptions? options)
    {
        if (rows is null)
        {
            throw new ArgumentNullException(nameof(rows));
        }

        ValidateStartAddress(startAddress);
        options ??= OdfRangeWriteOptions.Default;

        List<List<object?>> snapshot = rows.Select(row => row?.ToList() ?? new List<object?>()).ToList();
        int maxColumns = snapshot.Count == 0 ? 0 : snapshot.Max(row => row.Count);
        var report = new OdfRangeWriteReport
        {
            Range = new OdfCellRange(
                new OdfCellAddress(startAddress.Row, startAddress.Column, Name),
                new OdfCellAddress(startAddress.Row + Math.Max(0, snapshot.Count - 1), startAddress.Column + Math.Max(0, maxColumns - 1), Name))
        };

        if (snapshot.Count == 0 || maxColumns == 0)
        {
            report.Warnings.Add("RANGE0001");
            return report;
        }

        for (int row = 0; row < snapshot.Count; row++)
        {
            for (int column = 0; column < snapshot[row].Count; column++)
            {
                OdfCell cell = GetCell(startAddress.Row + row, startAddress.Column + column);
                string? styleName = cell.StyleName;
                cell.CellValue = snapshot[row][column];
                if (options.PreserveStyles)
                {
                    cell.StyleName = styleName;
                }

                report.WrittenCellCount++;
            }

            if (options.ClearTrailingCells)
            {
                for (int column = snapshot[row].Count; column < maxColumns; column++)
                {
                    GetCell(startAddress.Row + row, startAddress.Column + column).CellValue = null;
                    report.ClearedCellCount++;
                }
            }
            else
            {
                report.SkippedCellCount += maxColumns - snapshot[row].Count;
            }
        }

        return report;
    }

    /// <summary>
    /// Appends rows after the current used range.
    /// 將資料列附加到目前已使用範圍之後。
    /// </summary>
    /// <param name="rows">The row values to append. / 要附加的資料列。</param>
    /// <returns>The appended range. / 已附加的範圍。</returns>
    public OdfCellRange AppendRows(IEnumerable<IEnumerable<object?>> rows) => AppendRows(rows, 0);

    /// <summary>
    /// Convenience overload that uses default values for remaining parameters.
    /// 便利多載：其餘參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public OdfCellRange AppendRows(IEnumerable<IEnumerable<object?>> rows, int startColumn) =>
        AppendRows(rows, startColumn, OdfRangeWriteOptions.Default).Range;

    /// <summary>
    /// Appends rows after the current used range and returns a write report.
    /// 將資料列附加到目前已使用範圍之後並傳回寫入報告。
    /// </summary>
    /// <param name="rows">The row values to append. / 要附加的資料列。</param>
    /// <param name="startColumn">The zero-based start column. / 以 0 為基準的起始欄。</param>
    /// <param name="options">The range write options. / 範圍寫入選項。</param>
    /// <returns>The range write report. / 範圍寫入報告。</returns>
    public OdfRangeWriteReport AppendRows(IEnumerable<IEnumerable<object?>> rows, int startColumn, OdfRangeWriteOptions? options)
    {
        if (startColumn < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startColumn), OdfLocalizer.GetMessage("Err_OdfCellAddress_ColumnIndexNonNegative"));
        }

        options ??= OdfRangeWriteOptions.Default;
        if (options.AppendKeyColumn.HasValue && options.AppendKeyColumn.Value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), OdfLocalizer.GetMessage("Err_OdfCellAddress_ColumnIndexNonNegative"));
        }

        return SetValues(new OdfCellAddress(FindAppendRow(options.AppendKeyColumn), startColumn, Name), rows, options);
    }

    /// <summary>
    /// Updates rows inside the specified range from top to bottom.
    /// 由上而下更新指定範圍內的資料列。
    /// </summary>
    /// <param name="range">The target range. / 目標範圍。</param>
    /// <param name="rows">The row values to write. / 要寫入的資料列。</param>
    /// <returns>The updated range. / 已更新的範圍。</returns>
    public OdfCellRange UpdateRows(OdfCellRange range, IEnumerable<IEnumerable<object?>> rows) =>
        SetValues(range.StartAddress, rows);

    /// <summary>
    /// Updates rows inside the specified range and returns a write report.
    /// 更新指定範圍內的資料列並傳回寫入報告。
    /// </summary>
    /// <param name="range">The target range. / 目標範圍。</param>
    /// <param name="rows">The row values to write. / 要寫入的資料列。</param>
    /// <param name="options">The range write options. / 範圍寫入選項。</param>
    /// <returns>The range write report. / 範圍寫入報告。</returns>
    public OdfRangeWriteReport UpdateRows(OdfCellRange range, IEnumerable<IEnumerable<object?>> rows, OdfRangeWriteOptions? options) =>
        SetValues(range.StartAddress, rows, options);

    /// <summary>
    /// Applies a cell style name to every cell in the specified range.
    /// 將儲存格樣式名稱套用至指定範圍內的每個儲存格。
    /// </summary>
    /// <param name="range">The target range. / 目標範圍。</param>
    /// <param name="styleName">The style name to apply. / 要套用的樣式名稱。</param>
    /// <returns>The current worksheet. / 目前工作表。</returns>
    public OdfTableSheet ApplyStyle(OdfCellRange range, string? styleName)
    {
        foreach (OdfCell cell in GetRange(range))
        {
            cell.StyleName = styleName;
        }

        return this;
    }

    /// <summary>
    /// Gets the bounding range that contains all used cells in this worksheet.
    /// 取得此工作表所有已使用儲存格的外接範圍。
    /// </summary>
    /// <returns>The used range, or <see langword="null"/> when the sheet is empty. / 已使用範圍；工作表為空時傳回 <see langword="null"/>。</returns>
    public OdfCellRange? GetUsedRange()
    {
        int minRow = int.MaxValue;
        int minColumn = int.MaxValue;
        int maxRow = -1;
        int maxColumn = -1;
        foreach (OdfCell cell in GetUsedCells())
        {
            minRow = Math.Min(minRow, cell.Row);
            minColumn = Math.Min(minColumn, cell.Column);
            maxRow = Math.Max(maxRow, cell.Row);
            maxColumn = Math.Max(maxColumn, cell.Column);
        }

        return maxRow < 0
            ? null
            : new OdfCellRange(new OdfCellAddress(minRow, minColumn, Name), new OdfCellAddress(maxRow, maxColumn, Name));
    }

    private static IEnumerable<IEnumerable<object?>> ToRows(object?[,] values)
    {
        int rowCount = values.GetLength(0);
        int columnCount = values.GetLength(1);
        for (int row = 0; row < rowCount; row++)
        {
            var rowValues = new object?[columnCount];
            for (int column = 0; column < columnCount; column++)
            {
                rowValues[column] = values[row, column];
            }

            yield return rowValues;
        }
    }

    private int FindAppendRow(int? keyColumn)
    {
        if (!keyColumn.HasValue)
        {
            OdfCellRange? usedRange = GetUsedRange();
            return usedRange is null ? 0 : Math.Max(usedRange.Value.StartAddress.Row, usedRange.Value.EndAddress.Row) + 1;
        }

        int maxRow = -1;
        foreach (OdfCell cell in GetUsedCells())
        {
            if (cell.Column == keyColumn.Value && cell.CellValue is not null)
            {
                maxRow = Math.Max(maxRow, cell.Row);
            }
        }

        return maxRow + 1;
    }

    private static void ValidateStartAddress(OdfCellAddress startAddress)
    {
        if (startAddress.Row < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startAddress), OdfLocalizer.GetMessage("Err_OdfCellAddress_RowIndexNonNegative"));
        }

        if (startAddress.Column < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startAddress), OdfLocalizer.GetMessage("Err_OdfCellAddress_ColumnIndexNonNegative"));
        }
    }
}
