using System;
using System.Text;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Represents a cell range in an ODF spreadsheet.
/// 表示 ODF 試算表中的儲存格範圍。
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="OdfCellRange"/> struct.
/// 初始化 <see cref="OdfCellRange"/> 結構的新執行個體。
/// </remarks>
/// <param name="start">The start cell address. / 起始儲存格位址。</param>
/// <param name="end">The end cell address. / 結束儲存格位址。</param>
public readonly struct OdfCellRange(OdfCellAddress start, OdfCellAddress end) : IEquatable<OdfCellRange>
{
    /// <summary>
    /// Gets the start cell address of the range.
    /// 取得範圍的起始儲存格位址。
    /// </summary>
    public OdfCellAddress StartAddress { get; } = start;

    /// <summary>
    /// Gets the end cell address of the range.
    /// 取得範圍的結束儲存格位址。
    /// </summary>
    public OdfCellAddress EndAddress { get; } = end;

    /// <summary>
    /// Initializes a new instance of the <see cref="OdfCellRange"/> struct.
    /// 初始化 <see cref="OdfCellRange"/> 結構的新執行個體。
    /// </summary>
    /// <param name="startRow">The start row index. / 起始列索引。</param>
    /// <param name="startCol">The start column index. / 起始欄索引。</param>
    /// <param name="endRow">The end row index. / 結束列索引。</param>
    /// <param name="endCol">The end column index. / 結束欄索引。</param>
    /// <param name="sheetName">The sheet name. / 工作表名稱。</param>
    public OdfCellRange(int startRow, int startCol, int endRow, int endCol, string? sheetName = null)
        : this(new OdfCellAddress(startRow, startCol, sheetName), new OdfCellAddress(endRow, endCol, sheetName))
    {
    }

    /// <summary>
    /// Indicates whether the current instance is equal to another instance of the same type.
    /// 指出目前執行個體是否等於另一個相同類型的執行個體。
    /// </summary>
    /// <param name="other">The other instance to compare. / 要比較的另一個執行個體。</param>
    /// <returns><see langword="true"/> if the two instances are equal; otherwise, <see langword="false"/>. / 如果兩個執行個體相等則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool Equals(OdfCellRange other) =>
        StartAddress == other.StartAddress && EndAddress == other.EndAddress;

    /// <summary>
    /// Indicates whether this instance and the specified object are equal.
    /// 指出此執行個體與指定的物件是否相等。
    /// </summary>
    /// <param name="obj">The object to compare. / 要比較的物件。</param>
    /// <returns><see langword="true"/> if the specified object equals the current instance; otherwise, <see langword="false"/>. / 如果指定的物件與目前執行個體相等則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public override bool Equals(object? obj) => obj is OdfCellRange other && Equals(other);

    /// <summary>
    /// Returns the hash code for this instance.
    /// 傳回此執行個體的雜湊碼。
    /// </summary>
    /// <returns>A 32-bit signed integer hash code. / 32 位元有號整數雜湊碼。</returns>
    public override int GetHashCode()
    {
        unchecked
        {
            return (StartAddress.GetHashCode() * 397) ^ EndAddress.GetHashCode();
        }
    }

    /// <summary>
    /// Compares two <see cref="OdfCellRange"/> instances for equality.
    /// 比較兩個 <see cref="OdfCellRange"/> 執行個體是否相等。
    /// </summary>
    /// <param name="left">The left instance. / 左方的執行個體。</param>
    /// <param name="right">The right instance. / 右方的執行個體。</param>
    /// <returns><see langword="true"/> if the instances are equal; otherwise, <see langword="false"/>. / 如果相等則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public static bool operator ==(OdfCellRange left, OdfCellRange right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="OdfCellRange"/> instances for inequality.
    /// 比較兩個 <see cref="OdfCellRange"/> 執行個體是否不相等。
    /// </summary>
    /// <param name="left">The left instance. / 左方的執行個體。</param>
    /// <param name="right">The right instance. / 右方的執行個體。</param>
    /// <returns><see langword="true"/> if the instances are not equal; otherwise, <see langword="false"/>. / 如果不相等則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public static bool operator !=(OdfCellRange left, OdfCellRange right) => !left.Equals(right);

    /// <summary>
    /// Attempts to parse a cell range string.
    /// 嘗試解析儲存格範圍字串。
    /// </summary>
    /// <param name="value">The range string to parse. / 要解析的範圍字串。</param>
    /// <param name="range">The cell range returned when parsing succeeds. / 解析成功時傳回的儲存格範圍。</param>
    /// <returns><see langword="true"/> if parsing succeeds; otherwise, <see langword="false"/>. / 如果解析成功則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public static bool TryParse(string value, out OdfCellRange range)
    {
        try
        {
            range = ParseExcel(value);
            return true;
        }
        catch
        {
            try
            {
                range = ParseOdf(value);
                return true;
            }
            catch
            {
                range = default;
                return false;
            }
        }
    }

    /// <summary>
    /// Parses an Excel-style cell range string.
    /// 解析 Excel 格式的儲存格範圍字串。
    /// </summary>
    /// <param name="rangeStr">The Excel-style range string. / Excel 格式的範圍字串。</param>
    /// <returns>The parsed <see cref="OdfCellRange"/> instance. / 解析後的 <see cref="OdfCellRange"/> 執行個體。</returns>
    public static OdfCellRange ParseExcel(string rangeStr)
    {
        int colonIdx = FindUnquotedColon(rangeStr);
        if (colonIdx == -1)
        {
            var addr = OdfCellAddress.ParseExcel(rangeStr);
            return new OdfCellRange(addr, addr);
        }

        var start = OdfCellAddress.ParseExcel(rangeStr.Substring(0, colonIdx));
        var end = OdfCellAddress.ParseExcel(rangeStr.Substring(colonIdx + 1));

        // 若結束座標缺少工作表名稱，則予以傳遞
        if (end.SheetName is null && start.SheetName is not null)
        {
            end = new OdfCellAddress(end.Row, end.Column, start.SheetName,
                end.IsRowAbsolute, end.IsColumnAbsolute, start.IsSheetAbsolute);
        }

        return new OdfCellRange(start, end);
    }

    /// <summary>
    /// Parses an ODF-style cell range string.
    /// 解析 ODF 格式的儲存格範圍字串。
    /// </summary>
    /// <param name="rangeStr">The ODF-style range string. / ODF 格式的範圍字串。</param>
    /// <returns>The parsed <see cref="OdfCellRange"/> instance. / 解析後的 <see cref="OdfCellRange"/> 執行個體。</returns>
    public static OdfCellRange ParseOdf(string rangeStr)
    {
        int colonIdx = FindUnquotedColon(rangeStr);
        if (colonIdx == -1)
        {
            var addr = OdfCellAddress.ParseOdf(rangeStr);
            return new OdfCellRange(addr, addr);
        }

        var start = OdfCellAddress.ParseOdf(rangeStr.Substring(0, colonIdx));
        var end = OdfCellAddress.ParseOdf(rangeStr.Substring(colonIdx + 1));

        if (end.SheetName is null && start.SheetName is not null)
        {
            end = new OdfCellAddress(end.Row, end.Column, start.SheetName,
                end.IsRowAbsolute, end.IsColumnAbsolute, start.IsSheetAbsolute);
        }

        return new OdfCellRange(start, end);
    }

    private static int FindUnquotedColon(string str)
    {
        bool inQuotes = false;
        for (int i = 0; i < str.Length; i++)
        {
            char c = str[i];
            if (c == '\'')
                inQuotes = !inQuotes;
            if (!inQuotes && c == ':')
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Determines whether this range contains the specified cell address.
    /// 判斷此範圍是否包含指定的儲存格位址。
    /// </summary>
    /// <param name="address">The cell address to check. / 要檢查的儲存格位址。</param>
    /// <returns><see langword="true"/> if the range contains the address; otherwise, <see langword="false"/>. / 如果包含則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool Contains(OdfCellAddress address)
    {
        // 檢查工作表是否等價
        if (!string.Equals(StartAddress.SheetName, address.SheetName, StringComparison.OrdinalIgnoreCase))
            return false;

        int minRow = Math.Min(StartAddress.Row, EndAddress.Row);
        int maxRow = Math.Max(StartAddress.Row, EndAddress.Row);
        int minCol = Math.Min(StartAddress.Column, EndAddress.Column);
        int maxCol = Math.Max(StartAddress.Column, EndAddress.Column);

        return address.Row >= minRow && address.Row <= maxRow &&
               address.Column >= minCol && address.Column <= maxCol;
    }

    /// <summary>
    /// Determines whether this range intersects another range.
    /// 判斷此範圍是否與另一個範圍相交。
    /// </summary>
    /// <param name="other">The other range to check. / 要檢查的另一個範圍。</param>
    /// <returns><see langword="true"/> if the ranges intersect; otherwise, <see langword="false"/>. / 如果相交則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool Intersects(OdfCellRange other)
    {
        if (!string.Equals(StartAddress.SheetName, other.StartAddress.SheetName, StringComparison.OrdinalIgnoreCase))
            return false;

        int minRow1 = Math.Min(StartAddress.Row, EndAddress.Row);
        int maxRow1 = Math.Max(StartAddress.Row, EndAddress.Row);
        int minCol1 = Math.Min(StartAddress.Column, EndAddress.Column);
        int maxCol1 = Math.Max(StartAddress.Column, EndAddress.Column);

        int minRow2 = Math.Min(other.StartAddress.Row, other.EndAddress.Row);
        int maxRow2 = Math.Max(other.StartAddress.Row, other.EndAddress.Row);
        int minCol2 = Math.Min(other.StartAddress.Column, other.EndAddress.Column);
        int maxCol2 = Math.Max(other.StartAddress.Column, other.EndAddress.Column);

        return minRow1 <= maxRow2 && maxRow1 >= minRow2 &&
               minCol1 <= maxCol2 && maxCol1 >= minCol2;
    }

    /// <summary>
    /// Calculates the intersection between this range and another range.
    /// 計算此範圍與另一個範圍的相交區域。
    /// </summary>
    /// <param name="other">The other cell range. / 另一個儲存格範圍。</param>
    /// <returns>The intersecting <see cref="OdfCellRange"/>, or <see langword="null"/> if the ranges do not intersect. / 相交的 <see cref="OdfCellRange"/>，如果不相交則為 <see langword="null"/>。</returns>
    public OdfCellRange? Intersect(OdfCellRange other)
    {
        if (!Intersects(other))
            return null;

        int minRow1 = Math.Min(StartAddress.Row, EndAddress.Row);
        int maxRow1 = Math.Max(StartAddress.Row, EndAddress.Row);
        int minCol1 = Math.Min(StartAddress.Column, EndAddress.Column);
        int maxCol1 = Math.Max(StartAddress.Column, EndAddress.Column);

        int minRow2 = Math.Min(other.StartAddress.Row, other.EndAddress.Row);
        int maxRow2 = Math.Max(other.StartAddress.Row, other.EndAddress.Row);
        int minCol2 = Math.Min(other.StartAddress.Column, other.EndAddress.Column);
        int maxCol2 = Math.Max(other.StartAddress.Column, other.EndAddress.Column);

        int startRow = Math.Max(minRow1, minRow2);
        int endRow = Math.Min(maxRow1, maxRow2);
        int startCol = Math.Max(minCol1, minCol2);
        int endCol = Math.Min(maxCol1, maxCol2);

        var startAddress = new OdfCellAddress(startRow, startCol, StartAddress.SheetName);
        var endAddress = new OdfCellAddress(endRow, endCol, StartAddress.SheetName);
        return new OdfCellRange(startAddress, endAddress);
    }

    /// <summary>
    /// Adjusts the cell range according to row or column insertion and deletion.
    /// 根據列或欄的插入或刪除，調整儲存格範圍。
    /// </summary>
    /// <param name="insertRowIndex">The row insertion index. / 插入列的索引位置。</param>
    /// <param name="rowCount">The number of inserted rows; a negative value indicates deletion. / 插入的列數；負數表示刪除。</param>
    /// <param name="insertColIndex">The column insertion index. / 插入欄的索引位置。</param>
    /// <param name="colCount">The number of inserted columns; a negative value indicates deletion. / 插入的欄數；負數表示刪除。</param>
    /// <returns>The adjusted new <see cref="OdfCellRange"/> instance. / 調整後的新 <see cref="OdfCellRange"/> 執行個體。</returns>
    public OdfCellRange ShiftStructural(int insertRowIndex, int rowCount, int insertColIndex, int colCount)
    {
        var startAddress = StartAddress.ShiftStructural(insertRowIndex, rowCount, insertColIndex, colCount);
        var endAddress = EndAddress.ShiftStructural(insertRowIndex, rowCount, insertColIndex, colCount);
        return new OdfCellRange(startAddress, endAddress);
    }

    /// <summary>
    /// Converts this range to an Excel-style string.
    /// 將此範圍轉換為 Excel 格式的字串。
    /// </summary>
    /// <returns>The Excel-style range representation string. / Excel 格式的範圍表示字串。</returns>
    public string ToExcelString()
    {
        if (StartAddress.Equals(EndAddress))
        {
            return StartAddress.ToExcelString();
        }

        var startStr = StartAddress.ToExcelString();

        string endStr;
        if (StartAddress.SheetName is not null && string.Equals(StartAddress.SheetName, EndAddress.SheetName, StringComparison.OrdinalIgnoreCase))
        {
            var temp = new OdfCellAddress(EndAddress.Row, EndAddress.Column, null, EndAddress.IsRowAbsolute, EndAddress.IsColumnAbsolute, EndAddress.IsSheetAbsolute);
            endStr = temp.ToExcelString();
        }
        else
        {
            endStr = EndAddress.ToExcelString();
        }

        return $"{startStr}:{endStr}";
    }

    /// <summary>
    /// Converts this range to an ODF-style string.
    /// 將此範圍轉換為 ODF 格式的字串。
    /// </summary>
    /// <param name="includeBrackets">Whether to include brackets. / 是否包含中括號。</param>
    /// <returns>The ODF-style range representation string. / ODF 格式的範圍表示字串。</returns>
    public string ToOdfString(bool includeBrackets = false)
    {
        var sb = new StringBuilder();
        if (includeBrackets)
            sb.Append("[");

        var startStr = StartAddress.ToOdfString(false);

        string endStr;
        if (StartAddress.SheetName is not null && string.Equals(StartAddress.SheetName, EndAddress.SheetName, StringComparison.OrdinalIgnoreCase))
        {
            var temp = new OdfCellAddress(EndAddress.Row, EndAddress.Column, null, EndAddress.IsRowAbsolute, EndAddress.IsColumnAbsolute, EndAddress.IsSheetAbsolute);
            endStr = temp.ToOdfString(false);
        }
        else
        {
            endStr = EndAddress.ToOdfString(false);
        }

        sb.Append(startStr).Append(":").Append(endStr);

        if (includeBrackets)
            sb.Append("]");
        return sb.ToString();
    }

    /// <summary>
    /// Returns a string that represents the current object.
    /// 傳回代表目前物件的字串。
    /// </summary>
    /// <returns>A string that represents the current object. / 代表目前物件的字串。</returns>
    public override string ToString() => ToExcelString();
}
