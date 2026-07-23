using System;
using System.IO;
using System.Linq;
using System.Text;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Spreadsheet;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證 OdfKit 高階入口與 ODF 封裝結構描述。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Smoke)]
public sealed class HighLevelApiCompletionTests
{
    /// <summary>
    /// 驗證文件層級文字擷取器可保留邏輯段落與試算表儲存格邊界。
    /// </summary>
    [Fact]
    public void ExtractTextPreservesLogicalDocumentBoundaries()
    {
        using TextDocument text = TextDocument.Create();
        OdfParagraph firstParagraph = text.AddParagraph("Alpha");
        firstParagraph.AddComment(new OdfComment("Reviewer", "Internal note"));
        text.AddParagraph("Beta");

        using SpreadsheetDocument spreadsheet = SpreadsheetDocument.Create();
        spreadsheet.Worksheets.Add("Data");
        spreadsheet.SetValue("Data", "A1", "One");
        spreadsheet.SetValue("Data", "B1", "Two");

        Assert.Equal("Alpha\nBeta", text.ExtractText());
        Assert.Contains(
            "Internal note",
            text.ExtractText(new OdfTextExtractionOptions { IncludeAnnotations = true }),
            StringComparison.Ordinal);
        Assert.Equal("One\tTwo", spreadsheet.ExtractText());
    }

    /// <summary>
    /// 驗證內嵌 ODF 子文件可列舉、開啟、取代、移除並完成往返讀寫。
    /// </summary>
    [Fact]
    public void EmbeddedDocumentsSupportManagedLifecycleAndRoundTrip()
    {
        using TextDocument host = TextDocument.Create();
        host.AddParagraph("Host");
        using SpreadsheetDocument embedded = SpreadsheetDocument.Create();
        embedded.Worksheets.Add("Data");
        embedded.SetValue("Data", "A1", "Embedded");

        OdfEmbeddedObjectInfo added = host.Package.AddEmbeddedDocument("Object 1", embedded);
        Assert.Equal(OdfDocumentKind.Spreadsheet, added.DocumentKind);
        Assert.Contains(added.Entries, entry => entry.EndsWith("/content.xml", StringComparison.Ordinal));
        using (Stream content = added.OpenContent())
        {
            using var reader = new StreamReader(content, Encoding.UTF8);
            Assert.Contains("Embedded", reader.ReadToEnd(), StringComparison.Ordinal);
        }

        Assert.Throws<ArgumentNullException>(() => host.Package.ReplaceEmbeddedDocument("Object 1", null!));
        Assert.Single(host.Package.GetEmbeddedObjectInfos());

        using TextDocument replacement = TextDocument.Create();
        replacement.AddParagraph("Replacement");
        OdfEmbeddedObjectInfo replaced = host.Package.ReplaceEmbeddedDocument("Object 1", replacement);
        Assert.Equal(OdfDocumentKind.Text, replaced.DocumentKind);

        using var saved = new MemoryStream(host.SaveToBytes());
        using TextDocument reopened = TextDocument.Load(saved);
        OdfEmbeddedObjectInfo roundTripped = Assert.Single(reopened.Package.GetEmbeddedObjectInfos());
        Assert.Equal("Object 1", roundTripped.Path);
        Assert.True(reopened.Package.RemoveEmbeddedObject(roundTripped.Path));
        Assert.Empty(reopened.Package.GetEmbeddedObjectInfos());
    }

    /// <summary>
    /// 驗證版本化官方 manifest RNG 會拒絕不合法的封裝中繼資料。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf10)]
    [InlineData(OdfVersion.Odf11)]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void OfficialManifestSchemaRejectsInvalidMetadata(OdfVersion version)
    {
        using TextDocument document = TextDocument.Create();
        document.Package.Version = version;
        string versionAttribute = version is OdfVersion.Odf10 or OdfVersion.Odf11
            ? string.Empty
            : $" manifest:version=\"{OdfVersionInfo.ToVersionString(version)}\"";
        byte[] invalidManifest = Encoding.UTF8.GetBytes(
            "<manifest:manifest xmlns:manifest=\"urn:oasis:names:tc:opendocument:xmlns:manifest:1.0\"" +
            versionAttribute + "><manifest:unexpected/></manifest:manifest>");
        document.Package.WriteEntry("META-INF/manifest.xml", invalidManifest, "text/xml");

        OdfValidationReport report = OdfPackageValidator.Validate(
            document.Package,
            GetProfile(version));

        Assert.Contains(
            report.Issues,
            issue => issue.RuleId == "ODF3111" && issue.PackagePath == "META-INF/manifest.xml");
    }

    /// <summary>
    /// 驗證 ODF 1.2～1.4 的官方 dsig RNG 會拒絕不合法的簽章中繼資料。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void OfficialDigitalSignatureSchemaRejectsInvalidMetadata(OdfVersion version)
    {
        const string SignaturePath = "META-INF/documentsignatures.xml";
        using TextDocument document = TextDocument.Create();
        document.Package.Version = version;
        document.Package.WriteEntry(
            SignaturePath,
            Encoding.UTF8.GetBytes("<unexpected xmlns=\"urn:oasis:names:tc:opendocument:xmlns:digitalsignature:1.0\"/>"),
            "text/xml");

        OdfValidationReport report = OdfPackageValidator.Validate(document.Package, GetProfile(version));

        Assert.Contains(
            report.Issues,
            issue => issue.RuleId == "ODF3111" && issue.PackagePath == SignaturePath);
    }

    /// <summary>
    /// 驗證一般封裝與 Extended 封裝會依規範區分額外的 META-INF 項目。
    /// </summary>
    [Fact]
    public void StrictAndExtendedProfilesDistinguishExtraMetaInfEntries()
    {
        using TextDocument document = TextDocument.Create();
        document.Package.WriteEntry("META-INF/vendor.xml", Encoding.UTF8.GetBytes("<vendor/>"), "text/xml");

        OdfValidationReport strict = OdfPackageValidator.Validate(document.Package, OdfValidationOptions.Odf14Strict);
        OdfValidationReport extended = OdfPackageValidator.Validate(document.Package, OdfValidationOptions.Odf14Extended);

        Assert.Contains(strict.Issues, issue => issue.RuleId == "ODF3113");
        Assert.DoesNotContain(extended.Issues, issue => issue.RuleId == "ODF3113");
    }

    private static OdfComplianceProfile GetProfile(OdfVersion version) => version switch
    {
        OdfVersion.Odf10 => OdfComplianceProfiles.OasisOdf10,
        OdfVersion.Odf11 => OdfComplianceProfiles.OasisOdf11,
        OdfVersion.Odf12 => OdfComplianceProfiles.OasisOdf12Extended,
        OdfVersion.Odf13 => OdfComplianceProfiles.OasisOdf13Extended,
        OdfVersion.Odf14 => OdfComplianceProfiles.OasisOdf14Extended,
        _ => throw new ArgumentOutOfRangeException(nameof(version))
    };
}
