namespace OdfKit.Drawing;

/// <summary>
/// 表示繪圖頁面上一個群組圖形的摘要資訊。
/// </summary>
/// <param name="pageName">所在繪圖頁面名稱</param>
/// <param name="id">群組識別碼</param>
/// <param name="name">群組名稱（可選）</param>
public sealed class OdfGroupInfo(string pageName, string id, string? name)
{
    /// <summary>
    /// 取得所在繪圖頁面名稱。
    /// </summary>
    public string PageName { get; } = pageName ?? string.Empty;

    /// <summary>
    /// 取得群組識別碼。
    /// </summary>
    public string Id { get; } = id ?? string.Empty;

    /// <summary>
    /// 取得群組名稱。
    /// </summary>
    public string? Name { get; } = name;
}
