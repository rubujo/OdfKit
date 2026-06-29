using OdfKit.Compliance;
using OdfKit.Core;
using VDS.RDF;
using VDS.RDF.Query;
namespace OdfKit.Extensions.Rdf;

/// <summary>
/// Provides APIs for odf rdf metadata extensions.
/// 提供 <see cref="OdfRdfMetadata"/> 的 RDF 擴充方法。
/// </summary>
public static class OdfRdfMetadataExtensions
{
    /// <summary>
    /// Applies to graph.
    /// 將此 metadata 轉換為 dotNetRDF 圖形。
    /// </summary>
    /// <param name="metadata">The value to use. / 來源 RDF metadata</param>
    /// <param name="baseUri">The path or URI. / 選用的封裝基底 URI</param>
    /// <returns>The result. / 包含全部 triples 的圖形</returns>
    public static IGraph ToGraph(this OdfRdfMetadata metadata, Uri? baseUri = null) =>
        OdfRdfGraphBridge.ToGraph(metadata, baseUri);

    /// <summary>
    /// Provides execute sparql query.
    /// 對此 metadata 執行 SPARQL 查詢。
    /// </summary>
    /// <param name="metadata">The value to use. / 來源 RDF metadata</param>
    /// <param name="sparql">The value to use. / SPARQL 查詢字串</param>
    /// <param name="baseUri">The path or URI. / 選用的封裝基底 URI</param>
    /// <returns>The result. / 查詢結果</returns>
    public static object ExecuteSparqlQuery(this OdfRdfMetadata metadata, string sparql, Uri? baseUri = null) =>
        OdfRdfGraphBridge.ExecuteQuery(metadata, sparql, baseUri);

    /// <summary>
    /// Provides select sparql.
    /// 對此 metadata 執行 SPARQL SELECT 查詢。
    /// </summary>
    /// <param name="metadata">The value to use. / 來源 RDF metadata</param>
    /// <param name="sparql">The value to use. / SPARQL SELECT 查詢字串</param>
    /// <param name="baseUri">The path or URI. / 選用的封裝基底 URI</param>
    /// <returns>The result. / 查詢結果集</returns>
    public static SparqlResultSet SelectSparql(this OdfRdfMetadata metadata, string sparql, Uri? baseUri = null)
    {
        object result = OdfRdfGraphBridge.ExecuteQuery(metadata, sparql, baseUri);
        if (result is not SparqlResultSet resultSet)
        {
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfRdfMetadataExtensions_SparqlQueriesSelectForm"));
        }

        return resultSet;
    }
}
