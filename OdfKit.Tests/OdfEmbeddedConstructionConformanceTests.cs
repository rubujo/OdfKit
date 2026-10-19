using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using OdfKit.Chart;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Database;
using OdfKit.DOM;
using OdfKit.Drawing;
using OdfKit.Formula;
using OdfKit.Image;
using OdfKit.Presentation;
using OdfKit.Spreadsheet;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests that embedded wrappers load their subpath before any document DOM is initialized.
/// 測試內嵌包裝器在初始化任何文件 DOM 之前就載入正確子路徑。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfEmbeddedConstructionConformanceTests
{
    /// <summary>
    /// Built-in embedded kinds and variants retain independent bodies and MIME identities.
    /// 內建內嵌種類及變體保留獨立本文與 MIME 識別。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void BuiltInWrappersKeepTheirOwnContent(OdfVersion version)
    {
        using TextDocument parent = TextDocument.Create();
        parent.TargetVersion = version;
        parent.AddParagraph("parent-only-marker");
        foreach (string kind in Kinds)
        {
            OdfDocument child = Open(parent, kind, "Objects/" + kind, true);
            child.TargetVersion = version;
            Assert.Equal(ExpectedKind(kind), child.ContentKind);
            Assert.Equal(ExpectedIdentity(kind), child.DocumentKind);
            Assert.Equal("Objects/" + kind + "/", child.SubPath);
            Assert.DoesNotContain("parent-only-marker", Xml(child.ContentDom), StringComparison.Ordinal);
        }
        using var stream = new MemoryStream();
        parent.SaveToStream(stream);
        using TextDocument loaded = TextDocument.Load(new MemoryStream(stream.ToArray()));
        foreach (string kind in Kinds)
        {
            OdfDocument child = Open(loaded, kind, "Objects/" + kind, false);
            Assert.Equal(ExpectedKind(kind), child.ContentKind);
            Assert.Equal(ExpectedIdentity(kind), child.DocumentKind);
            Assert.DoesNotContain("parent-only-marker", Xml(child.ContentDom), StringComparison.Ordinal);
        }
        OdfComplianceProfile profile = version switch
        {
            OdfVersion.Odf12 => OdfComplianceProfiles.OasisOdf12Strict,
            OdfVersion.Odf13 => OdfComplianceProfiles.OasisOdf13Strict,
            _ => OdfComplianceProfiles.OasisOdf14Strict
        };
        OdfValidationReport report = OdfPackageValidator.Validate(loaded.Package, profile);
        Assert.True(report.IsValid, report.ToJson());
    }

    /// <summary>
    /// Reflection-created wrappers receive the same subpath-aware initialization.
    /// 透過反射建立的包裝器同樣依子路徑初始化。
    /// </summary>
    [Fact]
    public void ReflectionWrappersDoNotCopyParentContent()
    {
        using TextDocument parent = TextDocument.Create();
        parent.AddParagraph("parent-only-marker");
        ReflectionTextDocument child = parent.CreateEmbeddedDocument<ReflectionTextDocument>("Custom");
        Assert.Equal("Custom/", child.SubPath);
        Assert.DoesNotContain("parent-only-marker", Xml(child.ContentDom), StringComparison.Ordinal);
    }

    /// <summary>
    /// Failed constructors restore construction state before another root document is opened.
    /// 建構失敗後會在開啟另一根文件之前還原建構狀態。
    /// </summary>
    [Fact]
    public void FailedConstructorsDoNotLeakSubpaths()
    {
        using TextDocument parent = TextDocument.Create();
        parent.AddParagraph("parent-only-marker");
        using var saved = new MemoryStream();
        parent.SaveToStream(saved);
        Assert.Throws<TargetInvocationException>(() => parent.CreateEmbeddedDocument<ThrowingTextDocument>("Broken"));
        using var root = new TextDocument(parent.Package);
        Assert.Equal("", root.SubPath);
        Assert.Contains("parent-only-marker", Xml(root.ContentDom), StringComparison.Ordinal);
    }

    /// <summary>
    /// Default embedded text master pages reference a declared page layout.
    /// 預設內嵌文字主版頁面參照已宣告的頁面版面配置。
    /// </summary>
    [Fact]
    public void EmbeddedTextDefaultsResolveTheirPageLayouts()
    {
        using TextDocument parent = TextDocument.Create();
        TextDocument child = parent.CreateEmbeddedDocument<TextDocument>("Text");
        XElement styles = XElement.Parse(Xml(child.StylesDom));
        XNamespace style = OdfNamespaces.Style;
        HashSet<string> layouts = styles.Descendants(style + "page-layout").Select(node => node.Attribute(style + "name")!.Value).ToHashSet(StringComparer.Ordinal);
        foreach (XElement master in styles.Descendants(style + "master-page"))
            Assert.Contains(master.Attribute(style + "page-layout-name")!.Value, layouts);
    }

    /// <summary>
    /// A MathML root does not qualify as native text content merely because another embedded formula exists.
    /// MathML 根節點不會因另有內嵌公式而變成合法的原生文字內容。
    /// </summary>
    [Fact]
    public void MathRootsRequireTheirOwnFormulaMediaType()
    {
        using TextDocument parent = TextDocument.Create();
        parent.CreateEmbeddedDocument<FormulaDocument>("Formula");
        TextDocument text = parent.CreateEmbeddedDocument<TextDocument>("Text");
        using var stream = new MemoryStream();
        parent.SaveToStream(stream);
        byte[] math = Encoding.UTF8.GetBytes("<math xmlns='http://www.w3.org/1998/Math/MathML'><mi>x</mi></math>");
        parent.Package.WriteEntry("Text/content.xml", math, "text/xml");
        OdfValidationReport report = OdfPackageValidator.Validate(parent.Package, OdfComplianceProfiles.OasisOdf14Strict);
        Assert.Contains(report.Issues, issue => issue.RuleId == "ODF0300" && issue.PackagePath == "Text/content.xml");
        Assert.DoesNotContain(report.Issues, issue => issue.RuleId == "ODF0300" && issue.PackagePath == "Formula/content.xml");
        Assert.Equal(OdfDocumentKind.Text, text.ContentKind);
    }

    private sealed class ReflectionTextDocument : TextDocument
    {
        /// <summary>
        /// Creates a test wrapper using a package-only constructor.
        /// 以僅含封裝參數的建構函式建立測試包裝器。
        /// </summary>
        public ReflectionTextDocument(OdfPackage package) : base(package) { }
    }

    private sealed class ThrowingTextDocument : TextDocument
    {
        /// <summary>
        /// Fails after base construction to exercise context cleanup.
        /// 在基底建構之後失敗，以測試建構情境清理。
        /// </summary>
        public ThrowingTextDocument(OdfPackage package) : base(package) => throw new InvalidOperationException();
    }

    private static readonly string[] Kinds =
    [
        "text", "text-flat", "text-template", "text-master",
        "sheet", "sheet-flat", "sheet-template",
        "presentation", "presentation-flat", "presentation-template",
        "drawing", "drawing-flat", "drawing-template",
        "image", "image-alias", "image-flat", "database", "database-alias", "chart", "formula"
    ];
    private static OdfDocumentKind ExpectedIdentity(string kind) => kind switch
    {
        "text-template" => OdfDocumentKind.TextTemplate,
        "text-master" => OdfDocumentKind.TextMaster,
        "sheet-template" => OdfDocumentKind.SpreadsheetTemplate,
        "presentation-template" => OdfDocumentKind.PresentationTemplate,
        "drawing-template" => OdfDocumentKind.GraphicsTemplate,
        _ => ExpectedKind(kind)
    };
    private static OdfDocumentKind ExpectedKind(string kind) => kind.Split('-')[0] switch
    {
        "text" => OdfDocumentKind.Text,
        "sheet" => OdfDocumentKind.Spreadsheet,
        "presentation" => OdfDocumentKind.Presentation,
        "drawing" => OdfDocumentKind.Graphics,
        "image" => OdfDocumentKind.Image,
        "database" => OdfDocumentKind.Database,
        "chart" => OdfDocumentKind.Chart,
        _ => OdfDocumentKind.Formula
    };
    private static OdfDocument Open(OdfDocument parent, string kind, string path, bool create) => kind switch
    {
        "text" => Open<TextDocument>(parent, path, create),
        "text-flat" => Open<FlatTextDocument>(parent, path, create),
        "text-template" => Open<TextTemplateDocument>(parent, path, create),
        "text-master" => Open<TextMasterDocument>(parent, path, create),
        "sheet" => Open<SpreadsheetDocument>(parent, path, create),
        "sheet-flat" => Open<FlatSpreadsheetDocument>(parent, path, create),
        "sheet-template" => Open<SpreadsheetTemplateDocument>(parent, path, create),
        "presentation" => Open<PresentationDocument>(parent, path, create),
        "presentation-flat" => Open<FlatPresentationDocument>(parent, path, create),
        "presentation-template" => Open<PresentationTemplateDocument>(parent, path, create),
        "drawing" => Open<DrawingDocument>(parent, path, create),
        "drawing-flat" => Open<FlatGraphicsDocument>(parent, path, create),
        "drawing-template" => Open<GraphicsTemplateDocument>(parent, path, create),
        "image" => Open<OdfImageDocument>(parent, path, create),
        "image-alias" => Open<ImageDocument>(parent, path, create),
        "image-flat" => Open<FlatImageDocument>(parent, path, create),
        "database" => Open<OdfDatabaseDocument>(parent, path, create),
        "database-alias" => Open<DatabaseDocument>(parent, path, create),
        "chart" => Open<ChartDocument>(parent, path, create),
        _ => Open<FormulaDocument>(parent, path, create)
    };
    private static OdfDocument Open<
#if NET10_0_OR_GREATER
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
#endif
    T>(OdfDocument parent, string path, bool create) where T : OdfDocument =>
        create ? parent.CreateEmbeddedDocument<T>(path) : parent.GetEmbeddedDocument<T>(path);
    private static string Xml(OdfNode node)
    {
        using var stream = new MemoryStream();
        OdfXmlWriter.Write(node, stream);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
