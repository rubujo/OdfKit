using System;
using System.Text.RegularExpressions;
using OdfKit.Styles;

using OdfKit.Compliance;
namespace OdfKit.DOM;

/// <summary>
/// 表示 ODF schema 中名為 <c>point3D</c> 的三維點 lexical form。
/// </summary>
public readonly struct OdfPoint3D : IEquatable<OdfPoint3D>
{
    private static readonly Regex Point3DRegex = new(
        @"^\([ ]*(-?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:cm|mm|in|pt|pc))[ ]+(-?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:cm|mm|in|pt|pc))[ ]+(-?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:cm|mm|in|pt|pc))[ ]*\)$",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// 以三維點 lexical form 建立 <see cref="OdfPoint3D"/>。
    /// </summary>
    /// <param name="value">三維點字串，例如 <c>(1cm 0mm -0.5in)</c></param>
    /// <exception cref="ArgumentException">當三維點不符合 ODF <c>point3D</c> 格式時擲回</exception>
    public OdfPoint3D(string value)
    {
        if (!TryParseComponents(value, out OdfLength x, out OdfLength y, out OdfLength z))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfPoint3D_3dPointsConformOdf"), nameof(value));
        }

        Value = value;
        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>
    /// 以三個帶單位座標值建立 <see cref="OdfPoint3D"/>。
    /// </summary>
    /// <param name="x">X 座標值</param>
    /// <param name="y">Y 座標值</param>
    /// <param name="z">Z 座標值</param>
    /// <exception cref="ArgumentException">當任一座標不是 <c>cm</c>、<c>mm</c>、<c>in</c>、<c>pt</c> 或 <c>pc</c> 單位時擲回</exception>
    public OdfPoint3D(OdfLength x, OdfLength y, OdfLength z)
    {
        if (!IsAllowedUnit(x) || !IsAllowedUnit(y) || !IsAllowedUnit(z))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfPoint3D_3dPointCoordinatesOnly"));
        }

        X = x;
        Y = y;
        Z = z;
        Value = $"({x} {y} {z})";
    }

    /// <summary>
    /// 取得原始三維點字串。
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// 取得 X 座標值。
    /// </summary>
    public OdfLength X { get; }

    /// <summary>
    /// 取得 Y 座標值。
    /// </summary>
    public OdfLength Y { get; }

    /// <summary>
    /// 取得 Z 座標值。
    /// </summary>
    public OdfLength Z { get; }

    /// <summary>
    /// 嘗試解析三維點字串。
    /// </summary>
    /// <param name="value">三維點字串</param>
    /// <param name="point">成功時傳回解析後的三維點</param>
    /// <returns>若字串符合 ODF <c>point3D</c> 格式則為 <see langword="true"/></returns>
    public static bool TryParse(string? value, out OdfPoint3D point)
    {
        if (TryParseComponents(value, out OdfLength x, out OdfLength y, out OdfLength z))
        {
            point = new OdfPoint3D(value!, x, y, z);
            return true;
        }

        point = default;
        return false;
    }

    /// <summary>
    /// 傳回原始三維點字串。
    /// </summary>
    /// <returns>三維點字串</returns>
    public override string ToString() => Value ?? string.Empty;

    /// <summary>
    /// 判斷目前值是否等於另一個三維點。
    /// </summary>
    /// <param name="other">要比較的三維點</param>
    /// <returns>若 lexical form 相同則為 <see langword="true"/></returns>
    public bool Equals(OdfPoint3D other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OdfPoint3D other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);

    /// <summary>
    /// 判斷兩個三維點是否相等。
    /// </summary>
    /// <param name="left">左側三維點</param>
    /// <param name="right">右側三維點</param>
    /// <returns>若兩者相等則為 <see langword="true"/></returns>
    public static bool operator ==(OdfPoint3D left, OdfPoint3D right) => left.Equals(right);

    /// <summary>
    /// 判斷兩個三維點是否不相等。
    /// </summary>
    /// <param name="left">左側三維點</param>
    /// <param name="right">右側三維點</param>
    /// <returns>若兩者不相等則為 <see langword="true"/></returns>
    public static bool operator !=(OdfPoint3D left, OdfPoint3D right) => !left.Equals(right);

    private OdfPoint3D(string value, OdfLength x, OdfLength y, OdfLength z)
    {
        Value = value;
        X = x;
        Y = y;
        Z = z;
    }

    private static bool TryParseComponents(string? value, out OdfLength x, out OdfLength y, out OdfLength z)
    {
        x = default;
        y = default;
        z = default;

        if (value is null || ContainsControlCharacter(value))
        {
            return false;
        }

        Match match = Point3DRegex.Match(value);
        if (!match.Success)
        {
            return false;
        }

        return OdfLength.TryParse(match.Groups[1].Value, out x) &&
            OdfLength.TryParse(match.Groups[2].Value, out y) &&
            OdfLength.TryParse(match.Groups[3].Value, out z) &&
            IsAllowedUnit(x) &&
            IsAllowedUnit(y) &&
            IsAllowedUnit(z);
    }

    private static bool IsAllowedUnit(OdfLength length)
    {
        return length.Unit is OdfUnit.Centimeters or OdfUnit.Millimeters or OdfUnit.Inches or OdfUnit.Points or OdfUnit.Picas;
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
