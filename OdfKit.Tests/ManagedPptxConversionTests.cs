using System;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using OdfKit.Core;
using OdfKit.Conversion;
using OdfKit.DOM;
using OdfKit.Presentation;
using OdfKit.Styles;
using Xunit;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;
using OdfPresentationDocument = OdfKit.Presentation.PresentationDocument;
using OpenXmlPresentationPart = DocumentFormat.OpenXml.Packaging.PresentationPart;
using OpenXmlSlidePart = DocumentFormat.OpenXml.Packaging.SlidePart;
using PackagingPresentationDocument = DocumentFormat.OpenXml.Packaging.PresentationDocument;

namespace OdfKit.Tests;

[Trait(TestCategories.Kind, TestCategories.Regression)]
public class ManagedPptxConversionTests
{
    [Fact]
    public void OdpToPptxConverterWritesSlidesTextShapesAndImages()
    {
        using OdfPresentationDocument odp = OdfPresentationDocument.Create();
        OdfSlide slide = odp.AddSlide("Intro");
        slide.BackgroundColor = "#102030";
        slide.AddTextBox(
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(8),
            OdfLength.FromCentimeters(2),
            "Hello PPTX");
        ApplyStyledTextBox(source: odp, text: "Hello PPTX", bold: true, italic: true, fontSize: "24pt", color: "#336699");
        slide.AddShape(
            OdfShapeType.Rectangle,
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(4),
            OdfLength.FromCentimeters(3),
            OdfLength.FromCentimeters(2));
        slide.AddShape(
            OdfShapeType.Ellipse,
            OdfLength.FromCentimeters(5),
            OdfLength.FromCentimeters(4),
            OdfLength.FromCentimeters(3),
            OdfLength.FromCentimeters(2));
        slide.AddPicture(
            Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII="),
            OdfLength.FromCentimeters(9),
            OdfLength.FromCentimeters(4),
            OdfLength.FromCentimeters(2),
            OdfLength.FromCentimeters(2));
        slide.SpeakerNotes = "Review the managed conversion path.";

        using var pptxStream = new MemoryStream();
        OdpToPptxConverter.Convert(odp, pptxStream);

        pptxStream.Position = 0;
        using PackagingPresentationDocument pptx = PackagingPresentationDocument.Open(pptxStream, false);
        Assert.Empty(new OpenXmlValidator(FileFormatVersions.Office2019).Validate(pptx, TestContext.Current.CancellationToken));

        OpenXmlPresentationPart? presentationPart = pptx.PresentationPart;
        Assert.NotNull(presentationPart);
        OpenXmlSlidePart slidePart = Assert.Single(presentationPart!.SlideParts);
        P.Slide? parsedSlide = slidePart.Slide;
        Assert.NotNull(parsedSlide);
        Assert.Equal(
            "102030",
            parsedSlide!.CommonSlideData?.Background?.BackgroundProperties?.GetFirstChild<A.SolidFill>()?.GetFirstChild<A.RgbColorModelHex>()?.Val?.Value);

        Assert.Contains(parsedSlide.Descendants<A.Text>(), text => text.Text == "Hello PPTX");
        Assert.Contains(
            parsedSlide.Descendants<A.PresetGeometry>(),
            geometry => geometry.Preset?.Value == A.ShapeTypeValues.Rectangle);
        Assert.Contains(
            parsedSlide.Descendants<A.PresetGeometry>(),
            geometry => geometry.Preset?.Value == A.ShapeTypeValues.Ellipse);
        Assert.Single(slidePart.ImageParts);
        Assert.NotNull(slidePart.NotesSlidePart);
        Assert.Contains(
            slidePart.NotesSlidePart!.NotesSlide!.Descendants<A.Text>(),
            text => text.Text == "Review the managed conversion path.");
        A.RunProperties runProperties = Assert.Single(
            parsedSlide.Descendants<A.RunProperties>(),
            properties => properties.Parent is A.Run && properties.Parent.Descendants<A.Text>().Any(text => text.Text == "Hello PPTX"));
        Assert.True(runProperties.Bold?.Value);
        Assert.True(runProperties.Italic?.Value);
        Assert.Equal(2400, runProperties.FontSize?.Value);
        Assert.Equal(
            "336699",
            runProperties.GetFirstChild<A.SolidFill>()?.GetFirstChild<A.RgbColorModelHex>()?.Val?.Value);
    }

    [Fact]
    public void PptxToOdpConverterReadsSlidesTextShapesAndImages()
    {
        using OdfPresentationDocument source = OdfPresentationDocument.Create();
        OdfSlide sourceSlide = source.AddSlide("Intro");
        sourceSlide.BackgroundColor = "#203040";
        sourceSlide.AddTextBox(
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(8),
            OdfLength.FromCentimeters(2),
            "Round trip text");
        ApplyStyledTextBox(source, "Round trip text", bold: true, italic: true, fontSize: "22pt", color: "#AA5500");
        sourceSlide.AddShape(
            OdfShapeType.Rectangle,
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(4),
            OdfLength.FromCentimeters(3),
            OdfLength.FromCentimeters(2));
        sourceSlide.AddShape(
            OdfShapeType.Ellipse,
            OdfLength.FromCentimeters(5),
            OdfLength.FromCentimeters(4),
            OdfLength.FromCentimeters(3),
            OdfLength.FromCentimeters(2));
        sourceSlide.AddPicture(
            Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII="),
            OdfLength.FromCentimeters(9),
            OdfLength.FromCentimeters(4),
            OdfLength.FromCentimeters(2),
            OdfLength.FromCentimeters(2));
        sourceSlide.SpeakerNotes = "Presenter reminder";

        using var pptxStream = new MemoryStream();
        OdpToPptxConverter.Convert(source, pptxStream);

        pptxStream.Position = 0;
        using OdfPresentationDocument converted = PptxToOdpConverter.Convert(pptxStream);

        OdfSlide slide = Assert.Single(converted.Slides);
        Assert.Equal("#203040", slide.BackgroundColor);
        Assert.Contains(slide.TextBoxes, textBox => textBox.Text == "Round trip text");
        Assert.Contains(slide.Shapes, shape => shape.LocalName == "rect");
        Assert.Contains(slide.Shapes, shape => shape.LocalName == "ellipse");
        Assert.Single(slide.Pictures);
        Assert.Equal("Presenter reminder", slide.SpeakerNotes);

        OdfNode styledSpan = Assert.Single(
            converted.ContentDom.Descendants(),
            node => node.LocalName == "span" &&
                node.NamespaceUri == OdfNamespaces.Text &&
                node.TextContent == "Round trip text");
        string? styleName = styledSpan.GetAttribute("style-name", OdfNamespaces.Text);
        Assert.NotNull(styleName);
        Assert.Equal("bold", converted.StyleEngine.GetStyleProperty(styleName, "font-weight", OdfNamespaces.Fo, "text"));
        Assert.Equal("italic", converted.StyleEngine.GetStyleProperty(styleName, "font-style", OdfNamespaces.Fo, "text"));
        Assert.Equal("22pt", converted.StyleEngine.GetStyleProperty(styleName, "font-size", OdfNamespaces.Fo, "text"));
        Assert.Equal("#AA5500", converted.StyleEngine.GetStyleProperty(styleName, "color", OdfNamespaces.Fo, "text"));
    }

    [Fact]
    public void PptxConvertersPreserveSpeakerNoteParagraphs()
    {
        using OdfPresentationDocument source = OdfPresentationDocument.Create();
        OdfSlide sourceSlide = source.AddSlide("Notes");
        sourceSlide.SetSpeakerNotes(["Opening reminder", "Detailed follow-up"]);

        using var pptxStream = new MemoryStream();
        OdpToPptxConverter.Convert(source, pptxStream);

        pptxStream.Position = 0;
        using PackagingPresentationDocument pptx = PackagingPresentationDocument.Open(pptxStream, false);
        OpenXmlSlidePart slidePart = Assert.Single(pptx.PresentationPart!.SlideParts);
        Assert.NotNull(slidePart.NotesSlidePart);
        string[] noteParagraphs = slidePart.NotesSlidePart!.NotesSlide!.Descendants<A.Paragraph>()
            .Select(paragraph => string.Concat(paragraph.Descendants<A.Text>().Select(text => text.Text)))
            .Where(text => text.Length > 0)
            .ToArray();
        Assert.Equal(["Opening reminder", "Detailed follow-up"], noteParagraphs);

        pptxStream.Position = 0;
        using OdfPresentationDocument converted = PptxToOdpConverter.Convert(pptxStream);
        OdfSlide slide = Assert.Single(converted.Slides);
        Assert.Equal(["Opening reminder", "Detailed follow-up"], slide.SpeakerNoteParagraphs);
        Assert.Equal("Opening reminder" + Environment.NewLine + "Detailed follow-up", slide.SpeakerNotes);
    }
    [Fact]
    public void PptxConvertersPreserveTextParagraphAlignment()
    {
        using OdfPresentationDocument source = OdfPresentationDocument.Create();
        OdfSlide sourceSlide = source.AddSlide("Alignment");
        sourceSlide.AddTextBox(
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(8),
            OdfLength.FromCentimeters(3),
            ["Centered", "Right aligned"]);
        SetParagraphTextAlign(source, "Centered", "center");
        SetParagraphTextAlign(source, "Right aligned", "end");

        using var pptxStream = new MemoryStream();
        OdpToPptxConverter.Convert(source, pptxStream);

        pptxStream.Position = 0;
        using PackagingPresentationDocument pptx = PackagingPresentationDocument.Open(pptxStream, false);
        OpenXmlSlidePart slidePart = Assert.Single(pptx.PresentationPart!.SlideParts);
        A.Paragraph[] paragraphs = slidePart.Slide!.Descendants<A.Paragraph>()
            .Where(paragraph => paragraph.Descendants<A.Text>().Any(text => text.Text is "Centered" or "Right aligned"))
            .ToArray();
        Assert.Equal(2, paragraphs.Length);
        Assert.Equal(A.TextAlignmentTypeValues.Center, paragraphs[0].ParagraphProperties?.Alignment?.Value);
        Assert.Equal(A.TextAlignmentTypeValues.Right, paragraphs[1].ParagraphProperties?.Alignment?.Value);

        pptxStream.Position = 0;
        using OdfPresentationDocument converted = PptxToOdpConverter.Convert(pptxStream);
        Assert.Equal("center", ReadParagraphTextAlign(converted, "Centered"));
        Assert.Equal("end", ReadParagraphTextAlign(converted, "Right aligned"));
    }
    [Fact]
    public void PptxConvertersPreserveMixedTextRuns()
    {
        using OdfPresentationDocument source = OdfPresentationDocument.Create();
        OdfSlide sourceSlide = source.AddSlide("Mixed");
        sourceSlide.AddTextBox(
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(8),
            OdfLength.FromCentimeters(2),
            "FirstSecondThirdFourth");
        ReplaceTextBoxWithStyledRuns(
            source,
            "FirstSecondThirdFourth",
            ("First", true, false, "20pt", "#CC0000", true, false, "super"),
            ("Second", false, true, "16pt", "#0066CC", false, true, "sub"),
            ("Third", false, false, "14pt", "#008855", true, true, string.Empty),
            ("Fourth", true, true, "12pt", "#8844CC", false, false, string.Empty));

        using var pptxStream = new MemoryStream();
        OdpToPptxConverter.Convert(source, pptxStream);

        pptxStream.Position = 0;
        using PackagingPresentationDocument pptx = PackagingPresentationDocument.Open(pptxStream, false);
        Assert.Empty(new OpenXmlValidator(FileFormatVersions.Office2019).Validate(pptx, TestContext.Current.CancellationToken));
        OpenXmlSlidePart slidePart = Assert.Single(pptx.PresentationPart!.SlideParts);
        A.Run[] runs = slidePart.Slide!.Descendants<A.Run>()
            .Where(run => run.GetFirstChild<A.Text>()?.Text is "First" or "Second" or "Third" or "Fourth")
            .ToArray();
        Assert.Equal(4, runs.Length);
        Assert.True(runs[0].RunProperties?.Bold?.Value);
        Assert.Equal(A.TextUnderlineValues.Single, runs[0].RunProperties?.Underline?.Value);
        Assert.True(runs[0].RunProperties?.Baseline?.Value > 0);
        Assert.Equal("CC0000", runs[0].RunProperties?.GetFirstChild<A.SolidFill>()?.GetFirstChild<A.RgbColorModelHex>()?.Val?.Value);
        Assert.True(runs[1].RunProperties?.Italic?.Value);
        Assert.Equal(A.TextStrikeValues.SingleStrike, runs[1].RunProperties?.Strike?.Value);
        Assert.True(runs[1].RunProperties?.Baseline?.Value < 0);
        Assert.Equal("0066CC", runs[1].RunProperties?.GetFirstChild<A.SolidFill>()?.GetFirstChild<A.RgbColorModelHex>()?.Val?.Value);
        Assert.Equal(A.TextUnderlineValues.Single, runs[2].RunProperties?.Underline?.Value);
        Assert.Equal(A.TextStrikeValues.SingleStrike, runs[2].RunProperties?.Strike?.Value);

        pptxStream.Position = 0;
        using OdfPresentationDocument converted = PptxToOdpConverter.Convert(pptxStream);
        OdfNode[] spans = converted.ContentDom.Descendants()
            .Where(node => node.LocalName == "span" && node.NamespaceUri == OdfNamespaces.Text && node.TextContent is "First" or "Second" or "Third" or "Fourth")
            .ToArray();
        Assert.Equal(4, spans.Length);

        string? firstStyle = spans[0].GetAttribute("style-name", OdfNamespaces.Text);
        string? secondStyle = spans[1].GetAttribute("style-name", OdfNamespaces.Text);
        string? thirdStyle = spans[2].GetAttribute("style-name", OdfNamespaces.Text);
        Assert.NotNull(firstStyle);
        Assert.NotNull(secondStyle);
        Assert.NotNull(thirdStyle);
        Assert.Equal("bold", converted.StyleEngine.GetStyleProperty(firstStyle, "font-weight", OdfNamespaces.Fo, "text"));
        Assert.Equal("solid", converted.StyleEngine.GetStyleProperty(firstStyle, "text-underline-style", OdfNamespaces.Style, "text"));
        Assert.Equal("super", converted.StyleEngine.GetStyleProperty(firstStyle, "text-position", OdfNamespaces.Style, "text"));
        Assert.Equal("#CC0000", converted.StyleEngine.GetStyleProperty(firstStyle, "color", OdfNamespaces.Fo, "text"));
        Assert.Equal("italic", converted.StyleEngine.GetStyleProperty(secondStyle, "font-style", OdfNamespaces.Fo, "text"));
        Assert.Equal("solid", converted.StyleEngine.GetStyleProperty(secondStyle, "text-line-through-style", OdfNamespaces.Style, "text"));
        Assert.Equal("sub", converted.StyleEngine.GetStyleProperty(secondStyle, "text-position", OdfNamespaces.Style, "text"));
        Assert.Equal("#0066CC", converted.StyleEngine.GetStyleProperty(secondStyle, "color", OdfNamespaces.Fo, "text"));
        Assert.Equal("solid", converted.StyleEngine.GetStyleProperty(thirdStyle, "text-underline-style", OdfNamespaces.Style, "text"));
        Assert.Equal("solid", converted.StyleEngine.GetStyleProperty(thirdStyle, "text-line-through-style", OdfNamespaces.Style, "text"));
    }

    [Fact]
    public void PptxConvertersPreserveTextBoxParagraphs()
    {
        using OdfPresentationDocument source = OdfPresentationDocument.Create();
        OdfSlide sourceSlide = source.AddSlide("Paragraphs");
        sourceSlide.AddTextBox(
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(8),
            OdfLength.FromCentimeters(3),
            new[] { "First paragraph", "Second paragraph" });

        using var pptxStream = new MemoryStream();
        OdpToPptxConverter.Convert(source, pptxStream);

        pptxStream.Position = 0;
        using (PackagingPresentationDocument pptx = PackagingPresentationDocument.Open(pptxStream, false))
        {
            Assert.Empty(new OpenXmlValidator(FileFormatVersions.Office2019).Validate(pptx, TestContext.Current.CancellationToken));
            OpenXmlSlidePart slidePart = Assert.Single(pptx.PresentationPart!.SlideParts);
            P.Shape textShape = Assert.Single(
                slidePart.Slide!.Descendants<P.Shape>(),
                shape => shape.TextBody?.Descendants<A.Text>().Any(text => text.Text == "First paragraph") == true);
            A.Paragraph[] paragraphs = textShape.TextBody!.Elements<A.Paragraph>().ToArray();
            Assert.Equal(2, paragraphs.Length);
            Assert.Equal("First paragraph", string.Concat(paragraphs[0].Descendants<A.Text>().Select(text => text.Text)));
            Assert.Equal("Second paragraph", string.Concat(paragraphs[1].Descendants<A.Text>().Select(text => text.Text)));
        }

        pptxStream.Position = 0;
        using OdfPresentationDocument converted = PptxToOdpConverter.Convert(pptxStream);
        OdfNode convertedTextBox = Assert.Single(
            converted.ContentDom.Descendants(),
            node => node.LocalName == "text-box" && node.NamespaceUri == OdfNamespaces.Draw);
        OdfNode[] convertedParagraphs = convertedTextBox.Children
            .Where(node => node.LocalName == "p" && node.NamespaceUri == OdfNamespaces.Text)
            .ToArray();
        Assert.Equal(2, convertedParagraphs.Length);
        Assert.Equal("First paragraph", convertedParagraphs[0].TextContent);
        Assert.Equal("Second paragraph", convertedParagraphs[1].TextContent);
        OdfTextBox convertedApiTextBox = Assert.Single(Assert.Single(converted.Slides).TextBoxes);
        Assert.Equal(new[] { "First paragraph", "Second paragraph" }, convertedApiTextBox.Paragraphs);
        Assert.Equal("First paragraph" + Environment.NewLine + "Second paragraph", convertedApiTextBox.Text);
    }
    [Fact]
    public void PptxConvertersPreserveEmbeddedTableText()
    {
        using OdfPresentationDocument source = OdfPresentationDocument.Create();
        OdfSlide sourceSlide = source.AddSlide("Table");
        sourceSlide
            .AddShape(
                OdfShapeType.Rectangle,
                OdfLength.FromCentimeters(1),
                OdfLength.FromCentimeters(1),
                OdfLength.FromCentimeters(8),
                OdfLength.FromCentimeters(3))
            .AddEmbeddedTable(2, 2)
            .SetTemplateName("{5C22544A-7EE6-4342-B048-85BDC9FD1C3A}")
            .SetCellText(0, 0, "Q1")
            .SetCellTextStyle(0, 0, bold: true, underline: true, textPosition: "super", fontSize: "18pt", color: "#AA5500")
            .SetCellBackgroundColor(0, 0, "#FFEE99")
            .SetCellBorder(0, 0, "0.75pt solid #336699")
            .SetCellText(0, 1, "Q2")
            .SetCellText(1, 0, "10")
            .SetCellText(1, 1, "20");

        using var pptxStream = new MemoryStream();
        OdpToPptxConverter.Convert(source, pptxStream);

        pptxStream.Position = 0;
        using PackagingPresentationDocument pptx = PackagingPresentationDocument.Open(pptxStream, false);
        Assert.Empty(new OpenXmlValidator(FileFormatVersions.Office2019).Validate(pptx, TestContext.Current.CancellationToken));
        OpenXmlSlidePart slidePart = Assert.Single(pptx.PresentationPart!.SlideParts);
        A.Table table = Assert.Single(slidePart.Slide!.Descendants<A.Table>());
        Assert.Equal(2, table.Elements<A.TableRow>().Count());
        Assert.Equal(4, table.Descendants<A.TableCell>().Count());
        Assert.Equal("{5C22544A-7EE6-4342-B048-85BDC9FD1C3A}", table.TableProperties?.GetFirstChild<A.TableStyleId>()?.Text);
        Assert.Contains(table.Descendants<A.Text>(), text => text.Text == "Q1");
        Assert.Contains(table.Descendants<A.Text>(), text => text.Text == "20");
        A.TableCell styledCell = Assert.Single(
            table.Descendants<A.TableCell>(),
            cell => cell.Descendants<A.Text>().Any(text => text.Text == "Q1"));
        Assert.Equal(
            "FFEE99",
            styledCell.TableCellProperties?.GetFirstChild<A.SolidFill>()?.GetFirstChild<A.RgbColorModelHex>()?.Val?.Value);
        Assert.Equal(
            "336699",
            styledCell.TableCellProperties?.TopBorderLineProperties?.GetFirstChild<A.SolidFill>()?.GetFirstChild<A.RgbColorModelHex>()?.Val?.Value);
        A.RunProperties? styledRunProperties = styledCell.Descendants<A.Run>().First().RunProperties;
        Assert.True(styledRunProperties?.Bold?.Value);
        Assert.Equal(A.TextUnderlineValues.Single, styledRunProperties?.Underline?.Value);
        Assert.True(styledRunProperties?.Baseline?.Value > 0);
        Assert.Equal(1800, styledRunProperties?.FontSize?.Value);
        Assert.Equal(
            "AA5500",
            styledRunProperties?.GetFirstChild<A.SolidFill>()?.GetFirstChild<A.RgbColorModelHex>()?.Val?.Value);
        Assert.Equal(9525, styledCell.TableCellProperties?.TopBorderLineProperties?.Width?.Value);
        Assert.NotNull(styledCell.TableCellProperties?.RightBorderLineProperties);
        Assert.NotNull(styledCell.TableCellProperties?.BottomBorderLineProperties);
        Assert.NotNull(styledCell.TableCellProperties?.LeftBorderLineProperties);

        pptxStream.Position = 0;
        using OdfPresentationDocument converted = PptxToOdpConverter.Convert(pptxStream);
        OdfNode convertedTable = Assert.Single(
            converted.ContentDom.Descendants(),
            node => node.LocalName == "table" && node.NamespaceUri == OdfNamespaces.Table);
        Assert.Equal("{5C22544A-7EE6-4342-B048-85BDC9FD1C3A}", convertedTable.GetAttribute("template-name", OdfNamespaces.Table));
        Assert.Contains(
            convertedTable.Descendants(),
            node => node.LocalName == "p" && node.NamespaceUri == OdfNamespaces.Text && node.TextContent == "Q2");
        Assert.Contains(
            convertedTable.Descendants(),
            node => node.LocalName == "p" && node.NamespaceUri == OdfNamespaces.Text && node.TextContent == "10");
        OdfNode convertedStyledCell = Assert.Single(
            convertedTable.Descendants(),
            node => node.LocalName == "table-cell" &&
                node.NamespaceUri == OdfNamespaces.Table &&
                node.TextContent == "Q1");
        string? convertedStyleName = convertedStyledCell.GetAttribute("style-name", OdfNamespaces.Table);
        Assert.NotNull(convertedStyleName);
        Assert.Equal(
            "#FFEE99",
            converted.StyleEngine.GetStyleProperty(convertedStyleName!, "background-color", OdfNamespaces.Fo, "table-cell"));
        Assert.Equal(
            "0.75pt solid #336699",
            converted.StyleEngine.GetStyleProperty(convertedStyleName!, "border", OdfNamespaces.Fo, "table-cell"));
        OdfNode convertedStyledParagraph = Assert.Single(
            convertedStyledCell.Children,
            node => node.LocalName == "p" && node.NamespaceUri == OdfNamespaces.Text);
        string? convertedTextStyleName = convertedStyledParagraph.GetAttribute("style-name", OdfNamespaces.Text);
        Assert.NotNull(convertedTextStyleName);
        Assert.Equal("bold", converted.StyleEngine.GetStyleProperty(convertedTextStyleName!, "font-weight", OdfNamespaces.Fo, "paragraph"));
        Assert.Equal("solid", converted.StyleEngine.GetStyleProperty(convertedTextStyleName!, "text-underline-style", OdfNamespaces.Style, "paragraph"));
        Assert.Equal("super", converted.StyleEngine.GetStyleProperty(convertedTextStyleName!, "text-position", OdfNamespaces.Style, "paragraph"));
        Assert.Equal("18pt", converted.StyleEngine.GetStyleProperty(convertedTextStyleName!, "font-size", OdfNamespaces.Fo, "paragraph"));
        Assert.Equal("#AA5500", converted.StyleEngine.GetStyleProperty(convertedTextStyleName!, "color", OdfNamespaces.Fo, "paragraph"));
    }

    [Fact]
    public void PptxConvertersPreserveEmbeddedTableSpans()
    {
        using OdfPresentationDocument source = OdfPresentationDocument.Create();
        OdfSlide sourceSlide = source.AddSlide("Merged Table");
        sourceSlide
            .AddShape(
                OdfShapeType.Rectangle,
                OdfLength.FromCentimeters(1),
                OdfLength.FromCentimeters(1),
                OdfLength.FromCentimeters(8),
                OdfLength.FromCentimeters(3))
            .AddEmbeddedTable(2, 2)
            .SetCellText(0, 0, "Merged Header")
            .SetCellSpan(0, 0, rowSpan: 2, columnSpan: 2);

        using var pptxStream = new MemoryStream();
        OdpToPptxConverter.Convert(source, pptxStream);

        pptxStream.Position = 0;
        using PackagingPresentationDocument pptx = PackagingPresentationDocument.Open(pptxStream, false);
        Assert.Empty(new OpenXmlValidator(FileFormatVersions.Office2019).Validate(pptx, TestContext.Current.CancellationToken));
        OpenXmlSlidePart slidePart = Assert.Single(pptx.PresentationPart!.SlideParts);
        A.TableCell[] cells = Assert.Single(slidePart.Slide!.Descendants<A.Table>()).Descendants<A.TableCell>().ToArray();
        Assert.Equal(4, cells.Length);
        Assert.Equal(2, cells[0].RowSpan?.Value);
        Assert.Equal(2, cells[0].GridSpan?.Value);
        Assert.True(cells[1].HorizontalMerge?.Value);
        Assert.True(cells[2].VerticalMerge?.Value);
        Assert.True(cells[3].HorizontalMerge?.Value);
        Assert.True(cells[3].VerticalMerge?.Value);

        pptxStream.Position = 0;
        using OdfPresentationDocument converted = PptxToOdpConverter.Convert(pptxStream);
        OdfNode tableCell = Assert.Single(
            converted.ContentDom.Descendants(),
            node => node.LocalName == "table-cell" &&
                node.NamespaceUri == OdfNamespaces.Table &&
                node.TextContent == "Merged Header");
        Assert.Equal("2", tableCell.GetAttribute("number-columns-spanned", OdfNamespaces.Table));
        Assert.Equal("2", tableCell.GetAttribute("number-rows-spanned", OdfNamespaces.Table));
        Assert.Equal(
            3,
            converted.ContentDom.Descendants().Count(node => node.LocalName == "covered-table-cell" && node.NamespaceUri == OdfNamespaces.Table));
    }

    [Fact]
    public void PptxToOdpConverterResolvesThemeSchemeColors()
    {
        using OdfPresentationDocument source = OdfPresentationDocument.Create();
        OdfSlide sourceSlide = source.AddSlide("Theme");
        sourceSlide.BackgroundColor = "#111111";
        sourceSlide.AddTextBox(
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(6),
            OdfLength.FromCentimeters(2),
            "Theme text");
        ApplyStyledTextBox(source, "Theme text", bold: false, italic: false, fontSize: "18pt", color: "#222222");
        sourceSlide
            .AddShape(
                OdfShapeType.Rectangle,
                OdfLength.FromCentimeters(1),
                OdfLength.FromCentimeters(4),
                OdfLength.FromCentimeters(6),
                OdfLength.FromCentimeters(2))
            .AddEmbeddedTable(1, 1)
            .SetCellText(0, 0, "Theme cell")
            .SetCellBackgroundColor(0, 0, "#333333")
            .SetCellBorder(0, 0, "0.75pt solid #444444");

        using var pptxStream = new MemoryStream();
        OdpToPptxConverter.Convert(source, pptxStream);

        pptxStream.Position = 0;
        using (PackagingPresentationDocument pptx = PackagingPresentationDocument.Open(pptxStream, true))
        {
            OpenXmlSlidePart slidePart = Assert.Single(pptx.PresentationPart!.SlideParts);
            P.Slide slide = slidePart.Slide!;
            A.SolidFill backgroundFill = slide.CommonSlideData!.Background!.BackgroundProperties!.GetFirstChild<A.SolidFill>()!;
            backgroundFill.RemoveAllChildren();
            backgroundFill.Append(new A.SchemeColor { Val = A.SchemeColorValues.Accent1 });

            A.RunProperties runProperties = Assert.Single(
                slide.Descendants<A.RunProperties>(),
                properties => properties.Parent is A.Run && properties.Parent.Descendants<A.Text>().Any(text => text.Text == "Theme text"));
            runProperties.RemoveAllChildren<A.SolidFill>();
            runProperties.Append(new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.Accent2 }));

            A.TableCell cell = Assert.Single(slide.Descendants<A.TableCell>());
            A.TableCellProperties cellProperties = cell.TableCellProperties!;
            cellProperties.RemoveAllChildren<A.SolidFill>();
            cellProperties.Append(new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.Accent3 }));
            cellProperties.TopBorderLineProperties!.RemoveAllChildren<A.SolidFill>();
            cellProperties.TopBorderLineProperties.Append(new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.Accent4 }));

            slide.Save();
        }

        pptxStream.Position = 0;
        using OdfPresentationDocument converted = PptxToOdpConverter.Convert(pptxStream);
        OdfSlide convertedSlide = Assert.Single(converted.Slides);
        Assert.Equal("#111111", convertedSlide.BackgroundColor);

        OdfNode convertedSpan = Assert.Single(
            converted.ContentDom.Descendants(),
            node => node.LocalName == "span" &&
                node.NamespaceUri == OdfNamespaces.Text &&
                node.TextContent == "Theme text");
        string? textStyleName = convertedSpan.GetAttribute("style-name", OdfNamespaces.Text);
        Assert.NotNull(textStyleName);
        Assert.Equal("#222222", converted.StyleEngine.GetStyleProperty(textStyleName!, "color", OdfNamespaces.Fo, "text"));

        OdfNode convertedCell = Assert.Single(
            converted.ContentDom.Descendants(),
            node => node.LocalName == "table-cell" &&
                node.NamespaceUri == OdfNamespaces.Table &&
                node.TextContent == "Theme cell");
        string? cellStyleName = convertedCell.GetAttribute("style-name", OdfNamespaces.Table);
        Assert.NotNull(cellStyleName);
        Assert.Equal("#333333", converted.StyleEngine.GetStyleProperty(cellStyleName!, "background-color", OdfNamespaces.Fo, "table-cell"));
        Assert.Equal("0.75pt solid #444444", converted.StyleEngine.GetStyleProperty(cellStyleName!, "border", OdfNamespaces.Fo, "table-cell"));
    }

    [Fact]
    public void OdpToPptxConverterBuildsThemePaletteFromOdfColors()
    {
        using OdfPresentationDocument source = OdfPresentationDocument.Create();
        source.AddSlide("Palette").AddTextBox(
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(6),
            OdfLength.FromCentimeters(2),
            "Palette text");
        AddThemeSourceColor(source, "fill-color", OdfNamespaces.Draw, "#13579B");
        AddThemeSourceColor(source, "stroke-color", OdfNamespaces.Svg, "#2468AC");
        AddThemeSourceColor(source, "color", OdfNamespaces.Fo, "#3579BD");

        using var pptxStream = new MemoryStream();
        OdpToPptxConverter.Convert(source, pptxStream);

        pptxStream.Position = 0;
        using PackagingPresentationDocument pptx = PackagingPresentationDocument.Open(pptxStream, false);
        Assert.Empty(new OpenXmlValidator(FileFormatVersions.Office2019).Validate(pptx, TestContext.Current.CancellationToken));
        A.ColorScheme colorScheme = Assert.Single(pptx.PresentationPart!.SlideMasterParts)
            .ThemePart!
            .Theme!
            .ThemeElements!
            .ColorScheme!;
        Assert.Equal("13579B", colorScheme.Accent1Color?.GetFirstChild<A.RgbColorModelHex>()?.Val?.Value);
        Assert.Equal("2468AC", colorScheme.Accent2Color?.GetFirstChild<A.RgbColorModelHex>()?.Val?.Value);
        Assert.Equal("3579BD", colorScheme.Accent3Color?.GetFirstChild<A.RgbColorModelHex>()?.Val?.Value);
    }

    [Fact]
    public void PptxToOdpConverterInheritsLayoutBackground()
    {
        using OdfPresentationDocument source = OdfPresentationDocument.Create();
        OdfSlide sourceSlide = source.AddSlide("Inherited Background");
        sourceSlide.BackgroundColor = "#101010";

        using var pptxStream = new MemoryStream();
        OdpToPptxConverter.Convert(source, pptxStream);

        pptxStream.Position = 0;
        using (PackagingPresentationDocument pptx = PackagingPresentationDocument.Open(pptxStream, true))
        {
            OpenXmlSlidePart slidePart = pptx.PresentationPart!.SlideParts.Single();
            DocumentFormat.OpenXml.Packaging.SlideLayoutPart layoutPart =
                slidePart.SlideLayoutPart ?? slidePart.AddNewPart<DocumentFormat.OpenXml.Packaging.SlideLayoutPart>();
            slidePart.Slide!.CommonSlideData!.Background = null;
            layoutPart.SlideLayout ??= new P.SlideLayout();
            EnsureCommonSlideData(layoutPart.SlideLayout);
            layoutPart.SlideLayout.CommonSlideData!.Background = new P.Background(
                new P.BackgroundProperties(
                    new A.SolidFill(new A.RgbColorModelHex { Val = "13579B" })));

            slidePart.Slide.Save();
            layoutPart.SlideLayout.Save();
        }

        pptxStream.Position = 0;
        using (PackagingPresentationDocument pptx = PackagingPresentationDocument.Open(pptxStream, false))
        {
            Assert.Empty(new OpenXmlValidator(FileFormatVersions.Office2019).Validate(pptx, TestContext.Current.CancellationToken));
        }

        pptxStream.Position = 0;
        using OdfPresentationDocument converted = PptxToOdpConverter.Convert(pptxStream);
        OdfSlide convertedSlide = Assert.Single(converted.Slides);
        Assert.Equal("#13579B", convertedSlide.BackgroundColor);
    }
    private static void EnsureCommonSlideData(P.SlideLayout slideLayout)
    {
        slideLayout.CommonSlideData ??= new P.CommonSlideData(CreateEmptyShapeTree());
    }


    private static P.ShapeTree CreateEmptyShapeTree()
        => new(
            new P.NonVisualGroupShapeProperties(
                new P.NonVisualDrawingProperties { Id = 1U, Name = string.Empty },
                new P.NonVisualGroupShapeDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.GroupShapeProperties(new A.TransformGroup()));
    [Fact]
    public void PptxToOdpConverterResolvesThemeShapeStyleReferences()
    {
        using OdfPresentationDocument source = OdfPresentationDocument.Create();
        OdfSlide sourceSlide = source.AddSlide("Theme Style");
        sourceSlide.AddShape(
            OdfShapeType.Rectangle,
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(4),
            OdfLength.FromCentimeters(2));
        AddThemeSourceColor(source, "fill-color", OdfNamespaces.Draw, "#13579B");
        AddThemeSourceColor(source, "stroke-color", OdfNamespaces.Svg, "#2468AC");
        AddThemeSourceColor(source, "color", OdfNamespaces.Fo, "#3579BD");

        using var pptxStream = new MemoryStream();
        OdpToPptxConverter.Convert(source, pptxStream);

        pptxStream.Position = 0;
        using (PackagingPresentationDocument pptx = PackagingPresentationDocument.Open(pptxStream, true))
        {
            OpenXmlSlidePart slidePart = pptx.PresentationPart!.SlideParts.Single();
            P.Shape shape = Assert.Single(
                slidePart.Slide!.Descendants<P.Shape>(),
                value => value.ShapeProperties?.GetFirstChild<A.PresetGeometry>()?.Preset?.Value == A.ShapeTypeValues.Rectangle);
            shape.ShapeProperties!.RemoveAllChildren<A.SolidFill>();
            shape.ShapeProperties.RemoveAllChildren<A.Outline>();
            shape.ShapeStyle = new P.ShapeStyle(
                new A.LineReference(new A.SchemeColor { Val = A.SchemeColorValues.PhColor }) { Index = 2U },
                new A.FillReference(new A.SchemeColor { Val = A.SchemeColorValues.PhColor }) { Index = 1U },
                new A.EffectReference(new A.SchemeColor { Val = A.SchemeColorValues.Accent3 }) { Index = 0U },
                new A.FontReference(new A.SchemeColor { Val = A.SchemeColorValues.Text1 }) { Index = A.FontCollectionIndexValues.Minor });

            A.FormatScheme formatScheme = Assert.Single(pptx.PresentationPart!.SlideMasterParts)
                .ThemePart!
                .Theme!
                .ThemeElements!
                .FormatScheme!;
            formatScheme.FillStyleList!.RemoveAllChildren();
            formatScheme.FillStyleList.Append(
                new A.SolidFill(new A.RgbColorModelHex { Val = "13579B" }),
                new A.SolidFill(new A.RgbColorModelHex { Val = "AAAAAA" }),
                new A.SolidFill(new A.RgbColorModelHex { Val = "BBBBBB" }));
            formatScheme.LineStyleList!.RemoveAllChildren();
            formatScheme.LineStyleList.Append(
                new A.Outline(new A.SolidFill(new A.RgbColorModelHex { Val = "111111" })) { Width = 9525 },
                new A.Outline(new A.SolidFill(new A.RgbColorModelHex { Val = "2468AC" })) { Width = 25400 },
                new A.Outline(new A.SolidFill(new A.RgbColorModelHex { Val = "333333" })) { Width = 38100 });
            slidePart.Slide!.Save();
            pptx.PresentationPart!.SlideMasterParts.Single().ThemePart!.Theme!.Save();
        }

        pptxStream.Position = 0;
        using (PackagingPresentationDocument pptx = PackagingPresentationDocument.Open(pptxStream, false))
        {
            Assert.Empty(new OpenXmlValidator(FileFormatVersions.Office2019).Validate(pptx, TestContext.Current.CancellationToken));
        }

        pptxStream.Position = 0;
        using OdfPresentationDocument converted = PptxToOdpConverter.Convert(pptxStream);
        OdfShape convertedShape = Assert.Single(Assert.Single(converted.Slides).Shapes, shape => shape.LocalName == "rect");
        Assert.Equal("#13579B", convertedShape.FillColor);
        Assert.Equal("#2468AC", convertedShape.StrokeColor);
        Assert.Equal("2pt", convertedShape.StrokeWidth);
    }
    [Fact]
    public void PptxToOdpConverterResolvesThemeBackgroundStyleReference()
    {
        using OdfPresentationDocument source = OdfPresentationDocument.Create();
        OdfSlide sourceSlide = source.AddSlide("Theme Background");
        sourceSlide.BackgroundColor = "#101010";
        AddThemeSourceColor(source, "fill-color", OdfNamespaces.Draw, "#13579B");

        using var pptxStream = new MemoryStream();
        OdpToPptxConverter.Convert(source, pptxStream);

        pptxStream.Position = 0;
        using (PackagingPresentationDocument pptx = PackagingPresentationDocument.Open(pptxStream, true))
        {
            OpenXmlSlidePart slidePart = pptx.PresentationPart!.SlideParts.Single();
            P.Background background = slidePart.Slide!.CommonSlideData!.Background!;
            background.RemoveAllChildren<P.BackgroundProperties>();
            background.RemoveAllChildren<P.BackgroundStyleReference>();
            background.Append(new P.BackgroundStyleReference(new A.SchemeColor { Val = A.SchemeColorValues.PhColor }) { Index = 1001U });

            A.BackgroundFillStyleList backgroundFillStyleList = Assert.Single(pptx.PresentationPart!.SlideMasterParts)
                .ThemePart!
                .Theme!
                .ThemeElements!
                .FormatScheme!
                .BackgroundFillStyleList!;
            backgroundFillStyleList.RemoveAllChildren();
            backgroundFillStyleList.Append(
                new A.SolidFill(new A.RgbColorModelHex { Val = "13579B" }),
                new A.SolidFill(new A.RgbColorModelHex { Val = "2468AC" }),
                new A.SolidFill(new A.RgbColorModelHex { Val = "3579BD" }));
            slidePart.Slide.Save();
            pptx.PresentationPart!.SlideMasterParts.Single().ThemePart!.Theme!.Save();
        }

        pptxStream.Position = 0;
        using (PackagingPresentationDocument pptx = PackagingPresentationDocument.Open(pptxStream, false))
        {
            Assert.Empty(new OpenXmlValidator(FileFormatVersions.Office2019).Validate(pptx, TestContext.Current.CancellationToken));
        }

        pptxStream.Position = 0;
        using OdfPresentationDocument converted = PptxToOdpConverter.Convert(pptxStream);
        OdfSlide convertedSlide = Assert.Single(converted.Slides);
        Assert.Equal("#13579B", convertedSlide.BackgroundColor);
    }
    [Fact]
    public void OdpToPptxConverterBuildsThemeFontSchemeFromOdfFonts()
    {
        using OdfPresentationDocument source = OdfPresentationDocument.Create();
        source.AddSlide("Fonts").AddTextBox(
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(6),
            OdfLength.FromCentimeters(2),
            "Theme font");
        ApplyTextBoxFontFamily(source, "Theme font", "'Aptos Display', sans-serif");
        AddThemeSourceFont(source, "font-name", OdfNamespaces.Style, "Aptos");
        AddThemeSourceFont(source, "font-family-asian", OdfNamespaces.Style, "Yu Gothic");
        AddThemeSourceFont(source, "font-family-complex", OdfNamespaces.Style, "Noto Naskh Arabic");

        using var pptxStream = new MemoryStream();
        OdpToPptxConverter.Convert(source, pptxStream);

        pptxStream.Position = 0;
        using PackagingPresentationDocument pptx = PackagingPresentationDocument.Open(pptxStream, false);
        Assert.Empty(new OpenXmlValidator(FileFormatVersions.Office2019).Validate(pptx, TestContext.Current.CancellationToken));
        A.FontScheme fontScheme = Assert.Single(pptx.PresentationPart!.SlideMasterParts)
            .ThemePart!
            .Theme!
            .ThemeElements!
            .FontScheme!;
        Assert.Equal("Aptos Display", fontScheme.MajorFont!.LatinFont!.Typeface!.Value);
        Assert.Equal("Aptos", fontScheme.MinorFont!.LatinFont!.Typeface!.Value);
        Assert.Equal("Yu Gothic", fontScheme.MajorFont!.EastAsianFont!.Typeface!.Value);
        Assert.Equal("Noto Naskh Arabic", fontScheme.MajorFont!.ComplexScriptFont!.Typeface!.Value);

        A.RunProperties runProperties = Assert.Single(
            pptx.PresentationPart!.SlideParts.Single().Slide!.Descendants<A.RunProperties>(),
            properties => properties.Parent is A.Run && properties.Parent.Descendants<A.Text>().Any(text => text.Text == "Theme font"));
        Assert.Equal("Aptos Display", runProperties.GetFirstChild<A.LatinFont>()?.Typeface?.Value);
    }

    [Fact]
    public void PptxToOdpConverterResolvesThemeFontSchemeTokens()
    {
        using OdfPresentationDocument source = OdfPresentationDocument.Create();
        source.AddSlide("Fonts").AddTextBox(
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(6),
            OdfLength.FromCentimeters(2),
            "Theme font import");
        ApplyTextBoxFontFamily(source, "Theme font import", "Aptos Display");
        AddThemeSourceFont(source, "font-name", OdfNamespaces.Style, "Aptos");

        using var pptxStream = new MemoryStream();
        OdpToPptxConverter.Convert(source, pptxStream);

        pptxStream.Position = 0;
        using (PackagingPresentationDocument pptx = PackagingPresentationDocument.Open(pptxStream, true))
        {
            A.RunProperties runProperties = Assert.Single(
                pptx.PresentationPart!.SlideParts.Single().Slide!.Descendants<A.RunProperties>(),
                properties => properties.Parent is A.Run && properties.Parent.Descendants<A.Text>().Any(text => text.Text == "Theme font import"));
            runProperties.RemoveAllChildren<A.LatinFont>();
            runProperties.RemoveAllChildren<A.EastAsianFont>();
            runProperties.RemoveAllChildren<A.ComplexScriptFont>();
            runProperties.Append(new A.LatinFont { Typeface = "+mj-lt" });
            runProperties.Append(new A.EastAsianFont { Typeface = "+mj-ea" });
            runProperties.Append(new A.ComplexScriptFont { Typeface = "+mj-cs" });
            runProperties.Ancestors<P.Slide>().Single().Save();
        }

        pptxStream.Position = 0;
        using OdfPresentationDocument converted = PptxToOdpConverter.Convert(pptxStream);
        OdfNode span = Assert.Single(
            converted.ContentDom.Descendants(),
            node => node.LocalName == "span" &&
                node.NamespaceUri == OdfNamespaces.Text &&
                node.TextContent == "Theme font import");
        string? styleName = span.GetAttribute("style-name", OdfNamespaces.Text);
        Assert.NotNull(styleName);
        Assert.Equal("Aptos Display", converted.StyleEngine.GetStyleProperty(styleName!, "font-family", OdfNamespaces.Fo, "text"));
    }

    [Fact]
    public void PptxConvertersPreservePictureAltText()
    {
        using OdfPresentationDocument source = OdfPresentationDocument.Create();
        OdfSlide sourceSlide = source.AddSlide("Pictures");
        OdfPicture picture = sourceSlide.AddPicture(
            Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII="),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(3),
            OdfLength.FromCentimeters(2),
            altText: "Revenue chart");
        Assert.Equal("Revenue chart", picture.AltText);

        using var pptxStream = new MemoryStream();
        OdpToPptxConverter.Convert(source, pptxStream);

        pptxStream.Position = 0;
        using (PackagingPresentationDocument pptx = PackagingPresentationDocument.Open(pptxStream, false))
        {
            Assert.Empty(new OpenXmlValidator(FileFormatVersions.Office2019).Validate(pptx, TestContext.Current.CancellationToken));
            OpenXmlSlidePart slidePart = Assert.Single(pptx.PresentationPart!.SlideParts);
            P.Picture pptxPicture = Assert.Single(slidePart.Slide!.Descendants<P.Picture>());
            Assert.Equal("Revenue chart", pptxPicture.NonVisualPictureProperties?.NonVisualDrawingProperties?.Description?.Value);
        }

        pptxStream.Position = 0;
        using OdfPresentationDocument converted = PptxToOdpConverter.Convert(pptxStream);
        OdfPicture roundTrippedPicture = Assert.Single(Assert.Single(converted.Slides).Pictures);
        Assert.Equal("Revenue chart", roundTrippedPicture.AltText);
        Assert.Contains(
            converted.ContentDom.Descendants(),
            node => node.LocalName == "desc" &&
                node.NamespaceUri == OdfNamespaces.Svg &&
                node.TextContent == "Revenue chart");
    }
    [Fact]
    public void PptxConvertersPreservePictureCrop()
    {
        using OdfPresentationDocument source = OdfPresentationDocument.Create();
        OdfSlide sourceSlide = source.AddSlide("Picture Crop");
        OdfPicture picture = sourceSlide.AddPicture(
            Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII="),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(4),
            OdfLength.FromCentimeters(3));
        picture.SetCrop(
            OdfLength.FromCentimeters(0.3),
            OdfLength.FromCentimeters(3),
            OdfLength.FromCentimeters(2.4),
            OdfLength.FromCentimeters(0.8));

        using var pptxStream = new MemoryStream();
        OdpToPptxConverter.Convert(source, pptxStream);

        pptxStream.Position = 0;
        using (PackagingPresentationDocument pptx = PackagingPresentationDocument.Open(pptxStream, false))
        {
            Assert.Empty(new OpenXmlValidator(FileFormatVersions.Office2019).Validate(pptx, TestContext.Current.CancellationToken));
            OpenXmlSlidePart slidePart = Assert.Single(pptx.PresentationPart!.SlideParts);
            A.SourceRectangle sourceRectangle = Assert.Single(slidePart.Slide!.Descendants<A.SourceRectangle>());
            Assert.Equal(20000, sourceRectangle.Left?.Value);
            Assert.Equal(10000, sourceRectangle.Top?.Value);
            Assert.Equal(25000, sourceRectangle.Right?.Value);
            Assert.Equal(20000, sourceRectangle.Bottom?.Value);
        }

        pptxStream.Position = 0;
        using OdfPresentationDocument converted = PptxToOdpConverter.Convert(pptxStream);
        OdfPicture roundTrippedPicture = Assert.Single(Assert.Single(converted.Slides).Pictures);
        OdfLength[] crop = ReadClipLengths(roundTrippedPicture.CropClip);
        AssertLengthEquals(OdfLength.FromCentimeters(0.3), crop[0].ToString());
        AssertLengthEquals(OdfLength.FromCentimeters(3), crop[1].ToString());
        AssertLengthEquals(OdfLength.FromCentimeters(2.4), crop[2].ToString());
        AssertLengthEquals(OdfLength.FromCentimeters(0.8), crop[3].ToString());

        // 驗證 ClearCrop 清除裁切後，OOXML 轉換不應殘留過期的 a:srcRect 來源矩形。
        roundTrippedPicture.ClearCrop();
        Assert.Null(roundTrippedPicture.CropClip);

        using var clearedPptxStream = new MemoryStream();
        OdpToPptxConverter.Convert(converted, clearedPptxStream);
        clearedPptxStream.Position = 0;
        using PackagingPresentationDocument clearedPptx = PackagingPresentationDocument.Open(clearedPptxStream, false);
        Assert.Empty(new OpenXmlValidator(FileFormatVersions.Office2019).Validate(clearedPptx, TestContext.Current.CancellationToken));
        OpenXmlSlidePart clearedSlidePart = Assert.Single(clearedPptx.PresentationPart!.SlideParts);
        Assert.Empty(clearedSlidePart.Slide!.Descendants<A.SourceRectangle>());
    }
    [Fact]
    public void PptxConvertersPreserveShapeFillAndStrokeColors()
    {
        using OdfPresentationDocument source = OdfPresentationDocument.Create();
        OdfSlide sourceSlide = source.AddSlide("Shape Style");
        OdfShape shape = sourceSlide.AddShape(
            OdfShapeType.Rectangle,
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(4),
            OdfLength.FromCentimeters(2));
        shape.FillColor = "#66AA33";
        shape.StrokeColor = "#224466";

        using var pptxStream = new MemoryStream();
        OdpToPptxConverter.Convert(source, pptxStream);

        pptxStream.Position = 0;
        using (PackagingPresentationDocument pptx = PackagingPresentationDocument.Open(pptxStream, false))
        {
            Assert.Empty(new OpenXmlValidator(FileFormatVersions.Office2019).Validate(pptx, TestContext.Current.CancellationToken));
            OpenXmlSlidePart slidePart = Assert.Single(pptx.PresentationPart!.SlideParts);
            P.Shape convertedShape = Assert.Single(
                slidePart.Slide!.Descendants<P.Shape>(),
                candidate => candidate.ShapeProperties?.GetFirstChild<A.PresetGeometry>()?.Preset?.Value == A.ShapeTypeValues.Rectangle);
            Assert.Equal(
                "66AA33",
                convertedShape.ShapeProperties?.GetFirstChild<A.SolidFill>()?.GetFirstChild<A.RgbColorModelHex>()?.Val?.Value);
            Assert.Equal(
                "224466",
                convertedShape.ShapeProperties?.GetFirstChild<A.Outline>()?.GetFirstChild<A.SolidFill>()?.GetFirstChild<A.RgbColorModelHex>()?.Val?.Value);
        }

        pptxStream.Position = 0;
        using OdfPresentationDocument converted = PptxToOdpConverter.Convert(pptxStream);
        OdfShape roundTrippedShape = Assert.Single(Assert.Single(converted.Slides).Shapes, candidate => candidate.LocalName == "rect");
        Assert.Equal("#66AA33", roundTrippedShape.FillColor);
        Assert.Equal("#224466", roundTrippedShape.StrokeColor);
    }

    [Fact]
    public void OdpToPptxConverterWritesShapeShadowEffects()
    {
        using OdfPresentationDocument source = OdfPresentationDocument.Create();
        OdfSlide sourceSlide = source.AddSlide("Shape Shadow");
        OdfShape shape = sourceSlide.AddShape(
            OdfShapeType.Rectangle,
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(4),
            OdfLength.FromCentimeters(2));
        shape.FillColor = "#66AA33";
        source.StyleEngine.SetLocalStyleProperty(shape.Node, "graphic", "graphic-properties", "shadow", OdfNamespaces.Draw, "visible", "draw");
        source.StyleEngine.SetLocalStyleProperty(shape.Node, "graphic", "graphic-properties", "shadow-color", OdfNamespaces.Draw, "#336699", "draw");
        source.StyleEngine.SetLocalStyleProperty(shape.Node, "graphic", "graphic-properties", "shadow-offset-x", OdfNamespaces.Draw, "3pt", "draw");
        source.StyleEngine.SetLocalStyleProperty(shape.Node, "graphic", "graphic-properties", "shadow-offset-y", OdfNamespaces.Draw, "4pt", "draw");
        source.StyleEngine.SetLocalStyleProperty(shape.Node, "graphic", "graphic-properties", "shadow-opacity", OdfNamespaces.Draw, "65%", "draw");

        using var pptxStream = new MemoryStream();
        OdpToPptxConverter.Convert(source, pptxStream);

        pptxStream.Position = 0;
        using PackagingPresentationDocument pptx = PackagingPresentationDocument.Open(pptxStream, false);
        Assert.Empty(new OpenXmlValidator(FileFormatVersions.Office2019).Validate(pptx, TestContext.Current.CancellationToken));
        OpenXmlSlidePart slidePart = Assert.Single(pptx.PresentationPart!.SlideParts);
        P.Shape convertedShape = Assert.Single(
            slidePart.Slide!.Descendants<P.Shape>(),
            candidate => candidate.ShapeProperties?.GetFirstChild<A.PresetGeometry>()?.Preset?.Value == A.ShapeTypeValues.Rectangle);
        A.OuterShadow shadow = Assert.Single(convertedShape.ShapeProperties!.Descendants<A.OuterShadow>());
        A.RgbColorModelHex color = Assert.IsType<A.RgbColorModelHex>(Assert.Single(shadow.ChildElements, child => child is A.RgbColorModelHex));
        Assert.Equal("336699", color.Val?.Value);
        Assert.Equal(65000, Assert.Single(color.Elements<A.Alpha>()).Val?.Value);
        Assert.Equal(63500L, shadow.Distance?.Value);
        Assert.InRange(shadow.Direction?.Value ?? 0L, 3187000L, 3189000L);

        pptxStream.Position = 0;
        using OdfPresentationDocument converted = PptxToOdpConverter.Convert(pptxStream);
        OdfShape roundTrippedShape = Assert.Single(Assert.Single(converted.Slides).Shapes, candidate => candidate.LocalName == "rect");
        string styleName = roundTrippedShape.Node.GetAttribute("style-name", OdfNamespaces.Draw) ?? string.Empty;
        Assert.Equal("visible", converted.StyleEngine.GetStyleProperty(styleName, "shadow", OdfNamespaces.Draw, "graphic"));
        Assert.Equal("#336699", converted.StyleEngine.GetStyleProperty(styleName, "shadow-color", OdfNamespaces.Draw, "graphic"));
        AssertLengthEquals(OdfLength.FromPoints(3), converted.StyleEngine.GetStyleProperty(styleName, "shadow-offset-x", OdfNamespaces.Draw, "graphic"));
        AssertLengthEquals(OdfLength.FromPoints(4), converted.StyleEngine.GetStyleProperty(styleName, "shadow-offset-y", OdfNamespaces.Draw, "graphic"));
        Assert.Equal("65%", converted.StyleEngine.GetStyleProperty(styleName, "shadow-opacity", OdfNamespaces.Draw, "graphic"));
    }

    [Fact]
    public void PptxConvertersPreserveTextOnShapes()
    {
        using OdfPresentationDocument source = OdfPresentationDocument.Create();
        OdfSlide sourceSlide = source.AddSlide("Shape Text");
        OdfShape shape = sourceSlide.AddShape(
            OdfShapeType.Rectangle,
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(5),
            OdfLength.FromCentimeters(2));
        shape.FillColor = "#CCEEFF";
        shape.Node.AppendChild(new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text") { TextContent = "Shape label" });

        using var pptxStream = new MemoryStream();
        OdpToPptxConverter.Convert(source, pptxStream);

        pptxStream.Position = 0;
        using (PackagingPresentationDocument pptx = PackagingPresentationDocument.Open(pptxStream, false))
        {
            Assert.Empty(new OpenXmlValidator(FileFormatVersions.Office2019).Validate(pptx, TestContext.Current.CancellationToken));
            OpenXmlSlidePart slidePart = Assert.Single(pptx.PresentationPart!.SlideParts);
            P.Shape pptxShape = Assert.Single(
                slidePart.Slide!.Descendants<P.Shape>(),
                candidate => candidate.ShapeProperties?.GetFirstChild<A.PresetGeometry>()?.Preset?.Value == A.ShapeTypeValues.Rectangle &&
                    candidate.TextBody?.Descendants<A.Text>().Any(text => text.Text == "Shape label") == true);
            Assert.Equal("CCEEFF", pptxShape.ShapeProperties?.GetFirstChild<A.SolidFill>()?.GetFirstChild<A.RgbColorModelHex>()?.Val?.Value);
        }

        pptxStream.Position = 0;
        using OdfPresentationDocument converted = PptxToOdpConverter.Convert(pptxStream);
        OdfSlide convertedSlide = Assert.Single(converted.Slides);
        Assert.Empty(convertedSlide.TextBoxes);
        OdfShape roundTrippedShape = Assert.Single(convertedSlide.Shapes, candidate => candidate.LocalName == "rect");
        Assert.Equal("Shape label", roundTrippedShape.Node.TextContent);
        Assert.Equal("#CCEEFF", roundTrippedShape.FillColor);
    }
    [Fact]
    public void PptxConvertersPreserveLineStrokeColors()
    {
        using OdfPresentationDocument source = OdfPresentationDocument.Create();
        OdfSlide sourceSlide = source.AddSlide("Line Style");
        OdfShape line = sourceSlide.AddLine(
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(5),
            OdfLength.FromCentimeters(3));
        line.StrokeColor = "#CC5500";
        line.StrokeWidth = "2pt";
        line.StrokeStyle = "dash";

        using var pptxStream = new MemoryStream();
        OdpToPptxConverter.Convert(source, pptxStream);

        pptxStream.Position = 0;
        using (PackagingPresentationDocument pptx = PackagingPresentationDocument.Open(pptxStream, false))
        {
            Assert.Empty(new OpenXmlValidator(FileFormatVersions.Office2019).Validate(pptx, TestContext.Current.CancellationToken));
            OpenXmlSlidePart slidePart = Assert.Single(pptx.PresentationPart!.SlideParts);
            P.Shape pptxLine = Assert.Single(
                slidePart.Slide!.Descendants<P.Shape>(),
                candidate => candidate.ShapeProperties?.GetFirstChild<A.PresetGeometry>()?.Preset?.Value == A.ShapeTypeValues.Line);
            Assert.Equal(
                "CC5500",
                pptxLine.ShapeProperties?.GetFirstChild<A.Outline>()?.GetFirstChild<A.SolidFill>()?.GetFirstChild<A.RgbColorModelHex>()?.Val?.Value);
            Assert.Equal(25400, pptxLine.ShapeProperties?.GetFirstChild<A.Outline>()?.Width?.Value);
            Assert.Equal(A.PresetLineDashValues.Dash, pptxLine.ShapeProperties?.GetFirstChild<A.Outline>()?.GetFirstChild<A.PresetDash>()?.Val?.Value);
        }

        pptxStream.Position = 0;
        using OdfPresentationDocument converted = PptxToOdpConverter.Convert(pptxStream);
        OdfShape roundTrippedLine = Assert.Single(Assert.Single(converted.Slides).Shapes, candidate => candidate.LocalName == "line");
        Assert.Equal("#CC5500", roundTrippedLine.StrokeColor);
        Assert.Equal("2pt", roundTrippedLine.StrokeWidth);
        Assert.Equal("dash", roundTrippedLine.StrokeStyle);
    }

    [Fact]
    public void PptxConvertersPreserveLineDirection()
    {
        using OdfPresentationDocument source = OdfPresentationDocument.Create();
        OdfSlide sourceSlide = source.AddSlide("Line Direction");
        sourceSlide.AddLine(
            OdfLength.FromCentimeters(5),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(3));

        using var pptxStream = new MemoryStream();
        OdpToPptxConverter.Convert(source, pptxStream);

        pptxStream.Position = 0;
        using (PackagingPresentationDocument pptx = PackagingPresentationDocument.Open(pptxStream, false))
        {
            Assert.Empty(new OpenXmlValidator(FileFormatVersions.Office2019).Validate(pptx, TestContext.Current.CancellationToken));
            OpenXmlSlidePart slidePart = Assert.Single(pptx.PresentationPart!.SlideParts);
            A.Transform2D transform = Assert.Single(
                slidePart.Slide!.Descendants<P.Shape>(),
                candidate => candidate.ShapeProperties?.GetFirstChild<A.PresetGeometry>()?.Preset?.Value == A.ShapeTypeValues.Line)
                .ShapeProperties!
                .GetFirstChild<A.Transform2D>()!;
            Assert.True(transform.HorizontalFlip?.Value);
            Assert.False(transform.VerticalFlip?.Value ?? false);
        }

        pptxStream.Position = 0;
        using OdfPresentationDocument converted = PptxToOdpConverter.Convert(pptxStream);
        OdfNode line = Assert.Single(
            converted.ContentDom.Descendants(),
            node => node.LocalName == "line" && node.NamespaceUri == OdfNamespaces.Draw);
        AssertLengthEquals(OdfLength.FromCentimeters(5), line.GetAttribute("x1", OdfNamespaces.Svg));
        AssertLengthEquals(OdfLength.FromCentimeters(1), line.GetAttribute("y1", OdfNamespaces.Svg));
        AssertLengthEquals(OdfLength.FromCentimeters(1), line.GetAttribute("x2", OdfNamespaces.Svg));
        AssertLengthEquals(OdfLength.FromCentimeters(3), line.GetAttribute("y2", OdfNamespaces.Svg));
    }
    [Fact]
    public void PptxConvertersPreserveSlideTransitions()
    {
        using OdfPresentationDocument source = OdfPresentationDocument.Create();
        OdfSlide sourceSlide = source.AddSlide("Transition");
        sourceSlide.SetTransition(OdfTransitionType.Fade, OdfLength.FromPoints(180), OdfTransitionSpeed.Fast);
        sourceSlide.AddTextBox(
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(6),
            OdfLength.FromCentimeters(2),
            "Transition text");

        using var pptxStream = new MemoryStream();
        OdpToPptxConverter.Convert(source, pptxStream);

        pptxStream.Position = 0;
        using PackagingPresentationDocument pptx = PackagingPresentationDocument.Open(pptxStream, false);
        Assert.Empty(new OpenXmlValidator(FileFormatVersions.Office2019).Validate(pptx, TestContext.Current.CancellationToken));
        OpenXmlSlidePart slidePart = Assert.Single(pptx.PresentationPart!.SlideParts);
        P.Transition transition = Assert.Single(slidePart.Slide!.Elements<P.Transition>());
        Assert.Equal("2500", transition.Duration?.Value);
        Assert.Equal(P.TransitionSpeedValues.Fast, transition.Speed?.Value);
        Assert.IsType<P.FadeTransition>(Assert.Single(transition.ChildElements));

        pptxStream.Position = 0;
        using OdfPresentationDocument converted = PptxToOdpConverter.Convert(pptxStream);
        Assert.Equal(OdfSlideTransition.Fade, converted.GetSlideTransition(0));
        OdfNode convertedSlide = Assert.Single(
            converted.ContentDom.Descendants(),
            node => node.LocalName == "page" && node.NamespaceUri == OdfNamespaces.Draw);
        string? styleName = convertedSlide.GetAttribute("style-name", OdfNamespaces.Draw);
        Assert.NotNull(styleName);
        Assert.Equal("fade", converted.StyleEngine.GetStyleProperty(styleName!, "type", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "drawing-page"));
        Assert.Equal("PT2.50S", converted.StyleEngine.GetStyleProperty(styleName!, "duration", OdfNamespaces.Presentation, "drawing-page"));
        Assert.Equal("fast", converted.StyleEngine.GetStyleProperty(styleName!, "transition-speed", OdfNamespaces.Presentation, "drawing-page"));
    }

    [Fact]
    public void PptxConvertersPreserveBasicObjectAnimations()
    {
        using OdfPresentationDocument source = OdfPresentationDocument.Create();
        OdfSlide sourceSlide = source.AddSlide("Animations");
        OdfShape shape = sourceSlide.AddShape(
            OdfShapeType.Rectangle,
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(4),
            OdfLength.FromCentimeters(2));
        sourceSlide.AddEntranceEffect(
            shape.Id,
            OdfAnimationEffect.Fade,
            OdfAnimationTrigger.OnClick,
            delay: TimeSpan.FromMilliseconds(150),
            duration: TimeSpan.FromMilliseconds(750));
        sourceSlide.AddExitEffect(
            shape.Id,
            OdfAnimationEffect.Zoom,
            OdfAnimationTrigger.AfterPrevious,
            delay: TimeSpan.FromMilliseconds(400),
            duration: TimeSpan.FromMilliseconds(250));

        using var pptxStream = new MemoryStream();
        OdpToPptxConverter.Convert(source, pptxStream);

        pptxStream.Position = 0;
        using (PackagingPresentationDocument pptx = PackagingPresentationDocument.Open(pptxStream, false))
        {
            Assert.Empty(new OpenXmlValidator(FileFormatVersions.Office2019).Validate(pptx, TestContext.Current.CancellationToken));
            OpenXmlSlidePart slidePart = Assert.Single(pptx.PresentationPart!.SlideParts);
            P.ParallelTimeNode timingRoot = Assert.Single(
                slidePart.Slide!.Timing!.Descendants<P.ParallelTimeNode>(),
                node => node.CommonTimeNode?.NodeType?.Value == P.TimeNodeValues.TmingRoot);
            Assert.Equal(P.TimeNodeRestartValues.Always, timingRoot.CommonTimeNode?.Restart?.Value);
            P.SequenceTimeNode mainSequence = Assert.Single(slidePart.Slide!.Timing!.Descendants<P.SequenceTimeNode>());
            Assert.True(mainSequence.Concurrent?.Value);
            Assert.Equal(P.PreviousActionValues.SkipTimed, mainSequence.PreviousAction?.Value);
            Assert.Equal(P.NextActionValues.Seek, mainSequence.NextAction?.Value);
            P.AnimateEffect[] effects = slidePart.Slide!.Descendants<P.AnimateEffect>().ToArray();
            Assert.Equal(2, effects.Length);
            Assert.Contains(effects, effect =>
                effect.Transition?.Value == P.AnimateEffectTransitionValues.In &&
                effect.Filter?.Value == "fade" &&
                effect.CommonBehavior?.CommonTimeNode?.Duration?.Value == "750" &&
                GetAnimationDelay(effect) == "150");
            Assert.Contains(effects, effect =>
                effect.Transition?.Value == P.AnimateEffectTransitionValues.Out &&
                effect.Filter?.Value == "zoom" &&
                effect.CommonBehavior?.CommonTimeNode?.Duration?.Value == "250" &&
                GetAnimationDelay(effect) == "400");
            Assert.All(effects, effect => Assert.False(string.IsNullOrWhiteSpace(effect.CommonBehavior?.TargetElement?.ShapeTarget?.ShapeId?.Value)));
            P.BuildParagraph buildParagraph = Assert.Single(slidePart.Slide!.Timing!.BuildList!.Elements<P.BuildParagraph>());
            string targetShapeId = Assert.Single(effects.Select(effect => effect.CommonBehavior?.TargetElement?.ShapeTarget?.ShapeId?.Value).Distinct())!;
            Assert.Equal(targetShapeId, buildParagraph.ShapeId?.Value);
            Assert.Equal(P.ParagraphBuildValues.Whole, buildParagraph.Build?.Value);
            Assert.Equal(1U, buildParagraph.BuildLevel?.Value);
        }

        pptxStream.Position = 0;
        using OdfPresentationDocument converted = PptxToOdpConverter.Convert(pptxStream);
        OdfAnimationInfo[] animations = Assert.Single(converted.Slides).GetAnimations().ToArray();
        Assert.Equal(2, animations.Length);
        Assert.Contains(
            animations,
            animation => animation.Kind == OdfAnimationKind.Entrance &&
                animation.Effect == OdfAnimationEffect.Fade &&
                animation.Trigger == OdfAnimationTrigger.OnClick &&
                animation.TryGetDelaySeconds(out double delaySeconds) &&
                Math.Abs(delaySeconds - 0.15d) < 0.001d);
        Assert.Contains(
            animations,
            animation => animation.Kind == OdfAnimationKind.Exit &&
                animation.Effect == OdfAnimationEffect.Zoom &&
                animation.Trigger == OdfAnimationTrigger.AfterPrevious &&
                animation.TryGetDelaySeconds(out double delaySeconds) &&
                Math.Abs(delaySeconds - 0.4d) < 0.001d);
    }

    [Fact]
    public void OdpToPptxConverterExportsLowLevelAnimationTreeEffects()
    {
        using OdfPresentationDocument source = OdfPresentationDocument.Create();
        OdfSlide sourceSlide = source.AddSlide("Low-level animation tree");
        OdfShape shape = sourceSlide.AddShape(
            OdfShapeType.Rectangle,
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(4),
            OdfLength.FromCentimeters(2));
        sourceSlide.AnimationRoot
            .AddSequence("click")
            .AddParallel("0s")
            .AddEffect(
                OdfAnimationType.FadeIn,
                shape.Id,
                OdfLength.FromPoints(72),
                OdfLength.FromPoints(18));

        OdfAnimationInfo sourceAnimation = Assert.Single(sourceSlide.GetAnimations());
        Assert.Equal(OdfAnimationEffect.Fade, sourceAnimation.Effect);
        Assert.Equal(OdfAnimationTrigger.OnClick, sourceAnimation.Trigger);
        Assert.True(sourceAnimation.TryGetDurationSeconds(out double sourceDurationSeconds));
        Assert.True(Math.Abs(sourceDurationSeconds - 1d) < 0.001d);
        Assert.True(sourceAnimation.TryGetDelaySeconds(out double sourceDelaySeconds));
        Assert.True(Math.Abs(sourceDelaySeconds - 0.25d) < 0.001d);

        using var pptxStream = new MemoryStream();
        OdpToPptxConverter.Convert(source, pptxStream);

        pptxStream.Position = 0;
        using (PackagingPresentationDocument pptx = PackagingPresentationDocument.Open(pptxStream, false))
        {
            Assert.Empty(new OpenXmlValidator(FileFormatVersions.Office2019).Validate(pptx, TestContext.Current.CancellationToken));
            OpenXmlSlidePart slidePart = Assert.Single(pptx.PresentationPart!.SlideParts);
            P.AnimateEffect effect = Assert.Single(slidePart.Slide!.Descendants<P.AnimateEffect>());
            Assert.Equal(P.AnimateEffectTransitionValues.In, effect.Transition?.Value);
            Assert.Equal("fade", effect.Filter?.Value);
            Assert.Equal("1000", effect.CommonBehavior?.CommonTimeNode?.Duration?.Value);
            Assert.Equal("250", GetAnimationDelay(effect));
        }

        pptxStream.Position = 0;
        using OdfPresentationDocument converted = PptxToOdpConverter.Convert(pptxStream);
        OdfAnimationInfo convertedAnimation = Assert.Single(Assert.Single(converted.Slides).GetAnimations());
        Assert.Equal(OdfAnimationEffect.Fade, convertedAnimation.Effect);
        Assert.Equal(OdfAnimationTrigger.OnClick, convertedAnimation.Trigger);
        Assert.True(convertedAnimation.TryGetDurationSeconds(out double convertedDurationSeconds));
        Assert.True(Math.Abs(convertedDurationSeconds - 1d) < 0.001d);
        Assert.True(convertedAnimation.TryGetDelaySeconds(out double convertedDelaySeconds));
        Assert.True(Math.Abs(convertedDelaySeconds - 0.25d) < 0.001d);
    }

    [Fact]
    public void PptxConvertersPreserveWithPreviousAnimationNodeType()
    {
        using OdfPresentationDocument source = OdfPresentationDocument.Create();
        OdfSlide sourceSlide = source.AddSlide("With previous");
        OdfShape shape = sourceSlide.AddShape(
            OdfShapeType.Rectangle,
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(4),
            OdfLength.FromCentimeters(2));
        sourceSlide.AddEntranceEffect(
            shape.Id,
            OdfAnimationEffect.Fade,
            OdfAnimationTrigger.OnClick,
            duration: TimeSpan.FromMilliseconds(300));
        sourceSlide.AddEntranceEffect(
            shape.Id,
            OdfAnimationEffect.FlyIn,
            OdfAnimationTrigger.WithPrevious,
            delay: TimeSpan.FromMilliseconds(250),
            duration: TimeSpan.FromMilliseconds(700));

        using var pptxStream = new MemoryStream();
        OdpToPptxConverter.Convert(source, pptxStream);

        pptxStream.Position = 0;
        using (PackagingPresentationDocument pptx = PackagingPresentationDocument.Open(pptxStream, false))
        {
            Assert.Empty(new OpenXmlValidator(FileFormatVersions.Office2019).Validate(pptx, TestContext.Current.CancellationToken));
            OpenXmlSlidePart slidePart = Assert.Single(pptx.PresentationPart!.SlideParts);
            P.AnimateEffect flyIn = Assert.Single(
                slidePart.Slide!.Descendants<P.AnimateEffect>(),
                effect => effect.Filter?.Value == "fly");
            P.ParallelTimeNode flyInTimeNode = Assert.Single(
                flyIn.Ancestors<P.ParallelTimeNode>(),
                node => node.CommonTimeNode?.PresetClass?.Value == P.TimeNodePresetClassValues.Entrance);
            Assert.Equal(P.TimeNodeValues.WithEffect, flyInTimeNode.CommonTimeNode?.NodeType?.Value);
            Assert.Equal("250", GetAnimationDelay(flyIn));
        }

        pptxStream.Position = 0;
        using OdfPresentationDocument converted = PptxToOdpConverter.Convert(pptxStream);
        OdfAnimationInfo flyInAnimation = Assert.Single(
            Assert.Single(converted.Slides).GetAnimations(),
            animation => animation.Effect == OdfAnimationEffect.FlyIn);
        Assert.Equal(OdfAnimationTrigger.WithPrevious, flyInAnimation.Trigger);
        Assert.True(flyInAnimation.TryGetDelaySeconds(out double delaySeconds));
        Assert.True(Math.Abs(delaySeconds - 0.25d) < 0.001d);
    }

    [Fact]
    public void PptxConvertersPreserveEmphasisAnimationTiming()
    {
        using OdfPresentationDocument source = OdfPresentationDocument.Create();
        OdfSlide sourceSlide = source.AddSlide("Emphasis timing");
        OdfShape shape = sourceSlide.AddShape(
            OdfShapeType.Rectangle,
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(4),
            OdfLength.FromCentimeters(2));
        sourceSlide.AddEntranceEffect(
            shape.Id,
            OdfAnimationEffect.Fade,
            OdfAnimationTrigger.OnClick,
            duration: TimeSpan.FromMilliseconds(300));
        sourceSlide.AddEmphasisEffect(
            shape.Id,
            OdfAnimationEffect.Fade,
            TimeSpan.FromMilliseconds(900),
            OdfAnimationTrigger.AfterPrevious,
            TimeSpan.FromMilliseconds(250));

        using var pptxStream = new MemoryStream();
        OdpToPptxConverter.Convert(source, pptxStream);

        pptxStream.Position = 0;
        using (PackagingPresentationDocument pptx = PackagingPresentationDocument.Open(pptxStream, false))
        {
            Assert.Empty(new OpenXmlValidator(FileFormatVersions.Office2019).Validate(pptx, TestContext.Current.CancellationToken));
            OpenXmlSlidePart slidePart = Assert.Single(pptx.PresentationPart!.SlideParts);
            P.AnimateEffect emphasis = Assert.Single(
                slidePart.Slide!.Descendants<P.AnimateEffect>(),
                effect => effect.Transition?.Value == P.AnimateEffectTransitionValues.None);
            P.ParallelTimeNode emphasisTimeNode = Assert.Single(
                emphasis.Ancestors<P.ParallelTimeNode>(),
                node => node.CommonTimeNode?.PresetClass?.Value == P.TimeNodePresetClassValues.Emphasis);
            Assert.Equal(P.TimeNodePresetClassValues.Emphasis, emphasisTimeNode.CommonTimeNode?.PresetClass?.Value);
            Assert.Equal(P.TimeNodeValues.AfterEffect, emphasisTimeNode.CommonTimeNode?.NodeType?.Value);
            Assert.Equal("900", emphasis.CommonBehavior?.CommonTimeNode?.Duration?.Value);
            Assert.Equal("250", GetAnimationDelay(emphasis));
        }

        pptxStream.Position = 0;
        using OdfPresentationDocument converted = PptxToOdpConverter.Convert(pptxStream);
        OdfAnimationInfo emphasisAnimation = Assert.Single(
            Assert.Single(converted.Slides).GetAnimations(),
            animation => animation.Kind == OdfAnimationKind.Emphasis);
        Assert.Equal(OdfAnimationTrigger.AfterPrevious, emphasisAnimation.Trigger);
        Assert.True(emphasisAnimation.TryGetDelaySeconds(out double delaySeconds));
        Assert.True(Math.Abs(delaySeconds - 0.25d) < 0.001d);
        Assert.True(emphasisAnimation.TryGetDurationSeconds(out double durationSeconds));
        Assert.True(Math.Abs(durationSeconds - 0.9d) < 0.001d);
    }

    [Fact]
    public void PptxToOdpConverterCombinesNestedAnimationDelays()
    {
        using OdfPresentationDocument source = OdfPresentationDocument.Create();
        OdfSlide sourceSlide = source.AddSlide("Nested delays");
        OdfShape shape = sourceSlide.AddShape(
            OdfShapeType.Rectangle,
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(4),
            OdfLength.FromCentimeters(2));
        sourceSlide.AddEntranceEffect(
            shape.Id,
            OdfAnimationEffect.Fade,
            OdfAnimationTrigger.OnClick,
            delay: TimeSpan.FromMilliseconds(100),
            duration: TimeSpan.FromMilliseconds(500));

        using var pptxStream = new MemoryStream();
        OdpToPptxConverter.Convert(source, pptxStream);

        pptxStream.Position = 0;
        using (PackagingPresentationDocument pptx = PackagingPresentationDocument.Open(pptxStream, true))
        {
            OpenXmlSlidePart slidePart = Assert.Single(pptx.PresentationPart!.SlideParts);
            P.AnimateEffect effect = Assert.Single(slidePart.Slide!.Descendants<P.AnimateEffect>());
            P.ParallelTimeNode middleLayer = Assert.Single(
                effect.Ancestors<P.ParallelTimeNode>(),
                node => node.CommonTimeNode?
                    .StartConditionList?
                    .Elements<P.Condition>()
                    .Any(condition => condition.Delay?.Value == "0") == true);
            middleLayer.CommonTimeNode!.StartConditionList = new P.StartConditionList(new P.Condition { Delay = "250" });
            slidePart.Slide.Save();
        }

        pptxStream.Position = 0;
        using OdfPresentationDocument converted = PptxToOdpConverter.Convert(pptxStream);
        OdfAnimationInfo animation = Assert.Single(Assert.Single(converted.Slides).GetAnimations());
        Assert.True(animation.TryGetDelaySeconds(out double delaySeconds));
        Assert.True(Math.Abs(delaySeconds - 0.35d) < 0.001d);
    }

    [Fact]
    public void PptxConvertersPreserveParagraphAnimationRanges()
    {
        using OdfPresentationDocument source = OdfPresentationDocument.Create();
        OdfSlide sourceSlide = source.AddSlide("Paragraph ranges");
        OdfTextBox textBox = sourceSlide.AddTextBox(
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(8),
            OdfLength.FromCentimeters(3),
            ["第一段", "第二段"]);
        sourceSlide.AddEntranceEffect(
            textBox.Id,
            OdfAnimationEffect.Fade,
            OdfAnimationTrigger.OnClick,
            duration: TimeSpan.FromMilliseconds(600));

        using var pptxStream = new MemoryStream();
        OdpToPptxConverter.Convert(source, pptxStream);

        pptxStream.Position = 0;
        using (PackagingPresentationDocument pptx = PackagingPresentationDocument.Open(pptxStream, false))
        {
            OpenXmlSlidePart slidePart = Assert.Single(pptx.PresentationPart!.SlideParts);
            Assert.Equal([(0, 0), (1, 1)], GetParagraphAnimationRanges(slidePart));
        }

        pptxStream.Position = 0;
        using OdfPresentationDocument converted = PptxToOdpConverter.Convert(pptxStream);
        OdfAnimationInfo[] animations = Assert.Single(converted.Slides).GetAnimations().ToArray();
        Assert.Equal(2, animations.Length);
        Assert.Contains(animations, animation => animation.ParagraphStartIndex == 0 && animation.ParagraphEndIndex == 0);
        Assert.Contains(animations, animation => animation.ParagraphStartIndex == 1 && animation.ParagraphEndIndex == 1);

        using var roundTripPptxStream = new MemoryStream();
        OdpToPptxConverter.Convert(converted, roundTripPptxStream);

        roundTripPptxStream.Position = 0;
        using (PackagingPresentationDocument pptx = PackagingPresentationDocument.Open(roundTripPptxStream, false))
        {
            OpenXmlSlidePart slidePart = Assert.Single(pptx.PresentationPart!.SlideParts);
            Assert.Equal([(0, 0), (1, 1)], GetParagraphAnimationRanges(slidePart));
        }
    }

    [Fact]
    public void PptxToOdpConverterExpandsBuildParagraphWhenEffectOmitsParagraphRange()
    {
        using OdfPresentationDocument source = OdfPresentationDocument.Create();
        OdfSlide sourceSlide = source.AddSlide("Build paragraph");
        OdfTextBox textBox = sourceSlide.AddTextBox(
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(8),
            OdfLength.FromCentimeters(3),
            ["第一段", "第二段"]);
        sourceSlide.AddEntranceEffect(
            textBox.Id,
            OdfAnimationEffect.Fade,
            OdfAnimationTrigger.OnClick,
            duration: TimeSpan.FromMilliseconds(600));

        using var pptxStream = new MemoryStream();
        OdpToPptxConverter.Convert(source, pptxStream);

        pptxStream.Position = 0;
        using (PackagingPresentationDocument pptx = PackagingPresentationDocument.Open(pptxStream, true))
        {
            OpenXmlSlidePart slidePart = Assert.Single(pptx.PresentationPart!.SlideParts);
            foreach (OpenXmlElement paragraphRange in slidePart.Slide!.Descendants().Where(IsParagraphRangeElement).ToArray())
            {
                paragraphRange.Remove();
            }

            P.AnimateEffect[] effects = slidePart.Slide!.Descendants<P.AnimateEffect>().ToArray();
            Assert.NotEmpty(effects);
            P.AnimateEffect firstEffect = effects[0];
            foreach (P.AnimateEffect extraEffect in effects.Skip(1))
            {
                P.ParallelTimeNode? removableTimeNode = extraEffect
                    .Ancestors<P.ParallelTimeNode>()
                    .FirstOrDefault(timeNode => !timeNode.Descendants<P.AnimateEffect>().Contains(firstEffect));
                (removableTimeNode as OpenXmlElement ?? extraEffect).Remove();
            }

            Assert.Single(slidePart.Slide!.Descendants<P.AnimateEffect>());
            Assert.Empty(GetParagraphAnimationRanges(slidePart));
            P.BuildParagraph buildParagraph = Assert.Single(slidePart.Slide!.Timing!.BuildList!.Elements<P.BuildParagraph>());
            Assert.Equal(P.ParagraphBuildValues.Paragraph, buildParagraph.Build?.Value);
            slidePart.Slide.Save();
        }

        pptxStream.Position = 0;
        using OdfPresentationDocument converted = PptxToOdpConverter.Convert(pptxStream);

        OdfAnimationInfo[] animations = Assert.Single(converted.Slides).GetAnimations().ToArray();
        Assert.Equal(2, animations.Length);
        Assert.Contains(animations, animation => animation.ParagraphStartIndex == 0 && animation.ParagraphEndIndex == 0);
        Assert.Contains(animations, animation => animation.ParagraphStartIndex == 1 && animation.ParagraphEndIndex == 1);
    }

    [Fact]
    public void PptxConvertersPreserveSlidePlaceholders()
    {
        using OdfPresentationDocument source = OdfPresentationDocument.Create();
        OdfSlide sourceSlide = source.AddSlide("Placeholders");
        OdfPlaceholder titlePlaceholder = sourceSlide.AddPlaceholder(
            OdfPlaceholderType.Title,
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(8),
            OdfLength.FromCentimeters(1));
        titlePlaceholder.Node.AppendChild(new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text") { TextContent = "Quarterly Review" });
        OdfPlaceholder outlinePlaceholder = sourceSlide.AddPlaceholder(
            OdfPlaceholderType.Outline,
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(3),
            OdfLength.FromCentimeters(8),
            OdfLength.FromCentimeters(3));
        outlinePlaceholder.Node.AppendChild(new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text") { TextContent = "Revenue is ahead" });

        using var pptxStream = new MemoryStream();
        OdpToPptxConverter.Convert(source, pptxStream);

        pptxStream.Position = 0;
        using (PackagingPresentationDocument pptx = PackagingPresentationDocument.Open(pptxStream, false))
        {
            Assert.Empty(new OpenXmlValidator(FileFormatVersions.Office2019).Validate(pptx, TestContext.Current.CancellationToken));
            OpenXmlSlidePart slidePart = Assert.Single(pptx.PresentationPart!.SlideParts);
            P.PlaceholderShape[] placeholders = slidePart.Slide!.Descendants<P.PlaceholderShape>().ToArray();
            Assert.Contains(placeholders, placeholder => placeholder.Type?.Value == P.PlaceholderValues.Title);
            Assert.Contains(placeholders, placeholder => placeholder.Type?.Value == P.PlaceholderValues.Body);
            Assert.Contains(slidePart.Slide!.Descendants<A.Text>(), text => text.Text == "Quarterly Review");
            Assert.Contains(slidePart.Slide!.Descendants<A.Text>(), text => text.Text == "Revenue is ahead");
        }

        pptxStream.Position = 0;
        using OdfPresentationDocument converted = PptxToOdpConverter.Convert(pptxStream);
        OdfPlaceholder[] roundTrippedPlaceholders = Assert.Single(converted.Slides).Placeholders.ToArray();
        Assert.Contains(roundTrippedPlaceholders, placeholder => placeholder.PlaceholderType == OdfPlaceholderType.Title);
        Assert.Contains(roundTrippedPlaceholders, placeholder => placeholder.PlaceholderType == OdfPlaceholderType.Outline);
        Assert.Contains(
            roundTrippedPlaceholders,
            placeholder => placeholder.PlaceholderType == OdfPlaceholderType.Title && placeholder.Node.TextContent == "Quarterly Review");
        Assert.Contains(
            roundTrippedPlaceholders,
            placeholder => placeholder.PlaceholderType == OdfPlaceholderType.Outline && placeholder.Node.TextContent == "Revenue is ahead");
    }

    [Fact]
    public void OdpToPptxConverterWritesStandardLayoutPlaceholders()
    {
        using OdfPresentationDocument source = OdfPresentationDocument.Create();
        OdfSlide sourceSlide = source.AddSlide("Layout Placeholders");
        sourceSlide.PresentationPageLayoutName = "layout_TitleAndBody";
        sourceSlide.AddTextBox(
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(6),
            OdfLength.FromCentimeters(2),
            "Layout body");

        using var pptxStream = new MemoryStream();
        OdpToPptxConverter.Convert(source, pptxStream);

        pptxStream.Position = 0;
        using PackagingPresentationDocument pptx = PackagingPresentationDocument.Open(pptxStream, false);
        Assert.Empty(new OpenXmlValidator(FileFormatVersions.Office2019).Validate(pptx, TestContext.Current.CancellationToken));
        OpenXmlSlidePart slidePart = Assert.Single(pptx.PresentationPart!.SlideParts);
        DocumentFormat.OpenXml.Packaging.SlideLayoutPart layoutPart = slidePart.SlideLayoutPart!;
        Assert.Equal(P.SlideLayoutValues.Text, layoutPart.SlideLayout!.Type?.Value);

        P.PlaceholderShape[] layoutPlaceholders = layoutPart.SlideLayout.Descendants<P.PlaceholderShape>().ToArray();
        Assert.Contains(layoutPlaceholders, placeholder => placeholder.Type?.Value == P.PlaceholderValues.Title);
        Assert.Contains(layoutPlaceholders, placeholder => placeholder.Type?.Value == P.PlaceholderValues.Body);
    }

    [Fact]
    public void PptxConvertersPreserveSlideNames()
    {
        using OdfPresentationDocument source = OdfPresentationDocument.Create();
        source.AddSlide("Executive Summary").AddTextBox(
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(6),
            OdfLength.FromCentimeters(2),
            "Named slide");

        using var pptxStream = new MemoryStream();
        OdpToPptxConverter.Convert(source, pptxStream);

        pptxStream.Position = 0;
        using (PackagingPresentationDocument pptx = PackagingPresentationDocument.Open(pptxStream, false))
        {
            Assert.Empty(new OpenXmlValidator(FileFormatVersions.Office2019).Validate(pptx, TestContext.Current.CancellationToken));
            OpenXmlSlidePart slidePart = Assert.Single(pptx.PresentationPart!.SlideParts);
            Assert.Equal("Executive Summary", slidePart.Slide!.CommonSlideData?.Name?.Value);
        }

        pptxStream.Position = 0;
        using OdfPresentationDocument converted = PptxToOdpConverter.Convert(pptxStream);
        Assert.Equal("Executive Summary", Assert.Single(converted.Slides).Name);
    }
    [Fact]
    public void PptxConvertersPreserveSlideSize()
    {
        using OdfPresentationDocument source = OdfPresentationDocument.Create();
        source.SetSlideSize(OdfLength.FromCentimeters(25), OdfLength.FromCentimeters(14));
        source.AddSlide("Sized");

        using var pptxStream = new MemoryStream();
        OdpToPptxConverter.Convert(source, pptxStream);

        pptxStream.Position = 0;
        using (PackagingPresentationDocument pptx = PackagingPresentationDocument.Open(pptxStream, false))
        {
            Assert.Empty(new OpenXmlValidator(FileFormatVersions.Office2019).Validate(pptx, TestContext.Current.CancellationToken));
            P.SlideSize slideSize = pptx.PresentationPart!.Presentation!.SlideSize!;
            Assert.Equal(ToEmus(OdfLength.FromCentimeters(25)), slideSize.Cx?.Value);
            Assert.Equal(ToEmus(OdfLength.FromCentimeters(14)), slideSize.Cy?.Value);
        }

        pptxStream.Position = 0;
        using OdfPresentationDocument converted = PptxToOdpConverter.Convert(pptxStream);
        (OdfLength width, OdfLength height) = converted.GetSlideSize();
        AssertLengthEquals(OdfLength.FromCentimeters(25), width.ToString());
        AssertLengthEquals(OdfLength.FromCentimeters(14), height.ToString());
    }
    [Fact]
    public void PptxConvertersPreserveBasicSlideLayouts()
    {
        using OdfPresentationDocument source = OdfPresentationDocument.Create();
        source.AddSlide("Layout");
        source.SetLayout(0, OdfPresentationLayout.TitleOnly);

        using var pptxStream = new MemoryStream();
        OdpToPptxConverter.Convert(source, pptxStream);

        pptxStream.Position = 0;
        using (PackagingPresentationDocument pptx = PackagingPresentationDocument.Open(pptxStream, false))
        {
            Assert.Empty(new OpenXmlValidator(FileFormatVersions.Office2019).Validate(pptx, TestContext.Current.CancellationToken));
            OpenXmlSlidePart slidePart = Assert.Single(pptx.PresentationPart!.SlideParts);
            Assert.Equal(P.SlideLayoutValues.TitleOnly, slidePart.SlideLayoutPart?.SlideLayout?.Type?.Value);
        }

        pptxStream.Position = 0;
        using OdfPresentationDocument converted = PptxToOdpConverter.Convert(pptxStream);
        Assert.Equal(OdfPresentationLayout.TitleOnly, converted.GetLayout(0));
        Assert.Equal("layout_TitleOnly", Assert.Single(converted.Slides).PresentationPageLayoutName);
    }

    [Fact]
    public void PresentationOoxmlExtensionsExposeFriendlyPptxApi()
    {
        using OdfPresentationDocument source = OdfPresentationDocument.Create();
        source
            .AddSlide("API")
            .AddTextBox(
                OdfLength.FromCentimeters(1),
                OdfLength.FromCentimeters(1),
                OdfLength.FromCentimeters(6),
                OdfLength.FromCentimeters(2),
                "Friendly API");

        byte[] pptxBytes = source.ToPptx();
        Assert.NotEmpty(pptxBytes);

        using var stream = new MemoryStream(pptxBytes);
        using OdfPresentationDocument converted = stream.ToOdpPresentation();
        OdfSlide slide = Assert.Single(converted.Slides);
        Assert.Contains(slide.TextBoxes, textBox => textBox.Text == "Friendly API");

        string pptxPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".pptx");
        try
        {
            source.SaveAsPptx(pptxPath);
            using OdfPresentationDocument loaded = OdfPresentationOoxmlExtensions.LoadPptxAsOdp(pptxPath);
            Assert.Contains(Assert.Single(loaded.Slides).TextBoxes, textBox => textBox.Text == "Friendly API");
        }
        finally
        {
            if (File.Exists(pptxPath))
            {
                File.Delete(pptxPath);
            }
        }
    }

    private static void SetParagraphTextAlign(OdfPresentationDocument source, string text, string alignment)
    {
        OdfNode paragraph = FindParagraphByText(source, text);
        source.StyleEngine.SetLocalStyleProperty(
            paragraph,
            "paragraph",
            "paragraph-properties",
            "text-align",
            OdfNamespaces.Fo,
            alignment,
            "fo");
    }

    private static string? ReadParagraphTextAlign(OdfPresentationDocument document, string text)
    {
        OdfNode paragraph = FindParagraphByText(document, text);
        string? styleName = paragraph.GetAttribute("style-name", OdfNamespaces.Text);
        return string.IsNullOrWhiteSpace(styleName)
            ? null
            : document.StyleEngine.GetStyleProperty(styleName!, "text-align", OdfNamespaces.Fo, "paragraph");
    }

    private static OdfNode FindParagraphByText(OdfPresentationDocument document, string text)
    {
        return Assert.Single(
            document.ContentDom.Descendants(),
            node => node.LocalName == "p" &&
                node.NamespaceUri == OdfNamespaces.Text &&
                ContainsText(node, text));
    }

    private static bool ContainsText(OdfNode node, string text)
    {
        if (node.TextContent == text)
        {
            return true;
        }

        foreach (OdfNode child in node.Children)
        {
            if (ContainsText(child, text))
            {
                return true;
            }
        }

        return false;
    }
    private static OdfLength[] ReadClipLengths(string? clip)
    {
        Assert.False(string.IsNullOrWhiteSpace(clip));
        Assert.StartsWith("rect(", clip!, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(")", clip!);
        string[] parts = clip!.Substring(5, clip.Length - 6).Split(',');
        Assert.Equal(4, parts.Length);
        return parts.Select(part => OdfLength.Parse(part.Trim())).ToArray();
    }
    private static int ToEmus(OdfLength length)
        => (int)Math.Round(length.ToPoints() * 12700d, MidpointRounding.AwayFromZero);
    private static void AssertLengthEquals(OdfLength expected, string? actual)
    {
        Assert.NotNull(actual);
        double expectedPoints = expected.ToPoints();
        double actualPoints = OdfLength.Parse(actual).ToPoints();
        Assert.True(
            Math.Abs(expectedPoints - actualPoints) < 0.001d,
            $"Expected {expectedPoints}pt but got {actualPoints}pt from '{actual}'.");
    }
    private static string? GetAnimationDelay(P.AnimateEffect effect)
        => (effect.Parent?.Parent as P.CommonTimeNode)?
            .StartConditionList?
            .Elements<P.Condition>()
            .Select(condition => condition.Delay?.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static (int Start, int End)[] GetParagraphAnimationRanges(OpenXmlSlidePart slidePart)
        => slidePart.Slide!
            .Descendants<P.AnimateEffect>()
            .Select(effect => effect.CommonBehavior?.TargetElement?.ShapeTarget)
            .Where(target => target is not null)
            .SelectMany(target => target!.Descendants())
            .Where(IsParagraphRangeElement)
            .Select(element => (
                ReadRequiredIntAttribute(element, "st"),
                ReadRequiredIntAttribute(element, "end")))
            .ToArray();

    private static bool IsParagraphRangeElement(OpenXmlElement element)
        => element.LocalName == "pRg" &&
            element.NamespaceUri == "http://schemas.openxmlformats.org/presentationml/2006/main";

    private static int ReadRequiredIntAttribute(OpenXmlElement element, string localName)
    {
        string? value = element.GetAttribute(localName, string.Empty).Value;
        Assert.False(string.IsNullOrWhiteSpace(value));
        return int.Parse(value!, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void ApplyStyledTextBox(
        OdfPresentationDocument source,
        string text,
        bool bold,
        bool italic,
        string fontSize,
        string color)
    {
        OdfNode paragraph = Assert.Single(
            source.ContentDom.Descendants(),
            node => node.LocalName == "p" &&
                node.NamespaceUri == OdfNamespaces.Text &&
                node.TextContent == text);
        source.StyleEngine.SetLocalStyleProperty(paragraph, "paragraph", "text-properties", "font-weight", OdfNamespaces.Fo, bold ? "bold" : "normal", "fo");
        source.StyleEngine.SetLocalStyleProperty(paragraph, "paragraph", "text-properties", "font-style", OdfNamespaces.Fo, italic ? "italic" : "normal", "fo");
        source.StyleEngine.SetLocalStyleProperty(paragraph, "paragraph", "text-properties", "font-size", OdfNamespaces.Fo, fontSize, "fo");
        source.StyleEngine.SetLocalStyleProperty(paragraph, "paragraph", "text-properties", "color", OdfNamespaces.Fo, color, "fo");
    }

    private static void AddThemeSourceColor(OdfPresentationDocument source, string localName, string namespaceUri, string value)
    {
        var style = new OdfNode(OdfNodeType.Element, "style", OdfNamespaces.Style, "style");
        style.SetAttribute(localName, namespaceUri, value, OdfNamespaces.GetPrefix(namespaceUri));
        source.StylesDom.AppendChild(style);
    }

    private static void AddThemeSourceFont(OdfPresentationDocument source, string localName, string namespaceUri, string value)
    {
        var style = new OdfNode(OdfNodeType.Element, "style", OdfNamespaces.Style, "style");
        style.SetAttribute(localName, namespaceUri, value, OdfNamespaces.GetPrefix(namespaceUri));
        source.StylesDom.AppendChild(style);
    }

    private static void ApplyTextBoxFontFamily(OdfPresentationDocument source, string text, string fontFamily)
    {
        OdfNode paragraph = Assert.Single(
            source.ContentDom.Descendants(),
            node => node.LocalName == "p" &&
                node.NamespaceUri == OdfNamespaces.Text &&
                node.TextContent == text);
        source.StyleEngine.SetLocalStyleProperty(paragraph, "paragraph", "text-properties", "font-family", OdfNamespaces.Fo, fontFamily, "fo");
    }

    private static void ReplaceTextBoxWithStyledRuns(
        OdfPresentationDocument source,
        string originalText,
        params (string Text, bool Bold, bool Italic, string FontSize, string Color, bool Underline, bool Strikethrough, string TextPosition)[] runs)
    {
        OdfNode paragraph = Assert.Single(
            source.ContentDom.Descendants(),
            node => node.LocalName == "p" &&
                node.NamespaceUri == OdfNamespaces.Text &&
                node.TextContent == originalText);
        paragraph.Children.Clear();

        foreach ((string text, bool bold, bool italic, string fontSize, string color, bool underline, bool strikethrough, string textPosition) in runs)
        {
            var span = new OdfNode(OdfNodeType.Element, "span", OdfNamespaces.Text, "text") { TextContent = text };
            paragraph.AppendChild(span);
            source.StyleEngine.SetLocalStyleProperty(span, "text", "text-properties", "font-weight", OdfNamespaces.Fo, bold ? "bold" : "normal", "fo");

            source.StyleEngine.SetLocalStyleProperty(span, "text", "text-properties", "font-style", OdfNamespaces.Fo, italic ? "italic" : "normal", "fo");
            source.StyleEngine.SetLocalStyleProperty(span, "text", "text-properties", "font-size", OdfNamespaces.Fo, fontSize, "fo");
            source.StyleEngine.SetLocalStyleProperty(span, "text", "text-properties", "color", OdfNamespaces.Fo, color, "fo");
            source.StyleEngine.SetLocalStyleProperty(span, "text", "text-properties", "text-underline-style", OdfNamespaces.Style, underline ? "solid" : "none", "style");
            source.StyleEngine.SetLocalStyleProperty(span, "text", "text-properties", "text-line-through-style", OdfNamespaces.Style, strikethrough ? "solid" : "none", "style");
            if (!string.IsNullOrWhiteSpace(textPosition))
            {
                source.StyleEngine.SetLocalStyleProperty(span, "text", "text-properties", "text-position", OdfNamespaces.Style, textPosition, "style");
            }
        }
    }

    /// <summary>
    /// 驗證 PptxToOdpConverter 載入 Master / Theme / EffectStyle 矩陣與繼承的完整性，以及 timeline 動畫 Timing & Sequence 對應的正確性。
    /// </summary>
    [Fact]
    public void PptxToOdpConverterParsesMasterInheritanceAndTimeline()
    {
        using var stream = new MemoryStream();
        // 1. 建立一個最簡化的 PPTX 結構
        using (var pptx = PackagingPresentationDocument.Create(stream, PresentationDocumentType.Presentation, true))
        {
            var presPart = pptx.AddPresentationPart();
            presPart.Presentation = new P.Presentation(
                new P.SlideMasterIdList(new P.SlideMasterId { Id = 2147483648U, RelationshipId = "rId1" }),
                new P.SlideIdList(new P.SlideId { Id = 256U, RelationshipId = "rId2" })
            );

            // Slide Master
            var masterPart = presPart.AddNewPart<SlideMasterPart>("rId1");
            var masterCommon = new P.CommonSlideData(
                new P.ShapeTree(
                    new P.NonVisualGroupShapeProperties(
                        new P.NonVisualDrawingProperties { Id = 1U, Name = "" },
                        new P.NonVisualGroupShapeDrawingProperties(),
                        new P.ApplicationNonVisualDrawingProperties()),
                    new P.GroupShapeProperties(new A.TransformGroup()),
                    new P.Shape(
                        new P.NonVisualShapeProperties(
                            new P.NonVisualDrawingProperties { Id = 2U, Name = "Master Title" },
                            new P.NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true }),
                            new P.ApplicationNonVisualDrawingProperties(new P.PlaceholderShape { Type = P.PlaceholderValues.Title })),
                        new P.ShapeProperties(),
                        new P.TextBody(new A.BodyProperties(), new A.ListStyle(), new A.Paragraph()))
                )
            );
            masterPart.SlideMaster = new P.SlideMaster(masterCommon, new P.ColorMap { Background1 = A.ColorSchemeIndexValues.Light1 });

            // Theme (用於 FormatScheme, EffectStyleList 等)
            var themePart = masterPart.AddNewPart<ThemePart>();
            themePart.Theme = new A.Theme(
                new A.ThemeElements(
                    new A.ColorScheme(
                        new A.Dark1Color(new A.SystemColor { Val = A.SystemColorValues.WindowText, LastColor = "000000" }),
                        new A.Light1Color(new A.SystemColor { Val = A.SystemColorValues.Window, LastColor = "FFFFFF" }),
                        new A.Dark2Color(new A.RgbColorModelHex { Val = "1F497D" }),
                        new A.Light2Color(new A.RgbColorModelHex { Val = "E5E0EC" }),
                        new A.Accent1Color(new A.RgbColorModelHex { Val = "4F81BD" }),
                        new A.Accent2Color(new A.RgbColorModelHex { Val = "C0504D" }),
                        new A.Accent3Color(new A.RgbColorModelHex { Val = "9BBB59" }),
                        new A.Accent4Color(new A.RgbColorModelHex { Val = "8064A2" }),
                        new A.Accent5Color(new A.RgbColorModelHex { Val = "4BACC6" }),
                        new A.Accent6Color(new A.RgbColorModelHex { Val = "F79646" }),
                        new A.Hyperlink(new A.RgbColorModelHex { Val = "0000FF" }),
                        new A.FollowedHyperlinkColor(new A.RgbColorModelHex { Val = "800080" })
                    )
                    { Name = "Office" },
                    new A.FontScheme(
                        new A.MajorFont(new A.LatinFont { Typeface = "Calibri" }),
                        new A.MinorFont(new A.LatinFont { Typeface = "Calibri" })
                    )
                    { Name = "Office" },
                    new A.FormatScheme(
                        new A.FillStyleList(
                            new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.Accent1 }),
                            new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.Accent2 }),
                            new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.Accent3 })
                        ),
                        new A.LineStyleList(
                            new A.Outline(new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.Accent1 })) { Width = 9525 },
                            new A.Outline(new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.Accent2 })) { Width = 19050 },
                            new A.Outline(new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.Accent3 })) { Width = 28575 }
                        ),
                        new A.EffectStyleList(
                            new A.EffectStyle(new A.EffectList(new A.OuterShadow(new A.SchemeColor(new A.Alpha { Val = 72000 }) { Val = A.SchemeColorValues.Dark2 }) { Distance = 38100L, Direction = 5400000, Alignment = A.RectangleAlignmentValues.BottomLeft })),
                            new A.EffectStyle(new A.EffectList()),
                            new A.EffectStyle(new A.EffectList())
                        ),
                        new A.BackgroundFillStyleList()
                    )
                    { Name = "Office" }
                )
            )
            { Name = "Office Theme" };

            // Layout
            var layoutPart = masterPart.AddNewPart<SlideLayoutPart>();
            layoutPart.SlideLayout = new P.SlideLayout(
                new P.CommonSlideData(
                    new P.ShapeTree(
                        new P.NonVisualGroupShapeProperties(
                            new P.NonVisualDrawingProperties { Id = 1U, Name = "" },
                            new P.NonVisualGroupShapeDrawingProperties(),
                            new P.ApplicationNonVisualDrawingProperties()),
                        new P.GroupShapeProperties(new A.TransformGroup()),
                        new P.Shape(
                            new P.NonVisualShapeProperties(
                                new P.NonVisualDrawingProperties { Id = 3U, Name = "Layout Title" },
                                new P.NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true }),
                                new P.ApplicationNonVisualDrawingProperties(new P.PlaceholderShape { Type = P.PlaceholderValues.Title })),
                            new P.ShapeProperties(),
                            new P.TextBody(
                                new A.BodyProperties(),
                                new A.ListStyle(),
                                new A.Paragraph(
                                    new A.ParagraphProperties(
                                        new A.DefaultRunProperties(
                                            new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.Accent3 }),
                                            new A.LatinFont { Typeface = "+mj-lt" })
                                        {
                                            Bold = true,
                                            FontSize = 2400,
                                        })
                                    {
                                        Alignment = A.TextAlignmentTypeValues.Center,
                                    })))
                    )
                )
            )
            { Type = P.SlideLayoutValues.TitleOnly };

            // Slide
            var slidePart = presPart.AddNewPart<SlidePart>("rId2");
            slidePart.AddPart(layoutPart);

            var slideShape = new P.Shape(
                new P.NonVisualShapeProperties(
                    new P.NonVisualDrawingProperties { Id = 10U, Name = "Title Placeholder" },
                    new P.NonVisualShapeDrawingProperties(),
                    new P.ApplicationNonVisualDrawingProperties(new P.PlaceholderShape { Type = P.PlaceholderValues.Title })),
                new P.ShapeProperties(
                    new A.Transform2D(
                        new A.Offset { X = 100000L, Y = 100000L },
                        new A.Extents { Cx = 200000L, Cy = 200000L }),
                    new A.PresetGeometry { Preset = A.ShapeTypeValues.Rectangle }),
                new P.ShapeStyle(
                    new A.LineReference(new A.SchemeColor { Val = A.SchemeColorValues.Accent1 }) { Index = 1U },
                    new A.FillReference(new A.SchemeColor { Val = A.SchemeColorValues.Accent2 }) { Index = 1U },
                    new A.EffectReference(new A.SchemeColor { Val = A.SchemeColorValues.Accent3 }) { Index = 1U },
                    new A.FontReference { Index = A.FontCollectionIndexValues.Minor }
                ),
                new P.TextBody(
                    new A.BodyProperties(),
                    new A.ListStyle(),
                    new A.Paragraph(new A.Run(new A.Text("Master Title Text"))))
            );

            // Timeline Animations
            var timing = new P.Timing(
                new P.TimeNodeList(
                    new P.ParallelTimeNode(
                        new P.CommonTimeNode { Id = 1U, Restart = P.TimeNodeRestartValues.Always },
                        new P.TimeNodeList(
                            new P.ParallelTimeNode(
                                new P.CommonTimeNode { Id = 2U, NodeType = P.TimeNodeValues.ClickEffect, PresetId = 1, PresetClass = P.TimeNodePresetClassValues.Entrance },
                                new P.TimeNodeList(
                                    new P.AnimateEffect(
                                        new P.CommonBehavior(
                                            new P.CommonTimeNode { Duration = "1000" },
                                            new P.TargetElement(new P.ShapeTarget { ShapeId = "10" })
                                        )
                                    )
                                    { Transition = P.AnimateEffectTransitionValues.In, Filter = "fade" }
                                )
                            )
                        )
                    )
                )
            );

            slidePart.Slide = new P.Slide(
                new P.CommonSlideData(
                    new P.ShapeTree(
                        new P.NonVisualGroupShapeProperties(
                            new P.NonVisualDrawingProperties { Id = 1U, Name = "" },
                            new P.NonVisualGroupShapeDrawingProperties(),
                            new P.ApplicationNonVisualDrawingProperties()),
                        new P.GroupShapeProperties(new A.TransformGroup()),
                        slideShape
                    )
                ),
                timing
            );

            pptx.Save();
        }

        // 2. 轉換為 ODP
        stream.Position = 0;
        using OdfPresentationDocument odp = PptxToOdpConverter.Convert(stream);

        // 3. 驗證結果
        Assert.NotNull(odp);
        var slide = Assert.Single(odp.Slides);

        // 驗證 Placeholder 繼承與 Theme 矩陣套用
        var shape = Assert.Single(slide.Placeholders);
        Assert.Equal("Master Title Text", shape.Node.TextContent);
        OdfNode inheritedSpan = Assert.Single(shape.Node.Descendants().Where(node => node.LocalName == "span" && node.NamespaceUri == OdfNamespaces.Text));
        string? inheritedTextStyleName = inheritedSpan.GetAttribute("style-name", OdfNamespaces.Text);
        Assert.NotNull(inheritedTextStyleName);
        Assert.Equal("bold", shape.Document.StyleEngine.GetStyleProperty(inheritedTextStyleName, "font-weight", OdfNamespaces.Fo, "text"));
        Assert.Equal("24pt", shape.Document.StyleEngine.GetStyleProperty(inheritedTextStyleName, "font-size", OdfNamespaces.Fo, "text"));
        Assert.Equal("#9BBB59", shape.Document.StyleEngine.GetStyleProperty(inheritedTextStyleName, "color", OdfNamespaces.Fo, "text"));
        Assert.Equal("Calibri", shape.Document.StyleEngine.GetStyleProperty(inheritedTextStyleName, "font-family", OdfNamespaces.Fo, "text"));
        OdfNode inheritedParagraph = Assert.Single(shape.Node.Descendants().Where(node => node.LocalName == "p" && node.NamespaceUri == OdfNamespaces.Text));
        string? inheritedParagraphStyleName = inheritedParagraph.GetAttribute("style-name", OdfNamespaces.Text);
        Assert.NotNull(inheritedParagraphStyleName);
        Assert.Equal("center", shape.Document.StyleEngine.GetStyleProperty(inheritedParagraphStyleName, "text-align", OdfNamespaces.Fo, "paragraph"));

        // 驗證 FillColor 與 StrokeColor 來自 Style Reference 和 Theme Color
        Assert.Equal("#C0504D", shape.FillColor); // Accent2
        Assert.Equal("#4F81BD", shape.StrokeColor); // Accent1

        // 驗證是否套用了 Theme 的 EffectStyle 中的陰影。
        string styleName = shape.Node.GetAttribute("style-name", OdfNamespaces.Draw) ?? string.Empty;
        string? shadow = shape.Document.StyleEngine.GetStyleProperty(styleName, "shadow", OdfNamespaces.Draw, "graphic");
        string? shadowColor = shape.Document.StyleEngine.GetStyleProperty(styleName, "shadow-color", OdfNamespaces.Draw, "graphic");
        Assert.Equal("visible", shadow);
        Assert.Equal("#1F497D", shadowColor); // Accent3 maps to effect style index 1, which has shadow using Dark2 (1F497D)
        AssertLengthEquals(OdfLength.FromPoints(0), shape.Document.StyleEngine.GetStyleProperty(styleName, "shadow-offset-x", OdfNamespaces.Draw, "graphic"));
        AssertLengthEquals(OdfLength.FromPoints(3), shape.Document.StyleEngine.GetStyleProperty(styleName, "shadow-offset-y", OdfNamespaces.Draw, "graphic"));
        Assert.Equal("72%", shape.Document.StyleEngine.GetStyleProperty(styleName, "shadow-opacity", OdfNamespaces.Draw, "graphic"));
    }

    /// <summary>
    /// 驗證 ODP 轉換為 PPTX 時，是否支援進階的 Theme 母片、表格獨立邊框與背景填滿、以及段落動畫時序。
    /// </summary>
    [Fact]
    public void OdpToPptxConverterSupportsAdvancedThemeMasterAndTableBorders()
    {
        using OdfPresentationDocument odp = OdfPresentationDocument.Create();
        OdfSlide slide = odp.AddSlide("TestSlide");

        // 1. 建立具有多段落動畫的 TextBox
        OdfTextBox textBox = slide.AddTextBox(
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(10),
            OdfLength.FromCentimeters(4),
            ["第一段落動畫", "第二段落動畫"]);

        // 為 TextBox 加入動畫設定
        slide.AddEntranceEffect(
            textBox.Id,
            OdfAnimationEffect.Fade,
            OdfAnimationTrigger.OnClick,
            duration: TimeSpan.FromSeconds(1.5));

        slide.AddEntranceEffect(
            textBox.Id,
            OdfAnimationEffect.FlyIn,
            OdfAnimationTrigger.WithPrevious,
            delay: TimeSpan.FromSeconds(0.5),
            duration: TimeSpan.FromSeconds(2.0));

        // 2. 建立含有獨立邊框與背景的表格
        OdfShape tableShape = slide.AddShape(
            OdfShapeType.Rectangle,
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(6),
            OdfLength.FromCentimeters(12),
            OdfLength.FromCentimeters(5));
        OdfEmbeddedTable table = tableShape.AddEmbeddedTable(2, 2);

        // 設定第一個單元格 (0,0) 的背景與四向獨立邊框
        table.SetCellBackgroundColor(0, 0, "#FFCC00");
        table.SetCellBorderLeft(0, 0, "2pt solid #FF0000");
        table.SetCellBorderRight(0, 0, "1pt dashed #00FF00");
        table.SetCellBorderTop(0, 0, "3pt double #0000FF");
        table.SetCellBorderBottom(0, 0, "none");

        // 3. 執行匯出轉換
        using var pptxStream = new MemoryStream();
        OdpToPptxConverter.Convert(odp, pptxStream);

        // 4. 驗證產生的 PPTX 文件
        pptxStream.Position = 0;
        using PackagingPresentationDocument pptx = PackagingPresentationDocument.Open(pptxStream, false);

        // 驗證 OpenXML 文件結構合法性
        var validator = new OpenXmlValidator(FileFormatVersions.Office2019);
        var validationErrors = validator.Validate(pptx, TestContext.Current.CancellationToken).ToList();
        Assert.Empty(validationErrors);

        var presentationPart = pptx.PresentationPart;
        Assert.NotNull(presentationPart);

        // 驗證 SlideMaster 與 SlideLayout 背景樣式參考
        var masterPart = Assert.Single(presentationPart.SlideMasterParts);
        var slideMaster = masterPart.SlideMaster;
        Assert.NotNull(slideMaster);
        var commonSlideData = slideMaster.CommonSlideData;
        Assert.NotNull(commonSlideData);
        var background = commonSlideData.Background;
        Assert.NotNull(background);
        var bgRef = background.BackgroundStyleReference;
        Assert.NotNull(bgRef);
        Assert.NotNull(bgRef.Index);
        Assert.Equal(1U, bgRef.Index.Value);

        var layoutPart = Assert.Single(masterPart.SlideLayoutParts);
        var slideLayout = layoutPart.SlideLayout;
        Assert.NotNull(slideLayout);
        var layoutData = slideLayout.CommonSlideData;
        Assert.NotNull(layoutData);
        var layoutBg = layoutData.Background;
        Assert.NotNull(layoutBg);
        var layoutBgRef = layoutBg.BackgroundStyleReference;
        Assert.NotNull(layoutBgRef);
        Assert.NotNull(layoutBgRef.Index);
        Assert.Equal(1U, layoutBgRef.Index.Value);

        // 驗證 Slide 內容
        var slidePart = Assert.Single(presentationPart.SlideParts);
        var parsedSlide = slidePart.Slide;
        Assert.NotNull(parsedSlide);

        // 驗證 ShapeStyle
        var shapeStyle = parsedSlide.Descendants<P.ShapeStyle>().FirstOrDefault();
        Assert.NotNull(shapeStyle);
        Assert.NotNull(shapeStyle.LineReference);
        Assert.NotNull(shapeStyle.FillReference);
        Assert.NotNull(shapeStyle.EffectReference);
        Assert.NotNull(shapeStyle.FontReference);

        var effectStyles = masterPart.ThemePart!.Theme!.ThemeElements!.FormatScheme!.EffectStyleList!
            .Elements<A.EffectStyle>()
            .ToArray();
        Assert.Equal(3, effectStyles.Length);
        A.OuterShadow[] themeShadows = effectStyles
            .Select(style => style.GetFirstChild<A.EffectList>()?.GetFirstChild<A.OuterShadow>())
            .Where(shadow => shadow is not null)
            .Cast<A.OuterShadow>()
            .ToArray();
        Assert.Equal(3, themeShadows.Length);
        A.SchemeColor firstShadowColor = Assert.IsType<A.SchemeColor>(Assert.Single(themeShadows[0].ChildElements, child => child is A.SchemeColor));
        Assert.Equal(A.SchemeColorValues.Dark2, firstShadowColor.Val?.Value);
        Assert.Equal(72000, Assert.Single(firstShadowColor.Elements<A.Alpha>()).Val?.Value);
        Assert.Equal(38100L, themeShadows[0].Distance?.Value);

        // 驗證 Table 四向邊框與背景填滿
        var cell = parsedSlide.Descendants<A.TableCell>().FirstOrDefault();
        Assert.NotNull(cell);
        var cellProperties = cell.TableCellProperties;
        Assert.NotNull(cellProperties);

        // 背景驗證
        var cellFill = cellProperties.GetFirstChild<A.SolidFill>();
        Assert.NotNull(cellFill);
        Assert.Equal("FFCC00", cellFill.GetFirstChild<A.RgbColorModelHex>()?.Val?.Value);

        // 獨立邊框驗證
        Assert.NotNull(cellProperties.LeftBorderLineProperties);
        Assert.Equal("FF0000", cellProperties.LeftBorderLineProperties.GetFirstChild<A.SolidFill>()?.GetFirstChild<A.RgbColorModelHex>()?.Val?.Value);

        Assert.NotNull(cellProperties.RightBorderLineProperties);
        Assert.Equal("00FF00", cellProperties.RightBorderLineProperties.GetFirstChild<A.SolidFill>()?.GetFirstChild<A.RgbColorModelHex>()?.Val?.Value);
        Assert.Equal(A.PresetLineDashValues.Dash, cellProperties.RightBorderLineProperties.GetFirstChild<A.PresetDash>()?.Val?.Value);

        Assert.NotNull(cellProperties.TopBorderLineProperties);
        Assert.Equal("0000FF", cellProperties.TopBorderLineProperties.GetFirstChild<A.SolidFill>()?.GetFirstChild<A.RgbColorModelHex>()?.Val?.Value);
        Assert.Equal(A.PresetLineDashValues.Solid, cellProperties.TopBorderLineProperties.GetFirstChild<A.PresetDash>()?.Val?.Value);

        // 驗證動畫 Timeline 配置
        Assert.NotNull(parsedSlide.Timing);
        var parallelNodes = parsedSlide.Timing.Descendants<P.ParallelTimeNode>().ToList();
        Assert.NotEmpty(parallelNodes);

        // 驗證 BuildList 是否包含 BuildParagraph (ByLevel)
        var buildList = parsedSlide.Timing.BuildList;
        if (buildList != null)
        {
            var buildParagraphs = buildList.Descendants<P.BuildParagraph>().ToList();
            Assert.NotEmpty(buildParagraphs);
            Assert.Contains(buildParagraphs, bp => bp.Build?.Value == P.ParagraphBuildValues.Paragraph);
        }
    }
}
