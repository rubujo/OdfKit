namespace OdfKit.Text;

/// <summary>
/// 描述主控文字文件中指向外部子文件的區段參照。
/// </summary>
/// <param name="SectionName">區段名稱（<c>text:section/@text:name</c>）</param>
/// <param name="Href">子文件 URI（<c>text:section-source/@xlink:href</c>）</param>
/// <param name="Actuate">
/// 子文件載入時機（<c>text:section-source/@xlink:actuate</c>）<c>onLoad</c> 表示開啟主控文件時
/// 立即載入子文件內容；<c>onRequest</c> 表示延遲載入，由使用者端應用程式決定何時載入
/// （例如使用者展開該區段時）。
/// </param>
public sealed record OdfSubDocumentReference(string SectionName, string Href, string Actuate = "onLoad");
