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
    private static readonly Uri Odf13SchemaSource = new("https://docs.oasis-open.org/office/OpenDocument/v1.3/os/schemas/OpenDocument-v1.3-schema.rng");
    private static readonly Uri Odf12SchemaSource = new("https://docs.oasis-open.org/office/v1.2/os/OpenDocument-v1.2-os-schema.rng");
    private static readonly Uri Odf11SchemaSource = new("https://docs.oasis-open.org/office/v1.1/OS/OpenDocument-schema-v1.1.rng");
    private static readonly OdfSchemaSet Odf14Seed = CreateOdf14Seed();
    private static readonly OdfSchemaSet Odf14Default = OdfGeneratedSchemaProvider.CreateOdf14(Odf14Seed);

    // ODF 1.1/1.2/1.3：以官方獨立 RNG 衍生的真實版本專屬 schema（非從 1.4 過濾的近似值）。
    private static readonly OdfSchemaSet Odf13Default = OdfGeneratedSchemaProvider.CreateOdf13(
        CreateSeed(OdfVersion.Odf13, Odf13SchemaSource, "2026-06-16"));
    private static readonly OdfSchemaSet Odf12Default = OdfGeneratedSchemaProvider.CreateOdf12(
        CreateSeed(OdfVersion.Odf12, Odf12SchemaSource, "2026-06-16"));
    private static readonly OdfSchemaSet Odf11Default = OdfGeneratedSchemaProvider.CreateOdf11(
        CreateSeed(OdfVersion.Odf11, Odf11SchemaSource, "2026-06-16"));

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
    /// 取得未經額外覆寫的預設 ODF 1.4 結構描述集。
    /// </summary>
    internal static OdfSchemaSet DefaultOdf14 => Odf14Default;

    /// <summary>
    /// 指出指定版本是否有官方獨立 RNG 衍生的真實 schema（而非以 1.4 schema 近似的 best-effort 結果）。
    /// 目前 ODF 1.1/1.2/1.3/1.4 皆有官方 RNG；ODF 1.0 因 OASIS 未發布獨立 RNG，
    /// 仍維持以 1.4 schema 近似驗證。
    /// </summary>
    internal static bool HasNativeSchema(OdfVersion version)
    {
        return version is OdfVersion.Odf11 or OdfVersion.Odf12 or OdfVersion.Odf13 or OdfVersion.Odf14;
    }

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
        if (schema is null)
            throw new ArgumentNullException(nameof(schema));

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

        switch (version)
        {
            case OdfVersion.Odf14:
                return Odf14Default;
            case OdfVersion.Odf13:
                return Odf13Default;
            case OdfVersion.Odf12:
                return Odf12Default;
            case OdfVersion.Odf11:
                return Odf11Default;
            default:
                // ODF 1.0：OASIS 從未發布獨立 RNG schema 檔案（官方目錄僅提供規格 PDF），
                // 因此沒有真實 schema 可用。此處維持既有的已知限制：以 ODF 1.4 schema
                // 進行 best-effort 近似驗證（過濾掉不支援該版本的元素與屬性）。
                var schema = CreateApproximateSchema(version, Odf14Default);
                RegisteredSchemas[version] = schema;
                return schema;
        }
    }

    private static OdfSchemaSet CreateApproximateSchema(OdfVersion version, OdfSchemaSet baseSchema)
    {
        var filteredElements = baseSchema.Elements.Values
            .Where(e => e.SupportedVersions.Contains(version));
        var filteredAttributes = baseSchema.Attributes.Values
            .Where(a => a.SupportedVersions.Contains(version));

        return new OdfSchemaSet(
            version,
            baseSchema.SourceUrl,
            baseSchema.SourceDate,
            filteredElements,
            filteredAttributes,
            baseSchema.NameClasses,
            baseSchema.Patterns.Values);
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
        return CreateSeed(OdfVersion.Odf14, Odf14SchemaSource, "2025-10-06");
    }

    private static OdfSchemaSet CreateSeed(OdfVersion version, Uri source, string sourceDate)
    {
        OdfVersionRange versionRange = version == OdfVersion.Odf14 ? AllKnown : OdfVersionRange.Exact(version);

        OdfElementDefinition Root(string localName) => new(
            new OdfQualifiedName(OdfNamespaces.Office, localName),
            OdfSchemaElementRole.DocumentRoot,
            versionRange);

        OdfElementDefinition Body(string localName, OdfDocumentKind documentKind) => new(
            new OdfQualifiedName(OdfNamespaces.Office, localName),
            OdfSchemaElementRole.BodyContent,
            versionRange,
            documentKind);

        OdfElementDefinition Element(string localName) => new(
            new OdfQualifiedName(OdfNamespaces.Office, localName),
            OdfSchemaElementRole.Element,
            versionRange);

        OdfElementDefinition ElementNs(string namespaceUri, string localName) => new(
            new OdfQualifiedName(namespaceUri, localName),
            OdfSchemaElementRole.Element,
            versionRange);

        OdfAttributeDefinition Attribute(string localName, string valueType, bool isRequiredOnDocumentRoot = false) =>
            AttributeNs(OdfNamespaces.Office, localName, valueType, isRequiredOnDocumentRoot);

        OdfAttributeDefinition AttributeNs(string namespaceUri, string localName, string valueType, bool isRequiredOnDocumentRoot = false) => new(
            new OdfQualifiedName(namespaceUri, localName),
            valueType,
            versionRange,
            isRequiredOnDocumentRoot);

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
            Element("event-listeners"),
            ElementNs(OdfNamespaces.Script, "event-listener"),
            Element("font-face-decls"),
            Element("styles"),
            Element("automatic-styles"),
            Element("master-styles"),
            Element("meta"),
            Element("settings"),
            Element("dde-source-decls"),
            ElementNs(OdfNamespaces.Text, "p"),
            ElementNs(OdfNamespaces.Text, "h"),
            ElementNs(OdfNamespaces.Text, "span"),
            ElementNs(OdfNamespaces.Text, "list"),
            ElementNs(OdfNamespaces.Text, "list-item"),
            ElementNs(OdfNamespaces.Text, "section"),
            ElementNs(OdfNamespaces.Text, "note"),
            ElementNs(OdfNamespaces.Text, "annotation"),
            ElementNs(OdfNamespaces.Table, "table"),
            ElementNs(OdfNamespaces.Table, "table-column"),
            ElementNs(OdfNamespaces.Table, "table-row"),
            ElementNs(OdfNamespaces.Table, "table-cell"),
            ElementNs(OdfNamespaces.Table, "covered-table-cell"),
            ElementNs(OdfNamespaces.Draw, "page"),
            ElementNs(OdfNamespaces.Draw, "frame"),
            ElementNs(OdfNamespaces.Draw, "image"),
            ElementNs(OdfNamespaces.Draw, "text-box"),
            ElementNs(OdfNamespaces.Draw, "custom-shape"),
            ElementNs(OdfNamespaces.Draw, "rect"),
            ElementNs(OdfNamespaces.Draw, "ellipse"),
            ElementNs(OdfNamespaces.Presentation, "notes"),
            ElementNs(OdfNamespaces.Presentation, "placeholder"),
            ElementNs(OdfNamespaces.Style, "style"),
            ElementNs(OdfNamespaces.Style, "default-style"),
            ElementNs(OdfNamespaces.Style, "master-page"),
            ElementNs(OdfNamespaces.Style, "page-layout"),
            ElementNs(OdfNamespaces.Style, "text-properties"),
            ElementNs(OdfNamespaces.Style, "paragraph-properties"),
            ElementNs(OdfNamespaces.Style, "table-cell-properties"),
            ElementNs(OdfNamespaces.Number, "number-style"),
            ElementNs(OdfNamespaces.Number, "date-style"),
            ElementNs(OdfNamespaces.Number, "time-style"),
            ElementNs(OdfNamespaces.Meta, "initial-creator"),
            ElementNs(OdfNamespaces.Meta, "creation-date"),
            ElementNs(OdfNamespaces.Meta, "editing-duration"),
            ElementNs(OdfNamespaces.Config, "config-item-set"),
            ElementNs(OdfNamespaces.Config, "config-item-map-indexed"),
            ElementNs(OdfNamespaces.Config, "config-item-map-entry"),
            ElementNs(OdfNamespaces.Config, "config-item")
        ];

        List<OdfAttributeDefinition> attributes =
        [
            Attribute("version", "odf-version", isRequiredOnDocumentRoot: true),
            Attribute("mimetype", "media-type"),
            AttributeNs(OdfNamespaces.Text, "style-name", "style-name"),
            AttributeNs(OdfNamespaces.Text, "outline-level", "positive-integer"),
            AttributeNs(OdfNamespaces.Table, "name", "string"),
            AttributeNs(OdfNamespaces.Table, "style-name", "style-name"),
            AttributeNs(OdfNamespaces.Table, "number-columns-repeated", "positive-integer"),
            AttributeNs(OdfNamespaces.Table, "number-rows-repeated", "positive-integer"),
            AttributeNs(OdfNamespaces.Office, "value-type", "string"),
            AttributeNs(OdfNamespaces.Office, "value", "number"),
            AttributeNs(OdfNamespaces.Office, "date-value", "date"),
            AttributeNs(OdfNamespaces.Office, "time-value", "time"),
            AttributeNs(OdfNamespaces.Office, "string-value", "string"),
            AttributeNs(OdfNamespaces.Office, "boolean-value", "boolean"),
            AttributeNs(OdfNamespaces.Draw, "name", "string"),
            AttributeNs(OdfNamespaces.Draw, "style-name", "style-name"),
            AttributeNs(OdfNamespaces.Draw, "text-style-name", "style-name"),
            AttributeNs(OdfNamespaces.Presentation, "style-name", "style-name"),
            AttributeNs(OdfNamespaces.Style, "name", "style-name"),
            AttributeNs(OdfNamespaces.Style, "family", "style-family"),
            AttributeNs(OdfNamespaces.Style, "parent-style-name", "style-name"),
            AttributeNs(OdfNamespaces.Config, "name", "string"),
            AttributeNs(OdfNamespaces.Config, "type", "string"),
            AttributeNs(OdfNamespaces.Script, "event-name", "string"),
            AttributeNs(OdfNamespaces.Script, "language", "string"),
            AttributeNs(OdfNamespaces.Script, "macro-name", "string"),
            AttributeNs(OdfNamespaces.XLink, "href", "string"),
            AttributeNs(OdfNamespaces.XLink, "type", "string")
        ];

        return new OdfSchemaSet(version, source, sourceDate, elements, attributes);
    }
}
