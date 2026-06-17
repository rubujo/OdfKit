using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證未知 ODF XML 與 foreign namespace 內容在高階保存後仍可 round-trip。
/// </summary>
public class OdfUnknownXmlRoundTripTests
{
    private const string CustomNamespace = "urn:example:custom";
    private const string OfficeExtensionNamespace = "urn:example:office-extension";

    /// <summary>
    /// 驗證 high-level save 保留 unknown XML、foreign namespace、註解與處理指令。
    /// </summary>
    [Fact]
    public void HighLevelSavePreservesUnknownXmlForeignContentAndProcessingInstructions()
    {
        using MemoryStream source = CreatePackageWithUnknownXml();
        using var document = new TextDocument(OdfPackage.Open(source, leaveOpen: true));

        document.AddParagraph("觸發高階保存");

        using var saved = new MemoryStream();
        document.SaveToStream(saved);
        saved.Position = 0;

        using OdfPackage package = OdfPackage.Open(saved, leaveOpen: true);
        string contentXml = ReadEntryText(package, "content.xml");
        XDocument content = XDocument.Parse(contentXml, LoadOptions.PreserveWhitespace);
        XNamespace text = OdfNamespaces.Text;
        XNamespace custom = CustomNamespace;
        XNamespace officeExtension = OfficeExtensionNamespace;

        XElement unknownOdf = Assert.Single(content.Descendants(text + "unknown-child"));
        Assert.Equal("u1", unknownOdf.Attribute(text + "name")?.Value);

        XElement widget = Assert.Single(content.Descendants(custom + "widget"));
        Assert.Equal("yes", widget.Attribute(custom + "flag")?.Value);
        Assert.Equal("preserve", widget.Attribute(officeExtension + "flag")?.Value);
        Assert.Contains("custom:widget", contentXml);
        Assert.Contains("custom:flag", contentXml);
        Assert.Contains("officeext:flag", contentXml);

        Assert.Contains(content.DescendantNodes().OfType<XComment>(), comment => comment.Value == "keep-comment");
        Assert.Contains(
            content.DescendantNodes().OfType<XProcessingInstruction>(),
            instruction => instruction.Target == "odfkit" && instruction.Data == "status=\"keep\"");
    }

    private static MemoryStream CreatePackageWithUnknownXml()
    {
        string contentXml =
            "<office:document-content " +
            "xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
            "xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" " +
            "xmlns:custom=\"" + CustomNamespace + "\" " +
            "xmlns:officeext=\"" + OfficeExtensionNamespace + "\" " +
            "office:version=\"" + OdfVersionInfo.DefaultVersionString + "\">" +
            "<office:body><office:text>" +
            "<text:p>before</text:p>" +
            "<text:unknown-child text:name=\"u1\">unknown odf child</text:unknown-child>" +
            "<custom:widget custom:flag=\"yes\" officeext:flag=\"preserve\">" +
            "<custom:child>foreign child</custom:child>" +
            "</custom:widget>" +
            "<!--keep-comment-->" +
            "<?odfkit status=\"keep\"?>" +
            "</office:text></office:body>" +
            "</office:document-content>";

        var stream = new MemoryStream();
        using (OdfPackage package = OdfDocumentFactory.CreatePackage(stream, OdfKit.Compliance.OdfDocumentKind.Text, leaveOpen: true))
        {
            package.WriteEntry("content.xml", Encoding.UTF8.GetBytes(contentXml), "text/xml");
            package.Save();
        }

        stream.Position = 0;
        return stream;
    }

    private static string ReadEntryText(OdfPackage package, string entryName)
    {
        using Stream stream = package.GetEntryStream(entryName);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
