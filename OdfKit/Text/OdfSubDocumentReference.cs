namespace OdfKit.Text;

/// <summary>
/// Describes a section reference in a master text document pointing to an external sub-document.
/// 描述主控文字文件中指向外部子文件的區段參照。
/// </summary>
/// <param name="SectionName">The section name (<c>text:section/@text:name</c>). / 區段名稱（<c>text:section/@text:name</c>）。</param>
/// <param name="Href">The sub-document URI (<c>text:section-source/@xlink:href</c>). / 子文件 URI（<c>text:section-source/@xlink:href</c>）。</param>
/// <param name="Actuate">
/// The sub-document load timing (<c>text:section-source/@xlink:actuate</c>). <c>onLoad</c> means the sub-document content
/// loads immediately when the master document is opened; <c>onRequest</c> means lazy loading, with the consuming
/// application deciding when to load it (e.g. when the user expands the section).
/// 子文件載入時機（<c>text:section-source/@xlink:actuate</c>）<c>onLoad</c> 表示開啟主控文件時
/// 立即載入子文件內容；<c>onRequest</c> 表示延遲載入，由使用者端應用程式決定何時載入
/// （例如使用者展開該區段時）。
/// </param>
public sealed record OdfSubDocumentReference(string SectionName, string Href, string Actuate = "onLoad");
