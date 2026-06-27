using System.IO;
using System.Text;
using OdfKit.Core;
using OdfKit.Drawing;
using OdfKit.Presentation;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定 LibreOffice <c>loext</c> 擴充屬性與 ODF 標準屬性之互通行為。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Interop)]
public class LoExtInteropTests
{
    /// <summary>
    /// 驗證載入含 <c>loext:decorative</c> 的 ODT 圖片時會正規化為標準屬性並可讀回。
    /// </summary>
    [Fact]
    public void LoadMapsLoExtDecorativeImageToDrawDecorative()
    {
        using var stream = new MemoryStream();
        WriteLoExtDecorativeOdt(stream);
        stream.Position = 0;

        using TextDocument document = TextDocument.Load(stream);
        OdfImage image = document.Body.Images.Single();

        Assert.True(image.IsDecorative);
        Assert.Null(image.FrameNode.GetAttribute("decorative", OdfNamespaces.LoExt));
        Assert.Equal("true", image.FrameNode.GetAttribute("decorative", OdfNamespaces.Draw));

        string contentXml = ReadContentXml(document);
        Assert.Contains("draw:decorative=\"true\"", contentXml);
        Assert.DoesNotContain("loext:decorative", contentXml);
    }

    /// <summary>
    /// 驗證載入含 <c>loext:decorative</c> 的 ODG 圖形時會正規化並可讀回。
    /// </summary>
    [Fact]
    public void LoadMapsLoExtDecorativeDrawingShapeToDrawDecorative()
    {
        using var stream = new MemoryStream();
        WriteLoExtDecorativeOdg(stream);
        stream.Position = 0;

        using DrawingDocument document = DrawingDocument.Load(stream);
        OdfShape shape = document.Pages[0].Shapes.Single();

        Assert.True(shape.IsDecorative);
        Assert.Null(shape.Node.GetAttribute("decorative", OdfNamespaces.LoExt));
        Assert.Equal("true", shape.Node.GetAttribute("decorative", OdfNamespaces.Draw));
    }

    /// <summary>
    /// 驗證載入含 <c>loext:decorative</c> 的 ODP 圖形時會正規化並可讀回。
    /// </summary>
    [Fact]
    public void LoadMapsLoExtDecorativeShapeToDrawDecorative()
    {
        using var stream = new MemoryStream();
        WriteLoExtDecorativeOdp(stream);
        stream.Position = 0;

        using PresentationDocument document = PresentationDocument.Load(stream);
        OdfShape shape = document.Slides[0].Shapes.Single();

        Assert.True(shape.IsDecorative);
        Assert.Null(shape.Node.GetAttribute("decorative", OdfNamespaces.LoExt));
        Assert.Equal("true", shape.Node.GetAttribute("decorative", OdfNamespaces.Draw));
    }

    private static void WriteLoExtDecorativeOdt(Stream stream)
    {
        const string contentXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
            "xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" " +
            "xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" " +
            "xmlns:xlink=\"http://www.w3.org/1999/xlink\" " +
            "xmlns:loext=\"urn:org:documentfoundation:names:experimental:office:xmlns:loext:1.0\" " +
            "office:version=\"1.4\">" +
            "<office:body><office:text>" +
            "<text:p>" +
            "<draw:frame text:anchor-type=\"paragraph\" loext:decorative=\"true\">" +
            "<draw:image xlink:href=\"Pictures/image.png\" xlink:type=\"simple\" xlink:show=\"embed\" xlink:actuate=\"onLoad\"/>" +
            "</draw:frame></text:p>" +
            "</office:text></office:body></office:document-content>";

        WriteMinimalPackage(stream, "application/vnd.oasis.opendocument.text", contentXml);
    }

    private static void WriteLoExtDecorativeOdg(Stream stream)
    {
        const string contentXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
            "xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" " +
            "xmlns:loext=\"urn:org:documentfoundation:names:experimental:office:xmlns:loext:1.0\" " +
            "office:version=\"1.4\">" +
            "<office:body><office:drawing>" +
            "<draw:page draw:name=\"Page1\">" +
            "<draw:rect loext:decorative=\"true\" svg:width=\"2cm\" svg:height=\"2cm\" " +
            "xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\"/>" +
            "</draw:page></office:drawing></office:body></office:document-content>";

        WriteMinimalPackage(stream, "application/vnd.oasis.opendocument.graphics", contentXml);
    }

    private static void WriteLoExtDecorativeOdp(Stream stream)
    {
        const string contentXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
            "xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" " +
            "xmlns:presentation=\"urn:oasis:names:tc:opendocument:xmlns:presentation:1.0\" " +
            "xmlns:loext=\"urn:org:documentfoundation:names:experimental:office:xmlns:loext:1.0\" " +
            "office:version=\"1.4\">" +
            "<office:body><office:presentation>" +
            "<draw:page draw:name=\"Slide1\">" +
            "<draw:rect loext:decorative=\"true\" svg:width=\"2cm\" svg:height=\"2cm\" " +
            "xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\"/>" +
            "</draw:page></office:presentation></office:body></office:document-content>";

        WriteMinimalPackage(stream, "application/vnd.oasis.opendocument.presentation", contentXml);
    }

    private static void WriteMinimalPackage(Stream stream, string mimeType, string contentXml)
    {
        using OdfPackage package = OdfPackage.Create(stream, leaveOpen: true);
        package.SetMimeType(mimeType);
        package.WriteEntry("content.xml", Encoding.UTF8.GetBytes(contentXml), "text/xml");
        package.WriteEntry(
            "styles.xml",
            Encoding.UTF8.GetBytes(
                "<office:document-styles xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" office:version=\"1.4\"><office:styles/></office:document-styles>"),
            "text/xml");
        package.Save();
    }

    private static string ReadContentXml(TextDocument document)
    {
        using var saveStream = new MemoryStream();
        document.SaveToStream(saveStream);
        saveStream.Position = 0;

        using OdfPackage package = OdfPackage.Open(saveStream, leaveOpen: true);
        using Stream contentStream = package.GetEntryStream("content.xml");
        using var reader = new StreamReader(contentStream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
