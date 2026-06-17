using System;
using System.IO;
using System.Xml;
using OdfKit.DOM;

namespace OdfKit.Core;

/// <summary>
/// ODF 封裝 RDF metadata 載入與儲存引擎（內部協作者）。
/// </summary>
internal static class OdfPackageRdfMetadataEngine
{
    internal const string RdfMetadataPath = "META-INF/manifest.rdf";

    /// <summary>
    /// 從封裝項目載入 RDF metadata。
    /// </summary>
    internal static OdfRdfMetadata Load(OdfPackage.OdfPackageLoadCollaborators ctx)
    {
        var metadata = new OdfRdfMetadata();
        if (!ctx.Entries.TryGetValue(RdfMetadataPath, out var entry))
            return metadata;

        try
        {
            using var stream = entry.OpenReader();
            return OdfRdfParser.Parse(stream, ctx.LoadOptions.MaxXmlCharactersInDocument);
        }
        catch (XmlException ex)
        {
            throw new InvalidDataException("ODF RDF metadata 不是有效的 RDF/XML。", ex);
        }
    }

    /// <summary>
    /// 將已變更的 RDF metadata 序列化回封裝項目。
    /// </summary>
    internal static void Save(OdfPackage.OdfPackageSaveCollaborators ctx)
    {
        if (!ctx.RdfMetadata.IsDirty || ctx.RdfMetadata.Triples.Count == 0)
            return;

        byte[] content = OdfRdfParser.Serialize(ctx.RdfMetadata, ctx.SaveOptions.IndentXml);
        ctx.WriteEntry(RdfMetadataPath, content, "application/rdf+xml");
    }
}
