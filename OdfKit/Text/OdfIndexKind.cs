namespace OdfKit.Text;

/// <summary>
/// 表示 ODF 索引元素的高階類型。
/// </summary>
public enum OdfIndexKind
{
    /// <summary>目錄（<c>text:table-of-content</c>）。</summary>
    TableOfContents,

    /// <summary>字母索引（<c>text:alphabetical-index</c>）。</summary>
    AlphabeticalIndex,

    /// <summary>文獻目錄（<c>text:bibliography</c>）。</summary>
    Bibliography,

    /// <summary>表格索引（<c>text:table-index</c>）。</summary>
    TableIndex,
}
