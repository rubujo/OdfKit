namespace OdfKit.DOM;

/// <summary>
/// 表示 ODF schema 中 <c>fo:keep-together</c> 的段落分頁保持 token。
/// </summary>
public enum OdfFoKeepTogether
{
    /// <summary>
    /// 自動決定是否保持在同一頁或欄。
    /// </summary>
    Auto,

    /// <summary>
    /// 永遠保持在同一頁或欄。
    /// </summary>
    Always
}

/// <summary>
/// 表示 ODF schema 中 <c>fo:wrap-option</c> 的文字換行選項 token。
/// </summary>
public enum OdfFoWrapOption
{
    /// <summary>
    /// 允許換行。
    /// </summary>
    Wrap,

    /// <summary>
    /// 不允許換行。
    /// </summary>
    NoWrap
}
