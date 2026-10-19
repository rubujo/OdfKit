using System;
using System.Globalization;

using OdfKit.Compliance;
namespace OdfKit.DOM;

/// <summary>
/// Represents a bounded ODF percentage lexical form from -100% to 100%.
/// 表示 ODF schema 中 -100% 到 100% 的有界百分比詞法形式。
/// </summary>
public readonly struct OdfPercent : IEquatable<OdfPercent>
{
    /// <summary>
    /// Performs odf percent.
    /// 以百分比 lexical form 建立 <see cref="OdfPercent"/>。
    /// </summary>
    /// <param name="value">百分比字串，例如 <c>50%</c>、<c>.5%</c> 或 <c>-25%</c></param>
    /// <exception cref="ArgumentException">當百分比字串不是 <c>-100%</c> 到 <c>100%</c> 的格式時擲回</exception>
    public OdfPercent(string value)
    {
        if (!TryParseCore(value, allowNegative: true, out decimal percent))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfPercent_PercentageValueBetween100"), nameof(value));
        }

        Value = value;
        Percent = percent;
    }

    /// <summary>
    /// Gets the Value value.
    /// 取得原始百分比字串。
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets the Percent value.
    /// 取得百分比數值。
    /// </summary>
    /// <remarks>
    /// Decimal precision applies to this numeric view; <see cref="Value"/> preserves the exact lexical form.
    /// 此數值檢視受 decimal 精確度限制；<see cref="Value"/> 保留完整詞法形式。
    /// </remarks>
    public decimal Percent { get; }

    /// <summary>
    /// Creates from percent.
    /// 從百分比數值建立 <see cref="OdfPercent"/>。
    /// </summary>
    /// <param name="percent">百分比數值，必須介於 -100 到 100 之間</param>
    /// <returns>對應的百分比值</returns>
    public static OdfPercent FromPercent(decimal percent) => new(percent.ToString(CultureInfo.InvariantCulture) + "%");

    /// <summary>
    /// Performs the TryParse operation.
    /// 嘗試解析 0 到 100 的百分比字串。
    /// </summary>
    /// <param name="value">百分比字串</param>
    /// <param name="percent">成功時傳回解析後的百分比</param>
    /// <returns>若字串為 0 到 100 的百分比則為 <see langword="true"/></returns>
    public static bool TryParse(string? value, out OdfPercent percent) => TryParse(value, allowNegative: false, out percent);

    /// <summary>
    /// Performs the TryParse operation.
    /// 嘗試解析可選擇是否允許負值的百分比字串。
    /// </summary>
    /// <param name="value">百分比字串</param>
    /// <param name="allowNegative">是否允許負值</param>
    /// <param name="percent">成功時傳回解析後的百分比</param>
    /// <returns>若字串符合百分比範圍則為 <see langword="true"/></returns>
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
    /// Converts to string.
    /// 傳回原始百分比字串。
    /// </summary>
    /// <returns>百分比字串</returns>
    public override string ToString() => Value ?? string.Empty;

    /// <summary>
    /// Performs equals.
    /// 判斷目前值是否等於另一個百分比。
    /// </summary>
    /// <param name="other">要比較的百分比</param>
    /// <returns>若 lexical form 相同則為 <see langword="true"/></returns>
    public bool Equals(OdfPercent other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <summary>
    /// Performs equals.
    /// 執行 Equals。
    /// </summary>
    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OdfPercent other && Equals(other);

    /// <summary>
    /// Gets hash code.
    /// 取得 Hash Code。
    /// </summary>
    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);

    /// <summary>
    /// Performs equals.
    /// 判斷兩個百分比是否相等。
    /// </summary>
    /// <param name="left">左側百分比</param>
    /// <param name="right">右側百分比</param>
    /// <returns>若兩者相等則為 <see langword="true"/></returns>
    public static bool operator ==(OdfPercent left, OdfPercent right) => left.Equals(right);

    /// <summary>
    /// Performs equals.
    /// 判斷兩個百分比是否不相等。
    /// </summary>
    /// <param name="left">左側百分比</param>
    /// <param name="right">右側百分比</param>
    /// <returns>若兩者不相等則為 <see langword="true"/></returns>
    public static bool operator !=(OdfPercent left, OdfPercent right) => !left.Equals(right);

    private static bool TryParseCore(string? value, bool allowNegative, out decimal percent)
    {
        percent = 0;
        if (string.IsNullOrEmpty(value) || !global::OdfKit.Internal.OdfStringHelper.EndsWith(value!, '%'))
        {
            return false;
        }

        int end = value!.Length - 1;
        int start = value[0] == '-' ? 1 : 0;
        if (start == 1 && !allowNegative)
            return false;
        int index = start;
        while (index < end && value[index] >= '0' && value[index] <= '9')
            index++;
        int integerDigits = index - start;
        if (integerDigits > 3 || integerDigits == 3 &&
            (value[start] != '1' || value[start + 1] != '0' || value[start + 2] != '0'))
            return false;
        if (index < end)
        {
            if (value[index++] != '.')
                return false;
            int fractionStart = index;
            for (; index < end; index++)
                if (value[index] < '0' || value[index] > '9' || integerDigits == 3 && value[index] != '0')
                    return false;
            if (integerDigits == 0 && index == fractionStart)
                return false;
        }
        else if (integerDigits == 0)
            return false;
#if NETSTANDARD2_0
        return decimal.TryParse(value.Substring(0, end), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out percent);
#else
        return decimal.TryParse(value.AsSpan(0, end), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out percent);
#endif
    }
}
