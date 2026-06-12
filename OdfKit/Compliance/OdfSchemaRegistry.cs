using System;
using System.Collections.Generic;
using OdfKit.Core;

namespace OdfKit.Compliance;

/// <summary>
/// 提供驗證器與具類型 DOM 層所使用的版本化 ODF 結構描述中繼資料。
/// </summary>
public static class OdfSchemaRegistry
{
    private static readonly object SyncRoot = new();
    private static readonly OdfVersionRange AllKnown = OdfVersionRange.AllKnown;
    private static readonly Uri Odf14SchemaSource = new("https://docs.oasis-open.org/office/OpenDocument/v1.4/os/schemas/OpenDocument-v1.4-schema.rng");
    private static readonly OdfSchemaSet Odf14Seed = CreateOdf14Seed();
    private static readonly OdfSchemaSet Odf14Default = OdfGeneratedSchemaProvider.CreateOdf14(Odf14Seed);
    private static readonly Dictionary<OdfVersion, OdfSchemaSet> RegisteredSchemas = [];

    /// <summary>
    /// 取得此程式庫中可用的最新 ODF 結構描述中繼資料。
    /// </summary>
    public static OdfSchemaSet Latest => GetSchema(OdfVersion.Odf14);

    /// <summary>
    /// 取得 ODF 1.4 結構描述中繼資料種子。
    /// </summary>
    public static OdfSchemaSet Odf14 => GetSchema(OdfVersion.Odf14);

    /// <summary>
    /// Gets the clean default ODF 1.4 schema set.
    /// </summary>
    internal static OdfSchemaSet DefaultOdf14 => Odf14Default;

    /// <summary>
    /// 取得指定版本的結構描述中繼資料。
    /// </summary>
    /// <param name="version">ODF 版本</param>
    /// <returns>結構描述中繼資料集</returns>
    public static OdfSchemaSet GetSchema(OdfVersion version)
    {
        lock (SyncRoot)
        {
            return GetSchemaNoLock(version);
        }
    }

    /// <summary>
    /// 註冊指定版本的產出結構描述中繼資料，直到傳回的範圍被釋放為止。
    /// </summary>
    /// <param name="schema">要註冊的結構描述集</param>
    /// <param name="mergeWithExisting">是否與現有的結構描述合併</param>
    /// <param name="overwriteExisting">是否覆寫已存在的定義</param>
    /// <returns>用於控制註冊生命週期的 <see cref="IDisposable"/> 執行個體</returns>
    public static IDisposable RegisterSchema(
        OdfSchemaSet schema,
        bool mergeWithExisting = true,
        bool overwriteExisting = false)
    {
        if (schema is null) throw new ArgumentNullException(nameof(schema));

        lock (SyncRoot)
        {
            OdfSchemaSet? previous = RegisteredSchemas.TryGetValue(schema.Version, out OdfSchemaSet? existing)
                ? existing
                : null;
            OdfSchemaSet registered = mergeWithExisting
                ? GetSchemaNoLock(schema.Version).MergeWith(schema, overwriteExisting)
                : schema;
            RegisteredSchemas[schema.Version] = registered;
            return new RegistrationScope(schema.Version, previous);
        }
    }

    private static OdfSchemaSet GetSchemaNoLock(OdfVersion version)
    {
        if (RegisteredSchemas.TryGetValue(version, out OdfSchemaSet? registered))
        {
            return registered;
        }

        return version switch
        {
            OdfVersion.Odf14 => Odf14Default,
            OdfVersion.Odf13 => Odf14Default,
            OdfVersion.Odf12 => Odf14Default,
            OdfVersion.Odf11 => Odf14Default,
            OdfVersion.Odf10 => Odf14Default,
            _ => Odf14Default
        };
    }

    private sealed class RegistrationScope(OdfVersion version, OdfSchemaSet? previous) : IDisposable
    {
        private readonly OdfVersion _version = version;
        private readonly OdfSchemaSet? _previous = previous;
        private bool _disposed;

        public void Dispose()
        {
            lock (SyncRoot)
            {
                if (_disposed)
                {
                    return;
                }

                if (_previous is null)
                {
                    RegisteredSchemas.Remove(_version);
                }
                else
                {
                    RegisteredSchemas[_version] = _previous;
                }

                _disposed = true;
            }
        }
    }

    private static OdfSchemaSet CreateOdf14Seed()
    {
        List<OdfElementDefinition> elements =
        [
            Root("document"),
            Root("document-content"),
            Root("document-styles"),
            Root("document-meta"),
            Root("document-settings"),
            Element("body"),
            Body("text", OdfDocumentKind.Text),
            Body("spreadsheet", OdfDocumentKind.Spreadsheet),
            Body("presentation", OdfDocumentKind.Presentation),
            Body("drawing", OdfDocumentKind.Graphics),
            Body("chart", OdfDocumentKind.Chart),
            Body("formula", OdfDocumentKind.Formula),
            Body("image", OdfDocumentKind.Image),
            Body("database", OdfDocumentKind.Database),
            Element("scripts"),
            Element("font-face-decls"),
            Element("styles"),
            Element("automatic-styles"),
            Element("master-styles"),
            Element("meta"),
            Element("settings"),
            Element("dde-source-decls"),
            Element(OdfNamespaces.Text, "p"),
            Element(OdfNamespaces.Text, "h"),
            Element(OdfNamespaces.Text, "span"),
            Element(OdfNamespaces.Text, "list"),
            Element(OdfNamespaces.Text, "list-item"),
            Element(OdfNamespaces.Text, "section"),
            Element(OdfNamespaces.Text, "note"),
            Element(OdfNamespaces.Text, "annotation"),
            Element(OdfNamespaces.Table, "table"),
            Element(OdfNamespaces.Table, "table-column"),
            Element(OdfNamespaces.Table, "table-row"),
            Element(OdfNamespaces.Table, "table-cell"),
            Element(OdfNamespaces.Table, "covered-table-cell"),
            Element(OdfNamespaces.Draw, "page"),
            Element(OdfNamespaces.Draw, "frame"),
            Element(OdfNamespaces.Draw, "image"),
            Element(OdfNamespaces.Draw, "text-box"),
            Element(OdfNamespaces.Draw, "custom-shape"),
            Element(OdfNamespaces.Draw, "rect"),
            Element(OdfNamespaces.Draw, "ellipse"),
            Element(OdfNamespaces.Presentation, "notes"),
            Element(OdfNamespaces.Presentation, "placeholder"),
            Element(OdfNamespaces.Style, "style"),
            Element(OdfNamespaces.Style, "default-style"),
            Element(OdfNamespaces.Style, "master-page"),
            Element(OdfNamespaces.Style, "page-layout"),
            Element(OdfNamespaces.Style, "text-properties"),
            Element(OdfNamespaces.Style, "paragraph-properties"),
            Element(OdfNamespaces.Style, "table-cell-properties"),
            Element(OdfNamespaces.Number, "number-style"),
            Element(OdfNamespaces.Number, "date-style"),
            Element(OdfNamespaces.Number, "time-style"),
            Element(OdfNamespaces.Meta, "initial-creator"),
            Element(OdfNamespaces.Meta, "creation-date"),
            Element(OdfNamespaces.Meta, "editing-duration"),
            Element(OdfNamespaces.Config, "config-item-set"),
            Element(OdfNamespaces.Config, "config-item-map-indexed"),
            Element(OdfNamespaces.Config, "config-item-map-entry"),
            Element(OdfNamespaces.Config, "config-item")
        ];

        List<OdfAttributeDefinition> attributes =
        [
            Attribute("version", "odf-version", isRequiredOnDocumentRoot: true),
            Attribute("mimetype", "media-type"),
            Attribute(OdfNamespaces.Text, "style-name", "style-name"),
            Attribute(OdfNamespaces.Text, "outline-level", "positive-integer"),
            Attribute(OdfNamespaces.Table, "name", "string"),
            Attribute(OdfNamespaces.Table, "style-name", "style-name"),
            Attribute(OdfNamespaces.Table, "number-columns-repeated", "positive-integer"),
            Attribute(OdfNamespaces.Table, "number-rows-repeated", "positive-integer"),
            Attribute(OdfNamespaces.Office, "value-type", "string"),
            Attribute(OdfNamespaces.Office, "value", "number"),
            Attribute(OdfNamespaces.Office, "date-value", "date"),
            Attribute(OdfNamespaces.Office, "time-value", "time"),
            Attribute(OdfNamespaces.Office, "string-value", "string"),
            Attribute(OdfNamespaces.Office, "boolean-value", "boolean"),
            Attribute(OdfNamespaces.Draw, "name", "string"),
            Attribute(OdfNamespaces.Draw, "style-name", "style-name"),
            Attribute(OdfNamespaces.Draw, "text-style-name", "style-name"),
            Attribute(OdfNamespaces.Presentation, "style-name", "style-name"),
            Attribute(OdfNamespaces.Style, "name", "style-name"),
            Attribute(OdfNamespaces.Style, "family", "style-family"),
            Attribute(OdfNamespaces.Style, "parent-style-name", "style-name"),
            Attribute(OdfNamespaces.Config, "name", "string"),
            Attribute(OdfNamespaces.Config, "type", "string")
        ];

        return new OdfSchemaSet(OdfVersion.Odf14, Odf14SchemaSource, "2025-10-06", elements, attributes);
    }

    private static OdfElementDefinition Root(string localName)
    {
        return new OdfElementDefinition(
            new OdfQualifiedName(OdfNamespaces.Office, localName),
            OdfSchemaElementRole.DocumentRoot,
            AllKnown);
    }

    private static OdfElementDefinition Body(string localName, OdfDocumentKind documentKind)
    {
        return new OdfElementDefinition(
            new OdfQualifiedName(OdfNamespaces.Office, localName),
            OdfSchemaElementRole.BodyContent,
            AllKnown,
            documentKind);
    }

    private static OdfElementDefinition Element(string localName)
    {
        return new OdfElementDefinition(
            new OdfQualifiedName(OdfNamespaces.Office, localName),
            OdfSchemaElementRole.Element,
            AllKnown);
    }

    private static OdfElementDefinition Element(string namespaceUri, string localName)
    {
        return new OdfElementDefinition(
            new OdfQualifiedName(namespaceUri, localName),
            OdfSchemaElementRole.Element,
            AllKnown);
    }

    private static OdfAttributeDefinition Attribute(
        string localName,
        string valueType,
        bool isRequiredOnDocumentRoot = false)
    {
        return Attribute(OdfNamespaces.Office, localName, valueType, isRequiredOnDocumentRoot);
    }

    private static OdfAttributeDefinition Attribute(
        string namespaceUri,
        string localName,
        string valueType,
        bool isRequiredOnDocumentRoot = false)
    {
        return new OdfAttributeDefinition(
            new OdfQualifiedName(namespaceUri, localName),
            valueType,
            AllKnown,
            isRequiredOnDocumentRoot);
    }
}
