namespace OdfKit.Text;

/// <summary>
/// 指出反向提取出的範本欄位來源。
/// </summary>
public enum OdfExtractedFieldSource
{
    /// <summary>
    /// 由文字標記邊界提取，例如 <c>[Name]value[/Name]</c>。
    /// </summary>
    DelimitedText,

    /// <summary>
    /// 由書籤範圍提取。
    /// </summary>
    Bookmark,

    /// <summary>
    /// 由 ODF 變數或使用者輸入欄位提取。
    /// </summary>
    OdfField
}
