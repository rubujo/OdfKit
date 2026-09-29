using OdfKit.Compliance;
using OdfKit.Core;
using VDS.RDF;
using VDS.RDF.Query;
namespace OdfKit.Extensions.Rdf;

/// <summary>
/// Bridges ODF package RDF metadata and in-memory RDF graphs.
/// 提供 <see cref="OdfRdfMetadata"/> 與 dotNetRDF <see cref="IGraph"/> 之間的橋接與 SPARQL 查詢。
/// </summary>
public static class OdfRdfGraphBridge
{
    /// <summary>
    /// Converts package RDF metadata to an in-memory RDF graph.
    /// 將 OdfKit RDF metadata 轉換為 dotNetRDF 圖形。
    /// </summary>
    /// <param name="metadata">The value to use. / 來源 RDF metadata</param>
    /// <param name="baseUri">The path or URI. / 選用的封裝基底 URI；空白主詞會對應至此 URI</param>
    /// <returns>The result. / 包含全部 triples 的圖形</returns>
    /// <exception cref="ArgumentNullException">Thrown when the documented condition occurs. / 當 <paramref name="metadata"/> 為 <see langword="null"/> 時擲出</exception>
    public static IGraph ToGraph(OdfRdfMetadata metadata, Uri? baseUri = null)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(metadata, nameof(metadata));

        Uri graphBase = baseUri ?? OdfRdfGraphUris.DefaultPackageBaseUri;
        var graph = new Graph(graphBase);
        foreach (OdfRdfTriple triple in metadata.Triples)
        {
            INode subject = CreateUriNode(graph, triple.Subject, graphBase);
            // predicate 同樣需相對於 graphBase 解析：subject／object 已支援相對 IRI，
            // 若此處以 new Uri 直接建立，相對路徑 predicate 會擲 UriFormatException。
            INode predicate = CreateUriNode(graph, triple.Predicate, graphBase);
            INode obj = triple.IsLiteral
                ? graph.CreateLiteralNode(triple.ObjectValue)
                : CreateUriNode(graph, triple.ObjectValue, graphBase);
            graph.Assert(new Triple(subject, predicate, obj));
        }

        return graph;
    }

    /// <summary>
    /// Executes a SPARQL query against package RDF metadata.
    /// 對 OdfKit RDF metadata 執行 SPARQL 查詢。
    /// </summary>
    /// <param name="metadata">The value to use. / 來源 RDF metadata</param>
    /// <param name="sparql">The value to use. / SPARQL 查詢字串（支援 SELECT 與 ASK）</param>
    /// <param name="baseUri">The path or URI. / 選用的封裝基底 URI</param>
    /// <returns>The result. / 查詢結果；SELECT 為 <see cref="SparqlResultSet"/>，ASK 為 <see cref="bool"/></returns>
    /// <exception cref="ArgumentNullException">Thrown when the documented condition occurs. / 當 <paramref name="metadata"/> 為 <see langword="null"/> 時擲出</exception>
    /// <exception cref="ArgumentException">Thrown when the documented condition occurs. / 當 <paramref name="sparql"/> 為空白時擲出</exception>
    /// <exception cref="InvalidOperationException">Thrown when the documented condition occurs. / 當查詢類型不受支援時擲出</exception>
    public static object ExecuteQuery(OdfRdfMetadata metadata, string sparql, Uri? baseUri = null)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(metadata, nameof(metadata));

        if (string.IsNullOrWhiteSpace(sparql))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfRdfGraphBridge_SparqlCannotBeEmpty"), nameof(sparql));
        }

        // SPARQL 的 SERVICE 子句會讓查詢引擎對查詢中指定的任意端點發出 HTTP 請求（實測會連線）；
        // 若 SPARQL 字串來自不受信任的使用者，就是 SSRF。查詢文件中繼資料不需要聯邦查詢。
        // 掃描兩份文字：原始查詢，以及 dotNetRDF 剖析後輸出的正規化查詢。自寫的詞法掃描器與剖析器的
        // 切詞規則可能不同（例如 "<iri>.SERVICE" 這類省略空白的寫法）；正規化輸出的關鍵字一律以空白分隔，
        // 因此以「剖析器實際看到的查詢」為準，不會被切詞差異繞過。剖析失敗的查詢交由後續執行時擲出原本的例外。
        bool hasServiceClause = ContainsServiceClause(sparql);
        if (!hasServiceClause)
        {
            try
            {
                string canonical = new VDS.RDF.Parsing.SparqlQueryParser().ParseFromString(sparql).ToString();
                hasServiceClause = ContainsServiceClause(canonical);
            }
            catch (VDS.RDF.Parsing.RdfParseException)
            {
                // 語法錯誤：不會被執行，後續 ExecuteQuery 會擲出原本的剖析例外。
            }
        }

        if (hasServiceClause)
        {
            throw new ArgumentException(
                OdfLocalizer.GetMessage("Err_OdfRdfGraphBridge_ServiceClauseNotAllowed"),
                nameof(sparql));
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
    /// 以詞法掃描判斷 SPARQL 是否含有 <c>SERVICE</c> 關鍵字。
    /// 略過 IRI（<c>&lt;…&gt;</c>）、字串字面值（單雙引號與三重引號）與註解（<c>#</c> 到行尾），
    /// 因此 <c>FILTER(CONTAINS(?x, "service"))</c> 與 <c>ex:service</c> 不會被誤判。
    /// </summary>
    internal static bool ContainsServiceClause(string sparql)
    {
        int length = sparql.Length;
        for (int i = 0; i < length; i++)
        {
            char c = sparql[i];
            if (c == '#')
            {
                while (i < length && sparql[i] != '\n' && sparql[i] != '\r')
                {
                    i++;
                }

                continue;
            }

            if (c == '"' || c == '\'')
            {
                bool triple = i + 2 < length && sparql[i + 1] == c && sparql[i + 2] == c;
                i += triple ? 3 : 1;
                while (i < length)
                {
                    if (sparql[i] == '\\')
                    {
                        i += 2;
                        continue;
                    }

                    if (sparql[i] == c && (!triple || (i + 2 < length && sparql[i + 1] == c && sparql[i + 2] == c)))
                    {
                        i += triple ? 2 : 0;
                        break;
                    }

                    i++;
                }

                continue;
            }

            if (c == '<')
            {
                // IRI 參照：其中不含空白與 '<'；否則視為比較運算子。
                int end = i + 1;
                while (end < length && sparql[end] is not ('>' or '<' or ' ' or '\t' or '\r' or '\n' or '"' or '{' or '}'))
                {
                    end++;
                }

                if (end < length && sparql[end] == '>')
                {
                    i = end;
                }

                continue;
            }

            if (!char.IsLetter(c))
            {
                continue;
            }

            int start = i;
            while (i + 1 < length && (char.IsLetterOrDigit(sparql[i + 1]) || sparql[i + 1] is '_' or '-' or ':' or '.'))
            {
                i++;
            }

            if (i - start + 1 == 7 &&
                string.Compare(sparql, start, "SERVICE", 0, 7, StringComparison.OrdinalIgnoreCase) == 0 &&
                (start == 0 || !(char.IsLetterOrDigit(sparql[start - 1]) || sparql[start - 1] is '_' or '-' or ':' or '.' or '?' or '$')))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Imports RDF graph triples into package metadata.
    /// 將 dotNetRDF 圖形中的 triples 匯入至 OdfKit metadata（追加模式）。
    /// </summary>
    /// <param name="metadata">The value to use. / 目標 RDF metadata</param>
    /// <param name="graph">The source or target object. / 來源圖形</param>
    /// <param name="baseUri">The path or URI. / 選用的封裝基底 URI</param>
    /// <returns>The result. / 新增的 triple 數量</returns>
    /// <exception cref="ArgumentNullException">Thrown when the documented condition occurs. / 當必要參數為 <see langword="null"/> 時擲出</exception>
    public static int ImportGraph(OdfRdfMetadata metadata, IGraph graph, Uri? baseUri = null)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(metadata, nameof(metadata));

        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(graph, nameof(graph));

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

    private static INode CreateUriNode(Graph graph, string value, Uri graphBase)
    {
        return graph.CreateUriNode(OdfRdfGraphUris.ResolveSubjectUri(value, graphBase));
    }
}
