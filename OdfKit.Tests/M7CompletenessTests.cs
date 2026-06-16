using System;
using System.IO;
using System.Security;
using System.Text;
using Xunit;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Text;
using OdfKit.Styles;

namespace OdfKit.Tests
{
    public class M7CompletenessTests
    {
        [Fact]
        public void TestFlatXmlMathMLPreservationAndZipSlip()
        {
            // 1. MathML preservation in Flat XML
            string flatXmlTemplate = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><office:document xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" office:mimetype=\"application/vnd.oasis.opendocument.text\" office:version=\"1.3\"><office:body><office:text/></office:body></office:document>";
            
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(flatXmlTemplate));
            using (var package = OdfPackage.Open(ms, leaveOpen: true))
            {
                var doc = new TextDocument(package);
                var p = doc.AddParagraph("Formula paragraph:");
                
                string mathMl = "<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mi>y</mi><mo>=</mo><mi>x</mi></math>";
                p.AddFormula(mathMl);
                using var saveMs = new MemoryStream();
                doc.SaveToStream(saveMs);
                
                // Read back saved Flat XML
                saveMs.Position = 0;
                using (var loadPackage = OdfPackage.Open(saveMs, leaveOpen: true))
                {
                    Assert.True(loadPackage.IsFlatXml);
                    var loadDoc = new TextDocument(loadPackage);
                    
                    // Find formula object
                    OdfNode? formulaObjNode = null;
                    foreach (var child in loadDoc.BodyTextRoot.Children)
                    {
                        if (child.LocalName == "p")
                        {
                            foreach (var inner in child.Children)
                            {
                                if (inner.LocalName == "frame")
                                {
                                    foreach (var fChild in inner.Children)
                                    {
                                        if (fChild.LocalName == "object")
                                        {
                                            formulaObjNode = fChild;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    Assert.NotNull(formulaObjNode);
                    
                    var formulaObj = new OdfFormulaObject(formulaObjNode.Parent!, formulaObjNode, loadDoc);
                    string loadedMathMl = formulaObj.MathMlXmlString;
                    
                    string expected = mathMl.Replace(" ", "").Replace("\r", "").Replace("\n", "").Replace("\t", "");
                    string actual = loadedMathMl.Replace(" ", "").Replace("\r", "").Replace("\n", "").Replace("\t", "");
                    Assert.Equal(expected, actual);
                }
            }

            // 2. Malformed MathML should throw ArgumentException
            using (var package = OdfPackage.Create(new MemoryStream()))
            {
                var doc = new TextDocument(package);
                var p = doc.AddParagraph();
                Assert.Throws<ArgumentException>(() => p.AddFormula("<math><mi>x</mi></unclosed>"));
            }

            // 3. Zip Slip Defense
            using (var package = OdfPackage.Create(new MemoryStream()))
            {
                var doc = new TextDocument(package);
                var frame = new OdfNode(OdfNodeType.Element, "frame", OdfNamespaces.Draw, "draw");
                var obj = new OdfNode(OdfNodeType.Element, "object", OdfNamespaces.Draw, "draw");
                var formulaObj = new OdfFormulaObject(frame, obj, doc);
                
                Assert.Throws<InvalidOperationException>(() => formulaObj.FormulaFolder = "../Traversal");
                Assert.Throws<InvalidOperationException>(() => formulaObj.FormulaFolder = "/Absolute");
                Assert.Throws<InvalidOperationException>(() => formulaObj.FormulaFolder = @"\\UNC\path");
            }
        }

        [Fact]
        public void TestTrackedRevisionsAPI()
        {
            using (var package = OdfPackage.Create(new MemoryStream()))
            {
                var doc = new TextDocument(package);
                doc.TrackedChanges = true;

                // 1. Insertion tracking
                var p = doc.AddParagraph("Initial Text");
                var run = p.AddTextRun("Formatted Run");
                
                // 2. Format tracking via direct style-name assignment
                run.StyleName = "T1";
                p.StyleName = "P1";

                // 3. Deletion tracking
                doc.DeleteNode(run.Node);

                // Verify changes exist in tracked-changes metadata
                OdfNode? tcNode = null;
                foreach (var child in doc.BodyTextRoot.Children)
                {
                    if (child.LocalName == "tracked-changes")
                    {
                        tcNode = child;
                    }
                }
                Assert.NotNull(tcNode);
            }
        }

        [Fact]
        public void TestAcceptRejectChanges()
        {
            // Test individual Accept and Reject
            using (var package = OdfPackage.Create(new MemoryStream()))
            {
                var doc = new TextDocument(package);
                doc.TrackedChanges = true;

                // Format change test
                var p = doc.AddParagraph("Format Test");
                p.StyleName = "OriginalStyle";
                
                // Verify original style name was registered
                p.StyleName = "NewStyle"; // triggers format-change revision

                OdfNode? tcNode = null;
                foreach (var child in doc.BodyTextRoot.Children)
                {
                    if (child.LocalName == "tracked-changes") tcNode = child;
                }
                Assert.NotNull(tcNode);
                
                string? changeId = null;
                foreach (var region in tcNode.Children)
                {
                    changeId = region.GetAttribute("id", OdfNamespaces.Text);
                }
                Assert.NotNull(changeId);

                // Reject the change -> style name should revert to OriginalStyle
                doc.RejectChange(changeId);
                Assert.Equal("OriginalStyle", p.StyleName);

                // Now test Accept Change
                doc.TrackedChanges = true;
                p.StyleName = "NewStyle2";
                
                tcNode = null;
                foreach (var child in doc.BodyTextRoot.Children)
                {
                    if (child.LocalName == "tracked-changes") tcNode = child;
                }
                Assert.NotNull(tcNode);

                changeId = null;
                foreach (var region in tcNode.Children)
                {
                    changeId = region.GetAttribute("id", OdfNamespaces.Text);
                }
                Assert.NotNull(changeId);

                doc.AcceptChange(changeId);
                Assert.Equal("NewStyle2", p.StyleName);
            }

            // Test batch AcceptAllTrackedChanges and RejectAllTrackedChanges
            using (var package = OdfPackage.Create(new MemoryStream()))
            {
                var doc = new TextDocument(package);
                
                // Add p1 while TrackedChanges is false, so it's not marked for insertion purging
                doc.TrackedChanges = false;
                var p1 = doc.AddParagraph("Para 1");
                p1.StyleName = "StyleA";

                // Turn on revision tracking
                doc.TrackedChanges = true;
                p1.StyleName = "StyleB"; // format revision (tracked)

                var p2 = doc.AddParagraph("Para 2"); // insertion revision (tracked)

                doc.RejectAllTrackedChanges();

                // p2 should be purged because its insertion was rejected
                bool p2Exists = false;
                foreach (var child in doc.BodyTextRoot.Children)
                {
                    if (child.TextContent == "Para 2") p2Exists = true;
                }
                Assert.False(p2Exists);

                // p1's style name should revert to StyleA
                Assert.Equal("StyleA", p1.StyleName);
            }
        }

        [Fact]
        public void TestCjkLayoutAndFonts()
        {
            using (var package = OdfPackage.Create(new MemoryStream()))
            {
                var doc = new TextDocument(package);

                // 1. Ruby Layout
                var p = doc.AddParagraph();
                var ruby = p.AddRuby("漢字", "かんじ");
                Assert.NotNull(ruby);
                Assert.NotNull(ruby.RubyBaseNode);
                Assert.NotNull(ruby.RubyTextNode);
                Assert.Equal("漢字", ruby.RubyBaseNode.TextContent);
                Assert.Equal("かんじ", ruby.RubyTextNode.TextContent);

                ruby.RubyPosition = "above";
                ruby.RubyAlign = "distribute-letter";
                ruby.RubyTextStyleName = "RubyTextCharStyle";
                ruby.RubyBaseStyleName = "RubyBaseCharStyle";

                Assert.Equal("above", ruby.RubyPosition);
                Assert.Equal("distribute-letter", ruby.RubyAlign);
                Assert.Equal("RubyTextCharStyle", ruby.RubyTextStyleName);
                Assert.Equal("RubyBaseCharStyle", ruby.RubyBaseStyleName);

                // 2. Writing Mode & Page Layout Grid
                var pageSetup = doc.GetDefaultPageSetup();
                pageSetup.WritingMode = OdfWritingMode.TbRl;
                pageSetup.LayoutGridMode = OdfLayoutGridMode.Both;
                pageSetup.LayoutGridBaseHeight = "0.5cm";
                pageSetup.LayoutGridBaseWidth = "0.5cm";
                pageSetup.LayoutGridRubyHeight = "0.2cm";
                pageSetup.LayoutGridLines = 20;
                pageSetup.LayoutGridCharacters = 20;
                pageSetup.LayoutGridDisplay = true;
                pageSetup.LayoutGridPrint = true;

                Assert.Equal(OdfWritingMode.TbRl, pageSetup.WritingMode);
                Assert.Equal(OdfLayoutGridMode.Both, pageSetup.LayoutGridMode);
                Assert.Equal("0.5cm", pageSetup.LayoutGridBaseHeight);
                Assert.Equal("0.5cm", pageSetup.LayoutGridBaseWidth);
                Assert.Equal("0.2cm", pageSetup.LayoutGridRubyHeight);
                Assert.Equal(20, pageSetup.LayoutGridLines);
                Assert.Equal(20, pageSetup.LayoutGridCharacters);
                Assert.True(pageSetup.LayoutGridDisplay);
                Assert.True(pageSetup.LayoutGridPrint);

                // 3. CJK Fonts Fallback & Font Face declarations
                doc.ApplyCjkFontFallback();
                
                var run = p.AddTextRun("CJK Text");
                run.SetFont("Arial", "Microsoft JhengHei", "Microsoft JhengHei");
                run.SetFontSize("12pt", "14pt", "14pt");

                Assert.Equal("Arial", run.FontName);
                Assert.Equal("Microsoft JhengHei", run.FontNameAsian);
                Assert.Equal("Microsoft JhengHei", run.FontNameComplex);
                Assert.Equal("12pt", run.FontSize);
                Assert.Equal("14pt", run.FontSizeAsian);
                Assert.Equal("14pt", run.FontSizeComplex);

                p.SetFont("Times New Roman", "PMingLiU", "PMingLiU");
                p.SetFontSize("10pt", "12pt", "12pt");

                Assert.Equal("Times New Roman", p.FontName);
                Assert.Equal("PMingLiU", p.FontNameAsian);
                Assert.Equal("PMingLiU", p.FontNameComplex);
                Assert.Equal("10pt", p.FontSize);
                Assert.Equal("12pt", p.FontSizeAsian);
                Assert.Equal("12pt", p.FontSizeComplex);
            }
        }

        [Fact]
        public void TestChartDocumentMerge()
        {
            using var ms1 = new MemoryStream();
            using var ms2 = new MemoryStream();

            using (var pkg1 = OdfPackage.Create(ms1, leaveOpen: true))
            {
                var doc1 = new OdfKit.Chart.OdfChartDocument(pkg1);
                doc1.Save();
            }
            using (var pkg2 = OdfPackage.Create(ms2, leaveOpen: true))
            {
                var doc2 = new OdfKit.Chart.OdfChartDocument(pkg2);
                doc2.Save();
            }

            ms1.Position = 0;
            ms2.Position = 0;

            using var src = new OdfKit.Chart.OdfChartDocument(OdfPackage.Open(ms1));
            using var dest = new OdfKit.Chart.OdfChartDocument(OdfPackage.Open(ms2));

            dest.AppendDocument(src, OdfMergeOptions.Default);

            OdfNode? bodyNode = null;
            foreach (var child in dest.ContentDom.Children)
            {
                if (child.LocalName == "body" && child.NamespaceUri == OdfNamespaces.Office)
                {
                    bodyNode = child;
                    break;
                }
            }
            Assert.NotNull(bodyNode);
            
            OdfNode? chartRoot = null;
            foreach (var child in bodyNode.Children)
            {
                if (child.LocalName == "chart" && child.NamespaceUri == OdfNamespaces.Office)
                {
                    chartRoot = child;
                    break;
                }
            }
            Assert.NotNull(chartRoot);
            
            int chartCount = 0;
            foreach (var child in chartRoot.Children)
            {
                if (child.LocalName == "chart" && child.NamespaceUri == OdfNamespaces.Chart)
                {
                    chartCount++;
                }
            }
            Assert.Equal(2, chartCount);
        }

        [Fact]
        public void TestFormulaDocumentMerge()
        {
            using var ms1 = new MemoryStream();
            using var ms2 = new MemoryStream();

            using (var pkg1 = OdfPackage.Create(ms1, leaveOpen: true))
            {
                var doc1 = new OdfKit.Formula.OdfFormulaDocument(pkg1);
                doc1.Save();
            }
            using (var pkg2 = OdfPackage.Create(ms2, leaveOpen: true))
            {
                var doc2 = new OdfKit.Formula.OdfFormulaDocument(pkg2);
                doc2.Save();
            }

            ms1.Position = 0;
            ms2.Position = 0;

            using var src = new OdfKit.Formula.OdfFormulaDocument(OdfPackage.Open(ms1));
            using var dest = new OdfKit.Formula.OdfFormulaDocument(OdfPackage.Open(ms2));

            dest.AppendDocument(src, OdfMergeOptions.Default);

            OdfNode? bodyNode = null;
            foreach (var child in dest.ContentDom.Children)
            {
                if (child.LocalName == "body" && child.NamespaceUri == OdfNamespaces.Office)
                {
                    bodyNode = child;
                    break;
                }
            }
            Assert.NotNull(bodyNode);
            
            OdfNode? formulaRoot = null;
            foreach (var child in bodyNode.Children)
            {
                if (child.LocalName == "formula" && child.NamespaceUri == OdfNamespaces.Office)
                {
                    formulaRoot = child;
                    break;
                }
            }
            Assert.NotNull(formulaRoot);
            
            int mathCount = 0;
            foreach (var child in formulaRoot.Children)
            {
                if (child.LocalName == "math" && child.NamespaceUri == "http://www.w3.org/1998/Math/MathML")
                {
                    mathCount++;
                }
            }
            Assert.Equal(2, mathCount);
        }
    }
}
