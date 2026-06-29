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
/// Minimal packaging wrapper representing an ODF database document (.odb).
/// 表示 ODF 資料庫文件 (.odb) 的最小封裝 wrapper。
/// </summary>
public partial class OdfDatabaseDocument : OdfDocument
{
    private const string DatabaseNamespace = "urn:oasis:names:tc:opendocument:xmlns:database:1.0";

    /// <summary>
    /// Initializes a new instance of the <see cref="OdfDatabaseDocument"/> class with the specified ODF package.
    /// 使用指定的 ODF 封裝初始化 <see cref="OdfDatabaseDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">The ODF package. / ODF 封裝。</param>
    public OdfDatabaseDocument(OdfPackage package) : base(package)
    {
        if (string.IsNullOrEmpty(package.MimeType))
        {
            // 真實 LibreOffice 對 ODF 資料庫文件採用的正式 OASIS 註冊 MIME 媒體類型為
            // application/vnd.oasis.opendocument.base（而非字面上看起來更直覺的 .database），
            // 寫入錯誤的媒體類型會導致 LibreOffice 的封裝偵測篩選器拒絕載入檔案。
            package.SetMimeType("application/vnd.oasis.opendocument.base");
        }
    }

    /// <summary>
    /// Creates a new ODB database document.
    /// 建立新的 ODB 資料庫文件。
    /// </summary>
    /// <returns>A new <see cref="OdfDatabaseDocument"/> instance. / 新的 <see cref="OdfDatabaseDocument"/> 執行個體。</returns>
    public static OdfDatabaseDocument Create()
    {
        return (OdfDatabaseDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.Database);
    }

    /// <summary>
    /// Loads an ODB database document from the specified path.
    /// 從指定路徑載入 ODB 資料庫文件。
    /// </summary>
    /// <param name="path">The ODB document path. / ODB 文件路徑。</param>
    /// <returns>The loaded <see cref="OdfDatabaseDocument"/> instance. / 載入完成的 <see cref="OdfDatabaseDocument"/> 執行個體。</returns>
    /// <exception cref="InvalidOperationException">When the specified document is not an ODB database. / 當指定文件不是 ODB 資料庫時擲出。</exception>
    public new static OdfDatabaseDocument Load(string path)
    {
        return EnsureDatabase(OdfDocumentFactory.LoadDocument(path));
    }

    /// <summary>
    /// Asynchronously loads an ODB database document from the specified path.
    /// 非同步從指定路徑載入 ODB 資料庫文件。
    /// </summary>
    /// <param name="path">The ODB document path. / ODB 文件路徑。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task representing the asynchronous load operation, whose result is the loaded <see cref="OdfDatabaseDocument"/>. / 代表非同步載入作業的工作，其結果為載入完成的 <see cref="OdfDatabaseDocument"/>。</returns>
    public new static async Task<OdfDatabaseDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        EnsureDatabase(await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Loads an ODB database document from the specified stream.
    /// 從指定資料流載入 ODB 資料庫文件。
    /// </summary>
    /// <param name="stream">The stream containing the ODB document content. / 包含 ODB 文件內容的資料流。</param>
    /// <param name="fileName">The optional file name, used to assist format detection. / 選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>The loaded <see cref="OdfDatabaseDocument"/> instance. / 載入完成的 <see cref="OdfDatabaseDocument"/> 執行個體。</returns>
    /// <exception cref="InvalidOperationException">When the specified document is not an ODB database. / 當指定文件不是 ODB 資料庫時擲出。</exception>
    public new static OdfDatabaseDocument Load(Stream stream, string? fileName = null)
    {
        return EnsureDatabase(OdfDocumentFactory.LoadDocument(stream, fileName));
    }

    /// <summary>
    /// Asynchronously loads an ODB database document from the specified stream.
    /// 非同步從指定資料流載入 ODB 資料庫文件。
    /// </summary>
    /// <param name="stream">The stream containing the ODB document content. / 包含 ODB 文件內容的資料流。</param>
    /// <param name="fileName">The optional file name, used to assist format detection. / 選用的檔案名稱，用於輔助格式偵測。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task representing the asynchronous load operation, whose result is the loaded <see cref="OdfDatabaseDocument"/>. / 代表非同步載入作業的工作，其結果為載入完成的 <see cref="OdfDatabaseDocument"/>。</returns>
    public new static async Task<OdfDatabaseDocument> LoadAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default) =>
        EnsureDatabase(await OdfDocumentFactory.LoadDocumentAsync(stream, fileName, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Gets the main database node.
    /// 取得主要資料庫節點。
    /// </summary>
    public OdfNode DatabaseNode => GetDatabaseNode();

    /// <summary>
    /// Gets the current data source connection reference.
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
    /// Gets the list of currently declared table descriptions.
    /// 取得目前宣告的資料表描述清單。
    /// </summary>
    public IReadOnlyList<OdfDatabaseTableInfo> Tables => GetTables();

    /// <summary>
    /// Gets the list of currently declared query descriptions.
    /// 取得目前宣告的查詢描述清單。
    /// </summary>
    public IReadOnlyList<OdfDatabaseQueryInfo> Queries => GetQueries();

    /// <summary>
    /// Gets the list of currently declared data source settings.
    /// 取得目前宣告的資料來源設定清單。
    /// </summary>
    public IReadOnlyList<OdfDatabaseDataSourceSettingInfo> DataSourceSettings => GetDataSourceSettings();

    /// <summary>
    /// Gets the list of currently declared form components.
    /// 取得目前宣告的表單元件清單。
    /// </summary>
    public IReadOnlyList<OdfDatabaseFormInfo> Forms => GetForms();

    /// <summary>
    /// Sets the data source connection reference.
    /// 設定資料來源連線參照。
    /// </summary>
    /// <param name="href">The connection resource path or URL. / 連線資源路徑或 URL。</param>
    public void SetConnection(string href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_WireCannotBeEmpty"), nameof(href));
        }

        OdfNode dataSource = FindOrCreateDataSource();
        OdfNode connectionData = FindOrCreateChild(dataSource, "connection-data", DatabaseNamespace, "db");
        OdfNode connection = FindOrCreateChild(connectionData, "connection-resource", DatabaseNamespace, "db");
        connection.SetAttribute("href", OdfNamespaces.XLink, href, "xlink");
        connection.SetAttribute("type", OdfNamespaces.XLink, "simple", "xlink");
    }

    /// <summary>
    /// Gets the list of currently declared table descriptions.
    /// 取得目前宣告的資料表描述清單。
    /// </summary>
    /// <returns>The list of table descriptions. / 資料表描述清單。</returns>
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
    /// Gets the list of currently declared query descriptions.
    /// 取得目前宣告的查詢描述清單。
    /// </summary>
    /// <returns>The list of query descriptions. / 查詢描述清單。</returns>
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
    /// Gets the list of currently declared data source settings.
    /// 取得目前宣告的資料來源設定清單。
    /// </summary>
    /// <returns>The list of data source settings. / 資料來源設定清單。</returns>
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
    /// Finds a table description by name.
    /// 依名稱尋找資料表描述。
    /// </summary>
    /// <param name="name">The table name. / 資料表名稱。</param>
    /// <returns>The matching table description, or <see langword="null"/> if not found. / 符合名稱的資料表描述；找不到時為 <see langword="null"/>。</returns>
    public OdfDatabaseTableInfo? FindTable(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_DataCannotBeEmpty"), nameof(name));
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
    /// Finds a query description by name.
    /// 依名稱尋找查詢描述。
    /// </summary>
    /// <param name="name">The query name. / 查詢名稱。</param>
    /// <returns>The matching query description, or <see langword="null"/> if not found. / 符合名稱的查詢描述；找不到時為 <see langword="null"/>。</returns>
    public OdfDatabaseQueryInfo? FindQuery(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_QueryCannotBeEmpty"), nameof(name));
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
    /// Gets the list of currently declared form components.
    /// 取得目前宣告的表單元件清單。
    /// </summary>
    /// <returns>The list of form components. / 表單元件清單。</returns>
    public IReadOnlyList<OdfDatabaseFormInfo> GetForms()
    {
        OdfNode? formsNode = FindChildElement(GetDatabaseNode(), "forms", DatabaseNamespace);
        if (formsNode is null)
        {
            return [];
        }

        List<OdfDatabaseFormInfo> forms = [];
        CollectFormComponents(formsNode, forms);
        return forms.AsReadOnly();
    }

    /// <summary>
    /// Finds a form component by name.
    /// 依名稱尋找表單元件。
    /// </summary>
    /// <param name="name">The form name. / 表單名稱。</param>
    /// <returns>The matching form component, or <see langword="null"/> if not found. / 符合名稱的表單元件；找不到時為 <see langword="null"/>。</returns>
    public OdfDatabaseFormInfo? FindForm(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_FormCannotBeEmpty"), nameof(name));
        }

        foreach (OdfDatabaseFormInfo form in GetForms())
        {
            if (string.Equals(form.Name, name, StringComparison.Ordinal))
            {
                return form;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds a data source setting by name.
    /// 依名稱尋找資料來源設定。
    /// </summary>
    /// <param name="name">The setting name. / 設定名稱。</param>
    /// <returns>The matching data source setting, or <see langword="null"/> if not found. / 符合名稱的資料來源設定；找不到時為 <see langword="null"/>。</returns>
    public OdfDatabaseDataSourceSettingInfo? FindDataSourceSetting(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_DataCannotBeEmpty_2"), nameof(name));
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

    private static void CollectFormComponents(OdfNode parent, List<OdfDatabaseFormInfo> forms)
    {
        foreach (OdfNode child in parent.Children)
        {
            if (child.NodeType is not OdfNodeType.Element || child.NamespaceUri != DatabaseNamespace)
            {
                continue;
            }

            if (child.LocalName == "component")
            {
                forms.Add(new OdfDatabaseFormInfo(
                    child.GetAttribute("name", DatabaseNamespace) ?? string.Empty,
                    child.GetAttribute("href", OdfNamespaces.XLink),
                    child.GetAttribute("title", DatabaseNamespace),
                    child.GetAttribute("description", DatabaseNamespace),
                    ParseNullableBoolean(child.GetAttribute("as-template", DatabaseNamespace))));
            }
            else if (child.LocalName == "component-collection")
            {
                CollectFormComponents(child, forms);
            }
        }
    }
}
