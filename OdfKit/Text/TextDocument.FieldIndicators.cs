using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

public partial class TextDocument
{
    #region Dynamic Page / Field Indicators


    /// <summary>
    /// 在指定的段落中新增頁碼欄位。
    /// </summary>
    /// <param name="paragraph">目標段落</param>
    internal void AddPageNumberField(OdfParagraph paragraph)
    {
        var fNode = new OdfNode(OdfNodeType.Element, "page-number", OdfNamespaces.Text, "text");
        fNode.SetAttribute("select-page", OdfNamespaces.Text, "current", "text");
        paragraph.Node.AppendChild(fNode);
    }

    /// <summary>
    /// 在指定的段落中新增總頁數欄位。
    /// </summary>
    /// <param name="paragraph">目標段落</param>
    internal void AddPageCountField(OdfParagraph paragraph)
    {
        var fNode = new OdfNode(OdfNodeType.Element, "page-count", OdfNamespaces.Text, "text");
        fNode.SetAttribute("num-format", OdfNamespaces.Style, "1", "style");
        paragraph.Node.AppendChild(fNode);
    }


    #endregion
}
