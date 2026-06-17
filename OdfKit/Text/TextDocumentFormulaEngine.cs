using System;
using System.Xml.Linq;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// 文字文件 MathML 公式引擎（內部協作者）。
/// </summary>
internal static class TextDocumentFormulaEngine
{
    /// <summary>
    /// 在指定段落中新增內嵌 MathML 公式物件。
    /// </summary>
    internal static void AddFormula(TextDocument.TextDocumentCoreCollaborators ctx, OdfParagraph paragraph, string mathMlXmlString)
    {
        if (paragraph is null)
            throw new ArgumentNullException(nameof(paragraph));
        if (string.IsNullOrWhiteSpace(mathMlXmlString))
            throw new ArgumentException("MathML XML content cannot be empty.", nameof(mathMlXmlString));

        try
        {
            XElement.Parse(mathMlXmlString);
        }
        catch (Exception ex)
        {
            throw new ArgumentException("Invalid MathML XML: " + ex.Message, nameof(mathMlXmlString), ex);
        }

        string folder = $"Formula_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
        string mathDocXml = $"<office:document-meta xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:math=\"http://www.w3.org/1998/Math/MathML\"><office:body><office:formula>{mathMlXmlString}</office:formula></office:body></office:document-meta>";

        ctx.Package.WriteEntry($"{folder}/content.xml", System.Text.Encoding.UTF8.GetBytes(mathDocXml), "text/xml");
        ctx.Package.WriteEntry($"{folder}/mimetype", System.Text.Encoding.UTF8.GetBytes("application/vnd.oasis.opendocument.formula"), "application/vnd.oasis.opendocument.formula");

        ctx.Package.SaveManifestToEntries();

        var frame = new OdfNode(OdfNodeType.Element, "frame", OdfNamespaces.Draw, "draw");
        frame.SetAttribute("width", OdfNamespaces.Svg, "2cm", "svg");
        frame.SetAttribute("height", OdfNamespaces.Svg, "1cm", "svg");
        frame.SetAttribute("anchor-type", OdfNamespaces.Text, "as-char", "text");

        var obj = new OdfNode(OdfNodeType.Element, "object", OdfNamespaces.Draw, "draw");
        obj.SetAttribute("href", OdfNamespaces.XLink, folder, "xlink");
        frame.AppendChild(obj);

        paragraph.Node.AppendChild(frame);
    }
}
