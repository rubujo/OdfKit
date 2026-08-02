using System.Security;
using System.Text;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Spreadsheet;
using Xunit;

namespace OdfKit.Tests;

public partial class OptimizedRefactoringTests
{
    public static TheoryData<string, string, string> FastPathDocumentKinds => new()
    {
        {
            "application/vnd.oasis.opendocument.spreadsheet",
            "security.ods",
            "<office:spreadsheet><table:table table:name=\"Sheet1\"><table:table-row><table:table-cell><text:p>{0}</text:p></table:table-cell></table:table-row></table:table></office:spreadsheet>"
        },
        {
            "application/vnd.oasis.opendocument.text",
            "security.odt",
            "<office:text><text:p>{0}</text:p></office:text>"
        },
        {
            "application/vnd.oasis.opendocument.presentation",
            "security.odp",
            "<office:presentation><draw:page draw:name=\"Slide1\"><text:p>{0}</text:p></draw:page></office:presentation>"
        },
        {
            "application/vnd.oasis.opendocument.graphics",
            "security.odg",
            "<office:drawing><draw:page draw:name=\"Page1\"><text:p>{0}</text:p></draw:page></office:drawing>"
        }
    };

    /// <summary>
    /// 驗證四種高階 ODF 格式走 UTF-8 快速路徑時仍會拒絕 DTD，而非將宣告略過。
    /// </summary>
    [Theory]
    [MemberData(nameof(FastPathDocumentKinds))]
    public void DocumentFastPathRejectsDtdAcrossFormats(string mimeType, string fileName, string bodyTemplate)
    {
        string content = BuildDocumentContent(
            "<!DOCTYPE office:document-content [<!ENTITY xxe SYSTEM \"file:///etc/passwd\">]>",
            string.Format(System.Globalization.CultureInfo.InvariantCulture, bodyTemplate, "&xxe;"));
        using MemoryStream package = CreateDocumentPackage(mimeType, content);

        SecurityException exception = Assert.Throws<SecurityException>(() =>
            OdfDocumentFactory.LoadDocument(package, new OdfLoadOptions(), fileName));

        Assert.Equal(OdfLocalizer.GetMessage("Err_OdfXmlReader_DtdProhibited"), exception.Message);
    }

    /// <summary>
    /// 驗證四種高階 ODF 格式的 lazy subtree 內容都會計入 XML 字元上限。
    /// </summary>
    [Theory]
    [MemberData(nameof(FastPathDocumentKinds))]
    public void DocumentFastPathCountsLazyPayloadAgainstXmlCharacterLimit(string mimeType, string fileName, string bodyTemplate)
    {
        string content = BuildDocumentContent(
            declaration: string.Empty,
            string.Format(System.Globalization.CultureInfo.InvariantCulture, bodyTemplate, new string('x', 12_000)));
        using MemoryStream package = CreateDocumentPackage(mimeType, content);
        OdfLoadOptions options = new() { MaxXmlCharactersInDocument = 2_048 };

        Assert.Throws<SecurityException>(() => OdfDocumentFactory.LoadDocument(package, options, fileName));
    }

    /// <summary>
    /// 驗證直接使用 Stream parser 時，大型 lazy table 內容同樣計入 XML 字元上限。
    /// </summary>
    [Fact]
    public void StreamParserCountsLazyPayloadAgainstXmlCharacterLimit()
    {
        string body = $"<office:spreadsheet><table:table table:name=\"Sheet1\"><table:table-row><table:table-cell><text:p>{new string('x', 12_000)}</text:p></table:table-cell></table:table-row></table:table></office:spreadsheet>";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(BuildDocumentContent(string.Empty, body)));

        Assert.Throws<SecurityException>(() => OdfKit.DOM.OdfXmlReader.Parse(
            stream,
            new OdfLoadOptions { MaxXmlCharactersInDocument = 2_048 }));
    }

    /// <summary>
    /// 驗證 lazy subtree 首次具現化會沿用原始 StrictXmlParsing 選項。
    /// </summary>
    [Fact]
    public void LazySheetMaterializationPreservesStrictXmlParsing()
    {
        string repeated = new('x', 9_000);
        string body = $"<office:spreadsheet><table:table table:name=\"Sheet1\"><table:table-row><table:table-cell><text:p>{repeated}&undefined;</text:p></table:table-cell></table:table-row></table:table></office:spreadsheet>";
        using MemoryStream package = CreateDocumentPackage(
            "application/vnd.oasis.opendocument.spreadsheet",
            BuildDocumentContent(string.Empty, body));
        using OdfDocument loaded = OdfDocumentFactory.LoadDocument(
            package,
            new OdfLoadOptions
            {
                StrictXmlParsing = true,
                MaxXmlCharactersInDocument = 32_000
            },
            "strict.ods");
        SpreadsheetDocument document = Assert.IsType<SpreadsheetDocument>(loaded);

        Assert.Throws<System.Xml.XmlException>(() => document.Worksheets[0].GetCell(0, 0));
    }

    /// <summary>
    /// 驗證多執行緒同時取得同一工作表時只建立一個 facade 執行個體。
    /// </summary>
    [Fact]
    public void ConcurrentWorksheetLookupReturnsSingleFacadeInstance()
    {
        using MemoryStream package = CreateLargeLazyOdsPackage();
        using SpreadsheetDocument document = SpreadsheetDocument.Load(package, "large.ods");
        OdfTableSheet?[] results = new OdfTableSheet[32];

        Parallel.For(0, results.Length, index => results[index] = document.Worksheets[0]);

        Assert.All(results, sheet => Assert.Same(results[0], sheet));
    }

    /// <summary>
    /// 驗證 ODT、ODP 與 ODG 高階 wrapper 建構後仍保留大型段落的 lazy 狀態。
    /// </summary>
    [Theory]
    [InlineData("application/vnd.oasis.opendocument.text", "lazy.odt", "text", false)]
    [InlineData("application/vnd.oasis.opendocument.presentation", "lazy.odp", "presentation", true)]
    [InlineData("application/vnd.oasis.opendocument.graphics", "lazy.odg", "drawing", true)]
    public void OtherDocumentFormatsKeepLargeParagraphLazy(
        string mimeType,
        string fileName,
        string documentRootName,
        bool hasPage)
    {
        string paragraph = $"<text:p>{new string('x', 12_000)}</text:p>";
        string formatBody = hasPage
            ? $"<office:{documentRootName}><draw:page draw:name=\"Page1\">{paragraph}</draw:page></office:{documentRootName}>"
            : $"<office:{documentRootName}>{paragraph}</office:{documentRootName}>";
        using MemoryStream package = CreateDocumentPackage(
            mimeType,
            BuildDocumentContent(string.Empty, formatBody));
        using OdfDocument document = OdfDocumentFactory.LoadDocument(package, new OdfLoadOptions(), fileName);

        OdfKit.DOM.OdfNode body = document.ContentDom.Children.Single(node =>
            node.LocalName == "body" && node.NamespaceUri == OdfNamespaces.Office);
        OdfKit.DOM.OdfNode formatRoot = body.Children.Single(node =>
            node.LocalName == documentRootName && node.NamespaceUri == OdfNamespaces.Office);
        OdfKit.DOM.OdfNode paragraphNode = hasPage
            ? formatRoot.Children.Single(node => node.LocalName == "page").Children.Single(node => node.LocalName == "p")
            : formatRoot.Children.Single(node => node.LocalName == "p");

        Assert.True(paragraphNode._isLazy);
        Assert.Equal(12_000, paragraphNode.TextContent.Length);
        Assert.False(paragraphNode._isLazy);
    }

    private static string BuildDocumentContent(string declaration, string body)
    {
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            {declaration}
            <office:document-content
                xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0"
                xmlns:table="urn:oasis:names:tc:opendocument:xmlns:table:1.0"
                xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0"
                xmlns:draw="urn:oasis:names:tc:opendocument:xmlns:drawing:1.0"
                office:version="1.4">
              <office:body>{body}</office:body>
            </office:document-content>
            """;
    }

    private static MemoryStream CreateDocumentPackage(string mimeType, string content)
    {
        string manifest = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <manifest:manifest xmlns:manifest="urn:oasis:names:tc:opendocument:xmlns:manifest:1.0" manifest:version="1.4">
              <manifest:file-entry manifest:full-path="/" manifest:media-type="{mimeType}" manifest:version="1.4" />
              <manifest:file-entry manifest:full-path="content.xml" manifest:media-type="text/xml" />
            </manifest:manifest>
            """;
        return CreateZipPackage(
            ("mimetype", Encoding.ASCII.GetBytes(mimeType)),
            ("content.xml", Encoding.UTF8.GetBytes(content)),
            ("META-INF/manifest.xml", Encoding.UTF8.GetBytes(manifest)));
    }
}
