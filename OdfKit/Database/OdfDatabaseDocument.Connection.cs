using System.Globalization;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Database;
/// <summary>
/// Provides the OdfDatabaseDocument API.
/// 提供 OdfDatabaseDocument API。
/// </summary>

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
    /// Short overload of SetLogin that uses default values for all optional parameters and forwards to the full overload.
    /// 便利多載：SetLogin 的所有可選參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public OdfNode SetLogin() => SetLogin(null, null, null, null);

    /// <summary>
    /// Short overload of SetLogin that accepts userName; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 userName；其餘可選參數使用預設值並轉呼叫最長 SetLogin 多載。
    /// </summary>
    public OdfNode SetLogin(string? userName) => SetLogin(userName, null, null, null);

    /// <summary>
    /// Short overload of SetLogin that accepts userName and useSystemUser; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 userName 與 useSystemUser；其餘可選參數使用預設值並轉呼叫最長 SetLogin 多載。
    /// </summary>
    public OdfNode SetLogin(string? userName, bool? useSystemUser) => SetLogin(userName, useSystemUser, null, null);

    /// <summary>
    /// Short overload of SetLogin that accepts userName, useSystemUser, and isPasswordRequired; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 userName、useSystemUser 與 isPasswordRequired；其餘可選參數使用預設值並轉呼叫最長 SetLogin 多載。
    /// </summary>
    public OdfNode SetLogin(string? userName, bool? useSystemUser, bool? isPasswordRequired) => SetLogin(userName, useSystemUser, isPasswordRequired, null);


    /// <summary>
    /// Sets the login settings of the data source connection.
    /// 設定資料來源連線的登入設定。
    /// </summary>
    /// <param name="userName">The optional user name. / 選用的使用者名稱。</param>
    /// <param name="useSystemUser">The optional system user account setting. / 選用的系統使用者帳號設定。</param>
    /// <param name="isPasswordRequired">The optional password-required setting. / 選用的密碼必填設定。</param>
    /// <param name="loginTimeout">The optional login timeout in seconds. / 選用的登入逾時秒數。</param>
    /// <returns>The added or updated login settings node. / 新增或更新後的登入設定節點。</returns>
    public OdfNode SetLogin(string? userName, bool? useSystemUser, bool? isPasswordRequired, int? loginTimeout)
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
    /// Short overload of SetDriverSettings that uses default values for all optional parameters and forwards to the full overload.
    /// 便利多載：SetDriverSettings 的所有可選參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public OdfNode SetDriverSettings() => SetDriverSettings(null, null, null, null, null);

    /// <summary>
    /// Short overload of SetDriverSettings that accepts showDeleted; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 showDeleted；其餘可選參數使用預設值並轉呼叫最長 SetDriverSettings 多載。
    /// </summary>
    public OdfNode SetDriverSettings(bool? showDeleted) => SetDriverSettings(showDeleted, null, null, null, null);

    /// <summary>
    /// Short overload of SetDriverSettings that accepts showDeleted and isFirstRowHeaderLine; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 showDeleted 與 isFirstRowHeaderLine；其餘可選參數使用預設值並轉呼叫最長 SetDriverSettings 多載。
    /// </summary>
    public OdfNode SetDriverSettings(bool? showDeleted, bool? isFirstRowHeaderLine) => SetDriverSettings(showDeleted, isFirstRowHeaderLine, null, null, null);

    /// <summary>
    /// Short overload of SetDriverSettings that accepts showDeleted, isFirstRowHeaderLine, and parameterNameSubstitution; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 showDeleted、isFirstRowHeaderLine 與 parameterNameSubstitution；其餘可選參數使用預設值並轉呼叫最長 SetDriverSettings 多載。
    /// </summary>
    public OdfNode SetDriverSettings(bool? showDeleted, bool? isFirstRowHeaderLine, bool? parameterNameSubstitution) => SetDriverSettings(showDeleted, isFirstRowHeaderLine, parameterNameSubstitution, null, null);

    /// <summary>
    /// Short overload of SetDriverSettings that accepts showDeleted, isFirstRowHeaderLine, parameterNameSubstitution, and systemDriverSettings; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 showDeleted、isFirstRowHeaderLine、parameterNameSubstitution 與 systemDriverSettings；其餘可選參數使用預設值並轉呼叫最長 SetDriverSettings 多載。
    /// </summary>
    public OdfNode SetDriverSettings(bool? showDeleted, bool? isFirstRowHeaderLine, bool? parameterNameSubstitution, string? systemDriverSettings) => SetDriverSettings(showDeleted, isFirstRowHeaderLine, parameterNameSubstitution, systemDriverSettings, null);


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
    public OdfNode SetDriverSettings(bool? showDeleted, bool? isFirstRowHeaderLine, bool? parameterNameSubstitution, string? systemDriverSettings, string? baseDn)
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
