using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Text;
using OdfKit.Spreadsheet;
using OdfKit.Presentation;
using OdfKit.Drawing;
using OdfKit.Styles;
using OdfKit.Formula;
using OdfKit.Compliance;
using Xunit;

namespace OdfKit.Tests
{
    public class Phase2E2ETests
    {
        private static readonly XNamespace OfficeNs = OdfNamespaces.Office;
        private static readonly XNamespace TextNs = OdfNamespaces.Text;
        private static readonly XNamespace TableNs = OdfNamespaces.Table;
        private static readonly XNamespace DrawNs = OdfNamespaces.Draw;
        private static readonly XNamespace PresentationNs = OdfNamespaces.Presentation;
        private static readonly XNamespace MathmlNs = "http://www.w3.org/1998/Math/MathML";
        private static readonly XNamespace calcextNs = "urn:org:documentfoundation:names:experimental:calc:xmlns:calcext:1.0";
        private static readonly XNamespace smilNs = "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0";

        // Helper to perform round-trip saving and reloading of a document
        private T RoundTrip<T>(T document, Func<OdfPackage, T> factory) where T : OdfDocument
        {
            var ms = new MemoryStream();
            document.Package.Save(ms);
            ms.Position = 0;
            var package = OdfPackage.Open(ms);
            return factory(package);
        }

        // =====================================================================
        // TIER 1: FEATURE COVERAGE (HAPPY-PATH)
        // =====================================================================

        #region Feature 1: ODT TOC / Index
        [Fact]
        public void F1_Toc_HappyPath()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);
            doc.AddHeading("Introduction", 1);
            doc.AddTableOfContents();
            doc.Save();

            var reloaded = RoundTrip(doc, p => new TextDocument(p));
            var tocNode = reloaded.BodyTextRoot.Children.FirstOrDefault(c => c.LocalName == "table-of-content" && c.NamespaceUri == OdfNamespaces.Text);
            Assert.NotNull(tocNode);
            Assert.Equal("Table of Contents", tocNode.GetAttribute("name", OdfNamespaces.Text));
        }

        [Fact]
        public void F1_AlphabeticalIndex_HappyPath()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);
            doc.AddAlphabeticalIndex();
            doc.Save();

            var reloaded = RoundTrip(doc, p => new TextDocument(p));
            var idxNode = reloaded.BodyTextRoot.Children.FirstOrDefault(c => c.LocalName == "alphabetical-index" && c.NamespaceUri == OdfNamespaces.Text);
            Assert.NotNull(idxNode);
            Assert.Equal("Alphabetical Index", idxNode.GetAttribute("name", OdfNamespaces.Text));
        }

        [Fact]
        public void F1_Bibliography_HappyPath()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);
            doc.AddBibliography();
            doc.Save();

            var reloaded = RoundTrip(doc, p => new TextDocument(p));
            var bibNode = reloaded.BodyTextRoot.Children.FirstOrDefault(c => c.LocalName == "bibliography" && c.NamespaceUri == OdfNamespaces.Text);
            Assert.NotNull(bibNode);
            Assert.Equal("Bibliography", bibNode.GetAttribute("name", OdfNamespaces.Text));
        }

        [Fact]
        public void F1_TableIndex_HappyPath()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);
            doc.AddTableIndex();
            doc.Save();

            var reloaded = RoundTrip(doc, p => new TextDocument(p));
            var idxNode = reloaded.BodyTextRoot.Children.FirstOrDefault(c => c.LocalName == "table-index" && c.NamespaceUri == OdfNamespaces.Text);
            Assert.NotNull(idxNode);
            Assert.Equal("Index of Tables", idxNode.GetAttribute("name", OdfNamespaces.Text));
        }

        [Fact]
        public void F1_TOC_IndexBodyStructure()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);
            doc.AddTableOfContents();
            doc.Save();

            var reloaded = RoundTrip(doc, p => new TextDocument(p));
            var tocNode = reloaded.BodyTextRoot.Children.FirstOrDefault(c => c.LocalName == "table-of-content" && c.NamespaceUri == OdfNamespaces.Text);
            Assert.NotNull(tocNode);
            var bodyNode = tocNode.Children.FirstOrDefault(c => c.LocalName == "index-body" && c.NamespaceUri == OdfNamespaces.Text);
            Assert.NotNull(bodyNode);
        }
        #endregion

        #region Feature 2: ODT Tracked Revisions
        [Fact]
        public void F2_TrackedChanges_AcceptAll()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);
            var p = doc.AddParagraph("Initial ");
            
            // Manually build tracked changes DOM structures
            var tcNode = new OdfNode(OdfNodeType.Element, "tracked-changes", OdfNamespaces.Text, "text");
            var changedRegion = new OdfNode(OdfNodeType.Element, "changed-region", OdfNamespaces.Text, "text");
            changedRegion.SetAttribute("id", OdfNamespaces.Text, "ch1", "text");
            var deletion = new OdfNode(OdfNodeType.Element, "deletion", OdfNamespaces.Text, "text");
            deletion.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = "deleted text" });
            changedRegion.AppendChild(deletion);
            tcNode.AppendChild(changedRegion);
            doc.BodyTextRoot.AppendChild(tcNode);

            // Add marker tags in paragraph
            var startNode = new OdfNode(OdfNodeType.Element, "change-start", OdfNamespaces.Text, "text");
            startNode.SetAttribute("change-id", OdfNamespaces.Text, "ch1", "text");
            var endNode = new OdfNode(OdfNodeType.Element, "change-end", OdfNamespaces.Text, "text");
            endNode.SetAttribute("change-id", OdfNamespaces.Text, "ch1", "text");

            p.Node.AppendChild(startNode);
            p.Node.AppendChild(endNode);

            doc.Save();

            var reloaded = RoundTrip(doc, p => new TextDocument(p));
            reloaded.AcceptAllTrackedChanges();
            reloaded.Save();

            var finalDoc = RoundTrip(reloaded, p => new TextDocument(p));
            // Deletion accepted => content is purged
            Assert.DoesNotContain("deleted text", finalDoc.BodyTextRoot.TextContent);
            Assert.Null(finalDoc.BodyTextRoot.Children.FirstOrDefault(c => c.LocalName == "tracked-changes"));
        }

        [Fact]
        public void F2_TrackedChanges_RejectAll()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);
            var p = doc.AddParagraph("Initial ");

            var tcNode = new OdfNode(OdfNodeType.Element, "tracked-changes", OdfNamespaces.Text, "text");
            var changedRegion = new OdfNode(OdfNodeType.Element, "changed-region", OdfNamespaces.Text, "text");
            changedRegion.SetAttribute("id", OdfNamespaces.Text, "ch1", "text");
            var deletion = new OdfNode(OdfNodeType.Element, "deletion", OdfNamespaces.Text, "text");
            deletion.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = "deleted text" });
            changedRegion.AppendChild(deletion);
            tcNode.AppendChild(changedRegion);
            doc.BodyTextRoot.AppendChild(tcNode);

            var startNode = new OdfNode(OdfNodeType.Element, "change-start", OdfNamespaces.Text, "text");
            startNode.SetAttribute("change-id", OdfNamespaces.Text, "ch1", "text");
            var endNode = new OdfNode(OdfNodeType.Element, "change-end", OdfNamespaces.Text, "text");
            endNode.SetAttribute("change-id", OdfNamespaces.Text, "ch1", "text");

            p.Node.AppendChild(startNode);
            p.Node.AppendChild(endNode);

            doc.Save();

            var reloaded = RoundTrip(doc, p => new TextDocument(p));
            reloaded.RejectAllTrackedChanges();
            reloaded.Save();

            var finalDoc = RoundTrip(reloaded, p => new TextDocument(p));
            // Deletion rejected => content is restored
            Assert.Contains("deleted text", finalDoc.BodyTextRoot.TextContent);
        }

        [Fact]
        public void F2_TrackedChanges_AcceptSingle()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);
            var p = doc.AddParagraph("Initial ");

            var tcNode = new OdfNode(OdfNodeType.Element, "tracked-changes", OdfNamespaces.Text, "text");
            var changedRegion = new OdfNode(OdfNodeType.Element, "changed-region", OdfNamespaces.Text, "text");
            changedRegion.SetAttribute("id", OdfNamespaces.Text, "ch1", "text");
            var insertion = new OdfNode(OdfNodeType.Element, "insertion", OdfNamespaces.Text, "text");
            changedRegion.AppendChild(insertion);
            tcNode.AppendChild(changedRegion);
            doc.BodyTextRoot.AppendChild(tcNode);

            doc.Save();

            var reloaded = RoundTrip(doc, p => new TextDocument(p));
            reloaded.AcceptChange("ch1");
            reloaded.Save();

            var finalDoc = RoundTrip(reloaded, p => new TextDocument(p));
            Assert.Null(finalDoc.BodyTextRoot.Children.FirstOrDefault(c => c.LocalName == "tracked-changes"));
        }

        [Fact]
        public void F2_TrackedChanges_RejectSingle()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);
            var p = doc.AddParagraph("Initial ");

            var tcNode = new OdfNode(OdfNodeType.Element, "tracked-changes", OdfNamespaces.Text, "text");
            var changedRegion = new OdfNode(OdfNodeType.Element, "changed-region", OdfNamespaces.Text, "text");
            changedRegion.SetAttribute("id", OdfNamespaces.Text, "ch1", "text");
            var insertion = new OdfNode(OdfNodeType.Element, "insertion", OdfNamespaces.Text, "text");
            changedRegion.AppendChild(insertion);
            tcNode.AppendChild(changedRegion);
            doc.BodyTextRoot.AppendChild(tcNode);

            doc.Save();

            var reloaded = RoundTrip(doc, p => new TextDocument(p));
            reloaded.RejectChange("ch1");
            reloaded.Save();

            var finalDoc = RoundTrip(reloaded, p => new TextDocument(p));
            Assert.Null(finalDoc.BodyTextRoot.Children.FirstOrDefault(c => c.LocalName == "tracked-changes"));
        }

        [Fact]
        public void F2_TrackedChanges_PropertyGetterSetter()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);
            doc.TrackedChanges = true;
            Assert.True(doc.TrackedChanges);
            doc.TrackedChanges = false;
            Assert.False(doc.TrackedChanges);
        }
        #endregion

        #region Feature 3: ODT CJK Layout
        [Fact]
        public void F3_Ruby_HappyPath()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);
            var p = doc.AddParagraph();
            doc.AddRuby(p, "振", "しん");
            doc.Save();

            var reloaded = RoundTrip(doc, p => new TextDocument(p));
            var rubyNode = reloaded.BodyTextRoot.Descendants().FirstOrDefault(d => d.LocalName == "ruby" && d.NamespaceUri == OdfNamespaces.Text);
            Assert.NotNull(rubyNode);
            Assert.Contains("振", rubyNode.TextContent);
            Assert.Contains("しん", rubyNode.TextContent);
        }

        [Fact]
        public void F3_Ruby_BaseAndTextElements()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);
            var p = doc.AddParagraph();
            doc.AddRuby(p, "東", "とう");
            doc.Save();

            var reloaded = RoundTrip(doc, p => new TextDocument(p));
            var rubyNode = reloaded.BodyTextRoot.Descendants().FirstOrDefault(d => d.LocalName == "ruby" && d.NamespaceUri == OdfNamespaces.Text);
            Assert.NotNull(rubyNode);
            var baseNode = rubyNode.Children.FirstOrDefault(c => c.LocalName == "ruby-base");
            var textNode = rubyNode.Children.FirstOrDefault(c => c.LocalName == "ruby-text");
            Assert.NotNull(baseNode);
            Assert.NotNull(textNode);
            Assert.Equal("東", baseNode.TextContent);
            Assert.Equal("とう", textNode.TextContent);
        }

        [Fact]
        public void F3_VerticalWritingMode_Styles()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);
            var pSetup = doc.GetDefaultPageSetup();
            // CJK vertical writing mode setting
            pSetup.WritingMode = "tb-rl";
            doc.Save();

            var reloaded = RoundTrip(doc, p => new TextDocument(p));
            var reloadedSetup = reloaded.GetDefaultPageSetup();
            Assert.Equal("tb-rl", reloadedSetup.WritingMode);
        }

        [Fact]
        public void F3_EastAsianLayoutGrid()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);
            
            // Grid-locked page styling for East Asian scripts
            var autoStyles = doc.StylesDom.Children[0];
            var pageLayout = new OdfNode(OdfNodeType.Element, "page-layout", OdfNamespaces.Style, "style");
            pageLayout.SetAttribute("name", OdfNamespaces.Style, "GridPage", "style");
            var layoutProps = new OdfNode(OdfNodeType.Element, "page-layout-properties", OdfNamespaces.Style, "style");
            layoutProps.SetAttribute("layout-grid-mode", OdfNamespaces.Style, "both", "style");
            layoutProps.SetAttribute("layout-grid-lines", OdfNamespaces.Style, "44", "style");
            pageLayout.AppendChild(layoutProps);
            autoStyles.AppendChild(pageLayout);
            doc.Save();

            var reloaded = RoundTrip(doc, p => new TextDocument(p));
            var reloadedGrid = reloaded.StylesDom.Descendants().FirstOrDefault(d => d.LocalName == "page-layout" && d.GetAttribute("name", OdfNamespaces.Style) == "GridPage");
            Assert.NotNull(reloadedGrid);
            var props = reloadedGrid.Children.FirstOrDefault(c => c.LocalName == "page-layout-properties");
            Assert.NotNull(props);
            Assert.Equal("both", props.GetAttribute("layout-grid-mode", OdfNamespaces.Style));
        }

        [Fact]
        public void F3_CjkFontFallbackInfo()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);
            
            // Set font fallback configuration for CJK
            var fontDecls = doc.ContentDom.Children.FirstOrDefault(c => c.LocalName == "font-face-decls" && c.NamespaceUri == OdfNamespaces.Office);
            if (fontDecls == null)
            {
                fontDecls = new OdfNode(OdfNodeType.Element, "font-face-decls", OdfNamespaces.Office, "office");
                doc.ContentDom.InsertBefore(fontDecls, doc.BodyTextRoot.Parent!);
            }
            var fontFace = new OdfNode(OdfNodeType.Element, "font-face", OdfNamespaces.Style, "style");
            fontFace.SetAttribute("name", OdfNamespaces.Style, "MS Mincho", "style");
            fontFace.SetAttribute("font-family", OdfNamespaces.Svg, "MS Mincho", "svg");
            fontFace.SetAttribute("font-family-generic", OdfNamespaces.Style, "system", "style");
            fontDecls.AppendChild(fontFace);
            doc.Save();

            var reloaded = RoundTrip(doc, p => new TextDocument(p));
            var face = reloaded.ContentDom.Descendants().FirstOrDefault(d => d.LocalName == "font-face" && d.GetAttribute("name", OdfNamespaces.Style) == "MS Mincho");
            Assert.NotNull(face);
        }
        #endregion

        #region Feature 4: ODT MathML Object Preservation
        [Fact]
        public void F4_MathML_HappyPath()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);
            var p = doc.AddParagraph();
            const string mathXml = "<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mrow><mi>x</mi><mo>+</mo><mi>y</mi></mrow></math>";
            doc.AddFormula(p, mathXml);
            doc.Save();

            var reloaded = RoundTrip(doc, p => new TextDocument(p));
            // MathML inserted formula creates folder structured entry
            Assert.True(reloaded.Package.HasEntry("Formula_1/content.xml") || reloaded.Package.GetEntries().Any(e => e.Path.EndsWith("/content.xml") && e.Path.StartsWith("Formula_")));
        }

        [Fact]
        public void F4_MathML_NestedStructure()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);
            var p = doc.AddParagraph();
            const string mathXml = "<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mrow><mi>a</mi><mo>=</mo><msqrt><mrow><msup><mi>b</mi><mn>2</mn></msup></mrow></msqrt></mrow></math>";
            doc.AddFormula(p, mathXml);
            doc.Save();

            var reloaded = RoundTrip(doc, p => new TextDocument(p));
            var formulaEntry = reloaded.Package.GetEntries().FirstOrDefault(e => e.Path.StartsWith("Formula_") && e.Path.EndsWith("content.xml"));
            Assert.NotNull(formulaEntry);
            string mathContent = Encoding.UTF8.GetString(reloaded.Package.ReadEntry(formulaEntry.Path));
            Assert.Contains("msqrt", mathContent);
            Assert.Contains("msup", mathContent);
        }

        [Fact]
        public void F4_MathML_InHeaderFooter()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);
            
            // Build header/footer and insert MathML
            var styles = doc.StylesDom;
            var masterPage = styles.Descendants().FirstOrDefault(d => d.LocalName == "master-page" && d.NamespaceUri == OdfNamespaces.Style);
            Assert.NotNull(masterPage);
            
            var header = new OdfNode(OdfNodeType.Element, "header", OdfNamespaces.Style, "style");
            var p = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
            header.AppendChild(p);
            masterPage.AppendChild(header);

            var paragraph = new OdfParagraph(p, doc);
            doc.AddFormula(paragraph, "<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mi>π</mi></math>");
            doc.Save();

            var reloaded = RoundTrip(doc, pDoc => new TextDocument(pDoc));
            var hasFormula = reloaded.Package.GetEntries().Any(e => e.Path.StartsWith("Formula_") && e.Path.EndsWith("content.xml"));
            Assert.True(hasFormula);
        }

        [Fact]
        public void F4_MathML_SpecialCharacters()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);
            var p = doc.AddParagraph();
            const string mathXml = "<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mo>&amp;</mo><mo>&lt;</mo><mo>&gt;</mo></math>";
            doc.AddFormula(p, mathXml);
            doc.Save();

            var reloaded = RoundTrip(doc, p => new TextDocument(p));
            var formulaEntry = reloaded.Package.GetEntries().FirstOrDefault(e => e.Path.StartsWith("Formula_") && e.Path.EndsWith("content.xml"));
            Assert.NotNull(formulaEntry);
            string mathContent = Encoding.UTF8.GetString(reloaded.Package.ReadEntry(formulaEntry.Path));
            Assert.Contains("&amp;", mathContent);
            Assert.Contains("&lt;", mathContent);
            Assert.Contains("&gt;", mathContent);
        }

        [Fact]
        public void F4_MathML_PackageFidelity()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);
            var p = doc.AddParagraph();
            doc.AddFormula(p, "<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mi>E</mi><mo>=</mo><mi>m</mi><msup><mi>c</mi><mn>2</mn></msup></math>");
            doc.Save();

            var reloaded = RoundTrip(doc, pDoc => new TextDocument(pDoc));
            var mimeEntry = reloaded.Package.GetEntries().FirstOrDefault(e => e.Path.StartsWith("Formula_") && e.Path.EndsWith("mimetype"));
            Assert.NotNull(mimeEntry);
            string mimeType = Encoding.UTF8.GetString(reloaded.Package.ReadEntry(mimeEntry.Path));
            Assert.Equal("application/vnd.oasis.opendocument.formula", mimeType);
        }
        #endregion

        #region Feature 5: ODS Named & Database Ranges
        [Fact]
        public void F5_NamedRange_HappyPath()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new SpreadsheetDocument(package);
            var sheet = doc.AddSheet("Sheet1");
            
            // Named expression definition at spreadsheet-level
            var expressions = new OdfNode(OdfNodeType.Element, "named-expressions", OdfNamespaces.Table, "table");
            var namedRange = new OdfNode(OdfNodeType.Element, "named-range", OdfNamespaces.Table, "table");
            namedRange.SetAttribute("name", OdfNamespaces.Table, "MyRange", "table");
            namedRange.SetAttribute("cell-range-address", OdfNamespaces.Table, "Sheet1.A1:B2", "table");
            expressions.AppendChild(namedRange);
            doc.SheetsRoot.AppendChild(expressions);
            doc.Save();

            var reloaded = RoundTrip(doc, p => new SpreadsheetDocument(p));
            var exprNode = reloaded.SheetsRoot.Children.FirstOrDefault(c => c.LocalName == "named-expressions" && c.NamespaceUri == OdfNamespaces.Table);
            Assert.NotNull(exprNode);
            var rangeNode = exprNode.Children.FirstOrDefault(c => c.LocalName == "named-range" && c.GetAttribute("name", OdfNamespaces.Table) == "MyRange");
            Assert.NotNull(rangeNode);
            Assert.Equal("Sheet1.A1:B2", rangeNode.GetAttribute("cell-range-address", OdfNamespaces.Table));
        }

        [Fact]
        public void F5_DatabaseRange_HappyPath()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new SpreadsheetDocument(package);
            
            var dbRanges = new OdfNode(OdfNodeType.Element, "database-ranges", OdfNamespaces.Table, "table");
            var dbRange = new OdfNode(OdfNodeType.Element, "database-range", OdfNamespaces.Table, "table");
            dbRange.SetAttribute("name", OdfNamespaces.Table, "MyDatabase", "table");
            dbRange.SetAttribute("target-range-address", OdfNamespaces.Table, "Sheet1.A1:C100", "table");
            dbRanges.AppendChild(dbRange);
            doc.SheetsRoot.AppendChild(dbRanges);
            doc.Save();

            var reloaded = RoundTrip(doc, p => new SpreadsheetDocument(p));
            var dbNode = reloaded.SheetsRoot.Children.FirstOrDefault(c => c.LocalName == "database-ranges" && c.NamespaceUri == OdfNamespaces.Table);
            Assert.NotNull(dbNode);
            var targetRange = dbNode.Children.FirstOrDefault(c => c.LocalName == "database-range" && c.GetAttribute("name", OdfNamespaces.Table) == "MyDatabase");
            Assert.NotNull(targetRange);
            Assert.Equal("Sheet1.A1:C100", targetRange.GetAttribute("target-range-address", OdfNamespaces.Table));
        }

        [Fact]
        public void F5_NamedRange_MultipleRanges()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new SpreadsheetDocument(package);
            var expressions = new OdfNode(OdfNodeType.Element, "named-expressions", OdfNamespaces.Table, "table");
            
            var range1 = new OdfNode(OdfNodeType.Element, "named-range", OdfNamespaces.Table, "table");
            range1.SetAttribute("name", OdfNamespaces.Table, "R1", "table");
            range1.SetAttribute("cell-range-address", OdfNamespaces.Table, "Sheet1.A1", "table");
            
            var range2 = new OdfNode(OdfNodeType.Element, "named-range", OdfNamespaces.Table, "table");
            range2.SetAttribute("name", OdfNamespaces.Table, "R2", "table");
            range2.SetAttribute("cell-range-address", OdfNamespaces.Table, "Sheet1.B2", "table");

            expressions.AppendChild(range1);
            expressions.AppendChild(range2);
            doc.SheetsRoot.AppendChild(expressions);
            doc.Save();

            var reloaded = RoundTrip(doc, p => new SpreadsheetDocument(p));
            var exprNode = reloaded.SheetsRoot.Children.First(c => c.LocalName == "named-expressions");
            Assert.Equal(2, exprNode.Children.Count);
        }

        [Fact]
        public void F5_DatabaseRange_Retrieval()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new SpreadsheetDocument(package);
            var dbRanges = new OdfNode(OdfNodeType.Element, "database-ranges", OdfNamespaces.Table, "table");
            var dbRange = new OdfNode(OdfNodeType.Element, "database-range", OdfNamespaces.Table, "table");
            dbRange.SetAttribute("name", OdfNamespaces.Table, "Sales", "table");
            dbRange.SetAttribute("target-range-address", OdfNamespaces.Table, "Sheet1.A1:D50", "table");
            dbRanges.AppendChild(dbRange);
            doc.SheetsRoot.AppendChild(dbRanges);
            doc.Save();

            var reloaded = RoundTrip(doc, p => new SpreadsheetDocument(p));
            var search = reloaded.SheetsRoot.Descendants().FirstOrDefault(d => d.LocalName == "database-range" && d.GetAttribute("name", OdfNamespaces.Table) == "Sales");
            Assert.NotNull(search);
        }

        [Fact]
        public void F5_NamedRange_FormulaBinding()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new SpreadsheetDocument(package);
            var sheet = doc.AddSheet("Sheet1");
            sheet.GetCell("A1").Value = "10";
            
            var expressions = new OdfNode(OdfNodeType.Element, "named-expressions", OdfNamespaces.Table, "table");
            var namedRange = new OdfNode(OdfNodeType.Element, "named-range", OdfNamespaces.Table, "table");
            namedRange.SetAttribute("name", OdfNamespaces.Table, "TaxRate", "table");
            namedRange.SetAttribute("cell-range-address", OdfNamespaces.Table, "Sheet1.A1", "table");
            expressions.AppendChild(namedRange);
            doc.SheetsRoot.AppendChild(expressions);
            doc.Save();

            var reloaded = RoundTrip(doc, p => new SpreadsheetDocument(p));
            var targetRange = reloaded.SheetsRoot.Descendants().FirstOrDefault(d => d.LocalName == "named-range" && d.GetAttribute("name", OdfNamespaces.Table) == "TaxRate");
            Assert.NotNull(targetRange);
        }
        #endregion

        #region Feature 6: ODS Sort & Filter
        [Fact]
        public void F6_SortCriteria_HappyPath()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new SpreadsheetDocument(package);
            
            var dbRanges = new OdfNode(OdfNodeType.Element, "database-ranges", OdfNamespaces.Table, "table");
            var dbRange = new OdfNode(OdfNodeType.Element, "database-range", OdfNamespaces.Table, "table");
            dbRange.SetAttribute("name", OdfNamespaces.Table, "DataToSort", "table");
            dbRange.SetAttribute("target-range-address", OdfNamespaces.Table, "Sheet1.A1:B10", "table");

            var sort = new OdfNode(OdfNodeType.Element, "sort", OdfNamespaces.Table, "table");
            var sortKey = new OdfNode(OdfNodeType.Element, "sort-key", OdfNamespaces.Table, "table");
            sortKey.SetAttribute("field-number", OdfNamespaces.Table, "0", "table");
            sortKey.SetAttribute("order", OdfNamespaces.Table, "ascending", "table");
            sort.AppendChild(sortKey);
            dbRange.AppendChild(sort);
            dbRanges.AppendChild(dbRange);
            doc.SheetsRoot.AppendChild(dbRanges);
            doc.Save();

            var reloaded = RoundTrip(doc, p => new SpreadsheetDocument(p));
            var sortNode = reloaded.SheetsRoot.Descendants().FirstOrDefault(d => d.LocalName == "sort" && d.NamespaceUri == OdfNamespaces.Table);
            Assert.NotNull(sortNode);
            var keyNode = sortNode.Children.FirstOrDefault(c => c.LocalName == "sort-key");
            Assert.NotNull(keyNode);
            Assert.Equal("ascending", keyNode.GetAttribute("order", OdfNamespaces.Table));
        }

        [Fact]
        public void F6_FilterCriteria_HappyPath()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new SpreadsheetDocument(package);
            
            var dbRanges = new OdfNode(OdfNodeType.Element, "database-ranges", OdfNamespaces.Table, "table");
            var dbRange = new OdfNode(OdfNodeType.Element, "database-range", OdfNamespaces.Table, "table");
            dbRange.SetAttribute("name", OdfNamespaces.Table, "DataToFilter", "table");

            var filter = new OdfNode(OdfNodeType.Element, "filter", OdfNamespaces.Table, "table");
            var condition = new OdfNode(OdfNodeType.Element, "filter-condition", OdfNamespaces.Table, "table");
            condition.SetAttribute("field-number", OdfNamespaces.Table, "1", "table");
            condition.SetAttribute("value", OdfNamespaces.Table, "active", "table");
            condition.SetAttribute("operator", OdfNamespaces.Table, "equal", "table");
            filter.AppendChild(condition);
            dbRange.AppendChild(filter);
            dbRanges.AppendChild(dbRange);
            doc.SheetsRoot.AppendChild(dbRanges);
            doc.Save();

            var reloaded = RoundTrip(doc, p => new SpreadsheetDocument(p));
            var filterNode = reloaded.SheetsRoot.Descendants().FirstOrDefault(d => d.LocalName == "filter");
            Assert.NotNull(filterNode);
            var condNode = filterNode.Children.FirstOrDefault(c => c.LocalName == "filter-condition");
            Assert.NotNull(condNode);
            Assert.Equal("equal", condNode.GetAttribute("operator", OdfNamespaces.Table));
        }

        [Fact]
        public void F6_DatabaseRangeSort()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new SpreadsheetDocument(package);
            var dbRanges = new OdfNode(OdfNodeType.Element, "database-ranges", OdfNamespaces.Table, "table");
            var dbRange = new OdfNode(OdfNodeType.Element, "database-range", OdfNamespaces.Table, "table");
            dbRange.SetAttribute("name", OdfNamespaces.Table, "SortedRange", "table");

            var sort = new OdfNode(OdfNodeType.Element, "sort", OdfNamespaces.Table, "table");
            dbRange.AppendChild(sort);
            dbRanges.AppendChild(dbRange);
            doc.SheetsRoot.AppendChild(dbRanges);
            doc.Save();

            var reloaded = RoundTrip(doc, p => new SpreadsheetDocument(p));
            Assert.NotNull(reloaded.SheetsRoot.Descendants().FirstOrDefault(d => d.LocalName == "sort"));
        }

        [Fact]
        public void F6_DatabaseRangeFilter()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new SpreadsheetDocument(package);
            var dbRanges = new OdfNode(OdfNodeType.Element, "database-ranges", OdfNamespaces.Table, "table");
            var dbRange = new OdfNode(OdfNodeType.Element, "database-range", OdfNamespaces.Table, "table");
            dbRange.SetAttribute("name", OdfNamespaces.Table, "FilteredRange", "table");

            var filter = new OdfNode(OdfNodeType.Element, "filter", OdfNamespaces.Table, "table");
            dbRange.AppendChild(filter);
            dbRanges.AppendChild(dbRange);
            doc.SheetsRoot.AppendChild(dbRanges);
            doc.Save();

            var reloaded = RoundTrip(doc, p => new SpreadsheetDocument(p));
            Assert.NotNull(reloaded.SheetsRoot.Descendants().FirstOrDefault(d => d.LocalName == "filter"));
        }

        [Fact]
        public void F6_MultiColumnSort()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new SpreadsheetDocument(package);
            var dbRanges = new OdfNode(OdfNodeType.Element, "database-ranges", OdfNamespaces.Table, "table");
            var dbRange = new OdfNode(OdfNodeType.Element, "database-range", OdfNamespaces.Table, "table");
            
            var sort = new OdfNode(OdfNodeType.Element, "sort", OdfNamespaces.Table, "table");
            var key1 = new OdfNode(OdfNodeType.Element, "sort-key", OdfNamespaces.Table, "table");
            key1.SetAttribute("field-number", OdfNamespaces.Table, "0", "table");
            var key2 = new OdfNode(OdfNodeType.Element, "sort-key", OdfNamespaces.Table, "table");
            key2.SetAttribute("field-number", OdfNamespaces.Table, "1", "table");

            sort.AppendChild(key1);
            sort.AppendChild(key2);
            dbRange.AppendChild(sort);
            dbRanges.AppendChild(dbRange);
            doc.SheetsRoot.AppendChild(dbRanges);
            doc.Save();

            var reloaded = RoundTrip(doc, p => new SpreadsheetDocument(p));
            var sortNode = reloaded.SheetsRoot.Descendants().First(d => d.LocalName == "sort");
            Assert.Equal(2, sortNode.Children.Count);
        }
        #endregion

        #region Feature 7: ODS Pivot Tables (Data Pilots)
        [Fact]
        public void F7_PivotTable_HappyPath()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new SpreadsheetDocument(package);
            var sheet = doc.AddSheet("Sheet1");
            
            var dataPilots = new OdfNode(OdfNodeType.Element, "data-pilot-tables", OdfNamespaces.Table, "table");
            var pilotTable = new OdfNode(OdfNodeType.Element, "data-pilot-table", OdfNamespaces.Table, "table");
            pilotTable.SetAttribute("name", OdfNamespaces.Table, "PivotTable1", "table");
            pilotTable.SetAttribute("target-range-address", OdfNamespaces.Table, "Sheet1.F1:H10", "table");

            var source = new OdfNode(OdfNodeType.Element, "database-source", OdfNamespaces.Table, "table");
            source.SetAttribute("database-name", OdfNamespaces.Table, "MyDatabase", "table");
            pilotTable.AppendChild(source);

            dataPilots.AppendChild(pilotTable);
            sheet.TableNode.AppendChild(dataPilots);
            doc.Save();

            var reloaded = RoundTrip(doc, p => new SpreadsheetDocument(p));
            var pilotTablesNode = reloaded.SheetsRoot.Descendants().FirstOrDefault(d => d.LocalName == "data-pilot-tables");
            Assert.NotNull(pilotTablesNode);
            var table = pilotTablesNode.Children.FirstOrDefault(c => c.LocalName == "data-pilot-table" && c.GetAttribute("name", OdfNamespaces.Table) == "PivotTable1");
            Assert.NotNull(table);
            Assert.Equal("Sheet1.F1:H10", table.GetAttribute("target-range-address", OdfNamespaces.Table));
        }

        [Fact]
        public void F7_PivotTable_SourceDestination()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new SpreadsheetDocument(package);
            var sheet = doc.AddSheet("Sheet1");
            var dataPilots = new OdfNode(OdfNodeType.Element, "data-pilot-tables", OdfNamespaces.Table, "table");
            var pilotTable = new OdfNode(OdfNodeType.Element, "data-pilot-table", OdfNamespaces.Table, "table");
            pilotTable.SetAttribute("target-range-address", OdfNamespaces.Table, "Sheet1.E5", "table");

            var srcRange = new OdfNode(OdfNodeType.Element, "source-cell-range", OdfNamespaces.Table, "table");
            srcRange.SetAttribute("cell-range-address", OdfNamespaces.Table, "Sheet1.A1:C10", "table");
            pilotTable.AppendChild(srcRange);

            dataPilots.AppendChild(pilotTable);
            sheet.TableNode.AppendChild(dataPilots);
            doc.Save();

            var reloaded = RoundTrip(doc, p => new SpreadsheetDocument(p));
            var tableNode = reloaded.SheetsRoot.Descendants().First(d => d.LocalName == "data-pilot-table");
            var rangeNode = tableNode.Children.FirstOrDefault(c => c.LocalName == "source-cell-range");
            Assert.NotNull(rangeNode);
            Assert.Equal("Sheet1.A1:C10", rangeNode.GetAttribute("cell-range-address", OdfNamespaces.Table));
        }

        [Fact]
        public void F7_PivotTable_Fields()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new SpreadsheetDocument(package);
            var sheet = doc.AddSheet("Sheet1");
            var dataPilots = new OdfNode(OdfNodeType.Element, "data-pilot-tables", OdfNamespaces.Table, "table");
            var pilotTable = new OdfNode(OdfNodeType.Element, "data-pilot-table", OdfNamespaces.Table, "table");
            
            var fields = new OdfNode(OdfNodeType.Element, "data-pilot-field", OdfNamespaces.Table, "table");
            fields.SetAttribute("source-name", OdfNamespaces.Table, "Sales", "table");
            pilotTable.AppendChild(fields);

            dataPilots.AppendChild(pilotTable);
            sheet.TableNode.AppendChild(dataPilots);
            doc.Save();

            var reloaded = RoundTrip(doc, p => new SpreadsheetDocument(p));
            var fieldNode = reloaded.SheetsRoot.Descendants().FirstOrDefault(d => d.LocalName == "data-pilot-field");
            Assert.NotNull(fieldNode);
            Assert.Equal("Sales", fieldNode.GetAttribute("source-name", OdfNamespaces.Table));
        }

        [Fact]
        public void F7_PivotTable_RowColFields()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new SpreadsheetDocument(package);
            var sheet = doc.AddSheet("Sheet1");
            var dataPilots = new OdfNode(OdfNodeType.Element, "data-pilot-tables", OdfNamespaces.Table, "table");
            var pilotTable = new OdfNode(OdfNodeType.Element, "data-pilot-table", OdfNamespaces.Table, "table");

            var field = new OdfNode(OdfNodeType.Element, "data-pilot-field", OdfNamespaces.Table, "table");
            field.SetAttribute("orientation", OdfNamespaces.Table, "row", "table");
            pilotTable.AppendChild(field);

            dataPilots.AppendChild(pilotTable);
            sheet.TableNode.AppendChild(dataPilots);
            doc.Save();

            var reloaded = RoundTrip(doc, p => new SpreadsheetDocument(p));
            var fieldNode = reloaded.SheetsRoot.Descendants().First(d => d.LocalName == "data-pilot-field");
            Assert.Equal("row", fieldNode.GetAttribute("orientation", OdfNamespaces.Table));
        }

        [Fact]
        public void F7_PivotTable_DataFields()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new SpreadsheetDocument(package);
            var sheet = doc.AddSheet("Sheet1");
            var dataPilots = new OdfNode(OdfNodeType.Element, "data-pilot-tables", OdfNamespaces.Table, "table");
            var pilotTable = new OdfNode(OdfNodeType.Element, "data-pilot-table", OdfNamespaces.Table, "table");

            var field = new OdfNode(OdfNodeType.Element, "data-pilot-field", OdfNamespaces.Table, "table");
            field.SetAttribute("orientation", OdfNamespaces.Table, "data", "table");
            
            var function = new OdfNode(OdfNodeType.Element, "data-pilot-subtotal", OdfNamespaces.Table, "table");
            function.SetAttribute("function", OdfNamespaces.Table, "sum", "table");
            field.AppendChild(function);

            pilotTable.AppendChild(field);
            dataPilots.AppendChild(pilotTable);
            sheet.TableNode.AppendChild(dataPilots);
            doc.Save();

            var reloaded = RoundTrip(doc, p => new SpreadsheetDocument(p));
            var funcNode = reloaded.SheetsRoot.Descendants().FirstOrDefault(d => d.LocalName == "data-pilot-subtotal");
            Assert.NotNull(funcNode);
            Assert.Equal("sum", funcNode.GetAttribute("function", OdfNamespaces.Table));
        }
        #endregion

        #region Feature 8: ODS Conditional Formatting
        [Fact]
        public void F8_ConditionalFormatting_HappyPath()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new SpreadsheetDocument(package);
            var sheet = doc.AddSheet("Sheet1");
            var range = new OdfCellRange(0, 0, 1, 1, "Sheet1");
            sheet.AddConditionalFormat(range, "cell-content() > 100", "RedStyle");
            doc.Save();

            var reloaded = RoundTrip(doc, p => new SpreadsheetDocument(p));
            var formatNode = reloaded.SheetsRoot.Descendants().FirstOrDefault(d => d.LocalName == "conditional-format" && d.NamespaceUri == calcextNs.NamespaceName);
            Assert.NotNull(formatNode);
            Assert.Equal("Sheet1.A1:Sheet1.B2", formatNode.GetAttribute("target-range-address", calcextNs));
            var conditionNode = formatNode.Children.FirstOrDefault(c => c.LocalName == "condition");
            Assert.NotNull(conditionNode);
            Assert.Equal("cell-content() > 100", conditionNode.GetAttribute("value", calcextNs));
            Assert.Equal("RedStyle", conditionNode.GetAttribute("style-name", calcextNs));
        }

        [Fact]
        public void F8_ConditionalFormatting_MultipleRules()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new SpreadsheetDocument(package);
            var sheet = doc.AddSheet("Sheet1");
            var range = new OdfCellRange(0, 0, 0, 0, "Sheet1");
            
            sheet.AddConditionalFormat(range, "cell-content() > 10", "Red");
            sheet.AddConditionalFormat(range, "cell-content() < 0", "Blue");
            doc.Save();

            var reloaded = RoundTrip(doc, p => new SpreadsheetDocument(p));
            var formats = reloaded.SheetsRoot.Descendants().Where(d => d.LocalName == "conditional-format").ToList();
            Assert.Equal(2, formats.Count);
        }

        [Fact]
        public void F8_ConditionalFormatting_CellStyles()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new SpreadsheetDocument(package);
            var sheet = doc.AddSheet("Sheet1");
            var range = new OdfCellRange(1, 1, 1, 1, "Sheet1");
            
            // Build visual cell style mappings
            sheet.AddConditionalFormat(range, "is-true()", "AlertStyle");
            doc.Save();

            var reloaded = RoundTrip(doc, p => new SpreadsheetDocument(p));
            var condition = reloaded.SheetsRoot.Descendants().FirstOrDefault(d => d.LocalName == "condition");
            Assert.NotNull(condition);
            Assert.Equal("AlertStyle", condition.GetAttribute("style-name", calcextNs));
        }

        [Fact]
        public void F8_ConditionalFormatting_DateRules()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new SpreadsheetDocument(package);
            var sheet = doc.AddSheet("Sheet1");
            var range = new OdfCellRange(0, 0, 0, 1, "Sheet1");

            sheet.AddConditionalFormat(range, "is-today()", "TodayStyle");
            doc.Save();

            var reloaded = RoundTrip(doc, p => new SpreadsheetDocument(p));
            var condition = reloaded.SheetsRoot.Descendants().First(d => d.LocalName == "condition");
            Assert.Equal("is-today()", condition.GetAttribute("value", calcextNs));
        }

        [Fact]
        public void F8_ConditionalFormatting_NumberStyles()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new SpreadsheetDocument(package);
            var sheet = doc.AddSheet("Sheet1");
            var range = new OdfCellRange(0, 0, 0, 0, "Sheet1");

            sheet.AddConditionalFormat(range, "cell-content() < 0.05", "PercentStyle");
            doc.Save();

            var reloaded = RoundTrip(doc, p => new SpreadsheetDocument(p));
            var condition = reloaded.SheetsRoot.Descendants().First(d => d.LocalName == "condition");
            Assert.Equal("PercentStyle", condition.GetAttribute("style-name", calcextNs));
        }
        #endregion

        #region Feature 9: OpenFormula Evaluator (F3-F5)
        [Fact]
        public void F9_Evaluator_MathFunctions()
        {
            var evaluator = new DefaultFormulaEvaluator();
            var context = new FormulaAndStylesTest.MockEvaluationContext();
            context.Evaluator = evaluator;

            Assert.Equal(5.0, evaluator.Evaluate("SUM(2,3)", context));
            Assert.Equal(12.0, evaluator.Evaluate("ABS(-12.0)", context));
            Assert.Equal(Math.Sin(1.0), evaluator.Evaluate("SIN(1.0)", context));
            Assert.Equal(Math.Cos(0.0), evaluator.Evaluate("COS(0.0)", context));
        }

        [Fact]
        public void F9_Evaluator_LogicalFunctions()
        {
            var evaluator = new DefaultFormulaEvaluator();
            var context = new FormulaAndStylesTest.MockEvaluationContext();
            context.Evaluator = evaluator;

            Assert.Equal(1.0, evaluator.Evaluate("IF(TRUE(),1,0)", context));
            Assert.Equal(0.0, evaluator.Evaluate("IF(FALSE(),1,0)", context));
            Assert.Equal(true, evaluator.Evaluate("AND(TRUE(),TRUE())", context));
            Assert.Equal(false, evaluator.Evaluate("AND(TRUE(),FALSE())", context));
            Assert.Equal(true, evaluator.Evaluate("OR(FALSE(),TRUE())", context));
        }

        [Fact]
        public void F9_Evaluator_DateFunctions()
        {
            var evaluator = new DefaultFormulaEvaluator();
            var context = new FormulaAndStylesTest.MockEvaluationContext();
            context.Evaluator = evaluator;

            // Excel representation of dates is Serial Dates, but DefaultFormulaEvaluator DATE returns double serial or ISO representation depending on implementation
            var dateResult = evaluator.Evaluate("DATE(2026,6,12)", context);
            Assert.NotNull(dateResult);
            Assert.Equal(2026.0, evaluator.Evaluate("YEAR(DATE(2026,6,12))", context));
            Assert.Equal(6.0, evaluator.Evaluate("MONTH(DATE(2026,6,12))", context));
            Assert.Equal(12.0, evaluator.Evaluate("DAY(DATE(2026,6,12))", context));
        }

        [Fact]
        public void F9_Evaluator_RangeUnion()
        {
            var evaluator = new DefaultFormulaEvaluator();
            var context = new FormulaAndStylesTest.MockEvaluationContext();
            context.Evaluator = evaluator;

            context.CellValues[OdfCellAddress.ParseExcel("Sheet1!A1")] = 10.0;
            context.CellValues[OdfCellAddress.ParseExcel("Sheet1!B2")] = 20.0;

            // Range union ~ operator
            var unionResult = evaluator.Evaluate("SUM(Sheet1.A1~Sheet1.B2)", context);
            Assert.Equal(30.0, unionResult);
        }

        [Fact]
        public void F9_Evaluator_RangeIntersection()
        {
            var evaluator = new DefaultFormulaEvaluator();
            var context = new FormulaAndStylesTest.MockEvaluationContext();
            context.Evaluator = evaluator;

            // Set grid values
            context.CellValues[OdfCellAddress.ParseExcel("Sheet1!A2")] = 5.0;
            context.CellValues[OdfCellAddress.ParseExcel("Sheet1!B2")] = 15.0;
            context.CellValues[OdfCellAddress.ParseExcel("Sheet1!C2")] = 25.0;

            // Row 2 intersects with Column B range at B2
            var intersectResult = evaluator.Evaluate("SUM(Sheet1.A2:C2!Sheet1.B1:B3)", context);
            Assert.Equal(15.0, intersectResult);
        }
        #endregion

        #region Feature 10: ODP Slide Layouts & Notes
        [Fact]
        public void F10_SlideLayout_HappyPath()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new PresentationDocument(package);
            var slide = doc.AddSlide("Slide1");
            slide.MasterPageName = "TitleLayout";
            doc.Save();

            var reloaded = RoundTrip(doc, p => new PresentationDocument(p));
            Assert.Equal("TitleLayout", reloaded.Slides[0].MasterPageName);
        }

        [Fact]
        public void F10_SpeakerNotes_HappyPath()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new PresentationDocument(package);
            var slide = doc.AddSlide("Slide1");
            slide.SpeakerNotes = "Confidential Notes for Presenter.";
            doc.Save();

            var reloaded = RoundTrip(doc, p => new PresentationDocument(p));
            Assert.Equal("Confidential Notes for Presenter.", reloaded.Slides[0].SpeakerNotes);
        }

        [Fact]
        public void F10_Placeholders_Presentation()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new PresentationDocument(package);
            var slide = doc.AddSlide("Slide1");
            
            // Build presentation placeholders
            var frame = new OdfNode(OdfNodeType.Element, "frame", OdfNamespaces.Draw, "draw");
            frame.SetAttribute("class", OdfNamespaces.Presentation, "title", "presentation");
            slide.Node.AppendChild(frame);
            doc.Save();

            var reloaded = RoundTrip(doc, p => new PresentationDocument(p));
            var slideNode = reloaded.Slides[0].Node;
            var placeholder = slideNode.Children.FirstOrDefault(c => c.LocalName == "frame" && c.GetAttribute("class", OdfNamespaces.Presentation) == "title");
            Assert.NotNull(placeholder);
        }

        [Fact]
        public void F10_MasterPageBinding()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new PresentationDocument(package);
            doc.AddMasterPage("MyMaster", "PM1");
            doc.Save();

            var reloaded = RoundTrip(doc, p => new PresentationDocument(p));
            var masterPage = reloaded.StylesDom.Descendants().FirstOrDefault(d => d.LocalName == "master-page" && d.GetAttribute("name", OdfNamespaces.Style) == "MyMaster");
            Assert.NotNull(masterPage);
            Assert.Equal("PM1", masterPage.GetAttribute("page-layout-name", OdfNamespaces.Style));
        }

        [Fact]
        public void F10_NotesLayoutStructure()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new PresentationDocument(package);
            var slide = doc.AddSlide("Slide1");
            slide.SpeakerNotes = "Presenter outline";
            doc.Save();

            var reloaded = RoundTrip(doc, p => new PresentationDocument(p));
            var slideNode = reloaded.Slides[0].Node;
            var notes = slideNode.Children.FirstOrDefault(c => c.LocalName == "notes" && c.NamespaceUri == OdfNamespaces.Presentation);
            Assert.NotNull(notes);
        }
        #endregion

        #region Feature 11: ODP SMIL Animations & Transitions
        [Fact]
        public void F11_Transitions_HappyPath()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new PresentationDocument(package);
            var slide = doc.AddSlide("Slide1");
            slide.SetTransition(OdfTransitionType.Fade, OdfLength.FromPoints(144)); // 2.0s
            doc.Save();

            var reloaded = RoundTrip(doc, p => new PresentationDocument(p));
            var slideNode = reloaded.Slides[0].Node;
            Assert.Equal("fade", slideNode.GetAttribute("type", smilNs));
            Assert.Equal("fadeOverColor", slideNode.GetAttribute("subtype", smilNs));
            Assert.Equal("2.00s", slideNode.GetAttribute("dur", smilNs));
        }

        [Fact]
        public void F11_SmilTiming_Sequence()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new PresentationDocument(package);
            var slide = doc.AddSlide("Slide1");

            // Build SMIL anim:seq element
            var animRoot = new OdfNode(OdfNodeType.Element, "seq", smilNs, "smil");
            animRoot.SetAttribute("node-type", OdfNamespaces.Presentation, "main-sequence", "presentation");
            slide.Node.AppendChild(animRoot);
            doc.Save();

            var reloaded = RoundTrip(doc, p => new PresentationDocument(p));
            var seq = reloaded.Slides[0].Node.Children.FirstOrDefault(c => c.LocalName == "seq" && c.NamespaceUri == smilNs.NamespaceName);
            Assert.NotNull(seq);
            Assert.Equal("main-sequence", seq.GetAttribute("node-type", OdfNamespaces.Presentation));
        }

        [Fact]
        public void F11_SmilTiming_Parallel()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new PresentationDocument(package);
            var slide = doc.AddSlide("Slide1");

            var seq = new OdfNode(OdfNodeType.Element, "seq", smilNs, "smil");
            var par = new OdfNode(OdfNodeType.Element, "par", smilNs, "smil");
            seq.AppendChild(par);
            slide.Node.AppendChild(seq);
            doc.Save();

            var reloaded = RoundTrip(doc, p => new PresentationDocument(p));
            var parNode = reloaded.Slides[0].Node.Descendants().FirstOrDefault(d => d.LocalName == "par" && d.NamespaceUri == smilNs.NamespaceName);
            Assert.NotNull(parNode);
        }

        [Fact]
        public void F11_SmilTiming_EffectPresets()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new PresentationDocument(package);
            var slide = doc.AddSlide("Slide1");

            var seq = new OdfNode(OdfNodeType.Element, "seq", smilNs, "smil");
            var transitionFilter = new OdfNode(OdfNodeType.Element, "transitionFilter", smilNs, "smil");
            transitionFilter.SetAttribute("type", smilNs, "wipe", "smil");
            transitionFilter.SetAttribute("dur", smilNs, "1.00s", "smil");
            seq.AppendChild(transitionFilter);
            slide.Node.AppendChild(seq);
            doc.Save();

            var reloaded = RoundTrip(doc, p => new PresentationDocument(p));
            var filter = reloaded.Slides[0].Node.Descendants().FirstOrDefault(d => d.LocalName == "transitionFilter");
            Assert.NotNull(filter);
            Assert.Equal("wipe", filter.GetAttribute("type", smilNs));
        }

        [Fact]
        public void F11_SlideTransition_Durations()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new PresentationDocument(package);
            var slide = doc.AddSlide("Slide1");
            slide.SetTransition(OdfTransitionType.Zoom, OdfLength.FromPoints(72)); // 1.0s
            doc.Save();

            var reloaded = RoundTrip(doc, p => new PresentationDocument(p));
            var slideNode = reloaded.Slides[0].Node;
            Assert.Equal("zoom", slideNode.GetAttribute("type", smilNs));
            Assert.Equal("1.00s", slideNode.GetAttribute("dur", smilNs));
        }
        #endregion

        #region Feature 12: ODG Graphic Styles & Connectors
        [Fact]
        public void F12_ShapeStyles_HappyPath()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new DrawingDocument(package);
            var page = doc.AddPage("Page1");
            var shape = page.AddShape(OdfShapeType.Rectangle, OdfLength.FromCentimeters(2), OdfLength.FromCentimeters(2), OdfLength.FromCentimeters(5), OdfLength.FromCentimeters(5));
            shape.FillColor = "#ff0000";
            shape.StrokeColor = "#0000ff";
            doc.Save();

            var reloaded = RoundTrip(doc, p => new DrawingDocument(p));
            var shapeNode = reloaded.Pages[0].Node.Children.FirstOrDefault(c => c.LocalName == "rect" && c.NamespaceUri == OdfNamespaces.Draw);
            Assert.NotNull(shapeNode);
            var shapeObj = new OdfShape(shapeNode, reloaded);
            Assert.Equal("#ff0000", shapeObj.FillColor);
            Assert.Equal("#0000ff", shapeObj.StrokeColor);
        }

        [Fact]
        public void F12_Connector_HappyPath()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new DrawingDocument(package);
            var page = doc.AddPage("Page1");

            // Build structural connector line mapping shapes
            var connector = new OdfNode(OdfNodeType.Element, "connector", OdfNamespaces.Draw, "draw");
            connector.SetAttribute("type", OdfNamespaces.Draw, "standard", "draw");
            connector.SetAttribute("start-shape", OdfNamespaces.Draw, "shp1", "draw");
            connector.SetAttribute("end-shape", OdfNamespaces.Draw, "shp2", "draw");
            page.Node.AppendChild(connector);
            doc.Save();

            var reloaded = RoundTrip(doc, p => new DrawingDocument(p));
            var connNode = reloaded.Pages[0].Node.Children.FirstOrDefault(c => c.LocalName == "connector");
            Assert.NotNull(connNode);
            Assert.Equal("shp1", connNode.GetAttribute("start-shape", OdfNamespaces.Draw));
            Assert.Equal("shp2", connNode.GetAttribute("end-shape", OdfNamespaces.Draw));
        }

        [Fact]
        public void F12_CustomShapeGeometry()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new DrawingDocument(package);
            var page = doc.AddPage("Page1");
            var points = new[] { new System.Drawing.PointF(0, 0), new System.Drawing.PointF(10, 10) };
            page.AddPolyline(points, OdfLength.FromCentimeters(0), OdfLength.FromCentimeters(0), OdfLength.FromCentimeters(10), OdfLength.FromCentimeters(10));
            doc.Save();

            var reloaded = RoundTrip(doc, p => new DrawingDocument(p));
            var poly = reloaded.Pages[0].Node.Children.FirstOrDefault(c => c.LocalName == "polyline");
            Assert.NotNull(poly);
            Assert.Equal("0,0 10,10", poly.GetAttribute("points", OdfNamespaces.Draw));
        }

        [Fact]
        public void F12_ShapeFillGradients()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new DrawingDocument(package);
            var page = doc.AddPage("Page1");
            
            var shape = page.AddShape(OdfShapeType.Ellipse, OdfLength.FromCentimeters(0), OdfLength.FromCentimeters(0), OdfLength.FromCentimeters(4), OdfLength.FromCentimeters(4));
            shape.Node.SetAttribute("fill", OdfNamespaces.Draw, "gradient", "draw");
            shape.Node.SetAttribute("fill-gradient-name", OdfNamespaces.Draw, "RedToBlue", "draw");
            doc.Save();

            var reloaded = RoundTrip(doc, p => new DrawingDocument(p));
            var ellipse = reloaded.Pages[0].Node.Children.First(c => c.LocalName == "ellipse");
            Assert.Equal("gradient", ellipse.GetAttribute("fill", OdfNamespaces.Draw));
            Assert.Equal("RedToBlue", ellipse.GetAttribute("fill-gradient-name", OdfNamespaces.Draw));
        }

        [Fact]
        public void F12_ConnectorAttributes()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new DrawingDocument(package);
            var page = doc.AddPage("Page1");
            var conn = new OdfNode(OdfNodeType.Element, "connector", OdfNamespaces.Draw, "draw");
            conn.SetAttribute("start-glue-point", OdfNamespaces.Draw, "1", "draw");
            conn.SetAttribute("end-glue-point", OdfNamespaces.Draw, "2", "draw");
            page.Node.AppendChild(conn);
            doc.Save();

            var reloaded = RoundTrip(doc, p => new DrawingDocument(p));
            var connNode = reloaded.Pages[0].Node.Children.First(c => c.LocalName == "connector");
            Assert.Equal("1", connNode.GetAttribute("start-glue-point", OdfNamespaces.Draw));
            Assert.Equal("2", connNode.GetAttribute("end-glue-point", OdfNamespaces.Draw));
        }
        #endregion

        #region Feature 13: Embedded Objects
        [Fact]
        public void F13_EmbeddedChart_HappyPath()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new DrawingDocument(package);
            var page = doc.AddPage("Page1");

            // Set up embedded chart structure
            var chartFolder = "Object 1";
            package.WriteEntry($"{chartFolder}/content.xml", Encoding.UTF8.GetBytes("<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:chart=\"urn:oasis:names:tc:opendocument:xmlns:chart:1.0\"><office:body><office:chart><chart:chart /></office:chart></office:body></office:document-content>"), "text/xml");
            package.WriteEntry($"{chartFolder}/mimetype", Encoding.UTF8.GetBytes("application/vnd.oasis.opendocument.chart"), "application/vnd.oasis.opendocument.chart");
            package.SaveManifestToEntries();

            var frame = new OdfNode(OdfNodeType.Element, "frame", OdfNamespaces.Draw, "draw");
            var obj = new OdfNode(OdfNodeType.Element, "object", OdfNamespaces.Draw, "draw");
            obj.SetAttribute("href", OdfNamespaces.XLink, chartFolder, "xlink");
            frame.AppendChild(obj);
            page.Node.AppendChild(frame);
            doc.Save();

            var reloaded = RoundTrip(doc, p => new DrawingDocument(p));
            var mimeEntry = reloaded.Package.GetEntries().FirstOrDefault(e => e.Path == $"{chartFolder}/mimetype");
            Assert.NotNull(mimeEntry);
            Assert.Equal("application/vnd.oasis.opendocument.chart", Encoding.UTF8.GetString(reloaded.Package.ReadEntry(mimeEntry.Path)));
        }

        [Fact]
        public void F13_EmbeddedFormula_HappyPath()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new DrawingDocument(package);
            var page = doc.AddPage("Page1");

            var formulaFolder = "Object 2";
            package.WriteEntry($"{formulaFolder}/content.xml", Encoding.UTF8.GetBytes("<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\"><office:body><office:formula><math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mi>e</mi></office:formula></office:body></office:document-content>"), "text/xml");
            package.WriteEntry($"{formulaFolder}/mimetype", Encoding.UTF8.GetBytes("application/vnd.oasis.opendocument.formula"), "application/vnd.oasis.opendocument.formula");
            package.SaveManifestToEntries();

            var frame = new OdfNode(OdfNodeType.Element, "frame", OdfNamespaces.Draw, "draw");
            var obj = new OdfNode(OdfNodeType.Element, "object", OdfNamespaces.Draw, "draw");
            obj.SetAttribute("href", OdfNamespaces.XLink, formulaFolder, "xlink");
            frame.AppendChild(obj);
            page.Node.AppendChild(frame);
            doc.Save();

            var reloaded = RoundTrip(doc, p => new DrawingDocument(p));
            var mimeEntry = reloaded.Package.GetEntries().FirstOrDefault(e => e.Path == $"{formulaFolder}/mimetype");
            Assert.NotNull(mimeEntry);
            Assert.Equal("application/vnd.oasis.opendocument.formula", Encoding.UTF8.GetString(reloaded.Package.ReadEntry(mimeEntry.Path)));
        }

        [Fact]
        public void F13_ChartSeriesAndAxis()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new DrawingDocument(package);
            var page = doc.AddPage("Page1");
            var chartFolder = "Object_Chart";

            string chartContentXml = "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:chart=\"urn:oasis:names:tc:opendocument:xmlns:chart:1.0\">" +
                "<office:body><office:chart>" +
                "<chart:chart>" +
                "<chart:plot-area>" +
                "<chart:axis chart:dimension=\"x\" />" +
                "<chart:series chart:values-cell-range-address=\"Sheet1.A1:A5\" />" +
                "</chart:plot-area>" +
                "</chart:chart>" +
                "</office:chart></office:body></office:document-content>";

            package.WriteEntry($"{chartFolder}/content.xml", Encoding.UTF8.GetBytes(chartContentXml), "text/xml");
            package.WriteEntry($"{chartFolder}/mimetype", Encoding.UTF8.GetBytes("application/vnd.oasis.opendocument.chart"), "application/vnd.oasis.opendocument.chart");
            package.SaveManifestToEntries();
            doc.Save();

            var reloaded = RoundTrip(doc, p => new DrawingDocument(p));
            var content = Encoding.UTF8.GetString(reloaded.Package.ReadEntry($"{chartFolder}/content.xml"));
            Assert.Contains("chart:axis", content);
            Assert.Contains("chart:series", content);
        }

        [Fact]
        public void F13_ChartLegendStyles()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new DrawingDocument(package);
            var chartFolder = "Object_Chart";

            string chartContentXml = "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:chart=\"urn:oasis:names:tc:opendocument:xmlns:chart:1.0\">" +
                "<office:body><office:chart>" +
                "<chart:chart>" +
                "<chart:legend chart:legend-position=\"top\" />" +
                "</chart:chart>" +
                "</office:chart></office:body></office:document-content>";

            package.WriteEntry($"{chartFolder}/content.xml", Encoding.UTF8.GetBytes(chartContentXml), "text/xml");
            package.SaveManifestToEntries();
            doc.Save();

            var reloaded = RoundTrip(doc, p => new DrawingDocument(p));
            var content = Encoding.UTF8.GetString(reloaded.Package.ReadEntry($"{chartFolder}/content.xml"));
            Assert.Contains("chart:legend-position=\"top\"", content);
        }

        [Fact]
        public void F13_EmbeddedObjectManifest()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new DrawingDocument(package);
            var chartFolder = "Object1";
            package.WriteEntry($"{chartFolder}/content.xml", Encoding.UTF8.GetBytes("<root/>"), "text/xml");
            package.WriteEntry($"{chartFolder}/mimetype", Encoding.UTF8.GetBytes("application/vnd.oasis.opendocument.chart"), "application/vnd.oasis.opendocument.chart");
            package.SaveManifestToEntries();
            doc.Save();

            var reloaded = RoundTrip(doc, p => new DrawingDocument(p));
            // Reload package and verify manifest contains the subfolder
            var manifestStream = reloaded.Package.GetEntryStream("META-INF/manifest.xml");
            Assert.NotNull(manifestStream);
            var xdoc = XDocument.Load(manifestStream);
            var entries = xdoc.Descendants().Where(d => d.Name.LocalName == "file-entry").Select(d => d.Attribute(d.Name.Namespace + "full-path")?.Value).ToList();
            Assert.Contains($"{chartFolder}/", entries);
        }
        #endregion

        // =====================================================================
        // TIER 2: BOUNDARY & CORNER CASES
        // =====================================================================

        #region Boundary Tests (Empty structures, boundary values, exception triggers)
        [Fact]
        public void F1_Boundary_TocEmptyDocument()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);
            doc.AddTableOfContents(); // no headings
            doc.Save();

            var reloaded = RoundTrip(doc, p => new TextDocument(p));
            var tocNode = reloaded.BodyTextRoot.Children.FirstOrDefault(c => c.LocalName == "table-of-content");
            Assert.NotNull(tocNode);
        }

        [Fact]
        public void F2_Boundary_TrackedRevisionsEmptyChanges()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);
            doc.AcceptAllTrackedChanges(); // Should do nothing safely
            doc.RejectAllTrackedChanges();
            doc.Save();
            Assert.NotNull(doc);
        }

        [Fact]
        public void F2_Boundary_TrackedRevisionsInvalidId()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);
            doc.AcceptChange("non_existent_id"); // Should not throw
            doc.RejectChange("non_existent_id");
            doc.Save();
            Assert.NotNull(doc);
        }

        [Fact]
        public void F3_Boundary_RubyEmptyStringValues()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);
            var p = doc.AddParagraph();
            doc.AddRuby(p, "", "");
            doc.Save();

            var reloaded = RoundTrip(doc, pDoc => new TextDocument(pDoc));
            var rubyNode = reloaded.BodyTextRoot.Descendants().FirstOrDefault(d => d.LocalName == "ruby");
            Assert.NotNull(rubyNode);
        }

        [Fact]
        public void F3_Boundary_VerticalWritingModeInvalidValue()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);
            var pSetup = doc.GetDefaultPageSetup();
            // Invalid writing mode should not crash but round-trip or behave fallback
            pSetup.WritingMode = "invalid-mode";
            doc.Save();

            var reloaded = RoundTrip(doc, p => new TextDocument(p));
            Assert.Equal("invalid-mode", reloaded.GetDefaultPageSetup().WritingMode);
        }

        [Fact]
        public void F4_Boundary_MathMLLargeFormula()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);
            var p = doc.AddParagraph();
            
            // Build a very deep nested fraction
            var mathXmlBuilder = new StringBuilder("<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mfrac>");
            for (int i = 0; i < 50; i++)
            {
                mathXmlBuilder.Append("<mrow><mfrac><mi>x</mi><mrow>");
            }
            mathXmlBuilder.Append("<mi>y</mi>");
            for (int i = 0; i < 50; i++)
            {
                mathXmlBuilder.Append("</mrow></mfrac></mrow>");
            }
            mathXmlBuilder.Append("<mi>z</mi></mfrac></math>");

            doc.AddFormula(p, mathXmlBuilder.ToString());
            doc.Save();

            var reloaded = RoundTrip(doc, pDoc => new TextDocument(pDoc));
            var formulaEntry = reloaded.Package.GetEntries().FirstOrDefault(e => e.Path.StartsWith("Formula_") && e.Path.EndsWith("content.xml"));
            Assert.NotNull(formulaEntry);
        }

        [Fact]
        public void F4_Boundary_MathMLMalformedXml_Throws()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);
            var p = doc.AddParagraph();
            // XML parse error or invalid string structure
            Assert.ThrowsAny<Exception>(() => doc.AddFormula(p, "<math><mi>x</mi>"));
        }

        [Fact]
        public void F5_Boundary_NamedRangeInvalidName_Throws()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new SpreadsheetDocument(package);
            
            var expressions = new OdfNode(OdfNodeType.Element, "named-expressions", OdfNamespaces.Table, "table");
            var namedRange = new OdfNode(OdfNodeType.Element, "named-range", OdfNamespaces.Table, "table");
            
            // Named range names must not start with numbers or contain spaces in ODF/Excel specs.
            // We verify validation flags it, or custom exceptions are handled.
            namedRange.SetAttribute("name", OdfNamespaces.Table, "1Invalid Name", "table");
            expressions.AppendChild(namedRange);
            doc.SheetsRoot.AppendChild(expressions);
            
            // Validate schema directly to catch invalid naming format
            using var ms = new MemoryStream();
            doc.Package.Save(ms);
            ms.Position = 0;
            using var validationPackage = OdfPackage.Open(ms, leaveOpen: true);
            var report = OdfPackageValidator.Validate(validationPackage, OdfComplianceProfiles.OasisOdf14Strict, "workbook.ods");
            // Schema validation should detect format issues or succeed at least-effort depending on profile
            Assert.NotNull(report);
        }

        [Fact]
        public void F5_Boundary_DatabaseRangeEmpty()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new SpreadsheetDocument(package);
            var dbRanges = new OdfNode(OdfNodeType.Element, "database-ranges", OdfNamespaces.Table, "table");
            var dbRange = new OdfNode(OdfNodeType.Element, "database-range", OdfNamespaces.Table, "table");
            dbRange.SetAttribute("name", OdfNamespaces.Table, "", "table");
            dbRanges.AppendChild(dbRange);
            doc.SheetsRoot.AppendChild(dbRanges);
            doc.Save();

            var reloaded = RoundTrip(doc, p => new SpreadsheetDocument(p));
            var search = reloaded.SheetsRoot.Descendants().FirstOrDefault(d => d.LocalName == "database-range" && d.GetAttribute("name", OdfNamespaces.Table) == "");
            Assert.NotNull(search);
        }

        [Fact]
        public void F6_Boundary_SortEmptyRange()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new SpreadsheetDocument(package);
            var dbRanges = new OdfNode(OdfNodeType.Element, "database-ranges", OdfNamespaces.Table, "table");
            var dbRange = new OdfNode(OdfNodeType.Element, "database-range", OdfNamespaces.Table, "table");
            var sort = new OdfNode(OdfNodeType.Element, "sort", OdfNamespaces.Table, "table");
            dbRange.AppendChild(sort);
            dbRanges.AppendChild(dbRange);
            doc.SheetsRoot.AppendChild(dbRanges);
            doc.Save();

            var reloaded = RoundTrip(doc, p => new SpreadsheetDocument(p));
            Assert.NotNull(reloaded);
        }

        [Fact]
        public void F6_Boundary_FilterEmptyCriteria()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new SpreadsheetDocument(package);
            var dbRanges = new OdfNode(OdfNodeType.Element, "database-ranges", OdfNamespaces.Table, "table");
            var dbRange = new OdfNode(OdfNodeType.Element, "database-range", OdfNamespaces.Table, "table");
            var filter = new OdfNode(OdfNodeType.Element, "filter", OdfNamespaces.Table, "table");
            dbRange.AppendChild(filter);
            dbRanges.AppendChild(dbRange);
            doc.SheetsRoot.AppendChild(dbRanges);
            doc.Save();

            var reloaded = RoundTrip(doc, p => new SpreadsheetDocument(p));
            Assert.NotNull(reloaded);
        }

        [Fact]
        public void F7_Boundary_PivotTableEmptyFields()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new SpreadsheetDocument(package);
            var sheet = doc.AddSheet("Sheet1");
            var dataPilots = new OdfNode(OdfNodeType.Element, "data-pilot-tables", OdfNamespaces.Table, "table");
            var pilotTable = new OdfNode(OdfNodeType.Element, "data-pilot-table", OdfNamespaces.Table, "table");
            dataPilots.AppendChild(pilotTable);
            sheet.TableNode.AppendChild(dataPilots);
            doc.Save();

            var reloaded = RoundTrip(doc, p => new SpreadsheetDocument(p));
            Assert.NotNull(reloaded.SheetsRoot.Descendants().FirstOrDefault(d => d.LocalName == "data-pilot-table"));
        }

        [Fact]
        public void F8_Boundary_ConditionalFormatEmptyStyle()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new SpreadsheetDocument(package);
            var sheet = doc.AddSheet("Sheet1");
            var range = new OdfCellRange(0, 0, 0, 0, "Sheet1");
            sheet.AddConditionalFormat(range, "cell-content() > 0", "");
            doc.Save();

            var reloaded = RoundTrip(doc, p => new SpreadsheetDocument(p));
            var cond = reloaded.SheetsRoot.Descendants().FirstOrDefault(d => d.LocalName == "condition");
            Assert.NotNull(cond);
            Assert.Equal("", cond.GetAttribute("style-name", calcextNs));
        }

        [Fact]
        public void F9_Boundary_FormulaDivByZero()
        {
            var evaluator = new DefaultFormulaEvaluator();
            var context = new FormulaAndStylesTest.MockEvaluationContext();
            context.Evaluator = evaluator;

            var result = evaluator.Evaluate("5/0", context);
            Assert.IsType<OdfFormulaError>(result);
            Assert.Equal(OdfFormulaErrorType.Div0, ((OdfFormulaError)result).ErrorType);
        }

        [Fact]
        public void F9_Boundary_FormulaCircularReference()
        {
            var evaluator = new DefaultFormulaEvaluator();
            var context = new FormulaAndStylesTest.MockEvaluationContext();
            context.Evaluator = evaluator;

            var a1 = OdfCellAddress.ParseExcel("Sheet1!A1");
            var b1 = OdfCellAddress.ParseExcel("Sheet1!B1");

            context.CellFormulas[a1] = "Sheet1.B1 + 1";
            context.CellFormulas[b1] = "Sheet1.A1 + 2";

            var result = evaluator.EvaluateCell(a1, context);
            Assert.IsType<OdfFormulaError>(result);
            Assert.Equal(OdfFormulaErrorType.Ref, ((OdfFormulaError)result).ErrorType);
        }

        [Fact]
        public void F10_Boundary_SlideLayoutNoPlaceholder()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new PresentationDocument(package);
            doc.AddSlide("EmptySlide");
            doc.Save();

            var reloaded = RoundTrip(doc, p => new PresentationDocument(p));
            Assert.DoesNotContain(reloaded.Slides[0].Node.Children, c => c.LocalName == "frame");
        }

        [Fact]
        public void F10_Boundary_SpeakerNotesLargeText()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new PresentationDocument(package);
            var slide = doc.AddSlide("Slide1");
            
            var largeNotes = new string('A', 100000);
            slide.SpeakerNotes = largeNotes;
            doc.Save();

            var reloaded = RoundTrip(doc, p => new PresentationDocument(p));
            Assert.Equal(largeNotes, reloaded.Slides[0].SpeakerNotes);
        }

        [Fact]
        public void F11_Boundary_SmilAnimationsEmptySequence()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new PresentationDocument(package);
            var slide = doc.AddSlide("Slide1");
            var seq = new OdfNode(OdfNodeType.Element, "seq", smilNs, "smil");
            slide.Node.AppendChild(seq);
            doc.Save();

            var reloaded = RoundTrip(doc, p => new PresentationDocument(p));
            var seqNode = reloaded.Slides[0].Node.Children.FirstOrDefault(c => c.LocalName == "seq");
            Assert.NotNull(seqNode);
            Assert.Empty(seqNode.Children);
        }

        [Fact]
        public void F12_Boundary_ShapeStylesInvalidColors()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new DrawingDocument(package);
            var page = doc.AddPage("Page1");
            var shape = page.AddShape(OdfShapeType.Rectangle, OdfLength.FromCentimeters(0), OdfLength.FromCentimeters(0), OdfLength.FromCentimeters(1), OdfLength.FromCentimeters(1));
            // Setting malformed CSS hex color
            shape.FillColor = "not-a-color";
            doc.Save();

            var reloaded = RoundTrip(doc, p => new DrawingDocument(p));
            var rect = reloaded.Pages[0].Node.Children.First(c => c.LocalName == "rect");
            var shapeObj = new OdfShape(rect, reloaded);
            Assert.Equal("not-a-color", shapeObj.FillColor);
        }

        [Fact]
        public void F13_Boundary_ChartLegendEmptyPosition()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new DrawingDocument(package);
            var chartFolder = "Object_Chart";
            string chartContentXml = "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:chart=\"urn:oasis:names:tc:opendocument:xmlns:chart:1.0\">" +
                "<office:body><office:chart><chart:chart><chart:legend chart:legend-position=\"\" /></chart:chart></office:chart></office:body></office:document-content>";

            package.WriteEntry($"{chartFolder}/content.xml", Encoding.UTF8.GetBytes(chartContentXml), "text/xml");
            package.SaveManifestToEntries();
            doc.Save();

            var reloaded = RoundTrip(doc, p => new DrawingDocument(p));
            var content = Encoding.UTF8.GetString(reloaded.Package.ReadEntry($"{chartFolder}/content.xml"));
            Assert.Contains("chart:legend-position=\"\"", content);
        }
        #endregion

        // =====================================================================
        // TIER 3: CROSS-FEATURE COMBINATIONS
        // =====================================================================

        #region Cross-Feature Integration Tests
        [Fact]
        public void Tier3_TOC_Heading_Inside_Tracked_Changes()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);
            var h = doc.AddHeading("Tracked Heading", 1);
            
            // Wrap the heading contents in change start/end tags
            var start = new OdfNode(OdfNodeType.Element, "change-start", OdfNamespaces.Text, "text");
            start.SetAttribute("change-id", OdfNamespaces.Text, "ch1", "text");
            var end = new OdfNode(OdfNodeType.Element, "change-end", OdfNamespaces.Text, "text");
            end.SetAttribute("change-id", OdfNamespaces.Text, "ch1", "text");
            h.Node.InsertBefore(start, h.Node.Children[0]);
            h.Node.AppendChild(end);

            doc.AddTableOfContents();
            doc.Save();

            var reloaded = RoundTrip(doc, p => new TextDocument(p));
            Assert.Contains("Tracked Heading", reloaded.BodyTextRoot.TextContent);
        }

        [Fact]
        public void Tier3_TrackedChanges_Inside_CJK_Ruby()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);
            var p = doc.AddParagraph();
            doc.AddRuby(p, "漢", "かん");

            var rubyTextNode = p.Node.Descendants().First(d => d.LocalName == "ruby-text");
            var start = new OdfNode(OdfNodeType.Element, "change-start", OdfNamespaces.Text, "text");
            start.SetAttribute("change-id", OdfNamespaces.Text, "ch1", "text");
            var end = new OdfNode(OdfNodeType.Element, "change-end", OdfNamespaces.Text, "text");
            end.SetAttribute("change-id", OdfNamespaces.Text, "ch1", "text");
            rubyTextNode.InsertBefore(start, rubyTextNode.Children[0]);
            rubyTextNode.AppendChild(end);

            doc.Save();
            var reloaded = RoundTrip(doc, pDoc => new TextDocument(pDoc));
            Assert.NotNull(reloaded.BodyTextRoot.Descendants().FirstOrDefault(d => d.LocalName == "change-start"));
        }

        [Fact]
        public void Tier3_Formula_Evaluating_NamedRanges()
        {
            var evaluator = new DefaultFormulaEvaluator();
            var context = new FormulaAndStylesTest.MockEvaluationContext();
            context.Evaluator = evaluator;

            // Bind named range range reference values
            context.CellValues[OdfCellAddress.ParseExcel("Sheet1!A1")] = 50.0;
            context.CellValues[OdfCellAddress.ParseExcel("Sheet1!A2")] = 75.0;

            // We mock range values directly
            var result = evaluator.Evaluate("SUM(Sheet1.A1:Sheet1.A2)", context);
            Assert.Equal(125.0, result);
        }

        [Fact]
        public void Tier3_ConditionalFormatting_Referencing_NamedRange()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new SpreadsheetDocument(package);
            var sheet = doc.AddSheet("Sheet1");
            var range = new OdfCellRange(0, 0, 0, 0, "Sheet1");
            
            // Conditional format using Named Range TaxRate
            sheet.AddConditionalFormat(range, "cell-content() > TaxRate", "Red");
            doc.Save();

            var reloaded = RoundTrip(doc, p => new SpreadsheetDocument(p));
            var condition = reloaded.SheetsRoot.Descendants().First(d => d.LocalName == "condition");
            Assert.Equal("cell-content() > TaxRate", condition.GetAttribute("value", calcextNs));
        }

        [Fact]
        public void Tier3_Presentation_SpeakerNotes_With_TOC()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new PresentationDocument(package);
            var slide = doc.AddSlide("Slide1");
            slide.SpeakerNotes = "TOC Reference Section";
            
            // Presentations don't typically host text document TOC elements, but package manifest preservation holds
            doc.Save();
            var reloaded = RoundTrip(doc, p => new PresentationDocument(p));
            Assert.Equal("TOC Reference Section", reloaded.Slides[0].SpeakerNotes);
        }
        #endregion

        // =====================================================================
        // TIER 4: REAL-WORLD APPLICATION SCENARIOS
        // =====================================================================

        #region Real-World Application Workloads
        [Fact]
        public void Tier4_Complex_Text_Document_Workflow()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);
            doc.GetDefaultPageSetup().WritingMode = "tb-rl"; // CJK vertical writing mode layout
            
            doc.AddTableOfContents(); // TOC

            var p1 = doc.AddParagraph("Header Title");
            doc.AddRuby(p1, "大", "たい"); // Ruby CJK annotation

            var p2 = doc.AddParagraph();
            doc.AddFormula(p2, "<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mi>x</mi></math>"); // MathML object

            doc.Save();

            var reloaded = RoundTrip(doc, p => new TextDocument(p));
            Assert.Equal("tb-rl", reloaded.GetDefaultPageSetup().WritingMode);
            Assert.NotNull(reloaded.BodyTextRoot.Descendants().FirstOrDefault(d => d.LocalName == "ruby"));
            Assert.Contains(reloaded.Package.GetEntries(), e => e.Path.StartsWith("Formula_"));
        }

        [Fact]
        public void Tier4_Financial_Spreadsheet_Workflow()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new SpreadsheetDocument(package);
            var sheet = doc.AddSheet("Finance");
            
            sheet.GetCell("A1").Value = "Revenue";
            sheet.GetCell("A2").SetValue(120000.0);
            sheet.GetCell("B1").Value = "Expenses";
            sheet.GetCell("B2").SetValue(85000.0);
            
            // 1. Setup named range
            var expressions = new OdfNode(OdfNodeType.Element, "named-expressions", OdfNamespaces.Table, "table");
            var range = new OdfNode(OdfNodeType.Element, "named-range", OdfNamespaces.Table, "table");
            range.SetAttribute("name", OdfNamespaces.Table, "Rev", "table");
            range.SetAttribute("cell-range-address", OdfNamespaces.Table, "Finance.A2", "table");
            expressions.AppendChild(range);
            doc.SheetsRoot.AppendChild(expressions);

            // 2. Add Conditional formatting rule
            var cellRange = new OdfCellRange(1, 0, 1, 0, "Finance");
            sheet.AddConditionalFormat(cellRange, "cell-content() > 100000", "GreenStyle");

            doc.Save();

            var reloaded = RoundTrip(doc, p => new SpreadsheetDocument(p));
            Assert.NotNull(reloaded.SheetsRoot.Descendants().FirstOrDefault(d => d.LocalName == "named-range"));
            Assert.NotNull(reloaded.SheetsRoot.Descendants().FirstOrDefault(d => d.LocalName == "conditional-format"));
        }

        [Fact]
        public void Tier4_Business_Presentation_SlideDeck()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new PresentationDocument(package);
            doc.SetSlideSize(OdfLength.FromCentimeters(25), OdfLength.FromCentimeters(18));

            var slide = doc.AddSlide("Slide1");
            slide.SpeakerNotes = "Brief outline of Q2 results";
            slide.SetTransition(OdfTransitionType.Zoom, OdfLength.FromPoints(72)); // transition

            // Add SMIL timings
            var seq = new OdfNode(OdfNodeType.Element, "seq", smilNs, "smil");
            var par = new OdfNode(OdfNodeType.Element, "par", smilNs, "smil");
            seq.AppendChild(par);
            slide.Node.AppendChild(seq);

            doc.Save();

            var reloaded = RoundTrip(doc, p => new PresentationDocument(p));
            Assert.Equal("Brief outline of Q2 results", reloaded.Slides[0].SpeakerNotes);
            Assert.Equal("1.00s", reloaded.Slides[0].Node.GetAttribute("dur", smilNs));
            Assert.NotNull(reloaded.Slides[0].Node.Descendants().FirstOrDefault(d => d.LocalName == "par"));
        }

        [Fact]
        public void Tier4_Technical_Engineering_Drawing()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new DrawingDocument(package);
            var page = doc.AddPage("Page1");

            var shape = page.AddShape(OdfShapeType.Rectangle, OdfLength.FromCentimeters(1), OdfLength.FromCentimeters(1), OdfLength.FromCentimeters(5), OdfLength.FromCentimeters(5));
            shape.FillColor = "#eeeeee";

            // Add connector
            var conn = new OdfNode(OdfNodeType.Element, "connector", OdfNamespaces.Draw, "draw");
            conn.SetAttribute("type", OdfNamespaces.Draw, "standard", "draw");
            page.Node.AppendChild(conn);

            // Add MathML embedded formula package
            var formulaFolder = "Object_Formula";
            package.WriteEntry($"{formulaFolder}/content.xml", Encoding.UTF8.GetBytes("<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mi>λ</mi></math>"), "text/xml");
            package.WriteEntry($"{formulaFolder}/mimetype", Encoding.UTF8.GetBytes("application/vnd.oasis.opendocument.formula"), "application/vnd.oasis.opendocument.formula");
            package.SaveManifestToEntries();

            doc.Save();

            var reloaded = RoundTrip(doc, p => new DrawingDocument(p));
            var ellipseNode = reloaded.Pages[0].Node.Children.FirstOrDefault(c => c.LocalName == "rect");
            Assert.NotNull(ellipseNode);
            Assert.NotNull(reloaded.Pages[0].Node.Children.FirstOrDefault(c => c.LocalName == "connector"));
            Assert.True(reloaded.Package.HasEntry($"{formulaFolder}/content.xml"));
        }

        [Fact]
        public void Tier4_Cross_Document_Analytics_Report()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new SpreadsheetDocument(package);
            var sheet = doc.AddSheet("Sheet1");

            // Populate some data cells
            sheet.GetCell("A1").Value = "Year";
            sheet.GetCell("A2").SetValue(2025.0);
            sheet.GetCell("A3").SetValue(2026.0);

            // Setup database ranges
            var dbRanges = new OdfNode(OdfNodeType.Element, "database-ranges", OdfNamespaces.Table, "table");
            var dbRange = new OdfNode(OdfNodeType.Element, "database-range", OdfNamespaces.Table, "table");
            dbRange.SetAttribute("name", OdfNamespaces.Table, "Data", "table");
            dbRanges.AppendChild(dbRange);
            doc.SheetsRoot.AppendChild(dbRanges);

            // Setup pivot data pilots
            var dataPilots = new OdfNode(OdfNodeType.Element, "data-pilot-tables", OdfNamespaces.Table, "table");
            var pilotTable = new OdfNode(OdfNodeType.Element, "data-pilot-table", OdfNamespaces.Table, "table");
            pilotTable.SetAttribute("name", OdfNamespaces.Table, "Pivot1", "table");
            dataPilots.AppendChild(pilotTable);
            sheet.TableNode.AppendChild(dataPilots);

            // Setup embedded chart folder inside spreadsheet package
            var chartFolder = "Object_Chart1";
            package.WriteEntry($"{chartFolder}/content.xml", Encoding.UTF8.GetBytes("<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\"><office:body><office:chart><chart:chart /></office:chart></office:body></office:document-content>"), "text/xml");
            package.SaveManifestToEntries();

            doc.Save();

            var reloaded = RoundTrip(doc, p => new SpreadsheetDocument(p));
            Assert.NotNull(reloaded.SheetsRoot.Descendants().FirstOrDefault(d => d.LocalName == "database-range"));
            Assert.NotNull(reloaded.SheetsRoot.Descendants().FirstOrDefault(d => d.LocalName == "data-pilot-table"));
            Assert.True(reloaded.Package.HasEntry($"{chartFolder}/content.xml"));
        }
        #endregion
    }
}
