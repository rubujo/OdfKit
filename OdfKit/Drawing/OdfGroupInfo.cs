namespace OdfKit.Drawing;

/// <summary>
/// Represents summary information for a group shape on a drawing page.
/// 表示繪圖頁面上一個群組圖形的摘要資訊。
/// </summary>
/// <param name="pageName">The name of the drawing page. / 所在繪圖頁面名稱。</param>
/// <param name="id">The group identifier. / 群組識別碼。</param>
/// <param name="name">The optional group name. / 群組名稱（可選）。</param>
public sealed class OdfGroupInfo(string pageName, string id, string? name)
{
    /// <summary>
    /// Gets the name of the drawing page.
    /// 取得所在繪圖頁面名稱。
    /// </summary>
    public string PageName { get; } = pageName ?? string.Empty;

    /// <summary>
    /// Gets the group identifier.
    /// 取得群組識別碼。
    /// </summary>
    public string Id { get; } = id ?? string.Empty;

    /// <summary>
    /// Gets the group name.
    /// 取得群組名稱。
    /// </summary>
    public string? Name { get; } = name;
}
