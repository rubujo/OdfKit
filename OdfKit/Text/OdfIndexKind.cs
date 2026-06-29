namespace OdfKit.Text;

/// <summary>
/// Represents the high-level kind of an ODF index element.
/// 表示 ODF 索引元素的高階類型。
/// </summary>
public enum OdfIndexKind
{
    /// <summary>
    /// A table of contents (<c>text:table-of-content</c>).
    /// 目錄（<c>text:table-of-content</c>）。
    /// </summary>
    TableOfContents,

    /// <summary>
    /// An alphabetical index (<c>text:alphabetical-index</c>).
    /// 字母索引（<c>text:alphabetical-index</c>）。
    /// </summary>
    AlphabeticalIndex,

    /// <summary>
    /// A bibliography (<c>text:bibliography</c>).
    /// 文獻目錄（<c>text:bibliography</c>）。
    /// </summary>
    Bibliography,

    /// <summary>
    /// A table index (<c>text:table-index</c>).
    /// 表格索引（<c>text:table-index</c>）。
    /// </summary>
    TableIndex,
}
