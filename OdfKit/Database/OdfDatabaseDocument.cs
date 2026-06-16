using System;
using System.Collections.Generic;
using System.IO;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Database;

/// <summary>
/// 表示 ODF 資料庫文件 (.odb) 的最小封裝 wrapper。
/// </summary>
public partial class OdfDatabaseDocument : OdfDocument
{
    private const string DatabaseNamespace = "urn:oasis:names:tc:opendocument:xmlns:database:1.0";

    /// <summary>
    /// 使用指定的 ODF 封裝初始化 <see cref="OdfDatabaseDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">ODF 封裝。</param>
    public OdfDatabaseDocument(OdfPackage package) : base(package)
    {
        if (string.IsNullOrEmpty(package.MimeType))
        {
            package.SetMimeType("application/vnd.oasis.opendocument.database");
        }
    }

    /// <summary>
    /// 建立新的 ODB 資料庫文件。
    /// </summary>
    /// <returns>新的 <see cref="OdfDatabaseDocument"/> 執行個體。</returns>
    public static OdfDatabaseDocument Create()
    {
        return (OdfDatabaseDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.Database);
    }

    /// <summary>
    /// 從指定路徑載入 ODB 資料庫文件。
    /// </summary>
    /// <param name="path">ODB 文件路徑。</param>
    /// <returns>載入完成的 <see cref="OdfDatabaseDocument"/> 執行個體。</returns>
    /// <exception cref="InvalidOperationException">當指定文件不是 ODB 資料庫時擲出。</exception>
    public new static OdfDatabaseDocument Load(string path)
    {
        return EnsureDatabase(OdfDocumentFactory.LoadDocument(path));
    }

    /// <summary>
    /// 從指定資料流載入 ODB 資料庫文件。
    /// </summary>
    /// <param name="stream">包含 ODB 文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>載入完成的 <see cref="OdfDatabaseDocument"/> 執行個體。</returns>
    /// <exception cref="InvalidOperationException">當指定文件不是 ODB 資料庫時擲出。</exception>
    public new static OdfDatabaseDocument Load(Stream stream, string? fileName = null)
    {
        return EnsureDatabase(OdfDocumentFactory.LoadDocument(stream, fileName));
    }

    /// <summary>
    /// 取得主要資料庫節點。
    /// </summary>
    public OdfNode DatabaseNode => GetDatabaseNode();

    /// <summary>
    /// 取得目前資料來源連線參照。
    /// </summary>
    public string? ConnectionHref
    {
        get
        {
            OdfNode? connection = FindConnectionResource();
            return connection?.GetAttribute("href", OdfNamespaces.XLink);
        }
    }

    /// <summary>
    /// 取得目前宣告的資料表描述清單。
    /// </summary>
    public IReadOnlyList<OdfDatabaseTableInfo> Tables => GetTables();

    /// <summary>
    /// 取得目前宣告的查詢描述清單。
    /// </summary>
    public IReadOnlyList<OdfDatabaseQueryInfo> Queries => GetQueries();

    /// <summary>
    /// 取得目前宣告的資料來源設定清單。
    /// </summary>
    public IReadOnlyList<OdfDatabaseDataSourceSettingInfo> DataSourceSettings => GetDataSourceSettings();

    /// <summary>
    /// 設定資料來源連線參照。
    /// </summary>
    /// <param name="href">連線資源路徑或 URL。</param>
    public void SetConnection(string href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            throw new ArgumentException("連線參照不能為空。", nameof(href));
        }

        OdfNode dataSource = FindOrCreateDataSource();
        OdfNode connectionData = FindOrCreateChild(dataSource, "connection-data", DatabaseNamespace, "db");
        OdfNode connection = FindOrCreateChild(connectionData, "connection-resource", DatabaseNamespace, "db");
        connection.SetAttribute("href", OdfNamespaces.XLink, href, "xlink");
        connection.SetAttribute("type", OdfNamespaces.XLink, "simple", "xlink");
    }

    /// <summary>
    /// 取得目前宣告的資料表描述清單。
    /// </summary>
    /// <returns>資料表描述清單。</returns>
    public IReadOnlyList<OdfDatabaseTableInfo> GetTables()
    {
        OdfNode? tableRepresentations = FindChildElement(GetDatabaseNode(), "table-representations", DatabaseNamespace);
        if (tableRepresentations is null)
        {
            return [];
        }

        List<OdfDatabaseTableInfo> tables = [];
        foreach (OdfNode child in tableRepresentations.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == "table-representation" &&
                child.NamespaceUri == DatabaseNamespace)
            {
                tables.Add(new OdfDatabaseTableInfo(
                    child.GetAttribute("name", DatabaseNamespace) ?? string.Empty,
                    child.GetAttribute("command", DatabaseNamespace)));
            }
        }

        return tables.AsReadOnly();
    }

    /// <summary>
    /// 取得目前宣告的查詢描述清單。
    /// </summary>
    /// <returns>查詢描述清單。</returns>
    public IReadOnlyList<OdfDatabaseQueryInfo> GetQueries()
    {
        OdfNode? queries = FindChildElement(GetDatabaseNode(), "queries", DatabaseNamespace);
        if (queries is null)
        {
            return [];
        }

        List<OdfDatabaseQueryInfo> queryInfos = [];
        foreach (OdfNode child in queries.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == "query" &&
                child.NamespaceUri == DatabaseNamespace)
            {
                queryInfos.Add(new OdfDatabaseQueryInfo(
                    child.GetAttribute("name", DatabaseNamespace) ?? string.Empty,
                    child.GetAttribute("command", DatabaseNamespace) ?? string.Empty,
                    child.GetAttribute("title", DatabaseNamespace),
                    child.GetAttribute("description", DatabaseNamespace),
                    ParseNullableBoolean(child.GetAttribute("escape-processing", DatabaseNamespace))));
            }
        }

        return queryInfos.AsReadOnly();
    }

    /// <summary>
    /// 取得目前宣告的資料來源設定清單。
    /// </summary>
    /// <returns>資料來源設定清單。</returns>
    public IReadOnlyList<OdfDatabaseDataSourceSettingInfo> GetDataSourceSettings()
    {
        OdfNode? settings = FindDataSourceSettings();
        if (settings is null)
        {
            return [];
        }

        List<OdfDatabaseDataSourceSettingInfo> settingInfos = [];
        foreach (OdfNode child in settings.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == "data-source-setting" &&
                child.NamespaceUri == DatabaseNamespace)
            {
                List<string> values = [];
                foreach (OdfNode valueNode in child.Children)
                {
                    if (valueNode.NodeType is OdfNodeType.Element &&
                        valueNode.LocalName == "data-source-setting-value" &&
                        valueNode.NamespaceUri == DatabaseNamespace)
                    {
                        values.Add(valueNode.TextContent);
                    }
                }

                settingInfos.Add(new OdfDatabaseDataSourceSettingInfo(
                    child.GetAttribute("data-source-setting-name", DatabaseNamespace) ?? string.Empty,
                    ParseDataSourceSettingType(child.GetAttribute("data-source-setting-type", DatabaseNamespace)),
                    ParseNullableBoolean(child.GetAttribute("data-source-setting-is-list", DatabaseNamespace)),
                    values));
            }
        }

        return settingInfos.AsReadOnly();
    }

    /// <summary>
    /// 依名稱尋找資料表描述。
    /// </summary>
    /// <param name="name">資料表名稱。</param>
    /// <returns>符合名稱的資料表描述；找不到時為 <see langword="null"/>。</returns>
    public OdfDatabaseTableInfo? FindTable(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("資料表名稱不能為空。", nameof(name));
        }

        foreach (OdfDatabaseTableInfo table in GetTables())
        {
            if (string.Equals(table.Name, name, StringComparison.Ordinal))
            {
                return table;
            }
        }

        return null;
    }

    /// <summary>
    /// 依名稱尋找查詢描述。
    /// </summary>
    /// <param name="name">查詢名稱。</param>
    /// <returns>符合名稱的查詢描述；找不到時為 <see langword="null"/>。</returns>
    public OdfDatabaseQueryInfo? FindQuery(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("查詢名稱不能為空。", nameof(name));
        }

        foreach (OdfDatabaseQueryInfo query in GetQueries())
        {
            if (string.Equals(query.Name, name, StringComparison.Ordinal))
            {
                return query;
            }
        }

        return null;
    }

    /// <summary>
    /// 依名稱尋找資料來源設定。
    /// </summary>
    /// <param name="name">設定名稱。</param>
    /// <returns>符合名稱的資料來源設定；找不到時為 <see langword="null"/>。</returns>
    public OdfDatabaseDataSourceSettingInfo? FindDataSourceSetting(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("資料來源設定名稱不能為空。", nameof(name));
        }

        foreach (OdfDatabaseDataSourceSettingInfo setting in GetDataSourceSettings())
        {
            if (string.Equals(setting.Name, name, StringComparison.Ordinal))
            {
                return setting;
            }
        }

        return null;
    }

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
}
