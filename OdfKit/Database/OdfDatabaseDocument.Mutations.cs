using System;
using System.Collections.Generic;
using System.IO;
using OdfKit.Core;
using OdfKit.DOM;

using OdfKit.Compliance;
namespace OdfKit.Database;
/// <summary>
/// Provides the OdfDatabaseDocument API.
/// 提供 OdfDatabaseDocument API。
/// </summary>

public partial class OdfDatabaseDocument
{
    #region Add Operations
    /// <summary>
    /// Short overload of AddTable that accepts name; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 name；其餘可選參數使用預設值並轉呼叫最長 AddTable 多載。
    /// </summary>
    public OdfNode AddTable(string name) => AddTable(name, null);


    /// <summary>
    /// Adds a table description.
    /// 新增資料表描述。
    /// </summary>
    /// <param name="name">The table name. / 資料表名稱。</param>
    /// <param name="command">The optional table command or source name. / 選用的資料表命令或來源名稱。</param>
    /// <returns>The added table node. / 新增的資料表節點。</returns>
    public OdfNode AddTable(string name, string? command)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_DataCannotBeEmpty_7"), nameof(name));
        }

        OdfNode tableRepresentations = FindOrCreateChild(GetDatabaseNode(), "table-representations", DatabaseNamespace, "db");
        if (HasChildWithName(tableRepresentations, "table-representation", name))
        {
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_DuplicateName", name));
        }

        OdfNode table = OdfNodeFactory.CreateElement("table-representation", DatabaseNamespace, "db");
        table.SetAttribute("name", DatabaseNamespace, name, "db");
        if (!string.IsNullOrWhiteSpace(command))
        {
            table.SetAttribute("command", DatabaseNamespace, command!, "db");
        }

        tableRepresentations.AppendChild(table);
        return table;
    }
    /// <summary>
    /// Short overload of AddQuery that accepts name and command; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 name 與 command；其餘可選參數使用預設值並轉呼叫最長 AddQuery 多載。
    /// </summary>
    public OdfNode AddQuery(string name, string command) => AddQuery(name, command, null, null, null);

    /// <summary>
    /// Short overload of AddQuery that accepts name, command, and title; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 name、command 與 title；其餘可選參數使用預設值並轉呼叫最長 AddQuery 多載。
    /// </summary>
    public OdfNode AddQuery(string name, string command, string? title) => AddQuery(name, command, title, null, null);

    /// <summary>
    /// Short overload of AddQuery that accepts name, command, title, and description; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 name、command、title 與 description；其餘可選參數使用預設值並轉呼叫最長 AddQuery 多載。
    /// </summary>
    public OdfNode AddQuery(string name, string command, string? title, string? description) => AddQuery(name, command, title, description, null);



    /// <summary>
    /// Adds a query description.
    /// 新增查詢描述。
    /// </summary>
    /// <param name="name">The query name. / 查詢名稱。</param>
    /// <param name="command">The query command or SQL content. / 查詢命令或 SQL 內容。</param>
    /// <param name="title">The optional display title. / 選用的顯示標題。</param>
    /// <param name="description">The optional description text. / 選用的描述文字。</param>
    /// <param name="escapeProcessing">The optional SQL escape processing setting. / 選用的 SQL escape processing 設定。</param>
    /// <returns>The added query node. / 新增的查詢節點。</returns>
    public OdfNode AddQuery(string name, string command, string? title, string? description, bool? escapeProcessing)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_QueryCannotBeEmpty_4"), nameof(name));
        }

        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_QueryCannotBeEmpty_3"), nameof(command));
        }

        OdfNode queries = FindOrCreateOrderedChild(
            GetDatabaseNode(), "queries", DatabaseNamespace, "db",
            ("table-representations", DatabaseNamespace), ("schema-definition", DatabaseNamespace));
        if (HasChildWithName(queries, "query", name))
        {
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_DuplicateName", name));
        }

        OdfNode query = OdfNodeFactory.CreateElement("query", DatabaseNamespace, "db");
        query.SetAttribute("name", DatabaseNamespace, name, "db");
        query.SetAttribute("command", DatabaseNamespace, command, "db");
        if (!string.IsNullOrWhiteSpace(title))
        {
            query.SetAttribute("title", DatabaseNamespace, title!, "db");
        }

        if (!string.IsNullOrWhiteSpace(description))
        {
            query.SetAttribute("description", DatabaseNamespace, description!, "db");
        }

        if (escapeProcessing is not null)
        {
            query.SetAttribute(
                "escape-processing",
                DatabaseNamespace,
                escapeProcessing.Value ? "true" : "false",
                "db");
        }

        queries.AppendChild(query);
        return query;
    }

    /// <summary>
    /// Updates the command of an existing table description.
    /// 更新既有資料表描述的命令。
    /// </summary>
    /// <param name="name">The table name. / 資料表名稱。</param>
    /// <param name="command">The table command; a blank value removes the command. / 資料表命令；空白值會移除命令。</param>
    /// <returns><see langword="true"/> if the table was updated; otherwise <see langword="false"/>. / 若成功更新資料表則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public bool UpdateTable(string name, string? command)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_DataCannotBeEmpty_7"), nameof(name));
        }

        OdfNode? tableRepresentations = FindChildElement(GetDatabaseNode(), "table-representations", DatabaseNamespace);
        OdfNode? table = tableRepresentations is null
            ? null
            : FindNamedChild(tableRepresentations, "table-representation", name);
        if (table is null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(command))
        {
            table.RemoveAttribute("command", DatabaseNamespace);
        }
        else
        {
            table.SetAttribute("command", DatabaseNamespace, command!, "db");
        }

        return true;
    }

    /// <summary>
    /// Updates an existing table description to match the specified immutable snapshot.
    /// 將既有資料表描述更新為指定的不可變快照。
    /// </summary>
    /// <param name="table">The desired table snapshot; its name identifies the existing table. / 目標資料表快照；其名稱用於識別既有資料表。</param>
    /// <returns><see langword="true"/> if the table was updated; otherwise <see langword="false"/>. / 若成功更新資料表則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="table"/> is <see langword="null"/>. / 當 <paramref name="table"/> 為 <see langword="null"/> 時擲出。</exception>
    /// <exception cref="ArgumentException">When the table name is blank. / 當資料表名稱為空白時擲出。</exception>
    public bool UpdateTable(OdfDatabaseTableInfo table)
    {
        if (table is null)
        {
            throw new ArgumentNullException(nameof(table));
        }

        return UpdateTable(table.Name, table.Command);
    }

    /// <summary>
    /// Updates the command of an existing query description.
    /// 更新既有查詢描述的命令。
    /// </summary>
    /// <param name="name">The query name. / 查詢名稱。</param>
    /// <param name="command">The non-empty query command or SQL content. / 非空白的查詢命令或 SQL 內容。</param>
    /// <returns><see langword="true"/> if the query was updated; otherwise <see langword="false"/>. / 若成功更新查詢則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public bool UpdateQuery(string name, string command)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_QueryCannotBeEmpty_4"), nameof(name));
        }

        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_QueryCannotBeEmpty_3"), nameof(command));
        }

        OdfNode? queries = FindChildElement(GetDatabaseNode(), "queries", DatabaseNamespace);
        OdfNode? query = queries is null ? null : FindNamedChild(queries, "query", name);
        if (query is null)
        {
            return false;
        }

        query.SetAttribute("command", DatabaseNamespace, command, "db");
        return true;
    }

    /// <summary>
    /// Updates an existing query description to match the specified immutable snapshot.
    /// 將既有查詢描述更新為指定的不可變快照。
    /// </summary>
    /// <param name="query">The desired query snapshot; its name identifies the existing query. / 目標查詢快照；其名稱用於識別既有查詢。</param>
    /// <returns><see langword="true"/> if the query was updated; otherwise <see langword="false"/>. / 若成功更新查詢則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="query"/> is <see langword="null"/>. / 當 <paramref name="query"/> 為 <see langword="null"/> 時擲出。</exception>
    /// <exception cref="ArgumentException">When the query name or command is blank. / 當查詢名稱或命令為空白時擲出。</exception>
    public bool UpdateQuery(OdfDatabaseQueryInfo query)
    {
        if (query is null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        if (string.IsNullOrWhiteSpace(query.Name))
        {
            throw new ArgumentException(
                OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_QueryCannotBeEmpty_4"),
                nameof(query));
        }

        if (string.IsNullOrWhiteSpace(query.Command))
        {
            throw new ArgumentException(
                OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_QueryCannotBeEmpty_3"),
                nameof(query));
        }

        OdfNode? queries = FindChildElement(GetDatabaseNode(), "queries", DatabaseNamespace);
        OdfNode? node = queries is null ? null : FindNamedChild(queries, "query", query.Name);
        if (node is null)
        {
            return false;
        }

        node.SetAttribute("command", DatabaseNamespace, query.Command, "db");
        SetOrRemoveDatabaseAttribute(node, "title", query.Title);
        SetOrRemoveDatabaseAttribute(node, "description", query.Description);
        if (query.EscapeProcessing is null)
        {
            node.RemoveAttribute("escape-processing", DatabaseNamespace);
        }
        else
        {
            node.SetAttribute(
                "escape-processing",
                DatabaseNamespace,
                query.EscapeProcessing.Value ? "true" : "false",
                "db");
        }

        return true;
    }

    /// <summary>
    /// Removes all table descriptions.
    /// 移除所有資料表描述。
    /// </summary>
    /// <returns>The number of removed table descriptions. / 已移除的資料表描述數量。</returns>
    public int ClearTables() => ClearNamedChildren("table-representations", "table-representation");

    /// <summary>
    /// Removes all query descriptions.
    /// 移除所有查詢描述。
    /// </summary>
    /// <returns>The number of removed query descriptions. / 已移除的查詢描述數量。</returns>
    public int ClearQueries() => ClearNamedChildren("queries", "query");

    /// <summary>
    /// Updates an existing form component to match the specified description.
    /// 將既有表單元件更新為指定描述。
    /// </summary>
    /// <param name="form">The desired form description; its name identifies the existing component. / 目標表單描述；其名稱用於識別既有元件。</param>
    /// <returns><see langword="true"/> if the form was updated; otherwise <see langword="false"/>. / 若成功更新表單則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="form"/> is <see langword="null"/>. / 當 <paramref name="form"/> 為 <see langword="null"/> 時擲出。</exception>
    /// <exception cref="ArgumentException">When the form name is blank. / 當表單名稱為空白時擲出。</exception>
    public bool UpdateForm(OdfDatabaseFormInfo form)
    {
        if (form is null)
        {
            throw new ArgumentNullException(nameof(form));
        }

        if (string.IsNullOrWhiteSpace(form.Name))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_FormCannotBeEmpty_3"), nameof(form));
        }

        return UpdateComponent("forms", form.Name, form.Href, form.Title, form.Description, form.AsTemplate);
    }

    /// <summary>
    /// Updates an existing report component to match the specified description.
    /// 將既有報表元件更新為指定描述。
    /// </summary>
    /// <param name="report">The desired report description; its name identifies the existing component. / 目標報表描述；其名稱用於識別既有元件。</param>
    /// <returns><see langword="true"/> if the report was updated; otherwise <see langword="false"/>. / 若成功更新報表則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="report"/> is <see langword="null"/>. / 當 <paramref name="report"/> 為 <see langword="null"/> 時擲出。</exception>
    /// <exception cref="ArgumentException">When the report name is blank. / 當報表名稱為空白時擲出。</exception>
    public bool UpdateReport(OdfDatabaseReportInfo report)
    {
        if (report is null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        if (string.IsNullOrWhiteSpace(report.Name))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_ReportCannotBeEmpty_2"), nameof(report));
        }

        return UpdateComponent("reports", report.Name, report.Href, report.Title, report.Description, report.AsTemplate);
    }

    /// <summary>
    /// Updates an existing data source setting to match the specified description.
    /// 將既有資料來源設定更新為指定描述。
    /// </summary>
    /// <param name="setting">The desired setting description; its name identifies the existing setting. / 目標設定描述；其名稱用於識別既有設定。</param>
    /// <returns><see langword="true"/> if the setting was updated; otherwise <see langword="false"/>. / 若成功更新設定則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="setting"/> is <see langword="null"/>. / 當 <paramref name="setting"/> 為 <see langword="null"/> 時擲出。</exception>
    /// <exception cref="ArgumentException">When the setting name or values are empty. / 當設定名稱或值清單為空時擲出。</exception>
    public bool UpdateDataSourceSetting(OdfDatabaseDataSourceSettingInfo setting)
    {
        if (setting is null)
        {
            throw new ArgumentNullException(nameof(setting));
        }

        if (string.IsNullOrWhiteSpace(setting.Name))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_DataCannotBeEmpty_8"), nameof(setting));
        }

        if (setting.Values.Count == 0)
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_DataCannotBeEmpty_5"), nameof(setting));
        }

        foreach (string value in setting.Values)
        {
            if (value is null)
            {
                throw new ArgumentException(
                    OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_DataCannotBeEmpty_6"),
                    nameof(setting));
            }
        }

        OdfNode? settings = FindDataSourceSettings();
        if (settings is null)
        {
            return false;
        }

        foreach (OdfNode child in settings.Children)
        {
            if (child.NodeType is not OdfNodeType.Element ||
                child.LocalName != "data-source-setting" ||
                child.NamespaceUri != DatabaseNamespace ||
                !string.Equals(
                    child.GetAttribute("data-source-setting-name", DatabaseNamespace),
                    setting.Name,
                    StringComparison.Ordinal))
            {
                continue;
            }

            child.SetAttribute(
                "data-source-setting-type",
                DatabaseNamespace,
                ToDataSourceSettingTypeToken(setting.Type),
                "db");
            if (setting.IsList is null)
            {
                child.RemoveAttribute("data-source-setting-is-list", DatabaseNamespace);
            }
            else
            {
                child.SetAttribute(
                    "data-source-setting-is-list",
                    DatabaseNamespace,
                    setting.IsList.Value ? "true" : "false",
                    "db");
            }

            foreach (OdfNode valueNode in new List<OdfNode>(child.Children))
            {
                if (valueNode.NodeType is OdfNodeType.Element &&
                    valueNode.LocalName == "data-source-setting-value" &&
                    valueNode.NamespaceUri == DatabaseNamespace)
                {
                    child.RemoveChild(valueNode);
                }
            }

            foreach (string value in setting.Values)
            {
                OdfNode valueNode = OdfNodeFactory.CreateElement("data-source-setting-value", DatabaseNamespace, "db");
                valueNode.TextContent = value;
                child.AppendChild(valueNode);
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Removes all form component descriptions, including components in nested collections.
    /// 移除所有表單元件描述，包括巢狀集合中的元件。
    /// </summary>
    /// <returns>The number of removed form components. / 已移除的表單元件數量。</returns>
    public int ClearForms() => ClearComponents("forms");

    /// <summary>
    /// Removes all report component descriptions, including components in nested collections.
    /// 移除所有報表元件描述，包括巢狀集合中的元件。
    /// </summary>
    /// <returns>The number of removed report components. / 已移除的報表元件數量。</returns>
    public int ClearReports() => ClearComponents("reports");

    /// <summary>
    /// Removes all data source settings while preserving unrelated and foreign children.
    /// 移除所有資料來源設定，同時保留無關與外來子節點。
    /// </summary>
    /// <returns>The number of removed data source settings. / 已移除的資料來源設定數量。</returns>
    public int ClearDataSourceSettings()
    {
        OdfNode? settings = FindDataSourceSettings();
        if (settings is null)
        {
            return 0;
        }

        int removedCount = 0;
        foreach (OdfNode child in new List<OdfNode>(settings.Children))
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == "data-source-setting" &&
                child.NamespaceUri == DatabaseNamespace)
            {
                settings.RemoveChild(child);
                removedCount++;
            }
        }

        return removedCount;
    }


    /// <summary>
    /// Adds a data source setting.
    /// 新增資料來源設定。
    /// </summary>
    /// <param name="name">The setting name. / 設定名稱。</param>
    /// <param name="type">The setting value type. / 設定值型別。</param>
    /// <param name="value">The setting value. / 設定值。</param>
    /// <returns>The added data source setting node. / 新增的資料來源設定節點。</returns>
    /// <exception cref="InvalidOperationException">When the data source connection has not been set. / 當尚未設定資料來源連線時擲出。</exception>
    public OdfNode AddDataSourceSetting(string name, OdfDatabaseDataSourceSettingType type, string value)
    {
        return AddDataSourceSetting(name, type, isList: false, [value]);
    }

    /// <summary>
    /// Adds a data source setting.
    /// 新增資料來源設定。
    /// </summary>
    /// <param name="name">The setting name. / 設定名稱。</param>
    /// <param name="type">The setting value type. / 設定值型別。</param>
    /// <param name="isList">Whether the setting value is a list. / 設定值是否為清單。</param>
    /// <param name="values">The list of setting values. / 設定值清單。</param>
    /// <returns>The added data source setting node. / 新增的資料來源設定節點。</returns>
    /// <exception cref="InvalidOperationException">When the data source connection has not been set. / 當尚未設定資料來源連線時擲出。</exception>
    public OdfNode AddDataSourceSetting(
        string name,
        OdfDatabaseDataSourceSettingType type,
        bool isList,
        params string[] values)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_DataCannotBeEmpty_8"), nameof(name));
        }

        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        if (values.Length == 0)
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_DataCannotBeEmpty_5"), nameof(values));
        }

        if (FindConnectionResource() is null)
        {
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_BeforeAddingNewData"));
        }

        OdfNode settings = FindOrCreateDataSourceSettings();
        OdfNode setting = OdfNodeFactory.CreateElement("data-source-setting", DatabaseNamespace, "db");
        setting.SetAttribute("data-source-setting-name", DatabaseNamespace, name, "db");
        setting.SetAttribute("data-source-setting-type", DatabaseNamespace, ToDataSourceSettingTypeToken(type), "db");
        setting.SetAttribute("data-source-setting-is-list", DatabaseNamespace, isList ? "true" : "false", "db");

        foreach (string value in values)
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(values), OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_DataCannotBeEmpty_6"));
            }

            OdfNode valueNode = OdfNodeFactory.CreateElement("data-source-setting-value", DatabaseNamespace, "db");
            valueNode.TextContent = value;
            setting.AppendChild(valueNode);
        }

        settings.AppendChild(setting);
        return setting;
    }
    /// <summary>
    /// Short overload of AddForm that accepts name; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 name；其餘可選參數使用預設值並轉呼叫最長 AddForm 多載。
    /// </summary>
    public OdfNode AddForm(string name) => AddForm(name, null, null, null, null);

    /// <summary>
    /// Short overload of AddForm that accepts name and href; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 name 與 href；其餘可選參數使用預設值並轉呼叫最長 AddForm 多載。
    /// </summary>
    public OdfNode AddForm(string name, string? href) => AddForm(name, href, null, null, null);

    /// <summary>
    /// Short overload of AddForm that accepts name, href, and title; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 name、href 與 title；其餘可選參數使用預設值並轉呼叫最長 AddForm 多載。
    /// </summary>
    public OdfNode AddForm(string name, string? href, string? title) => AddForm(name, href, title, null, null);

    /// <summary>
    /// Short overload of AddForm that accepts name, href, title, and description; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 name、href、title 與 description；其餘可選參數使用預設值並轉呼叫最長 AddForm 多載。
    /// </summary>
    public OdfNode AddForm(string name, string? href, string? title, string? description) => AddForm(name, href, title, description, null);


    /// <summary>
    /// Adds a form component description.
    /// 新增表單元件描述。
    /// </summary>
    /// <param name="name">The form name. / 表單名稱。</param>
    /// <param name="href">The optional form resource reference path. / 選用的表單資源參照路徑。</param>
    /// <param name="title">The optional display title. / 選用的顯示標題。</param>
    /// <param name="description">The optional description text. / 選用的描述文字。</param>
    /// <param name="asTemplate">The optional template marker. / 選用的範本標記。</param>
    /// <returns>The added form component node. / 新增的表單元件節點。</returns>
    public OdfNode AddForm(string name, string? href, string? title, string? description, bool? asTemplate)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_FormCannotBeEmpty_3"), nameof(name));
        }

        OdfNode forms = FindOrCreateOrderedChild(
            GetDatabaseNode(), "forms", DatabaseNamespace, "db",
            ("reports", DatabaseNamespace), ("queries", DatabaseNamespace),
            ("table-representations", DatabaseNamespace), ("schema-definition", DatabaseNamespace));
        OdfNode component = OdfNodeFactory.CreateElement("component", DatabaseNamespace, "db");
        component.SetAttribute("name", DatabaseNamespace, name, "db");

        if (!string.IsNullOrWhiteSpace(href))
        {
            component.SetAttribute("href", OdfNamespaces.XLink, href!, "xlink");
            component.SetAttribute("type", OdfNamespaces.XLink, "simple", "xlink");
            component.SetAttribute("show", OdfNamespaces.XLink, "none", "xlink");
            component.SetAttribute("actuate", OdfNamespaces.XLink, "onRequest", "xlink");
        }

        if (!string.IsNullOrWhiteSpace(title))
        {
            component.SetAttribute("title", DatabaseNamespace, title!, "db");
        }

        if (!string.IsNullOrWhiteSpace(description))
        {
            component.SetAttribute("description", DatabaseNamespace, description!, "db");
        }

        if (asTemplate is not null)
        {
            component.SetAttribute("as-template", DatabaseNamespace, asTemplate.Value ? "true" : "false", "db");
        }

        forms.AppendChild(component);
        return component;
    }

    /// <summary>
    /// Short overload of AddReport that accepts name; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 name；其餘可選參數使用預設值並轉呼叫最長 AddReport 多載。
    /// </summary>
    public OdfNode AddReport(string name) => AddReport(name, null, null, null, null);

    /// <summary>
    /// Short overload of AddReport that accepts name and href; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 name 與 href；其餘可選參數使用預設值並轉呼叫最長 AddReport 多載。
    /// </summary>
    public OdfNode AddReport(string name, string? href) => AddReport(name, href, null, null, null);

    /// <summary>
    /// Short overload of AddReport that accepts name, href, and title; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 name、href 與 title；其餘可選參數使用預設值並轉呼叫最長 AddReport 多載。
    /// </summary>
    public OdfNode AddReport(string name, string? href, string? title) => AddReport(name, href, title, null, null);

    /// <summary>
    /// Short overload of AddReport that accepts name, href, title, and description; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 name、href、title 與 description；其餘可選參數使用預設值並轉呼叫最長 AddReport 多載。
    /// </summary>
    public OdfNode AddReport(string name, string? href, string? title, string? description) => AddReport(name, href, title, description, null);


    /// <summary>
    /// Adds a report component description.
    /// 新增報表元件描述。
    /// </summary>
    /// <param name="name">The report name. / 報表名稱。</param>
    /// <param name="href">The optional report resource reference path. / 選用的報表資源參照路徑。</param>
    /// <param name="title">The optional display title. / 選用的顯示標題。</param>
    /// <param name="description">The optional description text. / 選用的描述文字。</param>
    /// <param name="asTemplate">The optional template marker. / 選用的範本標記。</param>
    /// <returns>The added report component node. / 新增的報表元件節點。</returns>
    public OdfNode AddReport(string name, string? href, string? title, string? description, bool? asTemplate)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_ReportCannotBeEmpty_2"), nameof(name));
        }

        OdfNode reports = FindOrCreateOrderedChild(
            GetDatabaseNode(), "reports", DatabaseNamespace, "db",
            ("queries", DatabaseNamespace), ("table-representations", DatabaseNamespace),
            ("schema-definition", DatabaseNamespace));
        OdfNode component = OdfNodeFactory.CreateElement("component", DatabaseNamespace, "db");
        component.SetAttribute("name", DatabaseNamespace, name, "db");

        if (!string.IsNullOrWhiteSpace(href))
        {
            component.SetAttribute("href", OdfNamespaces.XLink, href!, "xlink");
            component.SetAttribute("type", OdfNamespaces.XLink, "simple", "xlink");
            component.SetAttribute("show", OdfNamespaces.XLink, "none", "xlink");
            component.SetAttribute("actuate", OdfNamespaces.XLink, "onRequest", "xlink");
        }

        if (!string.IsNullOrWhiteSpace(title))
        {
            component.SetAttribute("title", DatabaseNamespace, title!, "db");
        }

        if (!string.IsNullOrWhiteSpace(description))
        {
            component.SetAttribute("description", DatabaseNamespace, description!, "db");
        }

        if (asTemplate is not null)
        {
            component.SetAttribute("as-template", DatabaseNamespace, asTemplate.Value ? "true" : "false", "db");
        }

        reports.AppendChild(component);
        return component;
    }


    #endregion

    #region Remove Operations

    /// <summary>
    /// Removes the table description with the specified name.
    /// 移除指定名稱的資料表描述。
    /// </summary>
    /// <param name="name">The table name. / 資料表名稱。</param>
    /// <returns><see langword="true"/> if the table description was removed successfully; otherwise <see langword="false"/>. / 如果成功移除資料表描述，則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public bool RemoveTable(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_DataCannotBeEmpty_7"), nameof(name));
        }

        OdfNode? tableRepresentations = FindChildElement(GetDatabaseNode(), "table-representations", DatabaseNamespace);
        if (tableRepresentations is null)
        {
            return false;
        }

        foreach (OdfNode child in new List<OdfNode>(tableRepresentations.Children))
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == "table-representation" &&
                child.NamespaceUri == DatabaseNamespace &&
                string.Equals(child.GetAttribute("name", DatabaseNamespace), name, StringComparison.Ordinal))
            {
                tableRepresentations.RemoveChild(child);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Removes the query description with the specified name.
    /// 移除指定名稱的查詢描述。
    /// </summary>
    /// <param name="name">The query name. / 查詢名稱。</param>
    /// <returns><see langword="true"/> if the query description was removed successfully; otherwise <see langword="false"/>. / 如果成功移除查詢描述，則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public bool RemoveQuery(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_QueryCannotBeEmpty_4"), nameof(name));
        }

        OdfNode? queries = FindChildElement(GetDatabaseNode(), "queries", DatabaseNamespace);
        if (queries is null)
        {
            return false;
        }

        foreach (OdfNode child in new List<OdfNode>(queries.Children))
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == "query" &&
                child.NamespaceUri == DatabaseNamespace &&
                string.Equals(child.GetAttribute("name", DatabaseNamespace), name, StringComparison.Ordinal))
            {
                queries.RemoveChild(child);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Removes the report component with the specified name.
    /// 移除指定名稱的報表元件。
    /// </summary>
    /// <param name="name">The report name. / 報表名稱。</param>
    /// <returns><see langword="true"/> if the report component was removed successfully; otherwise <see langword="false"/>. / 如果成功移除報表元件，則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public bool RemoveReport(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_ReportCannotBeEmpty_2"), nameof(name));
        }

        OdfNode? reportsNode = FindChildElement(GetDatabaseNode(), "reports", DatabaseNamespace);
        if (reportsNode is null)
        {
            return false;
        }

        return RemoveNamedComponent(reportsNode, name, depth: 0);
    }

    /// <summary>
    /// Removes the form component with the specified name.
    /// 移除指定名稱的表單元件。
    /// </summary>
    /// <param name="name">The form name. / 表單名稱。</param>
    /// <returns><see langword="true"/> if the form component was removed successfully; otherwise <see langword="false"/>. / 如果成功移除表單元件，則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public bool RemoveForm(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_FormCannotBeEmpty_3"), nameof(name));
        }

        OdfNode? formsNode = FindChildElement(GetDatabaseNode(), "forms", DatabaseNamespace);
        if (formsNode is null)
        {
            return false;
        }

        return RemoveNamedComponent(formsNode, name, depth: 0);
    }

    /// <summary>
    /// Removes the data source setting with the specified name.
    /// 移除指定名稱的資料來源設定。
    /// </summary>
    /// <param name="name">The setting name. / 設定名稱。</param>
    /// <returns><see langword="true"/> if the data source setting was removed successfully; otherwise <see langword="false"/>. / 如果成功移除資料來源設定，則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public bool RemoveDataSourceSetting(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_DataCannotBeEmpty_8"), nameof(name));
        }

        OdfNode? settings = FindDataSourceSettings();
        if (settings is null)
        {
            return false;
        }

        foreach (OdfNode child in new List<OdfNode>(settings.Children))
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == "data-source-setting" &&
                child.NamespaceUri == DatabaseNamespace &&
                string.Equals(child.GetAttribute("data-source-setting-name", DatabaseNamespace), name, StringComparison.Ordinal))
            {
                settings.RemoveChild(child);
                return true;
            }
        }

        return false;
    }

    private static bool RemoveNamedComponent(OdfNode parent, string name, int depth)
    {
        if (depth > MaxFormComponentDepth)
        {
            throw new InvalidDataException(
                OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_FormComponentNestingTooDeep", MaxFormComponentDepth));
        }

        foreach (OdfNode child in new List<OdfNode>(parent.Children))
        {
            if (child.NodeType is not OdfNodeType.Element || child.NamespaceUri != DatabaseNamespace)
            {
                continue;
            }

            if (child.LocalName == "component" &&
                string.Equals(child.GetAttribute("name", DatabaseNamespace), name, StringComparison.Ordinal))
            {
                parent.RemoveChild(child);
                return true;
            }

            if (child.LocalName == "component-collection" && RemoveNamedComponent(child, name, depth + 1))
            {
                return true;
            }
        }

        return false;
    }

    private static OdfNode? FindNamedChild(OdfNode parent, string localName, string name)
    {
        foreach (OdfNode child in parent.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == localName &&
                child.NamespaceUri == DatabaseNamespace &&
                string.Equals(child.GetAttribute("name", DatabaseNamespace), name, StringComparison.Ordinal))
            {
                return child;
            }
        }

        return null;
    }

    private bool UpdateComponent(
        string containerLocalName,
        string name,
        string? href,
        string? title,
        string? description,
        bool? asTemplate)
    {
        OdfNode? container = FindChildElement(GetDatabaseNode(), containerLocalName, DatabaseNamespace);
        OdfNode? component = container is null ? null : FindNamedComponent(container, name, depth: 0);
        if (component is null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(href))
        {
            component.RemoveAttribute("href", OdfNamespaces.XLink);
            component.RemoveAttribute("type", OdfNamespaces.XLink);
            component.RemoveAttribute("show", OdfNamespaces.XLink);
            component.RemoveAttribute("actuate", OdfNamespaces.XLink);
        }
        else
        {
            component.SetAttribute("href", OdfNamespaces.XLink, href!, "xlink");
            component.SetAttribute("type", OdfNamespaces.XLink, "simple", "xlink");
            component.SetAttribute("show", OdfNamespaces.XLink, "none", "xlink");
            component.SetAttribute("actuate", OdfNamespaces.XLink, "onRequest", "xlink");
        }

        SetOrRemoveDatabaseAttribute(component, "title", title);
        SetOrRemoveDatabaseAttribute(component, "description", description);
        if (asTemplate is null)
        {
            component.RemoveAttribute("as-template", DatabaseNamespace);
        }
        else
        {
            component.SetAttribute("as-template", DatabaseNamespace, asTemplate.Value ? "true" : "false", "db");
        }

        return true;
    }

    private static OdfNode? FindNamedComponent(OdfNode parent, string name, int depth)
    {
        if (depth > MaxFormComponentDepth)
        {
            throw new InvalidDataException(
                OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_FormComponentNestingTooDeep", MaxFormComponentDepth));
        }

        foreach (OdfNode child in parent.Children)
        {
            if (child.NodeType is not OdfNodeType.Element || child.NamespaceUri != DatabaseNamespace)
            {
                continue;
            }

            if (child.LocalName == "component" &&
                string.Equals(child.GetAttribute("name", DatabaseNamespace), name, StringComparison.Ordinal))
            {
                return child;
            }

            if (child.LocalName == "component-collection")
            {
                OdfNode? found = FindNamedComponent(child, name, depth + 1);
                if (found is not null)
                {
                    return found;
                }
            }
        }

        return null;
    }

    private int ClearComponents(string containerLocalName)
    {
        OdfNode? container = FindChildElement(GetDatabaseNode(), containerLocalName, DatabaseNamespace);
        return container is null ? 0 : ClearComponents(container, depth: 0);
    }

    private static int ClearComponents(OdfNode parent, int depth)
    {
        if (depth > MaxFormComponentDepth)
        {
            throw new InvalidDataException(
                OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_FormComponentNestingTooDeep", MaxFormComponentDepth));
        }

        int removedCount = 0;
        foreach (OdfNode child in new List<OdfNode>(parent.Children))
        {
            if (child.NodeType is not OdfNodeType.Element || child.NamespaceUri != DatabaseNamespace)
            {
                continue;
            }

            if (child.LocalName == "component")
            {
                parent.RemoveChild(child);
                removedCount++;
            }
            else if (child.LocalName == "component-collection")
            {
                removedCount += ClearComponents(child, depth + 1);
            }
        }

        return removedCount;
    }

    private static void SetOrRemoveDatabaseAttribute(OdfNode node, string localName, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            node.RemoveAttribute(localName, DatabaseNamespace);
        }
        else
        {
            node.SetAttribute(localName, DatabaseNamespace, value!, "db");
        }
    }

    private int ClearNamedChildren(string containerLocalName, string childLocalName)
    {
        OdfNode? container = FindChildElement(GetDatabaseNode(), containerLocalName, DatabaseNamespace);
        if (container is null)
        {
            return 0;
        }

        int removedCount = 0;
        foreach (OdfNode child in new List<OdfNode>(container.Children))
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == childLocalName &&
                child.NamespaceUri == DatabaseNamespace)
            {
                container.RemoveChild(child);
                removedCount++;
            }
        }

        return removedCount;
    }

    #endregion
}
