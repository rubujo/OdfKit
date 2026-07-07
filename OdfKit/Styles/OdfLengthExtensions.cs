namespace OdfKit.Styles;

/// <summary>
/// Provides the OdfLengthExtensions API.
/// 提供以數字建立 <see cref="OdfLength"/> 的擴充方法。
/// </summary>
public static class OdfLengthExtensions
{
    /// <summary>
    /// Executes the Cm operation.
    /// 將 double 數值轉換為公分長度。
    /// </summary>
    /// <param name="value">公分數值</param>
    /// <returns>對應的 <see cref="OdfLength"/></returns>
    public static OdfLength Cm(this double value) => OdfLength.FromCentimeters(value);

    /// <summary>
    /// Executes the Mm operation.
    /// 將 double 數值轉換為公釐長度。
    /// </summary>
    /// <param name="value">公釐數值</param>
    /// <returns>對應的 <see cref="OdfLength"/></returns>
    public static OdfLength Mm(this double value) => OdfLength.FromMillimeters(value);

    /// <summary>
    /// Executes the Pt operation.
    /// 將 double 數值轉換為點數長度。
    /// </summary>
    /// <param name="value">點數數值</param>
    /// <returns>對應的 <see cref="OdfLength"/></returns>
    public static OdfLength Pt(this double value) => OdfLength.FromPoints(value);

    /// <summary>
    /// Executes the In operation.
    /// 將 double 數值轉換為英吋長度。
    /// </summary>
    /// <param name="value">英吋數值</param>
    /// <returns>對應的 <see cref="OdfLength"/></returns>
    public static OdfLength In(this double value) => OdfLength.FromInches(value);

    /// <summary>
    /// Executes the Pc operation.
    /// 將 double 數值轉換為派卡長度。
    /// </summary>
    /// <param name="value">派卡數值</param>
    /// <returns>對應的 <see cref="OdfLength"/></returns>
    public static OdfLength Pc(this double value) => OdfLength.FromPicas(value);

    /// <summary>
    /// Executes the Px operation.
    /// 將 double 數值轉換為像素長度。
    /// </summary>
    /// <param name="value">像素數值</param>
    /// <returns>對應的 <see cref="OdfLength"/></returns>
    public static OdfLength Px(this double value) => OdfLength.FromPixels(value);

    /// <summary>
    /// Executes the Percent operation.
    /// 將 double 數值轉換為百分比長度。
    /// </summary>
    /// <param name="value">百分比數值</param>
    /// <returns>對應的 <see cref="OdfLength"/></returns>
    public static OdfLength Percent(this double value) => OdfLength.FromPercentage(value);

    /// <summary>
    /// Executes the Em operation.
    /// 將 double 數值轉換為 Em 長度。
    /// </summary>
    /// <param name="value">Em 數值</param>
    /// <returns>對應的 <see cref="OdfLength"/></returns>
    public static OdfLength Em(this double value) => OdfLength.FromEm(value);

    /// <summary>
    /// Executes the Cm operation.
    /// 將 int 數值轉換為公分長度。
    /// </summary>
    /// <param name="value">公分數值</param>
    /// <returns>對應的 <see cref="OdfLength"/></returns>
    public static OdfLength Cm(this int value) => OdfLength.FromCentimeters(value);

    /// <summary>
    /// Executes the Mm operation.
    /// 將 int 數值轉換為公釐長度。
    /// </summary>
    /// <param name="value">公釐數值</param>
    /// <returns>對應的 <see cref="OdfLength"/></returns>
    public static OdfLength Mm(this int value) => OdfLength.FromMillimeters(value);

    /// <summary>
    /// Executes the Pt operation.
    /// 將 int 數值轉換為點數長度。
    /// </summary>
    /// <param name="value">點數數值</param>
    /// <returns>對應的 <see cref="OdfLength"/></returns>
    public static OdfLength Pt(this int value) => OdfLength.FromPoints(value);

    /// <summary>
    /// Executes the In operation.
    /// 將 int 數值轉換為英吋長度。
    /// </summary>
    /// <param name="value">英吋數值</param>
    /// <returns>對應的 <see cref="OdfLength"/></returns>
    public static OdfLength In(this int value) => OdfLength.FromInches(value);

    /// <summary>
    /// Executes the Pc operation.
    /// 將 int 數值轉換為派卡長度。
    /// </summary>
    /// <param name="value">派卡數值</param>
    /// <returns>對應的 <see cref="OdfLength"/></returns>
    public static OdfLength Pc(this int value) => OdfLength.FromPicas(value);

    /// <summary>
    /// Executes the Px operation.
    /// 將 int 數值轉換為像素長度。
    /// </summary>
    /// <param name="value">像素數值</param>
    /// <returns>對應的 <see cref="OdfLength"/></returns>
    public static OdfLength Px(this int value) => OdfLength.FromPixels(value);

    /// <summary>
    /// Executes the Percent operation.
    /// 將 int 數值轉換為百分比長度。
    /// </summary>
    /// <param name="value">百分比數值</param>
    /// <returns>對應的 <see cref="OdfLength"/></returns>
    public static OdfLength Percent(this int value) => OdfLength.FromPercentage(value);

    /// <summary>
    /// Executes the Em operation.
    /// 將 int 數值轉換為 Em 長度。
    /// </summary>
    /// <param name="value">Em 數值</param>
    /// <returns>對應的 <see cref="OdfLength"/></returns>
    public static OdfLength Em(this int value) => OdfLength.FromEm(value);
}
