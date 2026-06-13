using System;
using System.Globalization;

namespace OdfKit.DOM;

/// <summary>
/// 表示 ODF schema 中名為 <c>angle</c> 的角度值。
/// </summary>
public readonly struct OdfAngle : IEquatable<OdfAngle>
{
    /// <summary>
    /// 以角度 lexical form 建立 <see cref="OdfAngle"/>。
    /// </summary>
    /// <param name="value">角度字串，例如 <c>90</c> 或 <c>-45.5</c>。</param>
    /// <exception cref="ArgumentException">當角度字串空白或包含控制字元時擲回。</exception>
    public OdfAngle(string value)
    {
        if (!IsValid(value))
        {
            throw new ArgumentException("角度值不可為空白，且不可包含控制字元。", nameof(value));
        }

        Value = value.Trim();
    }

    /// <summary>
    /// 取得原始角度字串。
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// 從度數建立 <see cref="OdfAngle"/>。
    /// </summary>
    /// <param name="degrees">度數。</param>
    /// <returns>對應的角度值。</returns>
    public static OdfAngle FromDegrees(decimal degrees) => new(degrees.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// 嘗試解析角度字串。
    /// </summary>
    /// <param name="value">角度字串。</param>
    /// <param name="angle">成功時傳回解析後的角度。</param>
    /// <returns>若角度字串可接受則為 <see langword="true"/>。</returns>
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
    /// 嘗試將目前 lexical form 解析為度數。
    /// </summary>
    /// <param name="degrees">成功時傳回度數。</param>
    /// <returns>若目前值是十進位度數則為 <see langword="true"/>。</returns>
    public bool TryGetDegrees(out decimal degrees)
    {
        return decimal.TryParse(Value, NumberStyles.Number, CultureInfo.InvariantCulture, out degrees);
    }

    /// <summary>
    /// 傳回原始角度字串。
    /// </summary>
    /// <returns>角度字串。</returns>
    public override string ToString() => Value ?? string.Empty;

    /// <summary>
    /// 判斷目前值是否等於另一個角度。
    /// </summary>
    /// <param name="other">要比較的角度。</param>
    /// <returns>若 lexical form 相同則為 <see langword="true"/>。</returns>
    public bool Equals(OdfAngle other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OdfAngle other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);

    /// <summary>
    /// 判斷兩個角度是否相等。
    /// </summary>
    /// <param name="left">左側角度。</param>
    /// <param name="right">右側角度。</param>
    /// <returns>若兩者相等則為 <see langword="true"/>。</returns>
    public static bool operator ==(OdfAngle left, OdfAngle right) => left.Equals(right);

    /// <summary>
    /// 判斷兩個角度是否不相等。
    /// </summary>
    /// <param name="left">左側角度。</param>
    /// <param name="right">右側角度。</param>
    /// <returns>若兩者不相等則為 <see langword="true"/>。</returns>
    public static bool operator !=(OdfAngle left, OdfAngle right) => !left.Equals(right);

    private static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        foreach (char ch in value!.Trim())
        {
            if (char.IsControl(ch))
            {
                return false;
            }
        }

        return true;
    }
}
