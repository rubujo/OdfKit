using System;
using System.Globalization;

using OdfKit.Compliance;
namespace OdfKit.DOM;

/// <summary>
/// Represents an ODF angle with an optional degree, grad or radian unit.
/// 表示可選擇度、百分度或弧度單位的 ODF 角度值。
/// </summary>
public readonly struct OdfAngle : IEquatable<OdfAngle>
{
    /// <summary>
    /// Creates an angle from its numeric lexical form and optional unit.
    /// 以數值詞法形式及可選單位建立角度。
    /// </summary>
    /// <param name="value">An angle such as <c>90</c> or <c>-45.5deg</c>. / 角度字串，例如 <c>90</c> 或 <c>-45.5deg</c>。</param>
    /// <exception cref="ArgumentException">The value is not an ODF angle. / 值不是合法 ODF 角度。</exception>
    public OdfAngle(string value)
    {
        if (!IsValid(value))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfAngle_AngleCannotBeEmpty"), nameof(value));
        }

        Value = value.Trim();
    }

    /// <summary>
    /// Gets the angle lexical form with surrounding XML whitespace removed.
    /// 取得移除前後 XML 空白後的角度詞法形式。
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Creates from degrees.
    /// 從度數建立 <see cref="OdfAngle"/>。
    /// </summary>
    /// <param name="degrees">度數</param>
    /// <returns>對應的角度值</returns>
    public static OdfAngle FromDegrees(decimal degrees) => new(degrees.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// Performs the TryParse operation.
    /// 嘗試解析角度字串。
    /// </summary>
    /// <param name="value">角度字串</param>
    /// <param name="angle">成功時傳回解析後的角度</param>
    /// <returns>若角度字串可接受則為 <see langword="true"/></returns>
    public static bool TryParse(string? value, out OdfAngle angle)
    {
        if (IsValid(value))
        {
            angle = new OdfAngle(value!);
            return true;
        }

        angle = default;
        return false;
    }

    /// <summary>
    /// Converts the angle to decimal degrees when its value is representable.
    /// 在數值可表示時，將角度轉換為 decimal 度數。
    /// </summary>
    /// <param name="degrees">The degree value on success. / 成功時傳回度數。</param>
    /// <returns>Whether conversion fits decimal precision and range. / 轉換是否可由 decimal 的精確度與範圍表示。</returns>
    /// <remarks>
    /// Radian conversion uses a decimal approximation of pi; the lexical value remains unchanged.
    /// 弧度轉換使用 decimal 的圓周率近似值；原始詞法值不變。
    /// </remarks>
    public bool TryGetDegrees(out decimal degrees)
    {
        degrees = 0;
        if (Value is null)
            return false;
        bool grads = Value.EndsWith("grad", StringComparison.Ordinal);
        bool radians = Value.EndsWith("rad", StringComparison.Ordinal) && !grads;
        int suffixLength = grads ? 4 : radians || Value.EndsWith("deg", StringComparison.Ordinal) ? 3 : 0;
        const NumberStyles styles = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent;
#if NETSTANDARD2_0
        bool parsed = decimal.TryParse(Value.Substring(0, Value.Length - suffixLength), styles, CultureInfo.InvariantCulture, out decimal number);
#else
        bool parsed = decimal.TryParse(Value.AsSpan(0, Value.Length - suffixLength), styles, CultureInfo.InvariantCulture, out decimal number);
#endif
        if (!parsed)
            return false;
        try
        {
            degrees = grads ? number * 0.9m : radians ? number / 3.1415926535897932384626433833m * 180m : number;
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    /// <summary>
    /// Converts to string.
    /// 傳回原始角度字串。
    /// </summary>
    /// <returns>角度字串</returns>
    public override string ToString() => Value ?? string.Empty;

    /// <summary>
    /// Performs equals.
    /// 判斷目前值是否等於另一個角度。
    /// </summary>
    /// <param name="other">要比較的角度</param>
    /// <returns>若 lexical form 相同則為 <see langword="true"/></returns>
    public bool Equals(OdfAngle other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <summary>
    /// Performs equals.
    /// 執行 Equals。
    /// </summary>
    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OdfAngle other && Equals(other);

    /// <summary>
    /// Gets hash code.
    /// 取得 Hash Code。
    /// </summary>
    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);

    /// <summary>
    /// Performs equals.
    /// 判斷兩個角度是否相等。
    /// </summary>
    /// <param name="left">左側角度</param>
    /// <param name="right">右側角度</param>
    /// <returns>若兩者相等則為 <see langword="true"/></returns>
    public static bool operator ==(OdfAngle left, OdfAngle right) => left.Equals(right);

    /// <summary>
    /// Performs equals.
    /// 判斷兩個角度是否不相等。
    /// </summary>
    /// <param name="left">左側角度</param>
    /// <param name="right">右側角度</param>
    /// <returns>若兩者不相等則為 <see langword="true"/></returns>
    public static bool operator !=(OdfAngle left, OdfAngle right) => !left.Equals(right);

    private static bool IsValid(string? value)
    {
        if (value is null)
            return false;
        int start = 0;
        int end = value.Length;
        while (start < end && IsXmlWhitespace(value[start]))
            start++;
        while (end > start && IsXmlWhitespace(value[end - 1]))
            end--;
        if (end - start >= 4 && string.CompareOrdinal(value, end - 4, "grad", 0, 4) == 0)
            end -= 4;
        else if (end - start >= 3 &&
            (string.CompareOrdinal(value, end - 3, "deg", 0, 3) == 0 || string.CompareOrdinal(value, end - 3, "rad", 0, 3) == 0))
            end -= 3;
        int index = start;
        if (index < end && value[index] is '+' or '-')
            index++;
        int integerStart = index;
        while (index < end && IsAsciiDigit(value[index]))
            index++;
        bool hasInteger = index > integerStart;
        if (index < end && value[index] == '.')
        {
            int fractionStart = ++index;
            while (index < end && IsAsciiDigit(value[index]))
                index++;
            if (index == fractionStart)
                return false;
        }
        else if (!hasInteger)
            return false;
        if (index < end && value[index] is 'e' or 'E')
        {
            index++;
            if (index < end && value[index] is '+' or '-')
                index++;
            int exponentStart = index;
            while (index < end && IsAsciiDigit(value[index]))
                index++;
            if (index == exponentStart)
                return false;
        }
        return index == end;
    }

    private static bool IsAsciiDigit(char value) => value is >= '0' and <= '9';

    private static bool IsXmlWhitespace(char value) => value is ' ' or '\t' or '\r' or '\n';
}
