namespace OdfKit.DOM;

/// <summary>
/// 表示 ODF schema 中 <c>number:calendar</c> 的曆法 token。
/// </summary>
public enum OdfNumberCalendar
{
    /// <summary>佛曆。</summary>
    Buddhist,

    /// <summary>日本年號曆。</summary>
    Gengou,

    /// <summary>公曆。</summary>
    Gregorian,

    /// <summary>韓文漢字曆。</summary>
    Hanja,

    /// <summary>韓文漢字星期曆。</summary>
    HanjaYoil,

    /// <summary>伊斯蘭曆。</summary>
    Hijri,

    /// <summary>猶太曆。</summary>
    Jewish,

    /// <summary>民國曆。</summary>
    Roc
}

/// <summary>
/// 表示 ODF schema 中 <c>table:member-type</c> 的成員類型 token。
/// </summary>
public enum OdfTableMemberType
{
    /// <summary>具名成員。</summary>
    Named,

    /// <summary>下一個成員。</summary>
    Next,

    /// <summary>前一個成員。</summary>
    Previous
}

/// <summary>
/// 表示 ODF schema 中 <c>table:grouped-by</c> 的分組單位 token。
/// </summary>
public enum OdfTableGroupedBy
{
    /// <summary>依日分組。</summary>
    Days,

    /// <summary>依小時分組。</summary>
    Hours,

    /// <summary>依分鐘分組。</summary>
    Minutes,

    /// <summary>依月份分組。</summary>
    Months,

    /// <summary>依季度分組。</summary>
    Quarters,

    /// <summary>依秒分組。</summary>
    Seconds,

    /// <summary>依年份分組。</summary>
    Years
}

/// <summary>
/// 表示 ODF schema 中 <c>table:sort-mode</c> 的排序模式 token。
/// </summary>
public enum OdfTableSortMode
{
    /// <summary>依資料排序。</summary>
    Data,

    /// <summary>手動排序。</summary>
    Manual,

    /// <summary>依名稱排序。</summary>
    Name,

    /// <summary>不排序。</summary>
    None
}

/// <summary>
/// 表示 ODF schema 中 <c>table:condition-source</c> 的條件來源 token。
/// </summary>
public enum OdfTableConditionSource
{
    /// <summary>儲存格範圍。</summary>
    CellRange,

    /// <summary>自身。</summary>
    Self
}

/// <summary>
/// 表示 ODF schema 中 <c>anim:color-interpolation</c> 的色彩插值 token。
/// </summary>
public enum OdfAnimationColorInterpolation
{
    /// <summary>HSL 色彩空間。</summary>
    Hsl,

    /// <summary>RGB 色彩空間。</summary>
    Rgb
}

/// <summary>
/// 表示 ODF schema 中 <c>anim:color-interpolation-direction</c> 的色彩插值方向 token。
/// </summary>
public enum OdfAnimationColorInterpolationDirection
{
    /// <summary>順時針。</summary>
    Clockwise,

    /// <summary>逆時針。</summary>
    CounterClockwise
}

/// <summary>
/// 表示 ODF schema 中 <c>db:is-nullable</c> 的可空性 token。
/// </summary>
public enum OdfDatabaseIsNullable
{
    /// <summary>不允許 null。</summary>
    NoNulls,

    /// <summary>允許 null。</summary>
    Nullable
}

/// <summary>
/// 表示 ODF schema 中 <c>db:data-source-setting-type</c> 的資料來源設定型別 token。
/// </summary>
public enum OdfDatabaseDataSourceSettingType
{
    /// <summary>布林型別。</summary>
    Boolean,

    /// <summary>雙精確度浮點型別。</summary>
    Double,

    /// <summary>整數型別。</summary>
    Int,

    /// <summary>長整數型別。</summary>
    Long,

    /// <summary>短整數型別。</summary>
    Short,

    /// <summary>字串型別。</summary>
    String
}

/// <summary>
/// 表示 ODF schema 中 <c>draw:nohref</c> 的無連結 token。
/// </summary>
public enum OdfDrawNoHref
{
    /// <summary>無連結。</summary>
    Nohref
}

/// <summary>
/// 表示 ODF schema 中 <c>table:function</c> 的彙總函式 token。
/// </summary>
public enum OdfTableFunction
{
    /// <summary>自動函式。</summary>
    Auto,

    /// <summary>平均值。</summary>
    Average,

    /// <summary>計數。</summary>
    Count,

    /// <summary>數值計數。</summary>
    Countnums,

    /// <summary>最大值。</summary>
    Max,

    /// <summary>最小值。</summary>
    Min,

    /// <summary>乘積。</summary>
    Product,

    /// <summary>樣本標準差。</summary>
    Stdev,

    /// <summary>母體標準差。</summary>
    Stdevp,

    /// <summary>總和。</summary>
    Sum,

    /// <summary>樣本變異數。</summary>
    Var,

    /// <summary>母體變異數。</summary>
    Varp
}
