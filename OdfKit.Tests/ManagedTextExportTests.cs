using OdfKit.DOM;
using System;
using System.Globalization;
using OdfKit.Core;
using System.IO;
using System.Linq;
using System.Threading;
using OdfKit.Export;
using OdfKit.Styles;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

public class ManagedTextExportTests
{
    [Fact]
    public void MarkdownExporterConvertsHeadingsParagraphsAndLists()
    {
        using TextDocument document = TextDocument.Create();
        document.AddHeading("Roadmap", 2);
        document.AddParagraph("ODF first, LibreOffice fallback.");
        OdfList list = document.AddList();
        list.AddListItem("Markdown");
        list.AddListItem("RTF");

        string markdown = document.ToMarkdown();

        Assert.Contains("## Roadmap", markdown);
        Assert.Contains("ODF first, LibreOffice fallback\\.", markdown);
        Assert.Contains("- Markdown", markdown);
        Assert.Contains("- RTF", markdown);
    }

    [Fact]
    public void MarkdownExporterEscapesSyntaxCharacters()
    {
        using TextDocument document = TextDocument.Create();
        document.AddParagraph("A *clean* [room] #1");

        string markdown = document.ToMarkdown();

        Assert.Equal("A \\*clean\\* \\[room\\] \\#1", markdown);
    }

    [Fact]
    public void RtfExporterConvertsUnicodeTextAndEscapesControlCharacters()
    {
        using TextDocument document = TextDocument.Create();
        document.AddHeading("章節", 1);
        document.AddParagraph(@"Path C:\Temp {draft}");

        string rtf = document.ToRtf();

        Assert.StartsWith(@"{\rtf1\ansi\deff0", rtf);
        Assert.Contains(@"\b\fs44 ", rtf);
        Assert.Contains(@"\u31456?\u31680?", rtf);
        Assert.Contains(@"Path C:\\Temp \{draft\}", rtf);
    }

    [Fact]
    public void RtfExporterConvertsLists()
    {
        using TextDocument document = TextDocument.Create();
        OdfList list = document.AddList();
        list.AddListItem("One");
        list.AddListItem("Two");

        string rtf = document.ToRtf();

        Assert.Contains(@"\bullet\tab One\par", rtf);
        Assert.Contains(@"\bullet\tab Two\par", rtf);
    }

    [Fact]
    public void ManagedExportersHandleInlineTextNodes()
    {
        using TextDocument document = TextDocument.Create();
        OdfParagraph paragraph = document.AddParagraph();
        paragraph.AddTextRun("A");
        paragraph.AddSpace(2);
        paragraph.AddTextRun("B");
        paragraph.AddLineBreak();
        paragraph.AddTextRun("C");

        string markdown = document.ToMarkdown();
        string rtf = document.ToRtf();

        Assert.Contains("A  B", markdown);
        Assert.Contains("C", markdown);
        Assert.Contains(@"A  B\line C", rtf);
    }

    [Fact]
    public void ManagedExportersPreserveInlineTextStyles()
    {
        using TextDocument document = TextDocument.Create();
        OdfParagraph paragraph = document.AddParagraph();
        paragraph.AddTextRun("Bold").WithBold();
        paragraph.AddTextRun(" ");
        OdfTextRun styled = paragraph.AddTextRun("Style & Color").WithItalic().WithFontSize("14pt").WithColor("#0066CC");
        styled.IsUnderline = true;

        string markdown = document.ToMarkdown();
        string rtf = document.ToRtf();

        Assert.Contains("**Bold**", markdown);
        Assert.Contains("<span style=\"font-style:italic; text-decoration:underline; font-size:14pt; color:#0066CC\">Style &amp; Color</span>", markdown);
        Assert.Contains(@"{\colortbl;\red0\green102\blue204;}", rtf);
        Assert.Contains(@"{\b Bold}", rtf);
        Assert.Contains(@"{\i \ul \fs28 \cf1 Style & Color}", rtf);
    }

    [Fact]
    public void MarkdownExporterConvertsTextTables()
    {
        using TextDocument document = TextDocument.Create();
        OdfTable table = document.AddTable(2, 2);
        table.GetCell(0, 0).AddParagraph("Name");
        table.GetCell(0, 1).AddParagraph("Value");
        table.GetCell(1, 0).AddParagraph("A | B");
        table.GetCell(1, 1).AddParagraph("42");

        string markdown = document.ToMarkdown();

        Assert.Contains("| Name | Value |", markdown);
        Assert.Contains("| --- | --- |", markdown);
        Assert.Contains("| A \\| B | 42 |", markdown);
    }

    [Fact]
    public void MarkdownExporterConvertsFootnotesAndEndnotes()
    {
        using TextDocument document = TextDocument.Create();
        document.AddParagraph("Alpha").AddFootnote("1", "Footnote body.");
        document.AddParagraph("Omega").AddEndnote("i", "Endnote body.");

        string markdown = document.ToMarkdown();

        Assert.Contains("Alpha[^1]", markdown);
        Assert.Contains("Omega[^i]", markdown);
        Assert.Contains("[^1]: Footnote body\\.", markdown);
        Assert.Contains("[^i]: Endnote body\\.", markdown);
    }

    [Fact]
    public void MarkdownExporterConvertsHyperlinksImagesAndComments()
    {
        using TextDocument document = TextDocument.Create();
        OdfParagraph paragraph = document.AddParagraph();
        paragraph.AddTextRun("See ");
        paragraph.AddHyperlink("https://example.invalid/docs?a=1&b=2", "docs");
        paragraph.AddTextRun(" and ");
        OdfImage image = paragraph.AddImage("Pictures/chart.png", OdfLength.FromCentimeters(4), OdfLength.FromCentimeters(3), "Chart");
        image.AltText = "Revenue chart";
        paragraph.AddComment(new OdfComment("Ada", "Review this link."));

        string markdown = document.ToMarkdown();

        Assert.Contains("See [docs](<https://example.invalid/docs?a=1&b=2>)", markdown);
        Assert.Contains("![Revenue chart](<Pictures/chart.png>)", markdown);
        Assert.Contains("[^comment-1]", markdown);
        Assert.Contains("[^comment-1]: Comment by Ada: Review this link\\.", markdown);
    }

    [Fact]
    public void RtfExporterConvertsStrikethroughRuns()
    {
        using TextDocument document = TextDocument.Create();
        document.AddParagraph().AddTextRun("Removed").WithStrikethrough();

        string rtf = document.ToRtf();

        Assert.Contains(@"{\strike Removed}", rtf);
    }

    [Fact]
    public void RtfExporterConvertsSuperscriptAndSubscriptRuns()
    {
        using TextDocument document = TextDocument.Create();
        OdfParagraph paragraph = document.AddParagraph();
        paragraph.AddTextRun("E=mc");
        paragraph.AddTextRun("2").WithSuperscript();
        paragraph.AddTextRun(" H");
        paragraph.AddTextRun("2").WithSubscript();
        paragraph.AddTextRun("O");

        string rtf = document.ToRtf();

        Assert.Contains(@"{\super 2}", rtf);
        Assert.Contains(@"{\sub 2}", rtf);
    }

    [Fact]
    public void RtfExporterConvertsTextTables()
    {
        using TextDocument document = TextDocument.Create();
        OdfTable table = document.AddTable(1, 2);
        table.GetCell(0, 0).AddParagraph("Left");
        table.GetCell(0, 1).AddParagraph("Right");

        string rtf = document.ToRtf();

        Assert.Contains(@"\trowd\trautofit1", rtf);
        Assert.Contains(@"\cellx2000", rtf);
        Assert.Contains(@"\cellx4000", rtf);
        Assert.Contains(@"Left\cell", rtf);
        Assert.Contains(@"Right\cell", rtf);
        Assert.Contains(@"\row", rtf);
    }

    [Fact]
    public void RtfExporterConvertsFootnotes()
    {
        using TextDocument document = TextDocument.Create();
        document.AddParagraph("Alpha").AddFootnote("1", "Footnote body.");

        string rtf = document.ToRtf();

        Assert.Contains(@"Alpha1{\footnote 1 Footnote body.}", rtf);
    }

    [Fact]
    public void RtfExporterConvertsHyperlinksImagesAndComments()
    {
        using TextDocument document = TextDocument.Create();
        OdfParagraph paragraph = document.AddParagraph();
        paragraph.AddTextRun("See ");
        paragraph.AddHyperlink("https://example.invalid/docs", "docs");
        paragraph.AddTextRun(" and ");
        OdfImage image = paragraph.AddImage("Pictures/chart.png", OdfLength.FromCentimeters(4), OdfLength.FromCentimeters(3), "Chart");
        image.AltText = "Revenue chart";
        paragraph.AddComment(new OdfComment("Ada", "Review this link."));

        string rtf = document.ToRtf();

        Assert.Contains(@"{\field{\*\fldinst HYPERLINK ""https://example.invalid/docs""}{\fldrslt docs}}", rtf);
        Assert.Contains("[Image: Revenue chart (Pictures/chart.png)]", rtf);
        Assert.Contains("[Comment by Ada: Review this link.]", rtf);
    }

    [Fact]
    public void RtfExporterConvertsParagraphAlignment()
    {
        using TextDocument document = TextDocument.Create();
        document.AddParagraph("Centered").HorizontalAlignment = "center";
        document.AddParagraph("Right").HorizontalAlignment = "right";
        document.AddParagraph("Justified").HorizontalAlignment = "justify";

        string rtf = document.ToRtf();

        Assert.Contains(@"\pard\qc\fs24 Centered\par", rtf);
        Assert.Contains(@"\pard\qr\fs24 Right\par", rtf);
        Assert.Contains(@"\pard\qj\fs24 Justified\par", rtf);
    }

    [Fact]
    public void RtfImporterConvertsParagraphAlignment()
    {
        const string rtf = @"{\rtf1\ansi\pard\qc Centered\par\pard\qr Right\par\pard\qj Justified\par\pard Plain\par}";

        using TextDocument document = rtf.ToOdtTextDocumentFromRtf();

        OdfNode[] paragraphs = document.BodyTextRoot.Children
            .Where(node => node.LocalName == "p" && node.NamespaceUri == OdfNamespaces.Text)
            .ToArray();

        Assert.Equal(4, paragraphs.Length);
        Assert.Equal("center", ReadParagraphTextAlign(document, paragraphs[0]));
        Assert.Equal("right", ReadParagraphTextAlign(document, paragraphs[1]));
        Assert.Equal("justify", ReadParagraphTextAlign(document, paragraphs[2]));
        Assert.Null(ReadParagraphTextAlign(document, paragraphs[3]));
    }

    [Fact]
    public void RtfExporterConvertsParagraphIndents()
    {
        using TextDocument document = TextDocument.Create();
        OdfParagraph paragraph = document.AddParagraph("Indented");
        paragraph.Style.MarginLeft = "36pt";
        paragraph.Style.MarginRight = "18pt";
        paragraph.Style.TextIndent = "-9pt";

        string rtf = document.ToRtf();

        Assert.Contains(@"\pard\li720\ri360\fi-180\fs24 Indented\par", rtf);
    }

    [Fact]
    public void RtfImporterConvertsParagraphIndents()
    {
        const string rtf = @"{\rtf1\ansi\pard\li720\ri360\fi-180 Indented\par}";

        using TextDocument document = rtf.ToOdtTextDocumentFromRtf();

        OdfNode paragraph = Assert.Single(document.BodyTextRoot.Children, node => node.LocalName == "p" && node.NamespaceUri == OdfNamespaces.Text);
        Assert.Equal("36pt", ReadParagraphStyleProperty(document, paragraph, "margin-left"));
        Assert.Equal("18pt", ReadParagraphStyleProperty(document, paragraph, "margin-right"));
        Assert.Equal("-9pt", ReadParagraphStyleProperty(document, paragraph, "text-indent"));
    }

    [Fact]
    public void RtfExporterConvertsParagraphSpacing()
    {
        using TextDocument document = TextDocument.Create();
        OdfParagraph paragraph = document.AddParagraph("Spaced");
        paragraph.Style.MarginTop = "12pt";
        paragraph.Style.MarginBottom = "6pt";
        paragraph.Style.LineSpacing = "18pt";

        string rtf = document.ToRtf();

        Assert.Contains(@"\pard\sb240\sa120\sl360\fs24 Spaced\par", rtf);
    }

    [Fact]
    public void RtfImporterConvertsParagraphSpacing()
    {
        const string rtf = @"{\rtf1\ansi\pard\sb240\sa120\sl360\slmult0 Spaced\par}";

        using TextDocument document = rtf.ToOdtTextDocumentFromRtf();

        OdfNode paragraph = Assert.Single(document.BodyTextRoot.Children, node => node.LocalName == "p" && node.NamespaceUri == OdfNamespaces.Text);
        Assert.Equal("12pt", ReadParagraphStyleProperty(document, paragraph, "margin-top"));
        Assert.Equal("6pt", ReadParagraphStyleProperty(document, paragraph, "margin-bottom"));
        Assert.Equal("18pt", ReadParagraphStyleProperty(document, paragraph, "line-height"));
    }

    [Fact]
    public void RtfParagraphFormattingUsesInvariantCulture()
    {
        CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;
        CultureInfo originalUICulture = Thread.CurrentThread.CurrentUICulture;
        try
        {
            var turkish = CultureInfo.GetCultureInfo("tr-TR");
            Thread.CurrentThread.CurrentCulture = turkish;
            Thread.CurrentThread.CurrentUICulture = turkish;

            using TextDocument source = TextDocument.Create();
            OdfParagraph sourceParagraph = source.AddParagraph("Culture");
            sourceParagraph.Style.MarginTop = "1.5pt";
            sourceParagraph.Style.LineSpacing = "18pt";

            string rtf = source.ToRtf();
            using TextDocument imported = rtf.ToOdtTextDocumentFromRtf();

            Assert.Contains(@"\sb30\sl360", rtf);
            OdfNode paragraph = Assert.Single(imported.BodyTextRoot.Children, node => node.LocalName == "p" && node.NamespaceUri == OdfNamespaces.Text);
            Assert.Equal("1.5pt", ReadParagraphStyleProperty(imported, paragraph, "margin-top"));
            Assert.Equal("18pt", ReadParagraphStyleProperty(imported, paragraph, "line-height"));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
            Thread.CurrentThread.CurrentUICulture = originalUICulture;
        }
    }

    [Fact]
    public void RtfExporterConvertsSoftPageBreaks()
    {
        using TextDocument document = TextDocument.Create();
        OdfParagraph paragraph = document.AddParagraph();
        paragraph.AddTextRun("Before");
        paragraph.AddSoftPageBreak();
        paragraph.AddTextRun("After");

        string rtf = document.ToRtf();

        Assert.Contains(@"Before\page After", rtf);
    }

    [Fact]
    public void RtfImporterConvertsPageBreaks()
    {
        const string rtf = @"{\rtf1\ansi Before\page After}";

        using TextDocument document = rtf.ToOdtTextDocumentFromRtf();

        OdfNode paragraph = Assert.Single(document.BodyTextRoot.Children, node => node.LocalName == "p" && node.NamespaceUri == OdfNamespaces.Text);
        Assert.Contains(paragraph.Children, node => node.LocalName == "soft-page-break" && node.NamespaceUri == OdfNamespaces.Text);
        Assert.Equal("BeforeAfter", paragraph.TextContent);
    }

    [Fact]
    public void RtfExporterConvertsTypographicSymbols()
    {
        using TextDocument document = TextDocument.Create();
        document.AddParagraph("\u2018Single\u2019 \u201CDouble\u201D A\u2013B C\u2014D");

        string rtf = document.ToRtf();

        Assert.Contains(@"\lquote Single\rquote  \ldblquote Double\rdblquote  A\endash B C\emdash D", rtf);
    }

    [Fact]
    public void RtfImporterConvertsTypographicSymbols()
    {
        const string rtf = @"{\rtf1\ansi \lquote Single\rquote  \ldblquote Double\rdblquote  A\endash B C\emdash D}";

        using TextDocument document = rtf.ToOdtTextDocumentFromRtf();

        OdfNode paragraph = Assert.Single(document.BodyTextRoot.Children, node => node.LocalName == "p" && node.NamespaceUri == OdfNamespaces.Text);
        Assert.Equal("\u2018Single\u2019 \u201CDouble\u201D A\u2013B C\u2014D", paragraph.TextContent);
    }

    [Fact]
    public void RtfImporterDecodesAnsiHexEscapes()
    {
        const string rtf = @"{\rtf1\ansi Caf\'e9 \'5c \'7b \'7d \'96 dash \'80 euro}";

        using TextDocument document = rtf.ToOdtTextDocumentFromRtf();

        OdfNode paragraph = Assert.Single(document.BodyTextRoot.Children, node => node.LocalName == "p" && node.NamespaceUri == OdfNamespaces.Text);
        Assert.Equal("Café \\ { } \u2013 dash \u20AC euro", paragraph.TextContent);
    }

    [Fact]
    public void RtfImporterSkipsUnicodeFallbackText()
    {
        const string rtf = @"{\rtf1\ansi\uc1 Unicode \u8211? dash \u8364\'80 euro \uc0\u8211? kept}";

        using TextDocument document = rtf.ToOdtTextDocumentFromRtf();

        OdfNode paragraph = Assert.Single(document.BodyTextRoot.Children, node => node.LocalName == "p" && node.NamespaceUri == OdfNamespaces.Text);
        Assert.Equal("Unicode \u2013 dash \u20AC euro \u2013? kept", paragraph.TextContent);
    }

    [Fact]
    public void RtfExporterConvertsTypographicSpacesAndBullet()
    {
        using TextDocument document = TextDocument.Create();
        document.AddParagraph("A\u2002B\u2003C\u2005D \u2022 item");

        string rtf = document.ToRtf();

        Assert.Contains(@"\enspace ", rtf);
        Assert.Contains(@"\emspace ", rtf);
        Assert.Contains(@"\qmspace ", rtf);
        Assert.Contains(@"\bullet ", rtf);
    }

    [Fact]
    public void RtfImporterConvertsTypographicSpacesAndBullet()
    {
        const string rtf = @"{\rtf1\ansi A\enspace B\emspace C\qmspace D \bullet item}";

        using TextDocument document = rtf.ToOdtTextDocumentFromRtf();

        OdfNode paragraph = Assert.Single(document.BodyTextRoot.Children, node => node.LocalName == "p" && node.NamespaceUri == OdfNamespaces.Text);
        Assert.Equal("A\u2002B\u2003C\u2005D \u2022item", paragraph.TextContent);
    }

    [Fact]
    public void RtfExporterConvertsNonbreakingAndOptionalHyphens()
    {
        using TextDocument document = TextDocument.Create();
        document.AddParagraph("No\u00A0break soft\u00ADhyphen non\u2011breaking");

        string rtf = document.ToRtf();

        Assert.Contains(@"No\~break soft\-hyphen non\_breaking", rtf);
    }

    [Fact]
    public void RtfImporterConvertsNonbreakingAndOptionalHyphens()
    {
        const string rtf = @"{\rtf1\ansi No\~break soft\-hyphen non\_breaking}";

        using TextDocument document = rtf.ToOdtTextDocumentFromRtf();

        OdfNode paragraph = Assert.Single(document.BodyTextRoot.Children, node => node.LocalName == "p" && node.NamespaceUri == OdfNamespaces.Text);
        Assert.Equal("No\u00A0break soft\u00ADhyphen non\u2011breaking", paragraph.TextContent);
    }

    [Fact]
    public void RtfImporterPlainResetsInlineStyle()
    {
        const string rtf = @"{\rtf1\ansi {\b\i\ul Styled} \plain Plain}";

        using TextDocument document = rtf.ToOdtTextDocumentFromRtf();

        OdfNode paragraph = Assert.Single(document.BodyTextRoot.Children, node => node.LocalName == "p" && node.NamespaceUri == OdfNamespaces.Text);
        OdfNode styledRun = Assert.Single(paragraph.Children, node => node.LocalName == "span" && node.TextContent == "Styled");
        string? styledName = styledRun.GetAttribute("style-name", OdfNamespaces.Text);
        Assert.NotNull(styledName);
        Assert.Equal("bold", document.StyleEngine.GetStyleProperty(styledName, "font-weight", OdfNamespaces.Fo, "text"));
        Assert.Equal("italic", document.StyleEngine.GetStyleProperty(styledName, "font-style", OdfNamespaces.Fo, "text"));
        Assert.Equal("solid", document.StyleEngine.GetStyleProperty(styledName, "text-underline-style", OdfNamespaces.Style, "text"));

        OdfNode plainRun = Assert.Single(paragraph.Children, node => node.LocalName == "span" && node.TextContent == "Plain");
        Assert.True(string.IsNullOrEmpty(plainRun.GetAttribute("style-name", OdfNamespaces.Text)));
    }

    [Fact]
    public void RtfImporterRoundTripsStrikethroughRuns()
    {
        using TextDocument source = TextDocument.Create();
        source.AddParagraph().AddTextRun("Removed").WithStrikethrough();

        string rtf = source.ToRtf();
        using TextDocument document = rtf.ToOdtTextDocumentFromRtf();

        OdfNode paragraph = Assert.Single(document.BodyTextRoot.Children, node => node.LocalName == "p" && node.NamespaceUri == OdfNamespaces.Text);
        OdfNode removedRun = Assert.Single(paragraph.Children, node => node.LocalName == "span" && node.TextContent == "Removed");
        string? styleName = removedRun.GetAttribute("style-name", OdfNamespaces.Text);
        Assert.NotNull(styleName);
        Assert.Equal("solid", document.StyleEngine.GetStyleProperty(styleName, "text-line-through-style", OdfNamespaces.Style, "text"));
    }

    [Fact]
    public void RtfImporterRoundTripsSuperscriptAndSubscriptRuns()
    {
        const string rtf = @"{\rtf1\ansi E=mc{\super 2} H{\sub 2}{\nosupersub O}}";

        using TextDocument document = rtf.ToOdtTextDocumentFromRtf();

        OdfNode paragraph = Assert.Single(document.BodyTextRoot.Children, node => node.LocalName == "p" && node.NamespaceUri == OdfNamespaces.Text);
        OdfNode[] runs = paragraph.Children.Where(node => node.LocalName == "span" && node.TextContent == "2").ToArray();
        Assert.Equal(2, runs.Length);
        string? superscriptStyle = runs[0].GetAttribute("style-name", OdfNamespaces.Text);
        Assert.NotNull(superscriptStyle);
        Assert.Equal("super", document.StyleEngine.GetStyleProperty(superscriptStyle, "text-position", OdfNamespaces.Style, "text"));

        string? subscriptStyle = runs[1].GetAttribute("style-name", OdfNamespaces.Text);
        Assert.NotNull(subscriptStyle);
        Assert.Equal("sub", document.StyleEngine.GetStyleProperty(subscriptStyle, "text-position", OdfNamespaces.Style, "text"));
        Assert.EndsWith("O", paragraph.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void RtfImporterRoundTripsTextTables()
    {
        using TextDocument source = TextDocument.Create();
        OdfTable table = source.AddTable(2, 2);
        table.GetCell(0, 0).AddParagraph("Name");
        table.GetCell(0, 1).AddParagraph("Value");
        table.GetCell(1, 0).AddParagraph("Alpha");
        table.GetCell(1, 1).AddParagraph("42");
        source.AddParagraph("After table");

        string rtf = source.ToRtf();
        using TextDocument document = rtf.ToOdtTextDocumentFromRtf();

        OdfNode importedTable = Assert.Single(document.BodyTextRoot.Children, node => node.LocalName == "table" && node.NamespaceUri == OdfNamespaces.Table);
        Assert.Contains(importedTable.Descendants(), node => node.LocalName == "p" && node.TextContent == "Name");
        Assert.Contains(importedTable.Descendants(), node => node.LocalName == "p" && node.TextContent == "Value");
        Assert.Contains(importedTable.Descendants(), node => node.LocalName == "p" && node.TextContent == "Alpha");
        Assert.Contains(importedTable.Descendants(), node => node.LocalName == "p" && node.TextContent == "42");
        Assert.Contains(document.BodyTextRoot.Children, node => node.LocalName == "p" && node.TextContent == "After table");
    }
    [Fact]
    public void RtfImporterRoundTripsStyledTextFootnotesAndHyperlinks()
    {
        using TextDocument source = TextDocument.Create();
        OdfParagraph paragraph = source.AddParagraph();
        paragraph.AddTextRun("Hello ");
        OdfTextRun styled = paragraph.AddTextRun("Styled").WithBold().WithItalic().WithFontSize("14pt").WithColor("#0066CC");
        styled.IsUnderline = true;
        paragraph.AddTextRun(" ");
        paragraph.AddHyperlink("https://example.invalid/docs", "docs");
        paragraph.AddTextRun(" ");
        paragraph.AddFootnote("1", "Footnote body.");

        string rtf = source.ToRtf();
        using TextDocument document = rtf.ToOdtTextDocumentFromRtf();

        OdfNode convertedParagraph = Assert.Single(document.BodyTextRoot.Children, node => node.LocalName == "p" && node.NamespaceUri == OdfNamespaces.Text);
        Assert.Contains(convertedParagraph.TextContent, text => text == 'H');
        OdfNode styledSpan = Assert.Single(convertedParagraph.Children, node => node.LocalName == "span" && node.TextContent == "Styled");
        string? styledName = styledSpan.GetAttribute("style-name", OdfNamespaces.Text);
        Assert.NotNull(styledName);
        Assert.Equal("bold", document.StyleEngine.GetStyleProperty(styledName, "font-weight", OdfNamespaces.Fo, "text"));
        Assert.Equal("italic", document.StyleEngine.GetStyleProperty(styledName, "font-style", OdfNamespaces.Fo, "text"));
        Assert.Equal("solid", document.StyleEngine.GetStyleProperty(styledName, "text-underline-style", OdfNamespaces.Style, "text"));
        Assert.Equal("14pt", document.StyleEngine.GetStyleProperty(styledName, "font-size", OdfNamespaces.Fo, "text"));
        Assert.Equal("#0066CC", document.StyleEngine.GetStyleProperty(styledName, "color", OdfNamespaces.Fo, "text"));
        OdfNode link = Assert.Single(convertedParagraph.Children, node => node.LocalName == "a" && node.NamespaceUri == OdfNamespaces.Text);
        Assert.Equal("https://example.invalid/docs", link.GetAttribute("href", OdfNamespaces.XLink));
        OdfFootnoteInfo footnote = Assert.Single(document.GetFootnotes());
        Assert.Equal("1", footnote.Citation);
        Assert.Equal("Footnote body.", footnote.BodyText);
    }

    [Fact]
    public void RtfImporterRoundTripsImageReferencesAndComments()
    {
        using TextDocument source = TextDocument.Create();
        OdfParagraph paragraph = source.AddParagraph();
        paragraph.AddTextRun("See ");
        OdfImage image = paragraph.AddImage("Pictures/chart.png", OdfLength.FromCentimeters(4), OdfLength.FromCentimeters(3), "Chart");
        image.AltText = "Revenue chart";
        paragraph.AddTextRun(" and note ");
        paragraph.AddComment(new OdfComment("Ada", "Review this link."));

        string rtf = source.ToRtf();
        using TextDocument document = rtf.ToOdtTextDocumentFromRtf();

        OdfNode importedImage = Assert.Single(document.BodyTextRoot.Descendants(), node => node.LocalName == "image" && node.NamespaceUri == OdfNamespaces.Draw);
        Assert.Equal("Pictures/chart.png", importedImage.GetAttribute("href", OdfNamespaces.XLink));
        OdfNode importedFrame = Assert.Single(document.BodyTextRoot.Descendants(), node => node.LocalName == "frame" && node.NamespaceUri == OdfNamespaces.Draw);
        Assert.Contains(importedFrame.Descendants(), node => node.LocalName == "desc" && node.TextContent == "Revenue chart");
        OdfCommentInfo comment = Assert.Single(document.GetCommentInfos());
        Assert.Equal("Ada", comment.Author);
        Assert.Equal("Review this link.", comment.Text);
    }

    [Fact]
    public void RtfImporterConvertsStandardAnnotationGroups()
    {
        const string rtf = @"{\rtf1\ansi Intro {\*\atnid c1}{\*\atnauthor Ada}{\*\annotation Review this link.\line Needs follow-up with Caf\'e9.} outro}";

        using TextDocument document = rtf.ToOdtTextDocumentFromRtf();

        Assert.Contains(document.BodyTextRoot.Descendants(), node => node.LocalName == "p" && node.TextContent.Contains("Intro"));
        OdfCommentInfo comment = Assert.Single(document.GetCommentInfos());
        Assert.Equal("c1", comment.Name);
        Assert.Equal("Ada", comment.Author);
        Assert.Equal("Review this link.\nNeeds follow-up with Café.", comment.Text);
    }

    [Fact]
    public void RtfImporterConvertsEmbeddedPngPictures()
    {
        const string pngHex =
            "89504E470D0A1A0A0000000D4948445200000001000000010804000000B51C0C020000000B4944415478DA63FCFF1F0003030200EFBFA7DB0000000049454E44AE426082";
        string rtf = @"{\rtf1\ansi Before {\pict\pngblip\picw1\pich1\picwgoal1440\pichgoal720 " + pngHex + @"} After}";

        using TextDocument document = rtf.ToOdtTextDocumentFromRtf();

        OdfNode importedImage = Assert.Single(document.BodyTextRoot.Descendants(), node => node.LocalName == "image" && node.NamespaceUri == OdfNamespaces.Draw);
        string href = importedImage.GetAttribute("href", OdfNamespaces.XLink)!;
        Assert.StartsWith("Pictures/", href);
        Assert.EndsWith(".png", href);
        Assert.True(document.Package.HasEntry(href));
        OdfNode importedFrame = Assert.Single(document.BodyTextRoot.Descendants(), node => node.LocalName == "frame" && node.NamespaceUri == OdfNamespaces.Draw);
        Assert.Equal("72pt", importedFrame.GetAttribute("width", OdfNamespaces.Svg));
        Assert.Equal("36pt", importedFrame.GetAttribute("height", OdfNamespaces.Svg));
        Assert.Contains(importedFrame.Descendants(), node => node.LocalName == "desc" && node.TextContent == "RTF image");
    }

    [Fact]
    public void RtfImporterReadsFilesThroughFriendlyApis()
    {
        using TextDocument source = TextDocument.Create();
        source.AddParagraph("Managed RTF");
        string root = Path.Combine(Path.GetTempPath(), "OdfKit_RtfImport_" + Guid.NewGuid().ToString("N"));
        string rtfPath = Path.Combine(root, "document.rtf");

        try
        {
            Directory.CreateDirectory(root);
            source.SaveAsRtf(rtfPath);

            using TextDocument loadedByPath = OdfManagedTextExportExtensions.LoadRtfAsOdt(rtfPath);
            using TextDocument loadedByFile = new FileInfo(rtfPath).ToOdtTextDocumentFromRtf();
            Assert.Contains(loadedByPath.BodyTextRoot.Descendants(), node => node.LocalName == "p" && node.TextContent == "Managed RTF");
            Assert.Contains(loadedByFile.BodyTextRoot.Descendants(), node => node.LocalName == "p" && node.TextContent == "Managed RTF");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public void MarkdownExporterSupportsCommonGitHubAndGitLabFlavors()
    {
        using TextDocument document = TextDocument.Create();
        OdfTable table = document.AddTable(1, 2);
        table.GetCell(0, 0).AddParagraph("Left");
        table.GetCell(0, 1).AddParagraph("Right");

        string github = document.ToMarkdown(new OdfMarkdownExportOptions { Flavor = OdfMarkdownFlavor.GitHubFlavored });
        string gitlab = document.ToMarkdown(OdfMarkdownExportOptions.GitLab);
        string commonMark = document.ToMarkdown(OdfMarkdownExportOptions.CommonMark);

        Assert.Contains("| Left | Right |", github);
        Assert.Contains("| Left | Right |", gitlab);
        Assert.DoesNotContain("| --- |", commonMark);
        Assert.Contains("Left\tRight", commonMark);
    }

    [Fact]
    public void MarkdownImporterHonorsInputFlavorOptions()
    {
        const string markdown =
            "| Name | Value |\n" +
            "| --- | --- |\n" +
            "| Left | Right |\n";

        using TextDocument github = markdown.ToOdtTextDocument(new OdfMarkdownImportOptions { Flavor = OdfMarkdownFlavor.GitHubFlavored });
        using TextDocument gitlab = markdown.ToOdtTextDocument(new OdfMarkdownImportOptions { Flavor = OdfMarkdownFlavor.GitLabFlavored });
        using TextDocument commonMark = markdown.ToOdtTextDocument(new OdfMarkdownImportOptions { Flavor = OdfMarkdownFlavor.CommonMark });

        Assert.Single(github.BodyTextRoot.Children, node => node.LocalName == "table" && node.NamespaceUri == OdfNamespaces.Table);
        Assert.Single(gitlab.BodyTextRoot.Children, node => node.LocalName == "table" && node.NamespaceUri == OdfNamespaces.Table);
        Assert.Empty(commonMark.BodyTextRoot.Children.Where(node => node.LocalName == "table" && node.NamespaceUri == OdfNamespaces.Table));
    }

    [Fact]
    public void MarkdownImporterConvertsItalicBoldItalicAndHtmlSpanStyles()
    {
        const string markdown =
            "*Italic* ***Both*** <span style=\"font-style:italic; text-decoration:underline; font-size:14pt; color:#0066CC\">Style &amp; Color</span>";

        using TextDocument document = markdown.ToOdtTextDocument();

        OdfNode paragraph = Assert.Single(document.BodyTextRoot.Children, node => node.LocalName == "p" && node.NamespaceUri == OdfNamespaces.Text);
        OdfNode italic = Assert.Single(paragraph.Children, node => node.LocalName == "span" && node.TextContent == "Italic");
        OdfNode both = Assert.Single(paragraph.Children, node => node.LocalName == "span" && node.TextContent == "Both");
        OdfNode styled = Assert.Single(paragraph.Children, node => node.LocalName == "span" && node.TextContent == "Style & Color");

        string? italicStyle = italic.GetAttribute("style-name", OdfNamespaces.Text);
        string? bothStyle = both.GetAttribute("style-name", OdfNamespaces.Text);
        string? styledStyle = styled.GetAttribute("style-name", OdfNamespaces.Text);
        Assert.NotNull(italicStyle);
        Assert.NotNull(bothStyle);
        Assert.NotNull(styledStyle);
        Assert.Equal("italic", document.StyleEngine.GetStyleProperty(italicStyle, "font-style", OdfNamespaces.Fo, "text"));
        Assert.Equal("bold", document.StyleEngine.GetStyleProperty(bothStyle, "font-weight", OdfNamespaces.Fo, "text"));
        Assert.Equal("italic", document.StyleEngine.GetStyleProperty(bothStyle, "font-style", OdfNamespaces.Fo, "text"));
        Assert.Equal("italic", document.StyleEngine.GetStyleProperty(styledStyle, "font-style", OdfNamespaces.Fo, "text"));
        Assert.Equal("solid", document.StyleEngine.GetStyleProperty(styledStyle, "text-underline-style", OdfNamespaces.Style, "text"));
        Assert.Equal("14pt", document.StyleEngine.GetStyleProperty(styledStyle, "font-size", OdfNamespaces.Fo, "text"));
        Assert.Equal("#0066CC", document.StyleEngine.GetStyleProperty(styledStyle, "color", OdfNamespaces.Fo, "text"));
    }

    [Fact]
    public void MarkdownBasicImporterPreservesInlineHtmlTextWithoutStyles()
    {
        const string markdown = "Basic <span style=\"font-style:italic; color:#0066CC\">Style &amp; Color</span> Text";

        using TextDocument document = markdown.ToOdtTextDocument(new OdfMarkdownImportOptions { Flavor = OdfMarkdownFlavor.Basic });

        OdfNode paragraph = Assert.Single(document.BodyTextRoot.Children, node => node.LocalName == "p" && node.NamespaceUri == OdfNamespaces.Text);
        Assert.Equal("Basic Style & Color Text", paragraph.TextContent);
        Assert.DoesNotContain(paragraph.Descendants(), node => !string.IsNullOrEmpty(node.GetAttribute("style-name", OdfNamespaces.Text)));
    }

    [Fact]
    public void MarkdownImporterRoundTripsCommentsAndFootnotes()
    {
        const string markdown =
            "# Review\n\n" +
            "See paragraph[^comment-1] and footnote[^1].\n\n" +
            "[^comment-1]: Comment by Ada: Review this link\\.\n" +
            "[^1]: Footnote body\\.\n";

        using TextDocument document = markdown.ToOdtTextDocument();

        Assert.Contains(document.BodyTextRoot.Descendants(), node => node.LocalName == "h" && node.TextContent == "Review");
        Assert.Contains(document.BodyTextRoot.Descendants(), node => node.LocalName == "p" && node.TextContent.Contains("See paragraph"));
        OdfCommentInfo comment = Assert.Single(document.GetCommentInfos());
        Assert.Equal("Ada", comment.Author);
        Assert.Equal("Review this link.", comment.Text);
        OdfFootnoteInfo footnote = Assert.Single(document.GetFootnotes());
        Assert.Equal("1", footnote.Citation);
        Assert.Equal("Footnote body.", footnote.BodyText);

        string roundTrip = document.ToMarkdown();
        Assert.Contains("[^comment-1]: Comment by Ada: Review this link\\.", roundTrip);
        Assert.Contains("[^1]: Footnote body\\.", roundTrip);
    }

    [Fact]
    public void MarkdownImporterConvertsHeadingsParagraphsAndLists()
    {
        const string markdown =
            "## Roadmap\n\n" +
            "ODF first, LibreOffice fallback\\.\n\n" +
            "- Markdown\n" +
            "- RTF\n";

        using TextDocument document = markdown.ToOdtTextDocument();

        Assert.Contains(document.BodyTextRoot.Descendants(), node => node.LocalName == "h" && node.TextContent == "Roadmap");
        Assert.Contains(document.BodyTextRoot.Descendants(), node => node.LocalName == "p" && node.TextContent == "ODF first, LibreOffice fallback.");
        OdfNode list = Assert.Single(document.BodyTextRoot.Descendants(), node => node.LocalName == "list" && node.NamespaceUri == OdfNamespaces.Text);
        Assert.Contains(list.Descendants(), node => node.LocalName == "p" && node.TextContent == "Markdown");
        Assert.Contains(list.Descendants(), node => node.LocalName == "p" && node.TextContent == "RTF");
    }

    [Fact]
    public void MarkdownImporterConvertsTablesLinksImagesAndBoldRuns()
    {
        const string markdown =
            "See **bold** [docs](<https://example.invalid/docs?a=1&b=2>) and ![Revenue chart](<Pictures/chart.png>).\n\n" +
            "| Name | Value |\n" +
            "| --- | --- |\n" +
            "| A \\| B | 42 |\n";

        using TextDocument document = markdown.ToOdtTextDocument();

        OdfNode paragraph = Assert.Single(
            document.BodyTextRoot.Children,
            node => node.LocalName == "p" && node.NamespaceUri == OdfNamespaces.Text);
        Assert.Contains(paragraph.Children, node => node.LocalName == "span" && node.TextContent == "bold");
        OdfNode boldSpan = Assert.Single(paragraph.Children, node => node.LocalName == "span" && node.TextContent == "bold");
        string? boldStyle = boldSpan.GetAttribute("style-name", OdfNamespaces.Text);
        Assert.NotNull(boldStyle);
        Assert.Equal("bold", document.StyleEngine.GetStyleProperty(boldStyle, "font-weight", OdfNamespaces.Fo, "text"));
        OdfNode link = Assert.Single(paragraph.Children, node => node.LocalName == "a" && node.NamespaceUri == OdfNamespaces.Text);
        Assert.Equal("https://example.invalid/docs?a=1&b=2", link.GetAttribute("href", OdfNamespaces.XLink));
        OdfNode image = Assert.Single(paragraph.Descendants(), node => node.LocalName == "image" && node.NamespaceUri == OdfNamespaces.Draw);
        Assert.Equal("Pictures/chart.png", image.GetAttribute("href", OdfNamespaces.XLink));

        OdfNode table = Assert.Single(document.BodyTextRoot.Children, node => node.LocalName == "table" && node.NamespaceUri == OdfNamespaces.Table);
        Assert.Contains(table.Descendants(), node => node.LocalName == "p" && node.TextContent == "A | B");
        Assert.Contains(table.Descendants(), node => node.LocalName == "p" && node.TextContent == "42");

        string roundTrip = document.ToMarkdown();
        Assert.Contains("**bold**", roundTrip);
        Assert.Contains("[docs](<https://example.invalid/docs?a=1&b=2>)", roundTrip);
        Assert.Contains("![Revenue chart](<Pictures/chart.png>)", roundTrip);
        Assert.Contains("| A \\| B | 42 |", roundTrip);
    }

    [Fact]
    public void MarkdownFlavorPresetsAreFriendlyForExportAndImport()
    {
        using TextDocument source = TextDocument.Create();
        OdfTable table = source.AddTable(1, 2);
        table.GetCell(0, 0).AddParagraph("Left");
        table.GetCell(0, 1).AddParagraph("Right");

        string gitLab = source.ToMarkdown(OdfMarkdownExportOptions.GitLab);
        string commonMark = source.ToMarkdown(OdfMarkdownExportOptions.CommonMark);

        Assert.Contains("| Left | Right |", gitLab);
        Assert.Contains("Left\tRight", commonMark);

        using TextDocument importedGitLab = gitLab.ToOdtTextDocument(OdfMarkdownImportOptions.GitLab);
        using TextDocument importedCommonMark = gitLab.ToOdtTextDocument(OdfMarkdownImportOptions.CommonMark);

        Assert.Single(importedGitLab.BodyTextRoot.Children, node => node.LocalName == "table" && node.NamespaceUri == OdfNamespaces.Table);
        Assert.Empty(importedCommonMark.BodyTextRoot.Children.Where(node => node.LocalName == "table" && node.NamespaceUri == OdfNamespaces.Table));
    }

    [Fact]
    public void MarkdownExporterUsesGitLabStrikethroughSyntax()
    {
        using TextDocument document = TextDocument.Create();
        OdfParagraph paragraph = document.AddParagraph();
        paragraph.AddTextRun("Removed").WithStrikethrough();

        string gitlab = document.ToMarkdown(OdfMarkdownExportOptions.GitLab);
        string commonMark = document.ToMarkdown(OdfMarkdownExportOptions.CommonMark);
        string basic = document.ToMarkdown(OdfMarkdownExportOptions.Basic);

        Assert.Equal("~~Removed~~", gitlab);
        Assert.Equal("<span style=\"text-decoration:line-through\">Removed</span>", commonMark);
        Assert.Equal("Removed", basic);
    }

    [Fact]
    public void MarkdownImporterUsesMarkdigForGitHubAndGitLabTaskLists()
    {
        const string markdown = "- [x] Done\n- [ ] Todo\n";

        using TextDocument github = markdown.ToOdtTextDocument(new OdfMarkdownImportOptions { Flavor = OdfMarkdownFlavor.GitHubFlavored });
        using TextDocument gitlab = markdown.ToOdtTextDocument(new OdfMarkdownImportOptions { Flavor = OdfMarkdownFlavor.GitLabFlavored });
        using TextDocument commonMark = markdown.ToOdtTextDocument(new OdfMarkdownImportOptions { Flavor = OdfMarkdownFlavor.CommonMark });

        Assert.Contains(github.BodyTextRoot.Descendants(), node => node.LocalName == "p" && node.TextContent == "Done");
        Assert.Contains(github.BodyTextRoot.Descendants(), node => node.LocalName == "p" && node.TextContent == "Todo");
        Assert.Contains(gitlab.BodyTextRoot.Descendants(), node => node.LocalName == "p" && node.TextContent == "Done");
        Assert.Contains(gitlab.BodyTextRoot.Descendants(), node => node.LocalName == "p" && node.TextContent == "Todo");
        Assert.Contains(commonMark.BodyTextRoot.Descendants(), node => node.LocalName == "p" && node.TextContent == "[x] Done");
        Assert.Contains(commonMark.BodyTextRoot.Descendants(), node => node.LocalName == "p" && node.TextContent == "[ ] Todo");
    }

    [Fact]
    public void MarkdownImporterConvertsGitLabStrikethrough()
    {
        const string markdown = "Keep ~~Removed~~";

        using TextDocument gitlab = markdown.ToOdtTextDocument(new OdfMarkdownImportOptions { Flavor = OdfMarkdownFlavor.GitLabFlavored });
        using TextDocument commonMark = markdown.ToOdtTextDocument(new OdfMarkdownImportOptions { Flavor = OdfMarkdownFlavor.CommonMark });

        OdfNode gitlabParagraph = Assert.Single(gitlab.BodyTextRoot.Children, node => node.LocalName == "p" && node.NamespaceUri == OdfNamespaces.Text);
        OdfNode removedRun = Assert.Single(gitlabParagraph.Children, node => node.LocalName == "span" && node.TextContent == "Removed");
        string? styleName = removedRun.GetAttribute("style-name", OdfNamespaces.Text);
        Assert.NotNull(styleName);
        Assert.Equal("solid", gitlab.StyleEngine.GetStyleProperty(styleName, "text-line-through-style", OdfNamespaces.Style, "text"));

        Assert.Contains(commonMark.BodyTextRoot.Children, node => node.LocalName == "p" && node.TextContent == "Keep ~~Removed~~");
    }

    [Fact]
    public void ManagedTextExportExtensionsWriteFiles()
    {
        using TextDocument document = TextDocument.Create();
        document.AddParagraph("Managed first");
        string root = Path.Combine(Path.GetTempPath(), "OdfKit_ManagedExport_" + Guid.NewGuid().ToString("N"));
        string markdownPath = Path.Combine(root, "document.md");
        string rtfPath = Path.Combine(root, "document.rtf");
        string htmlPath = Path.Combine(root, "document.html");

        try
        {
            document.SaveAsMarkdown(markdownPath);
            document.SaveAsRtf(rtfPath);
            document.SaveAsHtml(htmlPath, new OdfHtmlExportOptions { FullPage = false });

            Assert.Contains("Managed first", File.ReadAllText(markdownPath));
            Assert.Contains("Managed first", File.ReadAllText(rtfPath));
            Assert.Contains("<p>Managed first</p>", File.ReadAllText(htmlPath));

            using TextDocument loadedByPath = OdfManagedTextExportExtensions.LoadMarkdownAsOdt(markdownPath);
            using TextDocument loadedByFile = new FileInfo(markdownPath).ToOdtTextDocument();
            Assert.Contains(loadedByPath.BodyTextRoot.Descendants(), node => node.LocalName == "p" && node.TextContent == "Managed first");
            Assert.Contains(loadedByFile.BodyTextRoot.Descendants(), node => node.LocalName == "p" && node.TextContent == "Managed first");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private static string? ReadParagraphTextAlign(TextDocument document, OdfNode paragraph)
    {
        return ReadParagraphStyleProperty(document, paragraph, "text-align");
    }

    private static string? ReadParagraphStyleProperty(TextDocument document, OdfNode paragraph, string propertyName)
    {
        string? styleName = paragraph.GetAttribute("style-name", OdfNamespaces.Text);
        return string.IsNullOrWhiteSpace(styleName)
            ? null
            : document.StyleEngine.GetStyleProperty(styleName!, propertyName, OdfNamespaces.Fo, "paragraph");
    }

    /// <summary>
    /// 驗證 Markdown 匯入與匯出中繼資料的往返保真度。
    /// </summary>
    [Fact]
    public void MarkdownRoundTripFidelityForMetadata()
    {
        using TextDocument doc = TextDocument.Create();
        doc.Metadata.Title = "我的測試標題";
        doc.Metadata.Creator = "臺灣作者";
        doc.Metadata.Subject = "測試主旨";
        doc.Metadata.Description = "這是一個測試描述";
        doc.Metadata.Language = "zh-TW";

        string markdown = doc.ToMarkdown();
        using TextDocument roundTripDoc = OdfMarkdownImporter.Import(markdown);

        Assert.Equal("我的測試標題", roundTripDoc.Metadata.Title);
        Assert.Equal("臺灣作者", roundTripDoc.Metadata.Creator);
        Assert.Equal("測試主旨", roundTripDoc.Metadata.Subject);
        Assert.Equal("這是一個測試描述", roundTripDoc.Metadata.Description);
        Assert.Equal("zh-TW", roundTripDoc.Metadata.Language);
    }

    /// <summary>
    /// 驗證 Markdown 匯入與匯出註解範圍的往返保真度。
    /// </summary>
    [Fact]
    public void MarkdownRoundTripFidelityForAnnotationRange()
    {
        using TextDocument doc = TextDocument.Create();
        OdfParagraph p = doc.AddParagraph();
        p.AddTextRun("前段文字 ");

        // 新增 annotation-start 節點
        var startNode = new OdfNode(OdfNodeType.Element, "annotation-start", OdfNamespaces.Office, "office");
        startNode.SetAttribute("name", OdfNamespaces.Office, "comment-台湾-1", "office");
        p.Node.AppendChild(startNode);

        // 新增 annotation 節點
        var annoNode = new OdfNode(OdfNodeType.Element, "annotation", OdfNamespaces.Office, "office");
        annoNode.SetAttribute("name", OdfNamespaces.Office, "comment-台湾-1", "office");
        var creator = new OdfNode(OdfNodeType.Element, "creator", OdfNamespaces.Dc, "dc") { TextContent = "朱自清" };
        var date = new OdfNode(OdfNodeType.Element, "date", OdfNamespaces.Dc, "dc") { TextContent = "2026-06-20T08:58:27Z" };
        var pText = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text") { TextContent = "背影" };
        annoNode.AppendChild(creator);
        annoNode.AppendChild(date);
        annoNode.AppendChild(pText);
        p.Node.AppendChild(annoNode);

        p.AddTextRun("被註解範圍");

        // 新增 annotation-end 節點
        var endNode = new OdfNode(OdfNodeType.Element, "annotation-end", OdfNamespaces.Office, "office");
        endNode.SetAttribute("name", OdfNamespaces.Office, "comment-台湾-1", "office");
        p.Node.AppendChild(endNode);

        p.AddTextRun(" 後段文字");

        string markdown = doc.ToMarkdown();
        using TextDocument roundTripDoc = OdfMarkdownImporter.Import(markdown);

        // 驗證 comments
        OdfCommentInfo comment = Assert.Single(roundTripDoc.GetCommentInfos());
        Assert.Equal("comment-台湾-1", comment.Name);
        Assert.Equal("朱自清", comment.Author);
        Assert.Equal("背影", comment.Text);

        // 驗證範圍節點
        var descNodes = roundTripDoc.BodyTextRoot.Descendants();
        Assert.Contains(descNodes, n => n.LocalName == "annotation-start" && n.GetAttribute("name", OdfNamespaces.Office) == "comment-台湾-1");
        Assert.Contains(descNodes, n => n.LocalName == "annotation-end" && n.GetAttribute("name", OdfNamespaces.Office) == "comment-台湾-1");
    }

    /// <summary>
    /// 驗證 RTF 匯入與匯出中繼資料的往返保真度。
    /// </summary>
    [Fact]
    public void RtfRoundTripFidelityForMetadata()
    {
        using TextDocument doc = TextDocument.Create();
        doc.Metadata.Title = "RTF 標題";
        doc.Metadata.Creator = "Ada";
        doc.Metadata.Subject = "RTF 主旨";
        doc.Metadata.Description = "RTF 描述";
        doc.Metadata.Language = "en-US";

        string rtf = OdfRtfExporter.Export(doc);
        using TextDocument roundTripDoc = OdfRtfImporter.Import(rtf);

        Assert.Equal("RTF 標題", roundTripDoc.Metadata.Title);
        Assert.Equal("Ada", roundTripDoc.Metadata.Creator);
        Assert.Equal("RTF 主旨", roundTripDoc.Metadata.Subject);
        Assert.Equal("RTF 描述", roundTripDoc.Metadata.Description);
        Assert.Equal("en-US", roundTripDoc.Metadata.Language);
    }

    /// <summary>
    /// 驗證 RTF 匯入與匯出註解範圍的往返保真度。
    /// </summary>
    [Fact]
    public void RtfRoundTripFidelityForAnnotationRange()
    {
        using TextDocument doc = TextDocument.Create();
        OdfParagraph p = doc.AddParagraph();
        p.AddTextRun("開頭 ");

        // 新增 annotation-start 節點
        var startNode = new OdfNode(OdfNodeType.Element, "annotation-start", OdfNamespaces.Office, "office");
        startNode.SetAttribute("name", OdfNamespaces.Office, "comment-rtf-1", "office");
        p.Node.AppendChild(startNode);

        // 新增 annotation 節點
        var annoNode = new OdfNode(OdfNodeType.Element, "annotation", OdfNamespaces.Office, "office");
        annoNode.SetAttribute("name", OdfNamespaces.Office, "comment-rtf-1", "office");
        var creator = new OdfNode(OdfNodeType.Element, "creator", OdfNamespaces.Dc, "dc") { TextContent = "臺灣Ada" };
        var date = new OdfNode(OdfNodeType.Element, "date", OdfNamespaces.Dc, "dc") { TextContent = "2026-06-20T08:58:27Z" };
        var pText = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text") { TextContent = "RTF 註解內文" };
        annoNode.AppendChild(creator);
        annoNode.AppendChild(date);
        annoNode.AppendChild(pText);
        p.Node.AppendChild(annoNode);

        p.AddTextRun("中間");

        // 新增 annotation-end 節點
        var endNode = new OdfNode(OdfNodeType.Element, "annotation-end", OdfNamespaces.Office, "office");
        endNode.SetAttribute("name", OdfNamespaces.Office, "comment-rtf-1", "office");
        p.Node.AppendChild(endNode);

        p.AddTextRun(" 結束");

        string rtf = OdfRtfExporter.Export(doc);
        using TextDocument roundTripDoc = OdfRtfImporter.Import(rtf);

        // 驗證 comments
        OdfCommentInfo comment = Assert.Single(roundTripDoc.GetCommentInfos());
        Assert.Equal("comment-1", comment.Name);
        Assert.Equal("臺灣Ada", comment.Author);
        Assert.Equal("RTF 註解內文", comment.Text);

        // 驗證範圍節點
        var descNodes = roundTripDoc.BodyTextRoot.Descendants();
        Assert.Contains(descNodes, n => n.LocalName == "annotation-start" && n.GetAttribute("name", OdfNamespaces.Office) == "comment-1");
        Assert.Contains(descNodes, n => n.LocalName == "annotation-end" && n.GetAttribute("name", OdfNamespaces.Office) == "comment-1");
    }

    /// <summary>
    /// 驗證 RTF 稀疏表格的對齊與合併之往返保真度。
    /// </summary>
    [Fact]
    public void RtfRoundTripSparseTableAlignmentAndMerging()
    {
        using TextDocument doc = TextDocument.Create();
        OdfTable table = doc.AddTable(2, 3);
        table.GetCell(0, 0).AddParagraph("A1");
        table.GetCell(0, 1).AddParagraph("A2-A3");
        table.MergeCells(0, 1, 1, 2);

        table.GetCell(1, 0).AddParagraph("B1");
        table.GetCell(1, 1).AddParagraph("B2");
        table.GetCell(1, 2).AddParagraph("B3");

        string rtf = OdfRtfExporter.Export(doc);
        using TextDocument roundTripDoc = OdfRtfImporter.Import(rtf);

        OdfTextTableInfo tableInfo = Assert.Single(roundTripDoc.Body.Tables);
        OdfNode tableNode = roundTripDoc.BodyTextRoot.Children.First(c => c.LocalName == "table" && c.NamespaceUri == OdfNamespaces.Table);
        OdfTable roundTripTable = new OdfTable(tableNode, 0, 0, roundTripDoc);

        List<OdfNode> rows = [];
        foreach (var child in roundTripTable.Node.Children)
        {
            if (child.LocalName == "table-row" && child.NamespaceUri == OdfNamespaces.Table)
            {
                rows.Add(child);
            }
        }
        Assert.Equal(2, rows.Count);

        List<OdfNode> firstRowCells = [];
        foreach (var child in rows[0].Children)
        {
            if ((child.LocalName == "table-cell" || child.LocalName == "covered-table-cell") && child.NamespaceUri == OdfNamespaces.Table)
            {
                firstRowCells.Add(child);
            }
        }
        Assert.Equal(3, firstRowCells.Count);
        Assert.Equal("table-cell", firstRowCells[0].LocalName);
        Assert.Equal("table-cell", firstRowCells[1].LocalName);
        Assert.Equal("covered-table-cell", firstRowCells[2].LocalName);
        Assert.Equal("2", firstRowCells[1].GetAttribute("number-columns-spanned", OdfNamespaces.Table));
    }
}
