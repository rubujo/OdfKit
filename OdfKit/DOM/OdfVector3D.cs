using System;
using System.Globalization;
using System.Text.RegularExpressions;

using OdfKit.Compliance;
namespace OdfKit.DOM;

/// <summary>
/// 表示 ODF schema 中名為 <c>vector3D</c> 的三維向量 lexical form。
/// </summary>
public readonly struct OdfVector3D : IEquatable<OdfVector3D>
{
    private static readonly Regex Vector3DRegex = new(
        @"^\([ ]*(-?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+))[ ]+(-?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+))[ ]+(-?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+))[ ]*\)$",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// 以三維向量 lexical form 建立 <see cref="OdfVector3D"/>。
    /// </summary>
    /// <param name="value">三維向量字串，例如 <c>(1 0 -0.5)</c></param>
    /// <exception cref="ArgumentException">當三維向量不符合 ODF <c>vector3D</c> 格式時擲回</exception>
    public OdfVector3D(string value)
    {
        if (!TryParseComponents(value, out decimal x, out decimal y, out decimal z))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfVector3D_ThreeDimensionalVectorsConform"), nameof(value));
        }

        Value = value;
        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>
    /// 以三個座標值建立 <see cref="OdfVector3D"/>。
    /// </summary>
    /// <param name="x">X 座標值</param>
    /// <param name="y">Y 座標值</param>
    /// <param name="z">Z 座標值</param>
    public OdfVector3D(decimal x, decimal y, decimal z)
    {
        X = x;
        Y = y;
        Z = z;
        Value = string.Format(CultureInfo.InvariantCulture, "({0} {1} {2})", x, y, z);
    }

    /// <summary>
    /// 取得原始三維向量字串。
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// 取得 X 座標值。
    /// </summary>
    public decimal X { get; }

    /// <summary>
    /// 取得 Y 座標值。
    /// </summary>
    public decimal Y { get; }

    /// <summary>
    /// 取得 Z 座標值。
    /// </summary>
    public decimal Z { get; }

    /// <summary>
    /// 嘗試解析三維向量字串。
    /// </summary>
    /// <param name="value">三維向量字串</param>
    /// <param name="vector">成功時傳回解析後的三維向量</param>
    /// <returns>若字串符合 ODF <c>vector3D</c> 格式則為 <see langword="true"/></returns>
    public static bool TryParse(string? value, out OdfVector3D vector)
    {
        if (TryParseComponents(value, out decimal x, out decimal y, out decimal z))
        {
            vector = new OdfVector3D(value!, x, y, z);
            return true;
        }

        vector = default;
        return false;
    }

    /// <summary>
    /// 傳回原始三維向量字串。
    /// </summary>
    /// <returns>三維向量字串</returns>
    public override string ToString() => Value ?? string.Empty;

    /// <summary>
    /// 判斷目前值是否等於另一個三維向量。
    /// </summary>
    /// <param name="other">要比較的三維向量</param>
    /// <returns>若 lexical form 相同則為 <see langword="true"/></returns>
    public bool Equals(OdfVector3D other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OdfVector3D other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);

    /// <summary>
    /// 判斷兩個三維向量是否相等。
    /// </summary>
    /// <param name="left">左側三維向量</param>
    /// <param name="right">右側三維向量</param>
    /// <returns>若兩者相等則為 <see langword="true"/></returns>
    public static bool operator ==(OdfVector3D left, OdfVector3D right) => left.Equals(right);

    /// <summary>
    /// 判斷兩個三維向量是否不相等。
    /// </summary>
    /// <param name="left">左側三維向量</param>
    /// <param name="right">右側三維向量</param>
    /// <returns>若兩者不相等則為 <see langword="true"/></returns>
    public static bool operator !=(OdfVector3D left, OdfVector3D right) => !left.Equals(right);

    private OdfVector3D(string value, decimal x, decimal y, decimal z)
    {
        Value = value;
        X = x;
        Y = y;
        Z = z;
    }

    private static bool TryParseComponents(string? value, out decimal x, out decimal y, out decimal z)
    {
        x = default;
        y = default;
        z = default;

        if (value is null || ContainsControlCharacter(value))
        {
            return false;
        }

        Match match = Vector3DRegex.Match(value);
        if (!match.Success)
        {
            return false;
        }

        return decimal.TryParse(match.Groups[1].Value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out x) &&
            decimal.TryParse(match.Groups[2].Value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out y) &&
            decimal.TryParse(match.Groups[3].Value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out z);
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
