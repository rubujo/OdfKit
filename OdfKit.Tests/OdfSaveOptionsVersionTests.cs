using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
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
