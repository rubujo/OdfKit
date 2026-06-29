using System.IO;
using System.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證格式支援矩陣中的最小文件可完成 package-level round-trip。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Regression)]
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class PackageRoundTripMatrixTests
{
    /// <summary>
    /// 驗證每種格式的最小文件可完成 package-level round-trip。
    /// </summary>
    [Fact(Explicit = true)]
    public void MinimalSupportedFormatRoundTrips()
    {
        foreach (OdfFormatInfo format in OdfDocumentKindDetector.SupportedFormats)
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
}
