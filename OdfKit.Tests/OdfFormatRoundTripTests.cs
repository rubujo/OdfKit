using System.Collections.Generic;
using System.IO;
using System.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證每一種已知 ODF 格式至少能建立、載入、保存並重新驗證最小文件。
/// </summary>
public class OdfFormatRoundTripTests
{
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
    /// <param name="format">要驗證的格式資訊。</param>
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
}
