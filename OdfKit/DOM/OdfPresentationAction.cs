namespace OdfKit.DOM;

/// <summary>
/// 表示 ODF schema 中 <c>presentation:action</c> 的簡報動作 token。
/// </summary>
public enum OdfPresentationAction
{
    /// <summary>
    /// 無動作。
    /// </summary>
    None,

    /// <summary>
    /// 前一頁。
    /// </summary>
    PreviousPage,

    /// <summary>
    /// 下一頁。
    /// </summary>
    NextPage,

    /// <summary>
    /// 第一頁。
    /// </summary>
    FirstPage,

    /// <summary>
    /// 最後一頁。
    /// </summary>
    LastPage,

    /// <summary>
    /// 隱藏。
    /// </summary>
    Hide,

    /// <summary>
    /// 停止。
    /// </summary>
    Stop,

    /// <summary>
    /// 執行。
    /// </summary>
    Execute,

    /// <summary>
    /// 顯示。
    /// </summary>
    Show,

    /// <summary>
    /// 動詞動作。
    /// </summary>
    Verb,

    /// <summary>
    /// 淡出。
    /// </summary>
    FadeOut,

    /// <summary>
    /// 聲音。
    /// </summary>
    Sound,

    /// <summary>
    /// 上次瀏覽頁。
    /// </summary>
    LastVisitedPage
}
