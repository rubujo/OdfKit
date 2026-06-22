using System;
using System.Text;
using OdfKit.Core;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 表示 ODF 試算表中的儲存格位址。
/// </summary>
public readonly struct OdfCellAddress : IEquatable<OdfCellAddress>
{
    /// <summary>
    /// 取得以 0 為基準的列索引。
    /// </summary>
    public int Row { get; }

    /// <summary>
    /// 取得以 0 為基準的欄索引。
    /// </summary>
    public int Column { get; }

    /// <summary>
    /// 取得工作表名稱。
    /// </summary>
    public string? SheetName { get; }

    /// <summary>
    /// 取得一個值，指出列索引是否為絕對參照。
    /// </summary>
    public bool IsRowAbsolute { get; }

    /// <summary>
    /// 取得一個值，指出欄索引是否為絕對參照。
    /// </summary>
    public bool IsColumnAbsolute { get; }

    /// <summary>
    /// 取得一個值，指出工作表名稱是否為絕對參照。
    /// </summary>
    public bool IsSheetAbsolute { get; }

    /// <summary>
    /// 初始化 <see cref="OdfCellAddress"/> 結構的新執行個體。
    /// </summary>
    /// <param name="row">以 0 為基準的列索引</param>
    /// <param name="column">以 0 為基準的欄索引</param>
    /// <param name="sheetName">工作表名稱</param>
    /// <param name="isRowAbsolute">列索引是否為絕對參照</param>
    /// <param name="isColumnAbsolute">欄索引是否為絕對參照</param>
    /// <param name="isSheetAbsolute">工作表名稱是否為絕對參照</param>
    /// <exception cref="ArgumentOutOfRangeException">當列索引或欄索引小於 0 時擲出</exception>
    public OdfCellAddress(int row, int column, string? sheetName = null,
        bool isRowAbsolute = false, bool isColumnAbsolute = false, bool isSheetAbsolute = false)
    {
        if (row < 0)
            throw new ArgumentOutOfRangeException(nameof(row), "Row index must be non-negative.");
        if (column < 0)
            throw new ArgumentOutOfRangeException(nameof(column), "Column index must be non-negative.");

        Row = row;
        Column = column;
        SheetName = sheetName;
        IsRowAbsolute = isRowAbsolute;
        IsColumnAbsolute = isColumnAbsolute;
        IsSheetAbsolute = isSheetAbsolute;
    }

    /// <summary>
    /// 指出目前執行個體是否等於另一個相同類型的執行個體。
    /// </summary>
    /// <param name="other">要比較的另一個執行個體</param>
    /// <returns>如果兩個執行個體相等則為 true，否則為 false</returns>
    public bool Equals(OdfCellAddress other)
    {
        return Row == other.Row &&
               Column == other.Column &&
               string.Equals(SheetName, other.SheetName, StringComparison.OrdinalIgnoreCase) &&
               IsRowAbsolute == other.IsRowAbsolute &&
               IsColumnAbsolute == other.IsColumnAbsolute &&
               IsSheetAbsolute == other.IsSheetAbsolute;
    }

    /// <summary>
    /// 指出此執行個體與指定的物件是否相等。
    /// </summary>
    /// <param name="obj">要比較的物件</param>
    /// <returns>如果指定的物件與目前執行個體相等則為 true，否則為 false</returns>
    public override bool Equals(object? obj) => obj is OdfCellAddress other && Equals(other);

    /// <summary>
    /// 傳回此執行個體的雜湊碼。
    /// </summary>
    /// <returns>32 位元有號整數雜湊碼</returns>
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 23 + Row;
            hash = hash * 23 + Column;
            hash = hash * 23 + (SheetName != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(SheetName) : 0);
            hash = hash * 23 + IsRowAbsolute.GetHashCode();
            hash = hash * 23 + IsColumnAbsolute.GetHashCode();
            hash = hash * 23 + IsSheetAbsolute.GetHashCode();
            return hash;
        }
    }

    /// <summary>
    /// 比較兩個 <see cref="OdfCellAddress"/> 執行個體是否相等。
    /// </summary>
    /// <param name="left">左方的執行個體</param>
    /// <param name="right">右方的執行個體</param>
    /// <returns>如果相等則為 true，否則為 false</returns>
    public static bool operator ==(OdfCellAddress left, OdfCellAddress right) => left.Equals(right);

    /// <summary>
    /// 比較兩個 <see cref="OdfCellAddress"/> 執行個體是否不相等。
    /// </summary>
    /// <param name="left">左方的執行個體</param>
    /// <param name="right">右方的執行個體</param>
    /// <returns>如果不相等則為 true，否則為 false</returns>
    public static bool operator !=(OdfCellAddress left, OdfCellAddress right) => !left.Equals(right);
    #region Address Parsing

    /// <summary>
    /// 解析 Excel 格式的儲存格位址字串。
    /// </summary>
    /// <param name="address">Excel 格式的位址字串</param>
    /// <returns>解析後的 <see cref="OdfCellAddress"/> 執行個體</returns>
    public static OdfCellAddress ParseExcel(string address) => Parse(address.AsSpan(), false);

    /// <summary>
    /// 解析 ODF 格式的儲存格位址字串。
    /// </summary>
    /// <param name="address">ODF 格式的位址字串</param>
    /// <returns>解析後的 <see cref="OdfCellAddress"/> 執行個體</returns>
    public static OdfCellAddress ParseOdf(string address) => Parse(address.AsSpan(), true);

    /// <summary>
    /// 嘗試解析儲存格位址字串。
    /// </summary>
    /// <param name="value">要解析的位址字串</param>
    /// <param name="address">解析成功時傳回的儲存格位址</param>
    /// <returns>如果解析成功則為 true，否則為 false</returns>
    public static bool TryParse(string value, out OdfCellAddress address)
    {
        try
        {
            address = ParseExcel(value);
            return true;
        }
        catch
        {
            try
            {
                address = ParseOdf(value);
                return true;
            }
            catch
            {
                address = default;
                return false;
            }
        }
    }

    /// <summary>
    /// 從字元範圍解析儲存格位址。
    /// </summary>
    /// <param name="span">包含儲存格位址的唯讀字元範圍</param>
    /// <param name="isOdfStyle">是否使用 ODF 格式樣式</param>
    /// <returns>解析後的 <see cref="OdfCellAddress"/> 結構</returns>
    /// <exception cref="FormatException">當字串格式無效時擲出</exception>
    public static OdfCellAddress Parse(ReadOnlySpan<char> span, bool isOdfStyle)
    {
        span = span.Trim();
        if (span.IsEmpty)
            throw new FormatException("Address string cannot be empty.");

        string? sheetName = null;
        bool isSheetAbsolute = false;
        bool isColAbsolute = false;
        bool isRowAbsolute = false;

        // 1. 分離工作表名稱與儲存格座標
        int sepIndex = -1;
        if (isOdfStyle)
        {
            // ODF 樣式：本地工作表具有點前綴，例如「.A1」。
            // 限定工作表的參照具有 'Sheet1.A1' 或 'Sheet 1'.A1 形式
            // 如果以「.」開頭且不包含其他「.」，則表示沒有工作表名稱。
            if (span.StartsWith(".") && span.Slice(1).IndexOf('.') == -1)
            {
                sepIndex = 0;
            }
            else
            {
                // 尋找最後一個點「.」（處理含空格之工作表的單引號）
                bool inQuotes = false;
                for (int j = span.Length - 1; j >= 0; j--)
                {
                    char c = span[j];
                    if (c == '\'')
                        inQuotes = !inQuotes;
                    if (!inQuotes && c == '.')
                    {
                        sepIndex = j;
                        break;
                    }
                }
            }
        }
        else
        {
            // Excel 樣式：分隔符號為「!」。尋找單引號外部最後一個「!」
            bool inQuotes = false;
            for (int j = span.Length - 1; j >= 0; j--)
            {
                char c = span[j];
                if (c == '\'')
                    inQuotes = !inQuotes;
                if (!inQuotes && c == '!')
                {
                    sepIndex = j;
                    break;
                }
            }
        }

        ReadOnlySpan<char> cellSpan = span;
        if (sepIndex != -1)
        {
            ReadOnlySpan<char> sheetSpan = span.Slice(0, sepIndex);
            cellSpan = span.Slice(sepIndex + 1);

            // 處理工作表名稱
            if (!sheetSpan.IsEmpty)
            {
                // 偵測絕對工作表參照（帶有「$」前綴）
                if (sheetSpan.StartsWith("$"))
                {
                    isSheetAbsolute = true;
                    sheetSpan = sheetSpan.Slice(1);
                }

                // 若有單引號則予以剝除
                if (sheetSpan.StartsWith("'") && sheetSpan.EndsWith("'"))
                {
                    sheetSpan = sheetSpan.Slice(1, sheetSpan.Length - 2);
                    // 將雙單引號 '' 還原為單引號
                    sheetName = sheetSpan.ToString().Replace("''", "'");
                }
                else
                {
                    sheetName = sheetSpan.ToString();
                }
            }
        }

        // 2. 從 cellSpan 解析資料行與資料列組件
        int i = 0;
        if (i < cellSpan.Length && cellSpan[i] == '$')
        {
            isColAbsolute = true;
            i++;
        }

        int colStart = i;
        while (i < cellSpan.Length && char.IsLetter(cellSpan[i]))
        {
            i++;
        }
        if (i == colStart)
            throw new FormatException("Invalid cell address: missing column letters.");

        ReadOnlySpan<char> colLetters = cellSpan.Slice(colStart, i - colStart);

        if (i < cellSpan.Length && cellSpan[i] == '$')
        {
            isRowAbsolute = true;
            i++;
        }

        int rowStart = i;
        while (i < cellSpan.Length && char.IsDigit(cellSpan[i]))
        {
            i++;
        }
        if (i == rowStart || i < cellSpan.Length)
            throw new FormatException("Invalid cell address: row index must be numeric digits and terminate the string.");

        ReadOnlySpan<char> rowDigits = cellSpan.Slice(rowStart, i - rowStart);

        // 將資料行字母 (A-Z, AA-ZZ) 轉換為以 0 為起始的索引
        int column = 0;
        for (int k = 0; k < colLetters.Length; k++)
        {
            char c = colLetters[k];
            int val = char.ToUpperInvariant(c) - 'A' + 1;
            column = column * 26 + val;
        }
        column--; // 0-based

        // 將資料列數字轉換為以 0 為起始的索引（文字中是以 1 為起始）
#if NET10_0_OR_GREATER
        int row = int.Parse(rowDigits) - 1;
#else
        int row = int.Parse(rowDigits.ToString()) - 1;
#endif

        return new OdfCellAddress(row, column, sheetName, isRowAbsolute, isColAbsolute, isSheetAbsolute);
    }

    #endregion

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
    /// <param name="insertColIndex">插入欄的索引位置</param>
    /// <param name="colCount">插入的欄數（負數表示刪除）</param>
    /// <returns>調整後的新 <see cref="OdfCellAddress"/> 執行個體</returns>
    public OdfCellAddress ShiftStructural(int insertRowIndex, int rowCount, int insertColIndex, int colCount)
    {
        int newRow = Row;
        int newCol = Column;

        // 資料列位移（刪除時 rowCount 可為負數）
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

        // 資料行位移（刪除時 colCount 可為負數）
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
