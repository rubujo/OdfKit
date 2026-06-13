namespace OdfKit.DOM;

/// <summary>
/// 表示 ODF schema 中 <c>fo:text-transform</c> 的大小寫轉換 token。
/// </summary>
public enum OdfFoTextTransform
{
    /// <summary>首字大寫。</summary>
    Capitalize,

    /// <summary>小寫。</summary>
    Lowercase,

    /// <summary>不轉換。</summary>
    None,

    /// <summary>大寫。</summary>
    Uppercase
}

/// <summary>
/// 表示 ODF schema 中 <c>style:text-rotation-scale</c> 的文字旋轉縮放 token。
/// </summary>
public enum OdfStyleTextRotationScale
{
    /// <summary>固定縮放。</summary>
    Fixed,

    /// <summary>依行高縮放。</summary>
    LineHeight
}

/// <summary>
/// 表示 ODF schema 中 <c>style:text-combine</c> 的文字組合 token。
/// </summary>
public enum OdfStyleTextCombine
{
    /// <summary>依字母組合。</summary>
    Letters,

    /// <summary>依行組合。</summary>
    Lines,

    /// <summary>不組合。</summary>
    None
}

/// <summary>
/// 表示 ODF schema 中 <c>draw:fill</c> 的繪圖填滿 token。
/// </summary>
public enum OdfDrawFill
{
    /// <summary>不填滿。</summary>
    None,

    /// <summary>純色填滿。</summary>
    Solid,

    /// <summary>陰影線填滿。</summary>
    Hatch,

    /// <summary>漸層填滿。</summary>
    Gradient,

    /// <summary>點陣圖填滿。</summary>
    Bitmap
}

/// <summary>
/// 表示 ODF schema 中 <c>smil:fill</c> 的動畫填滿行為 token。
/// </summary>
public enum OdfSmilFill
{
    /// <summary>移除動畫效果。</summary>
    Remove,

    /// <summary>凍結動畫效果。</summary>
    Freeze,

    /// <summary>保留動畫效果。</summary>
    Hold,

    /// <summary>轉場填滿行為。</summary>
    Transition,

    /// <summary>自動填滿行為。</summary>
    Auto,

    /// <summary>預設填滿行為。</summary>
    Default
}

/// <summary>
/// 表示 ODF schema 中 <c>draw:fill-image-ref-point</c> 的填滿圖片參照點 token。
/// </summary>
public enum OdfDrawFillImageRefPoint
{
    /// <summary>左上。</summary>
    TopLeft,

    /// <summary>上方。</summary>
    Top,

    /// <summary>右上。</summary>
    TopRight,

    /// <summary>左側。</summary>
    Left,

    /// <summary>中央。</summary>
    Center,

    /// <summary>右側。</summary>
    Right,

    /// <summary>左下。</summary>
    BottomLeft,

    /// <summary>下方。</summary>
    Bottom,

    /// <summary>右下。</summary>
    BottomRight
}

/// <summary>
/// 表示 ODF schema 中 <c>draw:color-mode</c> 的色彩模式 token。
/// </summary>
public enum OdfDrawColorMode
{
    /// <summary>灰階模式。</summary>
    Greyscale,

    /// <summary>單色模式。</summary>
    Mono,

    /// <summary>標準色彩模式。</summary>
    Standard,

    /// <summary>浮水印模式。</summary>
    Watermark
}

/// <summary>
/// 表示 ODF schema 中 <c>style:vertical-pos</c> 的垂直位置 token。
/// </summary>
public enum OdfStyleVerticalPos
{
    /// <summary>置於下方。</summary>
    Below,

    /// <summary>底部。</summary>
    Bottom,

    /// <summary>從頂端起算。</summary>
    FromTop,

    /// <summary>中間。</summary>
    Middle,

    /// <summary>頂部。</summary>
    Top
}

/// <summary>
/// 表示 ODF schema 中 <c>style:vertical-rel</c> 的垂直相對基準 token。
/// </summary>
public enum OdfStyleVerticalRel
{
    /// <summary>基線。</summary>
    Baseline,

    /// <summary>字元。</summary>
    Char,

    /// <summary>框架。</summary>
    Frame,

    /// <summary>框架內容。</summary>
    FrameContent,

    /// <summary>行。</summary>
    Line,

    /// <summary>頁面。</summary>
    Page,

    /// <summary>頁面內容。</summary>
    PageContent,

    /// <summary>頁面內容底部。</summary>
    PageContentBottom,

    /// <summary>頁面內容頂部。</summary>
    PageContentTop,

    /// <summary>段落。</summary>
    Paragraph,

    /// <summary>段落內容。</summary>
    ParagraphContent,

    /// <summary>文字。</summary>
    Text
}

/// <summary>
/// 表示 ODF schema 中 <c>style:horizontal-pos</c> 的水平位置 token。
/// </summary>
public enum OdfStyleHorizontalPos
{
    /// <summary>置中。</summary>
    Center,

    /// <summary>從內側起算。</summary>
    FromInside,

    /// <summary>從左側起算。</summary>
    FromLeft,

    /// <summary>內側。</summary>
    Inside,

    /// <summary>左側。</summary>
    Left,

    /// <summary>外側。</summary>
    Outside,

    /// <summary>右側。</summary>
    Right
}

/// <summary>
/// 表示 ODF schema 中 <c>style:horizontal-rel</c> 的水平相對基準 token。
/// </summary>
public enum OdfStyleHorizontalRel
{
    /// <summary>字元。</summary>
    Char,

    /// <summary>框架。</summary>
    Frame,

    /// <summary>框架內容。</summary>
    FrameContent,

    /// <summary>框架結束邊界。</summary>
    FrameEndMargin,

    /// <summary>框架起始邊界。</summary>
    FrameStartMargin,

    /// <summary>頁面。</summary>
    Page,

    /// <summary>頁面內容。</summary>
    PageContent,

    /// <summary>頁面結束邊界。</summary>
    PageEndMargin,

    /// <summary>頁面起始邊界。</summary>
    PageStartMargin,

    /// <summary>段落。</summary>
    Paragraph,

    /// <summary>段落內容。</summary>
    ParagraphContent,

    /// <summary>段落結束邊界。</summary>
    ParagraphEndMargin,

    /// <summary>段落起始邊界。</summary>
    ParagraphStartMargin
}

/// <summary>
/// 表示 ODF schema 中 <c>style:wrap</c> 的文繞圖 token。
/// </summary>
public enum OdfStyleWrap
{
    /// <summary>最大側繞排。</summary>
    Biggest,

    /// <summary>動態繞排。</summary>
    Dynamic,

    /// <summary>左側繞排。</summary>
    Left,

    /// <summary>不繞排。</summary>
    None,

    /// <summary>平行繞排。</summary>
    Parallel,

    /// <summary>右側繞排。</summary>
    Right,

    /// <summary>穿越繞排。</summary>
    RunThrough
}

/// <summary>
/// 表示 ODF schema 中 <c>style:run-through</c> 的穿越排列 token。
/// </summary>
public enum OdfStyleRunThrough
{
    /// <summary>背景。</summary>
    Background,

    /// <summary>前景。</summary>
    Foreground
}

/// <summary>
/// 表示 ODF schema 中 <c>style:wrap-contour-mode</c> 的輪廓繞排模式 token。
/// </summary>
public enum OdfStyleWrapContourMode
{
    /// <summary>完整輪廓。</summary>
    Full,

    /// <summary>外側輪廓。</summary>
    Outside
}

/// <summary>
/// 表示 ODF schema 中 <c>draw:stroke-linejoin</c> 的線條接合 token。
/// </summary>
public enum OdfDrawStrokeLineJoin
{
    /// <summary>斜角接合。</summary>
    Bevel,

    /// <summary>中間接合。</summary>
    Middle,

    /// <summary>尖角接合。</summary>
    Miter,

    /// <summary>無接合。</summary>
    None,

    /// <summary>圓角接合。</summary>
    Round
}

/// <summary>
/// 表示 ODF schema 中 <c>svg:stroke-linecap</c> 的線端樣式 token。
/// </summary>
public enum OdfSvgStrokeLineCap
{
    /// <summary>平頭線端。</summary>
    Butt,

    /// <summary>圓形線端。</summary>
    Round,

    /// <summary>方形線端。</summary>
    Square
}
