namespace OdfKit.Text;

/// <summary>
/// Defines high-level node categories emitted by the streaming ODT reader.
/// 表示 ODT 流式讀取器目前讀取到的文字元素類型。
/// </summary>
public enum OdtNodeType
{
    /// <summary>
    /// 一般段落。
    /// </summary>
    Paragraph,

    /// <summary>
    /// 標題段落。
    /// </summary>
    Heading,

    /// <summary>
    /// 清單專案。
    /// </summary>
    ListItem,

    /// <summary>
    /// 表格儲存格。
    /// </summary>
    TableCell,

    /// <summary>
    /// 其他未分類元素。
    /// </summary>
    Other
}
