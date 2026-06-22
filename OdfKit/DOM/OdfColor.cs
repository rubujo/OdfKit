using System;

using OdfKit.Compliance;
namespace OdfKit.DOM;

/// <summary>
/// 表示 ODF schema 中名為 <c>color</c> 的六位十六進位色彩值。
/// </summary>
public readonly struct OdfColor : IEquatable<OdfColor>
{
    /// <summary>
    /// 以色彩 lexical form 建立 <see cref="OdfColor"/>。
    /// </summary>
    /// <param name="value">色彩字串，例如 <c>#ffcc00</c></param>
    /// <exception cref="ArgumentException">當色彩字串不是 <c>#RRGGBB</c> 格式時擲回</exception>
    public OdfColor(string value)
    {
        if (!IsValid(value))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfColor_ColorValuesRrggbbFormat"), nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// 取得原始色彩字串。
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// 嘗試解析色彩字串。
    /// </summary>
    /// <param name="value">色彩字串</param>
    /// <param name="color">成功時傳回解析後的色彩</param>
    /// <returns>若字串是 <c>#RRGGBB</c> 格式則為 <see langword="true"/></returns>
    public static bool TryParse(string? value, out OdfColor color)
    {
        if (IsValid(value))
        {
            color = new OdfColor(value!);
            return true;
        }

        color = default;
        return false;
    }

    /// <summary>
    /// 從紅、綠、藍色彩通道建立 <see cref="OdfColor"/>。
    /// </summary>
    /// <param name="red">紅色通道</param>
    /// <param name="green">綠色通道</param>
    /// <param name="blue">藍色通道</param>
    /// <returns>對應的色彩值</returns>
    public static OdfColor FromRgb(byte red, byte green, byte blue) => new($"#{red:x2}{green:x2}{blue:x2}");

    /// <summary>
    /// 傳回原始色彩字串。
    /// </summary>
    /// <returns>色彩字串</returns>
    public override string ToString() => Value ?? string.Empty;

    /// <summary>
    /// 判斷目前值是否等於另一個色彩。
    /// </summary>
    /// <param name="other">要比較的色彩</param>
    /// <returns>若 lexical form 相同則為 <see langword="true"/></returns>
    public bool Equals(OdfColor other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OdfColor other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);

    /// <summary>
    /// 判斷兩個色彩是否相等。
    /// </summary>
    /// <param name="left">左側色彩</param>
    /// <param name="right">右側色彩</param>
    /// <returns>若兩者相等則為 <see langword="true"/></returns>
    public static bool operator ==(OdfColor left, OdfColor right) => left.Equals(right);

    /// <summary>
    /// 判斷兩個色彩是否不相等。
    /// </summary>
    /// <param name="left">左側色彩</param>
    /// <param name="right">右側色彩</param>
    /// <returns>若兩者不相等則為 <see langword="true"/></returns>
    public static bool operator !=(OdfColor left, OdfColor right) => !left.Equals(right);

    private static bool IsValid(string? value)
    {
        if (value is null || value.Length != 7 || value[0] != '#')
        {
            return false;
        }

        for (int i = 1; i < value.Length; i++)
        {
            char ch = value[i];
            bool isHex =
                ch >= '0' && ch <= '9' ||
                ch >= 'a' && ch <= 'f' ||
                ch >= 'A' && ch <= 'F';
            if (!isHex)
            {
                return false;
            }
        }

        return true;
    }
}
