namespace OdfKit;

/// <summary>
/// Configures low-magic template binding behavior.
/// 設定低魔法模板繫結行為。
/// </summary>
public sealed class OdfTemplateBindOptions
{
    /// <summary>
    /// Gets or sets whether <c>{{Items[].Field}}</c> collection placeholders are expanded.
    /// 取得或設定是否展開 <c>{{Items[].Field}}</c> 集合占位符。
    /// </summary>
    public bool ExpandCollections { get; set; } = true;

    /// <summary>
    /// Gets or sets whether <c>{{Image:Name}}</c> placeholders are replaced with images.
    /// 取得或設定是否將 <c>{{Image:Name}}</c> 占位符替換為圖片。
    /// </summary>
    public bool EnableImagePlaceholders { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of generated items for one collection placeholder.
    /// 取得或設定單一集合占位符可產生的最大項目數。
    /// </summary>
    public int MaxCollectionItems { get; set; } = 10_000;

    /// <summary>
    /// Gets or sets whether spreadsheet formulas in expanded template rows are shifted by row offset.
    /// 取得或設定試算表模板列展開時是否依列位移調整公式。
    /// </summary>
    public bool ShiftFormulas { get; set; } = true;

    /// <summary>
    /// Gets or sets whether binding only reports changes without mutating the document.
    /// 取得或設定是否只回報變更而不修改文件。
    /// </summary>
    public bool DryRun { get; set; }

    /// <summary>
    /// Gets or sets whether unresolved placeholders make the report incomplete with a strict warning.
    /// 取得或設定未解析占位符是否使報告以嚴格模式警告標記為未完成。
    /// </summary>
    public bool StrictMode { get; set; }

    /// <summary>
    /// Gets or sets the optional AOT-safe resolver for object property path segments.
    /// 取得或設定選用的 AOT-safe 物件屬性路徑片段解析器。
    /// </summary>
    public IOdfTemplateValueResolver? ValueResolver { get; set; }

    /// <summary>
    /// Gets or sets how unresolved placeholders are handled.
    /// 取得或設定如何處理未解析占位符。
    /// </summary>
    public OdfTemplateUnknownPlaceholderPolicy UnknownPlaceholderPolicy { get; set; } = OdfTemplateUnknownPlaceholderPolicy.Keep;

    /// <summary>
    /// Gets a default options instance.
    /// 取得預設選項執行個體。
    /// </summary>
    public static OdfTemplateBindOptions Default { get; } = new();
}
