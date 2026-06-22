namespace OdfKit.Core;

/// <summary>
/// 提供 ODF 1.2 封裝 RDF ontology（<c>pkg:</c>）的標準述詞 IRI。
/// </summary>
public static class OdfPkgRdfPredicates
{
    /// <summary>
    /// ODF 封裝 <c>pkg</c> 命名空間 URI
    /// </summary>
    public const string NamespaceUri = "http://docs.oasis-open.org/ns/office/1.2/meta/pkg#";

    /// <summary>
    /// 文件與封裝組件之間的 <c>pkg:hasPart</c> 關聯
    /// </summary>
    public const string HasPart = NamespaceUri + "hasPart";

    /// <summary>
    /// 封裝組件的 <c>pkg:mimeType</c> literal
    /// </summary>
    public const string MimeType = NamespaceUri + "mimeType";
}
