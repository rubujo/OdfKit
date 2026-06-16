using System;
using System.Text;
using OdfKit.Core;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 表示 ODF 試算表中的儲存格位址。
/// </summary>
public readonly partial struct OdfCellAddress : IEquatable<OdfCellAddress>
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
}
