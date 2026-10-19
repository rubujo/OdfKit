using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests preservation of metadata dates outside the CLR date range or fractional precision.
/// 測試超出 CLR 日期範圍或小數精度的中繼資料日期保存。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfMetadataDateValueConformanceTests
{
    /// <summary>
    /// Untyped metadata access preserves valid dates that cannot be represented exactly as DateTime.
    /// 未型別化中繼資料存取保留無法以 DateTime 精確表示的合法日期。
    /// </summary>
    [Theory]
    [InlineData("-0001-01-01")]
    [InlineData("12026-01-01")]
    [InlineData("12026-01-01T12:30:00+14:00")]
    [InlineData("2026-10-16T12:30:00.123456789Z")]
    [InlineData("2026-10-16T12:30:00.000000001Z")]
    public void UntypedDateAccessPreservesUnrepresentableValues(string lexical)
    {
        foreach (OdfVersion version in new[] { OdfVersion.Odf12, OdfVersion.Odf13, OdfVersion.Odf14 })
        {
            using TextDocument document = Document(version, lexical);
            using MemoryStream output = new();
            document.SaveToStream(output);
            using TextDocument loaded = TextDocument.Load(new MemoryStream(output.ToArray()));
            foreach (OdfComplianceProfile profile in Profiles(version))
            {
                OdfValidationReport report = OdfPackageValidator.Validate(loaded.Package, profile);
                Assert.True(report.IsValid, report.ToJson());
            }
            foreach (TextDocument item in new[] { document, loaded })
            {
                Assert.Equal(lexical, Assert.IsType<string>(item.FindCustomProperty("Date")));
                Assert.Equal(lexical, Assert.IsType<string>(item.GetAllCustomProperties()["Date"]));
                Assert.Equal(lexical, Property(item).TextContent);
            }
        }
    }

    /// <summary>
    /// Exactly representable metadata dates retain the existing DateTime result type.
    /// 可精確表示的中繼資料日期維持既有 DateTime 結果型別。
    /// </summary>
    [Theory]
    [InlineData("0001-01-01T00:00:00Z")]
    [InlineData("9999-12-31T23:59:59.9999999Z")]
    [InlineData("2026-10-16T12:30:00.1234567000Z")]
    public void RepresentableDatesKeepTheirTypedResults(string lexical)
    {
        using TextDocument document = Document(OdfVersion.Odf14, lexical);
        DateTime value = Assert.IsType<DateTime>(document.FindCustomProperty("Date"));
        Assert.Equal(value, Assert.IsType<DateTime>(document.GetAllCustomProperties()["Date"]));
        Assert.Equal(lexical, Property(document).TextContent);
        Assert.Equal(value, document.FindCustomProperty<DateTime>("Date"));
    }

    private static TextDocument Document(OdfVersion version, string lexical)
    {
        TextDocument document = TextDocument.Create();
        document.TargetVersion = version;
        document.SetCustomProperty("Date", new DateTime(2026, 10, 16, 12, 30, 0, DateTimeKind.Utc));
        Property(document).TextContent = lexical;
        return document;
    }

    private static OdfNode Property(TextDocument document)
        => document.MetaDom.Descendants().Single(node => node.NamespaceUri == OdfNamespaces.Meta && node.LocalName == "user-defined");

    private static OdfComplianceProfile[] Profiles(OdfVersion version) => version switch
    {
        OdfVersion.Odf12 => [OdfComplianceProfiles.OasisOdf12Strict, OdfComplianceProfiles.OasisOdf12Extended],
        OdfVersion.Odf13 => [OdfComplianceProfiles.OasisOdf13Strict, OdfComplianceProfiles.OasisOdf13Extended],
        _ => [OdfComplianceProfiles.OasisOdf14Strict, OdfComplianceProfiles.OasisOdf14Extended]
    };
}
