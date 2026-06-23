using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 測試 OdfPackage 在 Flat XML 與 ZIP 封裝包之間的互轉與 Round-trip 保真度。
/// </summary>
public class PackageRoundTripTests
{
    private static readonly XNamespace OfficeNs = OdfNamespaces.Office;
    private static readonly XNamespace TextNs = OdfNamespaces.Text;
    private static readonly XNamespace DrawNs = OdfNamespaces.Draw;
    private static readonly XNamespace XLinkNs = OdfNamespaces.XLink;
    private static readonly XNamespace SvgNs = "urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0";
    private static readonly XNamespace StyleNs = OdfNamespaces.Style;
    private static readonly XNamespace TableNs = OdfNamespaces.Table;
    private static readonly XNamespace ConfigNs = OdfNamespaces.Config;

    private const string Base64Image = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";

    [Theory]
    [InlineData("application/vnd.oasis.opendocument.spreadsheet", "spreadsheet")]
    [InlineData("application/vnd.oasis.opendocument.presentation", "presentation")]
    [InlineData("application/vnd.oasis.opendocument.graphics", "drawing")]
    public void TestFlatXmlAndZipPackageCoreDocumentRoundTrip(string mimeType, string bodyLocalName)
    {
        var flatXml = new XDocument(
            new XElement(OfficeNs + "document",
                new XAttribute(OfficeNs + "mimetype", mimeType),
                new XAttribute(OfficeNs + "version", "1.4"),
                new XAttribute(XNamespace.Xmlns + "office", OfficeNs.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "style", StyleNs.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "table", TableNs.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "draw", DrawNs.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "config", ConfigNs.NamespaceName),
                new XElement(OfficeNs + "meta",
                    new XElement(XName.Get("generator", OdfNamespaces.Meta), "OdfKit R3")),
                new XElement(OfficeNs + "settings",
                    new XElement(ConfigNs + "config-item-set",
                        new XAttribute(ConfigNs + "name", "ooo:view-settings"),
                        new XElement(ConfigNs + "config-item",
                            new XAttribute(ConfigNs + "name", "ShowGrid"),
                            new XAttribute(ConfigNs + "type", "boolean"),
                            "true"))),
                new XElement(OfficeNs + "font-face-decls",
                    new XElement(StyleNs + "font-face",
                        new XAttribute(StyleNs + "name", "Liberation Sans"))),
                new XElement(OfficeNs + "styles",
                    new XElement(StyleNs + "style",
                        new XAttribute(StyleNs + "name", "Default"))),
                new XElement(OfficeNs + "automatic-styles",
                    new XElement(StyleNs + "style",
                        new XAttribute(StyleNs + "name", "Auto1"))),
                new XElement(OfficeNs + "master-styles"),
                new XElement(OfficeNs + "body",
                    new XElement(OfficeNs + bodyLocalName))
            )
        );

        using var msFlat = new MemoryStream();
        flatXml.Save(msFlat);
        msFlat.Position = 0;

        using var package = OdfPackage.Open(msFlat, leaveOpen: true);
        Assert.True(package.IsFlatXml);
        Assert.True(package.HasEntry("content.xml"));
        Assert.True(package.HasEntry("styles.xml"));
        Assert.True(package.HasEntry("meta.xml"));
        Assert.True(package.HasEntry("settings.xml"));

        package.IsFlatXml = false;
        using var msZip = new MemoryStream();
        package.Save(msZip);
        msZip.Position = 0;

        using (var zipPackage = OdfPackage.Open(msZip, leaveOpen: true))
        {
            OdfValidationReport report = OdfPackageValidator.Validate(zipPackage);
            Assert.True(report.IsValid, string.Join(", ", report.Issues.Select(issue => issue.RuleId + ": " + issue.Message)));
        }

        msZip.Position = 0;
        using var reloaded = OdfPackage.Open(msZip, leaveOpen: true);
        reloaded.IsFlatXml = true;
        using var msFlatOutput = new MemoryStream();
        reloaded.Save(msFlatOutput);
        msFlatOutput.Position = 0;

        XDocument output = XDocument.Load(msFlatOutput);
        Assert.NotNull(output.Descendants(OfficeNs + bodyLocalName).SingleOrDefault());
        Assert.Equal("OdfKit R3", output.Descendants(XName.Get("generator", OdfNamespaces.Meta)).Single().Value);
        Assert.Equal("true", output.Descendants(ConfigNs + "config-item").Single(item => item.Attribute(ConfigNs + "name")?.Value == "ShowGrid").Value);
        Assert.NotNull(output.Descendants(OfficeNs + "font-face-decls").SingleOrDefault());
        Assert.NotNull(output.Descendants(OfficeNs + "styles").SingleOrDefault());
    }

    [Fact]
    public void TestFlatXmlAndZipPackageImageRoundTrip()
    {
        // 1. 建立一個包含內嵌圖片 (Base64) 的 Flat XML 文件
        var flatXml = new XDocument(
            new XElement(OfficeNs + "document",
                new XAttribute(OfficeNs + "mimetype", "application/vnd.oasis.opendocument.text"),
                new XAttribute(OfficeNs + "version", "1.4"),
                new XAttribute(XNamespace.Xmlns + "office", OfficeNs.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "text", TextNs.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "draw", DrawNs.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "xlink", XLinkNs.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "svg", SvgNs.NamespaceName),
                new XElement(OfficeNs + "body",
                    new XElement(OfficeNs + "text",
                        new XElement(TextNs + "p",
                            new XElement(DrawNs + "frame",
                                new XAttribute(DrawNs + "name", "Image1"),
                                new XAttribute(TextNs + "anchor-type", "paragraph"),
                                new XAttribute(SvgNs + "width", "2cm"),
                                new XAttribute(SvgNs + "height", "2cm"),
                                new XElement(DrawNs + "image",
                                    new XElement(OfficeNs + "binary-data", Base64Image)
                                )
                            )
                        )
                    )
                )
            )
        );

        using var msFlat = new MemoryStream();
        flatXml.Save(msFlat);
        msFlat.Position = 0;

        // 2. 以 OdfPackage 載入此 Flat XML 串流
        using var package = OdfPackage.Open(msFlat, leaveOpen: true);
        Assert.True(package.IsFlatXml);
        Assert.Equal("application/vnd.oasis.opendocument.text", package.MimeType);
        Assert.Equal(OdfVersion.Odf14, package.Version);

        // 驗證內部 entries 是否已被拆分，且包含圖片路徑
        string expectedImagePath = "Pictures/image_1.png";
        Assert.True(package.HasEntry(expectedImagePath));
        Assert.True(package.HasEntry("content.xml"));

        // 3. 將 IsFlatXml 設為 false，並存為 ZIP Package 串流
        package.IsFlatXml = false;
        using var msZip = new MemoryStream();
        package.Save(msZip);
        msZip.Position = 0;

        // 4. 使用 ZipArchive 驗證 ZIP 的內部結構與 strict 規格
        using (var zip = new ZipArchive(msZip, ZipArchiveMode.Read, true))
        {
            var entries = zip.Entries;
            Assert.NotEmpty(entries);

            // strict 規格：mimetype 必須是第一個 entry，且沒有壓縮
            var firstEntry = entries[0];
            Assert.Equal("mimetype", firstEntry.Name);
            Assert.Equal(0, firstEntry.CompressedLength - firstEntry.Length); // STORED

            // 必須包含指定的 entry
            Assert.Contains(entries, e => e.FullName == expectedImagePath);
            Assert.Contains(entries, e => e.FullName == "META-INF/manifest.xml");
        }

        // 5. 重新以 OdfPackage 載入 ZIP 串流，此時應被識別為 ZIP 包
        msZip.Position = 0;
        using var packageReloaded = OdfPackage.Open(msZip, leaveOpen: true);
        Assert.False(packageReloaded.IsFlatXml);
        Assert.True(packageReloaded.HasEntry(expectedImagePath));

        // 6. 將 IsFlatXml 設為 true，並存回 Flat XML
        packageReloaded.IsFlatXml = true;
        using var msFlatOutput = new MemoryStream();
        packageReloaded.Save(msFlatOutput);
        msFlatOutput.Position = 0;

        // 7. 驗證產出的 Flat XML 內容，確認 binary-data 正確還原且 Base64 完全一致
        var docOutput = XDocument.Load(msFlatOutput);
        var binaryData = docOutput.Descendants(OfficeNs + "binary-data").FirstOrDefault();
        Assert.NotNull(binaryData);

        string base64Cleaned = binaryData.Value.Replace("\r", "").Replace("\n", "").Replace(" ", "").Replace("\t", "");
        Assert.Equal(Base64Image, base64Cleaned);
    }

    [Fact]
    public void TestFlatXmlAndZipPackageFormulaRoundTrip()
    {
        // 1. 建立一個包含內嵌公式 (office:document) 的 Flat XML 文件
        var flatXml = new XDocument(
            new XElement(OfficeNs + "document",
                new XAttribute(OfficeNs + "mimetype", "application/vnd.oasis.opendocument.text"),
                new XAttribute(OfficeNs + "version", "1.4"),
                new XAttribute(XNamespace.Xmlns + "office", OfficeNs.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "text", TextNs.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "draw", DrawNs.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "xlink", XLinkNs.NamespaceName),
                new XElement(OfficeNs + "body",
                    new XElement(OfficeNs + "text",
                        new XElement(TextNs + "p",
                            new XElement(DrawNs + "object",
                                new XAttribute(XLinkNs + "href", "./Object1"),
                                new XAttribute(XLinkNs + "type", "simple"),
                                new XAttribute(XLinkNs + "show", "embed"),
                                new XAttribute(XLinkNs + "actuate", "onLoad"),
                                new XElement(OfficeNs + "document",
                                    new XAttribute(OfficeNs + "mimetype", "application/vnd.oasis.opendocument.formula"),
                                    new XAttribute(OfficeNs + "version", "1.4"),
                                    new XElement(OfficeNs + "body",
                                        new XElement(OfficeNs + "formula",
                                            new XElement(XNamespace.Get("http://www.w3.org/1998/Math/MathML") + "math",
                                                new XElement(XNamespace.Get("http://www.w3.org/1998/Math/MathML") + "mi", "x")
                                            )
                                        )
                                    )
                                )
                            )
                        )
                    )
                )
            )
        );

        using var msFlat = new MemoryStream();
        flatXml.Save(msFlat);
        msFlat.Position = 0;

        // 2. 以 OdfPackage 載入
        using var package = OdfPackage.Open(msFlat, leaveOpen: true);
        Assert.True(package.IsFlatXml);

        // 驗證內嵌公式子目錄之 content.xml 與 mimetype 被正確抽取
        Assert.True(package.HasEntry("Object1/content.xml"));
        Assert.True(package.HasEntry("Object1/mimetype"));

        // 3. 轉存為 ZIP Package
        package.IsFlatXml = false;
        using var msZip = new MemoryStream();
        package.Save(msZip);
        msZip.Position = 0;

        // 4. 驗證 ZIP 中子目錄 Entry 的存在
        using (var zip = new ZipArchive(msZip, ZipArchiveMode.Read, true))
        {
            var entries = zip.Entries;
            Assert.Contains(entries, e => e.FullName == "Object1/content.xml");
            Assert.Contains(entries, e => e.FullName == "Object1/mimetype");
        }

        msZip.Position = 0;
        using (var zipPackage = OdfPackage.Open(msZip, leaveOpen: true))
        {
            Assert.Equal("application/vnd.oasis.opendocument.formula", zipPackage.Manifest["Object1/"]);
            Assert.Equal("text/xml", zipPackage.Manifest["Object1/content.xml"]);
            Assert.Equal("application/vnd.oasis.opendocument.formula", zipPackage.Manifest["Object1/mimetype"]);
        }

        // 5. 重新載入 ZIP Package 並轉回 Flat XML
        msZip.Position = 0;
        using var packageReloaded = OdfPackage.Open(msZip, leaveOpen: true);
        packageReloaded.IsFlatXml = true;
        using var msFlatOutput = new MemoryStream();
        packageReloaded.Save(msFlatOutput);
        msFlatOutput.Position = 0;

        // 6. 驗證產出的 Flat XML 是否包含公式的內嵌 office:document 結構
        var docOutput = XDocument.Load(msFlatOutput);
        var nestedDocument = docOutput.Descendants(OfficeNs + "document").Where(d => d != docOutput.Root).FirstOrDefault();
        Assert.NotNull(nestedDocument);
        Assert.Equal("application/vnd.oasis.opendocument.formula", nestedDocument.Attribute(OfficeNs + "mimetype")?.Value);

        var miElement = nestedDocument.Descendants(XNamespace.Get("http://www.w3.org/1998/Math/MathML") + "mi").FirstOrDefault();
        Assert.NotNull(miElement);
        Assert.Equal("x", miElement.Value);
    }

    /// <summary>
    /// 列舉所有格式支援矩陣中的格式。
    /// </summary>
    public static IEnumerable<object[]> SupportedFormats()
    {
        return OdfDocumentKindDetector.SupportedFormats.Select(format => new object[] { format });
    }

    /// <summary>
    /// 驗證每種格式的最小文件可完成 package-level round-trip。
    /// </summary>
    /// <param name="format">要驗證的格式資訊</param>
    [Theory]
    [MemberData(nameof(SupportedFormats))]
    public void MinimalSupportedFormatRoundTrips(OdfFormatInfo format)
    {
        using MemoryStream first = CreateMinimalDocument(format);
        using OdfPackage package = OdfPackage.Open(first, leaveOpen: true);

        Assert.Equal(format.IsFlatXml, package.IsFlatXml);
        Assert.Equal(format.MimeType, package.MimeType);

        using var second = new MemoryStream();
        package.Save(second);
        second.Position = 0;

        OdfValidationReport report = ValidateRoundTrippedDocument(second, format);

        Assert.True(report.IsValid, string.Join(", ", report.Issues.Select(issue => issue.RuleId + ": " + issue.Message)));
        Assert.Equal(format.Kind, report.DocumentKind);
        Assert.Equal(OdfVersion.Odf14, report.DetectedVersion);
    }

    private static MemoryStream CreateMinimalDocument(OdfFormatInfo format)
    {
        var stream = new MemoryStream();
        if (format.IsFlatXml)
        {
            OdfDocumentFactory.WriteFlatXml(stream, format.Kind, leaveOpen: true);
        }
        else
        {
            using OdfPackage package = OdfDocumentFactory.CreatePackage(stream, format.Kind, leaveOpen: true);
            package.Save();
        }

        stream.Position = 0;
        return stream;
    }

    private static OdfValidationReport ValidateRoundTrippedDocument(Stream stream, OdfFormatInfo format)
    {
        if (format.IsFlatXml)
        {
            return OdfFlatDocumentValidator.Validate(stream, "document" + format.Extension);
        }

        using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
        return OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Extended, "document" + format.Extension);
    }

    private const string UnknownXmlCustomNamespace = "urn:example:custom";
    private const string UnknownXmlOfficeExtensionNamespace = "urn:example:office-extension";

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
        XNamespace custom = UnknownXmlCustomNamespace;
        XNamespace officeExtension = UnknownXmlOfficeExtensionNamespace;

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
            "xmlns:custom=\"" + UnknownXmlCustomNamespace + "\" " +
            "xmlns:officeext=\"" + UnknownXmlOfficeExtensionNamespace + "\" " +
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
        using (OdfPackage package = OdfDocumentFactory.CreatePackage(stream, OdfDocumentKind.Text, leaveOpen: true))
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
