using OdfKit.Chart;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Database;
using OdfKit.DOM;
using OdfKit.Formula;
using OdfKit.Image;
using OdfKit.Styles;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Verifies cross-cutting semantic quality evidence for ODC, ODB, ODF, and ODI.
/// 驗證 ODC、ODB、ODF 與 ODI 的跨領域語意品質證據。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Smoke)]
public class SecondaryFormatSemanticCoverageTests
{
    /// <summary>
    /// Verifies ODF 1.0 through 1.3 secondary documents retain their version and unknown content.
    /// 驗證 ODF 1.0～1.3 次要格式文件保留版本與未知內容。
    /// </summary>
    /// <param name="version">The document version. / 文件版本。</param>
    /// <param name="kind">The document kind. / 文件種類。</param>
    [Theory]
    [MemberData(nameof(LegacyVersionCases))]
    public void SecondaryFormats_HighLevelMutationPreservesVersionAndForeignContent(
        OdfVersion version,
        OdfDocumentKind kind)
    {
        using var stream = new MemoryStream();
        using OdfPackage package = OdfDocumentFactory.CreatePackage(stream, kind, version, leaveOpen: true);
        using OdfDocument document = CreateDocument(package, kind);
        document.TargetVersion = version;
        AddContent(document, kind);

        const string foreignNamespace = "urn:odfkit:test:secondary-semantic-foreign";
        var foreign = new OdfNode(OdfNodeType.Element, "semantic-marker", foreignNamespace, "foreign");
        foreign.SetAttribute("value", foreignNamespace, "preserved", "foreign");
        GetSemanticRoot(document, kind).AppendChild(foreign);
        document.Save();

        stream.Position = 0;
        using OdfDocument reloaded = LoadDocument(stream, kind);
        Assert.Equal(version, reloaded.Package.Version);
        OdfNode? marker = FindDescendant(reloaded.ContentRoot, "semantic-marker", foreignNamespace);
        Assert.NotNull(marker);
        Assert.Equal("preserved", marker.GetAttribute("value", foreignNamespace));
    }

    /// <summary>
    /// Verifies downgrade diagnostics cover every secondary document format.
    /// 驗證降版診斷涵蓋所有次要文件格式。
    /// </summary>
    /// <param name="kind">The document kind. / 文件種類。</param>
    [Theory]
    [InlineData(OdfDocumentKind.Chart)]
    [InlineData(OdfDocumentKind.Database)]
    [InlineData(OdfDocumentKind.Formula)]
    [InlineData(OdfDocumentKind.Image)]
    public void VersionCompatibilityReport_CoversEverySecondaryFormat(OdfDocumentKind kind)
    {
        using OdfDocument document = OdfDocument.Create(kind);
        GetSemanticRoot(document, kind).AppendChild(
            new OdfUnknownElement("num-list-format", OdfNamespaces.Number, "number"));

        OdfVersionCompatibilityReport report = document.AnalyzeVersionCompatibility(OdfVersion.Odf13);

        Assert.Contains(
            report.Issues,
            issue => issue.Kind == OdfVersionCompatibilityIssueKind.ElementNotSupported &&
                issue.LocalName == "num-list-format");
    }

    /// <summary>
    /// Verifies secondary format loaders reject a package of a different document kind.
    /// 驗證次要格式載入器會拒絕不同文件種類的封裝。
    /// </summary>
    [Fact]
    public void SecondaryLoaders_RejectMismatchedDocumentKinds()
    {
        byte[] textPackage;
        using (var stream = new MemoryStream())
        {
            using OdfDocument text = OdfDocument.Create(OdfDocumentKind.Text);
            text.SaveToStream(stream);
            textPackage = stream.ToArray();
        }

        Assert.Throws<InvalidOperationException>(
            () => ChartDocument.Load(new MemoryStream(textPackage), "wrong.odc"));
        Assert.Throws<InvalidOperationException>(
            () => DatabaseDocument.Load(new MemoryStream(textPackage), "wrong.odb"));
        Assert.Throws<InvalidOperationException>(
            () => FormulaDocument.Load(new MemoryStream(textPackage), "wrong.odf"));
        Assert.Throws<InvalidOperationException>(
            () => ImageDocument.Load(new MemoryStream(textPackage), "wrong.odi"));
    }

    /// <summary>
    /// Gets the legacy-version and secondary-kind test cases.
    /// 取得舊版與次要文件種類測試案例。
    /// </summary>
    public static IEnumerable<object[]> LegacyVersionCases()
    {
        OdfVersion[] versions = [OdfVersion.Odf10, OdfVersion.Odf11, OdfVersion.Odf12, OdfVersion.Odf13];
        OdfDocumentKind[] kinds =
            [OdfDocumentKind.Chart, OdfDocumentKind.Database, OdfDocumentKind.Formula, OdfDocumentKind.Image];
        foreach (OdfVersion version in versions)
        {
            foreach (OdfDocumentKind kind in kinds)
            {
                yield return [version, kind];
            }
        }
    }

    private static OdfDocument CreateDocument(OdfPackage package, OdfDocumentKind kind) => kind switch
    {
        OdfDocumentKind.Chart => new ChartDocument(package),
        OdfDocumentKind.Database => new DatabaseDocument(package),
        OdfDocumentKind.Formula => new FormulaDocument(package),
        OdfDocumentKind.Image => new ImageDocument(package),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static OdfDocument LoadDocument(Stream stream, OdfDocumentKind kind) => kind switch
    {
        OdfDocumentKind.Chart => ChartDocument.Load(stream, "legacy.odc"),
        OdfDocumentKind.Database => DatabaseDocument.Load(stream, "legacy.odb"),
        OdfDocumentKind.Formula => FormulaDocument.Load(stream, "legacy.odf"),
        OdfDocumentKind.Image => ImageDocument.Load(stream, "legacy.odi"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static void AddContent(OdfDocument document, OdfDocumentKind kind)
    {
        switch (kind)
        {
            case OdfDocumentKind.Chart:
                ((ChartDocument)document).AddSeries("LocalTable.A1:A2");
                break;
            case OdfDocumentKind.Database:
                ((DatabaseDocument)document).AddTable("LegacyTable", "legacy_source");
                break;
            case OdfDocumentKind.Formula:
                ((FormulaDocument)document).SetMathRow(OdfMathToken.Identifier("x"));
                break;
            case OdfDocumentKind.Image:
                var image = (ImageDocument)document;
                image.SetImageLayout(
                    OdfLength.FromCentimeters(0),
                    OdfLength.FromCentimeters(0),
                    OdfLength.FromCentimeters(1),
                    OdfLength.FromCentimeters(1),
                    "LegacyImage");
                image.SetImage(CreatePngBytes(), "legacy.png");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    private static OdfNode? FindDescendant(OdfNode root, string localName, string namespaceUri)
    {
        foreach (OdfNode child in root.Children)
        {
            if (child.LocalName == localName && child.NamespaceUri == namespaceUri)
            {
                return child;
            }

            OdfNode? descendant = FindDescendant(child, localName, namespaceUri);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private static OdfNode GetSemanticRoot(OdfDocument document, OdfDocumentKind kind) =>
        kind == OdfDocumentKind.Formula
            ? ((FormulaDocument)document).MathNode
            : document.ContentRoot;

    private static byte[] CreatePngBytes() =>
        Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");
}
