using System.Text;
using OdfKit.Core;
using OdfKit.Extensions.Rdf;
using VDS.RDF;
using VDS.RDF.Nodes;
using VDS.RDF.Query;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證 OdfKit.Extensions.Rdf 與 dotNetRDF 的橋接與 SPARQL 查詢。
/// </summary>
public class RdfExtensionTests
{
    /// <summary>
    /// 驗證 <see cref="OdfRdfGraphBridge.ToGraph"/> 可反映 OdfKit triple 數量。
    /// </summary>
    [Fact]
    public void ToGraph_ReflectsMetadataTripleCount()
    {
        var metadata = new OdfRdfMetadata();
        metadata.LinkDocumentPart(string.Empty, "content.xml");
        metadata.SetPartMimeType("content.xml", "text/xml");
        metadata.AddTriple(string.Empty, "http://purl.org/dc/elements/1.1/title", "測試標題");

        IGraph graph = metadata.ToGraph();
        Assert.Equal(3, graph.Triples.Count);
    }

    /// <summary>
    /// 驗證 SPARQL SELECT 可讀回 Dublin Core 標題。
    /// </summary>
    [Fact]
    public void SelectSparql_ReturnsDublinCoreTitle()
    {
        var metadata = new OdfRdfMetadata();
        metadata.AddTriple(string.Empty, "http://purl.org/dc/elements/1.1/title", "季度報表");

        const string sparql =
            "PREFIX dc: <http://purl.org/dc/elements/1.1/> " +
            "SELECT ?title WHERE { ?doc dc:title ?title . }";

        SparqlResultSet results = metadata.SelectSparql(sparql);
        Assert.False(results.IsEmpty);
        Assert.Equal("季度報表", results[0]["title"].AsValuedNode().AsString());
    }

    /// <summary>
    /// 驗證 SPARQL SELECT 可列舉 pkg:hasPart 連結的封裝組件。
    /// </summary>
    [Fact]
    public void SelectSparql_ReturnsLinkedPackageParts()
    {
        var metadata = new OdfRdfMetadata();
        metadata.LinkDocumentPart(string.Empty, "content.xml");
        metadata.LinkDocumentPart(string.Empty, "styles.xml");

        const string sparql =
            "PREFIX pkg: <http://docs.oasis-open.org/ns/office/1.2/meta/pkg#> " +
            "SELECT ?part WHERE { ?doc pkg:hasPart ?part . } ORDER BY ?part";

        SparqlResultSet results = metadata.SelectSparql(sparql);
        Assert.Equal(2, results.Count);
        Assert.Equal("content.xml", OdfRdfGraphUris.ToSubjectString(new Uri(results[0]["part"].AsValuedNode().AsString())));
        Assert.Equal("styles.xml", OdfRdfGraphUris.ToSubjectString(new Uri(results[1]["part"].AsValuedNode().AsString())));
    }

    /// <summary>
    /// 驗證 SPARQL ASK 可判斷 pkg:mimeType 是否存在。
    /// </summary>
    [Fact]
    public void ExecuteQuery_AskDetectsMimeTypeTriple()
    {
        var metadata = new OdfRdfMetadata();
        metadata.SetPartMimeType("content.xml", "text/xml");

        const string sparql =
            "PREFIX pkg: <http://docs.oasis-open.org/ns/office/1.2/meta/pkg#> " +
            "ASK WHERE { ?part pkg:mimeType \"text/xml\" . }";

        object result = metadata.ExecuteSparqlQuery(sparql);
        Assert.IsType<bool>(result);
        Assert.True((bool)result);
    }

    /// <summary>
    /// 驗證圖形匯入可將 dotNetRDF triples 還原至 OdfKit metadata。
    /// </summary>
    [Fact]
    public void ImportGraph_RoundTripsThroughMetadata()
    {
        var source = new OdfRdfMetadata();
        source.LinkDocumentPart(string.Empty, "content.xml");
        source.SetPartMimeType("content.xml", "text/xml");
        source.AddTriple(string.Empty, "http://purl.org/dc/elements/1.1/title", "匯入測試");

        IGraph graph = source.ToGraph();
        var target = new OdfRdfMetadata();
        int imported = OdfRdfGraphBridge.ImportGraph(target, graph);

        Assert.Equal(3, imported);
        Assert.Equal(3, target.Triples.Count);
        Assert.True(target.TryGetLiteral(string.Empty, "http://purl.org/dc/elements/1.1/title", out string title));
        Assert.Equal("匯入測試", title);
        Assert.Contains("content.xml", target.GetLinkedPartPaths(string.Empty));
        Assert.True(target.TryGetLiteral("content.xml", OdfPkgRdfPredicates.MimeType, out string mimeType));
        Assert.Equal("text/xml", mimeType);
    }

    /// <summary>
    /// 驗證封裝層 RDF metadata 可透過 SPARQL 查詢讀回標題。
    /// </summary>
    [Fact]
    public void PackageMetadata_SelectSparqlAfterSaveAndLoad()
    {
        const string dublinCoreTitle = "http://purl.org/dc/elements/1.1/title";
        using var stream = new MemoryStream();
        using (var package = OdfPackage.Create(stream, leaveOpen: true))
        {
            package.SetMimeType("application/vnd.oasis.opendocument.text");
            package.WriteEntry("content.xml", Encoding.UTF8.GetBytes("<content/>"), "text/xml");
            package.RdfMetadata.AddTriple(string.Empty, dublinCoreTitle, "封裝標題");
            package.Save();
        }

        stream.Position = 0;
        using var reopened = OdfPackage.Open(stream, leaveOpen: true);

        const string sparql =
            "PREFIX dc: <http://purl.org/dc/elements/1.1/> " +
            "SELECT ?title WHERE { ?doc dc:title ?title . }";

        SparqlResultSet results = reopened.RdfMetadata.SelectSparql(sparql);
        Assert.Equal("封裝標題", results[0]["title"].AsValuedNode().AsString());
    }
}
