using System.Text;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests form label associations and inherited list property datatypes.
/// 測試表單標籤關聯及清單屬性繼承的資料型別。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfFormConformanceTests
{
    /// <summary>
    /// Two labels cannot target the same control, including through comma-separated lists.
    /// 兩個標籤不能指向同一控制項，包含以逗號分隔清單參照的情況。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void MultipleLabelsForOneControlAreRejected(OdfVersion version)
    {
        foreach (OdfComplianceProfile profile in Profiles(version))
        {
            string controls = "<form:text xml:id='A'/><form:text xml:id='B'/>";
            string labels = Label("L1", "A, B") + Label("L2", " B ");
            OdfValidationReport report = Validate(Form(controls + labels), version, profile);
            Assert.DoesNotContain(report.Issues, issue => issue.RuleId == "ODF3101");
            Assert.Contains(report.Issues, issue => issue.RuleId == "ODF3124");
            string acrossForms = Form(controls + Label("L1", "A")) + Form(Label("L2", "A"));
            Assert.Contains(Validate(acrossForms, version, profile).Issues, issue => issue.RuleId == "ODF3124");
        }
    }

    /// <summary>
    /// One label can target several controls and control names need not be unique.
    /// 一個標籤可指向多個控制項，控制項名稱不必唯一。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void SharedLabelAndRepeatedNamesRemainValid(OdfVersion version)
    {
        foreach (OdfComplianceProfile profile in Profiles(version))
        {
            string controls = "<form:text xml:id='A' form:name='same'/><form:text xml:id='B' form:name='same'/>";
            foreach (string labels in new[] { Label("L1", "A,B,A"), Label("L1", "A") + Label("L2", "B") })
            {
                OdfValidationReport report = Validate(Form(controls + labels), version, profile);
                Assert.True(report.IsValid, report.ToJson());
            }
            OdfValidationReport frame = Validate(Form(controls + Label("L1", "A") + "<form:frame xml:id='F' form:for='A'/>"), version, profile);
            Assert.True(frame.IsValid, frame.ToJson());
        }
    }

    /// <summary>
    /// Foreign attributes cannot create native associations and ignored subtrees cannot add labels.
    /// 外來屬性不能建立原生關聯，忽略的子樹不能增加標籤。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void ForeignLabelsAreIsolatedBeforeAssociationChecks(OdfVersion version)
    {
        string controls = "<form:text xml:id='A'/>" + Label("L1", "A");
        string extra = "<form:fixed-text xml:id='L2' f:for='A'/><f:opaque>" + Label("L3", "A") + "</f:opaque>";
        OdfValidationReport report = Validate(Form(controls + extra), version, Profiles(version)[1]);
        Assert.True(report.IsValid, report.ToJson());
        string renamed = Form(controls + Label("L2", "A")).Replace("form:", "frm:", StringComparison.Ordinal);
        Assert.Contains(Validate(renamed, version, Profiles(version)[1]).Issues, issue => issue.RuleId == "ODF3124");
    }

    /// <summary>
    /// List values must match the parent's declared datatype, including empty and void lists.
    /// 清單值必須符合父元素宣告的資料型別，包含空清單及 void 清單。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void ListValueAttributesFollowParentType(OdfVersion version)
    {
        (string Type, string Value)[] cases =
        [
            ("float", "office:value='2.5'"), ("percentage", "office:value='0.5'"),
            ("currency", "office:value='12' office:currency='USD'"),
            ("date", "office:date-value='2026-10-16'"), ("time", "office:time-value='PT1H'"),
            ("boolean", "office:boolean-value='true'"), ("string", "office:string-value='text'")
        ];
        foreach (OdfComplianceProfile profile in Profiles(version))
        {
            foreach ((string type, string value) in cases)
            {
                string item = $"<form:list-value {value}/>";
                AssertValid(List(type, item + item), version, profile);
                AssertValid(List(type, ""), version, profile);
                AssertGrammarInvalid(List(type, "<form:list-value/>"), version, profile);
                string wrong = type == "string" ? "office:boolean-value='true'" : "office:string-value='wrong'";
                AssertGrammarInvalid(List(type, $"<form:list-value {wrong}/>"), version, profile);
                AssertGrammarInvalid(List(type, $"<form:list-value {value} office:value-type='{type}'/>"), version, profile);
            }
            AssertValid(List("void", ""), version, profile);
            // 正式 grammar 的 currency 分支允許省略貨幣符號，不將一般值型別表的敘述擴張成額外拒絕規則。
            AssertValid(List("currency", "<form:list-value office:value='12'/>"), version, profile);
            AssertGrammarInvalid(List("void", "<form:list-value office:string-value='not void'/>"), version, profile);
            AssertGrammarInvalid(List("boolean", "<form:list-value office:boolean-value='not a boolean'/>"), version, profile);
            AssertGrammarInvalid(List("string", "<form:list-value office:string-value='x'><text:p/></form:list-value>"), version, profile);
            AssertGrammarInvalid("<form:properties><form:list-value office:string-value='orphan'/></form:properties>", version, profile);
            AssertGrammarInvalid("<form:properties><form:list-property office:value-type='string'/></form:properties>", version, profile);
            AssertGrammarInvalid("<form:properties><form:list-property form:property-name='missing type'/></form:properties>", version, profile);
            AssertGrammarInvalid(List("date", "<form:list-value office:date-value='not a date'/>"), version, profile);
            AssertGrammarInvalid(List("time", "<form:list-value office:time-value='not a duration'/>"), version, profile);
        }
    }

    /// <summary>
    /// Flat documents use the same association checks and separate XML entries have independent IDs.
    /// 單一 XML 文件使用相同關聯檢查，不同 XML 項目的識別碼互相獨立。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void FlatAndEmbeddedFormsKeepXmlFileBoundaries(OdfVersion version)
    {
        string controls = "<form:text xml:id='A'/>" + Label("L1", "A");
        string forms = Form(controls);
        string flat = Content(forms, version).Replace("document-content", "document", StringComparison.Ordinal)
            .Replace(" office:version=", " office:mimetype='application/vnd.oasis.opendocument.text' office:version=", StringComparison.Ordinal);
        foreach (OdfComplianceProfile profile in Profiles(version))
        {
            using var input = new MemoryStream(Encoding.UTF8.GetBytes(flat));
            Assert.True(OdfValidator.Validate(input, "forms.fodt", profile).IsValid);
            OdfValidationReport separate = Validate(forms, version, profile, Content(forms, version));
            Assert.True(separate.IsValid, separate.ToJson());
            string invalidFlat = flat.Replace("</form:form>", Label("L2", "A") + "</form:form>", StringComparison.Ordinal);
            using var invalidInput = new MemoryStream(Encoding.UTF8.GetBytes(invalidFlat));
            Assert.Contains(OdfValidator.Validate(invalidInput, "forms.fodt", profile).Issues, issue => issue.RuleId == "ODF3124");
            OdfValidationReport invalidEmbedded = Validate(forms, version, profile, Content(Form(controls + Label("L2", "A")), version));
            Assert.Contains(invalidEmbedded.Issues, issue => issue.RuleId == "ODF3124" && issue.PackagePath == "Object1/content.xml");
        }
    }

    private static void AssertValid(string properties, OdfVersion version, OdfComplianceProfile profile)
    {
        OdfValidationReport report = Validate(Form(properties), version, profile);
        Assert.True(report.IsValid, report.ToJson());
    }

    private static void AssertGrammarInvalid(string properties, OdfVersion version, OdfComplianceProfile profile) =>
        Assert.Contains(Validate(Form(properties), version, profile).Issues, issue => issue.RuleId == "ODF3101");

    private static string List(string type, string children) =>
        $"<form:properties><form:list-property form:property-name='Example' office:value-type='{type}'>{children}</form:list-property></form:properties>";

    private static string Label(string id, string targets) => $"<form:fixed-text xml:id='{id}' form:for='{targets}'/>";
    private static string Form(string children) => $"<form:form>{children}</form:form>";

    private static string Content(string forms, OdfVersion version) =>
        $"<office:document-content xmlns:office='{OdfNamespaces.Office}' xmlns:text='{OdfNamespaces.Text}' xmlns:form='{OdfNamespaces.Form}' xmlns:frm='{OdfNamespaces.Form}' xmlns:f='urn:test:foreign' office:version='{OdfVersionInfo.ToVersionString(version)}'><office:body><office:text><office:forms>{forms}</office:forms><text:p/></office:text></office:body></office:document-content>";

    private static OdfValidationReport Validate(string forms, OdfVersion version, OdfComplianceProfile profile, string? embedded = null)
    {
        string xml = Content(forms, version);
        using var stream = new MemoryStream();
        using (OdfPackage writer = OdfDocumentFactory.CreatePackage(stream, OdfDocumentKind.Text, version, true))
        {
            writer.WriteEntry("content.xml", Encoding.UTF8.GetBytes(xml), "text/xml");
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
        OdfVersion.Odf12 => [OdfComplianceProfiles.OasisOdf12Strict, OdfComplianceProfiles.OasisOdf12Extended],
        OdfVersion.Odf13 => [OdfComplianceProfiles.OasisOdf13Strict, OdfComplianceProfiles.OasisOdf13Extended],
        _ => [OdfComplianceProfiles.OasisOdf14Strict, OdfComplianceProfiles.OasisOdf14Extended]
    };
}
