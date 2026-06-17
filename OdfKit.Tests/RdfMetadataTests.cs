using System;
using System.IO;
using System.Linq;
using System.Text;
using OdfKit.Core;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證 ODF 封裝的 RDF metadata 支援。
/// </summary>
public class RdfMetadataTests
{
    [Fact]
    public void RdfMetadata_Save_WritesManifestRdfAndRoundTripsTriples()
    {
        using var stream = new MemoryStream();
        using (var package = OdfPackage.Create(stream, leaveOpen: true))
        {
            package.SetMimeType("application/vnd.oasis.opendocument.text");
            package.WriteEntry("content.xml", Encoding.UTF8.GetBytes("<content/>"), "text/xml");
            package.RdfMetadata.AddTriple(
                "./content.xml",
                "http://purl.org/dc/elements/1.1/title",
                "測試標題");
            package.RdfMetadata.AddTriple(
                "./content.xml",
                "urn:odfkit:test#related-resource",
                "Pictures/image.png",
                isLiteral: false);

            package.Save();
        }

        stream.Position = 0;
        using var reopened = OdfPackage.Open(stream, leaveOpen: true);

        Assert.True(reopened.HasEntry("META-INF/manifest.rdf"));
        Assert.Contains(
            reopened.RdfMetadata.Triples,
            triple => triple.Subject == "./content.xml" &&
                triple.Predicate == "http://purl.org/dc/elements/1.1/title" &&
                triple.ObjectValue == "測試標題" &&
                triple.IsLiteral);
        Assert.Contains(
            reopened.RdfMetadata.Triples,
            triple => triple.Subject == "./content.xml" &&
                triple.Predicate == "urn:odfkit:test#related-resource" &&
                triple.ObjectValue == "Pictures/image.png" &&
                !triple.IsLiteral);
    }

    [Fact]
    public void RdfMetadata_Load_ParsesExistingManifestRdf()
    {
        const string rdfXml =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\" xmlns:pkg=\"urn:odfkit:test#\">" +
            "<rdf:Description rdf:about=\"./content.xml\">" +
            "<pkg:label>內容</pkg:label>" +
            "<pkg:preview rdf:resource=\"Thumbnails/thumbnail.png\" />" +
            "</rdf:Description>" +
            "</rdf:RDF>";

        using var stream = new MemoryStream();
        using (var package = OdfPackage.Create(stream, leaveOpen: true))
        {
            package.SetMimeType("application/vnd.oasis.opendocument.text");
            package.WriteEntry("content.xml", Encoding.UTF8.GetBytes("<content/>"), "text/xml");
            package.WriteEntry("META-INF/manifest.rdf", Encoding.UTF8.GetBytes(rdfXml), "application/rdf+xml");
            package.Save();
        }

        stream.Position = 0;
        using var reopened = OdfPackage.Open(stream, leaveOpen: true);

        Assert.Equal(2, reopened.RdfMetadata.Triples.Count);
        Assert.Contains(reopened.RdfMetadata.Triples, triple => triple.Predicate == "urn:odfkit:test#label");
        Assert.Contains(reopened.RdfMetadata.Triples, triple => triple.Predicate == "urn:odfkit:test#preview" && !triple.IsLiteral);
    }

    /// <summary>
    /// 驗證 pkg ontology 便利 API 可建立、查詢與移除 triples。
    /// </summary>
    [Fact]
    public void RdfMetadata_PkgOntologyHelpersRoundTrip()
    {
        using var stream = new MemoryStream();
        using (var package = OdfPackage.Create(stream, leaveOpen: true))
        {
            package.SetMimeType("application/vnd.oasis.opendocument.text");
            package.WriteEntry("content.xml", Encoding.UTF8.GetBytes("<content/>"), "text/xml");
            package.RdfMetadata.LinkDocumentPart(string.Empty, "content.xml");
            package.RdfMetadata.SetPartMimeType("content.xml", "text/xml");
            package.Save();
        }

        stream.Position = 0;
        using var reopened = OdfPackage.Open(stream, leaveOpen: true);

        Assert.Single(reopened.RdfMetadata.FindTriples(string.Empty, OdfPkgRdfPredicates.HasPart));
        Assert.True(reopened.RdfMetadata.TryGetLiteral("content.xml", OdfPkgRdfPredicates.MimeType, out string mimeType));
        Assert.Equal("text/xml", mimeType);

        Assert.Equal(1, reopened.RdfMetadata.RemoveTriples("content.xml", OdfPkgRdfPredicates.MimeType));
        Assert.False(reopened.RdfMetadata.TryGetLiteral("content.xml", OdfPkgRdfPredicates.MimeType, out _));
    }

    /// <summary>
    /// 驗證 SyncWithPackageEntries 會為封裝項目建立 pkg:hasPart 與 pkg:mimeType。
    /// </summary>
    [Fact]
    public void SyncWithPackageEntries_AddsHasPartAndMimeTypeForEntries()
    {
        using var stream = new MemoryStream();
        using (var package = OdfPackage.Create(stream, leaveOpen: true))
        {
            package.SetMimeType("application/vnd.oasis.opendocument.text");
            package.WriteEntry("content.xml", Encoding.UTF8.GetBytes("<content/>"), "text/xml");
            package.WriteEntry("styles.xml", Encoding.UTF8.GetBytes("<styles/>"), "text/xml");
            package.RdfMetadata.AddTriple(
                string.Empty,
                "http://purl.org/dc/elements/1.1/title",
                "保留標題");

            int changed = package.SyncRdfMetadataWithEntries();

            Assert.True(changed >= 4);
            Assert.Equal(2, package.RdfMetadata.GetLinkedPartPaths(string.Empty).Count);
            Assert.True(package.RdfMetadata.TryGetLiteral("content.xml", OdfPkgRdfPredicates.MimeType, out string contentMime));
            Assert.Equal("text/xml", contentMime);
            Assert.Contains(
                package.RdfMetadata.Triples,
                triple => triple.Predicate == "http://purl.org/dc/elements/1.1/title");
        }
    }

    /// <summary>
    /// 驗證新增與移除封裝項目後，同步會更新 pkg triples。
    /// </summary>
    [Fact]
    public void SyncWithPackageEntries_ReflectsAddedAndRemovedEntries()
    {
        using var stream = new MemoryStream();
        using (var package = OdfPackage.Create(stream, leaveOpen: true))
        {
            package.SetMimeType("application/vnd.oasis.opendocument.text");
            package.WriteEntry("content.xml", Encoding.UTF8.GetBytes("<content/>"), "text/xml");
            package.RdfMetadata.LinkDocumentPart(string.Empty, "content.xml");
            package.RdfMetadata.SetPartMimeType("content.xml", "text/xml");

            package.WriteEntry("meta.xml", Encoding.UTF8.GetBytes("<meta/>"), "text/xml");
            package.SyncRdfMetadataWithEntries();
            Assert.Contains("meta.xml", package.RdfMetadata.GetLinkedPartPaths(string.Empty));

            package.RemoveEntry("meta.xml");
            package.SyncRdfMetadataWithEntries();
            Assert.DoesNotContain("meta.xml", package.RdfMetadata.GetLinkedPartPaths(string.Empty));
            Assert.False(package.RdfMetadata.TryGetLiteral("meta.xml", OdfPkgRdfPredicates.MimeType, out _));
        }
    }

    /// <summary>
    /// 驗證儲存時會自動同步 manifest.rdf 與封裝項目。
    /// </summary>
    [Fact]
    public void Save_AutoSyncsManifestRdfWithPackageEntries()
    {
        using var stream = new MemoryStream();
        using (var package = OdfPackage.Create(stream, leaveOpen: true))
        {
            package.SetMimeType("application/vnd.oasis.opendocument.text");
            package.WriteEntry("content.xml", Encoding.UTF8.GetBytes("<content/>"), "text/xml");
            package.RdfMetadata.LinkDocumentPart(string.Empty, "content.xml");
            package.RdfMetadata.SetPartMimeType("content.xml", "text/xml");
            package.Save();
        }

        stream.Position = 0;
        using (var package = OdfPackage.Open(stream, leaveOpen: true))
        {
            package.WriteEntry("styles.xml", Encoding.UTF8.GetBytes("<styles/>"), "text/xml");
            package.Save();
        }

        stream.Position = 0;
        using var reopened = OdfPackage.Open(stream, leaveOpen: true);

        Assert.Contains("content.xml", reopened.RdfMetadata.GetLinkedPartPaths(string.Empty));
        Assert.Contains("styles.xml", reopened.RdfMetadata.GetLinkedPartPaths(string.Empty));
        Assert.True(reopened.RdfMetadata.TryGetLiteral("styles.xml", OdfPkgRdfPredicates.MimeType, out string stylesMime));
        Assert.Equal("text/xml", stylesMime);
    }

    [Fact]
    public void RdfMetadata_Load_RejectsDtd()
    {
        const string rdfXml =
            "<!DOCTYPE rdf:RDF [<!ENTITY xxe SYSTEM \"file:///etc/passwd\">]>" +
            "<rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\">" +
            "<rdf:Description rdf:about=\"./content.xml\">" +
            "<label>&xxe;</label>" +
            "</rdf:Description>" +
            "</rdf:RDF>";

        using var stream = new MemoryStream();
        using (var package = OdfPackage.Create(stream, leaveOpen: true))
        {
            package.SetMimeType("application/vnd.oasis.opendocument.text");
            package.WriteEntry("content.xml", Encoding.UTF8.GetBytes("<content/>"), "text/xml");
            package.WriteEntry("META-INF/manifest.rdf", Encoding.UTF8.GetBytes(rdfXml), "application/rdf+xml");
            package.Save();
        }

        stream.Position = 0;

        Assert.Throws<InvalidDataException>(() => OdfPackage.Open(stream, leaveOpen: true));
    }
}
