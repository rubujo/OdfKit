namespace OdfKit.DOM;

/// <summary>
/// 表示 ODF schema 中名為 <c>fontFamilyGeneric</c> 的通用字型家族 token。
/// </summary>
public enum OdfFontFamilyGeneric
{
    /// <summary>
    /// 羅馬字型家族。
    /// </summary>
    Roman,

    /// <summary>
    /// 瑞士字型家族。
    /// </summary>
    Swiss,

    /// <summary>
    /// 現代字型家族。
    /// </summary>
    Modern,

    /// <summary>
    /// 裝飾字型家族。
    /// </summary>
    Decorative,

    /// <summary>
    /// 手寫字型家族。
    /// </summary>
    Script,

    /// <summary>
    /// 系統字型家族。
    /// </summary>
    System
}

/// <summary>
/// 表示 ODF schema 中名為 <c>fontPitch</c> 的字型間距 token。
/// </summary>
public enum OdfFontPitch
{
    /// <summary>
    /// 固定寬度字型間距。
    /// </summary>
    Fixed,

    /// <summary>
    /// 可變寬度字型間距。
    /// </summary>
    Variable
}

/// <summary>
/// 表示 ODF schema 中 <c>style:font-relief</c> 的字型浮雕 token。
/// </summary>
public enum OdfFontRelief
{
    /// <summary>
    /// 無字型浮雕效果。
    /// </summary>
    None,

    /// <summary>
    /// 凸起字型浮雕效果。
    /// </summary>
    Embossed,

    /// <summary>
    /// 雕刻字型浮雕效果。
    /// </summary>
    Engraved
}

/// <summary>
/// 表示 ODF schema 中 <c>svg:font-stretch</c> 的字型伸縮 token。
/// </summary>
public enum OdfFontStretch
{
    /// <summary>
    /// 一般字型伸縮。
    /// </summary>
    Normal,

    /// <summary>
    /// 超窄字型伸縮。
    /// </summary>
    UltraCondensed,

    /// <summary>
    /// 特窄字型伸縮。
    /// </summary>
    ExtraCondensed,

    /// <summary>
    /// 窄字型伸縮。
    /// </summary>
    Condensed,

    /// <summary>
    /// 半窄字型伸縮。
    /// </summary>
    SemiCondensed,

    /// <summary>
    /// 半寬字型伸縮。
    /// </summary>
    SemiExpanded,

    /// <summary>
    /// 寬字型伸縮。
    /// </summary>
    Expanded,

    /// <summary>
    /// 特寬字型伸縮。
    /// </summary>
    ExtraExpanded,

    /// <summary>
    /// 超寬字型伸縮。
    /// </summary>
    UltraExpanded
}

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
