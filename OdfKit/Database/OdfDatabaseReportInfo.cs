namespace OdfKit.Database;

/// <summary>
/// Represents summary information for an ODB database report component.
/// 表示 ODB 資料庫報表元件的摘要資訊。
/// </summary>
/// <param name="name">The report name (<c>db:name</c>). / 報表名稱（<c>db:name</c>）。</param>
/// <param name="href">The report resource reference path (<c>xlink:href</c>). / 報表資源參照路徑（<c>xlink:href</c>）。</param>
/// <param name="title">The display title. / 顯示標題。</param>
/// <param name="description">The description text. / 描述文字。</param>
/// <param name="asTemplate">Whether it is used as a template (<c>db:as-template</c>). / 是否作為範本（<c>db:as-template</c>）。</param>
public sealed class OdfDatabaseReportInfo(
    string name,
    string? href,
    string? title,
    string? description,
    bool? asTemplate)
{
    /// <summary>
    /// Gets the report name.
    /// 取得報表名稱。
    /// </summary>
    public string Name { get; } = name ?? string.Empty;

    /// <summary>
    /// Gets the report resource reference path.
    /// 取得報表資源參照路徑。
    /// </summary>
    public string? Href { get; } = href;

    /// <summary>
    /// Gets the display title.
    /// 取得顯示標題。
    /// </summary>
    public string? Title { get; } = title;

    /// <summary>
    /// Gets the description text.
    /// 取得描述文字。
    /// </summary>
    public string? Description { get; } = description;

    /// <summary>
    /// Gets whether it is used as a template.
    /// 取得是否作為範本。
    /// </summary>
    public bool? AsTemplate { get; } = asTemplate;
}
