using System.Text;
using OdfKit.Compliance;
using OdfKit.Core;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests the ancestor constraints on native measure fields.
/// 測試原生尺寸標註欄位的祖先元素限制。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfMeasureFieldConformanceTests
{
    /// <summary>
    /// Measure fields require a native drawing measure ancestor.
    /// 尺寸標註欄位必須位於原生尺寸標註繪圖物件內。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void FieldOutsideMeasureObjectIsRejected(OdfVersion version)
    {
        foreach (OdfComplianceProfile profile in Profiles(version))
        {
            foreach (string body in new[]
            {
                "<text:p><text:measure text:kind='value'>12</text:measure></text:p>",
                "<text:p><draw:frame svg:width='1cm' svg:height='1cm'><draw:text-box><text:p><text:measure text:kind='unit'>cm</text:measure></text:p></draw:text-box></draw:frame></text:p>",
                MeasureObject() + "<text:p><text:measure text:kind='gap'> </text:measure></text:p>"
            })
            {
                OdfValidationReport report = ValidateContent(Wrap(version, body), version, profile);
                Assert.DoesNotContain(report.Issues, issue => issue.RuleId == "ODF3101");
                Assert.Contains(report.Issues, issue => issue.RuleId == "ODF3117" &&
                    issue.Severity == OdfIssueSeverity.Error && issue.PackagePath == "content.xml");
            }
        }
    }

    /// <summary>
    /// Valid fields preserve nested spans and alternate namespace prefixes.
    /// 合法欄位保留巢狀文字及替代命名空間前綴的支援。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void FieldWithinMeasureObjectIsValid(OdfVersion version)
    {
        foreach (OdfComplianceProfile profile in Profiles(version))
        {
            string xml = Wrap(version, MeasureObject()).Replace("draw:", "d:", StringComparison.Ordinal)
                .Replace("xmlns:draw=", "xmlns:d=", StringComparison.Ordinal);
            OdfValidationReport report = ValidateContent(xml, version, profile);
            Assert.True(report.IsValid, report.ToJson());
        }
    }

    /// <summary>
    /// Extended validation checks visible foreign content after applying its isolation rules.
    /// 擴充驗證在套用隔離規則後才檢查可見的外來內容。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void ExtendedForeignIsolationPrecedesAncestorChecks(OdfVersion version)
    {
        OdfComplianceProfile profile = Profiles(version)[^1];
        string[] ignored =
        [
            "<f:opaque><text:p><text:measure text:kind='value'>12</text:measure></text:p></f:opaque><text:p>visible</text:p>",
            "<text:p><f:measure>foreign</f:measure></text:p>"
        ];
        foreach (string body in ignored)
        {
            OdfValidationReport report = ValidateContent(Wrap(version, body), version, profile);
            Assert.True(report.IsValid, report.ToJson());
        }
        const string visible = "<text:p><f:inline><text:measure text:kind='value'>12</text:measure></f:inline></text:p>";
        Assert.Contains(ValidateContent(Wrap(version, visible), version, profile).Issues,
            issue => issue.RuleId == "ODF3117");
        const string foreignAncestor = "<text:p><f:measure><text:measure text:kind='value'>12</text:measure></f:measure></text:p>";
        Assert.Contains(ValidateContent(Wrap(version, foreignAncestor), version, profile).Issues,
            issue => issue.RuleId == "ODF3117");
    }

    /// <summary>
    /// Flat and embedded documents receive the same version-aware semantic checks.
    /// 單一 XML 與內嵌文件使用一致且依版本判斷的語意檢查。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void FlatAndEmbeddedDocumentsCheckMeasureScope(OdfVersion version)
    {
        const string body = "<text:p><text:measure text:kind='value'>12</text:measure></text:p>";
        string flat = Wrap(version, body).Replace("document-content", "document", StringComparison.Ordinal);
        flat = flat.Replace(" office:version=", " office:mimetype='application/vnd.oasis.opendocument.text' office:version=", StringComparison.Ordinal);
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(flat));
        OdfValidationReport flatReport = OdfValidator.Validate(input, "field.fodt", Profiles(version)[0]);
        Assert.Contains(flatReport.Issues, issue => issue.RuleId == "ODF3117");

        OdfValidationReport embeddedReport = ValidateContent(Wrap(OdfVersion.Odf14, "<text:p>outer</text:p>"),
            OdfVersion.Odf14, Profiles(OdfVersion.Odf14)[0], Wrap(version, body));
        Assert.Contains(embeddedReport.Issues, issue => issue.RuleId == "ODF3117" && issue.PackagePath == "Object1/content.xml");
    }

    /// <summary>
    /// Inline documents cannot borrow a drawing measure from their enclosing document.
    /// 內嵌 XML 文件不能借用外層文件的尺寸標註物件。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void InlineDocumentStartsItsOwnMeasureScope(OdfVersion version)
    {
        string InnerDocument(string body) =>
            $"<office:document office:version='{OdfVersionInfo.ToVersionString(version)}' office:mimetype='application/vnd.oasis.opendocument.text'><office:body><office:text>{body}</office:text></office:body></office:document>";
        string OuterDocument(string inner) => Wrap(version,
            $"<text:p><draw:measure svg:x1='0cm' svg:y1='0cm' svg:x2='1cm' svg:y2='1cm'><text:p><draw:frame svg:width='1cm' svg:height='1cm'><draw:object>{inner}</draw:object></draw:frame></text:p></draw:measure></text:p>");

        const string field = "<text:p><text:measure text:kind='value'>12</text:measure></text:p>";
        OdfComplianceProfile profile = Profiles(version)[0];
        OdfValidationReport invalid = ValidateContent(OuterDocument(InnerDocument(field)), version, profile);
        Assert.DoesNotContain(invalid.Issues, issue => issue.RuleId == "ODF3101");
        Assert.Contains(invalid.Issues, issue => issue.RuleId == "ODF3117");
        OdfValidationReport valid = ValidateContent(OuterDocument(InnerDocument(MeasureObject())), version, profile);
        Assert.True(valid.IsValid, valid.ToJson());
    }

    /// <summary>
    /// The new check is limited to the versions whose explicit restriction was reviewed.
    /// 新檢查僅套用於已核對明確限制的版本。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf10)]
    [InlineData(OdfVersion.Odf11)]
    public void EarlierVersionsDoNotInheritLaterProseRestrictions(OdfVersion version)
    {
        OdfComplianceProfile profile = version == OdfVersion.Odf10
            ? OdfComplianceProfiles.OasisOdf10 : OdfComplianceProfiles.OasisOdf11;
        const string field = "<text:p><text:measure text:kind='value'>12</text:measure></text:p>";
        Assert.DoesNotContain(ValidateContent(Wrap(version, field), version, profile).Issues,
            issue => issue.RuleId == "ODF3117");
    }

    private static string MeasureObject() =>
        "<text:p><draw:measure svg:x1='0cm' svg:y1='0cm' svg:x2='1cm' svg:y2='1cm'><text:p><text:span><text:measure text:kind='value'>12</text:measure></text:span><text:measure text:kind='unit'>cm</text:measure><text:measure text:kind='gap'> </text:measure></text:p></draw:measure></text:p>";

    private static string Wrap(OdfVersion version, string body) =>
        $"<office:document-content xmlns:office='{OdfNamespaces.Office}' xmlns:text='{OdfNamespaces.Text}' xmlns:draw='{OdfNamespaces.Draw}' xmlns:svg='{OdfNamespaces.Svg}' xmlns:f='urn:test:foreign' office:version='{OdfVersionInfo.ToVersionString(version)}'><office:body><office:text>{body}</office:text></office:body></office:document-content>";

    private static OdfValidationReport ValidateContent(string content, OdfVersion version, OdfComplianceProfile profile, string? embedded = null)
    {
        using var stream = new MemoryStream();
        using (OdfPackage writer = OdfDocumentFactory.CreatePackage(stream, OdfDocumentKind.Text, version, true))
        {
            writer.WriteEntry("content.xml", Encoding.UTF8.GetBytes(content), "text/xml");
            if (embedded is not null)
                writer.WriteEntry("Object1/content.xml", Encoding.UTF8.GetBytes(embedded), "text/xml");
            writer.Save();
        }
        stream.Position = 0;
        using OdfPackage package = OdfPackage.Open(stream);
        byte[] original = (byte[])package.ReadEntry("content.xml").Clone();
        OdfValidationReport report = OdfPackageValidator.Validate(package, profile);
        Assert.Equal(original, package.ReadEntry("content.xml"));
        return report;
    }

    private static OdfComplianceProfile[] Profiles(OdfVersion version) => version switch
    {
        OdfVersion.Odf12 => [OdfComplianceProfiles.OasisOdf12, OdfComplianceProfiles.OasisOdf12Strict, OdfComplianceProfiles.OasisOdf12Extended],
        OdfVersion.Odf13 => [OdfComplianceProfiles.OasisOdf13, OdfComplianceProfiles.OasisOdf13Strict, OdfComplianceProfiles.OasisOdf13Extended],
        _ => [OdfComplianceProfiles.OasisOdf14Strict, OdfComplianceProfiles.OasisOdf14Extended]
    };
}
