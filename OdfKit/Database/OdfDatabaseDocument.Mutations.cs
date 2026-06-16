using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

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
            throw new ArgumentException("資料表名稱不能為空。", nameof(name));
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
            throw new ArgumentException("查詢名稱不能為空。", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentException("查詢命令不能為空。", nameof(command));
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
            throw new ArgumentException("資料來源設定名稱不能為空。", nameof(name));
        }

        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        if (values.Length == 0)
        {
            throw new ArgumentException("資料來源設定值不能為空。", nameof(values));
        }

        if (FindConnectionResource() is null)
        {
            throw new InvalidOperationException("新增資料來源設定前必須先設定資料來源連線。");
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
                throw new ArgumentNullException(nameof(values), "資料來源設定值不能為 null。");
            }

            OdfNode valueNode = OdfNodeFactory.CreateElement("data-source-setting-value", DatabaseNamespace, "db");
            valueNode.TextContent = value;
            setting.AppendChild(valueNode);
        }

        settings.AppendChild(setting);
        return setting;
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
            throw new ArgumentException("資料表名稱不能為空。", nameof(name));
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
            throw new ArgumentException("查詢名稱不能為空。", nameof(name));
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
    /// 移除指定名稱的資料來源設定。
    /// </summary>
    /// <param name="name">設定名稱。</param>
    /// <returns>如果成功移除資料來源設定，則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public bool RemoveDataSourceSetting(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("資料來源設定名稱不能為空。", nameof(name));
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

    #endregion
}
