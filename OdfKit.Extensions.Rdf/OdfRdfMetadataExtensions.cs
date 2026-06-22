using OdfKit.Compliance;
using OdfKit.Core;
using VDS.RDF;
using VDS.RDF.Query;
namespace OdfKit.Extensions.Rdf;

/// <summary>
/// 提供 <see cref="OdfRdfMetadata"/> 的 RDF 擴充方法。
/// </summary>
public static class OdfRdfMetadataExtensions
{
    /// <summary>
    /// 將此 metadata 轉換為 dotNetRDF 圖形。
    /// </summary>
    /// <param name="metadata">來源 RDF metadata</param>
    /// <param name="baseUri">選用的封裝基底 URI</param>
    /// <returns>包含全部 triples 的圖形</returns>
    public static IGraph ToGraph(this OdfRdfMetadata metadata, Uri? baseUri = null) =>
        OdfRdfGraphBridge.ToGraph(metadata, baseUri);

    /// <summary>
    /// 對此 metadata 執行 SPARQL 查詢。
    /// </summary>
    /// <param name="metadata">來源 RDF metadata</param>
    /// <param name="sparql">SPARQL 查詢字串</param>
    /// <param name="baseUri">選用的封裝基底 URI</param>
    /// <returns>查詢結果</returns>
    public static object ExecuteSparqlQuery(this OdfRdfMetadata metadata, string sparql, Uri? baseUri = null) =>
        OdfRdfGraphBridge.ExecuteQuery(metadata, sparql, baseUri);

    /// <summary>
    /// 對此 metadata 執行 SPARQL SELECT 查詢。
    /// </summary>
    /// <param name="metadata">來源 RDF metadata</param>
    /// <param name="sparql">SPARQL SELECT 查詢字串</param>
    /// <param name="baseUri">選用的封裝基底 URI</param>
    /// <returns>查詢結果集</returns>
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
