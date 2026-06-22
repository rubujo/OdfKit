namespace OdfKit.Database;

/// <summary>
/// 表示資料庫連線的登入設定摘要（<c>db:login</c>）。
/// </summary>
/// <param name="userName">使用者名稱。</param>
/// <param name="useSystemUser">是否使用系統使用者帳號。</param>
/// <param name="isPasswordRequired">是否需要輸入密碼。</param>
/// <param name="loginTimeout">登入逾時秒數。</param>
public sealed class OdfDatabaseLoginInfo(string? userName, bool? useSystemUser, bool? isPasswordRequired, int? loginTimeout)
{
    /// <summary>
    /// 取得使用者名稱。
    /// </summary>
    public string? UserName { get; } = userName;

    /// <summary>
    /// 取得是否使用系統使用者帳號。
    /// </summary>
    public bool? UseSystemUser { get; } = useSystemUser;

    /// <summary>
    /// 取得是否需要輸入密碼。
    /// </summary>
    public bool? IsPasswordRequired { get; } = isPasswordRequired;

    /// <summary>
    /// 取得登入逾時秒數。
    /// </summary>
    public int? LoginTimeout { get; } = loginTimeout;
}
