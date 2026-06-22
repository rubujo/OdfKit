using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

using OdfKit.Compliance;
namespace OdfKit.Database;

public partial class OdfDatabaseDocument
{
    #region Add Operations

    /// <summary>
    /// 新增資料表描述。
    /// </summary>
    /// <param name="name">資料表名稱。</param>
    /// <param name="command">選用的資料表命令或來源名稱。</param>
    /// <returns>新增的資料表節點。</returns>
    public OdfNode AddTable(string name, string? command = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_DataCannotBeEmpty_7"), nameof(name));
        }

        OdfNode tableRepresentations = FindOrCreateChild(GetDatabaseNode(), "table-representations", DatabaseNamespace, "db");
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
    /// 新增查詢描述。
    /// </summary>
    /// <param name="name">查詢名稱。</param>
    /// <param name="command">查詢命令或 SQL 內容。</param>
    /// <param name="title">選用的顯示標題。</param>
    /// <param name="description">選用的描述文字。</param>
    /// <param name="escapeProcessing">選用的 SQL escape processing 設定。</param>
    /// <returns>新增的查詢節點。</returns>
    public OdfNode AddQuery(
        string name,
        string command,
        string? title = null,
        string? description = null,
        bool? escapeProcessing = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_QueryCannotBeEmpty_4"), nameof(name));
        }

        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_QueryCannotBeEmpty_3"), nameof(command));
        }

        OdfNode queries = FindOrCreateChild(GetDatabaseNode(), "queries", DatabaseNamespace, "db");
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
    /// 新增資料來源設定。
    /// </summary>
    /// <param name="name">設定名稱。</param>
    /// <param name="type">設定值型別。</param>
    /// <param name="value">設定值。</param>
    /// <returns>新增的資料來源設定節點。</returns>
    /// <exception cref="InvalidOperationException">當尚未設定資料來源連線時擲出。</exception>
    public OdfNode AddDataSourceSetting(string name, OdfDatabaseDataSourceSettingType type, string value)
    {
        return AddDataSourceSetting(name, type, isList: false, [value]);
    }

    /// <summary>
    /// 新增資料來源設定。
    /// </summary>
    /// <param name="name">設定名稱。</param>
    /// <param name="type">設定值型別。</param>
    /// <param name="isList">設定值是否為清單。</param>
    /// <param name="values">設定值清單。</param>
    /// <returns>新增的資料來源設定節點。</returns>
    /// <exception cref="InvalidOperationException">當尚未設定資料來源連線時擲出。</exception>
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
    /// 新增表單元件描述。
    /// </summary>
    /// <param name="name">表單名稱。</param>
    /// <param name="href">選用的表單資源參照路徑。</param>
    /// <param name="title">選用的顯示標題。</param>
    /// <param name="description">選用的描述文字。</param>
    /// <param name="asTemplate">選用的範本標記。</param>
    /// <returns>新增的表單元件節點。</returns>
    public OdfNode AddForm(
        string name,
        string? href = null,
        string? title = null,
        string? description = null,
        bool? asTemplate = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_FormCannotBeEmpty_3"), nameof(name));
        }

        OdfNode forms = FindOrCreateChild(GetDatabaseNode(), "forms", DatabaseNamespace, "db");
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
    /// 新增報表元件描述。
    /// </summary>
    /// <param name="name">報表名稱。</param>
    /// <param name="href">選用的報表資源參照路徑。</param>
    /// <param name="title">選用的顯示標題。</param>
    /// <param name="description">選用的描述文字。</param>
    /// <param name="asTemplate">選用的範本標記。</param>
    /// <returns>新增的報表元件節點。</returns>
    public OdfNode AddReport(
        string name,
        string? href = null,
        string? title = null,
        string? description = null,
        bool? asTemplate = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_ReportCannotBeEmpty_2"), nameof(name));
        }

        OdfNode reports = FindOrCreateChild(GetDatabaseNode(), "reports", DatabaseNamespace, "db");
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
    /// 移除指定名稱的資料表描述。
    /// </summary>
    /// <param name="name">資料表名稱。</param>
    /// <returns>如果成功移除資料表描述，則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
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
    /// 移除指定名稱的查詢描述。
    /// </summary>
    /// <param name="name">查詢名稱。</param>
    /// <returns>如果成功移除查詢描述，則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
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
    /// 移除指定名稱的報表元件。
    /// </summary>
    /// <param name="name">報表名稱。</param>
    /// <returns>如果成功移除報表元件，則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
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

        return RemoveNamedComponent(reportsNode, name);
    }

    /// <summary>
    /// 移除指定名稱的表單元件。
    /// </summary>
    /// <param name="name">表單名稱。</param>
    /// <returns>如果成功移除表單元件，則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
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

        return RemoveNamedComponent(formsNode, name);
    }

    /// <summary>
    /// 移除指定名稱的資料來源設定。
    /// </summary>
    /// <param name="name">設定名稱。</param>
    /// <returns>如果成功移除資料來源設定，則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
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

    private static bool RemoveNamedComponent(OdfNode parent, string name)
    {
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

            if (child.LocalName == "component-collection" && RemoveNamedComponent(child, name))
            {
                return true;
            }
        }

        return false;
    }

    #endregion
}
