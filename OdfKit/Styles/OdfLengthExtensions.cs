namespace OdfKit.Styles;

/// <summary>
/// 提供以數字建立 <see cref="OdfLength"/> 的擴充方法。
/// </summary>
public static class OdfLengthExtensions
{
    /// <summary>
    /// 將 double 數值轉換為公分長度。
    /// </summary>
    /// <param name="value">公分數值</param>
    /// <returns>對應的 <see cref="OdfLength"/></returns>
    public static OdfLength Cm(this double value) => OdfLength.FromCentimeters(value);

    /// <summary>
    /// 將 double 數值轉換為公釐長度。
    /// </summary>
    /// <param name="value">公釐數值</param>
    /// <returns>對應的 <see cref="OdfLength"/></returns>
    public static OdfLength Mm(this double value) => OdfLength.FromMillimeters(value);

    /// <summary>
    /// 將 double 數值轉換為點數長度。
    /// </summary>
    /// <param name="value">點數數值</param>
    /// <returns>對應的 <see cref="OdfLength"/></returns>
    public static OdfLength Pt(this double value) => OdfLength.FromPoints(value);

    /// <summary>
    /// 將 int 數值轉換為公分長度。
    /// </summary>
    /// <param name="value">公分數值</param>
    /// <returns>對應的 <see cref="OdfLength"/></returns>
    public static OdfLength Cm(this int value) => OdfLength.FromCentimeters(value);

    /// <summary>
    /// 將 int 數值轉換為公釐長度。
    /// </summary>
    /// <param name="value">公釐數值</param>
    /// <returns>對應的 <see cref="OdfLength"/></returns>
    public static OdfLength Mm(this int value) => OdfLength.FromMillimeters(value);

    /// <summary>
    /// 將 int 數值轉換為點數長度。
    /// </summary>
    /// <param name="value">點數數值</param>
    /// <returns>對應的 <see cref="OdfLength"/></returns>
    public static OdfLength Pt(this int value) => OdfLength.FromPoints(value);
}
