using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using OdfKit.Drawing;
using OdfKit.Export;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 真實文件（LibreOffice）把填色、線條、線寬、虛線放在圖形樣式並由 parent-style-name 繼承，頁面大小在版面主頁；
/// SVG 匯出曾只讀圖形元素上的內嵌屬性，線寬全為 1、「無線條」被畫成預設藍框、虛線與圓角遺失、頁面變成 A4 橫向。
/// 預期值以 LibreOffice 26.2 開啟同一份 FODG 的轉出結果為準（逐像素比對見 docs/libreoffice-interop-matrix.md）。
/// </summary>
public sealed class SvgExportStyleTests
{
    private const string Fodg = """
        <?xml version="1.0" encoding="UTF-8"?>
        <office:document xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0" xmlns:style="urn:oasis:names:tc:opendocument:xmlns:style:1.0" xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0" xmlns:draw="urn:oasis:names:tc:opendocument:xmlns:drawing:1.0" xmlns:fo="urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0" xmlns:svg="urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0" xmlns:xlink="http://www.w3.org/1999/xlink" office:version="1.3" office:mimetype="application/vnd.oasis.opendocument.graphics">
        <office:styles>
        <draw:stroke-dash draw:name="Dash" draw:style="rect" draw:dots1="1" draw:dots1-length="200%" draw:distance="100%"/>
        <style:style style:name="standard" style:family="graphic"><style:graphic-properties draw:stroke="solid" svg:stroke-width="0cm" svg:stroke-color="#3465a4" draw:fill="solid" draw:fill-color="#729fcf" fo:padding-top="0.125cm" fo:padding-left="0.25cm"/></style:style>
        </office:styles>
        <office:automatic-styles>
        <style:page-layout style:name="PM1"><style:page-layout-properties fo:page-width="20cm" fo:page-height="14cm"/></style:page-layout>
        <style:style style:name="gr1" style:family="graphic" style:parent-style-name="standard"><style:graphic-properties draw:stroke="solid" svg:stroke-width="0.1cm" svg:stroke-color="#000000" draw:fill="solid" draw:fill-color="#ff0000"/></style:style>
        <style:style style:name="gr2" style:family="graphic" style:parent-style-name="standard"><style:graphic-properties draw:stroke="none" draw:fill="solid" draw:fill-color="#00aa00"/></style:style>
        <style:style style:name="gr4" style:family="graphic" style:parent-style-name="standard"><style:graphic-properties draw:stroke="dash" draw:stroke-dash="Dash" svg:stroke-width="0.05cm" svg:stroke-color="#444444" draw:fill="none"/></style:style>
        <style:style style:name="gr5" style:family="graphic" style:parent-style-name="standard"><style:graphic-properties draw:stroke="none" draw:fill="none"/></style:style>
        </office:automatic-styles>
        <office:master-styles><style:master-page style:name="Default" style:page-layout-name="PM1"/></office:master-styles>
        <office:body><office:drawing><draw:page draw:name="page1" draw:master-page-name="Default">
        <draw:rect draw:style-name="gr1" svg:x="1cm" svg:y="1cm" svg:width="5cm" svg:height="3cm" draw:corner-radius="0.5cm"/>
        <draw:ellipse draw:style-name="gr2" svg:x="8cm" svg:y="1cm" svg:width="4cm" svg:height="3cm"/>
        <draw:rect draw:style-name="gr4" svg:x="1cm" svg:y="6cm" svg:width="5cm" svg:height="2cm"/>
        <draw:frame draw:style-name="gr5" svg:x="6cm" svg:y="9.5cm" svg:width="12cm" svg:height="2cm"><draw:text-box><text:p>Hello</text:p></draw:text-box></draw:frame>
        </draw:page></office:drawing></office:body></office:document>
        """;

    private static XDocument Export()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(Fodg));
        using FlatGraphicsDocument flat = FlatGraphicsDocument.Load(stream);
        using DrawingDocument document = DrawingDocument.CreateFromFlatDocument(flat);
        return XDocument.Parse(OdfSvgExporter.Export(document));
    }

    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    /// <summary>
    /// 樣式決定的線寬（0.1 cm ＝ 2.8346 pt）、線條模式、顏色與圓角。
    /// </summary>
    [Fact]
    public void ShapeStrokeAndFillComeFromTheStyle()
    {
        XDocument svg = Export();
        XElement rect = svg.Descendants(Svg + "rect").First();
        Assert.Equal("#ff0000", (string?)rect.Attribute("fill"));
        Assert.Equal("#000000", (string?)rect.Attribute("stroke"));
        Assert.Equal("2.8346", (string?)rect.Attribute("stroke-width"));
        Assert.Equal("14.1732", (string?)rect.Attribute("rx"));

        XElement ellipse = svg.Descendants(Svg + "ellipse").Single();
        Assert.Equal("#00aa00", (string?)ellipse.Attribute("fill"));
        Assert.Equal("none", (string?)ellipse.Attribute("stroke"));
    }

    /// <summary>
    /// 樣式指定虛線時輸出 stroke-dasharray（長度為線寬的百分比）。
    /// </summary>
    [Fact]
    public void DashedStrokeBecomesDashArray()
    {
        XDocument svg = Export();
        XElement dashed = svg.Descendants(Svg + "rect").Single(rect => (string?)rect.Attribute("stroke") == "#444444");
        Assert.Equal("none", (string?)dashed.Attribute("fill"));
        Assert.Equal("2.8346 1.4173", (string?)dashed.Attribute("stroke-dasharray"));
    }

    /// <summary>
    /// 頁面大小取自版面主頁（20 × 14 cm），文字框沒有填色與線條。
    /// </summary>
    [Fact]
    public void PageSizeAndInvisibleTextFrameFollowTheDocument()
    {
        XDocument svg = Export();
        Assert.Equal("566.9291pt", (string?)svg.Root!.Attribute("width"));
        Assert.Equal("396.8504pt", (string?)svg.Root.Attribute("height"));

        XElement frameRect = svg.Descendants(Svg + "g").First().Element(Svg + "rect")!;
        Assert.Equal("none", (string?)frameRect.Attribute("fill"));
        Assert.Equal("none", (string?)frameRect.Attribute("stroke"));
    }
}
