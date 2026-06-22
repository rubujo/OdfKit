using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OdfKit.Compliance;
using OdfKit.Core;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證 interop corpus 的最小格式覆蓋與保真行為。
/// </summary>
public class InteropCorpusTests
{
    private static readonly byte[] UnknownPayload = Encoding.UTF8.GetBytes("odfkit interop corpus");

    /// <summary>
    /// 列舉主要封裝格式 corpus。
    /// </summary>
    public static IEnumerable<object[]> PackageCorpusFormats()
    {
        return OdfDocumentKindDetector.SupportedFormats
            .Where(format => !format.IsFlatXml)
            .Select(format => new object[] { format });
    }

    /// <summary>
    /// 驗證每種主要封裝格式都可經由公開 validator 入口取得穩定報告。
    /// </summary>
    /// <param name="format">要驗證的格式資訊</param>
    [Theory]
    [MemberData(nameof(PackageCorpusFormats))]
    public void PackageCorpusValidatesThroughPublicEntryPoint(OdfFormatInfo format)
    {
        using MemoryStream source = CreateMinimalPackage(format);

        OdfValidationReport sourceReport = OdfValidator.Validate(
            source,
            new OdfValidationOptions
            {
                FileName = "document" + format.Extension,
                Profile = OdfComplianceProfiles.OasisOdf14Extended
            });

        Assert.True(sourceReport.IsValid, FormatIssues(sourceReport));
        Assert.Equal(format.Kind, sourceReport.DocumentKind);
        Assert.Equal(OdfVersion.Odf14, sourceReport.DetectedVersion);
    }

    /// <summary>
    /// 驗證每種主要封裝格式保存後會保留未知 package entry。
    /// </summary>
    /// <param name="format">要驗證的格式資訊</param>
    [Theory]
    [MemberData(nameof(PackageCorpusFormats))]
    public void PackageCorpusPreservesUnknownEntry(OdfFormatInfo format)
    {
        using MemoryStream source = CreatePackageWithUnknownEntry(format);

        source.Position = 0;
        using OdfPackage package = OdfPackage.Open(source, leaveOpen: true);
        using var saved = new MemoryStream();
        package.Save(saved);
        saved.Position = 0;

        using OdfPackage reopened = OdfPackage.Open(saved, leaveOpen: true);
        Assert.True(reopened.HasEntry("Interop/unknown.bin"));
        using Stream unknown = reopened.GetEntryStream("Interop/unknown.bin");
        using var buffer = new MemoryStream();
        unknown.CopyTo(buffer);
        Assert.Equal(UnknownPayload, buffer.ToArray());
    }

    private static MemoryStream CreateMinimalPackage(OdfFormatInfo format)
    {
        var stream = new MemoryStream();
        using (OdfPackage package = OdfDocumentFactory.CreatePackage(stream, format.Kind, leaveOpen: true))
        {
            package.Save();
        }

        stream.Position = 0;
        return stream;
    }

    private static MemoryStream CreatePackageWithUnknownEntry(OdfFormatInfo format)
    {
        var stream = new MemoryStream();
        using (OdfPackage package = OdfDocumentFactory.CreatePackage(stream, format.Kind, leaveOpen: true))
        {
            package.WriteEntry("Interop/unknown.bin", UnknownPayload, "application/octet-stream");
            package.Save();
        }

        stream.Position = 0;
        return stream;
    }

    private static string FormatIssues(OdfValidationReport report)
    {
        return string.Join(
            ", ",
            report.Issues.Select(issue => issue.RuleId + ": " + issue.Message));
    }
}
