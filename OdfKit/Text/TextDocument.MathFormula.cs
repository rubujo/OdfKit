using System;
using System.Xml.Linq;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

public partial class TextDocument
{
    #region Mathematical Formulas (MathML)


    /// <summary>
    /// 在指定的段落中新增數學公式。
    /// </summary>
    /// <param name="paragraph">要插入公式的段落</param>
    /// <param name="mathMlXmlString">MathML 結構的 XML 字串內容</param>
    internal void AddFormula(OdfParagraph paragraph, string mathMlXmlString)
    {
        if (paragraph is null)
            throw new ArgumentNullException(nameof(paragraph));
        if (string.IsNullOrWhiteSpace(mathMlXmlString))
            throw new ArgumentException("MathML XML content cannot be empty.", nameof(mathMlXmlString));

        // 驗證 mathMlXmlString 是否為格式正確的 XML
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

        Package.WriteEntry($"{folder}/content.xml", System.Text.Encoding.UTF8.GetBytes(mathDocXml), "text/xml");
        Package.WriteEntry($"{folder}/mimetype", System.Text.Encoding.UTF8.GetBytes("application/vnd.oasis.opendocument.formula"), "application/vnd.oasis.opendocument.formula");

        Package.SaveManifestToEntries();

        var frame = new OdfNode(OdfNodeType.Element, "frame", OdfNamespaces.Draw, "draw");
        frame.SetAttribute("width", OdfNamespaces.Svg, "2cm", "svg");
        frame.SetAttribute("height", OdfNamespaces.Svg, "1cm", "svg");
        frame.SetAttribute("anchor-type", OdfNamespaces.Text, "as-char", "text");

        var obj = new OdfNode(OdfNodeType.Element, "object", OdfNamespaces.Draw, "draw");
        obj.SetAttribute("href", OdfNamespaces.XLink, folder, "xlink");
        frame.AppendChild(obj);

        paragraph.Node.AppendChild(frame);
    }


    #endregion
}
