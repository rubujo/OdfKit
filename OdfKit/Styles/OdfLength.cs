using System;
using System.Globalization;

namespace OdfKit.Styles;

/// <summary>
/// 表示 ODF 長度單位的列舉。
/// </summary>
public enum OdfUnit
{
    /// <summary>
    /// 未指定單位。
    /// </summary>
    Unspecified,

    /// <summary>
    /// 公分。
    /// </summary>
    Centimeters,

    /// <summary>
    /// 公釐。
    /// </summary>
    Millimeters,

    /// <summary>
    /// 英吋。
    /// </summary>
    Inches,

    /// <summary>
    /// 點 (Points)。
    /// </summary>
    Points,

    /// <summary>
    /// 派卡 (Picas)。
    /// </summary>
    Picas,

    /// <summary>
    /// 像素。
    /// </summary>
    Pixels,

    /// <summary>
    /// 百分比。
    /// </summary>
    Percentage,

    /// <summary>
    /// Em 單位。
    /// </summary>
    Em
}

/// <summary>
/// 表示 ODF 長度的結構。
/// </summary>
/// <remarks>
/// 以指定的數值與單位建立新的 <see cref="OdfLength"/> 結構。
/// </remarks>
/// <param name="value">長度數值</param>
/// <param name="unit">長度單位</param>
public struct OdfLength(double value, OdfUnit unit) : IEquatable<OdfLength>
{
    /// <summary>
    /// 取得長度的數值。
    /// </summary>
    public double Value { get; } = value;

    /// <summary>
    /// 取得長度的單位。
    /// </summary>
    public OdfUnit Unit { get; } = unit;

    #region 工廠方法

    /// <summary>
    /// 從指定的公分值建立 <see cref="OdfLength"/> 結構。
    /// </summary>
    /// <param name="val">長度數值</param>
    /// <returns>表示該公分值的 <see cref="OdfLength"/> 結構</returns>
    public static OdfLength FromCentimeters(double val) => new(val, OdfUnit.Centimeters);

    /// <summary>
    /// 從指定的公釐值建立 <see cref="OdfLength"/> 結構。
    /// </summary>
    /// <param name="val">長度數值</param>
    /// <returns>表示該公釐值的 <see cref="OdfLength"/> 結構</returns>
    public static OdfLength FromMillimeters(double val) => new(val, OdfUnit.Millimeters);

    /// <summary>
    /// 從指定的英吋值建立 <see cref="OdfLength"/> 結構。
    /// </summary>
    /// <param name="val">長度數值</param>
    /// <returns>表示該英吋值的 <see cref="OdfLength"/> 結構</returns>
    public static OdfLength FromInches(double val) => new(val, OdfUnit.Inches);

    /// <summary>
    /// 從指定的點數值建立 <see cref="OdfLength"/> 結構。
    /// </summary>
    /// <param name="val">長度數值</param>
    /// <returns>表示該點數值的 <see cref="OdfLength"/> 結構</returns>
    public static OdfLength FromPoints(double val) => new(val, OdfUnit.Points);

    /// <summary>
    /// 從指定的像素值建立 <see cref="OdfLength"/> 結構。
    /// </summary>
    /// <param name="val">長度數值</param>
    /// <returns>表示該像素值的 <see cref="OdfLength"/> 結構</returns>
    public static OdfLength FromPixels(double val) => new(val, OdfUnit.Pixels);

    /// <summary>
    /// 從指定的百分比值建立 <see cref="OdfLength"/> 結構。
    /// </summary>
    /// <param name="val">長度數值</param>
    /// <returns>表示該百分比值的 <see cref="OdfLength"/> 結構</returns>
    public static OdfLength FromPercentage(double val) => new(val, OdfUnit.Percentage);

    /// <summary>
    /// 從指定的 Em 值建立 <see cref="OdfLength"/> 結構。
    /// </summary>
    /// <param name="val">長度數值</param>
    /// <returns>表示該 Em 值的 <see cref="OdfLength"/> 結構</returns>
    public static OdfLength FromEm(double val) => new(val, OdfUnit.Em);

    #endregion

    #region 轉換與解析

    /// <summary>
    /// 解析長度字串並傳回 <see cref="OdfLength"/> 結構。
    /// </summary>
    /// <param name="text">要解析的長度字串</param>
    /// <returns>解析後的 <see cref="OdfLength"/> 結構</returns>
    /// <exception cref="FormatException">當長度格式無效或是不支援的單位時拋出</exception>
    public static OdfLength Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new(0, OdfUnit.Unspecified);
        }

        text = text!.Trim();

        // 擷取數值部分與字尾字串
        int suffixIndex = text.Length;
        while (suffixIndex > 0 && !char.IsDigit(text[suffixIndex - 1]) && text[suffixIndex - 1] != '.' && text[suffixIndex - 1] != '-')
        {
            suffixIndex--;
        }

        string numPart = text.Substring(0, suffixIndex).Trim();
        string unitPart = text.Substring(suffixIndex).Trim().ToLowerInvariant();

        if (!double.TryParse(numPart, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
        {
            throw new FormatException($"長度中的數值格式無效：'{text}'");
        }

        OdfUnit unit = unitPart switch
        {
            "cm" => OdfUnit.Centimeters,
            "mm" => OdfUnit.Millimeters,
            "in" => OdfUnit.Inches,
            "pt" => OdfUnit.Points,
            "pc" => OdfUnit.Picas,
            "px" => OdfUnit.Pixels,
            "%" => OdfUnit.Percentage,
            "em" => OdfUnit.Em,
            "" => OdfUnit.Unspecified,
            _ => throw new FormatException($"長度中包含不受支援的單位 '{unitPart}'：'{text}'")
        };

        return new(val, unit);
    }

    /// <summary>
    /// 嘗試解析長度字串。
    /// </summary>
    /// <param name="text">要解析的長度字串</param>
    /// <param name="result">當此方法傳回時，若解析成功，則包含解析後的 <see cref="OdfLength"/>；若解析失敗，則為預設值</param>
    /// <returns>若解析成功則為 true，否則為 false</returns>
    public static bool TryParse(string? text, out OdfLength result)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                result = new(0, OdfUnit.Unspecified);
                return false;
            }
            result = Parse(text);
            return true;
        }
        catch
        {
            result = new(0, OdfUnit.Unspecified);
            return false;
        }
    }

    /// <summary>
    /// 將長度轉換為公分。
    /// </summary>
    /// <returns>轉換為公分的數值</returns>
    public double ToCentimeters() => ConvertTo(OdfUnit.Centimeters);

    /// <summary>
    /// 將長度轉換為點。
    /// </summary>
    /// <returns>轉換為點的數值</returns>
    public double ToPoints() => ConvertTo(OdfUnit.Points);

    /// <summary>
    /// 將長度轉換為英吋。
    /// </summary>
    /// <returns>轉換為英吋的數值</returns>
    public double ToInches() => ConvertTo(OdfUnit.Inches);

    /// <summary>
    /// 將長度轉換為公釐。
    /// </summary>
    /// <returns>轉換為公釐的數值</returns>
    public double ToMillimeters() => ConvertTo(OdfUnit.Millimeters);

    /// <summary>
    /// 將長度轉換為指定的目標單位。
    /// </summary>
    /// <param name="targetUnit">目標長度單位</param>
    /// <returns>轉換為目標單位的數值</returns>
    /// <exception cref="InvalidOperationException">當嘗試直接在相對單位與絕對單位之間轉換時拋出</exception>
    /// <exception cref="NotSupportedException">當長度單位轉換不支援時拋出</exception>
    public double ConvertTo(OdfUnit targetUnit)
    {
        if (Unit == targetUnit) return Value;
        if (Unit is OdfUnit.Unspecified || targetUnit is OdfUnit.Unspecified)
        {
            return Value; // 如果未指定，則視為 1:1 比率
        }

        if (Unit is OdfUnit.Percentage or OdfUnit.Em || targetUnit is OdfUnit.Percentage or OdfUnit.Em)
        {
            throw new InvalidOperationException($"無法直接將相對單位 '{Unit}' 轉換為絕對單位 '{targetUnit}'。");
        }

        // 將目前單位轉換為點 (72 點 = 1 英吋)
        double points = Unit switch
        {
            OdfUnit.Points => Value,
            OdfUnit.Centimeters => Value * 28.3464567,
            OdfUnit.Millimeters => Value * 2.83464567,
            OdfUnit.Inches => Value * 72.0,
            OdfUnit.Picas => Value * 12.0,
            OdfUnit.Pixels => Value * (72.0 / 96.0), // 假設為標準的 96 DPI
            _ => throw new NotSupportedException($"不支援自單位 {Unit} 進行轉換。")
        };

        // 將點轉換為目標單位
        return targetUnit switch
        {
            OdfUnit.Points => points,
            OdfUnit.Centimeters => points / 28.3464567,
            OdfUnit.Millimeters => points / 2.83464567,
            OdfUnit.Inches => points / 72.0,
            OdfUnit.Picas => points / 12.0,
            OdfUnit.Pixels => points / (72.0 / 96.0),
            _ => throw new NotSupportedException($"不支援轉換為單位 {targetUnit}。")
        };
    }

    /// <summary>
    /// 如果是 Unspecified 則套用 context 預設的單位進行轉譯。
    /// </summary>
    /// <param name="defaultUnit">當單位為未指定時所套用的預設長度單位</param>
    /// <returns>套用預設單位後的 <see cref="OdfLength"/> 結構</returns>
    public OdfLength FallbackTo(OdfUnit defaultUnit)
    {
        if (Unit is OdfUnit.Unspecified)
        {
            return new(Value, defaultUnit);
        }
        return this;
    }

    /// <summary>
    /// 將長度結構轉換為其字串表示法。
    /// </summary>
    /// <returns>代表目前長度結構的字串</returns>
    public override string ToString()
    {
        string unitStr = Unit switch
        {
            OdfUnit.Centimeters => "cm",
            OdfUnit.Millimeters => "mm",
            OdfUnit.Inches => "in",
            OdfUnit.Points => "pt",
            OdfUnit.Picas => "pc",
            OdfUnit.Pixels => "px",
            OdfUnit.Percentage => "%",
            OdfUnit.Em => "em",
            _ => ""
        };
        return Value.ToString(CultureInfo.InvariantCulture) + unitStr;
    }

    #endregion

    #region 隱式/顯式運算子

    /// <summary>
    /// 隱式將字串轉換為 <see cref="OdfLength"/> 結構。
    /// </summary>
    /// <param name="text">要解析的長度字串</param>
    public static implicit operator OdfLength(string text) => Parse(text);

    /// <summary>
    /// 隱式將 <see cref="OdfLength"/> 結構轉換為字串。
    /// </summary>
    /// <param name="len">長度結構</param>
    public static implicit operator string(OdfLength len) => len.ToString();

    /// <summary>
    /// 顯式將 <see cref="OdfLength"/> 結構轉換為 double。
    /// </summary>
    /// <param name="len">長度結構</param>
    public static explicit operator double(OdfLength len) => len.Value;

    #endregion

    #region 相等性比較

    /// <summary>
    /// 判斷目前的長度結構是否與另一個長度結構相等。
    /// </summary>
    /// <param name="other">要比較的另一個長度結構</param>
    /// <returns>如果兩個結構相等則為 true，否則為 false</returns>
    public bool Equals(OdfLength other)
    {
        if (Unit == other.Unit)
        {
            return Math.Abs(Value - other.Value) < 1e-9;
        }
        try
        {
            double converted = other.ConvertTo(Unit);
            return Math.Abs(Value - converted) < 1e-9;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 判斷指定的物件是否與目前的長度結構相等。
    /// </summary>
    /// <param name="obj">要比較的物件</param>
    /// <returns>如果指定的物件與目前的結構相等則為 true，否則為 false</returns>
    public override bool Equals(object? obj) => obj is OdfLength other && Equals(other);

    /// <summary>
    /// 傳回此長度結構的雜湊碼。
    /// </summary>
    /// <returns>一個 32 位元有正負號的整數雜湊碼</returns>
    public override int GetHashCode()
    {
        // 儘可能標準化為點以取得一致的雜湊
        try
        {
            if (Unit is not (OdfUnit.Percentage or OdfUnit.Em or OdfUnit.Unspecified))
            {
                double roundedPoints = Math.Round(ToPoints(), 4);
                return roundedPoints.GetHashCode() ^ OdfUnit.Points.GetHashCode();
            }
        }
        catch { }
        return Math.Round(Value, 4).GetHashCode() ^ Unit.GetHashCode();
    }

    /// <summary>
    /// 判斷兩個 <see cref="OdfLength"/> 結構是否相等。
    /// </summary>
    /// <param name="left">要比較的左側結構</param>
    /// <param name="right">要比較的右側結構</param>
    /// <returns>如果兩個結構相等則為 true，否則為 false</returns>
    public static bool operator ==(OdfLength left, OdfLength right) => left.Equals(right);

    /// <summary>
    /// 判斷兩個 <see cref="OdfLength"/> 結構是否不相等。
    /// </summary>
    /// <param name="left">要比較的左側結構</param>
    /// <param name="right">要比較的右側結構</param>
    /// <returns>如果兩個結構不相等則為 true，否則為 false</returns>
    public static bool operator !=(OdfLength left, OdfLength right) => !left.Equals(right);

    #endregion
}
