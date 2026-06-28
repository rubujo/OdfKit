using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Drawing;
using OdfKit.Export;
using OdfKit.Presentation;
using OdfKit.Styles;
using Xunit;

namespace OdfKit.Tests;

public class ManagedSvgExportTests
{
    [Fact]
    public void SvgExporterConvertsBasicDrawingShapes()
    {
        using DrawingDocument document = DrawingDocument.Create();
        OdfDrawPage page = document.AddPage("Page 1");
        page.AddShape(OdfShapeType.Rectangle, OdfLength.FromCentimeters(1), OdfLength.FromCentimeters(1), OdfLength.FromCentimeters(4), OdfLength.FromCentimeters(2));
        page.AddShape(OdfShapeType.Ellipse, OdfLength.FromCentimeters(6), OdfLength.FromCentimeters(1), OdfLength.FromCentimeters(3), OdfLength.FromCentimeters(2));
        page.AddLine(OdfLength.FromCentimeters(1), OdfLength.FromCentimeters(4), OdfLength.FromCentimeters(5), OdfLength.FromCentimeters(4));
        page.AddPath("M 0 0 L 1000 1000 Z", OdfLength.FromCentimeters(1), OdfLength.FromCentimeters(5), OdfLength.FromCentimeters(3), OdfLength.FromCentimeters(3));
        page.AddPolygon(
        [
            (OdfLength.FromCentimeters(8), OdfLength.FromCentimeters(5)),
            (OdfLength.FromCentimeters(10), OdfLength.FromCentimeters(5)),
            (OdfLength.FromCentimeters(9), OdfLength.FromCentimeters(7)),
        ]);

        string svg = document.ToSvg();
        XDocument parsed = XDocument.Parse(svg);
        XNamespace ns = "http://www.w3.org/2000/svg";

        Assert.NotNull(parsed.Root);
        Assert.Equal(ns + "svg", parsed.Root!.Name);
        Assert.Single(parsed.Descendants(ns + "rect"));
        Assert.Single(parsed.Descendants(ns + "ellipse"));
        Assert.Single(parsed.Descendants(ns + "line"));
        Assert.Single(parsed.Descendants(ns + "path"));
        Assert.Single(parsed.Descendants(ns + "polygon"));
        Assert.Contains(parsed.Descendants(ns + "path"), path => (string?)path.Attribute("d") == "M 0 0 L 1000 1000 Z");
    }

    [Fact]
    public void SvgExporterConvertsCompatibleCustomShapeEnhancedPaths()
    {
        using DrawingDocument document = DrawingDocument.Create();
        OdfDrawPage page = document.AddPage("Page 1");
        OdfShape shape = page.AddCustomShape(
            "custom",
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(3),
            OdfLength.FromCentimeters(2));
        OdfNode geometry = Assert.Single(
            shape.Node.Children,
            child => child.LocalName == "enhanced-geometry" && child.NamespaceUri == OdfNamespaces.Draw);
        geometry.SetAttribute("enhanced-path", OdfNamespaces.Draw, "M 0 0 L 1000 0 L 500 1000 Z", "draw");
        geometry.SetAttribute("viewBox", OdfNamespaces.Svg, "0 0 1000 1000", "svg");
        shape.FillColor = "#88CCFF";

        string svg = document.ToSvg();
        XDocument parsed = XDocument.Parse(svg);
        XNamespace ns = "http://www.w3.org/2000/svg";

        XElement nestedSvg = Assert.Single(parsed.Root!.Elements(ns + "svg"));
        Assert.Equal("0 0 1000 1000", (string?)nestedSvg.Attribute("viewBox"));
        XElement path = Assert.Single(nestedSvg.Elements(ns + "path"));
        Assert.Equal("M 0 0 L 1000 0 L 500 1000 Z", (string?)path.Attribute("d"));
        Assert.Equal("#88CCFF", (string?)path.Attribute("fill"));
        Assert.Empty(parsed.Descendants(ns + "rect"));
    }

    [Fact]
    public void SvgExporterExpandsEnhancedPathModifiersAndEquations()
    {
        using DrawingDocument document = DrawingDocument.Create();
        OdfDrawPage page = document.AddPage("Page 1");
        OdfShape shape = page.AddCustomShape(
            "custom",
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(3),
            OdfLength.FromCentimeters(2));
        OdfNode geometry = Assert.Single(
            shape.Node.Children,
            child => child.LocalName == "enhanced-geometry" && child.NamespaceUri == OdfNamespaces.Draw);
        geometry.SetAttribute("enhanced-path", OdfNamespaces.Draw, "M 0 0 L $0 0 L ?half 1000 Z", "draw");
        geometry.SetAttribute("modifiers", OdfNamespaces.Draw, "1000", "draw");
        geometry.SetAttribute("viewBox", OdfNamespaces.Svg, "0 0 1000 1000", "svg");
        var equation = new OdfNode(OdfNodeType.Element, "equation", OdfNamespaces.Draw, "draw");
        equation.SetAttribute("name", OdfNamespaces.Draw, "half", "draw");
        equation.SetAttribute("formula", OdfNamespaces.Draw, "$0 / 2", "draw");
        geometry.AppendChild(equation);

        string svg = document.ToSvg();
        XDocument parsed = XDocument.Parse(svg);
        XNamespace ns = "http://www.w3.org/2000/svg";

        XElement path = Assert.Single(parsed.Descendants(ns + "path"));
        Assert.Equal("M 0 0 L 1000 0 L 500 1000 Z", (string?)path.Attribute("d"));
        Assert.Empty(parsed.Descendants(ns + "rect"));
    }

    [Fact]
    public void SvgExporterTranslatesEnhancedPathHorizontalAndVerticalLinesAfterSubstitution()
    {
        using DrawingDocument document = DrawingDocument.Create();
        OdfDrawPage page = document.AddPage("Page 1");
        OdfShape shape = page.AddCustomShape(
            "custom",
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(3),
            OdfLength.FromCentimeters(2));
        OdfNode geometry = Assert.Single(
            shape.Node.Children,
            child => child.LocalName == "enhanced-geometry" && child.NamespaceUri == OdfNamespaces.Draw);
        geometry.SetAttribute("enhanced-path", OdfNamespaces.Draw, "M 0 0 H $0 V ?bottom H 0 Z", "draw");
        geometry.SetAttribute("modifiers", OdfNamespaces.Draw, "1000", "draw");
        geometry.SetAttribute("viewBox", OdfNamespaces.Svg, "0 0 1000 1000", "svg");
        AddEquation(geometry, "bottom", "$0");

        string svg = document.ToSvg();
        XDocument parsed = XDocument.Parse(svg);
        XNamespace ns = "http://www.w3.org/2000/svg";

        XElement path = Assert.Single(parsed.Descendants(ns + "path"));
        Assert.Equal("M 0 0 H 1000 V 1000 H 0 Z", (string?)path.Attribute("d"));
        Assert.Empty(parsed.Descendants(ns + "rect"));
    }

    [Fact]
    public void SvgExporterTranslatesRelativeEnhancedPathCommandsAfterSubstitution()
    {
        using DrawingDocument document = DrawingDocument.Create();
        OdfDrawPage page = document.AddPage("Page 1");
        OdfShape shape = page.AddCustomShape(
            "custom",
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(3),
            OdfLength.FromCentimeters(2));
        OdfNode geometry = Assert.Single(
            shape.Node.Children,
            child => child.LocalName == "enhanced-geometry" && child.NamespaceUri == OdfNamespaces.Draw);
        geometry.SetAttribute("enhanced-path", OdfNamespaces.Draw, "m $0 10 l 20 30 h 40 v 50 q 10 10 20 0 c 5 5 10 10 15 15 z", "draw");
        geometry.SetAttribute("modifiers", OdfNamespaces.Draw, "100", "draw");
        geometry.SetAttribute("viewBox", OdfNamespaces.Svg, "0 0 300 300", "svg");

        string svg = document.ToSvg();
        XDocument parsed = XDocument.Parse(svg);
        XNamespace ns = "http://www.w3.org/2000/svg";

        XElement path = Assert.Single(parsed.Descendants(ns + "path"));
        Assert.Equal("M 100 10 L 120 40 H 160 V 90 Q 170 100 180 90 C 185 95 190 100 195 105 Z", (string?)path.Attribute("d"));
        Assert.Empty(parsed.Descendants(ns + "rect"));
    }

    [Fact]
    public void SvgExporterSupportsCommonEnhancedPathFormulaFunctions()
    {
        using DrawingDocument document = DrawingDocument.Create();
        OdfDrawPage page = document.AddPage("Page 1");
        OdfShape shape = page.AddCustomShape(
            "custom",
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(3),
            OdfLength.FromCentimeters(2));
        OdfNode geometry = Assert.Single(
            shape.Node.Children,
            child => child.LocalName == "enhanced-geometry" && child.NamespaceUri == OdfNamespaces.Draw);
        geometry.SetAttribute("enhanced-path", OdfNamespaces.Draw, "M 0 0 L ?pow ?rounded L ?max ?mod Z", "draw");
        geometry.SetAttribute("modifiers", OdfNamespaces.Draw, "10 26", "draw");
        geometry.SetAttribute("viewBox", OdfNamespaces.Svg, "0 0 1000 1000", "svg");
        AddEquation(geometry, "pow", "pow($0, 3)");
        AddEquation(geometry, "rounded", "round($1 / 10)");
        AddEquation(geometry, "max", "max(floor(9.9), ceil(10.1))");
        AddEquation(geometry, "mod", "mod(17, 5)");

        string svg = document.ToSvg();
        XDocument parsed = XDocument.Parse(svg);
        XNamespace ns = "http://www.w3.org/2000/svg";

        XElement path = Assert.Single(parsed.Descendants(ns + "path"));
        Assert.Equal("M 0 0 L 1000 3 L 11 2 Z", (string?)path.Attribute("d"));
        Assert.Empty(parsed.Descendants(ns + "rect"));
    }

    [Fact]
    public void SvgExporterIgnoresEnhancedPathStateCommands()
    {
        using DrawingDocument document = DrawingDocument.Create();
        OdfDrawPage page = document.AddPage("Page 1");
        OdfShape shape = page.AddCustomShape(
            "custom",
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(3),
            OdfLength.FromCentimeters(2));
        OdfNode geometry = Assert.Single(
            shape.Node.Children,
            child => child.LocalName == "enhanced-geometry" && child.NamespaceUri == OdfNamespaces.Draw);
        geometry.SetAttribute("enhanced-path", OdfNamespaces.Draw, "M 0 0 L 1000 0 L 500 1000 Z N F", "draw");
        geometry.SetAttribute("viewBox", OdfNamespaces.Svg, "0 0 1000 1000", "svg");

        string svg = document.ToSvg();
        XDocument parsed = XDocument.Parse(svg);
        XNamespace ns = "http://www.w3.org/2000/svg";

        XElement path = Assert.Single(parsed.Descendants(ns + "path"));
        Assert.Equal("M 0 0 L 1000 0 L 500 1000 Z", (string?)path.Attribute("d"));
        Assert.Empty(parsed.Descendants(ns + "rect"));
    }

    [Fact]
    public void SvgExporterTranslatesEnhancedPathAngleEllipseArcs()
    {
        using DrawingDocument document = DrawingDocument.Create();
        OdfDrawPage page = document.AddPage("Page 1");
        OdfShape shape = page.AddCustomShape(
            "custom",
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(3),
            OdfLength.FromCentimeters(2));
        OdfNode geometry = Assert.Single(
            shape.Node.Children,
            child => child.LocalName == "enhanced-geometry" && child.NamespaceUri == OdfNamespaces.Draw);
        geometry.SetAttribute("enhanced-path", OdfNamespaces.Draw, "U 500 500 500 250 0 180 T 500 500 500 250 180 360 Z", "draw");
        geometry.SetAttribute("viewBox", OdfNamespaces.Svg, "0 0 1000 1000", "svg");

        string svg = document.ToSvg();
        XDocument parsed = XDocument.Parse(svg);
        XNamespace ns = "http://www.w3.org/2000/svg";

        XElement path = Assert.Single(parsed.Descendants(ns + "path"));
        Assert.Equal("M 1000 500 A 500 250 0 0 1 0 500 L 0 500 A 500 250 0 0 1 1000 500 Z", (string?)path.Attribute("d"));
        Assert.Empty(parsed.Descendants(ns + "rect"));
    }

    [Fact]
    public void SvgExporterSplitsFullEnhancedPathEllipseArcs()
    {
        using DrawingDocument document = DrawingDocument.Create();
        OdfDrawPage page = document.AddPage("Page 1");
        OdfShape shape = page.AddCustomShape(
            "custom",
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(3),
            OdfLength.FromCentimeters(2));
        OdfNode geometry = Assert.Single(
            shape.Node.Children,
            child => child.LocalName == "enhanced-geometry" && child.NamespaceUri == OdfNamespaces.Draw);
        geometry.SetAttribute("enhanced-path", OdfNamespaces.Draw, "U 500 500 500 250 0 360 Z", "draw");
        geometry.SetAttribute("viewBox", OdfNamespaces.Svg, "0 0 1000 1000", "svg");

        string svg = document.ToSvg();
        XDocument parsed = XDocument.Parse(svg);
        XNamespace ns = "http://www.w3.org/2000/svg";

        XElement path = Assert.Single(parsed.Descendants(ns + "path"));
        Assert.Equal("M 1000 500 A 500 250 0 0 1 0 500 A 500 250 0 0 1 1000 500 Z", (string?)path.Attribute("d"));
        Assert.Empty(parsed.Descendants(ns + "rect"));
    }

    [Fact]
    public void SvgExporterTranslatesEnhancedPathBoundingBoxArcs()
    {
        using DrawingDocument document = DrawingDocument.Create();
        OdfDrawPage page = document.AddPage("Page 1");
        OdfShape shape = page.AddCustomShape(
            "custom",
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(3),
            OdfLength.FromCentimeters(2));
        OdfNode geometry = Assert.Single(
            shape.Node.Children,
            child => child.LocalName == "enhanced-geometry" && child.NamespaceUri == OdfNamespaces.Draw);
        geometry.SetAttribute("enhanced-path", OdfNamespaces.Draw, "B 0 0 1000 1000 1000 500 500 1000 W 0 0 1000 1000 500 1000 0 500", "draw");
        geometry.SetAttribute("viewBox", OdfNamespaces.Svg, "0 0 1000 1000", "svg");

        string svg = document.ToSvg();
        XDocument parsed = XDocument.Parse(svg);
        XNamespace ns = "http://www.w3.org/2000/svg";

        XElement path = Assert.Single(parsed.Descendants(ns + "path"));
        Assert.Equal("M 1000 500 A 500 500 0 1 0 500 1000 L 500 1000 A 500 500 0 0 1 0 500", (string?)path.Attribute("d"));
        Assert.Empty(parsed.Descendants(ns + "rect"));
    }

    [Fact]
    public void SvgExporterCoversLibreOfficeStyleCustomShapeCorpus()
    {
        using DrawingDocument document = DrawingDocument.Create();
        OdfDrawPage page = document.AddPage("Page 1");
        AddCustomShapeExample(page, "flowchart-process", "M 0 0 L 1000 0 L 1000 1000 L 0 1000 Z");
        AddCustomShapeExample(page, "parallelogram", "M 200 0 L 1000 0 L 800 1000 L 0 1000 Z");
        AddCustomShapeExample(page, "can", "U 500 150 450 120 0 180 T 500 150 450 120 180 360 L 950 850 U 500 850 450 120 0 180 T 500 850 450 120 180 360 Z");
        AddCustomShapeExample(page, "smiley", "U 500 500 450 450 0 180 T 500 500 450 450 180 360 M 330 360 U 330 360 55 55 0 360 M 670 360 U 670 360 55 55 0 360 M 280 560 T 500 680 220 120 180 360");
        AddCustomShapeExample(page, "cloud", "M 180 650 C 30 630 20 430 190 390 C 210 190 470 150 590 290 C 780 230 950 350 910 540 C 1000 650 870 820 700 760 C 560 900 290 850 260 700 Z");

        string svg = document.ToSvg();
        XDocument parsed = XDocument.Parse(svg);
        XNamespace ns = "http://www.w3.org/2000/svg";

        XElement[] paths = parsed.Descendants(ns + "path").ToArray();
        Assert.Equal(5, paths.Length);
        Assert.Contains(paths, path => ((string?)path.Attribute("d"))!.Contains("A 450 120", StringComparison.Ordinal));
        Assert.Contains(paths, path => ((string?)path.Attribute("d"))!.Contains("C 30 630", StringComparison.Ordinal));
        Assert.Empty(parsed.Descendants(ns + "rect"));
    }

    [Fact]
    public void SvgExporterConvertsTextBoxesAndEscapesContent()
    {
        using DrawingDocument document = DrawingDocument.Create();
        OdfDrawPage page = document.AddPage();
        page.AddTextBox(
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(4),
            OdfLength.FromCentimeters(2),
            "A < B & C");

        string svg = OdfSvgExporter.Export(document);
        XDocument parsed = XDocument.Parse(svg);
        XNamespace ns = "http://www.w3.org/2000/svg";

        XElement text = Assert.Single(parsed.Descendants(ns + "text"));
        Assert.Equal("A < B & C", text.Value);
        Assert.Single(parsed.Descendants(ns + "rect"));
    }

    [Fact]
    public void SvgExporterPreservesStyledTextRuns()
    {
        using DrawingDocument document = DrawingDocument.Create();
        OdfDrawPage page = document.AddPage();
        page.AddTextBox(
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(4),
            OdfLength.FromCentimeters(2),
            "BoldItalic");
        ReplaceTextBoxWithStyledRuns(
            document,
            "BoldItalic",
            ("Bold", true, false, "18pt", "#CC0000"),
            ("Italic", false, true, "14pt", "#0066CC"));

        string svg = OdfSvgExporter.Export(document);
        XDocument parsed = XDocument.Parse(svg);
        XNamespace ns = "http://www.w3.org/2000/svg";

        XElement text = Assert.Single(parsed.Descendants(ns + "text"));
        Assert.Equal("BoldItalic", text.Value);

        XElement[] tspans = text.Elements(ns + "tspan").ToArray();
        Assert.Equal(2, tspans.Length);
        Assert.Equal("Bold", tspans[0].Value);
        Assert.Equal("bold", (string?)tspans[0].Attribute("font-weight"));
        Assert.Equal("normal", (string?)tspans[0].Attribute("font-style"));
        Assert.Equal("18", (string?)tspans[0].Attribute("font-size"));
        Assert.Equal("#CC0000", (string?)tspans[0].Attribute("fill"));
        Assert.Equal("Italic", tspans[1].Value);
        Assert.Equal("normal", (string?)tspans[1].Attribute("font-weight"));
        Assert.Equal("italic", (string?)tspans[1].Attribute("font-style"));
        Assert.Equal("14", (string?)tspans[1].Attribute("font-size"));
        Assert.Equal("#0066CC", (string?)tspans[1].Attribute("fill"));
    }

    [Fact]
    public void SvgExporterConvertsPicturesWithDataUri()
    {
        using DrawingDocument document = DrawingDocument.Create();
        OdfDrawPage page = document.AddPage();
        page.AddPicture(
            Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII="),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(2),
            OdfLength.FromCentimeters(2));

        string svg = document.ToSvg();
        XDocument parsed = XDocument.Parse(svg);
        XNamespace ns = "http://www.w3.org/2000/svg";

        XElement image = Assert.Single(parsed.Descendants(ns + "image"));
        Assert.StartsWith("data:image/png;base64,", (string?)image.Attribute("href"));
        Assert.Equal((string?)image.Attribute("href"), (string?)image.Attribute(XNamespace.Get("http://www.w3.org/1999/xlink") + "href"));
    }

    [Fact]
    public void SvgExporterConvertsImageClipRectangles()
    {
        using DrawingDocument document = DrawingDocument.Create();
        OdfDrawPage page = document.AddPage();
        page.AddPicture(
            Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII="),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(2),
            OdfLength.FromCentimeters(2));
        OdfNode frame = page.Node.Children.Last(child => child.LocalName == "frame");
        OdfNode imageNode = Assert.Single(frame.Children, child => child.LocalName == "image");
        imageNode.SetAttribute("clip", OdfNamespaces.Fo, "rect(0.25cm, 1.5cm, 1.25cm, 0.5cm)", "fo");

        string svg = document.ToSvg();
        XDocument parsed = XDocument.Parse(svg);
        XNamespace ns = "http://www.w3.org/2000/svg";

        XElement clipPath = Assert.Single(parsed.Descendants(ns + "clipPath"));
        Assert.Equal("odf-clip-1", (string?)clipPath.Attribute("id"));
        XElement clipRect = Assert.Single(clipPath.Elements(ns + "rect"));
        Assert.Equal("42.5197", (string?)clipRect.Attribute("x"));
        Assert.Equal("35.4331", (string?)clipRect.Attribute("y"));
        Assert.Equal("28.3465", (string?)clipRect.Attribute("width"));
        Assert.Equal("28.3465", (string?)clipRect.Attribute("height"));

        XElement image = Assert.Single(parsed.Descendants(ns + "image"));
        Assert.Equal("url(#odf-clip-1)", (string?)image.Attribute("clip-path"));
    }

    [Fact]
    public void SvgExporterAppliesFrameClipRectanglesToTextBoxes()
    {
        using DrawingDocument document = DrawingDocument.Create();
        OdfDrawPage page = document.AddPage();
        page.AddTextBox(
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(4),
            OdfLength.FromCentimeters(2),
            "Clipped text");
        OdfNode frame = page.Node.Children.Last(child => child.LocalName == "frame");
        frame.SetAttribute("clip", OdfNamespaces.Fo, "rect(0cm, 2cm, 1cm, 0.25cm)", "fo");

        string svg = document.ToSvg();
        XDocument parsed = XDocument.Parse(svg);
        XNamespace ns = "http://www.w3.org/2000/svg";

        XElement clipPath = Assert.Single(parsed.Descendants(ns + "clipPath"));
        Assert.Equal("odf-clip-1", (string?)clipPath.Attribute("id"));
        XElement clipRect = Assert.Single(clipPath.Elements(ns + "rect"));
        Assert.Equal("35.4331", (string?)clipRect.Attribute("x"));
        Assert.Equal("28.3465", (string?)clipRect.Attribute("y"));
        Assert.Equal("49.6063", (string?)clipRect.Attribute("width"));
        Assert.Equal("28.3465", (string?)clipRect.Attribute("height"));

        XElement group = Assert.Single(parsed.Descendants(ns + "g"));
        Assert.Equal("url(#odf-clip-1)", (string?)group.Attribute("clip-path"));
        Assert.Equal("Clipped text", Assert.Single(parsed.Descendants(ns + "text")).Value);
    }

    [Fact]
    public void SvgExporterAppliesContourPathClipsToImages()
    {
        using DrawingDocument document = DrawingDocument.Create();
        OdfDrawPage page = document.AddPage();
        page.AddPicture(
            Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII="),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(2),
            OdfLength.FromCentimeters(2));
        OdfNode frame = page.Node.Children.Last(child => child.LocalName == "frame");
        var contour = new OdfNode(OdfNodeType.Element, "contour-path", OdfNamespaces.Draw, "draw");
        contour.SetAttribute("d", OdfNamespaces.Svg, "M 0 0 L 100 0 L 50 100 Z", "svg");
        contour.SetAttribute("viewBox", OdfNamespaces.Svg, "0 0 100 100", "svg");
        frame.AppendChild(contour);

        string svg = document.ToSvg();
        XDocument parsed = XDocument.Parse(svg);
        XNamespace ns = "http://www.w3.org/2000/svg";

        XElement clipPath = Assert.Single(parsed.Descendants(ns + "clipPath"));
        XElement clipShape = Assert.Single(clipPath.Elements(ns + "path"));
        Assert.Equal("M 0 0 L 100 0 L 50 100 Z", (string?)clipShape.Attribute("d"));
        Assert.Equal("translate(28.3465 28.3465) scale(0.5669 0.5669)", (string?)clipShape.Attribute("transform"));
        XElement image = Assert.Single(parsed.Descendants(ns + "image"));
        Assert.Equal("url(#odf-clip-1)", (string?)image.Attribute("clip-path"));
    }

    [Fact]
    public void SvgExporterAppliesContourPolygonClipsToTextBoxes()
    {
        using DrawingDocument document = DrawingDocument.Create();
        OdfDrawPage page = document.AddPage();
        page.AddTextBox(
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(4),
            OdfLength.FromCentimeters(2),
            "Polygon clipped text");
        OdfNode frame = page.Node.Children.Last(child => child.LocalName == "frame");
        var contour = new OdfNode(OdfNodeType.Element, "contour-polygon", OdfNamespaces.Draw, "draw");
        contour.SetAttribute("points", OdfNamespaces.Draw, "0,0 100,0 100,100 0,50", "draw");
        contour.SetAttribute("viewBox", OdfNamespaces.Svg, "0 0 100 100", "svg");
        frame.AppendChild(contour);

        string svg = document.ToSvg();
        XDocument parsed = XDocument.Parse(svg);
        XNamespace ns = "http://www.w3.org/2000/svg";

        XElement clipPath = Assert.Single(parsed.Descendants(ns + "clipPath"));
        XElement clipShape = Assert.Single(clipPath.Elements(ns + "polygon"));
        Assert.Equal("0,0 100,0 100,100 0,50", (string?)clipShape.Attribute("points"));
        Assert.Equal("translate(28.3465 28.3465) scale(1.1339 0.5669)", (string?)clipShape.Attribute("transform"));
        XElement group = Assert.Single(parsed.Descendants(ns + "g"));
        Assert.Equal("url(#odf-clip-1)", (string?)group.Attribute("clip-path"));
        Assert.Equal("Polygon clipped text", Assert.Single(parsed.Descendants(ns + "text")).Value);
    }

    [Fact]
    public void SvgExporterPreservesShapeStylesFromHighLevelApi()
    {
        using DrawingDocument document = DrawingDocument.Create();
        OdfDrawPage page = document.AddPage();
        OdfShape shape = page.AddShape(
            OdfShapeType.Rectangle,
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(2),
            OdfLength.FromCentimeters(2));
        shape.FillColor = "#ffcc00";
        shape.StrokeColor = "#333333";

        string svg = document.ToSvg();
        XDocument parsed = XDocument.Parse(svg);
        XNamespace ns = "http://www.w3.org/2000/svg";

        XElement rect = Assert.Single(parsed.Descendants(ns + "rect"));
        Assert.Equal("#ffcc00", (string?)rect.Attribute("fill"));
        Assert.Equal("#333333", (string?)rect.Attribute("stroke"));
    }

    [Fact]
    public void SvgExporterPreservesDirectTransformAttributes()
    {
        using DrawingDocument document = DrawingDocument.Create();
        OdfDrawPage page = document.AddPage();
        page.AddShape(
            OdfShapeType.Rectangle,
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(2),
            OdfLength.FromCentimeters(2));
        OdfNode rectNode = page.Node.Children.Last(child => child.LocalName == "rect");
        rectNode.SetAttribute("transform", OdfNamespaces.Draw, "rotate(15)", "draw");

        string svg = document.ToSvg();
        XDocument parsed = XDocument.Parse(svg);
        XNamespace ns = "http://www.w3.org/2000/svg";

        XElement rect = Assert.Single(parsed.Descendants(ns + "rect"));
        Assert.Equal("rotate(15)", (string?)rect.Attribute("transform"));
    }

    [Fact]
    public void SvgExporterPreservesAdvancedStrokeAndOpacityAttributes()
    {
        using DrawingDocument document = DrawingDocument.Create();
        OdfDrawPage page = document.AddPage();
        page.AddPath(
            "M 0 0 C 200 100 300 100 500 0",
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(4),
            OdfLength.FromCentimeters(2));
        OdfNode pathNode = page.Node.Children.Last(child => child.LocalName == "path");
        pathNode.SetAttribute("stroke-linecap", OdfNamespaces.Svg, "round", "svg");
        pathNode.SetAttribute("stroke-linejoin", OdfNamespaces.Draw, "miter", "draw");
        pathNode.SetAttribute("fill-rule", OdfNamespaces.Svg, "evenodd", "svg");
        pathNode.SetAttribute("opacity", OdfNamespaces.Draw, "87.5%", "draw");
        pathNode.SetAttribute("stroke-opacity", OdfNamespaces.Svg, "50%", "svg");

        string svg = document.ToSvg();
        XDocument parsed = XDocument.Parse(svg);
        XNamespace ns = "http://www.w3.org/2000/svg";

        XElement path = Assert.Single(parsed.Descendants(ns + "path"));
        Assert.Equal("round", (string?)path.Attribute("stroke-linecap"));
        Assert.Equal("miter", (string?)path.Attribute("stroke-linejoin"));
        Assert.Equal("evenodd", (string?)path.Attribute("fill-rule"));
        Assert.Equal("0.875", (string?)path.Attribute("opacity"));
        Assert.Equal("0.5", (string?)path.Attribute("stroke-opacity"));
    }

    [Fact]
    public void SvgExporterConvertsDrawMarkersToSvgMarkerDefinitions()
    {
        using DrawingDocument document = DrawingDocument.Create();
        OdfDrawPage page = document.AddPage();
        page.AddLine(
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(5),
            OdfLength.FromCentimeters(1));
        OdfNode lineNode = page.Node.Children.Last(child => child.LocalName == "line");
        lineNode.SetAttribute("marker-end", OdfNamespaces.Draw, "Arrow", "draw");

        var marker = new OdfNode(OdfNodeType.Element, "marker", OdfNamespaces.Draw, "draw");
        marker.SetAttribute("name", OdfNamespaces.Draw, "Arrow", "draw");
        marker.SetAttribute("viewBox", OdfNamespaces.Svg, "0 0 10 10", "svg");
        marker.SetAttribute("d", OdfNamespaces.Svg, "M 0 0 L 10 5 L 0 10 Z", "svg");
        document.StylesDom.AppendChild(marker);

        string svg = document.ToSvg();
        XDocument parsed = XDocument.Parse(svg);
        XNamespace ns = "http://www.w3.org/2000/svg";

        XElement svgMarker = Assert.Single(parsed.Descendants(ns + "marker"));
        Assert.Equal("odf-marker-Arrow", (string?)svgMarker.Attribute("id"));
        Assert.Equal("0 0 10 10", (string?)svgMarker.Attribute("viewBox"));
        Assert.Equal("10", (string?)svgMarker.Attribute("refX"));
        Assert.Equal("5", (string?)svgMarker.Attribute("refY"));
        Assert.Equal("auto", (string?)svgMarker.Attribute("orient"));

        XElement markerPath = Assert.Single(svgMarker.Elements(ns + "path"));
        Assert.Equal("M 0 0 L 10 5 L 0 10 Z", (string?)markerPath.Attribute("d"));
        Assert.Equal("context-stroke", (string?)markerPath.Attribute("fill"));

        XElement line = Assert.Single(parsed.Descendants(ns + "line"));
        Assert.Equal("url(#odf-marker-Arrow)", (string?)line.Attribute("marker-end"));
    }

    [Fact]
    public void SvgExporterConvertsNamedLinearGradients()
    {
        using DrawingDocument document = DrawingDocument.Create();
        OdfDrawPage page = document.AddPage();
        page.AddShape(
            OdfShapeType.Rectangle,
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(2),
            OdfLength.FromCentimeters(2));
        OdfNode rectNode = page.Node.Children.Last(child => child.LocalName == "rect");
        rectNode.SetAttribute("fill", OdfNamespaces.Draw, "gradient", "draw");
        rectNode.SetAttribute("fill-gradient-name", OdfNamespaces.Draw, "Red To Blue", "draw");

        var gradient = new OdfNode(OdfNodeType.Element, "gradient", OdfNamespaces.Draw, "draw");
        gradient.SetAttribute("name", OdfNamespaces.Draw, "Red To Blue", "draw");
        gradient.SetAttribute("start-color", OdfNamespaces.Draw, "#ff0000", "draw");
        gradient.SetAttribute("end-color", OdfNamespaces.Draw, "#0000ff", "draw");
        document.StylesDom.AppendChild(gradient);

        string svg = document.ToSvg();
        XDocument parsed = XDocument.Parse(svg);
        XNamespace ns = "http://www.w3.org/2000/svg";

        XElement svgGradient = Assert.Single(parsed.Descendants(ns + "linearGradient"));
        Assert.Equal("odf-gradient-Red-To-Blue", (string?)svgGradient.Attribute("id"));

        XElement[] stops = svgGradient.Elements(ns + "stop").ToArray();
        Assert.Equal(2, stops.Length);
        Assert.Equal("#ff0000", (string?)stops[0].Attribute("stop-color"));
        Assert.Equal("#0000ff", (string?)stops[1].Attribute("stop-color"));

        XElement rect = Assert.Single(parsed.Descendants(ns + "rect"));
        Assert.Equal("url(#odf-gradient-Red-To-Blue)", (string?)rect.Attribute("fill"));
    }

    [Fact]
    public void SvgExporterConvertsLinearGradientAngle()
    {
        using DrawingDocument document = DrawingDocument.Create();
        OdfDrawPage page = document.AddPage();
        page.AddShape(
            OdfShapeType.Rectangle,
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(2),
            OdfLength.FromCentimeters(2));
        OdfNode rectNode = page.Node.Children.Last(child => child.LocalName == "rect");
        rectNode.SetAttribute("fill", OdfNamespaces.Draw, "gradient", "draw");
        rectNode.SetAttribute("fill-gradient-name", OdfNamespaces.Draw, "Vertical", "draw");

        var gradient = new OdfNode(OdfNodeType.Element, "gradient", OdfNamespaces.Draw, "draw");
        gradient.SetAttribute("name", OdfNamespaces.Draw, "Vertical", "draw");
        gradient.SetAttribute("style", OdfNamespaces.Draw, "linear", "draw");
        gradient.SetAttribute("angle", OdfNamespaces.Draw, "900", "draw");
        gradient.SetAttribute("start-color", OdfNamespaces.Draw, "#111111", "draw");
        gradient.SetAttribute("end-color", OdfNamespaces.Draw, "#eeeeee", "draw");
        document.StylesDom.AppendChild(gradient);

        string svg = document.ToSvg();
        XDocument parsed = XDocument.Parse(svg);
        XNamespace ns = "http://www.w3.org/2000/svg";

        XElement svgGradient = Assert.Single(parsed.Descendants(ns + "linearGradient"));
        Assert.Equal("50%", (string?)svgGradient.Attribute("x1"));
        Assert.Equal("0%", (string?)svgGradient.Attribute("y1"));
        Assert.Equal("50%", (string?)svgGradient.Attribute("x2"));
        Assert.Equal("100%", (string?)svgGradient.Attribute("y2"));
    }

    [Fact]
    public void SvgExporterConvertsRadialGradients()
    {
        using DrawingDocument document = DrawingDocument.Create();
        OdfDrawPage page = document.AddPage();
        page.AddShape(
            OdfShapeType.Ellipse,
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(2),
            OdfLength.FromCentimeters(2));
        OdfNode ellipseNode = page.Node.Children.Last(child => child.LocalName == "ellipse");
        ellipseNode.SetAttribute("fill", OdfNamespaces.Draw, "gradient", "draw");
        ellipseNode.SetAttribute("fill-gradient-name", OdfNamespaces.Draw, "Glow", "draw");

        var gradient = new OdfNode(OdfNodeType.Element, "gradient", OdfNamespaces.Draw, "draw");
        gradient.SetAttribute("name", OdfNamespaces.Draw, "Glow", "draw");
        gradient.SetAttribute("style", OdfNamespaces.Draw, "radial", "draw");
        gradient.SetAttribute("cx", OdfNamespaces.Draw, "40%", "draw");
        gradient.SetAttribute("cy", OdfNamespaces.Draw, "60%", "draw");
        gradient.SetAttribute("start-color", OdfNamespaces.Draw, "#ffffff", "draw");
        gradient.SetAttribute("end-color", OdfNamespaces.Draw, "#003366", "draw");
        document.StylesDom.AppendChild(gradient);

        string svg = document.ToSvg();
        XDocument parsed = XDocument.Parse(svg);
        XNamespace ns = "http://www.w3.org/2000/svg";

        XElement svgGradient = Assert.Single(parsed.Descendants(ns + "radialGradient"));
        Assert.Equal("odf-gradient-Glow", (string?)svgGradient.Attribute("id"));
        Assert.Equal("40%", (string?)svgGradient.Attribute("cx"));
        Assert.Equal("60%", (string?)svgGradient.Attribute("cy"));
        Assert.Equal("40%", (string?)svgGradient.Attribute("fx"));
        Assert.Equal("60%", (string?)svgGradient.Attribute("fy"));
        Assert.Equal("50%", (string?)svgGradient.Attribute("r"));

        XElement ellipse = Assert.Single(parsed.Descendants(ns + "ellipse"));
        Assert.Equal("url(#odf-gradient-Glow)", (string?)ellipse.Attribute("fill"));
    }

    [Fact]
    public void SvgExporterWritesFile()
    {
        using DrawingDocument document = DrawingDocument.Create();
        document.AddPage().AddShape(
            OdfShapeType.Rectangle,
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(2),
            OdfLength.FromCentimeters(2));

        string root = Path.Combine(Path.GetTempPath(), "OdfKit_SvgExport_" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(root, "drawing.svg");

        try
        {
            document.SaveAsSvg(path);

            Assert.True(File.Exists(path));
            Assert.Contains("<svg", File.ReadAllText(path));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private static void ReplaceTextBoxWithStyledRuns(
        DrawingDocument document,
        string originalText,
        params (string Text, bool Bold, bool Italic, string FontSize, string Color)[] runs)
    {
        OdfNode paragraph = Assert.Single(
            document.ContentDom.Descendants(),
            node => node.LocalName == "p" &&
                node.NamespaceUri == OdfNamespaces.Text &&
                node.TextContent == originalText);
        paragraph.Children.Clear();

        foreach ((string text, bool bold, bool italic, string fontSize, string color) in runs)
        {
            var span = new OdfNode(OdfNodeType.Element, "span", OdfNamespaces.Text, "text") { TextContent = text };
            paragraph.AppendChild(span);
            document.StyleEngine.SetLocalStyleProperty(span, "text", "text-properties", "font-weight", OdfNamespaces.Fo, bold ? "bold" : "normal", "fo");
            document.StyleEngine.SetLocalStyleProperty(span, "text", "text-properties", "font-style", OdfNamespaces.Fo, italic ? "italic" : "normal", "fo");
            document.StyleEngine.SetLocalStyleProperty(span, "text", "text-properties", "font-size", OdfNamespaces.Fo, fontSize, "fo");
            document.StyleEngine.SetLocalStyleProperty(span, "text", "text-properties", "color", OdfNamespaces.Fo, color, "fo");
        }
    }

    private static void AddEquation(OdfNode geometry, string name, string formula)
    {
        var equation = new OdfNode(OdfNodeType.Element, "equation", OdfNamespaces.Draw, "draw");
        equation.SetAttribute("name", OdfNamespaces.Draw, name, "draw");
        equation.SetAttribute("formula", OdfNamespaces.Draw, formula, "draw");
        geometry.AppendChild(equation);
    }

    private static void AddCustomShapeExample(OdfDrawPage page, string type, string enhancedPath)
    {
        OdfShape shape = page.AddCustomShape(
            type,
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(3),
            OdfLength.FromCentimeters(2));
        OdfNode geometry = Assert.Single(
            shape.Node.Children,
            child => child.LocalName == "enhanced-geometry" && child.NamespaceUri == OdfNamespaces.Draw);
        geometry.SetAttribute("type", OdfNamespaces.Draw, type, "draw");
        geometry.SetAttribute("enhanced-path", OdfNamespaces.Draw, enhancedPath, "draw");
        geometry.SetAttribute("viewBox", OdfNamespaces.Svg, "0 0 1000 1000", "svg");
    }
}
