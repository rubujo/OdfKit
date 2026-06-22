namespace OdfKit.DOM;

#region Visual Tokens - Presentation & Stroke

/// <summary>
/// 表示 ODF schema 中 <c>style:horizontal-rel</c> 的水平相對基準 token。
/// </summary>
public enum OdfStyleHorizontalRel
{
    /// <summary>
    /// 字元
    /// </summary>
    Char,

    /// <summary>
    /// 框架
    /// </summary>
    Frame,

    /// <summary>
    /// 框架內容
    /// </summary>
    FrameContent,

    /// <summary>
    /// 框架結束邊界
    /// </summary>
    FrameEndMargin,

    /// <summary>
    /// 框架起始邊界
    /// </summary>
    FrameStartMargin,

    /// <summary>
    /// 頁面
    /// </summary>
    Page,

    /// <summary>
    /// 頁面內容
    /// </summary>
    PageContent,

    /// <summary>
    /// 頁面結束邊界
    /// </summary>
    PageEndMargin,

    /// <summary>
    /// 頁面起始邊界
    /// </summary>
    PageStartMargin,

    /// <summary>
    /// 段落
    /// </summary>
    Paragraph,

    /// <summary>
    /// 段落內容
    /// </summary>
    ParagraphContent,

    /// <summary>
    /// 段落結束邊界
    /// </summary>
    ParagraphEndMargin,

    /// <summary>
    /// 段落起始邊界
    /// </summary>
    ParagraphStartMargin
}

/// <summary>
/// 表示 ODF schema 中 <c>style:writing-mode</c> 的書寫方向 token。
/// </summary>
public enum OdfStyleWritingMode
{
    /// <summary>
    /// 由左至右
    /// </summary>
    Lr,

    /// <summary>
    /// 由左至右、由上至下
    /// </summary>
    LrTb,

    /// <summary>
    /// 依頁面設定
    /// </summary>
    Page,

    /// <summary>
    /// 由右至左
    /// </summary>
    Rl,

    /// <summary>
    /// 由右至左、由上至下
    /// </summary>
    RlTb,

    /// <summary>
    /// 側向由左至右
    /// </summary>
    SidewaysLr,

    /// <summary>
    /// 側向由右至左
    /// </summary>
    SidewaysRl,

    /// <summary>
    /// 由上至下
    /// </summary>
    Tb,

    /// <summary>
    /// 由上至下、由左至右
    /// </summary>
    TbLr,

    /// <summary>
    /// 由上至下、由右至左
    /// </summary>
    TbRl
}

/// <summary>
/// 表示 ODF schema 中 <c>style:wrap</c> 的文繞圖 token。
/// </summary>
public enum OdfStyleWrap
{
    /// <summary>
    /// 最大側繞排
    /// </summary>
    Biggest,

    /// <summary>
    /// 動態繞排
    /// </summary>
    Dynamic,

    /// <summary>
    /// 左側繞排
    /// </summary>
    Left,

    /// <summary>
    /// 不繞排
    /// </summary>
    None,

    /// <summary>
    /// 平行繞排
    /// </summary>
    Parallel,

    /// <summary>
    /// 右側繞排
    /// </summary>
    Right,

    /// <summary>
    /// 穿越繞排
    /// </summary>
    RunThrough
}

/// <summary>
/// 表示 ODF schema 中 <c>style:run-through</c> 的穿越排列 token。
/// </summary>
public enum OdfStyleRunThrough
{
    /// <summary>
    /// 背景
    /// </summary>
    Background,

    /// <summary>
    /// 前景
    /// </summary>
    Foreground
}

/// <summary>
/// 表示 ODF schema 中 <c>style:wrap-contour-mode</c> 的輪廓繞排模式 token。
/// </summary>
public enum OdfStyleWrapContourMode
{
    /// <summary>
    /// 完整輪廓
    /// </summary>
    Full,

    /// <summary>
    /// 外側輪廓
    /// </summary>
    Outside
}

/// <summary>
/// 表示 ODF schema 中 <c>table:display-member-mode</c> 的成員顯示方向 token。
/// </summary>
public enum OdfTableDisplayMemberMode
{
    /// <summary>
    /// 由下方開始顯示
    /// </summary>
    FromBottom,

    /// <summary>
    /// 由上方開始顯示
    /// </summary>
    FromTop
}

/// <summary>
/// 表示 ODF schema 中 <c>table:layout-mode</c> 的版面配置模式 token。
/// </summary>
public enum OdfTableLayoutMode
{
    /// <summary>
    /// 大綱小計在下方
    /// </summary>
    OutlineSubtotalsBottom,

    /// <summary>
    /// 大綱小計在上方
    /// </summary>
    OutlineSubtotalsTop,

    /// <summary>
    /// 表格版面配置
    /// </summary>
    TabularLayout
}

/// <summary>
/// 表示 ODF schema 中資料庫參照動作規則的 token。
/// </summary>
public enum OdfDatabaseRule
{
    /// <summary>
    /// 連鎖處理
    /// </summary>
    Cascade,

    /// <summary>
    /// 不執行動作
    /// </summary>
    NoAction,

    /// <summary>
    /// 限制動作
    /// </summary>
    Restrict,

    /// <summary>
    /// 設為預設值
    /// </summary>
    SetDefault,

    /// <summary>
    /// 設為 null
    /// </summary>
    SetNull
}

/// <summary>
/// 表示 ODF schema 中 <c>presentation:preset-class</c> 的預設動畫類別 token。
/// </summary>
public enum OdfPresentationPresetClass
{
    /// <summary>
    /// 自訂類別
    /// </summary>
    Custom,

    /// <summary>
    /// 強調類別
    /// </summary>
    Emphasis,

    /// <summary>
    /// 進入類別
    /// </summary>
    Entrance,

    /// <summary>
    /// 離開類別
    /// </summary>
    Exit,

    /// <summary>
    /// 媒體呼叫類別
    /// </summary>
    MediaCall,

    /// <summary>
    /// 動作路徑類別
    /// </summary>
    MotionPath,

    /// <summary>
    /// OLE 動作類別
    /// </summary>
    OleAction
}

/// <summary>
/// 表示 ODF schema 中 <c>number:transliteration-style</c> 的音譯樣式 token。
/// </summary>
public enum OdfNumberTransliterationStyle
{
    /// <summary>
    /// 長格式
    /// </summary>
    Long,

    /// <summary>
    /// 中格式
    /// </summary>
    Medium,

    /// <summary>
    /// 短格式
    /// </summary>
    Short
}

/// <summary>
/// 表示 ODF schema 中 <c>style:script-type</c> 的文字系統類型 token。
/// </summary>
public enum OdfStyleScriptType
{
    /// <summary>
    /// 亞洲文字系統
    /// </summary>
    Asian,

    /// <summary>
    /// 複雜文字系統
    /// </summary>
    Complex,

    /// <summary>
    /// 忽略文字系統
    /// </summary>
    Ignore,

    /// <summary>
    /// 拉丁文字系統
    /// </summary>
    Latin
}

/// <summary>
/// 表示 ODF schema 中 <c>style:text-emphasize</c> 的文字強調標記 token。
/// </summary>
public enum OdfStyleTextEmphasize
{
    /// <summary>
    /// 置於上方
    /// </summary>
    Above,

    /// <summary>
    /// 重音標記
    /// </summary>
    Accent,

    /// <summary>
    /// 置於下方
    /// </summary>
    Below,

    /// <summary>
    /// 圓圈標記
    /// </summary>
    Circle,

    /// <summary>
    /// 實心圓標記
    /// </summary>
    Disc,

    /// <summary>
    /// 點標記
    /// </summary>
    Dot,

    /// <summary>
    /// 無強調標記
    /// </summary>
    None
}

/// <summary>
/// 表示 ODF schema 中 <c>draw:stroke-linejoin</c> 的線條接合 token。
/// </summary>
public enum OdfDrawStrokeLineJoin
{
    /// <summary>
    /// 斜角接合
    /// </summary>
    Bevel,

    /// <summary>
    /// 中間接合
    /// </summary>
    Middle,

    /// <summary>
    /// 尖角接合
    /// </summary>
    Miter,

    /// <summary>
    /// 無接合
    /// </summary>
    None,

    /// <summary>
    /// 圓角接合
    /// </summary>
    Round
}

/// <summary>
/// 表示 ODF schema 中 <c>svg:stroke-linecap</c> 的線端樣式 token。
/// </summary>
public enum OdfSvgStrokeLineCap
{
    /// <summary>
    /// 平頭線端
    /// </summary>
    Butt,

    /// <summary>
    /// 圓形線端
    /// </summary>
    Round,

    /// <summary>
    /// 方形線端
    /// </summary>
    Square
}

#endregion
