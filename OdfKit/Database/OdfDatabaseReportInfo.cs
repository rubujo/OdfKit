namespace OdfKit.Database;

/// <summary>
/// 表示 ODB 資料庫報表元件的摘要資訊。
/// </summary>
/// <param name="name">報表名稱（<c>db:name</c>）</param>
/// <param name="href">報表資源參照路徑（<c>xlink:href</c>）</param>
/// <param name="title">顯示標題</param>
/// <param name="description">描述文字</param>
/// <param name="asTemplate">是否作為範本（<c>db:as-template</c>）</param>
public sealed class OdfDatabaseReportInfo(
    string name,
    string? href,
    string? title,
    string? description,
    bool? asTemplate)
{
    /// <summary>
    /// 取得報表名稱。
    /// </summary>
    public string Name { get; } = name ?? string.Empty;

    /// <summary>
    /// 取得報表資源參照路徑。
    /// </summary>
    public string? Href { get; } = href;

    /// <summary>
    /// 取得顯示標題。
    /// </summary>
    public string? Title { get; } = title;

    /// <summary>
    /// 取得描述文字。
    /// </summary>
    public string? Description { get; } = description;

    /// <summary>
    /// 取得是否作為範本。
    /// </summary>
    public bool? AsTemplate { get; } = asTemplate;
}
