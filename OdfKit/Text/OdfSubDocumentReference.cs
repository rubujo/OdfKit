namespace OdfKit.Text;

/// <summary>
/// 描述主控文字文件中指向外部子文件的區段參照。
/// </summary>
/// <param name="SectionName">區段名稱（<c>text:section/@text:name</c>）。</param>
/// <param name="Href">子文件 URI（<c>text:section-source/@xlink:href</c>）。</param>
public sealed record OdfSubDocumentReference(string SectionName, string Href);
