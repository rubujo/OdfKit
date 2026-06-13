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
public class OdfDatabaseDocument : OdfDocument
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
    /// 設定資料來源連線參照。
    /// </summary>
    /// <param name="href">連線資源路徑或 URL。</param>
    public void SetConnection(string href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            throw new ArgumentException("連線參照不能為空。", nameof(href));
        }

        OdfNode connection = FindOrCreateChild(GetDatabaseNode(), "connection-resource", DatabaseNamespace, "db");
        connection.SetAttribute("href", OdfNamespaces.XLink, href, "xlink");
        connection.SetAttribute("type", OdfNamespaces.XLink, "simple", "xlink");
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

    private static OdfDatabaseDocument EnsureDatabase(OdfDocument document)
    {
        if (document is OdfDatabaseDocument database)
        {
            return database;
        }

        document.Dispose();
        throw new InvalidOperationException("指定的 ODF 文件不是 ODB 資料庫。");
    }

    /// <summary>
    /// 取得預設的內容 XML 字串。
    /// </summary>
    /// <returns>預設的內容 XML 字串。</returns>
    protected override string GetDefaultContentXml()
    {
        return "<office:document-content " +
            "xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
            "xmlns:db=\"urn:oasis:names:tc:opendocument:xmlns:database:1.0\" " +
            "xmlns:xlink=\"http://www.w3.org/1999/xlink\" " +
            "office:version=\"" + OdfVersionInfo.DefaultVersionString + "\">" +
            "<office:body><office:database /></office:body>" +
            "</office:document-content>";
    }

    /// <summary>
    /// 取得預設的樣式 XML 字串。
    /// </summary>
    /// <returns>預設的樣式 XML 字串。</returns>
    protected override string GetDefaultStylesXml()
    {
        return "<office:document-styles " +
            "xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
            "xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" " +
            "office:version=\"" + OdfVersionInfo.DefaultVersionString + "\">" +
            "<office:styles /></office:document-styles>";
    }

    /// <summary>
    /// 合併來源資料庫文件的內容節點至此文件。
    /// </summary>
    /// <param name="sourceDoc">來源文件。</param>
    /// <param name="options">合併選項。</param>
    /// <param name="renameMap">樣式重新命名對照表。</param>
    /// <exception cref="ArgumentException">當來源文件不是 <see cref="OdfDatabaseDocument"/> 時擲出。</exception>
    protected override void MergeContentNodes(OdfDocument sourceDoc, OdfMergeOptions options, Dictionary<string, string> renameMap)
    {
        var source = sourceDoc as OdfDatabaseDocument ?? throw new ArgumentException("來源文件必須是 OdfDatabaseDocument。", nameof(sourceDoc));
        OdfNode sourceBody = source.FindOrCreateChild(source.ContentDom, "body", OdfNamespaces.Office, "office");
        OdfNode sourceDatabase = source.FindOrCreateChild(sourceBody, "database", OdfNamespaces.Office, "office");
        OdfNode body = FindOrCreateChild(ContentDom, "body", OdfNamespaces.Office, "office");
        OdfNode database = FindOrCreateChild(body, "database", OdfNamespaces.Office, "office");

        foreach (OdfNode child in sourceDatabase.Children)
        {
            if (child.NodeType == OdfNodeType.Element)
            {
                database.AppendChild(OdfNode.ImportNode(child, source.Package, Package));
            }
        }
    }

    private OdfNode GetDatabaseNode()
    {
        OdfNode body = FindOrCreateChild(ContentDom, "body", OdfNamespaces.Office, "office");
        return FindOrCreateChild(body, "database", OdfNamespaces.Office, "office");
    }
}
