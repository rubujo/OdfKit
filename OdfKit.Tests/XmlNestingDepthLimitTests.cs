using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security;
using System.Text;
using OdfKit.Compliance;
using OdfKit.Core;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 回歸測試：超深的 XML 元素巢狀不得使處理程序崩潰或耗盡記憶體。
/// 修正前：Flat ODF 載入會因 XElement 複製遞迴而堆疊溢位；啟用 Profile 規則的封裝驗證會因
/// SanitizeForeignContentForSchemaValidation 遞迴而堆疊溢位，且 ScanXml 為每一層保存完整 XPath，
/// 記憶體隨深度呈二次方成長。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Boundary)]
public sealed class XmlNestingDepthLimitTests
{
    private const string Namespaces =
        "xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\"";

    private static string BuildBody(int depth) =>
        "<office:body><office:text><text:p>" +
        string.Concat(Enumerable.Repeat("<text:span>", depth)) + "x" +
        string.Concat(Enumerable.Repeat("</text:span>", depth)) +
        "</text:p></office:text></office:body>";

    private static byte[] BuildFlatDocument(int depth) =>
        Encoding.UTF8.GetBytes(
            "<?xml version=\"1.0\"?><office:document " + Namespaces +
            " office:version=\"1.3\" office:mimetype=\"application/vnd.oasis.opendocument.text\">" +
            BuildBody(depth) + "</office:document>");

    private static byte[] BuildPackage(int depth)
    {
        string content =
            "<?xml version=\"1.0\"?><office:document-content " + Namespaces + " office:version=\"1.3\">" +
            BuildBody(depth) + "</office:document-content>";
        const string manifest =
            "<?xml version=\"1.0\"?><manifest:manifest xmlns:manifest=\"urn:oasis:names:tc:opendocument:xmlns:manifest:1.0\" manifest:version=\"1.3\">" +
            "<manifest:file-entry manifest:full-path=\"/\" manifest:media-type=\"application/vnd.oasis.opendocument.text\"/>" +
            "<manifest:file-entry manifest:full-path=\"content.xml\" manifest:media-type=\"text/xml\"/></manifest:manifest>";

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Add(string name, string text, CompressionLevel level = CompressionLevel.Optimal)
            {
                using Stream w = zip.CreateEntry(name, level).Open();
                byte[] b = Encoding.UTF8.GetBytes(text);
                w.Write(b, 0, b.Length);
            }

            Add("mimetype", "application/vnd.oasis.opendocument.text", CompressionLevel.NoCompression);
            Add("META-INF/manifest.xml", manifest);
            Add("content.xml", content);
        }

        return ms.ToArray();
    }

    [Theory]
    [InlineData(1_000)]
    [InlineData(100_000)]
    public void FlatDocumentLoadWithExcessiveNestingIsRejectedWithoutCrashing(int depth)
    {
        byte[] flat = BuildFlatDocument(depth);

        Assert.ThrowsAny<Exception>(() =>
        {
            using OdfDocument document = OdfDocument.Load(new MemoryStream(flat));
        });
    }

    [Fact]
    public void FlatDocumentLoadWithReasonableNestingSucceeds()
    {
        using OdfDocument document = OdfDocument.Load(new MemoryStream(BuildFlatDocument(100)));

        Assert.NotNull(document);
    }

    [Fact]
    public void FlatDocumentLoadWithNestingBeyondLimitThrowsSecurityException()
    {
        byte[] flat = BuildFlatDocument(1_000);

        Assert.Throws<SecurityException>(() =>
        {
            using OdfDocument document = OdfDocument.Load(new MemoryStream(flat));
        });
    }

    [Theory]
    [InlineData(1_000)]
    [InlineData(100_000)]
    public void ProfileValidationWithExcessiveNestingReportsFatalIssueInsteadOfCrashing(int depth)
    {
        using OdfPackage package = OdfPackage.Open(new MemoryStream(BuildPackage(depth)));

        OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf12Strict);

        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, issue => issue.Severity == OdfIssueSeverity.Fatal);
    }

    [Fact]
    public void ProfileValidationWithReasonableNestingDoesNotReportNestingIssue()
    {
        using OdfPackage package = OdfPackage.Open(new MemoryStream(BuildPackage(100)));

        OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf12Strict);

        Assert.DoesNotContain(report.Issues, issue => issue.RuleId == "ODF0303");
    }
}
