namespace OdfKit.Database;

/// <summary>
/// 表示資料庫連線的驅動程式設定摘要（<c>db:driver-settings</c>）。
/// </summary>
/// <param name="showDeleted">是否顯示已刪除的資料列。</param>
/// <param name="isFirstRowHeaderLine">第一列是否視為標頭列（適用文字／CSV 等驅動程式）。</param>
/// <param name="parameterNameSubstitution">是否支援具名參數替代為 <c>?</c> 佔位符。</param>
/// <param name="systemDriverSettings">系統驅動程式設定字串。</param>
/// <param name="baseDn">LDAP 連線的 base DN 設定。</param>
public sealed class OdfDatabaseDriverSettingsInfo(
    bool? showDeleted,
    bool? isFirstRowHeaderLine,
    bool? parameterNameSubstitution,
    string? systemDriverSettings,
    string? baseDn)
{
    /// <summary>
    /// 取得是否顯示已刪除的資料列。
    /// </summary>
    public bool? ShowDeleted { get; } = showDeleted;

    /// <summary>
    /// 取得第一列是否視為標頭列。
    /// </summary>
    public bool? IsFirstRowHeaderLine { get; } = isFirstRowHeaderLine;

    /// <summary>
    /// 取得是否支援具名參數替代為 <c>?</c> 佔位符。
    /// </summary>
    public bool? ParameterNameSubstitution { get; } = parameterNameSubstitution;

    /// <summary>
    /// 取得系統驅動程式設定字串。
    /// </summary>
    public string? SystemDriverSettings { get; } = systemDriverSettings;

    /// <summary>
    /// 取得 LDAP 連線的 base DN 設定。
    /// </summary>
    public string? BaseDn { get; } = baseDn;
}
