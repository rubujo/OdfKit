using System;
using System.IO;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Verifies ODF schema and package conformance for embedded font sources.
/// 驗證內嵌字型來源的 ODF 結構描述與封裝合規性。
/// </summary>
public sealed class OdfFontEmbeddingComplianceTests
{
    /// <summary>
    /// Verifies that full font embedding writes a conforming font source and manifest entry.
    /// 驗證完整字型內嵌會寫出合規的字型來源與指令清單項目。
    /// </summary>
    [Fact]
    public void EmbedFonts_WritesConformingFontSourceAndManifestEntry()
    {
        const string fontName = "OdfKit-Embedded-Font-UnitTest";
        string fontPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.ttf");
        File.WriteAllBytes(fontPath, [0x00, 0x01, 0x00, 0x00]);

        try
        {
            var context = new OdfFontContext();
            context.RegisterFont(fontName, fontPath);

            using TextDocument document = TextDocument.Create();
            document.AddFontFace(fontName, fontName, "system-serif", "variable");

            context.EmbedFonts(document.Package, document.ContentDom, document.StylesDom);

            string packagePath = $"Fonts/{fontName}.ttf";
            Assert.True(document.Package.HasEntry(packagePath));
            Assert.Equal("application/x-font-truetype", document.Package.Manifest[packagePath]);
            AssertConformingEmbeddedFontFace(FindFontFace(document.ContentDom, fontName), packagePath, "truetype");
        }
        finally
        {
            File.Delete(fontPath);
        }
    }

    /// <summary>
    /// Verifies that external PUA subsetting writes a conforming font source and manifest entry.
    /// 驗證外部 PUA 字型子集化會寫出合規的字型來源與指令清單項目。
    /// </summary>
    [Fact]
    public void EmbedFontSubsets_WritesConformingFontSourceAndManifestEntry()
    {
        const string fontName = "OdfKit-Subset-Font-UnitTest";
        var context = new OdfFontContext();
        using IDisposable registration = context.RegisterFontSubsetter(new FakeFontSubsetter());

        using TextDocument document = TextDocument.Create();
        document.FontContext = context;
        document.AddFontFace(fontName, fontName, "system-serif", "variable");
        document.AddParagraph("自造字" + char.ConvertFromUtf32(0xF0000));

        using var output = new MemoryStream();
        document.SaveToStream(output);

        string packagePath = $"Fonts/Subsets/{fontName}-subset.ttf";
        Assert.True(document.Package.HasEntry(packagePath));
        Assert.Equal("font/ttf", document.Package.Manifest[packagePath]);
        AssertConformingEmbeddedFontFace(FindFontFace(document.ContentDom, fontName), packagePath, "truetype");
    }

    private static OdfNode FindFontFace(OdfNode root, string fontName) => Assert.Single(
        root.Descendants(),
        node => node.LocalName == "font-face" &&
                node.NamespaceUri == OdfNamespaces.Style &&
                node.GetAttribute("name", OdfNamespaces.Style) == fontName);

    private static void AssertConformingEmbeddedFontFace(
        OdfNode fontFace,
        string expectedPackagePath,
        string expectedFormat)
    {
        OdfNode source = Assert.Single(
            fontFace.Children,
            node => node.LocalName == "font-face-src" && node.NamespaceUri == OdfNamespaces.Svg);
        OdfNode uri = Assert.Single(
            source.Children,
            node => node.LocalName == "font-face-uri" && node.NamespaceUri == OdfNamespaces.Svg);
        Assert.Equal(expectedPackagePath, uri.GetAttribute("href", OdfNamespaces.XLink));
        Assert.Equal("simple", uri.GetAttribute("type", OdfNamespaces.XLink));

        OdfNode format = Assert.Single(
            uri.Children,
            node => node.LocalName == "font-face-format" && node.NamespaceUri == OdfNamespaces.Svg);
        Assert.Equal(expectedFormat, format.GetAttribute("string", OdfNamespaces.Svg));
        Assert.DoesNotContain(fontFace.Children, node => node.LocalName == "font-face-uri");

        using var stream = new MemoryStream();
        OdfXmlWriter.Write(fontFace, stream, new OdfSaveOptions { IndentXml = false });
        stream.Position = 0;
        XElement element = XElement.Load(stream);
        XElement sourceElement = Assert.Single(
            element.Elements(XName.Get("font-face-src", OdfNamespaces.Svg)));
        OdfVersion[] supportedVersions =
        [
            OdfVersion.Odf11,
            OdfVersion.Odf12,
            OdfVersion.Odf13,
            OdfVersion.Odf14
        ];
        foreach (OdfVersion version in supportedVersions)
        {
            OdfSchemaPatternValidationResult result = OdfSchemaPatternValidator.ValidateElement(
                sourceElement,
                OdfSchemaRegistry.GetSchema(version),
                "svg-font-face-src");
            Assert.True(
                result.IsMatch,
                $"{version}: {sourceElement}{Environment.NewLine}" + string.Join(
                    Environment.NewLine,
                    result.Issues.Select(issue => $"{issue.RuleId}: {issue.Message}")));
        }
    }

    private sealed class FakeFontSubsetter : IFontSubsetter
    {
        public OdfFontSubset CreateSubset(OdfFontSubsetRequest request) =>
            new([0x00, 0x01], ".ttf", "font/ttf");
    }
}
