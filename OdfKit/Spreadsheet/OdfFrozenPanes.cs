using System;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 表示工作表凍結窗格設定。
/// </summary>
/// <param name="rows">凍結列數</param>
/// <param name="columns">凍結欄數</param>
public readonly struct OdfFrozenPanes(int rows, int columns) : IEquatable<OdfFrozenPanes>
{
    /// <summary>
    /// 取得凍結列數。
    /// </summary>
    public int Rows { get; } = rows;

    /// <summary>
    /// 取得凍結欄數。
    /// </summary>
    public int Columns { get; } = columns;

    /// <summary>
    /// 取得是否有任何凍結窗格設定。
    /// </summary>
    public bool IsFrozen => Rows > 0 || Columns > 0;

    /// <inheritdoc />
    public bool Equals(OdfFrozenPanes other) => Rows == other.Rows && Columns == other.Columns;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OdfFrozenPanes other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            return (Rows * 397) ^ Columns;
        }
    }

    /// <summary>
    /// 比較兩個 <see cref="OdfFrozenPanes"/> 執行個體是否相等。
    /// </summary>
    public static bool operator ==(OdfFrozenPanes left, OdfFrozenPanes right) => left.Equals(right);

    /// <summary>
    /// 比較兩個 <see cref="OdfFrozenPanes"/> 執行個體是否不相等。
    /// </summary>
    public static bool operator !=(OdfFrozenPanes left, OdfFrozenPanes right) => !left.Equals(right);
}
