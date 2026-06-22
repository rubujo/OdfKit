using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證簽章、加密與巨集淨化的保真邊界。
/// </summary>
public class OdfSecurityBoundaryTests
{
    private const string Password = "R5BoundaryPassword";
    private static readonly XNamespace ScriptNs = "urn:oasis:names:tc:opendocument:xmlns:script:1.0";
    private static readonly XNamespace XLinkNs = OdfNamespaces.XLink;
    private static readonly XNamespace CustomNs = "urn:odfkit:test:foreign";

    /// <summary>
    /// 驗證未編輯內容時，單純封裝保存會保留既有文件簽章專案。
    /// </summary>
    [Fact]
    public void SignaturePackageSaveWithoutContentEditsPreservesSignatureEntry()
    {
        using MemoryStream source = CreateTextPackage();
        using (OdfPackage package = OdfPackage.Open(source, leaveOpen: true))
        {
            AddSignatureEntry(package);
            using var saved = new MemoryStream();

            package.SaveToStream(saved);
            saved.Position = 0;

            using OdfPackage reopened = OdfPackage.Open(saved, leaveOpen: true);
            Assert.True(reopened.HasEntry("META-INF/documentsignatures.xml"));
        }
    }

    /// <summary>
    /// 驗證內容被修改時，已過期的文件簽章會被移除。
    /// </summary>
    [Fact]
    public void SignatureContentEditRemovesOutdatedSignatureEntry()
    {
        using MemoryStream source = CreateTextPackage();
        using OdfPackage package = OdfPackage.Open(source, leaveOpen: true);
        AddSignatureEntry(package);

        package.WriteEntry("content.xml", Encoding.UTF8.GetBytes(CreateContentXml("已修改內容")), "text/xml");

        Assert.False(package.HasEntry("META-INF/documentsignatures.xml"));
    }

    /// <summary>
    /// 驗證文件層簽章摘要會回報未簽署文件。
    /// </summary>
    [Fact]
    public void DocumentSignatureSummaryReportsUnsignedDocument()
    {
        using OdfDocument document = OdfDocument.Create(OdfDocumentKind.Text);

        OdfDocumentSignatureSummary summary = document.GetSignatureSummary();

        Assert.Equal("META-INF/documentsignatures.xml", summary.SignatureEntryPath);
        Assert.False(summary.HasSignatureEntry);
        Assert.False(summary.IsSignatureEntryReadable);
        Assert.False(summary.IsSigned);
        Assert.Equal(0, summary.SignatureCount);
        Assert.Null(summary.ErrorMessage);
    }

    /// <summary>
    /// 驗證文件層簽章摘要會以命名空間 URI 統計 XML 簽章數。
    /// </summary>
    [Fact]
    public void DocumentSignatureSummaryCountsXmlSignaturesByNamespaceUri()
    {
        using MemoryStream source = CreateTextPackage();
        using var document = new TextDocument(OdfPackage.Open(source, leaveOpen: true));
        document.Package.WriteEntry(
            "META-INF/documentsignatures.xml",
            Encoding.UTF8.GetBytes($"""
                <odfds:document-signatures xmlns:odfds="urn:oasis:names:tc:opendocument:xmlns:digitalsignature:1.0"
                                           xmlns:any="{OdfNamespaces.Ds}">
                  <any:Signature Id="a" />
                  <any:Signature Id="b" />
                  <Signature xmlns="urn:odfkit:test:ignored" />
                </odfds:document-signatures>
                """),
            "text/xml");

        OdfDocumentSignatureSummary summary = document.GetSignatureSummary();

        Assert.True(summary.HasSignatureEntry);
        Assert.True(summary.IsSignatureEntryReadable);
        Assert.True(summary.IsSigned);
        Assert.Equal(2, summary.SignatureCount);
        Assert.Null(summary.ErrorMessage);
    }

    /// <summary>
    /// 驗證文件層簽章摘要會安全回報無法解析的簽章專案。
    /// </summary>
    [Fact]
    public void DocumentSignatureSummaryReportsUnreadableSignatureEntry()
    {
        using MemoryStream source = CreateTextPackage();
        using var document = new TextDocument(OdfPackage.Open(source, leaveOpen: true));
        document.Package.WriteEntry(
            "META-INF/documentsignatures.xml",
            Encoding.UTF8.GetBytes("<broken-signatures>"),
            "text/xml");

        OdfDocumentSignatureSummary summary = document.GetSignatureSummary();

        Assert.True(summary.HasSignatureEntry);
        Assert.False(summary.IsSignatureEntryReadable);
        Assert.False(summary.IsSigned);
        Assert.Equal(0, summary.SignatureCount);
        Assert.NotNull(summary.ErrorMessage);
    }

    /// <summary>
    /// 驗證文件層詳細驗章入口會委派到底層封裝驗章器。
    /// </summary>
    [Fact]
    public async Task DocumentVerifySignaturesAsyncReportsUnsignedDocument()
    {
        using OdfDocument document = OdfDocument.Create(OdfDocumentKind.Text);

        OdfSignatureValidationResult result = await document.VerifySignaturesAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Empty(result.Signatures);
    }

    /// <summary>
    /// 驗證巨集淨化會移除巨集與簽章，但不會誤刪安全的 foreign content。
    /// </summary>
    [Fact]
    public void MacroSanitizeRemovesMacroArtifactsButPreservesForeignContent()
    {
        using MemoryStream source = CreateTextPackage(includeMacroContent: true);
        using var document = new TextDocument(OdfPackage.Open(source, leaveOpen: true));
        AddMacroAndSignatureEntries(document.Package);

        document.SanitizeMacros();

        using var saved = new MemoryStream();
        document.SaveToStream(saved);
        saved.Position = 0;

        using OdfPackage package = OdfPackage.Open(saved, leaveOpen: true);
        Assert.False(package.HasEntry("Basic/script.xlb"));
        Assert.False(package.HasEntry("Scripts/python/hello.py"));
        Assert.False(package.HasEntry("META-INF/macrosignatures.xml"));
        Assert.False(package.HasEntry("META-INF/documentsignatures.xml"));

        XDocument content = ReadXml(package, "content.xml");
        Assert.Empty(content.Descendants(ScriptNs + "event-listener"));
        Assert.Empty(content.Descendants(ScriptNs + "script"));
        Assert.Contains(content.Descendants(CustomNs + "payload"), element => element.Value == "安全外來內容");
        Assert.Contains(
            content.Descendants(CustomNs + "resource"),
            element => element.Attribute(XLinkNs + "href")?.Value == "Pictures/foreign-safe.bin");
    }

    /// <summary>
    /// 驗證加密文件淨化後，可用單次儲存選項重新加密輸出。
    /// </summary>
    [Fact]
    public void EncryptionSanitizeCanResaveAsEncryptedPackage()
    {
        using MemoryStream encrypted = CreateEncryptedTextPackage();
        using var document = new TextDocument(OdfPackage.Open(
            encrypted,
            leaveOpen: true,
            new OdfLoadOptions { Password = Password }));

        AddMacroAndSignatureEntries(document.Package);
        document.SanitizeMacros();

        using var saved = new MemoryStream();
        document.SaveToStream(saved, new OdfSaveOptions
        {
            Password = Password,
            EncryptionAlgorithm = OdfEncryptionAlgorithm.Aes256
        });

        saved.Position = 0;
        using (OdfPackage metadataPackage = OdfPackage.Open(saved, leaveOpen: true))
        {
            Assert.NotNull(metadataPackage.GetEntryEncryptionInfo("content.xml"));
        }

        saved.Position = 0;
        using OdfPackage reopened = OdfPackage.Open(saved, leaveOpen: true, new OdfLoadOptions { Password = Password });
        Assert.False(reopened.HasEntry("Basic/script.xlb"));
        Assert.False(reopened.HasEntry("Scripts/python/hello.py"));
        Assert.False(reopened.HasEntry("META-INF/macrosignatures.xml"));
        Assert.False(reopened.HasEntry("META-INF/documentsignatures.xml"));

        XDocument content = ReadXml(reopened, "content.xml");
        Assert.Empty(content.Descendants(ScriptNs + "event-listener"));
        Assert.Contains(content.Descendants(CustomNs + "payload"), element => element.Value == "安全外來內容");
    }

    private static MemoryStream CreateTextPackage(bool includeMacroContent = false)
    {
        var stream = new MemoryStream();
        using (OdfPackage package = OdfDocumentFactory.CreatePackage(stream, OdfDocumentKind.Text, leaveOpen: true))
        {
            package.WriteEntry("content.xml", Encoding.UTF8.GetBytes(CreateContentXml(includeMacroContent ? "巨集邊界" : "一般內容", includeMacroContent)), "text/xml");
            package.WriteEntry("styles.xml", Encoding.UTF8.GetBytes(CreateStylesXml()), "text/xml");
            package.WriteEntry("meta.xml", Encoding.UTF8.GetBytes(CreateMetaXml()), "text/xml");
            package.WriteEntry("settings.xml", Encoding.UTF8.GetBytes(CreateSettingsXml()), "text/xml");
            package.WriteEntry("Pictures/foreign-safe.bin", [1, 2, 3, 4], "application/octet-stream");
            package.Save();
        }

        stream.Position = 0;
        return stream;
    }

    private static MemoryStream CreateEncryptedTextPackage()
    {
        MemoryStream stream = CreateTextPackage(includeMacroContent: true);
        var encrypted = new MemoryStream();
        using (OdfPackage package = OdfPackage.Open(stream, leaveOpen: false))
        {
            package.SaveToStream(encrypted, new OdfSaveOptions
            {
                Password = Password,
                EncryptionAlgorithm = OdfEncryptionAlgorithm.Aes256
            });
        }

        encrypted.Position = 0;
        return encrypted;
    }

    private static void AddSignatureEntry(OdfPackage package)
    {
        package.WriteEntry(
            "META-INF/documentsignatures.xml",
            Encoding.UTF8.GetBytes("<dsig:document-signatures xmlns:dsig=\"urn:oasis:names:tc:opendocument:xmlns:digitalsignature:1.0\"/>"),
            "text/xml");
    }

    private static void AddMacroAndSignatureEntries(OdfPackage package)
    {
        package.WriteEntry("Basic/script.xlb", Encoding.UTF8.GetBytes("macro"), "application/octet-stream");
        package.WriteEntry("Scripts/python/hello.py", Encoding.UTF8.GetBytes("print('macro')"), "text/x-python");
        package.WriteEntry("META-INF/macrosignatures.xml", Encoding.UTF8.GetBytes("<macro-signatures/>"), "text/xml");
        AddSignatureEntry(package);
    }

    private static XDocument ReadXml(OdfPackage package, string entryName)
    {
        using Stream stream = package.GetEntryStream(entryName);
        return XDocument.Load(stream);
    }

    private static string CreateContentXml(string paragraphText, bool includeMacroContent = false)
    {
        string macroXml = includeMacroContent
            ? "<office:scripts><office:event-listeners><script:event-listener script:event-name=\"dom-click\" script:language=\"ooo:script\" script:macro-name=\"MyMacro\" /></office:event-listeners><script:script script:language=\"StarBasic\">Sub Main\nEnd Sub</script:script></office:scripts>"
            : string.Empty;

        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <office:document-content
                xmlns:office="{OdfNamespaces.Office}"
                xmlns:text="{OdfNamespaces.Text}"
                xmlns:script="{ScriptNs}"
                xmlns:xlink="{OdfNamespaces.XLink}"
                xmlns:custom="{CustomNs}"
                office:version="1.4">
              {macroXml}
              <office:body>
                <office:text>
                  <text:p>{paragraphText}</text:p>
                  <custom:payload custom:flag="keep">安全外來內容</custom:payload>
                  <custom:resource xlink:type="simple" xlink:href="Pictures/foreign-safe.bin" />
                </office:text>
              </office:body>
            </office:document-content>
            """;
    }

    private static string CreateStylesXml()
    {
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <office:document-styles xmlns:office="{OdfNamespaces.Office}" office:version="1.4">
              <office:styles />
            </office:document-styles>
            """;
    }

    private static string CreateMetaXml()
    {
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <office:document-meta xmlns:office="{OdfNamespaces.Office}" office:version="1.4">
              <office:meta />
            </office:document-meta>
            """;
    }

    private static string CreateSettingsXml()
    {
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <office:document-settings xmlns:office="{OdfNamespaces.Office}" office:version="1.4">
              <office:settings />
            </office:document-settings>
            """;
    }
}
