using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using OdfKit.Compliance;
namespace OdfKit.DOM;

/// <summary>
/// 表示 ODF schema 中名為 <c>points</c> 的二維整數座標清單 lexical form。
/// </summary>
public readonly struct OdfPointList : IEquatable<OdfPointList>
{
    private static readonly Regex PointsRegex = new(
        @"^-?[0-9]+,-?[0-9]+(?:[ ]+-?[0-9]+,-?[0-9]+)*$",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// 以座標清單 lexical form 建立 <see cref="OdfPointList"/>。
    /// </summary>
    /// <param name="value">座標清單字串，例如 <c>0,0 10,20 -5,30</c></param>
    /// <exception cref="ArgumentException">當座標清單不符合 ODF <c>points</c> 格式時擲回</exception>
    public OdfPointList(string value)
    {
        if (!TryParseItems(value, out OdfPoint2D[] points))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfPointList_CoordinateListConformOdf"), nameof(value));
        }

        Value = value;
        Points = points;
    }

    /// <summary>
    /// 以座標集合建立 <see cref="OdfPointList"/>。
    /// </summary>
    /// <param name="points">要寫入的座標集合</param>
    /// <exception cref="ArgumentException">當集合未包含任何座標時擲回</exception>
    public OdfPointList(IEnumerable<OdfPoint2D> points)
    {
        OdfPoint2D[] items = points?.ToArray() ?? [];
        if (items.Length == 0)
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfPointList_CoordinateCannotBeEmpty"), nameof(points));
        }

        Value = string.Join(" ", items.Select(item => item.ToString()));
        Points = items;
    }

    /// <summary>
    /// 取得原始座標清單字串。
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// 取得解析後的座標集合。
    /// </summary>
    public IReadOnlyList<OdfPoint2D> Points { get; }

    /// <summary>
    /// 嘗試解析座標清單。
    /// </summary>
    /// <param name="value">座標清單字串</param>
    /// <param name="pointList">成功時傳回解析後的座標清單</param>
    /// <returns>若字串符合 ODF <c>points</c> 格式則為 <see langword="true"/></returns>
    public static bool TryParse(string? value, out OdfPointList pointList)
    {
        if (TryParseItems(value, out OdfPoint2D[] points))
        {
            pointList = new OdfPointList(value!, points);
            return true;
        }

        pointList = default;
        return false;
    }

    /// <summary>
    /// 傳回原始座標清單字串。
    /// </summary>
    /// <returns>座標清單字串</returns>
    public override string ToString() => Value ?? string.Empty;

    /// <summary>
    /// 判斷目前值是否等於另一個座標清單。
    /// </summary>
    /// <param name="other">要比較的座標清單</param>
    /// <returns>若 lexical form 相同則為 <see langword="true"/></returns>
    public bool Equals(OdfPointList other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OdfPointList other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);

    /// <summary>
    /// 判斷兩個座標清單是否相等。
    /// </summary>
    /// <param name="left">左側座標清單</param>
    /// <param name="right">右側座標清單</param>
    /// <returns>若兩者相等則為 <see langword="true"/></returns>
    public static bool operator ==(OdfPointList left, OdfPointList right) => left.Equals(right);

    /// <summary>
    /// 判斷兩個座標清單是否不相等。
    /// </summary>
    /// <param name="left">左側座標清單</param>
    /// <param name="right">右側座標清單</param>
    /// <returns>若兩者不相等則為 <see langword="true"/></returns>
    public static bool operator !=(OdfPointList left, OdfPointList right) => !left.Equals(right);

    private OdfPointList(string value, OdfPoint2D[] points)
    {
        Value = value;
        Points = points;
    }

    private static bool TryParseItems(string? value, out OdfPoint2D[] points)
    {
        points = [];
        if (value is null || ContainsControlCharacter(value) || !PointsRegex.IsMatch(value))
        {
            return false;
        }

        string[] tokens = value.Split(' ');
        OdfPoint2D[] parsed = new OdfPoint2D[tokens.Length];
        for (int index = 0; index < tokens.Length; index++)
        {
            string[] coordinates = tokens[index].Split(',');
            if (coordinates.Length != 2 ||
                !int.TryParse(coordinates[0], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int x) ||
                !int.TryParse(coordinates[1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int y))
            {
                return false;
            }

            parsed[index] = new OdfPoint2D(x, y);
        }

        points = parsed;
        return true;
    }

    private static bool ContainsControlCharacter(string value)
    {
        foreach (char ch in value)
        {
            if (char.IsControl(ch))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// 表示 ODF <c>points</c> 清單中的單一二維整數座標。
/// </summary>
public readonly struct OdfPoint2D : IEquatable<OdfPoint2D>
{
    /// <summary>
    /// 以 X 與 Y 座標建立 <see cref="OdfPoint2D"/>。
    /// </summary>
    /// <param name="x">X 座標</param>
    /// <param name="y">Y 座標</param>
    public OdfPoint2D(int x, int y)
    {
        X = x;
        Y = y;
    }

    /// <summary>
    /// 取得 X 座標。
    /// </summary>
    public int X { get; }

    /// <summary>
    /// 取得 Y 座標。
    /// </summary>
    public int Y { get; }

    /// <summary>
    /// 傳回座標的 lexical form。
    /// </summary>
    /// <returns>座標字串</returns>
    public override string ToString()
    {
        return string.Format(CultureInfo.InvariantCulture, "{0},{1}", X, Y);
    }

    /// <summary>
    /// 判斷目前座標是否等於另一個座標。
    /// </summary>
    /// <param name="other">要比較的座標</param>
    /// <returns>若 X 與 Y 座標皆相同則為 <see langword="true"/></returns>
    public bool Equals(OdfPoint2D other) => X == other.X && Y == other.Y;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OdfPoint2D other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(X, Y);

    /// <summary>
    /// 判斷兩個座標是否相等。
    /// </summary>
    /// <param name="left">左側座標</param>
    /// <param name="right">右側座標</param>
    /// <returns>若兩者相等則為 <see langword="true"/></returns>
    public static bool operator ==(OdfPoint2D left, OdfPoint2D right) => left.Equals(right);

    /// <summary>
    /// 判斷兩個座標是否不相等。
    /// </summary>
    /// <param name="left">左側座標</param>
    /// <param name="right">右側座標</param>
    /// <returns>若兩者不相等則為 <see langword="true"/></returns>
    public static bool operator !=(OdfPoint2D left, OdfPoint2D right) => !left.Equals(right);
}
