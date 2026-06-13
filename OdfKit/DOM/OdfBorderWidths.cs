using System;
using OdfKit.Styles;

namespace OdfKit.DOM;

/// <summary>
/// 表示 ODF schema 中名為 <c>borderWidths</c> 的三段邊框線寬 lexical form。
/// </summary>
public readonly struct OdfBorderWidths : IEquatable<OdfBorderWidths>
{
    /// <summary>
    /// 以三段邊框線寬 lexical form 建立 <see cref="OdfBorderWidths"/>。
    /// </summary>
    /// <param name="value">三段正長度字串，例如 <c>0.05pt 0.10pt 0.05pt</c>。</param>
    /// <exception cref="ArgumentException">當字串不是三個 ODF <c>positiveLength</c> 值時擲回。</exception>
    public OdfBorderWidths(string value)
    {
        if (!TryParseComponents(value, out OdfLength innerLineWidth, out OdfLength spacing, out OdfLength outerLineWidth))
        {
            throw new ArgumentException("邊框線寬必須是三個以空白分隔的正長度值。", nameof(value));
        }

        Value = value;
        InnerLineWidth = innerLineWidth;
        Spacing = spacing;
        OuterLineWidth = outerLineWidth;
    }

    /// <summary>
    /// 以三個正長度值建立 <see cref="OdfBorderWidths"/>。
    /// </summary>
    /// <param name="innerLineWidth">內側線寬。</param>
    /// <param name="spacing">兩條線之間的間距。</param>
    /// <param name="outerLineWidth">外側線寬。</param>
    /// <exception cref="ArgumentException">當任一長度不是 ODF <c>positiveLength</c> 時擲回。</exception>
    public OdfBorderWidths(OdfLength innerLineWidth, OdfLength spacing, OdfLength outerLineWidth)
    {
        if (!IsPositiveLength(innerLineWidth) || !IsPositiveLength(spacing) || !IsPositiveLength(outerLineWidth))
        {
            throw new ArgumentException("邊框線寬只能使用大於 0 的 cm、mm、in、pt、pc 或 px 長度。");
        }

        InnerLineWidth = innerLineWidth;
        Spacing = spacing;
        OuterLineWidth = outerLineWidth;
        Value = $"{innerLineWidth} {spacing} {outerLineWidth}";
    }

    /// <summary>
    /// 取得原始三段邊框線寬字串。
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// 取得內側線寬。
    /// </summary>
    public OdfLength InnerLineWidth { get; }

    /// <summary>
    /// 取得兩條線之間的間距。
    /// </summary>
    public OdfLength Spacing { get; }

    /// <summary>
    /// 取得外側線寬。
    /// </summary>
    public OdfLength OuterLineWidth { get; }

    /// <summary>
    /// 嘗試解析三段邊框線寬字串。
    /// </summary>
    /// <param name="value">三段邊框線寬字串。</param>
    /// <param name="borderWidths">成功時傳回解析後的三段邊框線寬。</param>
    /// <returns>若字串符合 ODF <c>borderWidths</c> 格式則為 <see langword="true"/>。</returns>
    public static bool TryParse(string? value, out OdfBorderWidths borderWidths)
    {
        if (TryParseComponents(value, out OdfLength innerLineWidth, out OdfLength spacing, out OdfLength outerLineWidth))
        {
            borderWidths = new OdfBorderWidths(value!, innerLineWidth, spacing, outerLineWidth);
            return true;
        }

        borderWidths = default;
        return false;
    }

    /// <summary>
    /// 傳回原始三段邊框線寬字串。
    /// </summary>
    /// <returns>三段邊框線寬字串。</returns>
    public override string ToString() => Value ?? string.Empty;

    /// <summary>
    /// 判斷目前值是否等於另一組三段邊框線寬。
    /// </summary>
    /// <param name="other">要比較的三段邊框線寬。</param>
    /// <returns>若 lexical form 相同則為 <see langword="true"/>。</returns>
    public bool Equals(OdfBorderWidths other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OdfBorderWidths other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);

    /// <summary>
    /// 判斷兩組三段邊框線寬是否相等。
    /// </summary>
    /// <param name="left">左側三段邊框線寬。</param>
    /// <param name="right">右側三段邊框線寬。</param>
    /// <returns>若兩者相等則為 <see langword="true"/>。</returns>
    public static bool operator ==(OdfBorderWidths left, OdfBorderWidths right) => left.Equals(right);

    /// <summary>
    /// 判斷兩組三段邊框線寬是否不相等。
    /// </summary>
    /// <param name="left">左側三段邊框線寬。</param>
    /// <param name="right">右側三段邊框線寬。</param>
    /// <returns>若兩者不相等則為 <see langword="true"/>。</returns>
    public static bool operator !=(OdfBorderWidths left, OdfBorderWidths right) => !left.Equals(right);

    private OdfBorderWidths(string value, OdfLength innerLineWidth, OdfLength spacing, OdfLength outerLineWidth)
    {
        Value = value;
        InnerLineWidth = innerLineWidth;
        Spacing = spacing;
        OuterLineWidth = outerLineWidth;
    }

    private static bool TryParseComponents(string? value, out OdfLength innerLineWidth, out OdfLength spacing, out OdfLength outerLineWidth)
    {
        innerLineWidth = default;
        spacing = default;
        outerLineWidth = default;

        if (value is null || ContainsControlCharacter(value))
        {
            return false;
        }

        string[] parts = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
        {
            return false;
        }

        return OdfLength.TryParse(parts[0], out innerLineWidth) &&
            OdfLength.TryParse(parts[1], out spacing) &&
            OdfLength.TryParse(parts[2], out outerLineWidth) &&
            IsPositiveLength(innerLineWidth) &&
            IsPositiveLength(spacing) &&
            IsPositiveLength(outerLineWidth);
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
