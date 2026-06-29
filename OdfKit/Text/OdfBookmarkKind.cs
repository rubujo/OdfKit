namespace OdfKit.Text;

/// <summary>
/// Represents odf bookmark kind.
/// 表示 ODF 書籤節點種類。
/// </summary>
public enum OdfBookmarkKind
{
    /// <summary>
    /// 行內書籤（<c>text:bookmark</c>）。
    /// </summary>
    Inline,

    /// <summary>
    /// 範圍書籤起點（<c>text:bookmark-start</c>）。
    /// </summary>
    RangeStart,

    /// <summary>
    /// 範圍書籤終點（<c>text:bookmark-end</c>）。
    /// </summary>
    RangeEnd,
}
