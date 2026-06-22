using OdfKit.Compliance;
using OdfKit.Core;
using VDS.RDF;
using VDS.RDF.Query;
namespace OdfKit.Extensions.Rdf;

/// <summary>
/// 提供 <see cref="OdfRdfMetadata"/> 與 dotNetRDF <see cref="IGraph"/> 之間的橋接與 SPARQL 查詢。
/// </summary>
public static class OdfRdfGraphBridge
{
    /// <summary>
    /// 將 OdfKit RDF metadata 轉換為 dotNetRDF 圖形。
    /// </summary>
    /// <param name="metadata">來源 RDF metadata</param>
    /// <param name="baseUri">選用的封裝基底 URI；空白主詞會對應至此 URI</param>
    /// <returns>包含全部 triples 的圖形</returns>
    /// <exception cref="ArgumentNullException">當 <paramref name="metadata"/> 為 <see langword="null"/> 時擲出</exception>
    public static IGraph ToGraph(OdfRdfMetadata metadata, Uri? baseUri = null)
    {
        if (metadata is null)
        {
            throw new ArgumentNullException(nameof(metadata));
        }

        Uri graphBase = baseUri ?? OdfRdfGraphUris.DefaultPackageBaseUri;
        var graph = new Graph(graphBase);
        foreach (OdfRdfTriple triple in metadata.Triples)
        {
            INode subject = CreateUriNode(graph, triple.Subject, graphBase);
            INode predicate = graph.CreateUriNode(new Uri(triple.Predicate));
            INode obj = triple.IsLiteral
                ? graph.CreateLiteralNode(triple.ObjectValue)
                : CreateUriNode(graph, triple.ObjectValue, graphBase);
            graph.Assert(new Triple(subject, predicate, obj));
        }

        return graph;
    }

    /// <summary>
    /// 對 OdfKit RDF metadata 執行 SPARQL 查詢。
    /// </summary>
    /// <param name="metadata">來源 RDF metadata</param>
    /// <param name="sparql">SPARQL 查詢字串（支援 SELECT 與 ASK）</param>
    /// <param name="baseUri">選用的封裝基底 URI</param>
    /// <returns>查詢結果；SELECT 為 <see cref="SparqlResultSet"/>，ASK 為 <see cref="bool"/></returns>
    /// <exception cref="ArgumentNullException">當 <paramref name="metadata"/> 為 <see langword="null"/> 時擲出</exception>
    /// <exception cref="ArgumentException">當 <paramref name="sparql"/> 為空白時擲出</exception>
    /// <exception cref="InvalidOperationException">當查詢類型不受支援時擲出</exception>
    public static object ExecuteQuery(OdfRdfMetadata metadata, string sparql, Uri? baseUri = null)
    {
        if (metadata is null)
        {
            throw new ArgumentNullException(nameof(metadata));
        }

        if (string.IsNullOrWhiteSpace(sparql))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfRdfGraphBridge_SparqlCannotBeEmpty"), nameof(sparql));
        }

        IGraph graph = ToGraph(metadata, baseUri);
        object result = graph.ExecuteQuery(sparql);
        if (result is SparqlResultSet resultSet && resultSet.ResultsType == SparqlResultsType.Boolean)
        {
            return resultSet.Result;
        }

        return result switch
        {
            SparqlResultSet or bool => result,
            _ => throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfRdfGraphBridge_OnlySelectAskQuery"))
        };
    }

    /// <summary>
    /// 將 dotNetRDF 圖形中的 triples 匯入至 OdfKit metadata（追加模式）。
    /// </summary>
    /// <param name="metadata">目標 RDF metadata</param>
    /// <param name="graph">來源圖形</param>
    /// <param name="baseUri">選用的封裝基底 URI</param>
    /// <returns>新增的 triple 數量</returns>
    /// <exception cref="ArgumentNullException">當必要參數為 <see langword="null"/> 時擲出</exception>
    public static int ImportGraph(OdfRdfMetadata metadata, IGraph graph, Uri? baseUri = null)
    {
        if (metadata is null)
        {
            throw new ArgumentNullException(nameof(metadata));
        }

        if (graph is null)
        {
            throw new ArgumentNullException(nameof(graph));
        }

        Uri graphBase = baseUri ?? OdfRdfGraphUris.DefaultPackageBaseUri;
        int imported = 0;
        foreach (Triple triple in graph.Triples)
        {
            if (triple.Subject.NodeType != NodeType.Uri || triple.Predicate.NodeType != NodeType.Uri)
            {
                continue;
            }

            string subject = OdfRdfGraphUris.ToSubjectString(((IUriNode)triple.Subject).Uri, graphBase);
            string predicate = ((IUriNode)triple.Predicate).Uri.AbsoluteUri;
            if (triple.Object.NodeType == NodeType.Literal)
            {
                metadata.AddTriple(subject, predicate, ((ILiteralNode)triple.Object).Value, isLiteral: true);
                imported++;
                continue;
            }

            if (triple.Object.NodeType == NodeType.Uri)
            {
                string objectValue = OdfRdfGraphUris.ToSubjectString(((IUriNode)triple.Object).Uri, graphBase);
                metadata.AddTriple(subject, predicate, objectValue, isLiteral: false);
                imported++;
            }
        }

        return imported;
    }

    private static INode CreateUriNode(IGraph graph, string value, Uri graphBase)
    {
        return graph.CreateUriNode(OdfRdfGraphUris.ResolveSubjectUri(value, graphBase));
    }
}
