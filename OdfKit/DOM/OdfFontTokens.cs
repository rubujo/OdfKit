namespace OdfKit.DOM;

/// <summary>
/// 表示 ODF schema 中名為 <c>fontStyle</c> 的字型樣式 token。
/// </summary>
public enum OdfFontStyle
{
    /// <summary>
    /// 一般字型樣式。
    /// </summary>
    Normal,

    /// <summary>
    /// 斜體字型樣式。
    /// </summary>
    Italic,

    /// <summary>
    /// 傾斜字型樣式。
    /// </summary>
    Oblique
}

/// <summary>
/// 表示 ODF schema 中名為 <c>fontVariant</c> 的字型變體 token。
/// </summary>
public enum OdfFontVariant
{
    /// <summary>
    /// 一般字型變體。
    /// </summary>
    Normal,

    /// <summary>
    /// 小型大寫字母變體。
    /// </summary>
    SmallCaps
}

/// <summary>
/// 表示 ODF schema 中名為 <c>fontWeight</c> 的字型粗細 token。
/// </summary>
public enum OdfFontWeight
{
    /// <summary>
    /// 一般字型粗細。
    /// </summary>
    Normal,

    /// <summary>
    /// 粗體字型粗細。
    /// </summary>
    Bold,

    /// <summary>
    /// <c>100</c> 字型粗細。
    /// </summary>
    Weight100,

    /// <summary>
    /// <c>200</c> 字型粗細。
    /// </summary>
    Weight200,

    /// <summary>
    /// <c>300</c> 字型粗細。
    /// </summary>
    Weight300,

    /// <summary>
    /// <c>400</c> 字型粗細。
    /// </summary>
    Weight400,

    /// <summary>
    /// <c>500</c> 字型粗細。
    /// </summary>
    Weight500,

    /// <summary>
    /// <c>600</c> 字型粗細。
    /// </summary>
    Weight600,

    /// <summary>
    /// <c>700</c> 字型粗細。
    /// </summary>
    Weight700,

    /// <summary>
    /// <c>800</c> 字型粗細。
    /// </summary>
    Weight800,

    /// <summary>
    /// <c>900</c> 字型粗細。
    /// </summary>
    Weight900
}
