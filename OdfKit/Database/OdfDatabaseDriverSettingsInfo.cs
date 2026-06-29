namespace OdfKit.Database;

/// <summary>
/// Represents a summary of the driver settings for a database connection (<c>db:driver-settings</c>).
/// 表示資料庫連線的驅動程式設定摘要（<c>db:driver-settings</c>）。
/// </summary>
/// <param name="showDeleted">Whether to show deleted rows. / 是否顯示已刪除的資料列。</param>
/// <param name="isFirstRowHeaderLine">Whether the first row is treated as a header line (applies to text/CSV drivers, etc.). / 第一列是否視為標頭列（適用文字／CSV 等驅動程式）。</param>
/// <param name="parameterNameSubstitution">Whether named parameters can be substituted with a <c>?</c> placeholder. / 是否支援具名參數替代為 <c>?</c> 佔位符。</param>
/// <param name="systemDriverSettings">The system driver settings string. / 系統驅動程式設定字串。</param>
/// <param name="baseDn">The base DN setting for an LDAP connection. / LDAP 連線的 base DN 設定。</param>
public sealed class OdfDatabaseDriverSettingsInfo(
    bool? showDeleted,
    bool? isFirstRowHeaderLine,
    bool? parameterNameSubstitution,
    string? systemDriverSettings,
    string? baseDn)
{
    /// <summary>
    /// Gets whether to show deleted rows.
    /// 取得是否顯示已刪除的資料列。
    /// </summary>
    public bool? ShowDeleted { get; } = showDeleted;

    /// <summary>
    /// Gets whether the first row is treated as a header line.
    /// 取得第一列是否視為標頭列。
    /// </summary>
    public bool? IsFirstRowHeaderLine { get; } = isFirstRowHeaderLine;

    /// <summary>
    /// Gets whether named parameters can be substituted with a <c>?</c> placeholder.
    /// 取得是否支援具名參數替代為 <c>?</c> 佔位符。
    /// </summary>
    public bool? ParameterNameSubstitution { get; } = parameterNameSubstitution;

    /// <summary>
    /// Gets the system driver settings string.
    /// 取得系統驅動程式設定字串。
    /// </summary>
    public string? SystemDriverSettings { get; } = systemDriverSettings;

    /// <summary>
    /// Gets the base DN setting for an LDAP connection.
    /// 取得 LDAP 連線的 base DN 設定。
    /// </summary>
    public string? BaseDn { get; } = baseDn;
}
