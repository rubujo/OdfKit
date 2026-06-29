namespace OdfKit.Text;

/// <summary>
/// Represents the kind of an ODF bookmark node.
/// 表示 ODF 書籤節點種類。
/// </summary>
public enum OdfBookmarkKind
{
    /// <summary>
    /// An inline bookmark (<c>text:bookmark</c>).
    /// 行內書籤（<c>text:bookmark</c>）。
    /// </summary>
    Inline,

    /// <summary>
    /// The start of a range bookmark (<c>text:bookmark-start</c>).
    /// 範圍書籤起點（<c>text:bookmark-start</c>）。
    /// </summary>
    RangeStart,

    /// <summary>
    /// The end of a range bookmark (<c>text:bookmark-end</c>).
    /// 範圍書籤終點（<c>text:bookmark-end</c>）。
    /// </summary>
    RangeEnd,
}
