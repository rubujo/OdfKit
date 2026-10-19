using System.Text;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests author and sender field elements across the versions that define them.
/// 在各個定義作者與寄件人欄位的版本中測試這些欄位元素。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfAuthorSenderFieldConformanceTests
{
    private static readonly string[] Fields =
    [
        "author-initials", "author-name", "sender-city", "sender-company", "sender-country", "sender-email",
        "sender-fax", "sender-firstname", "sender-initials", "sender-lastname", "sender-phone-private",
        "sender-phone-work", "sender-position", "sender-postal-code", "sender-state-or-province",
        "sender-street", "sender-title"
    ];

    private static readonly string[] Parents = ["p", "h", "span", "a", "ruby-base", "meta", "meta-field"];
    private static readonly string[] Values = ["", " ", " \t\n ", "文字😀<&>"];

    /// <summary>
    /// Each field is usable in every documented parent and keeps its character data through both readers.
    /// 每個欄位可用於文件列出的所有父元素，且兩種讀取器都保留字元資料。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf11)]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void FieldsAreUsableInEveryDocumentedParent(OdfVersion version)
    {
        foreach (string field in Fields)
            foreach (string parent in version == OdfVersion.Odf11 ? Parents[..5] : Parents)
                foreach (string value in Values)
                {
                    XElement element = Field(field, value);
                    XElement document = Document(version, Wrap(parent, element));
                    AssertValid(version, document, parent);
                    AssertRoundTrip(version, document, field, value);
                }
    }

    /// <summary>
    /// The optional fixed attribute accepts boolean values and the field remains valid without it.
    /// 可選的 fixed 屬性接受布林值，沒有該屬性時欄位仍然有效。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf11)]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void FixedAttributeIsOptionalBoolean(OdfVersion version)
    {
        foreach (string field in Fields)
            foreach (string? fixedValue in new string?[] { null, "true", "false" })
            {
                XElement element = Field(field, "Value");
                if (fixedValue is not null)
                    element.SetAttributeValue(XName.Get("fixed", OdfNamespaces.Text), fixedValue);
                AssertValid(version, Document(version, Wrap("p", element)));
            }
    }

    /// <summary>
    /// Fields reject invalid fixed values, unknown native attributes, child elements and incorrect placement.
    /// 欄位拒絕無效的 fixed 值、未知原生屬性、子元素及錯誤位置。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf11)]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void FieldsRejectInvalidNativeStructure(OdfVersion version)
    {
        foreach (string field in Fields)
            foreach (int mutation in new[] { 0, 1, 2, 3 })
            {
                XElement element = Field(field, "Value");
                XElement document;
                if (mutation == 0)
                {
                    element.SetAttributeValue(XName.Get("fixed", OdfNamespaces.Text), "maybe");
                    document = Document(version, Wrap("p", element));
                }
                else if (mutation == 1)
                {
                    element.SetAttributeValue(XName.Get("bogus", OdfNamespaces.Text), "x");
                    document = Document(version, Wrap("p", element));
                }
                else if (mutation == 2)
                {
                    element.Add(new XElement(XName.Get("span", OdfNamespaces.Text), "child"));
                    document = Document(version, Wrap("p", element));
                }
                else
                    document = Document(version, element);
                AssertSchemaInvalid(version, document);
            }
    }

    /// <summary>
    /// Native fields in the wrong namespace do not satisfy the definitions and foreign look-alikes are not native fields.
    /// 錯誤命名空間的欄位不符合定義，外來的同名元素也不是原生欄位。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void ForeignLookAlikesAreNotNativeFields(OdfVersion version)
    {
        foreach (string field in Fields)
        {
            XElement foreign = new(XName.Get(field, OdfNamespaces.Office), "Value");
            AssertSchemaInvalid(version, Document(version, Wrap("p", foreign)));
        }
    }

    /// <summary>
    /// Loading and saving never refreshes field data, so fixed and non-fixed fields keep their stored values.
    /// 載入與儲存不會更新欄位資料，因此固定與非固定欄位都保留已儲存的值。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf11)]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void SavingKeepsStoredFieldValuesAndFixedFlags(OdfVersion version)
    {
        XNamespace text = OdfNamespaces.Text;
        XElement fixedParagraph = new(text + "p");
        XElement variableParagraph = new(text + "p");
        for (int index = 0; index < Fields.Length; index++)
        {
            XElement locked = Field(Fields[index], "Stored-" + index);
            locked.SetAttributeValue(text + "fixed", "true");
            fixedParagraph.Add(locked);
            XElement open = Field(Fields[index], "Stored-" + index);
            open.SetAttributeValue(text + "fixed", "false");
            variableParagraph.Add(open);
        }
        XElement source = Document(version, fixedParagraph);
        source.Descendants(XName.Get("text", OdfNamespaces.Office)).Single().Add(variableParagraph);
        using MemoryStream flat = new(Encoding.UTF8.GetBytes(source.ToString(SaveOptions.DisableFormatting)));
        using OdfDocument document = OdfDocument.Load(flat, "fields.fodt");
        using MemoryStream saved = new();
        document.SaveToStream(saved);
        using OdfPackage package = OdfPackage.Open(new MemoryStream(saved.ToArray()));
        XElement content = XElement.Parse(Encoding.UTF8.GetString(package.ReadEntry("content.xml")), LoadOptions.PreserveWhitespace);
        for (int index = 0; index < Fields.Length; index++)
        {
            XElement[] fields = content.Descendants(text + Fields[index]).ToArray();
            Assert.Equal(2, fields.Length);
            Assert.All(fields, field => Assert.Equal("Stored-" + index, field.Value));
            Assert.Equal(["true", "false"], fields.Select(field => (string?)field.Attribute(text + "fixed")));
        }
    }

    private static XElement Field(string field, string value) => new(XName.Get(field, OdfNamespaces.Text), value);

    private static XElement Wrap(string parent, XElement field)
    {
        XNamespace text = OdfNamespaces.Text;
        return parent switch
        {
            "p" => new XElement(text + "p", field),
            "h" => new XElement(text + "h", new XAttribute(text + "outline-level", "1"), field),
            "span" => new XElement(text + "p", new XElement(text + "span", field)),
            "a" => new XElement(text + "p", new XElement(text + "a", new XAttribute(XName.Get("type", OdfNamespaces.XLink), "simple"), new XAttribute(XName.Get("href", OdfNamespaces.XLink), "http://example.com/"), field)),
            "meta" => new XElement(text + "p", new XElement(text + "meta", field)),
            "meta-field" => new XElement(text + "p", new XElement(text + "meta-field", new XAttribute(XNamespace.Xml + "id", "meta-field-1"), field)),
            _ => new XElement(text + "p", new XElement(text + "ruby", new XElement(text + "ruby-base", field), new XElement(text + "ruby-text", "r")))
        };
    }

    private static XElement Document(OdfVersion version, XElement content)
    {
        XNamespace office = OdfNamespaces.Office;
        return new XElement(office + "document",
            new XAttribute(XNamespace.Xmlns + "o", office),
            new XAttribute(XNamespace.Xmlns + "t", OdfNamespaces.Text),
            new XAttribute(XNamespace.Xmlns + "x", OdfNamespaces.XLink),
            new XAttribute(office + "version", OdfVersionInfo.ToVersionString(version)),
            new XAttribute(office + "mimetype", "application/vnd.oasis.opendocument.text"),
            new XElement(office + "body", new XElement(office + "text", content)));
    }

    private static void AssertRoundTrip(OdfVersion version, XElement document, string field, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(document.ToString(SaveOptions.DisableFormatting));
        foreach (bool streamReader in new[] { false, true })
        {
            using MemoryStream input = new(bytes);
            OdfNode dom = streamReader ? OdfXmlReader.Parse(input) : OdfXmlReader.Parse(bytes);
            Assert.Equal(value, dom.Descendants().Single(node => node.NamespaceUri == OdfNamespaces.Text && node.LocalName == field).TextContent);
            using MemoryStream output = new();
            OdfXmlWriter.Write(dom, output);
            XElement saved = XElement.Parse(Encoding.UTF8.GetString(output.ToArray()), LoadOptions.PreserveWhitespace);
            Assert.Equal(value, saved.Descendants(XName.Get(field, OdfNamespaces.Text)).Single().Value);
            AssertValid(version, saved);
        }
    }

    private static OdfValidationReport Validate(XElement document, OdfComplianceProfile profile)
    {
        using MemoryStream input = new(Encoding.UTF8.GetBytes(document.ToString(SaveOptions.DisableFormatting)));
        return OdfValidator.Validate(input, "field.fodt", profile);
    }

    private static void AssertValid(OdfVersion version, XElement document, string context = "")
    {
        foreach (OdfComplianceProfile profile in Profiles(version))
        {
            OdfValidationReport report = Validate(document, profile);
            Assert.True(report.IsValid, context + " " + report.ToJson());
        }
    }

    private static void AssertSchemaInvalid(OdfVersion version, XElement document)
    {
        foreach (OdfComplianceProfile profile in Profiles(version))
            Assert.Contains(Validate(document, profile).Issues, issue => issue.RuleId == "ODF3101");
    }

    private static OdfComplianceProfile[] Profiles(OdfVersion version) => version switch
    {
        OdfVersion.Odf11 => [OdfComplianceProfiles.OasisOdf11],
        OdfVersion.Odf12 => [OdfComplianceProfiles.OasisOdf12Strict, OdfComplianceProfiles.OasisOdf12Extended],
        OdfVersion.Odf13 => [OdfComplianceProfiles.OasisOdf13Strict, OdfComplianceProfiles.OasisOdf13Extended],
        _ => [OdfComplianceProfiles.OasisOdf14Strict, OdfComplianceProfiles.OasisOdf14Extended]
    };
}
