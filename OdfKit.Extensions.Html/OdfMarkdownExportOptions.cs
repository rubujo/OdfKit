namespace OdfKit.Export;

/// <summary>
/// Markdown 方言。
/// </summary>
public enum OdfMarkdownFlavor
{
    /// <summary>
    /// GitHub Flavored Markdown。包含 pipe table，為預設值。
    /// </summary>
    GitHubFlavored,

    /// <summary>
    /// GitLab Flavored Markdown。包含 pipe table 與刪除線語法，並保留日後 GitLab 特有語法的擴充點。
    /// </summary>
    GitLabFlavored,

    /// <summary>
    /// CommonMark 相容輸出，避免 GitHub/GitLab-only table 語法。
    /// </summary>
    CommonMark,

    /// <summary>
    /// 基礎 Markdown，盡量避免 inline HTML。
    /// </summary>
    Basic,
}

/// <summary>
/// Markdown 匯出的選項設定。
/// </summary>
public sealed class OdfMarkdownExportOptions
{
    /// <summary>
    /// GitHub Flavored Markdown 匯出 preset。
    /// </summary>
    public static OdfMarkdownExportOptions GitHub { get; } = new() { Flavor = OdfMarkdownFlavor.GitHubFlavored };

    /// <summary>
    /// GitLab Flavored Markdown 匯出 preset。
    /// </summary>
    public static OdfMarkdownExportOptions GitLab { get; } = new() { Flavor = OdfMarkdownFlavor.GitLabFlavored };

    /// <summary>
    /// CommonMark 相容匯出 preset。
    /// </summary>
    public static OdfMarkdownExportOptions CommonMark { get; } = new() { Flavor = OdfMarkdownFlavor.CommonMark };

    /// <summary>
    /// 基礎 Markdown 匯出 preset。
    /// </summary>
    public static OdfMarkdownExportOptions Basic { get; } = new() { Flavor = OdfMarkdownFlavor.Basic };

    /// <summary>
    /// 取得或設定輸出的 Markdown 方言，預設為 GitHub Flavored Markdown。
    /// </summary>
    public OdfMarkdownFlavor Flavor { get; init; } = OdfMarkdownFlavor.GitHubFlavored;

    /// <summary>
    /// 取得或設定是否在段落與區塊之間使用空白行，預設為 true。
    /// </summary>
    public bool BlankLineBetweenBlocks { get; init; } = true;

    internal bool UsePipeTables => Flavor is OdfMarkdownFlavor.GitHubFlavored or OdfMarkdownFlavor.GitLabFlavored;

    internal bool AllowInlineHtml => Flavor != OdfMarkdownFlavor.Basic;

    internal bool UseTildeStrikethrough => Flavor is OdfMarkdownFlavor.GitHubFlavored or OdfMarkdownFlavor.GitLabFlavored;
}
