using System;
using System.IO;
using System.Xml;
using OdfKit.DOM;

using OdfKit.Compliance;
namespace OdfKit.Core;

/// <summary>
/// ODF 封裝 RDF metadata 載入與儲存引擎（內部協作者）。
/// </summary>
internal static class OdfPackageRdfMetadataEngine
{
    internal const string RdfMetadataPath = "META-INF/manifest.rdf";

    /// <summary>
    /// 從封裝專案載入 RDF metadata。
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
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_OdfPackageRdfMetadataEngine_OdfRdfMetadataValid"), ex);
        }
    }

    /// <summary>
    /// 將已變更的 RDF metadata 序列化回封裝專案。
    /// </summary>
    internal static void Save(OdfPackage.OdfPackageSaveCollaborators ctx)
    {
        bool hadRdfEntry = ctx.Entries.ContainsKey(RdfMetadataPath);
        if (ctx.RdfMetadata.Triples.Count > 0 || ctx.RdfMetadata.IsDirty)
        {
            ctx.RdfMetadata.SyncWithPackageEntries(ctx.Entries.Keys, ctx.Manifest);
        }

        if (ctx.RdfMetadata.Triples.Count == 0)
            return;

        if (!ctx.RdfMetadata.IsDirty && !hadRdfEntry)
            return;

        byte[] content = OdfRdfParser.Serialize(ctx.RdfMetadata, ctx.SaveOptions.IndentXml);
        ctx.WriteEntry(RdfMetadataPath, content, "application/rdf+xml");
    }
}
