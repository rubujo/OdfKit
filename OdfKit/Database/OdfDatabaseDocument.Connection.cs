using System.Globalization;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Database;

public partial class OdfDatabaseDocument
{
    /// <summary>
    /// Gets the login settings of the current data source connection.
    /// 取得目前資料來源連線的登入設定。
    /// </summary>
    /// <returns>The login settings summary, or <see langword="null"/> if not set. / 登入設定摘要；若未設定則為 <see langword="null"/>。</returns>
    public OdfDatabaseLoginInfo? GetLogin()
    {
        OdfNode? dataSource = FindChildElement(GetDatabaseNode(), "data-source", DatabaseNamespace);
        OdfNode? connectionData = dataSource is null ? null : FindChildElement(dataSource, "connection-data", DatabaseNamespace);
        OdfNode? login = connectionData is null ? null : FindChildElement(connectionData, "login", DatabaseNamespace);
        if (login is null)
        {
            return null;
        }

        return new OdfDatabaseLoginInfo(
            login.GetAttribute("user-name", DatabaseNamespace),
            ParseNullableBoolean(login.GetAttribute("use-system-user", DatabaseNamespace)),
            ParseNullableBoolean(login.GetAttribute("is-password-required", DatabaseNamespace)),
            int.TryParse(login.GetAttribute("login-timeout", DatabaseNamespace), out int timeout) ? timeout : null);
    }

    /// <summary>
    /// Sets the login settings of the data source connection.
    /// 設定資料來源連線的登入設定。
    /// </summary>
    /// <param name="userName">The optional user name. / 選用的使用者名稱。</param>
    /// <param name="useSystemUser">The optional system user account setting. / 選用的系統使用者帳號設定。</param>
    /// <param name="isPasswordRequired">The optional password-required setting. / 選用的密碼必填設定。</param>
    /// <param name="loginTimeout">The optional login timeout in seconds. / 選用的登入逾時秒數。</param>
    /// <returns>The added or updated login settings node. / 新增或更新後的登入設定節點。</returns>
    public OdfNode SetLogin(
        string? userName = null,
        bool? useSystemUser = null,
        bool? isPasswordRequired = null,
        int? loginTimeout = null)
    {
        OdfNode dataSource = FindOrCreateDataSource();
        OdfNode connectionData = FindOrCreateChild(dataSource, "connection-data", DatabaseNamespace, "db");
        OdfNode login = FindOrCreateChild(connectionData, "login", DatabaseNamespace, "db");

        if (!string.IsNullOrWhiteSpace(userName))
        {
            login.SetAttribute("user-name", DatabaseNamespace, userName!, "db");
        }

        if (useSystemUser is not null)
        {
            login.SetAttribute("use-system-user", DatabaseNamespace, useSystemUser.Value ? "true" : "false", "db");
        }

        if (isPasswordRequired is not null)
        {
            login.SetAttribute("is-password-required", DatabaseNamespace, isPasswordRequired.Value ? "true" : "false", "db");
        }

        if (loginTimeout is not null)
        {
            login.SetAttribute("login-timeout", DatabaseNamespace, loginTimeout.Value.ToString(CultureInfo.InvariantCulture), "db");
        }

        return login;
    }

    /// <summary>
    /// Gets the driver settings of the current data source connection.
    /// 取得目前資料來源連線的驅動程式設定。
    /// </summary>
    /// <returns>The driver settings summary, or <see langword="null"/> if not set. / 驅動程式設定摘要；若未設定則為 <see langword="null"/>。</returns>
    public OdfDatabaseDriverSettingsInfo? GetDriverSettings()
    {
        OdfNode? dataSource = FindChildElement(GetDatabaseNode(), "data-source", DatabaseNamespace);
        OdfNode? applicationSettings = dataSource is null
            ? null
            : FindChildElement(dataSource, "application-connection-settings", DatabaseNamespace);
        OdfNode? driverSettings = applicationSettings is null
            ? null
            : FindChildElement(applicationSettings, "driver-settings", DatabaseNamespace);
        if (driverSettings is null)
        {
            return null;
        }

        return new OdfDatabaseDriverSettingsInfo(
            ParseNullableBoolean(driverSettings.GetAttribute("show-deleted", DatabaseNamespace)),
            ParseNullableBoolean(driverSettings.GetAttribute("is-first-row-header-line", DatabaseNamespace)),
            ParseNullableBoolean(driverSettings.GetAttribute("parameter-name-substitution", DatabaseNamespace)),
            driverSettings.GetAttribute("system-driver-settings", DatabaseNamespace),
            driverSettings.GetAttribute("base-dn", DatabaseNamespace));
    }

    /// <summary>
    /// Sets the driver settings of the data source connection.
    /// 設定資料來源連線的驅動程式設定。
    /// </summary>
    /// <param name="showDeleted">The optional show-deleted-rows setting. / 選用的顯示已刪除資料列設定。</param>
    /// <param name="isFirstRowHeaderLine">The optional first-row-as-header setting. / 選用的第一列視為標頭列設定。</param>
    /// <param name="parameterNameSubstitution">The optional named parameter substitution setting. / 選用的具名參數替代設定。</param>
    /// <param name="systemDriverSettings">The optional system driver settings string. / 選用的系統驅動程式設定字串。</param>
    /// <param name="baseDn">The optional LDAP base DN setting. / 選用的 LDAP base DN 設定。</param>
    /// <returns>The added or updated driver settings node. / 新增或更新後的驅動程式設定節點。</returns>
    public OdfNode SetDriverSettings(
        bool? showDeleted = null,
        bool? isFirstRowHeaderLine = null,
        bool? parameterNameSubstitution = null,
        string? systemDriverSettings = null,
        string? baseDn = null)
    {
        OdfNode dataSource = FindOrCreateDataSource();
        OdfNode applicationSettings = FindOrCreateChild(dataSource, "application-connection-settings", DatabaseNamespace, "db");
        OdfNode driverSettings = FindOrCreateChild(applicationSettings, "driver-settings", DatabaseNamespace, "db");

        if (showDeleted is not null)
        {
            driverSettings.SetAttribute("show-deleted", DatabaseNamespace, showDeleted.Value ? "true" : "false", "db");
        }

        if (isFirstRowHeaderLine is not null)
        {
            driverSettings.SetAttribute("is-first-row-header-line", DatabaseNamespace, isFirstRowHeaderLine.Value ? "true" : "false", "db");
        }

        if (parameterNameSubstitution is not null)
        {
            driverSettings.SetAttribute("parameter-name-substitution", DatabaseNamespace, parameterNameSubstitution.Value ? "true" : "false", "db");
        }

        if (!string.IsNullOrWhiteSpace(systemDriverSettings))
        {
            driverSettings.SetAttribute("system-driver-settings", DatabaseNamespace, systemDriverSettings!, "db");
        }

        if (!string.IsNullOrWhiteSpace(baseDn))
        {
            driverSettings.SetAttribute("base-dn", DatabaseNamespace, baseDn!, "db");
        }

        return driverSettings;
    }
}
