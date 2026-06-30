using System;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Presentation;
using OdfKit.Styles;

namespace OdfKit.Drawing;

public partial class OdfDrawPage
{
    /// <summary>
    /// Adds a text box with the specified font fallback options.
    /// 使用指定的字型遞補選項新增文字方塊。
    /// </summary>
    /// <param name="x">The X-axis position. / X 軸座標位置。</param>
    /// <param name="y">The Y-axis position. / Y 軸座標位置。</param>
    /// <param name="w">The width. / 寬度。</param>
    /// <param name="h">The height. / 高度。</param>
    /// <param name="text">The text content. / 文字內容。</param>
    /// <param name="options">The font fallback options. / 字型遞補選項。</param>
    /// <returns>The newly added text box instance. / 新增的文字方塊執行個體。</returns>
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
        var textBoxNode = OdfNodeFactory.CreateElement("text-box", OdfNamespaces.Draw, "draw");
        frame.AppendChild(textBoxNode);

        var pNode = OdfNodeFactory.CreateElement("p", OdfNamespaces.Text, "text");
        OdfCjkTextSpanBuilder.AppendSegmentedText(Document, pNode, text, options);
        textBoxNode.AppendChild(pNode);

        Node.AppendChild(frame);
        return new OdfTextBox(frame, Document);
    }
}
