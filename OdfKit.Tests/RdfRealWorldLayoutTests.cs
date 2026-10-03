using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using OdfKit.Core;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 真實的 LibreOffice 把中繼資料清單寫在封裝根目錄的 <c>manifest.rdf</c>，並大量使用 <c>rdf:type</c>；
/// OdfKit 曾只讀 <c>META-INF/manifest.rdf</c> 且略過 <c>rdf:*</c> 屬性，因此載入 LibreOffice 文件得到 0 個三元組。
/// </summary>
public sealed class RdfRealWorldLayoutTests
{
    // 內容取自 LibreOffice 26.2 輸出的 manifest.rdf。
    private const string LibreOfficeManifestRdf = """
        <?xml version="1.0" encoding="utf-8"?>
        <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
          <rdf:Description rdf:about="styles.xml">
            <rdf:type rdf:resource="http://docs.oasis-open.org/ns/office/1.2/meta/odf#StylesFile"/>
          </rdf:Description>
          <rdf:Description rdf:about="">
            <ns0:hasPart xmlns:ns0="http://docs.oasis-open.org/ns/office/1.2/meta/pkg#" rdf:resource="styles.xml"/>
          </rdf:Description>
          <rdf:Description rdf:about="content.xml">
            <rdf:type rdf:resource="http://docs.oasis-open.org/ns/office/1.2/meta/odf#ContentFile"/>
          </rdf:Description>
          <rdf:Description rdf:about="">
            <ns0:hasPart xmlns:ns0="http://docs.oasis-open.org/ns/office/1.2/meta/pkg#" rdf:resource="content.xml"/>
          </rdf:Description>
          <rdf:Description rdf:about="">
            <rdf:type rdf:resource="http://docs.oasis-open.org/ns/office/1.2/meta/pkg#Document"/>
          </rdf:Description>
        </rdf:RDF>
        """;

    private static MemoryStream CreatePackage(string rdfPath)
    {
        var stream = new MemoryStream();
        using (var package = OdfPackage.Create(stream, leaveOpen: true))
        {
            package.SetMimeType("application/vnd.oasis.opendocument.text");
            package.WriteEntry("content.xml", Encoding.UTF8.GetBytes("<content/>"), "text/xml");
            package.WriteEntry(rdfPath, Encoding.UTF8.GetBytes(LibreOfficeManifestRdf), "application/rdf+xml");
            package.Save();
        }

        stream.Position = 0;
        return stream;
    }

    /// <summary>
    /// 根目錄的 manifest.rdf 五個三元組（含三個 rdf:type）全部載入。
    /// </summary>
    [Fact]
    public void LibreOfficeManifestRdfLoadsAllTriplesIncludingRdfType()
    {
        using MemoryStream stream = CreatePackage("manifest.rdf");
        using var package = OdfPackage.Open(stream, leaveOpen: true);

        Assert.Equal(5, package.RdfMetadata.Triples.Count);
        const string type = "http://www.w3.org/1999/02/22-rdf-syntax-ns#type";
        Assert.Equal(3, package.RdfMetadata.Triples.Count(triple => triple.Predicate == type && !triple.IsLiteral));
        Assert.Contains(package.RdfMetadata.Triples, triple => triple.Subject == "content.xml" && triple.ObjectValue.EndsWith("#ContentFile", System.StringComparison.Ordinal));
    }

    /// <summary>
    /// 舊版 OdfKit 寫在 META-INF/manifest.rdf 的封裝仍能載入，存檔時搬到根目錄且不留下重複項目。
    /// </summary>
    [Fact]
    public void LegacyMetaInfLocationIsReadAndMigratedToTheRoot()
    {
        using MemoryStream stream = CreatePackage("META-INF/manifest.rdf");
        using var output = new MemoryStream();
        using (var package = OdfPackage.Open(stream, leaveOpen: true))
        {
            Assert.Equal(5, package.RdfMetadata.Triples.Count);
            package.RdfMetadata.AddTriple("content.xml", "http://purl.org/dc/terms/title", "標題");
            package.Save(output);
        }

        output.Position = 0;
        using var reopened = OdfPackage.Open(output, leaveOpen: true);
        Assert.True(reopened.HasEntry("manifest.rdf"));
        Assert.False(reopened.HasEntry("META-INF/manifest.rdf"));
        Assert.Equal("application/rdf+xml", reopened.Manifest["manifest.rdf"]);
        Assert.True(reopened.RdfMetadata.TryGetLiteral("content.xml", "http://purl.org/dc/terms/title", out string title));
        Assert.Equal("標題", title);
    }

    /// <summary>
    /// 存檔時 rdf:type 以標準的 rdf:type 元素輸出（不是自訂前綴的同名元素），其他 RDF 解析器能讀。
    /// </summary>
    [Fact]
    public void SavedRdfTypeUsesTheRdfPrefix()
    {
        using MemoryStream stream = CreatePackage("manifest.rdf");
        using var output = new MemoryStream();
        using (var package = OdfPackage.Open(stream, leaveOpen: true))
        {
            package.RdfMetadata.AddTriple("content.xml", "http://purl.org/dc/terms/title", "x");
            package.Save(output);
        }

        output.Position = 0;
        using var reopened = OdfPackage.Open(output, leaveOpen: true);
        using Stream rdf = reopened.GetEntryStream("manifest.rdf");
        XDocument document = XDocument.Load(rdf);
        XNamespace rdfNs = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        Assert.Equal(3, document.Descendants(rdfNs + "type").Count());
    }
}
