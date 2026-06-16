using System;
using System.Globalization;
using OdfKit.Styles;

namespace OdfKit.DOM;

/// <summary>
/// 表示 ODF schema 中名為 <c>lineWidth</c> 的線條寬度 lexical form。
/// </summary>
public readonly struct OdfLineWidth : IEquatable<OdfLineWidth>
{
    /// <summary>
    /// 以線條寬度 lexical form 建立 <see cref="OdfLineWidth"/>。
    /// </summary>
    /// <param name="value">線條寬度，例如 <c>auto</c>、<c>bold</c>、<c>150%</c>、<c>2pt</c> 或 <c>3</c>。</param>
    /// <exception cref="ArgumentException">當字串不符合 ODF <c>lineWidth</c> 格式時擲回。</exception>
    public OdfLineWidth(string value)
    {
        if (!TryParseParts(value, out OdfLineWidthKind kind, out int? positiveInteger, out decimal? percent, out OdfLength? length))
        {
            throw new ArgumentException("線條寬度必須是已知 keyword、正整數、百分比或正長度。", nameof(value));
        }

        Value = value;
        Kind = kind;
        PositiveInteger = positiveInteger;
        Percent = percent;
        Length = length;
    }

    /// <summary>
    /// 取得原始線條寬度字串。
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// 取得線條寬度值的分類。
    /// </summary>
    public OdfLineWidthKind Kind { get; }

    /// <summary>
    /// 當 <see cref="Kind"/> 為 <see cref="OdfLineWidthKind.PositiveInteger"/> 時，取得正整數值。
    /// </summary>
    public int? PositiveInteger { get; }

    /// <summary>
    /// 當 <see cref="Kind"/> 為 <see cref="OdfLineWidthKind.Percent"/> 時，取得百分比數值。
    /// </summary>
    public decimal? Percent { get; }

    /// <summary>
    /// 當 <see cref="Kind"/> 為 <see cref="OdfLineWidthKind.Length"/> 時，取得正長度值。
    /// </summary>
    public OdfLength? Length { get; }

    /// <summary>
    /// 嘗試解析線條寬度字串。
    /// </summary>
    /// <param name="value">線條寬度字串。</param>
    /// <param name="lineWidth">成功時傳回解析後的線條寬度。</param>
    /// <returns>若字串符合 ODF <c>lineWidth</c> 格式則為 <see langword="true"/>。</returns>
    public static bool TryParse(string? value, out OdfLineWidth lineWidth)
    {
        if (TryParseParts(value, out OdfLineWidthKind kind, out int? positiveInteger, out decimal? percent, out OdfLength? length))
        {
            lineWidth = new OdfLineWidth(value!, kind, positiveInteger, percent, length);
            return true;
        }

        lineWidth = default;
        return false;
    }

    /// <summary>
    /// 傳回原始線條寬度字串。
    /// </summary>
    /// <returns>線條寬度字串。</returns>
    public override string ToString() => Value ?? string.Empty;

    /// <summary>
    /// 判斷目前值是否等於另一個線條寬度。
    /// </summary>
    /// <param name="other">要比較的線條寬度。</param>
    /// <returns>若 lexical form 相同則為 <see langword="true"/>。</returns>
    public bool Equals(OdfLineWidth other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OdfLineWidth other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);

    /// <summary>
    /// 判斷兩個線條寬度是否相等。
    /// </summary>
    /// <param name="left">左側線條寬度。</param>
    /// <param name="right">右側線條寬度。</param>
    /// <returns>若兩者相等則為 <see langword="true"/>。</returns>
    public static bool operator ==(OdfLineWidth left, OdfLineWidth right) => left.Equals(right);

    /// <summary>
    /// 判斷兩個線條寬度是否不相等。
    /// </summary>
    /// <param name="left">左側線條寬度。</param>
    /// <param name="right">右側線條寬度。</param>
    /// <returns>若兩者不相等則為 <see langword="true"/>。</returns>
    public static bool operator !=(OdfLineWidth left, OdfLineWidth right) => !left.Equals(right);

    private OdfLineWidth(string value, OdfLineWidthKind kind, int? positiveInteger, decimal? percent, OdfLength? length)
    {
        Value = value;
        Kind = kind;
        PositiveInteger = positiveInteger;
        Percent = percent;
        Length = length;
    }

    private static bool TryParseParts(string? value, out OdfLineWidthKind kind, out int? positiveInteger, out decimal? percent, out OdfLength? length)
    {
        kind = default;
        positiveInteger = null;
        percent = null;
        length = null;

        if (value is null || value.Length == 0 || ContainsControlCharacter(value))
        {
            return false;
        }

        switch (value)
        {
            case "auto":
                kind = OdfLineWidthKind.Auto;
                return true;
            case "normal":
                kind = OdfLineWidthKind.Normal;
                return true;
            case "bold":
                kind = OdfLineWidthKind.Bold;
                return true;
            case "thin":
                kind = OdfLineWidthKind.Thin;
                return true;
            case "medium":
                kind = OdfLineWidthKind.Medium;
                return true;
            case "thick":
                kind = OdfLineWidthKind.Thick;
                return true;
        }

        if (TryParsePositiveInteger(value, out int integer))
        {
            kind = OdfLineWidthKind.PositiveInteger;
            positiveInteger = integer;
            return true;
        }

        if (TryParsePercent(value, out decimal parsedPercent))
        {
            kind = OdfLineWidthKind.Percent;
            percent = parsedPercent;
            return true;
        }

        if (OdfLength.TryParse(value, out OdfLength parsedLength) && IsPositiveLength(parsedLength))
        {
            kind = OdfLineWidthKind.Length;
            length = parsedLength;
            return true;
        }

        return false;
    }

    private static bool TryParsePositiveInteger(string value, out int positiveInteger)
    {
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out positiveInteger) && positiveInteger > 0;
    }

    private static bool TryParsePercent(string value, out decimal percent)
    {
        percent = 0;
        if (!value.EndsWith("%", StringComparison.Ordinal))
        {
            return false;
        }

        string number = value.Substring(0, value.Length - 1);
        if (number.Length == 0 || number.StartsWith("+", StringComparison.Ordinal))
        {
            return false;
        }

        return decimal.TryParse(number, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out percent);
    }

    private static bool IsPositiveLength(OdfLength length)
    {
        return length.Value > 0 &&
            length.Unit is OdfUnit.Centimeters or OdfUnit.Millimeters or OdfUnit.Inches or OdfUnit.Points or OdfUnit.Picas or OdfUnit.Pixels;
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
/// 表示 <see cref="OdfLineWidth"/> 的 schema choice 分類。
/// </summary>
public enum OdfLineWidthKind
{
    /// <summary>
    /// <c>auto</c> keyword。
    /// </summary>
    Auto,

    /// <summary>
    /// <c>normal</c> keyword。
    /// </summary>
    Normal,

    /// <summary>
    /// <c>bold</c> keyword。
    /// </summary>
    Bold,

    /// <summary>
    /// <c>thin</c> keyword。
    /// </summary>
    Thin,

    /// <summary>
    /// <c>medium</c> keyword。
    /// </summary>
    Medium,

    /// <summary>
    /// <c>thick</c> keyword。
    /// </summary>
    Thick,

    /// <summary>
    /// 正整數值。
    /// </summary>
    PositiveInteger,

    /// <summary>
    /// 百分比值。
    /// </summary>
    Percent,

    /// <summary>
    /// 正長度值。
    /// </summary>
    Length
}
