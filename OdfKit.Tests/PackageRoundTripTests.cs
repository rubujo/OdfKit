using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
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

    private const string Base64Image = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";

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
}
