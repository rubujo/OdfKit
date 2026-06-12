using System;
using System.Text;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 表示 ODF 試算表中的儲存格範圍。
/// </summary>
/// <remarks>
/// 初始化 <see cref="OdfCellRange"/> 結構的新執行個體。
/// </remarks>
/// <param name="start">起始儲存格位址</param>
/// <param name="end">結束儲存格位址</param>
public readonly struct OdfCellRange(OdfCellAddress start, OdfCellAddress end) : IEquatable<OdfCellRange>
{
    /// <summary>
    /// 取得範圍的起始儲存格位址。
    /// </summary>
    public OdfCellAddress StartAddress { get; } = start;

    /// <summary>
    /// 取得範圍的結束儲存格位址。
    /// </summary>
    public OdfCellAddress EndAddress { get; } = end;

    /// <summary>
    /// 初始化 <see cref="OdfCellRange"/> 結構的新執行個體。
    /// </summary>
    /// <param name="startRow">起始列索引</param>
    /// <param name="startCol">起始欄索引</param>
    /// <param name="endRow">結束列索引</param>
    /// <param name="endCol">結束欄索引</param>
    /// <param name="sheetName">工作表名稱</param>
    public OdfCellRange(int startRow, int startCol, int endRow, int endCol, string? sheetName = null)
        : this(new OdfCellAddress(startRow, startCol, sheetName), new OdfCellAddress(endRow, endCol, sheetName))
    {
    }

    /// <summary>
    /// 指出目前執行個體是否等於另一個相同類型的執行個體。
    /// </summary>
    /// <param name="other">要比較的另一個執行個體</param>
    /// <returns>如果兩個執行個體相等則為 true，否則為 false</returns>
    public bool Equals(OdfCellRange other) => 
        StartAddress == other.StartAddress && EndAddress == other.EndAddress;

    /// <summary>
    /// 指出此執行個體與指定的物件是否相等。
    /// </summary>
    /// <param name="obj">要比較的物件</param>
    /// <returns>如果指定的物件與目前執行個體相等則為 true，否則為 false</returns>
    public override bool Equals(object? obj) => obj is OdfCellRange other && Equals(other);

    /// <summary>
    /// 傳回此執行個體的雜湊碼。
    /// </summary>
    /// <returns>32 位元有號整數雜湊碼</returns>
    public override int GetHashCode()
    {
        unchecked
        {
            return (StartAddress.GetHashCode() * 397) ^ EndAddress.GetHashCode();
        }
    }

    /// <summary>
    /// 比較兩個 <see cref="OdfCellRange"/> 執行個體是否相等。
    /// </summary>
    /// <param name="left">左方的執行個體</param>
    /// <param name="right">右方的執行個體</param>
    /// <returns>如果相等則為 true，否則為 false</returns>
    public static bool operator ==(OdfCellRange left, OdfCellRange right) => left.Equals(right);

    /// <summary>
    /// 比較兩個 <see cref="OdfCellRange"/> 執行個體是否不相等。
    /// </summary>
    /// <param name="left">左方的執行個體</param>
    /// <param name="right">右方的執行個體</param>
    /// <returns>如果不相等則為 true，否則為 false</returns>
    public static bool operator !=(OdfCellRange left, OdfCellRange right) => !left.Equals(right);

    /// <summary>
    /// 嘗試解析儲存格範圍字串。
    /// </summary>
    /// <param name="value">要解析的範圍字串</param>
    /// <param name="range">解析成功時傳回的儲存格範圍</param>
    /// <returns>如果解析成功則為 true，否則為 false</returns>
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
    /// 解析 Excel 格式的儲存格範圍字串。
    /// </summary>
    /// <param name="rangeStr">Excel 格式的範圍字串</param>
    /// <returns>解析後的 <see cref="OdfCellRange"/> 執行個體</returns>
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

        // Propagate sheet name if missing on the end coordinate
        if (end.SheetName is null && start.SheetName is not null)
        {
            end = new OdfCellAddress(end.Row, end.Column, start.SheetName, 
                end.IsRowAbsolute, end.IsColumnAbsolute, start.IsSheetAbsolute);
        }

        return new OdfCellRange(start, end);
    }

    /// <summary>
    /// 解析 ODF 格式的儲存格範圍字串。
    /// </summary>
    /// <param name="rangeStr">ODF 格式的範圍字串</param>
    /// <returns>解析後的 <see cref="OdfCellRange"/> 執行個體</returns>
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
            if (c == '\'') inQuotes = !inQuotes;
            if (!inQuotes && c == ':') return i;
        }
        return -1;
    }

    /// <summary>
    /// 判斷此範圍是否包含指定的儲存格位址。
    /// </summary>
    /// <param name="address">要檢查的儲存格位址</param>
    /// <returns>如果包含則為 true，否則為 false</returns>
    public bool Contains(OdfCellAddress address)
    {
        // Check sheet equivalence
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
    /// 判斷此範圍是否與另一個範圍相交。
    /// </summary>
    /// <param name="other">要檢查的另一個範圍</param>
    /// <returns>如果相交則為 true，否則為 false</returns>
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
    /// 計算此範圍與另一個範圍的相交區域。
    /// </summary>
    /// <param name="other">另一個儲存格範圍</param>
    /// <returns>相交的 <see cref="OdfCellRange"/>，如果不相交則為 null</returns>
    public OdfCellRange? Intersect(OdfCellRange other)
    {
        if (!Intersects(other)) return null;

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
    /// 根據列或欄的插入或刪除，調整儲存格範圍。
    /// </summary>
    /// <param name="insertRowIndex">插入列的索引位置</param>
    /// <param name="rowCount">插入的列數（負數表示刪除）</param>
    /// <param name="insertColIndex">插入欄的索引位置</param>
    /// <param name="colCount">插入的欄數（負數表示刪除）</param>
    /// <returns>調整後的新 <see cref="OdfCellRange"/> 執行個體</returns>
    public OdfCellRange ShiftStructural(int insertRowIndex, int rowCount, int insertColIndex, int colCount)
    {
        var startAddress = StartAddress.ShiftStructural(insertRowIndex, rowCount, insertColIndex, colCount);
        var endAddress = EndAddress.ShiftStructural(insertRowIndex, rowCount, insertColIndex, colCount);
        return new OdfCellRange(startAddress, endAddress);
    }

    /// <summary>
    /// 將此範圍轉換為 Excel 格式的字串。
    /// </summary>
    /// <returns>Excel 格式的範圍表示字串</returns>
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
    /// 將此範圍轉換為 ODF 格式的字串。
    /// </summary>
    /// <param name="includeBrackets">是否包含中括號</param>
    /// <returns>ODF 格式的範圍表示字串</returns>
    public string ToOdfString(bool includeBrackets = false)
    {
        var sb = new StringBuilder();
        if (includeBrackets) sb.Append("[");

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

        if (includeBrackets) sb.Append("]");
        return sb.ToString();
    }

    /// <summary>
    /// 傳回代表目前物件的字串。
    /// </summary>
    /// <returns>代表目前物件的字串</returns>
    public override string ToString() => ToExcelString();
}
