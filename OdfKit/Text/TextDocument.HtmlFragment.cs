namespace OdfKit.Text;

public partial class TextDocument
{
    #region HTML Fragment Parsing

    /// <summary>
    /// 在指定的段落中解析並新增 HTML 片段。
    /// </summary>
    /// <param name="paragraph">The value to use. / 要加入 HTML 內容的段落</param>
    /// <param name="html">The value to use. / 要解析的 HTML 字串片段</param>
    internal void AddHtmlFragment(OdfParagraph paragraph, string html) =>
        TextDocumentHtmlFragmentEngine.AddHtmlFragment(this, paragraph, html);

    #endregion
}
