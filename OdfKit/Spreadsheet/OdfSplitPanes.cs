using System;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 表示工作表分割窗格設定（非凍結模式）。
/// </summary>
/// <param name="rows">水平分割線所在的列索引</param>
/// <param name="columns">垂直分割線所在的欄索引</param>
public readonly struct OdfSplitPanes(int rows, int columns) : IEquatable<OdfSplitPanes>
{
    /// <summary>
    /// 取得水平分割線所在的列索引。
    /// </summary>
    public int Rows { get; } = rows;

    /// <summary>
    /// 取得垂直分割線所在的欄索引。
    /// </summary>
    public int Columns { get; } = columns;

    /// <summary>
    /// 取得是否有任何分割窗格設定。
    /// </summary>
    public bool IsSplit => Rows > 0 || Columns > 0;

    /// <inheritdoc />
    public bool Equals(OdfSplitPanes other) => Rows == other.Rows && Columns == other.Columns;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OdfSplitPanes other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            return (Rows * 397) ^ Columns;
        }
    }

    /// <summary>
    /// 比較兩個 <see cref="OdfSplitPanes"/> 執行個體是否相等。
    /// </summary>
    public static bool operator ==(OdfSplitPanes left, OdfSplitPanes right) => left.Equals(right);

    /// <summary>
    /// 比較兩個 <see cref="OdfSplitPanes"/> 執行個體是否不相等。
    /// </summary>
    public static bool operator !=(OdfSplitPanes left, OdfSplitPanes right) => !left.Equals(right);
}
