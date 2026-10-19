using System.Text;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests that the text field producers write elements valid for every supported version.
/// 測試文字欄位產生器在每個支援的版本寫出有效元素。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfFieldProducerConformanceTests
{
    private static readonly OdfVersion[] Versions = [OdfVersion.Odf12, OdfVersion.Odf13, OdfVersion.Odf14];

    private static readonly (string Element, Action<OdfParagraph> Add)[] Producers =
    [
        ("date", paragraph => paragraph.AddDateField()),
        ("time", paragraph => paragraph.AddTimeField()),
        ("author-name", paragraph => paragraph.AddAuthorField()),
        ("chapter", paragraph => paragraph.AddChapterField()),
        ("sequence", paragraph => paragraph.AddSequenceField("Figure")),
        ("sequence-ref", paragraph => paragraph.AddSequenceRefField("Figure")),
        ("bookmark-ref", paragraph => paragraph.AddBookmarkReferenceField("mark")),
        ("variable-set", paragraph => paragraph.AddVariableSetField("v", "value")),
        ("variable-get", paragraph => paragraph.AddVariableGetField("v")),
        ("database-display", paragraph => paragraph.AddDatabaseDisplayField("table", "column")),
        ("database-next", paragraph => paragraph.AddDatabaseNextField("table")),
        ("page-number", paragraph => paragraph.AddPageNumberField()),
        ("page-count", paragraph => paragraph.AddPageCountField())
    ];

    /// <summary>
    /// Every public producer writes a package that validates in each version and profile.
    /// 每個公開產生器都寫出在各版本與各設定檔皆可驗證的封裝。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void ProducersWriteValidFields(OdfVersion version)
    {
        List<string> failures = [];
        foreach ((string element, Action<OdfParagraph> add) in Producers)
        {
            using TextDocument document = TextDocument.Create();
            document.TargetVersion = version;
            OdfParagraph paragraph = document.AddParagraph("Field: ");
            add(paragraph);
            using MemoryStream output = new();
            document.SaveToStream(output);
            using OdfPackage package = OdfPackage.Open(new MemoryStream(output.ToArray()));
            foreach (OdfComplianceProfile profile in Profiles(version))
            {
                OdfValidationReport report = OdfPackageValidator.Validate(package, profile);
                if (!report.IsValid)
                    failures.Add(element + " " + string.Join(";", report.Issues.Select(issue => issue.RuleId + ":" + issue.Message)));
            }
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures.Select(failure => failure.Length > 400 ? failure[..400] : failure).Distinct()));
    }

    private static OdfComplianceProfile[] Profiles(OdfVersion version) => version switch
    {
        OdfVersion.Odf12 => [OdfComplianceProfiles.OasisOdf12Strict, OdfComplianceProfiles.OasisOdf12Extended],
        OdfVersion.Odf13 => [OdfComplianceProfiles.OasisOdf13Strict, OdfComplianceProfiles.OasisOdf13Extended],
        _ => [OdfComplianceProfiles.OasisOdf14Strict, OdfComplianceProfiles.OasisOdf14Extended]
    };
}
