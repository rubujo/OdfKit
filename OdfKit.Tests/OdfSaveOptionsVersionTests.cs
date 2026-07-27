using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證 ODF 儲存選項中的版本保存、強制版本與確定性輸出策略。
/// </summary>
public class OdfSaveOptionsVersionTests
{
    /// <summary>
    /// 驗證未指定強制版本時，保存會保留載入文件宣告的舊版 ODF 版本。
    /// </summary>
    [Fact]
    public void SavePreservesLoadedOdfVersionByDefault()
    {
        using MemoryStream source = CreateTextPackage(OdfVersion.Odf12);
        using var document = new TextDocument(OdfPackage.Open(source, leaveOpen: true));

        document.AddParagraph("保留 ODF 1.2");

        using var saved = new MemoryStream();
        document.SaveToStream(saved);
        saved.Position = 0;

        using OdfPackage package = OdfPackage.Open(saved, leaveOpen: true);
        Assert.Equal("1.2", ReadOfficeVersion(package, "content.xml"));
        Assert.Equal("1.2", ReadOfficeVersion(package, "styles.xml"));
        Assert.Equal("1.2", ReadOfficeVersion(package, "meta.xml"));
        Assert.Equal("1.2", ReadOfficeVersion(package, "settings.xml"));
        Assert.Equal("1.2", ReadManifestVersion(package));
    }

    /// <summary>
    /// 驗證強制版本會同步更新核心 XML 與 manifest 版本。
    /// </summary>
    [Fact]
    public void SaveCanForceOdfVersionAcrossCoreXmlAndManifest()
    {
        using MemoryStream source = CreateTextPackage(OdfVersion.Odf12);
        using var document = new TextDocument(OdfPackage.Open(source, leaveOpen: true));

        document.AddParagraph("升級到 ODF 1.4");

        using var saved = new MemoryStream();
        document.SaveToStream(saved, new OdfSaveOptions { ForceVersion = OdfVersion.Odf14 });
        saved.Position = 0;

        using OdfPackage package = OdfPackage.Open(saved, leaveOpen: true);
        Assert.Equal("1.4", ReadOfficeVersion(package, "content.xml"));
        Assert.Equal("1.4", ReadOfficeVersion(package, "styles.xml"));
        Assert.Equal("1.4", ReadOfficeVersion(package, "meta.xml"));
        Assert.Equal("1.4", ReadOfficeVersion(package, "settings.xml"));
        Assert.Equal("1.4", ReadManifestVersion(package));
    }

    /// <summary>
    /// 驗證確定性儲存會使用固定 ZIP timestamp。
    /// </summary>
    [Fact]
    public void DeterministicSaveUsesStableZipTimestamps()
    {
        using MemoryStream source = CreateTextPackage(OdfVersion.Odf14);
        using var document = new TextDocument(OdfPackage.Open(source, leaveOpen: true));

        using var saved = new MemoryStream();
        document.SaveToStream(saved, new OdfSaveOptions { Deterministic = true });
        saved.Position = 0;

        using var zip = new ZipArchive(saved, ZipArchiveMode.Read, leaveOpen: true);
        var expectedTimestamp = new DateTime(2026, 1, 1, 0, 0, 0);
        Assert.All(
            zip.Entries.Where(entry => entry.Length > 0),
            entry => Assert.Equal(expectedTimestamp, entry.LastWriteTime.DateTime));
    }

    /// <summary>
    /// Verifies a safe downgrade produces an empty structured report during save.
    /// 驗證安全降版會在儲存期間產生不含問題的結構化報告。
    /// </summary>
    [Fact]
    public void VersionCompatibilityReportSafeDowngradeIsReportedDuringSave()
    {
        using TextDocument document = TextDocument.Create();
        document.AddParagraph("ODF 1.3 safe content");
        OdfVersionCompatibilityReport? callbackReport = null;

        using var saved = new MemoryStream();
        document.SaveToStream(
            saved,
            new OdfSaveOptions
            {
                ForceVersion = OdfVersion.Odf13,
                VersionCompatibilityReportHandler = report => callbackReport = report
            });

        Assert.NotNull(callbackReport);
        Assert.True(callbackReport.IsSafe);
        Assert.Empty(callbackReport.Issues);
        Assert.Same(callbackReport, document.LastVersionCompatibilityReport);
    }

    /// <summary>
    /// Verifies ODF 1.4-only elements and attributes produce structured downgrade diagnostics without deleting content.
    /// 驗證僅限 ODF 1.4 的元素與屬性會產生結構化降版診斷，且不會刪除內容。
    /// </summary>
    [Fact]
    public void VersionCompatibilityReportReportsUnsupportedSemanticsAndPreservesThem()
    {
        using TextDocument document = TextDocument.Create();
        OdfUnknownElement odf14Element = new("num-list-format", OdfNamespaces.Number, "number");
        document.BodyTextRoot.AppendChild(odf14Element);
        odf14Element.SetAttribute("decorative", OdfNamespaces.Draw, "true", "draw");
        odf14Element.AppendChild(new OdfUnknownElement("payload", "urn:example:foreign", "ext"));

        OdfVersionCompatibilityReport report = document.AnalyzeVersionCompatibility(OdfVersion.Odf13);

        Assert.False(report.IsSafe);
        Assert.Contains(
            report.Issues,
            issue => issue.Kind == OdfVersionCompatibilityIssueKind.ElementNotSupported &&
                issue.LocalName == "num-list-format");
        Assert.Contains(
            report.Issues,
            issue => issue.Kind == OdfVersionCompatibilityIssueKind.AttributeNotSupported &&
                issue.LocalName == "decorative");
        Assert.DoesNotContain(report.Issues, issue => issue.NamespaceUri == "urn:example:foreign");

        using var saved = new MemoryStream();
        document.SaveToStream(saved, new OdfSaveOptions { ForceVersion = OdfVersion.Odf13 });
        saved.Position = 0;
        using TextDocument reloaded = TextDocument.Load(saved, "downgraded.odt");
        Assert.Contains(
            reloaded.BodyTextRoot.Descendants(),
            node => node.LocalName == "num-list-format" && node.NamespaceUri == OdfNamespaces.Number);
        Assert.NotNull(document.LastVersionCompatibilityReport);
        Assert.False(document.LastVersionCompatibilityReport.IsSafe);
    }

    /// <summary>
    /// Verifies structured downgrade diagnostics cover all four primary document formats.
    /// 驗證結構化降版診斷涵蓋四種主要文件格式。
    /// </summary>
    [Theory]
    [InlineData(OdfDocumentKind.Text)]
    [InlineData(OdfDocumentKind.Spreadsheet)]
    [InlineData(OdfDocumentKind.Presentation)]
    [InlineData(OdfDocumentKind.Graphics)]
    public void VersionCompatibilityReportCoversEveryPrimaryFormat(OdfDocumentKind kind)
    {
        using OdfDocument document = OdfDocument.Create(kind);
        document.ContentDom.AppendChild(
            new OdfUnknownElement("num-list-format", OdfNamespaces.Number, "number"));

        OdfVersionCompatibilityReport report = document.AnalyzeVersionCompatibility(OdfVersion.Odf13);

        Assert.Contains(
            report.Issues,
            issue => issue.Kind == OdfVersionCompatibilityIssueKind.ElementNotSupported &&
                issue.LocalName == "num-list-format");
    }

    private static MemoryStream CreateTextPackage(OdfVersion version)
    {
        var stream = new MemoryStream();
        using (OdfPackage package = OdfDocumentFactory.CreatePackage(stream, OdfDocumentKind.Text, version, leaveOpen: true))
        {
            package.Save();
        }

        stream.Position = 0;
        return stream;
    }

    private static string? ReadOfficeVersion(OdfPackage package, string entryName)
    {
        using Stream stream = package.GetEntryStream(entryName);
        XDocument document = XDocument.Load(stream);
        return document.Root?.Attribute(XName.Get("version", OdfNamespaces.Office))?.Value;
    }

    private static string? ReadManifestVersion(OdfPackage package)
    {
        using Stream stream = package.GetEntryStream("META-INF/manifest.xml");
        XDocument document = XDocument.Load(stream);
        return document.Root?.Attribute(XName.Get("version", OdfNamespaces.Manifest))?.Value;
    }
}
