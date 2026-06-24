using System;
using OdfKit.Core;
using OdfKit.Text;

namespace OdfKit.DOM;

/// <summary>
/// 提供鏈式（Fluent）建構行內富文本樣式（產生 <c>text:span</c>）與文字節點的建構器。
/// </summary>
public sealed class InlineTextBuilder
{
    private readonly OdfNode _paragraphNode;
    private readonly TextDocument _doc;

    private bool _bold;
    private bool _italic;
    private bool _underline;
    private string? _color;
    private string? _fontSize;

    internal InlineTextBuilder(OdfNode paragraphNode, TextDocument doc)
    {
        _paragraphNode = paragraphNode ?? throw new ArgumentNullException(nameof(paragraphNode));
        _doc = doc ?? throw new ArgumentNullException(nameof(doc));
    }

    /// <summary>
    /// 設定後續文字是否為粗體。
    /// </summary>
    /// <param name="bold">是否為粗體，預設為 <see langword="true"/></param>
    /// <returns>當前建構器執行個體，支援鏈式呼叫</returns>
    public InlineTextBuilder Bold(bool bold = true)
    {
        _bold = bold;
        return this;
    }

    /// <summary>
    /// 設定後續文字是否為斜體。
    /// </summary>
    /// <param name="italic">是否為斜體，預設為 <see langword="true"/></param>
    /// <returns>當前建構器執行個體，支援鏈式呼叫</returns>
    public InlineTextBuilder Italic(bool italic = true)
    {
        _italic = italic;
        return this;
    }

    /// <summary>
    /// 設定後續文字是否具備下劃線。
    /// </summary>
    /// <param name="underline">是否具備下劃線，預設為 <see langword="true"/></param>
    /// <returns>當前建構器執行個體，支援鏈式呼叫</returns>
    public InlineTextBuilder Underline(bool underline = true)
    {
        _underline = underline;
        return this;
    }

    /// <summary>
    /// 設定後續文字的顏色（如十六進位色彩碼 "#FF0000"）。
    /// </summary>
    /// <param name="colorHex">顏色的十六進位碼字串</param>
    /// <returns>當前建構器執行個體，支援鏈式呼叫</returns>
    public InlineTextBuilder Color(string? colorHex)
    {
        _color = colorHex;
        return this;
    }

    /// <summary>
    /// 設定後續文字的字型大小（例如 "12pt" 或 "120%"）。
    /// </summary>
    /// <param name="size">字型大小描述字串</param>
    /// <returns>當前建構器執行個體，支援鏈式呼叫</returns>
    public InlineTextBuilder FontSize(string? size)
    {
        _fontSize = size;
        return this;
    }

    /// <summary>
    /// 清除目前累積的所有行內樣式狀態，重設為預設文字。
    /// </summary>
    /// <returns>當前建構器執行個體，支援鏈式呼叫</returns>
    public InlineTextBuilder Clear()
    {
        _bold = false;
        _italic = false;
        _underline = false;
        _color = null;
        _fontSize = null;
        return this;
    }

    /// <summary>
    /// 在段落中追加一段指定樣式的文字。
    /// </summary>
    /// <param name="value">要寫入的文字內容</param>
    /// <returns>當前建構器執行個體，支援鏈式呼叫</returns>
    public InlineTextBuilder Text(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return this;
        }

        bool hasStyles = _bold || _italic || _underline || _color != null || _fontSize != null;

        if (!hasStyles)
        {
            var textNode = new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = value };
            _paragraphNode.AppendChild(textNode);
        }
        else
        {
            var spanNode = new OdfNode(OdfNodeType.Element, "span", OdfNamespaces.Text, "text");
            _paragraphNode.AppendChild(spanNode);

            if (_bold)
            {
                _doc.StyleEngine.SetLocalStyleProperty(spanNode, "text", "text-properties", "font-weight", OdfNamespaces.Fo, "bold", "fo", deferSave: true);
            }
            if (_italic)
            {
                _doc.StyleEngine.SetLocalStyleProperty(spanNode, "text", "text-properties", "font-style", OdfNamespaces.Fo, "italic", "fo", deferSave: true);
            }
            if (_underline)
            {
                _doc.StyleEngine.SetLocalStyleProperty(spanNode, "text", "text-properties", "text-underline-style", OdfNamespaces.Style, "solid", "style", deferSave: true);
            }
            if (!string.IsNullOrEmpty(_color))
            {
                _doc.StyleEngine.SetLocalStyleProperty(spanNode, "text", "text-properties", "color", OdfNamespaces.Fo, _color, "fo", deferSave: true);
            }
            if (!string.IsNullOrEmpty(_fontSize))
            {
                _doc.StyleEngine.SetLocalStyleProperty(spanNode, "text", "text-properties", "font-size", OdfNamespaces.Fo, _fontSize, "fo", deferSave: true);
            }

            _doc.StyleEngine.DeduplicateAndSaveStyles();

            var textNode = new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = value };
            spanNode.AppendChild(textNode);
        }

        return this;
    }
}
