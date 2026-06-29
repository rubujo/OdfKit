using System.Globalization;
using System;
using OdfKit.Compliance;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Text;
using OdfKit.Spreadsheet;
using OdfKit.Presentation;
using OdfKit.Styles;
using OdfKit.Extensions.Rendering;

namespace OdfKit.Tests
{
    [Collection("SequentialRenderingTests")]
    [Trait(TestCategories.Kind, TestCategories.Scenario)]
    public class PresentationAndRenderingTests
    {
        public PresentationAndRenderingTests()
        {
            OdfLocalizer.DefaultCulture = new CultureInfo("en");
        }

        #region 1. OdfComment Tests

        [Fact]
        public void TestOdfCommentRepliesAndXmlRoundtrip()
        {
            var date = new DateTime(2026, 6, 9, 12, 0, 0, DateTimeKind.Utc);
            var comment = new OdfComment("John Doe", "Parent comment text", date, "comment_id_123");

            comment.AddReply("Jane Smith", "First reply text");
            comment.AddReply(new OdfComment("Alice Green", "Second reply text", date.AddMinutes(5), "reply_id_456"));

            Assert.Equal("John Doe", comment.Author);
            Assert.Equal("Parent comment text", comment.Text);
            Assert.Equal(date, comment.Date);
            Assert.Equal("comment_id_123", comment.Name);
            Assert.Equal(2, comment.Replies.Count);
            Assert.Equal("Jane Smith", comment.Replies[0].Author);
            Assert.Equal("First reply text", comment.Replies[0].Text);
            Assert.Equal("Alice Green", comment.Replies[1].Author);
            Assert.Equal("Second reply text", comment.Replies[1].Text);
            Assert.Equal("reply_id_456", comment.Replies[1].Name);

            // Convert to XML
            var xmlNode = comment.ToXmlNode();
            Assert.NotNull(xmlNode);
            var rootNode = xmlNode.LocalName == "annotation-list" ? xmlNode.Children[0] : xmlNode;
            Assert.Equal("annotation", rootNode.LocalName);
            Assert.Equal(OdfNamespaces.Office, rootNode.NamespaceUri);
            Assert.Equal("comment_id_123", rootNode.GetAttribute("name", OdfNamespaces.Office));

            // Convert back from XML
            var roundtrip = OdfComment.FromXmlNode(xmlNode);
            Assert.Equal(comment.Author, roundtrip.Author);
            Assert.Equal(comment.Text, roundtrip.Text);
            Assert.Equal(comment.Date, roundtrip.Date);
            Assert.Equal(comment.Name, roundtrip.Name);
            Assert.Equal(comment.Replies.Count, roundtrip.Replies.Count);

            Assert.Equal(comment.Replies[0].Author, roundtrip.Replies[0].Author);
            Assert.Equal(comment.Replies[0].Text, roundtrip.Replies[0].Text);

            Assert.Equal(comment.Replies[1].Author, roundtrip.Replies[1].Author);
            Assert.Equal(comment.Replies[1].Text, roundtrip.Replies[1].Text);
            Assert.Equal(comment.Replies[1].Name, roundtrip.Replies[1].Name);
        }

        #endregion

        #region 2. TextDocument Tests

        [Fact]
        public void TestTextDocumentTOCAndFields()
        {
            using var ms = new MemoryStream();
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                var doc = new TextDocument(package);
                var p = doc.AddParagraph("Intro");

                doc.AddTableOfContents();
                p.AddPageNumberField();
                p.AddPageCountField();

                doc.Save();
            }

            ms.Position = 0;
            using (var package = OdfPackage.Open(ms))
            {
                var doc = new TextDocument(package);
                // Verify TOC element exists
                var tocNode = FindNodeByLocalName(doc.BodyTextRoot, "table-of-content");
                Assert.NotNull(tocNode);
                Assert.Equal("Table of Contents", tocNode.GetAttribute("name", OdfNamespaces.Text));

                // Verify update fields when opening view settings
                var settingsNode = doc.SettingsDom.Children[0];
                var configItem = FindConfigItemNode(settingsNode, "UpdateFieldsWhenOpening");
                Assert.NotNull(configItem);
                Assert.Equal("true", configItem.TextContent);

                // Verify page number/count elements in paragraph
                var pNode = doc.BodyTextRoot.Children[0];
                var pageNum = FindNodeByLocalName(pNode, "page-number");
                var pageCount = FindNodeByLocalName(pNode, "page-count");
                Assert.NotNull(pageNum);
                Assert.NotNull(pageCount);
            }
        }

        [Fact]
        public void TestTextDocumentTrackedChanges()
        {
            using var ms = new MemoryStream();
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                var doc = new TextDocument(package);

                // Construct text:tracked-changes and inline change-start/change-end
                // Structure:
                // <text:tracked-changes>
                //   <text:changed-region text:id="ch1"><text:insertion/></text:changed-region>
                //   <text:changed-region text:id="ch2"><text:deletion/></text:changed-region>
                // </text:tracked-changes>
                // <text:p>Hello <text:change-start text:change-id="ch1"/>inserted text<text:change-end text:change-id="ch1"/> and <text:change-start text:change-id="ch2"/>deleted text<text:change-end text:change-id="ch2"/>.</text:p>

                var trackedChanges = new OdfNode(OdfNodeType.Element, "tracked-changes", OdfNamespaces.Text, "text");

                var cr1 = new OdfNode(OdfNodeType.Element, "changed-region", OdfNamespaces.Text, "text");
                cr1.SetAttribute("id", OdfNamespaces.Text, "ch1", "text");
                cr1.AppendChild(new OdfNode(OdfNodeType.Element, "insertion", OdfNamespaces.Text, "text"));
                trackedChanges.AppendChild(cr1);

                var cr2 = new OdfNode(OdfNodeType.Element, "changed-region", OdfNamespaces.Text, "text");
                cr2.SetAttribute("id", OdfNamespaces.Text, "ch2", "text");
                cr2.AppendChild(new OdfNode(OdfNodeType.Element, "deletion", OdfNamespaces.Text, "text"));
                trackedChanges.AppendChild(cr2);

                doc.BodyTextRoot.AppendChild(trackedChanges);

                var p = doc.AddParagraph("Hello ");

                var cs1 = new OdfNode(OdfNodeType.Element, "change-start", OdfNamespaces.Text, "text");
                cs1.SetAttribute("change-id", OdfNamespaces.Text, "ch1", "text");
                p.Node.AppendChild(cs1);

                p.Node.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = "inserted text" });

                var ce1 = new OdfNode(OdfNodeType.Element, "change-end", OdfNamespaces.Text, "text");
                ce1.SetAttribute("change-id", OdfNamespaces.Text, "ch1", "text");
                p.Node.AppendChild(ce1);

                p.Node.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = " and " });

                var cs2 = new OdfNode(OdfNodeType.Element, "change-start", OdfNamespaces.Text, "text");
                cs2.SetAttribute("change-id", OdfNamespaces.Text, "ch2", "text");
                p.Node.AppendChild(cs2);

                p.Node.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = "deleted text" });

                var ce2 = new OdfNode(OdfNodeType.Element, "change-end", OdfNamespaces.Text, "text");
                ce2.SetAttribute("change-id", OdfNamespaces.Text, "ch2", "text");
                p.Node.AppendChild(ce2);

                p.Node.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = "." });

                Assert.Contains("inserted text", p.TextContent);
                Assert.Contains("deleted text", p.TextContent);

                doc.AcceptAllTrackedChanges();

                // Deletions should be purged, insertions kept, and tracked changes structure removed.
                Assert.Contains("inserted text", p.TextContent);
                Assert.DoesNotContain("deleted text", p.TextContent);
                Assert.Equal("Hello inserted text and .", p.TextContent);
                Assert.Null(FindNodeByLocalName(doc.BodyTextRoot, "tracked-changes"));

                doc.Save();
            }
        }

        [Fact]
        public void TestTextDocumentHtmlFragmentParsing()
        {
            using var ms = new MemoryStream();
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                var doc = new TextDocument(package);
                var p = doc.AddParagraph();

                string html = "This is <b>bold</b> and <i>italic</i> and <u>underline</u> and <a href=\"http://example.com\">a link</a>.";
                p.AddHtmlFragment(html);

                // Verify runs
                var spans = new List<OdfNode>();
                FindNodesByLocalName(p.Node, "span", spans);
                var links = new List<OdfNode>();
                FindNodesByLocalName(p.Node, "a", links);

                Assert.Equal(3, spans.Count);
                Assert.Single(links);

                // Verify spans styles
                var boldSpan = spans[0];
                Assert.Equal("bold", boldSpan.TextContent);
                var boldStyleName = boldSpan.GetAttribute("style-name", OdfNamespaces.Text);
                Assert.Equal("bold", doc.StyleEngine.GetStyleProperty(boldStyleName!, "font-weight", OdfNamespaces.Fo, "text"));

                var italicSpan = spans[1];
                Assert.Equal("italic", italicSpan.TextContent);
                var italicStyleName = italicSpan.GetAttribute("style-name", OdfNamespaces.Text);
                Assert.Equal("italic", doc.StyleEngine.GetStyleProperty(italicStyleName!, "font-style", OdfNamespaces.Fo, "text"));

                var underlineSpan = spans[2];
                Assert.Equal("underline", underlineSpan.TextContent);
                var underlineStyleName = underlineSpan.GetAttribute("style-name", OdfNamespaces.Text);
                Assert.Equal("solid", doc.StyleEngine.GetStyleProperty(underlineStyleName!, "text-underline-style", OdfNamespaces.Style, "text"));

                var linkNode = links[0];
                Assert.Equal("a link", linkNode.TextContent);
                Assert.Equal("http://example.com", linkNode.GetAttribute("href", OdfNamespaces.XLink));
            }
        }

        [Fact]
        public void TestTextDocumentHtmlFragmentParsingRobustness()
        {
            using var ms = new MemoryStream();
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                var doc = new TextDocument(package);
                var p = doc.AddParagraph();

                string html = "This is <b >bold</b > and <span style=\"font-weight: bold; font-style: italic;\">styled span</span> and <i   >italic</i   > and <em class=\"highlighted\">emphasized</em><br />new line.";
                p.AddHtmlFragment(html);

                // Verify spans
                var spans = new List<OdfNode>();
                FindNodesByLocalName(p.Node, "span", spans);

                // We expect 4 spans:
                // 1. "bold" (bold)
                // 2. "styled span" (bold + italic)
                // 3. "italic" (italic)
                // 4. "emphasized" (italic)
                Assert.Equal(4, spans.Count);

                var span1 = spans[0];
                Assert.Equal("bold", span1.TextContent);
                var style1 = span1.GetAttribute("style-name", OdfNamespaces.Text);
                Assert.Equal("bold", doc.StyleEngine.GetStyleProperty(style1!, "font-weight", OdfNamespaces.Fo, "text"));

                var span2 = spans[1];
                Assert.Equal("styled span", span2.TextContent);
                var style2 = span2.GetAttribute("style-name", OdfNamespaces.Text);
                Assert.Equal("bold", doc.StyleEngine.GetStyleProperty(style2!, "font-weight", OdfNamespaces.Fo, "text"));
                Assert.Equal("italic", doc.StyleEngine.GetStyleProperty(style2!, "font-style", OdfNamespaces.Fo, "text"));

                var span3 = spans[2];
                Assert.Equal("italic", span3.TextContent);
                var style3 = span3.GetAttribute("style-name", OdfNamespaces.Text);
                Assert.Equal("italic", doc.StyleEngine.GetStyleProperty(style3!, "font-style", OdfNamespaces.Fo, "text"));

                var span4 = spans[3];
                Assert.Equal("emphasized", span4.TextContent);
                var style4 = span4.GetAttribute("style-name", OdfNamespaces.Text);
                Assert.Equal("italic", doc.StyleEngine.GetStyleProperty(style4!, "font-style", OdfNamespaces.Fo, "text"));

                // Verify line-break
                var lineBreaks = new List<OdfNode>();
                FindNodesByLocalName(p.Node, "line-break", lineBreaks);
                Assert.Single(lineBreaks);
            }
        }

        [Fact]
        public void TestTextDocumentMerging()
        {
            using var ms1 = new MemoryStream();
            using var ms2 = new MemoryStream();

            using (var pkg2 = OdfPackage.Create(ms2, leaveOpen: true))
            {
                var doc2 = new TextDocument(pkg2);
                doc2.AddParagraph("Hello from doc2");
                doc2.Save();
            }

            ms2.Position = 0;

            using (var pkg1 = OdfPackage.Create(ms1, leaveOpen: true))
            {
                var doc1 = new TextDocument(pkg1);
                doc1.AddParagraph("Hello from doc1");

                using (var pkg2 = OdfPackage.Open(ms2))
                {
                    var doc2 = new TextDocument(pkg2);
                    doc1.AppendDocument(doc2);
                }

                doc1.Save();
            }

            ms1.Position = 0;
            using (var pkg1 = OdfPackage.Open(ms1))
            {
                var doc1 = new TextDocument(pkg1);
                Assert.Equal(2, doc1.BodyTextRoot.Children.Count);
                Assert.Equal("Hello from doc1", doc1.BodyTextRoot.Children[0].TextContent);
                Assert.Equal("Hello from doc2", doc1.BodyTextRoot.Children[1].TextContent);
            }
        }

        #endregion

        #region 3. SpreadsheetDocument Tests

        [Fact]
        public void TestSpreadsheetWorkbookAndSheetProtection()
        {
            using var ms = new MemoryStream();
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                var doc = new SpreadsheetDocument(package);
                doc.ProtectWorkbook("workbook_password");

                var sheet = doc.AddSheet("ProtectedSheet");
                sheet.Protect("sheet_password");

                doc.Save();
            }

            ms.Position = 0;
            using (var package = OdfPackage.Open(ms))
            {
                var doc = new SpreadsheetDocument(package);
                Assert.True(doc.WorkbookStructureProtected);

                var settingsNode = doc.SettingsDom.Children[0];
                var mapNode = FindMapNode(settingsNode, "WorkbookSettings");
                Assert.NotNull(mapNode);
                var entry = mapNode.Children[0];

                var structItem = FindConfigItemNode(entry, "StructureProtected");
                Assert.Equal("true", structItem?.TextContent);

                var keyItem = FindConfigItemNode(entry, "WorkbookProtectionKey");
                Assert.NotNull(keyItem?.TextContent);

                var sheet = doc.FindSheet("ProtectedSheet");
                Assert.NotNull(sheet);
                Assert.True(sheet.IsProtected);
                Assert.Equal("true", sheet.TableNode.GetAttribute("protected", OdfNamespaces.Table));
                Assert.NotNull(sheet.TableNode.GetAttribute("protection-key", OdfNamespaces.Table));
            }
        }

        [Fact]
        public void TestSpreadsheetCellMergeWithOuterBorders()
        {
            using var ms = new MemoryStream();
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                var doc = new SpreadsheetDocument(package);
                var sheet = doc.AddSheet("Sheet1");

                var border = OdfBorder.Parse("1pt solid #000000");
                var range = OdfCellRange.ParseExcel("A1:B2");

                sheet.MergeCells(range, border);
                doc.Save();
            }

            ms.Position = 0;
            using (var package = OdfPackage.Open(ms))
            {
                var doc = new SpreadsheetDocument(package);
                var sheet = doc.FindSheet("Sheet1");
                Assert.NotNull(sheet);

                var a1 = sheet.GetCell(0, 0);
                Assert.Equal("2", a1.Node.GetAttribute("number-columns-spanned", OdfNamespaces.Table));
                Assert.Equal("2", a1.Node.GetAttribute("number-rows-spanned", OdfNamespaces.Table));

                // B2 is covered-table-cell
                var b2 = sheet.GetCell(1, 1);
                Assert.Equal("covered-table-cell", b2.Node.LocalName);

                // Top-left is A1, Bottom-right is B2
                // B2 (row=1, col=1) is bottom and right boundary, so top=None, bottom=1pt, left=None, right=1pt
                var b2StyleName = b2.Node.GetAttribute("style-name", OdfNamespaces.Table);
                var b2BorderBottom = doc.StyleEngine.GetStyleProperty(b2StyleName!, "border-bottom", OdfNamespaces.Fo, "table-cell");
                var b2BorderRight = doc.StyleEngine.GetStyleProperty(b2StyleName!, "border-right", OdfNamespaces.Fo, "table-cell");

                Assert.Equal("1pt solid #000000", b2BorderBottom);
                Assert.Equal("1pt solid #000000", b2BorderRight);
            }
        }

        [Fact]
        public void TestSpreadsheetFormattingAndConditionalFormat()
        {
            using var ms = new MemoryStream();
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                var doc = new SpreadsheetDocument(package);
                var sheet = doc.AddSheet("Sheet1");

                var cell = sheet.GetCell("A1");
                cell.SetValue("Hello\nWorld\tWide");
                // DisplayText should preserve formatting and trigger wrap option
                Assert.Contains("\n", cell.DisplayText);

                var range = OdfCellRange.ParseExcel("A1:A5");
                sheet.AddConditionalFormat(range, "cell-content() > 5", "MyStyle");

                doc.Save();
            }

            ms.Position = 0;
            using (var package = OdfPackage.Open(ms))
            {
                var doc = new SpreadsheetDocument(package);
                var sheet = doc.FindSheet("Sheet1");
                Assert.NotNull(sheet);

                var formatsNode = FindNodeByLocalName(sheet.TableNode, "conditional-formats");
                Assert.NotNull(formatsNode);

                var format = formatsNode.Children[0];
                Assert.Equal("Sheet1.A1:Sheet1.A5", format.GetAttribute("target-range-address", format.NamespaceUri));

                var condition = format.Children[0];
                Assert.Equal("cell-content() > 5", condition.GetAttribute("value", condition.NamespaceUri));
                Assert.Equal("MyStyle", condition.GetAttribute("style-name", condition.NamespaceUri));
            }
        }

        #endregion

        #region 4. OdsStreamWriter Tests

        [Fact]
        public void TestOdsStreamWriterLowMemoryStreaming()
        {
            using var ms = new MemoryStream();
            var testDate = new DateTime(2026, 6, 9, 8, 30, 0, DateTimeKind.Utc);

            using (var writer = new OdsStreamWriter(ms))
            {
                writer.WriteStartSheet("StreamingSheet");

                writer.WriteColumn(OdfLength.Parse("3cm"));
                writer.WriteColumn(OdfLength.Parse("4cm"));

                writer.WriteStartRow();
                writer.WriteCell("StringValue");
                writer.WriteCell(123.45);
                writer.WriteEndRow();

                writer.WriteStartRow();
                writer.WriteCell(testDate);
                writer.WriteCell(true);
                writer.WriteEndRow();

                writer.WriteEndSheet();
            }

            ms.Position = 0;
            using (var package = OdfPackage.Open(ms))
            {
                var doc = new SpreadsheetDocument(package);
                var sheet = doc.FindSheet("StreamingSheet");
                Assert.NotNull(sheet);

                // Row 0 Col 0
                var a1 = sheet.GetCell(0, 0);
                Assert.Equal("string", a1.ValueType);
                Assert.Equal("StringValue", a1.DisplayText);

                // Row 0 Col 1
                var b1 = sheet.GetCell(0, 1);
                Assert.Equal("float", b1.ValueType);
                Assert.Equal("123.45", b1.RawValue);
                Assert.Equal("123.45", b1.DisplayText);

                // Row 1 Col 0
                var a2 = sheet.GetCell(1, 0);
                Assert.Equal("date", a2.ValueType);
                Assert.Equal("2026-06-09T08:30:00Z", a2.Node.GetAttribute("date-value", OdfNamespaces.Office));

                // Row 1 Col 1
                var b2 = sheet.GetCell(1, 1);
                Assert.Equal("boolean", b2.ValueType);
                Assert.Equal("true", b2.Node.GetAttribute("boolean-value", OdfNamespaces.Office));
                Assert.Equal("TRUE", b2.DisplayText);
            }
        }

        #endregion

        #region 5. PresentationDocument Tests

        [Fact]
        public void TestPresentationManagementAndDrawingElements()
        {
            using var ms = new MemoryStream();
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                var doc = new PresentationDocument(package);
                doc.SetSlideSize(OdfLength.Parse("28cm"), OdfLength.Parse("21cm"));
                doc.SetSlideOrientation(OdfPageOrientation.Landscape);

                var s1 = doc.AddSlide("First Slide");
                s1.SpeakerNotes = "These are speaker notes for slide 1.";

                s1.AddTextBox(OdfLength.Parse("2cm"), OdfLength.Parse("2cm"), OdfLength.Parse("10cm"), OdfLength.Parse("4cm"), "Hello Presentation");

                var shape = s1.AddShape(OdfShapeType.Rectangle, OdfLength.Parse("3cm"), OdfLength.Parse("8cm"), OdfLength.Parse("5cm"), OdfLength.Parse("5cm"));
                shape.Animate(OdfAnimationType.FadeIn, OdfLength.Parse("1.5in"), OdfLength.Parse("0.5in"));

                var s2 = doc.AddSlide("Second Slide");
                s2.AddPicture(new byte[] { 1, 2, 3, 4 }, OdfLength.Parse("1cm"), OdfLength.Parse("1cm"), OdfLength.Parse("5cm"), OdfLength.Parse("5cm"));
                s2.SetTransition(OdfTransitionType.Fade, OdfLength.Parse("2in"));

                // Manage slides
                var s3 = doc.AddSlide("Third Slide");

                // Clone slide 0
                var cloned = doc.CloneSlide(0);
                Assert.Equal("First Slide_Clone", cloned.Name);

                // Move slide
                doc.MoveSlide(0, 2); // First Slide moved to index 2

                // Delete slide
                doc.DeleteSlide(3); // Delete the 4th slide

                doc.Save();
            }

            ms.Position = 0;
            using (var package = OdfPackage.Open(ms))
            {
                var doc = new PresentationDocument(package);
                Assert.Equal(3, doc.Slides.Count);

                // Check speaker notes
                var notesSlide = doc.Slides[0]; // Slide order changed due to Move/Clone/Delete
                Assert.Contains("speaker notes", notesSlide.SpeakerNotes);

                // Check transition
                var s2 = doc.Slides[1];
                string? styleName = s2.Node.GetAttribute("style-name", OdfNamespaces.Draw);
                Assert.NotNull(styleName);
                Assert.Equal("fade", doc.StyleEngine.GetStyleProperty(styleName!, "type", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "drawing-page"));

                // Check shape and animation
                var s1 = doc.Slides[2];
                var rectNode = FindNodeByLocalName(s1.Node, "rect");
                Assert.NotNull(rectNode);

                var seqNode = FindNodeByLocalName(s1.Node, "seq");
                Assert.NotNull(seqNode);
                Assert.Equal("main-sequence", seqNode.GetAttribute("node-type", OdfNamespaces.Presentation));
            }
        }

        #endregion

        #region 6. LibreOfficeRenderer Tests

        [Fact]
        public void TestLibreOfficeRendererConversionFlow()
        {
            string mockSoffice = GetMockSofficePath();
            if (string.IsNullOrEmpty(mockSoffice))
            {
                // Skip if MockSoffice not found
                return;
            }

            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);
            doc.AddParagraph("Hello Mock Rendering");

            var renderer = new LibreOfficeRenderer
            {
                LibreOfficePath = mockSoffice,
                Timeout = TimeSpan.FromSeconds(5)
            };

            string outPath = Path.Combine(Path.GetTempPath(), "OdfKit_Test_Out_" + Guid.NewGuid().ToString("N") + ".pdf");
            try
            {
                renderer.Convert(doc, outPath, "pdf");

                Assert.True(File.Exists(outPath));
                string content = File.ReadAllText(outPath);
                Assert.Contains("%PDF-1.4", content);
                Assert.Contains("%mock pdf", content);
            }
            finally
            {
                if (File.Exists(outPath))
                    File.Delete(outPath);
            }
        }

        [Fact]
        public void TestLibreOfficeRendererTimeoutSafety()
        {
            string mockSoffice = GetMockSofficePath();
            if (string.IsNullOrEmpty(mockSoffice))
            {
                return;
            }

            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);

            var renderer = new LibreOfficeRenderer
            {
                LibreOfficePath = mockSoffice,
                Timeout = TimeSpan.FromMilliseconds(50) // Very short timeout to trigger exception
            };

            string outPath = Path.Combine(Path.GetTempPath(), "OdfKit_Test_Out_" + Guid.NewGuid().ToString("N") + ".pdf");

            // Format containing "simulate-timeout" will cause MockSoffice to sleep 5s
            var ex = Assert.Throws<TimeoutException>(() => renderer.Convert(doc, outPath, "pdf-simulate-timeout"));
            Assert.Contains("timed out", ex.Message);
        }

        [Fact]
        public void TestLibreOfficeRendererErrorValidation()
        {
            string mockSoffice = GetMockSofficePath();
            if (string.IsNullOrEmpty(mockSoffice))
            {
                return;
            }

            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);

            var renderer = new LibreOfficeRenderer
            {
                LibreOfficePath = mockSoffice,
                Timeout = TimeSpan.FromSeconds(5)
            };

            string outPath = Path.Combine(Path.GetTempPath(), "OdfKit_Test_Out_" + Guid.NewGuid().ToString("N") + ".pdf");

            // Format containing "simulate-error" will cause MockSoffice to exit with code 1
            var ex = Assert.Throws<InvalidOperationException>(() => renderer.Convert(doc, outPath, "pdf-simulate-error"));
            Assert.Contains("exited with code 1", ex.Message);
        }

        [Fact]
        public void TestAddMasterPage()
        {
            using var ms = new MemoryStream();
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                var doc = new PresentationDocument(package);
                doc.AddMasterPage("CustomMaster", "PM1");
                doc.Save();
            }

            ms.Position = 0;
            using (var package = OdfPackage.Open(ms, leaveOpen: true))
            {
                var doc = new PresentationDocument(package);
                var masterStyles = doc.FindChildElement(doc.StylesRoot, "master-styles", OdfNamespaces.Office);
                Assert.NotNull(masterStyles);

                OdfNode? customMaster = null;
                foreach (var child in masterStyles.Children)
                {
                    if (child.LocalName == "master-page" && child.NamespaceUri == OdfNamespaces.Style &&
                        child.GetAttribute("name", OdfNamespaces.Style) == "CustomMaster")
                    {
                        customMaster = child;
                        break;
                    }
                }
                Assert.NotNull(customMaster);
                Assert.Equal("PM1", customMaster.GetAttribute("page-layout-name", OdfNamespaces.Style));
            }
        }

        [Fact]
        public void TestSlideLayoutAndPlaceholderAPIs()
        {
            using var ms = new MemoryStream();
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                var doc = new PresentationDocument(package);
                var layout = doc.CreatePresentationPageLayout("CustomLayout");
                Assert.NotNull(layout);
                Assert.Equal("CustomLayout", layout.Name);

                var phTemplate = layout.AddPlaceholder(OdfPlaceholderType.Title, OdfLength.Parse("1cm"), OdfLength.Parse("2cm"), OdfLength.Parse("10cm"), OdfLength.Parse("3cm"));
                Assert.Equal(OdfPlaceholderType.Title, phTemplate.PlaceholderType);
                Assert.Equal("1cm", phTemplate.X?.ToString());
                Assert.Equal("2cm", phTemplate.Y?.ToString());

                var slide = doc.AddSlide("Slide 1");
                slide.PresentationPageLayoutName = "CustomLayout";
                Assert.Equal("CustomLayout", slide.PresentationPageLayoutName);

                var instancedPh = slide.AddPlaceholder(OdfPlaceholderType.Title, OdfLength.Parse("1.5cm"), OdfLength.Parse("2.5cm"), OdfLength.Parse("9cm"), OdfLength.Parse("2.5cm"));
                Assert.Equal(OdfPlaceholderType.Title, instancedPh.PlaceholderType);

                var placeholders = slide.Placeholders;
                Assert.Single(placeholders);
                Assert.Equal(OdfPlaceholderType.Title, placeholders[0].PlaceholderType);

                doc.Save();
            }

            ms.Position = 0;
            using (var package = OdfPackage.Open(ms))
            {
                var doc = new PresentationDocument(package);
                var layout = doc.FindPresentationPageLayout("CustomLayout");
                Assert.NotNull(layout);
                Assert.Single(layout.Placeholders);
                Assert.Equal(OdfPlaceholderType.Title, layout.Placeholders[0].PlaceholderType);

                var slide = doc.Slides[0];
                Assert.Equal("CustomLayout", slide.PresentationPageLayoutName);
                Assert.Single(slide.Placeholders);
                Assert.Equal(OdfPlaceholderType.Title, slide.Placeholders[0].PlaceholderType);
            }
        }

        [Fact]
        public void TestSpeakerNotesAndHandoutsCanvas()
        {
            using var ms = new MemoryStream();
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                var doc = new PresentationDocument(package);
                var slide = doc.AddSlide("Slide 1");

                // Note page
                var notes = slide.SpeakerNotesPage;
                Assert.NotNull(notes);
                notes.SpeakerNotesText = "Hello speaker notes";
                notes.AddSlideThumbnail(OdfLength.Parse("1cm"), OdfLength.Parse("1cm"), OdfLength.Parse("5cm"), OdfLength.Parse("4cm"));
                notes.AddShape(OdfShapeType.Rectangle, OdfLength.Parse("2cm"), OdfLength.Parse("6cm"), OdfLength.Parse("3cm"), OdfLength.Parse("2cm"));

                // Handout page
                var handout = doc.HandoutPage;
                Assert.NotNull(handout);
                handout.AddTextBox(OdfLength.Parse("1cm"), OdfLength.Parse("1cm"), OdfLength.Parse("10cm"), OdfLength.Parse("2cm"), "Handout Header");
                handout.AddSlideThumbnailPlaceholder(OdfLength.Parse("2cm"), OdfLength.Parse("4cm"), OdfLength.Parse("8cm"), OdfLength.Parse("6cm"));

                doc.Save();
            }

            ms.Position = 0;
            using (var package = OdfPackage.Open(ms))
            {
                var doc = new PresentationDocument(package);
                var slide = doc.Slides[0];
                var notes = slide.SpeakerNotesPage;
                Assert.Equal("Hello speaker notes", notes.SpeakerNotesText);

                // Expect thumbnail and shape
                Assert.Equal(2, notes.Shapes.Count); // Note frame + shape

                var handout = doc.HandoutPage;
                Assert.Single(handout.Shapes); // Text box frame
                Assert.Equal("Handout Header", handout.Shapes[0].Node.TextContent.Trim());
            }
        }

        [Fact]
        public void TestSmilTimingAnimations()
        {
            using var ms = new MemoryStream();
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                var doc = new PresentationDocument(package);
                var slide = doc.AddSlide("Slide 1");
                var shape = slide.AddShape(OdfShapeType.Rectangle, OdfLength.Parse("1cm"), OdfLength.Parse("1cm"), OdfLength.Parse("2cm"), OdfLength.Parse("2cm"));
                shape.Id = "shape1";

                var rootSeq = slide.AnimationRoot;
                Assert.NotNull(rootSeq);

                var seqNode = rootSeq.AddSequence("click");
                var parNode = seqNode.AddParallel("0s");
                parNode.AddEffect(OdfAnimationType.FadeIn, "shape1", OdfLength.Parse("1in"), OdfLength.Parse("0in"));

                doc.Save();
            }

            ms.Position = 0;
            using (var package = OdfPackage.Open(ms))
            {
                var doc = new PresentationDocument(package);
                var slide = doc.Slides[0];
                var rootSeq = slide.AnimationRoot;

                Assert.Single(rootSeq.Children);
                var seq = rootSeq.Children[0];
                Assert.Equal(OdfAnimationNodeType.Sequence, seq.Type);
                Assert.Equal("click", seq.Begin);

                Assert.Single(seq.Children);
                var par = seq.Children[0];
                Assert.Equal(OdfAnimationNodeType.Parallel, par.Type);
                Assert.Equal("0s", par.Begin);

                Assert.Single(par.Children);
                var effect = par.Children[0];
                Assert.Equal(OdfAnimationNodeType.Effect, effect.Type);
                Assert.Equal("shape1", effect.TargetElement);
            }
        }

        [Fact]
        public void TestEmbeddedChartAndFormulaDocuments()
        {
            using var ms = new MemoryStream();
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                var doc = new PresentationDocument(package);

                var chartDoc = doc.CreateEmbeddedDocument<OdfKit.Chart.OdfChartDocument>("Object 1");
                Assert.NotNull(chartDoc);

                var formulaDoc = doc.CreateEmbeddedDocument<OdfKit.Formula.OdfFormulaDocument>("Object 2");
                Assert.NotNull(formulaDoc);
                formulaDoc.SetIdentifierEquation("x", "y");

                var slide = doc.AddSlide("Slide 1");
                slide.AddEmbeddedObject("Object 1", OdfLength.Parse("1cm"), OdfLength.Parse("1cm"), OdfLength.Parse("8cm"), OdfLength.Parse("6cm"));
                slide.AddEmbeddedObject("Object 2", OdfLength.Parse("10cm"), OdfLength.Parse("1cm"), OdfLength.Parse("8cm"), OdfLength.Parse("6cm"));

                doc.Save();
            }

            ms.Position = 0;
            using (var package = OdfPackage.Open(ms))
            {
                var doc = new PresentationDocument(package);

                var chartDoc = doc.GetEmbeddedDocument<OdfKit.Chart.OdfChartDocument>("Object 1");
                Assert.NotNull(chartDoc);
                Assert.Equal("application/vnd.oasis.opendocument.chart", chartDoc.Package.Manifest["Object 1/"]);

                var formulaDoc = doc.GetEmbeddedDocument<OdfKit.Formula.OdfFormulaDocument>("Object 2");
                Assert.NotNull(formulaDoc);
                Assert.Equal("application/vnd.oasis.opendocument.formula", formulaDoc.Package.Manifest["Object 2/"]);
                Assert.Equal("x=y", formulaDoc.MathText);

                // Verify the directory full-paths exist in manifest
                Assert.True(package.Manifest.ContainsKey("Object 1/"));
                Assert.True(package.Manifest.ContainsKey("Object 2/"));

                var slide = doc.Slides[0];
                var frames = new List<OdfNode>();
                FindNodesByLocalName(slide.Node, "frame", frames);
                Assert.Equal(2, frames.Count);

                var obj1 = FindNodeByLocalName(frames[0], "object");
                Assert.NotNull(obj1);
                Assert.Equal("./Object 1", obj1.GetAttribute("href", OdfNamespaces.XLink));

                var obj2 = FindNodeByLocalName(frames[1], "object");
                Assert.NotNull(obj2);
                Assert.Equal("./Object 2", obj2.GetAttribute("href", OdfNamespaces.XLink));
            }
        }

        #endregion

        #region Helpers

        private string GetMockSofficePath()
        {
            var baseDir = AppContext.BaseDirectory;
            var paths = new List<string>
            {
                Path.Combine(baseDir, "MockSoffice", "MockSoffice.exe"),
                Path.Combine(baseDir, "MockSoffice", "MockSoffice"),
                Path.Combine(baseDir, "..", "..", "..", "MockSoffice", "bin", "MockSoffice.exe"),
                Path.Combine(baseDir, "..", "..", "..", "MockSoffice", "bin", "MockSoffice"),
                Path.Combine(baseDir, "..", "..", "..", "..", "OdfKit.Tests", "MockSoffice", "bin", "MockSoffice.exe"),
                Path.Combine(baseDir, "..", "..", "..", "..", "OdfKit.Tests", "MockSoffice", "bin", "MockSoffice"),
                Path.Combine(baseDir, "..", "..", "..", "..", "OdfKit.Tests", "MockSoffice", "bin", "Debug", "net8.0", "MockSoffice.exe"),
                Path.Combine(baseDir, "..", "..", "..", "..", "OdfKit.Tests", "MockSoffice", "bin", "Debug", "net8.0", "MockSoffice")
            };

            foreach (var path in paths)
            {
                if (File.Exists(path))
                {
                    return Path.GetFullPath(path);
                }
            }

            return string.Empty;
        }

        private OdfNode? FindNodeByLocalName(OdfNode parent, string name)
        {
            if (parent.LocalName == name)
                return parent;
            foreach (var child in parent.Children)
            {
                var found = FindNodeByLocalName(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        private void FindNodesByLocalName(OdfNode parent, string name, List<OdfNode> result)
        {
            if (parent.LocalName == name)
                result.Add(parent);
            foreach (var child in parent.Children)
            {
                FindNodesByLocalName(child, name, result);
            }
        }

        private OdfNode? FindMapNode(OdfNode parent, string name)
        {
            foreach (var child in parent.Children)
            {
                if (child.LocalName == "config-item-map-named" && child.GetAttribute("name", child.NamespaceUri) == name)
                    return child;
                var found = FindMapNode(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        private OdfNode? FindConfigItemNode(OdfNode parent, string name)
        {
            foreach (var child in parent.Children)
            {
                if (child.LocalName == "config-item" && child.GetAttribute("name", child.NamespaceUri) == name)
                    return child;
                var found = FindConfigItemNode(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        #endregion
    }
}
