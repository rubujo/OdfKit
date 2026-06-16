using System;
using System.Globalization;

namespace OdfKit.DOM;

/// <summary>
/// 表示 ODF schema 中 0 到 100 範圍內的百分比 lexical form。
/// </summary>
public readonly struct OdfPercent : IEquatable<OdfPercent>
{
    /// <summary>
    /// 以百分比 lexical form 建立 <see cref="OdfPercent"/>。
    /// </summary>
    /// <param name="value">百分比字串，例如 <c>50%</c>、<c>.5%</c> 或 <c>-25%</c>。</param>
    /// <exception cref="ArgumentException">當百分比字串不是 <c>-100%</c> 到 <c>100%</c> 的格式時擲回。</exception>
    public OdfPercent(string value)
    {
        if (!TryParseCore(value, allowNegative: true, out decimal percent))
        {
            throw new ArgumentException("百分比值必須介於 -100% 到 100% 之間。", nameof(value));
        }

        Value = value;
        Percent = percent;
    }

    /// <summary>
    /// 取得原始百分比字串。
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// 取得百分比數值。
    /// </summary>
    public decimal Percent { get; }

    /// <summary>
    /// 從百分比數值建立 <see cref="OdfPercent"/>。
    /// </summary>
    /// <param name="percent">百分比數值，必須介於 -100 到 100 之間。</param>
    /// <returns>對應的百分比值。</returns>
    public static OdfPercent FromPercent(decimal percent) => new(percent.ToString(CultureInfo.InvariantCulture) + "%");

    /// <summary>
    /// 嘗試解析 0 到 100 的百分比字串。
    /// </summary>
    /// <param name="value">百分比字串。</param>
    /// <param name="percent">成功時傳回解析後的百分比。</param>
    /// <returns>若字串為 0 到 100 的百分比則為 <see langword="true"/>。</returns>
    public static bool TryParse(string? value, out OdfPercent percent) => TryParse(value, allowNegative: false, out percent);

    /// <summary>
    /// 嘗試解析可選擇是否允許負值的百分比字串。
    /// </summary>
    /// <param name="value">百分比字串。</param>
    /// <param name="allowNegative">是否允許負值。</param>
    /// <param name="percent">成功時傳回解析後的百分比。</param>
    /// <returns>若字串符合百分比範圍則為 <see langword="true"/>。</returns>
    public static bool TryParse(string? value, bool allowNegative, out OdfPercent percent)
    {
        if (TryParseCore(value, allowNegative, out _))
        {
            percent = new OdfPercent(value!);
            return true;
        }

        percent = default;
        return false;
    }

    /// <summary>
    /// 傳回原始百分比字串。
    /// </summary>
    /// <returns>百分比字串。</returns>
    public override string ToString() => Value ?? string.Empty;

    /// <summary>
    /// 判斷目前值是否等於另一個百分比。
    /// </summary>
    /// <param name="other">要比較的百分比。</param>
    /// <returns>若 lexical form 相同則為 <see langword="true"/>。</returns>
    public bool Equals(OdfPercent other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OdfPercent other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);

    /// <summary>
    /// 判斷兩個百分比是否相等。
    /// </summary>
    /// <param name="left">左側百分比。</param>
    /// <param name="right">右側百分比。</param>
    /// <returns>若兩者相等則為 <see langword="true"/>。</returns>
    public static bool operator ==(OdfPercent left, OdfPercent right) => left.Equals(right);

    /// <summary>
    /// 判斷兩個百分比是否不相等。
    /// </summary>
    /// <param name="left">左側百分比。</param>
    /// <param name="right">右側百分比。</param>
    /// <returns>若兩者不相等則為 <see langword="true"/>。</returns>
    public static bool operator !=(OdfPercent left, OdfPercent right) => !left.Equals(right);

    private static bool TryParseCore(string? value, bool allowNegative, out decimal percent)
    {
        percent = 0;
        if (string.IsNullOrEmpty(value) || !value!.EndsWith("%", StringComparison.Ordinal))
        {
            return false;
        }

        string number = value.Substring(0, value.Length - 1);
        if (number.Length == 0 || number.StartsWith("+", StringComparison.Ordinal))
        {
            return false;
        }

        if (number.StartsWith("-", StringComparison.Ordinal) && !allowNegative)
        {
            return false;
        }

        if (!decimal.TryParse(number, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out percent))
        {
            return false;
        }

        return percent >= (allowNegative ? -100 : 0) && percent <= 100;
    }
}
