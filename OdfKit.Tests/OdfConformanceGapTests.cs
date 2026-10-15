using System.Text;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Chart;
using OdfKit.Image;
using OdfKit.Database;
using OdfKit.Styles;
using System.Xml.Linq;
using Xunit;

namespace OdfKit.Tests;

[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfConformanceGapTests
{
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void ForeignElementXmlIdsStillRequireNcNames(OdfVersion version)
    {
        string xml = $"<office:document-content xmlns:office='{OdfNamespaces.Office}' xmlns:text='{OdfNamespaces.Text}' xmlns:f='urn:test:foreign' office:version='{OdfVersionInfo.ToVersionString(version)}'><office:body><office:text><f:opaque xml:id='bad:id'/><text:p>Text</text:p></office:text></office:body></office:document-content>";
        Assert.Contains(ValidateContent(xml, OdfDocumentKind.Text, version).Issues,
            issue => issue.RuleId == "ODF3105" && issue.PackagePath == "content.xml");
        Assert.DoesNotContain(ValidateContent(xml.Replace("bad:id", " valid ", StringComparison.Ordinal), OdfDocumentKind.Text, version).Issues,
            issue => issue.RuleId == "ODF3105");
    }

    [Fact]
    public void DatabaseTableCommandsUseIsolatedSdkMetadataAndMigrateOnUpdate()
    {
        using DatabaseDocument database = DatabaseDocument.Create();
        database.AddTable("Customers", "SELECT * FROM Customers");
        var table = Assert.Single(database.GetTables());
        Assert.Equal("SELECT * FROM Customers", table.Command);
        using var stream = new MemoryStream();
        database.SaveToStream(stream);
        stream.Position = 0;
        using OdfPackage package = OdfPackage.Open(stream);
        OdfValidationReport extended = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Extended);
        Assert.True(extended.IsValid, extended.ToJson());
        OdfValidationReport strict = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);
        Assert.Contains(strict.Issues, issue => issue.RuleId == "ODF3101");

        var node = database.AddTable("Legacy");
        node.SetAttribute("command", OdfNamespaces.Database, "old", "db");
        Assert.Equal("old", database.FindTable("Legacy")?.Command);
        Assert.True(database.UpdateTable("Legacy", "new"));
        Assert.Null(node.GetAttribute("command", OdfNamespaces.Database));
        Assert.Equal("new", database.FindTable("Legacy")?.Command);
        Assert.True(database.UpdateTable("Legacy", null));
        Assert.Null(database.FindTable("Legacy")?.Command);
    }

    [Theory]
    [InlineData("")]
    [InlineData("AQID")]
    public void FlatBinaryDataDoesNotConsumeFollowingFrameMetadata(string base64)
    {
        string xml = $"<office:document xmlns:office=\"{OdfNamespaces.Office}\" xmlns:draw=\"{OdfNamespaces.Draw}\" xmlns:svg=\"{OdfNamespaces.Svg}\" office:version=\"1.4\" office:mimetype=\"application/vnd.oasis.opendocument.image\"><office:body><office:image><draw:frame><draw:image><office:binary-data>{base64}</office:binary-data></draw:image><svg:title>Title</svg:title><svg:desc>Description</svg:desc></draw:frame></office:image></office:body></office:document>";
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        using FlatImageDocument image = FlatImageDocument.Load(input);
        OdfImageFrameInfo frame = Assert.Single(image.GetImageFrames());
        Assert.Equal("Title", frame.Title);
        Assert.Equal("Description", frame.Description);
        Assert.Equal(Convert.FromBase64String(base64), image.GetImageBytes());
    }

    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void QNameDatatypeUsesCurrentAndInheritedNamespaceDeclarations(OdfVersion version)
    {
        using OdfChartDocument chart = OdfChartDocument.Create();
        using var stream = new MemoryStream();
        chart.SaveToStream(stream);
        stream.Position = 0;
        using OdfPackage package = OdfPackage.Open(stream);
        using Stream content = package.GetEntryStream("content.xml");
        XDocument document = XDocument.Load(content);
        XElement element = document.Descendants(XName.Get("chart", OdfNamespaces.Chart)).Single();
        OdfSchemaSet schema = OdfSchemaRegistry.GetSchema(version);
        element.SetAttributeValue(XName.Get("class", OdfNamespaces.Chart), "custom:bar");
        Assert.False(OdfSchemaPatternValidator.ValidateElement(element, schema, "chart-chart").IsMatch);
        document.Root!.SetAttributeValue(XNamespace.Xmlns + "custom", "urn:test:chart");
        Assert.True(OdfSchemaPatternValidator.ValidateElement(element, schema, "chart-chart").IsMatch);
        element.SetAttributeValue(XName.Get("class", OdfNamespaces.Chart), " \tcustom:bar\n ");
        Assert.True(OdfSchemaPatternValidator.ValidateElement(element, schema, "chart-chart").IsMatch);

        XElement plot = element.Element(XName.Get("plot-area", OdfNamespaces.Chart))!;
        XElement series = new(XName.Get("series", OdfNamespaces.Chart),
            new XAttribute(XName.Get("class", OdfNamespaces.Chart), "missing:line"));
        plot.Add(series);
        Assert.False(OdfSchemaPatternValidator.ValidateElement(element, schema, "chart-chart").IsMatch);
        series.SetAttributeValue(XNamespace.Xmlns + "missing", "urn:test:series");
        Assert.True(OdfSchemaPatternValidator.ValidateElement(element, schema, "chart-chart").IsMatch);
    }

    [Theory]
    [InlineData(OdfVersion.Odf12, "formula", OdfDocumentKind.Formula)]
    [InlineData(OdfVersion.Odf13, "formula", OdfDocumentKind.Formula)]
    [InlineData(OdfVersion.Odf14, "formula", OdfDocumentKind.Formula)]
    [InlineData(OdfVersion.Odf12, "database", OdfDocumentKind.Database)]
    [InlineData(OdfVersion.Odf13, "database", OdfDocumentKind.Database)]
    [InlineData(OdfVersion.Odf14, "database", OdfDocumentKind.Database)]
    public void BodyChecksDoNotExemptEnvelopeFromSchemaValidation(OdfVersion version, string body, OdfDocumentKind kind)
    {
        string bodyContent = body == "database"
            ? $"<office:database><db:data-source xmlns:db=\"{OdfNamespaces.Database}\"><db:connection-data><db:connection-resource xmlns:xlink=\"{OdfNamespaces.XLink}\" xlink:type=\"simple\" xlink:href=\"\"/></db:connection-data></db:data-source></office:database>"
            : "<office:formula/>";
        string xml = $"<office:document-content xmlns:office=\"{OdfNamespaces.Office}\" office:version=\"{OdfVersionInfo.ToVersionString(version)}\"><office:body>{bodyContent}</office:body></office:document-content>";
        OdfValidationReport valid = ValidateContent(xml, kind, version);
        Assert.True(valid.IsValid, string.Join("; ", valid.Issues.Select(issue => issue.Message)));
        if (body == "formula")
        {
            Assert.Contains(valid.Issues, issue => issue.RuleId == "ODF3103" && issue.PackagePath == "content.xml");
        }
        else
        {
            Assert.DoesNotContain(valid.Issues, issue => issue.RuleId == "ODF3103");
            string missingDataSource = xml.Replace(bodyContent, "<office:database/>", StringComparison.Ordinal);
            Assert.Contains(ValidateContent(missingDataSource, kind, version).Issues,
                issue => issue.RuleId == "ODF3101" && issue.PackagePath == "content.xml");
            string invalidChild = xml.Replace("</office:database>", "<office:text/></office:database>", StringComparison.Ordinal);
            Assert.Contains(ValidateContent(invalidChild, kind, version).Issues,
                issue => issue.RuleId == "ODF3101" && issue.PackagePath == "content.xml");
        }

        string invalidAttribute = xml.Replace("<office:body>", "<office:body office:version=\"1.4\">", StringComparison.Ordinal);
        OdfValidationReport invalid = ValidateContent(invalidAttribute, kind, version);
        Assert.Contains(invalid.Issues, issue => issue.RuleId == "ODF3101" && issue.Severity == OdfIssueSeverity.Error);

        string invalidSibling = xml.Replace("<office:body>", "<office:text/><office:body>", StringComparison.Ordinal);
        Assert.Contains(ValidateContent(invalidSibling, kind, version).Issues,
            issue => issue.RuleId == "ODF3101" && issue.PackagePath == "content.xml");
    }

    [Fact]
    public void BareMathmlExemptionIsVisibleInValidationReport()
    {
        OdfValidationReport report = ValidateContent("<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mi>x</mi></math>",
            OdfDocumentKind.Formula, OdfVersion.Odf14);
        Assert.Contains(report.Issues, issue => issue.RuleId == "ODF3103" && issue.Severity == OdfIssueSeverity.Warning);
    }

    [Theory]
    [InlineData(OdfVersion.Odf10)]
    [InlineData(OdfVersion.Odf11)]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void NativeSchemaDoesNotRepairInvalidVersionBeforeMatching(OdfVersion version)
    {
        // 主文件決定 schema，另一串流的非法 version 不可先改寫成合法值才驗證。
        string xml = $"<office:document-content xmlns:office=\"{OdfNamespaces.Office}\" office:version=\"{OdfVersionInfo.ToVersionString(version)}\"><office:body><office:text/></office:body></office:document-content>";
        string styles = $"<office:document-styles xmlns:office=\"{OdfNamespaces.Office}\" office:version=\"bogus\"><office:styles/></office:document-styles>";
        OdfValidationReport report = ValidateContent(xml, OdfDocumentKind.Text, version, styles);
        Assert.Contains(report.Issues, issue => issue.RuleId == "ODF0401" && issue.PackagePath == "styles.xml");
        if (version is not (OdfVersion.Odf10 or OdfVersion.Odf11))
        {
            Assert.Contains(report.Issues, issue => issue.RuleId == "ODF3101" && issue.PackagePath == "styles.xml");
        }
    }

    [Theory]
    [InlineData(OdfVersion.Odf10)]
    [InlineData(OdfVersion.Odf11)]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void ProducedManifestUsesOnlyAttributesDefinedBySelectedVersion(OdfVersion version)
    {
        string xml = $"<office:document-content xmlns:office=\"{OdfNamespaces.Office}\" office:version=\"{OdfVersionInfo.ToVersionString(version)}\"><office:body><office:text/></office:body></office:document-content>";
        OdfValidationReport report = ValidateContent(xml, OdfDocumentKind.Text, version);
        Assert.True(report.IsValid, string.Join("; ", report.Issues.Select(issue => issue.Message)));
        Assert.DoesNotContain(report.Issues, issue => issue.PackagePath == "META-INF/manifest.xml");
    }

    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void XmlIdsAreUniqueWithinEachStreamAndCanRepeatAcrossEmbeddedDocuments(OdfVersion version)
    {
        string xml = $"<office:document-content xmlns:office=\"{OdfNamespaces.Office}\" xmlns:text=\"{OdfNamespaces.Text}\" office:version=\"{OdfVersionInfo.ToVersionString(version)}\"><office:body><office:text><text:p xml:id=\"first\">A</text:p><text:p xml:id=\"second\">B</text:p></office:text></office:body></office:document-content>";
        OdfValidationReport valid = ValidateContent(xml, OdfDocumentKind.Text, version, embedded: xml);
        Assert.True(valid.IsValid, string.Join("; ", valid.Issues.Select(issue => issue.Message)));
        string duplicate = xml.Replace("xml:id=\"second\"", "xml:id=\"first\"", StringComparison.Ordinal);
        Assert.Contains(ValidateContent(duplicate, OdfDocumentKind.Text, version).Issues,
            issue => issue.RuleId == "ODF3104" && issue.PackagePath == "content.xml");
        Assert.Contains(ValidateContent(xml, OdfDocumentKind.Text, version, embedded: duplicate).Issues,
            issue => issue.RuleId == "ODF3104" && issue.PackagePath == "Object1/content.xml");
    }

    [Theory]
    [InlineData("1.0", true)]
    [InlineData("1.1", true)]
    [InlineData("1.2", true)]
    [InlineData("1.3", true)]
    [InlineData("1.4", true)]
    [InlineData("bogus", false)]
    public void EmbeddedDocumentsUseTheirOwnDeclaredSchemaVersion(string embeddedVersion, bool expected)
    {
        string xml = $"<office:document-content xmlns:office=\"{OdfNamespaces.Office}\" office:version=\"1.4\"><office:body><office:text/></office:body></office:document-content>";
        string embedded = xml.Replace("office:version=\"1.4\"", $"office:version=\"{embeddedVersion}\"", StringComparison.Ordinal);
        OdfValidationReport report = ValidateContent(xml, OdfDocumentKind.Text, OdfVersion.Odf14, embedded: embedded);
        Assert.Equal(expected, report.IsValid);
        if (!expected)
        {
            Assert.Contains(report.Issues, issue => issue.RuleId == "ODF0401" && issue.PackagePath == "Object1/content.xml");
        }
    }

    [Theory]
    [InlineData(OdfVersion.Odf10)]
    [InlineData(OdfVersion.Odf11)]
    public async Task LegacyPackageVersionSurvivesSynchronousAndAsynchronousLoadAndResave(OdfVersion version)
    {
        using var original = new MemoryStream();
        using (OdfPackage writer = OdfDocumentFactory.CreatePackage(original, OdfDocumentKind.Text, version, true))
        {
            writer.Save();
        }
        original.Position = 0;
        using (OdfPackage loaded = OdfPackage.Open(original, true))
        {
            Assert.Equal(version, loaded.Version);
            using var saved = new MemoryStream();
            loaded.SaveToStream(saved);
            saved.Position = 0;
            using OdfPackage reloaded = OdfPackage.Open(saved);
            Assert.Equal(version, reloaded.Version);
            Assert.True(OdfPackageValidator.Validate(reloaded, version == OdfVersion.Odf10
                ? OdfComplianceProfiles.OasisOdf10 : OdfComplianceProfiles.OasisOdf11).IsValid);
        }
        original.Position = 0;
        await using OdfPackage asyncLoaded = await OdfPackage.OpenAsync(original, leaveOpen: true,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(version, asyncLoaded.Version);
    }

    [Theory]
    [InlineData("bar")]
    [InlineData("chart:bar")]
    public void ChartApiWritesNamespacedClassTokens(string chartClass)
    {
        using OdfChartDocument chart = OdfChartDocument.Create();
        chart.ChartClass = chartClass;
        Assert.Equal("chart:bar", chart.ChartClass);
        using var stream = new MemoryStream();
        chart.SaveToStream(stream);
        stream.Position = 0;
        using OdfPackage package = OdfPackage.Open(stream);
        using Stream content = package.GetEntryStream("content.xml");
        XElement element = XDocument.Load(content).Descendants(XName.Get("chart", OdfNamespaces.Chart)).Single();
        OdfSchemaSet schema = OdfSchemaRegistry.GetSchema(OdfVersion.Odf14);
        Assert.True(OdfSchemaPatternValidator.ValidateElement(element, schema, "chart-chart").IsMatch);
        element.SetAttributeValue(XName.Get("class", OdfNamespaces.Chart), "bar");
        Assert.False(OdfSchemaPatternValidator.ValidateElement(element, schema, "chart-chart").IsMatch);
    }

    [Fact]
    public void ImageApiKeepsAccessibilityMetadataAfterImageContentWhenReplacingImage()
    {
        using ImageDocument image = ImageDocument.Create();
        image.SetImageLayout(OdfLength.FromCentimeters(0), OdfLength.FromCentimeters(0),
            OdfLength.FromCentimeters(1), OdfLength.FromCentimeters(1), "Image", "Title", "Description");
        byte[] bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j8ioAAAAASUVORK5CYII=");
        image.SetImage(bytes, "first.png");
        image.SetImage(bytes, "second.png");
        using var stream = new MemoryStream();
        image.SaveToStream(stream);
        stream.Position = 0;
        using OdfPackage package = OdfPackage.Open(stream);
        using Stream content = package.GetEntryStream("content.xml");
        XElement frame = XDocument.Load(content).Descendants(XName.Get("frame", OdfNamespaces.Draw)).Single();
        OdfSchemaSet schema = OdfSchemaRegistry.GetSchema(OdfVersion.Odf14);
        Assert.True(OdfSchemaPatternValidator.ValidateElement(frame, schema, "draw-frame").IsMatch);
        Assert.Equal("Title", frame.Element(XName.Get("title", OdfNamespaces.Svg))?.Value);
        XElement imageElement = Assert.Single(frame.Elements(XName.Get("image", OdfNamespaces.Draw)));
        imageElement.Remove();
        frame.Add(imageElement);
        Assert.False(OdfSchemaPatternValidator.ValidateElement(frame, schema, "draw-frame").IsMatch);
    }

    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void InlineForeignContentIsValidatedInsteadOfDiscarded(OdfVersion version)
    {
        string valid = WrapForeignText(version, "<text:p><f:outer>before<f:inner><text:span>after</text:span></f:inner></f:outer></text:p>");
        OdfValidationReport accepted = ValidateContent(valid, OdfDocumentKind.Text, version);
        Assert.True(accepted.IsValid, string.Join("; ", accepted.Issues.Select(issue => issue.Message)));
        string invalid = WrapForeignText(version, "<text:p><f:outer><f:inner><text:p>illegal nested paragraph</text:p></f:inner></f:outer></text:p>");
        Assert.Contains(ValidateContent(invalid, OdfDocumentKind.Text, version).Issues, issue => issue.RuleId == "ODF3101");
    }

    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void ForeignContentRequiresAnUnbrokenTextPermittingAncestorChain(OdfVersion version)
    {
        string outside = WrapForeignText(version, "<f:outer><text:p>outside mixed text</text:p></f:outer>");
        string blocked = WrapForeignText(version, "<text:p><text:bookmark-start text:name='mark'><f:outer><text:p>ignored foreign content</text:p></f:outer></text:bookmark-start></text:p>");
        foreach (string content in new[] { outside, blocked })
        {
            OdfValidationReport report = ValidateContent(content, OdfDocumentKind.Text, version);
            Assert.True(report.IsValid, string.Join("; ", report.Issues.Select(issue => issue.Message)));
        }
    }

    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void InlineForeignUnwrapPreservesQNameBindings(OdfVersion version)
    {
        const string Shape = "<draw:custom-shape draw:engine='q:custom' svg:width='1cm' svg:height='1cm' svg:x='0cm' svg:y='0cm'/>";
        string valid = WrapForeignText(version, $"<text:p><f:outer xmlns:q='urn:test:drawing-engine'>{Shape}</f:outer></text:p>");
        OdfValidationReport report = ValidateContent(valid, OdfDocumentKind.Text, version);
        Assert.True(report.IsValid, string.Join("; ", report.Issues.Select(issue => issue.Message)));
        string unbound = valid.Replace(" xmlns:q='urn:test:drawing-engine'", string.Empty, StringComparison.Ordinal);
        Assert.Contains(ValidateContent(unbound, OdfDocumentKind.Text, version).Issues, issue => issue.RuleId == "ODF3101");
    }

    private static string WrapForeignText(OdfVersion version, string body) =>
        $"<office:document-content xmlns:office='{OdfNamespaces.Office}' xmlns:text='{OdfNamespaces.Text}' xmlns:draw='{OdfNamespaces.Draw}' xmlns:svg='{OdfNamespaces.Svg}' xmlns:f='urn:test:foreign' office:version='{OdfVersionInfo.ToVersionString(version)}'><office:body><office:text>{body}</office:text></office:body></office:document-content>";

    private static OdfValidationReport ValidateContent(string content, OdfDocumentKind kind, OdfVersion version, string? styles = null, string? embedded = null)
    {
        using var stream = new MemoryStream();
        using (OdfPackage writer = OdfDocumentFactory.CreatePackage(stream, kind, version, true))
        {
            writer.WriteEntry("content.xml", Encoding.UTF8.GetBytes(content), "text/xml");
            if (styles is not null)
            {
                writer.WriteEntry("styles.xml", Encoding.UTF8.GetBytes(styles), "text/xml");
            }
            if (embedded is not null)
            {
                writer.WriteEntry("Object1/content.xml", Encoding.UTF8.GetBytes(embedded), "text/xml");
            }
            writer.Save();
        }
        stream.Position = 0;
        using OdfPackage package = OdfPackage.Open(stream);
        OdfComplianceProfile profile = version switch
        {
            OdfVersion.Odf10 => OdfComplianceProfiles.OasisOdf10,
            OdfVersion.Odf11 => OdfComplianceProfiles.OasisOdf11,
            OdfVersion.Odf12 => OdfComplianceProfiles.OasisOdf12Extended,
            OdfVersion.Odf13 => OdfComplianceProfiles.OasisOdf13Extended,
            _ => OdfComplianceProfiles.OasisOdf14Extended
        };
        byte[] original = (byte[])package.ReadEntry("content.xml").Clone();
        OdfValidationReport report = OdfPackageValidator.Validate(package, profile);
        Assert.Equal(original, package.ReadEntry("content.xml"));
        return report;
    }
}
