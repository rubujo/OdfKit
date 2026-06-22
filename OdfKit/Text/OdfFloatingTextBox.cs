using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// 表示 ODT 文件中的浮動文字框。
/// </summary>
public sealed class OdfFloatingTextBox
{
    private readonly OdfNode _textBoxNode;
    private readonly TextDocument _document;

    internal OdfFloatingTextBox(OdfNode textBoxNode, TextDocument document)
    {
        _textBoxNode = textBoxNode ?? throw new ArgumentNullException(nameof(textBoxNode));
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>
    /// 新增文字框段落。
    /// </summary>
    /// <param name="text">段落文字</param>
    /// <returns>新建立的段落</returns>
    public OdfParagraph AddParagraph(string text = "")
    {
        var paragraphNode = OdfNodeFactory.CreateElement("p", OdfNamespaces.Text, "text");
        paragraphNode.TextContent = text;
        _textBoxNode.AppendChild(paragraphNode);
        return new OdfParagraph(paragraphNode, _document);
    }
}
