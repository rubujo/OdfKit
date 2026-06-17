using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using OdfKit.Compliance;
using OdfKit.DOM;

namespace OdfKit.Core;

public sealed partial class OdfPackage
{
    #region Saving - Metadata & Manifest

    private void LoadRdfMetadata()
    {
        RdfMetadata = new OdfRdfMetadata();
        if (!_entries.TryGetValue(RdfMetadataPath, out var entry))
        {
            return;
        }

        try
        {
            using var stream = entry.OpenReader();
            RdfMetadata = OdfRdfParser.Parse(stream, _loadOptions.MaxXmlCharactersInDocument);
        }
        catch (XmlException ex)
        {
            throw new InvalidDataException("ODF RDF metadata 不是有效的 RDF/XML。", ex);
        }
    }

    private void SaveRdfMetadataToEntries()
    {
        if (!RdfMetadata.IsDirty || RdfMetadata.Triples.Count == 0)
        {
            return;
        }

        byte[] content = OdfRdfParser.Serialize(RdfMetadata, _saveOptions.IndentXml);
        WriteEntry(RdfMetadataPath, content, "application/rdf+xml");
    }

    internal void SaveManifestToEntries() => OdfPackageManifestWriter.WriteManifest(SaveCollaborators);


    #endregion
}
