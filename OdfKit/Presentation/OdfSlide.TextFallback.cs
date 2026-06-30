using System;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Presentation;

public partial class OdfSlide
{
    /// <summary>
    /// Adds a text box with the specified font fallback options.
    /// 使用指定的字型遞補選項新增文字方塊。
    /// </summary>
    /// <param name="x">The X-axis coordinate position. / X 軸座標位置。</param>
    /// <param name="y">The Y-axis coordinate position. / Y 軸座標位置。</param>
    /// <param name="w">The width. / 寬度。</param>
    /// <param name="h">The height. / 高度。</param>
    /// <param name="text">The text content. / 文字內容。</param>
    /// <param name="options">The font fallback options. / 字型遞補選項。</param>
    /// <returns>The added text box shape instance. / 新增的文字方塊圖形執行個體。</returns>
    public OdfTextBox AddTextBox(OdfLength x, OdfLength y, OdfLength w, OdfLength h, string text, OdfTextFontFallbackOptions options)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        var frame = CreateDrawingFrame(x, y, w, h);
        var textBoxNode = new OdfNode(OdfNodeType.Element, "text-box", OdfNamespaces.Draw, "draw");
        frame.AppendChild(textBoxNode);

        var pNode = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
        OdfCjkTextSpanBuilder.AppendSegmentedText(Document, pNode, text, options);
        textBoxNode.AppendChild(pNode);

        AddDrawingObjectNode(frame);
        return new OdfTextBox(frame, this);
    }
}
