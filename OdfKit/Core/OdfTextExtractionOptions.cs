namespace OdfKit.Core;

/// <summary>
/// Controls plain-text extraction from an ODF document.
/// 控制從 ODF 文件擷取純文字時所包含的內容與分隔方式。
/// </summary>
public sealed class OdfTextExtractionOptions
{
    /// <summary>
    /// Gets the default text-extraction options.
    /// 取得預設文字擷取選項。
    /// </summary>
    public static OdfTextExtractionOptions Default { get; } = new();

    /// <summary>
    /// Gets or sets whether annotation text is included.
    /// 取得或設定是否包含註解文字。
    /// </summary>
    public bool IncludeAnnotations { get; set; }

    /// <summary>
    /// Gets or sets whether tracked-change definition text, including deleted content, is included.
    /// 取得或設定是否包含追蹤修訂定義中的文字，包括已刪除內容。
    /// </summary>
    public bool IncludeTrackedChanges { get; set; }

    /// <summary>
    /// Gets or sets whether presentation notes are included.
    /// 取得或設定是否包含簡報備忘稿。
    /// </summary>
    public bool IncludePresentationNotes { get; set; }

    /// <summary>
    /// Gets or sets the separator inserted between logical blocks.
    /// 取得或設定邏輯內容區塊之間插入的分隔字串。
    /// </summary>
    public string BlockSeparator { get; set; } = "\n";
}
