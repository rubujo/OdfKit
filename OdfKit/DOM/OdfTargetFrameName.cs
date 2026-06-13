using System;

namespace OdfKit.DOM;

/// <summary>
/// 表示 ODF schema 中名為 <c>targetFrameName</c> 的目標框架名稱 lexical form。
/// </summary>
public readonly struct OdfTargetFrameName : IEquatable<OdfTargetFrameName>
{
    /// <summary>
    /// 以目標框架名稱 lexical form 建立 <see cref="OdfTargetFrameName"/>。
    /// </summary>
    /// <param name="value">目標框架名稱，例如 <c>_self</c>、<c>_blank</c> 或自訂框架名稱。</param>
    /// <exception cref="ArgumentException">當目標框架名稱為空白或包含控制字元時擲回。</exception>
    public OdfTargetFrameName(string value)
    {
        if (!IsValid(value))
        {
            throw new ArgumentException("目標框架名稱不可空白，且不可包含控制字元。", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// 取得原始目標框架名稱。
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// 取得值是否為 ODF schema 明列的特殊目標框架名稱。
    /// </summary>
    public bool IsReservedTarget => Value is "_self" or "_blank" or "_parent" or "_top";

    /// <summary>
    /// 嘗試解析目標框架名稱。
    /// </summary>
    /// <param name="value">目標框架名稱字串。</param>
    /// <param name="targetFrameName">成功時傳回解析後的目標框架名稱。</param>
    /// <returns>若字串符合 ODF <c>targetFrameName</c> 格式則為 <see langword="true"/>。</returns>
    public static bool TryParse(string? value, out OdfTargetFrameName targetFrameName)
    {
        if (IsValid(value))
        {
            targetFrameName = new OdfTargetFrameName(value!);
            return true;
        }

        targetFrameName = default;
        return false;
    }

    /// <summary>
    /// 傳回原始目標框架名稱。
    /// </summary>
    /// <returns>目標框架名稱。</returns>
    public override string ToString() => Value ?? string.Empty;

    /// <summary>
    /// 判斷目前值是否等於另一個目標框架名稱。
    /// </summary>
    /// <param name="other">要比較的目標框架名稱。</param>
    /// <returns>若 lexical form 相同則為 <see langword="true"/>。</returns>
    public bool Equals(OdfTargetFrameName other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OdfTargetFrameName other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);

    /// <summary>
    /// 判斷兩個目標框架名稱是否相等。
    /// </summary>
    /// <param name="left">左側目標框架名稱。</param>
    /// <param name="right">右側目標框架名稱。</param>
    /// <returns>若兩者相等則為 <see langword="true"/>。</returns>
    public static bool operator ==(OdfTargetFrameName left, OdfTargetFrameName right) => left.Equals(right);

    /// <summary>
    /// 判斷兩個目標框架名稱是否不相等。
    /// </summary>
    /// <param name="left">左側目標框架名稱。</param>
    /// <param name="right">右側目標框架名稱。</param>
    /// <returns>若兩者不相等則為 <see langword="true"/>。</returns>
    public static bool operator !=(OdfTargetFrameName left, OdfTargetFrameName right) => !left.Equals(right);

    private static bool IsValid(string? value)
    {
        if (value is null || value.Trim().Length == 0)
        {
            return false;
        }

        foreach (char ch in value)
        {
            if (char.IsControl(ch))
            {
                return false;
            }
        }

        return true;
    }
}
