using System.Text;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Database;
using OdfKit.DOM;
using OdfKit.Spreadsheet;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests schema-conforming form designer output and embedded database form persistence.
/// 測試表單設計器的原生結構及資料庫內嵌表單保存。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfDatabaseFormProducerConformanceTests
{
    private static readonly string[] ItemLabels = ["First", "Second"];
    /// <summary>
    /// Every supported control produces a document accepted by the official schema.
    /// 每個支援的控制項均產生可通過官方結構描述的文件。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf10)]
    [InlineData(OdfVersion.Odf11)]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void SupportedControlsProduceNativeDocuments(OdfVersion version)
    {
        List<string> failures = [];
        foreach (string kind in Kinds)
        {
            using TextDocument document = TextDocument.Create();
            document.TargetVersion = version;
            document.AddParagraph("Existing content");
            Add(new OdfDatabaseFormDesigner(document), kind);
            byte[] bytes = Save(document);
            using OdfPackage package = OdfPackage.Open(new MemoryStream(bytes));
            foreach (OdfComplianceProfile profile in Profiles(version))
            {
                OdfValidationReport report = OdfPackageValidator.Validate(package, profile);
                if (!report.IsValid)
                    failures.Add(kind + ": " + report.ToJson());
            }
        }
        Assert.Empty(failures);
    }

    /// <summary>
    /// Forms precede body paragraphs and generated IDs identify the native control consistently.
    /// 表單位於本文段落之前，產生的識別碼一致指向原生控制項。
    /// </summary>
    [Fact]
    public void ContainersValuesAndReferencesUseNativeNamespaces()
    {
        using TextDocument document = TextDocument.Create();
        document.AddParagraph("Existing content");
        var designer = new OdfDatabaseFormDesigner(document);
        foreach (string kind in Kinds)
            Add(designer, kind);
        XElement xml = Content(Save(document));
        XNamespace office = OdfNamespaces.Office;
        XNamespace form = OdfNamespaces.Form;
        XElement text = xml.Element(office + "body")!.Element(office + "text")!;
        Assert.Equal(office + "forms", text.Elements().First().Name);
        Assert.Empty(text.Elements(form + "form"));
        XElement[] controls = text.Element(office + "forms")!.Element(form + "form")!.Elements().ToArray();
        Assert.Equal(Kinds.Length, controls.Length);
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (XElement control in controls)
        {
            string id = control.Attribute(XNamespace.Xml + "id")!.Value;
            Assert.True(ids.Add(id));
            Assert.Equal(id, control.Attribute(form + "id")!.Value);
        }
        foreach (XElement property in xml.Descendants(form + "property"))
        {
            Assert.NotNull(property.Attribute(office + "string-value"));
            Assert.Null(property.Attribute(form + "string-value"));
        }
        foreach (XElement shape in xml.Descendants(XName.Get("control", OdfNamespaces.Draw)))
            Assert.Contains(shape.Attribute(XName.Get("control", OdfNamespaces.Draw))!.Value, ids);
    }

    /// <summary>
    /// Reopened documents and interleaved designers cannot reuse existing XML IDs.
    /// 重新開啟文件及交錯使用設計器時不重用既有 XML 識別碼。
    /// </summary>
    [Fact]
    public void ReopenedAndInterleavedDesignersKeepIdsUnique()
    {
        using TextDocument document = TextDocument.Create();
        var first = new OdfDatabaseFormDesigner(document);
        var second = new OdfDatabaseFormDesigner(document);
        first.AddTextBox("A", "A");
        second.AddTextBox("B", "B");
        first.AddTextBox("C", "C");
        using TextDocument loaded = TextDocument.Load(new MemoryStream(Save(document)));
        new OdfDatabaseFormDesigner(loaded).AddTextBox("D", "D");
        XElement xml = Content(Save(loaded));
        string[] ids = xml.Descendants().Attributes(XNamespace.Xml + "id").Select(attribute => attribute.Value).ToArray();
        Assert.Equal(4, ids.Length);
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        AssertValid(Save(loaded), OdfVersion.Odf14);
    }

    /// <summary>
    /// Database documents persist designed controls in tracked embedded text documents.
    /// 資料庫文件將設計的控制項保存於追蹤的內嵌文字文件。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public async Task DatabaseFormsPersistInNativeEmbeddedDocuments(OdfVersion version)
    {
        using DatabaseDocument document = DatabaseDocument.Create();
        document.TargetVersion = version;
        var designer = new OdfDatabaseFormDesigner(document);
        designer.AddTextBox("Customer", "Customer", "A & B");
        designer.AddGroupBox("Group", "Group");
        OdfDatabaseFormInfo info = Assert.Single(document.Forms);
        Assert.NotNull(info.Href);
        using var stream = new MemoryStream();
        await document.SaveToStreamAsync(stream, TestContext.Current.CancellationToken);
        byte[] bytes = stream.ToArray();
        AssertValid(bytes, version);
        using OdfPackage package = OdfPackage.Open(new MemoryStream(bytes));
        XElement root = XElement.Parse(Encoding.UTF8.GetString(package.ReadEntry("content.xml")));
        XNamespace office = OdfNamespaces.Office;
        Assert.Null(root.Element(office + "body")!.Element(office + "text"));
        string subpath = info.Href!.TrimEnd('/') + "/";
        XElement embedded = XElement.Parse(Encoding.UTF8.GetString(package.ReadEntry(subpath + "content.xml")));
        Assert.Equal(OdfVersionInfo.ToVersionString(version), embedded.Attribute(office + "version")!.Value);
        Assert.Contains(embedded.Descendants(XName.Get("property", OdfNamespaces.Form)),
            property => property.Attribute(office + "string-value")?.Value == "A & B");
    }

    /// <summary>
    /// Existing database components and orphan package entries are preserved when allocating a form.
    /// 配置表單時保留既有資料庫元件及孤立封裝項目。
    /// </summary>
    [Fact]
    public void DatabaseFormCreationPreservesOccupiedNamesAndPaths()
    {
        using DatabaseDocument document = DatabaseDocument.Create();
        document.AddForm("Standard", "https://example.invalid/existing-form");
        byte[] original = Encoding.UTF8.GetBytes("untouched");
        document.Package.WriteEntry("Forms/Standard2/custom.xml", original, "text/xml");
        new OdfDatabaseFormDesigner(document).AddTextBox("A", "A");
        OdfDatabaseFormInfo created = Assert.Single(document.Forms, form => form.Name != "Standard");
        Assert.Equal("Standard3", created.Name);
        Assert.Equal("Forms/Standard3", created.Href);
        using OdfPackage package = OdfPackage.Open(new MemoryStream(Save(document)));
        Assert.Equal(original, package.ReadEntry("Forms/Standard2/custom.xml"));
    }

    /// <summary>
    /// Unsupported document kinds are rejected before their content tree is changed.
    /// 不支援的文件種類會在內容樹變更之前被拒絕。
    /// </summary>
    [Fact]
    public void UnsupportedDocumentsRemainUnmodified()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        string before = Xml(document.ContentDom);
        Assert.Throws<ArgumentException>(() => new OdfDatabaseFormDesigner(document));
        Assert.Equal(before, Xml(document.ContentDom));
    }

    /// <summary>
    /// Numeric special values and time boundaries retain XML Schema lexical representations.
    /// 特殊數值與時間邊界保留 XML Schema 的詞法表示。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf10)]
    [InlineData(OdfVersion.Odf11)]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void TypedValuesUseSchemaLexicalForms(OdfVersion version)
    {
        using TextDocument document = TextDocument.Create();
        document.TargetVersion = version;
        var designer = new OdfDatabaseFormDesigner(document);
        Assert.Equal("INF", designer.AddNumericField("Infinity", "", double.PositiveInfinity).GetAttribute("value", OdfNamespaces.Form));
        Assert.Equal("-INF", designer.AddNumericField("NegativeInfinity", "", double.NegativeInfinity).GetAttribute("value", OdfNamespaces.Form));
        Assert.Equal("NaN", designer.AddNumericField("NaN", "", double.NaN).GetAttribute("value", OdfNamespaces.Form));
        Assert.Equal("24:00:00", designer.AddTimeField("EndOfDay", "", TimeSpan.FromDays(1)).GetAttribute("value", OdfNamespaces.Form));
        Assert.Equal("00:00:00.0000001", designer.AddTimeField("Fraction", "", TimeSpan.FromTicks(1)).GetAttribute("value", OdfNamespaces.Form));
        designer.AddDateField("MinimumDate", "", DateTime.MinValue);
        designer.AddDateField("MaximumDate", "", DateTime.MaxValue);
        AssertValid(Save(document), version);
    }

    /// <summary>
    /// Time values outside one day are rejected without changing the content tree.
    /// 超出一天範圍的時間值會被拒絕，且不變更內容樹。
    /// </summary>
    [Fact]
    public void InvalidTimesDoNotAddPartialControls()
    {
        using TextDocument document = TextDocument.Create();
        var designer = new OdfDatabaseFormDesigner(document);
        string before = Xml(document.ContentDom);
        foreach (TimeSpan value in new[] { TimeSpan.FromTicks(-1), TimeSpan.FromDays(1).Add(TimeSpan.FromTicks(1)), TimeSpan.MinValue, TimeSpan.MaxValue })
            Assert.Throws<ArgumentOutOfRangeException>(() => designer.AddTimeField("Time", "", value));
        Assert.Equal(before, Xml(document.ContentDom));
    }

    /// <summary>
    /// Existing IDs are reserved after XML Schema whitespace normalization.
    /// 既有識別碼會在 XML Schema 空白正規化後保留。
    /// </summary>
    [Fact]
    public void ExistingNormalizedIdsAreNotReused()
    {
        using TextDocument document = TextDocument.Create();
        var paragraph = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
        paragraph.SetAttribute("id", OdfNamespaces.Xml, " control1 ", "xml");
        paragraph.TextContent = "Existing content";
        document.BodyTextRoot.AppendChild(paragraph);
        OdfNode control = new OdfDatabaseFormDesigner(document).AddTextBox("A", "A");
        Assert.Equal("control2", control.GetAttribute("id", OdfNamespaces.Xml));
        AssertValid(Save(document), OdfVersion.Odf14);
    }

    /// <summary>
    /// Text defaults and item labels use the attributes defined for native controls.
    /// 文字預設值及項目標籤使用原生控制項定義的屬性。
    /// </summary>
    [Fact]
    public void TextDefaultsAndItemLabelsUseNativeAttributes()
    {
        using TextDocument document = TextDocument.Create();
        var designer = new OdfDatabaseFormDesigner(document);
        OdfNode text = designer.AddTextBox("Text", "Label", "A & B");
        Assert.Equal("A & B", text.GetAttribute("value", OdfNamespaces.Form));
        Assert.Equal("A & B", text.GetAttribute("current-value", OdfNamespaces.Form));
        OdfNode empty = designer.AddTextBox("Empty", "Label", "");
        Assert.Equal("", empty.GetAttribute("value", OdfNamespaces.Form));
        OdfNode list = designer.AddListBox("List", "Label", ["First", "Second"]);
        OdfNode combo = designer.AddComboBox("Combo", "Label", ["First", "Second"]);
        foreach (OdfNode control in new[] { list, combo })
        {
            OdfNode[] items = control.Children.Where(node => node.LocalName is "item" or "option").ToArray();
            Assert.Equal(ItemLabels, items.Select(node => node.GetAttribute("label", OdfNamespaces.Form)));
        }
        AssertValid(Save(document), OdfVersion.Odf14);
    }

    private static readonly string[] Kinds = ["text", "checkbox", "listbox", "button", "label", "radio", "combobox", "number", "date", "time", "group"];
    private static OdfNode Add(OdfDatabaseFormDesigner designer, string kind) => kind switch
    {
        "text" => designer.AddTextBox(kind, "Label", "Hello"),
        "checkbox" => designer.AddCheckBox(kind, "Label", true),
        "listbox" => designer.AddListBox(kind, "Label", ["A", "B"]),
        "button" => designer.AddButton(kind, "Label", "submit"),
        "label" => designer.AddLabel(kind, "Label"),
        "radio" => designer.AddRadioButton(kind, "Label", true),
        "combobox" => designer.AddComboBox(kind, "Label", ["A", "B"]),
        "number" => designer.AddNumericField(kind, "Label", 123.5),
        "date" => designer.AddDateField(kind, "Label", new DateTime(2026, 10, 16)),
        "time" => designer.AddTimeField(kind, "Label", new TimeSpan(13, 30, 0)),
        "group" => designer.AddGroupBox(kind, "Label"),
        _ => throw new InvalidOperationException()
    };
    private static byte[] Save(OdfDocument document)
    {
        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        return stream.ToArray();
    }
    private static XElement Content(byte[] bytes)
    {
        using OdfPackage package = OdfPackage.Open(new MemoryStream(bytes));
        return XElement.Parse(Encoding.UTF8.GetString(package.ReadEntry("content.xml")));
    }
    private static string Xml(OdfNode node)
    {
        using var stream = new MemoryStream();
        OdfXmlWriter.Write(node, stream);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
    private static void AssertValid(byte[] bytes, OdfVersion version)
    {
        using OdfPackage package = OdfPackage.Open(new MemoryStream(bytes));
        foreach (OdfComplianceProfile profile in Profiles(version))
        {
            OdfValidationReport report = OdfPackageValidator.Validate(package, profile);
            Assert.True(report.IsValid, report.ToJson());
        }
    }
    private static OdfComplianceProfile[] Profiles(OdfVersion version) => version switch
    {
        OdfVersion.Odf10 => [OdfComplianceProfiles.OasisOdf10],
        OdfVersion.Odf11 => [OdfComplianceProfiles.OasisOdf11],
        OdfVersion.Odf12 => [OdfComplianceProfiles.OasisOdf12Strict, OdfComplianceProfiles.OasisOdf12Extended],
        OdfVersion.Odf13 => [OdfComplianceProfiles.OasisOdf13Strict, OdfComplianceProfiles.OasisOdf13Extended],
        _ => [OdfComplianceProfiles.OasisOdf14Strict, OdfComplianceProfiles.OasisOdf14Extended]
    };
}
