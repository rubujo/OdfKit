namespace OdfKit.Database;

/// <summary>
/// Represents a summary of the login settings for a database connection (<c>db:login</c>).
/// 表示資料庫連線的登入設定摘要（<c>db:login</c>）。
/// </summary>
/// <param name="userName">The user name. / 使用者名稱。</param>
/// <param name="useSystemUser">Whether to use the system user account. / 是否使用系統使用者帳號。</param>
/// <param name="isPasswordRequired">Whether a password is required. / 是否需要輸入密碼。</param>
/// <param name="loginTimeout">The login timeout in seconds. / 登入逾時秒數。</param>
public sealed class OdfDatabaseLoginInfo(string? userName, bool? useSystemUser, bool? isPasswordRequired, int? loginTimeout)
{
    /// <summary>
    /// Gets the user name.
    /// 取得使用者名稱。
    /// </summary>
    public string? UserName { get; } = userName;

    /// <summary>
    /// Gets whether to use the system user account.
    /// 取得是否使用系統使用者帳號。
    /// </summary>
    public bool? UseSystemUser { get; } = useSystemUser;

    /// <summary>
    /// Gets whether a password is required.
    /// 取得是否需要輸入密碼。
    /// </summary>
    public bool? IsPasswordRequired { get; } = isPasswordRequired;

    /// <summary>
    /// Gets the login timeout in seconds.
    /// 取得登入逾時秒數。
    /// </summary>
    public int? LoginTimeout { get; } = loginTimeout;
}
