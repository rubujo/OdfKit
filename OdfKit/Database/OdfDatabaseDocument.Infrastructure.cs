using System;
using System.Collections.Generic;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Database;

public partial class OdfDatabaseDocument
{
    #region Defaults, Merge & Helpers

    private static OdfDatabaseDocument EnsureDatabase(OdfDocument document)
    {
        if (document is OdfDatabaseDocument database)
        {
            return database;
        }

        document.Dispose();
        throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_SpecifiedOdfFileOdb"));
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
        var source = sourceDoc as OdfDatabaseDocument ?? throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_SourceFileOdfdatabasedocument"), nameof(sourceDoc));
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

    private OdfNode FindOrCreateDataSource()
    {
        OdfNode database = GetDatabaseNode();
        OdfNode? dataSource = FindChildElement(database, "data-source", DatabaseNamespace);
        if (dataSource is not null)
        {
            return dataSource;
        }

        dataSource = OdfNodeFactory.CreateElement("data-source", DatabaseNamespace, "db");
        OdfNode? firstAfterDataSource = FindFirstChildElement(
            database,
            ("forms", DatabaseNamespace),
            ("reports", DatabaseNamespace),
            ("queries", DatabaseNamespace),
            ("table-representations", DatabaseNamespace),
            ("schema-definition", DatabaseNamespace));
        if (firstAfterDataSource is null)
        {
            database.AppendChild(dataSource);
        }
        else
        {
            database.InsertBefore(dataSource, firstAfterDataSource);
        }

        return dataSource;
    }

    private OdfNode FindOrCreateDataSourceSettings()
    {
        OdfNode dataSource = FindOrCreateDataSource();
        OdfNode applicationSettings = FindOrCreateChild(dataSource, "application-connection-settings", DatabaseNamespace, "db");
        return FindOrCreateChild(applicationSettings, "data-source-settings", DatabaseNamespace, "db");
    }

    private OdfNode? FindDataSourceSettings()
    {
        OdfNode? dataSource = FindChildElement(GetDatabaseNode(), "data-source", DatabaseNamespace);
        OdfNode? applicationSettings = dataSource is null
            ? null
            : FindChildElement(dataSource, "application-connection-settings", DatabaseNamespace);
        return applicationSettings is null
            ? null
            : FindChildElement(applicationSettings, "data-source-settings", DatabaseNamespace);
    }

    private OdfNode? FindConnectionResource()
    {
        OdfNode? dataSource = FindChildElement(GetDatabaseNode(), "data-source", DatabaseNamespace);
        OdfNode? connectionData = dataSource is null
            ? null
            : FindChildElement(dataSource, "connection-data", DatabaseNamespace);
        OdfNode? connection = connectionData is null
            ? null
            : FindChildElement(connectionData, "connection-resource", DatabaseNamespace);
        return connection ?? FindChildElement(GetDatabaseNode(), "connection-resource", DatabaseNamespace);
    }

    private static OdfNode? FindChildElement(OdfNode parent, string localName, string namespaceUri)
    {
        foreach (OdfNode child in parent.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == localName &&
                child.NamespaceUri == namespaceUri)
            {
                return child;
            }
        }

        return null;
    }

    private static OdfNode? FindFirstChildElement(OdfNode parent, params (string LocalName, string NamespaceUri)[] names)
    {
        foreach (OdfNode child in parent.Children)
        {
            if (child.NodeType is not OdfNodeType.Element)
            {
                continue;
            }

            foreach ((string localName, string namespaceUri) in names)
            {
                if (child.LocalName == localName && child.NamespaceUri == namespaceUri)
                {
                    return child;
                }
            }
        }

        return null;
    }

    private static bool? ParseNullableBoolean(string? value)
    {
        if (value is null)
        {
            return null;
        }

        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "1", StringComparison.Ordinal);
    }

    private static OdfDatabaseDataSourceSettingType ParseDataSourceSettingType(string? value)
    {
        return value switch
        {
            "boolean" => OdfDatabaseDataSourceSettingType.Boolean,
            "double" => OdfDatabaseDataSourceSettingType.Double,
            "int" => OdfDatabaseDataSourceSettingType.Int,
            "long" => OdfDatabaseDataSourceSettingType.Long,
            "short" => OdfDatabaseDataSourceSettingType.Short,
            "string" => OdfDatabaseDataSourceSettingType.String,
            _ => OdfDatabaseDataSourceSettingType.String
        };
    }

    private static string ToDataSourceSettingTypeToken(OdfDatabaseDataSourceSettingType type)
    {
        return type switch
        {
            OdfDatabaseDataSourceSettingType.Boolean => "boolean",
            OdfDatabaseDataSourceSettingType.Double => "double",
            OdfDatabaseDataSourceSettingType.Int => "int",
            OdfDatabaseDataSourceSettingType.Long => "long",
            OdfDatabaseDataSourceSettingType.Short => "short",
            OdfDatabaseDataSourceSettingType.String => "string",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_UnknownDataSourceSetting"))
        };
    }

    #endregion
}
