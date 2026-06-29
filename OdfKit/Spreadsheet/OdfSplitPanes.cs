using System;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Represents split pane settings for a worksheet in non-frozen mode.
/// 表示工作表分割窗格設定（非凍結模式）。
/// </summary>
/// <param name="rows">The row index of the horizontal split line. / 水平分割線所在的列索引。</param>
/// <param name="columns">The column index of the vertical split line. / 垂直分割線所在的欄索引。</param>
public readonly struct OdfSplitPanes(int rows, int columns) : IEquatable<OdfSplitPanes>
{
    /// <summary>
    /// Gets the row index of the horizontal split line.
    /// 取得水平分割線所在的列索引。
    /// </summary>
    public int Rows { get; } = rows;

    /// <summary>
    /// Gets the column index of the vertical split line.
    /// 取得垂直分割線所在的欄索引。
    /// </summary>
    public int Columns { get; } = columns;

    /// <summary>
    /// Gets whether any split pane setting exists.
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
    /// Compares two <see cref="OdfSplitPanes"/> instances for equality.
    /// 比較兩個 <see cref="OdfSplitPanes"/> 執行個體是否相等。
    /// </summary>
    public static bool operator ==(OdfSplitPanes left, OdfSplitPanes right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="OdfSplitPanes"/> instances for inequality.
    /// 比較兩個 <see cref="OdfSplitPanes"/> 執行個體是否不相等。
    /// </summary>
    public static bool operator !=(OdfSplitPanes left, OdfSplitPanes right) => !left.Equals(right);
}
