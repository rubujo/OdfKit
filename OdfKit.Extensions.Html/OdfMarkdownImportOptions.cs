namespace OdfKit.Export;

/// <summary>
/// Markdown 匯入的選項設定。
/// </summary>
public sealed class OdfMarkdownImportOptions
{
    /// <summary>
    /// GitHub Flavored Markdown 匯入 preset。
    /// </summary>
    public static OdfMarkdownImportOptions GitHub { get; } = new() { Flavor = OdfMarkdownFlavor.GitHubFlavored };

    /// <summary>
    /// GitLab Flavored Markdown 匯入 preset。
    /// </summary>
    public static OdfMarkdownImportOptions GitLab { get; } = new() { Flavor = OdfMarkdownFlavor.GitLabFlavored };

    /// <summary>
    /// CommonMark 相容匯入 preset。
    /// </summary>
    public static OdfMarkdownImportOptions CommonMark { get; } = new() { Flavor = OdfMarkdownFlavor.CommonMark };

    /// <summary>
    /// 基礎 Markdown 匯入 preset。
    /// </summary>
    public static OdfMarkdownImportOptions Basic { get; } = new() { Flavor = OdfMarkdownFlavor.Basic };

    /// <summary>
    /// 取得或設定輸入 Markdown 的方言，預設為 GitHub Flavored Markdown。
    /// </summary>
    public OdfMarkdownFlavor Flavor { get; init; } = OdfMarkdownFlavor.GitHubFlavored;

    internal bool AcceptPipeTables => Flavor is OdfMarkdownFlavor.GitHubFlavored or OdfMarkdownFlavor.GitLabFlavored;

    internal bool AcceptInlineHtml => Flavor != OdfMarkdownFlavor.Basic;

    internal bool AcceptTildeStrikethrough => Flavor is OdfMarkdownFlavor.GitHubFlavored or OdfMarkdownFlavor.GitLabFlavored;
}
