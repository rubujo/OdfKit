using System;
using System.Drawing;

namespace OdfKit.Styles;

/// <summary>
/// 表示 ODF 框線的唯讀結構。
/// </summary>
/// <remarks>
/// 建立新的 <see cref="OdfBorder"/> 執行個體。
/// </remarks>
/// <param name="style">框線的樣式</param>
/// <param name="width">框線的寬度</param>
/// <param name="color">框線的色彩</param>
public readonly struct OdfBorder(OdfBorder.BorderStyle style, OdfLength width, Color color) : IEquatable<OdfBorder>
{
    /// <summary>
    /// 表示框線樣式的列舉。
    /// </summary>
    public enum BorderStyle
    {
        /// <summary>
        /// 無框線。
        /// </summary>
        None,

        /// <summary>
        /// 實線框線。
        /// </summary>
        Solid,

        /// <summary>
        /// 雙線框線。
        /// </summary>
        Double,

        /// <summary>
        /// 點線框線。
        /// </summary>
        Dotted,

        /// <summary>
        /// 虛線框線。
        /// </summary>
        Dashed
    }

    /// <summary>
    /// 取得框線的樣式。
    /// </summary>
    public BorderStyle Style { get; } = style;

    /// <summary>
    /// 取得框線的寬度。
    /// </summary>
    public OdfLength Width { get; } = width;

    /// <summary>
    /// 取得框線的色彩。
    /// </summary>
    public Color Color { get; } = color;

    /// <summary>
    /// 取得一個表示無框線的 <see cref="OdfBorder"/> 結構。
    /// </summary>
    public static OdfBorder None => new(BorderStyle.None, new(0, OdfUnit.Unspecified), Color.Empty);

    /// <summary>
    /// 解析框線字串並傳回 <see cref="OdfBorder"/> 結構。
    /// </summary>
    /// <param name="borderString">要解析的框線字串</param>
    /// <returns>解析後的 <see cref="OdfBorder"/> 結構</returns>
    public static OdfBorder Parse(string borderString)
    {
        if (string.IsNullOrWhiteSpace(borderString) || borderString == "none")
            return None;

        string[] parts = borderString.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        OdfLength width = new(0, OdfUnit.Unspecified);
        BorderStyle style = BorderStyle.Solid;
        Color color = Color.Black;

        foreach (var part in parts)
        {
            if (part.StartsWith("#"))
            {
                try
                {
                    if (part.Length == 7)
                    {
                        int r = Convert.ToInt32(part.Substring(1, 2), 16);
                        int g = Convert.ToInt32(part.Substring(3, 2), 16);
                        int b = Convert.ToInt32(part.Substring(5, 2), 16);
                        color = Color.FromArgb(r, g, b);
                    }
                }
                catch
                {
                    color = Color.Black;
                }
            }
            else if (Enum.TryParse<BorderStyle>(part, true, out var parsedStyle))
            {
                style = parsedStyle;
            }
            else if (OdfLength.TryParse(part, out var parsedLength))
            {
                width = parsedLength;
            }
        }
        return new(style, width, color);
    }

    /// <summary>
    /// 將目前的框線結構轉換為其字串表示法。
    /// </summary>
    /// <returns>代表目前結構的字串</returns>
    public override string ToString()
    {
        if (Style == BorderStyle.None)
            return "none";
        string hexColor = $"#{Color.R:X2}{Color.G:X2}{Color.B:X2}";
        return $"{Width.ToString()} {Style.ToString().ToLowerInvariant()} {hexColor}";
    }

    /// <summary>
    /// 判斷目前的框線結構是否與另一個框線結構相等。
    /// </summary>
    /// <param name="other">要比較的另一個框線結構</param>
    /// <returns>如果兩個結構相等則為 true，否則為 false</returns>
    public bool Equals(OdfBorder other) => Style == other.Style && Width.Equals(other.Width) && Color.ToArgb() == other.Color.ToArgb();

    /// <summary>
    /// 判斷指定的物件是否與目前的框線結構相等。
    /// </summary>
    /// <param name="obj">要比較的物件</param>
    /// <returns>如果指定的物件與目前的結構相等則為 true，否則為 false</returns>
    public override bool Equals(object? obj) => obj is OdfBorder other && Equals(other);

    /// <summary>
    /// 傳回此框線結構的雜湊碼。
    /// </summary>
    /// <returns>一個 32 位元有正負號的整數雜湊碼</returns>
    public override int GetHashCode()
    {
        int hash = 17;
        hash = hash * 23 + Style.GetHashCode();
        hash = hash * 23 + Width.GetHashCode();
        hash = hash * 23 + Color.ToArgb().GetHashCode();
        return hash;
    }

    /// <summary>
    /// 判斷兩個 <see cref="OdfBorder"/> 結構是否相等。
    /// </summary>
    /// <param name="left">要比較的左側結構</param>
    /// <param name="right">要比較的右側結構</param>
    /// <returns>如果兩個結構相等則為 true，否則為 false</returns>
    public static bool operator ==(OdfBorder left, OdfBorder right) => left.Equals(right);

    /// <summary>
    /// 判斷兩個 <see cref="OdfBorder"/> 結構是否不相等。
    /// </summary>
    /// <param name="left">要比較的左側結構</param>
    /// <param name="right">要比較的右側結構</param>
    /// <returns>如果兩個結構不相等則為 true，否則為 false</returns>
    public static bool operator !=(OdfBorder left, OdfBorder right) => !left.Equals(right);
}
