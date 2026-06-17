using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
    /// 非同步從指定路徑載入 ODB 資料庫文件。
    /// </summary>
    /// <param name="path">ODB 文件路徑。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="OdfDatabaseDocument"/>。</returns>
    public new static async Task<OdfDatabaseDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        EnsureDatabase(await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false));

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
    /// 非同步從指定資料流載入 ODB 資料庫文件。
    /// </summary>
    /// <param name="stream">包含 ODB 文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="OdfDatabaseDocument"/>。</returns>
    public new static async Task<OdfDatabaseDocument> LoadAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default) =>
        EnsureDatabase(await OdfDocumentFactory.LoadDocumentAsync(stream, fileName, cancellationToken).ConfigureAwait(false));

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
}
