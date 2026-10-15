using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using Xunit;

namespace OdfKit.Tests;

[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfPackageSignatureConformanceTests
{
    public static IEnumerable<object[]> SignatureCases()
    {
        foreach (OdfVersion version in new[] { OdfVersion.Odf12, OdfVersion.Odf13, OdfVersion.Odf14 })
            foreach (string path in new[] { "META-INF/documentsignatures.xml", "META-INF/nested/documentsignatures.xml", "META-INF/signatures/vendor.bin" })
                foreach (int scenario in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 })
                    yield return [version, path, scenario];
    }

    [Theory]
    [MemberData(nameof(SignatureCases))]
    public void SignatureFilesAreRecognizedWithoutManifestRegistration(OdfVersion version, string path, int scenario)
    {
        string signature = scenario switch
        {
            0 => "<ds:Signature Id='s1'><ds:KeyInfo/></ds:Signature>",
            1 => "<ds:Signature><ds:KeyInfo/></ds:Signature>",
            2 => "<ds:Signature Id='same'><ds:KeyInfo/></ds:Signature><ds:Signature Id='same'><ds:KeyInfo/></ds:Signature>",
            3 => "<ds:Signature Id='s1'/>",
            4 => "<ds:Signature Id='s1'><ds:KeyInfo/><ds:KeyInfo/></ds:Signature>",
            _ => CreateReferenceSignature(scenario)
        };
        string xml = $"<d:document-signatures xmlns:d='urn:oasis:names:tc:opendocument:xmlns:digitalsignature:1.0' xmlns:ds='http://www.w3.org/2000/09/xmldsig#' d:version='{OdfVersionInfo.ToVersionString(version)}'>{signature}</d:document-signatures>";
        using MemoryStream stream = CreatePackage(version, path, xml);
        using OdfPackage package = OdfPackage.Open(stream);
        Assert.False(package.Manifest.ContainsKey(path));
        foreach (OdfComplianceProfile profile in Profiles(version))
        {
            OdfValidationReport report = OdfPackageValidator.Validate(package, profile);
            Assert.DoesNotContain(report.Issues, issue => issue.RuleId is "ODF0102" or "ODF0110" or "ODF3113" or "ODF3111");
            if (scenario is 1 or 2)
                Assert.Contains(report.Issues, issue => issue.RuleId == "ODF3114" && issue.PackagePath == path);
            else
                Assert.DoesNotContain(report.Issues, issue => issue.RuleId == "ODF3114");
            if (scenario is 3 or 4)
                Assert.Contains(report.Issues, issue => issue.RuleId == "ODF3115" && issue.PackagePath == path);
            else
                Assert.DoesNotContain(report.Issues, issue => issue.RuleId == "ODF3115");
            if (scenario == 5)
                Assert.Contains(report.Issues, issue => issue.RuleId == "ODF3116" && issue.PackagePath == path);
            else
                Assert.DoesNotContain(report.Issues, issue => issue.RuleId == "ODF3116");
        }
    }

    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void SignatureMetadataValidatesXmlIdsAfterWhitespaceNormalization(OdfVersion version)
    {
        const string path = "META-INF/documentsignatures.xml";
        foreach (string value in new[] { "alpha", " alpha ", "", "a:b", "two words" })
        {
            string xml = $"<d:document-signatures xmlns:d='urn:oasis:names:tc:opendocument:xmlns:digitalsignature:1.0' xmlns:ds='http://www.w3.org/2000/09/xmldsig#' d:version='{OdfVersionInfo.ToVersionString(version)}'><ds:Signature Id='s1'><ds:KeyInfo/><ds:Object xml:id='{value}'/><ds:Object xml:id='alpha'/></ds:Signature></d:document-signatures>";
            using MemoryStream stream = CreatePackage(version, path, xml);
            using OdfPackage package = OdfPackage.Open(stream);
            OdfValidationReport report = OdfPackageValidator.Validate(package, Profiles(version)[1]);
            Assert.DoesNotContain(report.Issues, issue => issue.RuleId == "ODF3111");
            if (value.Trim() == "alpha")
            {
                Assert.Contains(report.Issues, issue => issue.RuleId == "ODF3104" && issue.PackagePath == path);
                Assert.DoesNotContain(report.Issues, issue => issue.RuleId == "ODF3105");
            }
            else
                Assert.Contains(report.Issues, issue => issue.RuleId == "ODF3105" && issue.PackagePath == path && issue.Severity == OdfIssueSeverity.Error);
        }
    }

    private static string CreateReferenceSignature(int scenario)
    {
        string algorithm = scenario switch
        {
            6 => "http://www.w3.org/TR/2001/REC-xml-c14n-20010315",
            7 => "http://www.w3.org/2006/12/xml-c14n11",
            8 => "http://www.w3.org/2001/10/xml-exc-c14n#",
            _ => "http://www.w3.org/TR/1999/REC-xslt-19991116"
        };
        string uri = scenario switch
        {
            9 => "#signed-properties",
            10 => "https://example.test/external.xml",
            _ => "content.xml"
        };
        return $"<ds:Signature Id='s1'><ds:SignedInfo><ds:Reference URI='{uri}'><ds:Transforms><ds:Transform Algorithm='{algorithm}'/></ds:Transforms></ds:Reference></ds:SignedInfo><ds:KeyInfo/></ds:Signature>";
    }

    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void MissingPayloadManifestEntryIsAnError(OdfVersion version)
    {
        const string Path = "Pictures/payload.bin";
        using MemoryStream stream = CreatePackage(version, Path, "payload");
        using OdfPackage package = OdfPackage.Open(stream);
        foreach (OdfComplianceProfile profile in Profiles(version))
        {
            OdfValidationReport report = OdfPackageValidator.Validate(package, profile);
            Assert.False(report.IsValid);
            Assert.Contains(report.Issues, issue => issue.RuleId == "ODF0102" && issue.PackagePath == Path && issue.Severity == OdfIssueSeverity.Error);
        }
    }

    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void SignatureDirectoryIsPartOfTheZipEntryName(OdfVersion version)
    {
        const string Path = "META-INF/signatures/vendor.bin";
        using MemoryStream stream = CreatePackage(version, Path, "payload");
        using OdfPackage package = OdfPackage.Open(stream);
        OdfComplianceProfile[] profiles = Profiles(version);
        OdfValidationReport strict = OdfPackageValidator.Validate(package, profiles[0]);
        OdfValidationReport extended = OdfPackageValidator.Validate(package, profiles[1]);
        Assert.DoesNotContain(strict.Issues, issue => issue.RuleId == "ODF3113");
        Assert.DoesNotContain(extended.Issues, issue => issue.RuleId == "ODF3113");
        Assert.DoesNotContain(strict.Issues, issue => issue.RuleId == "ODF0102");
        Assert.DoesNotContain(extended.Issues, issue => issue.RuleId == "ODF0102");
        Assert.Contains(strict.Issues, issue => issue.RuleId == "ODF0301" && issue.PackagePath == Path);
        Assert.Contains(extended.Issues, issue => issue.RuleId == "ODF0301" && issue.PackagePath == Path);
    }

    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void NestedSignatureFilesStillRequireTheDigitalSignatureGrammar(OdfVersion version)
    {
        const string Path = "META-INF/nested/documentsignatures.xml";
        using MemoryStream stream = CreatePackage(version, Path, "<unexpected/>");
        using OdfPackage package = OdfPackage.Open(stream);
        foreach (OdfComplianceProfile profile in Profiles(version))
        {
            OdfValidationReport report = OdfPackageValidator.Validate(package, profile);
            Assert.Contains(report.Issues, issue => issue.RuleId == "ODF3111" && issue.PackagePath == Path);
            Assert.DoesNotContain(report.Issues, issue => issue.RuleId == "ODF3113");
        }
    }

    private static OdfComplianceProfile[] Profiles(OdfVersion version) => version switch
    {
        OdfVersion.Odf12 => [OdfComplianceProfiles.OasisOdf12Strict, OdfComplianceProfiles.OasisOdf12Extended],
        OdfVersion.Odf13 => [OdfComplianceProfiles.OasisOdf13Strict, OdfComplianceProfiles.OasisOdf13Extended],
        _ => [OdfComplianceProfiles.OasisOdf14Strict, OdfComplianceProfiles.OasisOdf14Extended]
    };

    private static MemoryStream CreatePackage(OdfVersion version, string path, string xml)
    {
        var stream = new MemoryStream();
        using (OdfPackage writer = OdfDocumentFactory.CreatePackage(stream, OdfDocumentKind.Text, version, true))
        {
            writer.WriteEntry(path, Encoding.UTF8.GetBytes(xml), "text/xml");
            writer.Save();
        }
        stream.Position = 0;
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            ZipArchiveEntry manifest = archive.GetEntry("META-INF/manifest.xml")!;
            XDocument document;
            using (Stream content = manifest.Open())
                document = XDocument.Load(content);
            XNamespace ns = OdfNamespaces.Manifest;
            document.Descendants(ns + "file-entry").Where(entry => (string?)entry.Attribute(ns + "full-path") == path).Remove();
            manifest.Delete();
            using Stream output = archive.CreateEntry("META-INF/manifest.xml").Open();
            document.Save(output);
        }
        stream.Position = 0;
        return stream;
    }
}
