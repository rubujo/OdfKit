using System;
using System.Text;
using OdfKit.Core;

namespace OdfKit.Spreadsheet;

public readonly partial struct OdfCellAddress
{
    #region Address Formatting & Structural Shift

    /// <summary>
    /// 將此儲存格位址轉換為 Excel 格式的字串。
    /// </summary>
    /// <returns>Excel 格式的位址字串</returns>
    public string ToExcelString()
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(SheetName))
        {
            bool needsQuotes = SheetName!.Contains(" ") || SheetName.Contains("'") || SheetName.Contains("-") || SheetName.Contains("!");
            if (needsQuotes)
                sb.Append("'").Append(SheetName.Replace("'", "''")).Append("'!");
            else
                sb.Append(SheetName).Append("!");
        }
        if (IsColumnAbsolute)
            sb.Append("$");
        sb.Append(IndexToColumnName(Column));
        if (IsRowAbsolute)
            sb.Append("$");
        sb.Append(Row + 1);
        return sb.ToString();
    }

    /// <summary>
    /// 將此儲存格位址轉換為 ODF 格式的字串。
    /// </summary>
    /// <param name="includeBrackets">是否包含中括號</param>
    /// <returns>ODF 格式的位址字串</returns>
    public string ToOdfString(bool includeBrackets = false)
    {
        var sb = new StringBuilder();
        if (includeBrackets)
            sb.Append("[");

        if (!string.IsNullOrEmpty(SheetName))
        {
            if (IsSheetAbsolute)
                sb.Append("$");
            bool needsQuotes = SheetName!.Contains(" ") || SheetName.Contains("'") || SheetName.Contains("-") || SheetName.Contains(".");
            if (needsQuotes)
                sb.Append("'").Append(SheetName.Replace("'", "''")).Append("'");
            else
                sb.Append(SheetName);
            sb.Append(".");
        }
        else
        {
            sb.Append("."); // Local reference prefix
        }

        if (IsColumnAbsolute)
            sb.Append("$");
        sb.Append(IndexToColumnName(Column));
        if (IsRowAbsolute)
            sb.Append("$");
        sb.Append(Row + 1);

        if (includeBrackets)
            sb.Append("]");
        return sb.ToString();
    }

    private static string IndexToColumnName(int columnIndex)
    {
        int temp = columnIndex + 1;
        var name = new StringBuilder();
        while (temp > 0)
        {
            int modulo = (temp - 1) % 26;
            name.Insert(0, (char)('A' + modulo));
            temp = (temp - modulo) / 26;
        }
        return name.ToString();
    }

    /// <summary>
    /// 根據列或欄的插入或刪除，調整儲存格位址。
    /// </summary>
    /// <param name="insertRowIndex">插入列的索引位置</param>
    /// <param name="rowCount">插入的列數（負數表示刪除）</param>
    /// <param name="insertColIndex">插入欄 of 索引位置</param>
    /// <param name="colCount">插入的欄數（負數表示刪除）</param>
    /// <returns>調整後的新 <see cref="OdfCellAddress"/> 執行個體</returns>
    public OdfCellAddress ShiftStructural(int insertRowIndex, int rowCount, int insertColIndex, int colCount)
    {
        int newRow = Row;
        int newCol = Column;

        // Row shifts (rowCount can be negative for deletions)
        if (rowCount != 0)
        {
            if (rowCount > 0) // Insertion
            {
                if (Row >= insertRowIndex)
                    newRow += rowCount;
            }
            else // Deletion
            {
                int deletedCount = -rowCount;
                if (Row >= insertRowIndex + deletedCount)
                    newRow -= deletedCount;
                else if (Row >= insertRowIndex)
                    newRow = insertRowIndex; // Clamp to start of deleted region
            }
        }

        // Column shifts (colCount can be negative for deletions)
        if (colCount != 0)
        {
            if (colCount > 0) // Insertion
            {
                if (Column >= insertColIndex)
                    newCol += colCount;
            }
            else // Deletion
            {
                int deletedCount = -colCount;
                if (Column >= insertColIndex + deletedCount)
                    newCol -= deletedCount;
                else if (Column >= insertColIndex)
                    newCol = insertColIndex; // Clamp to start of deleted region
            }
        }

        return new OdfCellAddress(newRow, newCol, SheetName, IsRowAbsolute, IsColumnAbsolute, IsSheetAbsolute);
    }

    /// <summary>
    /// 傳回代表目前物件的字串。
    /// </summary>
    /// <returns>代表目前物件的字串</returns>
    public override string ToString() => ToExcelString();

    #endregion
}
