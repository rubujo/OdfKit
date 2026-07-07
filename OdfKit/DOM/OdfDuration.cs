using System;
using System.Xml;

using OdfKit.Compliance;
namespace OdfKit.DOM;

/// <summary>
/// 表示 XML Schema <c>duration</c> 值，並保留原始 lexical form。
/// </summary>
public readonly struct OdfDuration : IEquatable<OdfDuration>
{
    /// <summary>
    /// Executes the OdfDuration operation.
    /// 以 XML Schema <c>duration</c> 字串建立 <see cref="OdfDuration"/>。
    /// </summary>
    /// <param name="value">duration 字串，例如 <c>PT1H30M</c></param>
    /// <exception cref="ArgumentException">當字串不是有效 XML Schema <c>duration</c> 時擲回</exception>
    public OdfDuration(string value)
    {
        if (!IsValid(value))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDuration_DurationValidXmlSchema"), nameof(value));
        }

        Value = value.Trim();
    }

    /// <summary>
    /// Gets the Value value.
    /// 取得原始 duration 字串。
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Executes the TryParse operation.
    /// 嘗試解析 XML Schema <c>duration</c> 字串。
    /// </summary>
    /// <param name="value">要解析的字串</param>
    /// <param name="duration">成功時傳回解析後的 duration</param>
    /// <returns>若字串是有效 XML Schema <c>duration</c> 則為 <see langword="true"/></returns>
    public static bool TryParse(string? value, out OdfDuration duration)
    {
        if (IsValid(value))
        {
            duration = new OdfDuration(value!);
            return true;
        }

        duration = default;
        return false;
    }

    /// <summary>
    /// Executes the TryGetTimeSpan operation.
    /// 嘗試轉換為 <see cref="TimeSpan"/>。
    /// </summary>
    /// <param name="timeSpan">成功時傳回對應的 <see cref="TimeSpan"/></param>
    /// <returns>若目前 duration 可由 <see cref="TimeSpan"/> 表示則為 <see langword="true"/></returns>
    public bool TryGetTimeSpan(out TimeSpan timeSpan)
    {
        try
        {
            timeSpan = XmlConvert.ToTimeSpan(Value);
            return true;
        }
        catch (FormatException)
        {
            timeSpan = default;
            return false;
        }
        catch (OverflowException)
        {
            timeSpan = default;
            return false;
        }
    }

    /// <summary>
    /// Executes the ToString operation.
    /// 傳回原始 duration 字串。
    /// </summary>
    /// <returns>duration 字串</returns>
    public override string ToString() => Value ?? string.Empty;

    /// <summary>
    /// Executes the Equals operation.
    /// 判斷目前值是否等於另一個 duration。
    /// </summary>
    /// <param name="other">要比較的 duration</param>
    /// <returns>若 lexical form 相同則為 <see langword="true"/></returns>
    public bool Equals(OdfDuration other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <summary>
    /// Executes the Equals operation.
    /// 執行 Equals 作業。
    /// </summary>
    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OdfDuration other && Equals(other);

    /// <summary>
    /// Executes the GetHashCode operation.
    /// 執行 GetHashCode 作業。
    /// </summary>
    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);

    /// <summary>
    /// Executes the Equals operation.
    /// 判斷兩個 duration 是否相等。
    /// </summary>
    /// <param name="left">左側 duration</param>
    /// <param name="right">右側 duration</param>
    /// <returns>若兩者相等則為 <see langword="true"/></returns>
    public static bool operator ==(OdfDuration left, OdfDuration right) => left.Equals(right);

    /// <summary>
    /// Executes the Equals operation.
    /// 判斷兩個 duration 是否不相等。
    /// </summary>
    /// <param name="left">左側 duration</param>
    /// <param name="right">右側 duration</param>
    /// <returns>若兩者不相等則為 <see langword="true"/></returns>
    public static bool operator !=(OdfDuration left, OdfDuration right) => !left.Equals(right);

    private static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            XmlConvert.ToTimeSpan(value!.Trim());
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (OverflowException)
        {
            return false;
        }
    }
}
