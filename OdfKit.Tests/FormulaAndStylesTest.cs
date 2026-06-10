using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using Xunit;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Spreadsheet;
using OdfKit.Formula;
using OdfKit.Styles;

namespace OdfKit.Tests
{
    public class FormulaAndStylesTest
    {
        #region Mock IEvaluationContext

        private class MockEvaluationContext : IEvaluationContext
        {
            public OdfCellAddress CurrentCell { get; set; }
            public Dictionary<OdfCellAddress, object> CellValues { get; } = new();
            public Dictionary<OdfCellAddress, string> CellFormulas { get; } = new();
            public DefaultFormulaEvaluator? Evaluator { get; set; }

            public object GetCellValue(OdfCellAddress address)
            {
                // If there's an uncalculated formula, trigger its evaluation.
                if (CellFormulas.TryGetValue(address, out var formula))
                {
                    if (Evaluator != null)
                    {
                        return Evaluator.EvaluateCell(address, this);
                    }
                }
                if (CellValues.TryGetValue(address, out var val)) return val;
                return 0.0;
            }

            public object[,] GetRangeValues(OdfCellRange range)
            {
                int minRow = Math.Min(range.StartAddress.Row, range.EndAddress.Row);
                int maxRow = Math.Max(range.StartAddress.Row, range.EndAddress.Row);
                int minCol = Math.Min(range.StartAddress.Column, range.EndAddress.Column);
                int maxCol = Math.Max(range.StartAddress.Column, range.EndAddress.Column);

                int rows = maxRow - minRow + 1;
                int cols = maxCol - minCol + 1;
                var arr = new object[rows, cols];

                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        // Propagate sheet name to lookup if necessary
                        var addr = new OdfCellAddress(minRow + r, minCol + c, range.StartAddress.SheetName);
                        arr[r, c] = GetCellValue(addr);
                    }
                }
                return arr;
            }

            public string? GetCellFormula(OdfCellAddress address)
            {
                return CellFormulas.TryGetValue(address, out var formula) ? formula : null;
            }
        }

        #endregion

        #region Scenario 15: OdfCellAddress & OdfCellRange Tests

        [Fact]
        public void TestCellAddressParsingAndBiDirectionalFormatting()
        {
            // 1. Parse Excel Style
            var a1 = OdfCellAddress.ParseExcel("A1");
            Assert.Equal(0, a1.Row);
            Assert.Equal(0, a1.Column);
            Assert.Null(a1.SheetName);
            Assert.False(a1.IsRowAbsolute);
            Assert.False(a1.IsColumnAbsolute);

            var absA1 = OdfCellAddress.ParseExcel("$A$1");
            Assert.Equal(0, absA1.Row);
            Assert.Equal(0, absA1.Column);
            Assert.True(absA1.IsRowAbsolute);
            Assert.True(absA1.IsColumnAbsolute);

            var sheetA1 = OdfCellAddress.ParseExcel("Sheet1!B5");
            Assert.Equal(4, sheetA1.Row);
            Assert.Equal(1, sheetA1.Column);
            Assert.Equal("Sheet1", sheetA1.SheetName);
            Assert.False(sheetA1.IsRowAbsolute);
            Assert.False(sheetA1.IsColumnAbsolute);
            Assert.False(sheetA1.IsSheetAbsolute);

            var quotedSheet = OdfCellAddress.ParseExcel("'My Sheet'!$C$10");
            Assert.Equal(9, quotedSheet.Row);
            Assert.Equal(2, quotedSheet.Column);
            Assert.Equal("My Sheet", quotedSheet.SheetName);
            Assert.True(quotedSheet.IsRowAbsolute);
            Assert.True(quotedSheet.IsColumnAbsolute);

            // 2. Parse ODF Style
            var odfLocal = OdfCellAddress.ParseOdf(".A1");
            Assert.Equal(0, odfLocal.Row);
            Assert.Equal(0, odfLocal.Column);
            Assert.Null(odfLocal.SheetName);

            var odfSheet = OdfCellAddress.ParseOdf("Sheet1.B2");
            Assert.Equal(1, odfSheet.Row);
            Assert.Equal(1, odfSheet.Column);
            Assert.Equal("Sheet1", odfSheet.SheetName);

            var odfQuoted = OdfCellAddress.ParseOdf("'My Sheet'.C3");
            Assert.Equal(2, odfQuoted.Row);
            Assert.Equal(2, odfQuoted.Column);
            Assert.Equal("My Sheet", odfQuoted.SheetName);

            // 3. Bi-directional formatting
            Assert.Equal("A1", a1.ToExcelString());
            Assert.Equal(".A1", a1.ToOdfString(false));
            Assert.Equal("[.A1]", a1.ToOdfString(true));

            Assert.Equal("$A$1", absA1.ToExcelString());
            Assert.Equal(".$A$1", absA1.ToOdfString(false));

            Assert.Equal("Sheet1!B5", sheetA1.ToExcelString());
            Assert.Equal("Sheet1.B5", sheetA1.ToOdfString(false));

            Assert.Equal("'My Sheet'!$C$10", quotedSheet.ToExcelString());
            Assert.Equal("'My Sheet'.$C$10", quotedSheet.ToOdfString(false));
        }

        [Fact]
        public void TestRangeOperationsAndStructuralShifting()
        {
            // Range Parsing
            var range1 = OdfCellRange.ParseExcel("A1:C3");
            Assert.Equal(0, range1.StartAddress.Row);
            Assert.Equal(0, range1.StartAddress.Column);
            Assert.Equal(2, range1.EndAddress.Row);
            Assert.Equal(2, range1.EndAddress.Column);

            // Sheet propagation
            var range2 = OdfCellRange.ParseExcel("Sheet1!A1:B2");
            Assert.Equal("Sheet1", range2.StartAddress.SheetName);
            Assert.Equal("Sheet1", range2.EndAddress.SheetName);

            // Containment and Intersection
            var inside = OdfCellAddress.ParseExcel("Sheet1!B2");
            var outside = OdfCellAddress.ParseExcel("Sheet1!D4");
            var wrongSheet = OdfCellAddress.ParseExcel("Sheet2!B2");

            Assert.True(range2.Contains(inside));
            Assert.False(range2.Contains(outside));
            Assert.False(range2.Contains(wrongSheet));

            var overlappingRange = OdfCellRange.ParseExcel("Sheet1!B2:C4");
            var separateRange = OdfCellRange.ParseExcel("Sheet1!D4:E5");
            Assert.True(range2.Intersects(overlappingRange));
            Assert.False(range2.Intersects(separateRange));

            // Shifting
            // Inserting 1 row at index 0 and 1 column at index 0
            var shiftedRange = range1.ShiftStructural(0, 1, 0, 1);
            Assert.Equal("B2:D4", shiftedRange.ToExcelString());

            // Deleting 1 row at index 0 and 1 col at index 0
            var shiftedBack = shiftedRange.ShiftStructural(0, -1, 0, -1);
            Assert.Equal("A1:C3", shiftedBack.ToExcelString());
        }

        #endregion

        #region OdfFormulaTranslator Tests

        [Fact]
        public void TestDualFormulaTranslation()
        {
            // 1. Excel to ODF Formula
            string excel = "=SUM(A1:B2)";
            string odf = OdfFormulaTranslator.ExcelToOdfFormula(excel);
            Assert.Equal("oooc:=SUM([.A1:.B2])", odf);

            excel = "=IF(A1>0, 1, 0)";
            odf = OdfFormulaTranslator.ExcelToOdfFormula(excel);
            Assert.Equal("oooc:=IF([.A1]>0; 1; 0)", odf);

            excel = "=AVERAGE(Sheet1!$A$1:$B$10)";
            odf = OdfFormulaTranslator.ExcelToOdfFormula(excel);
            Assert.Equal("oooc:=AVERAGE([Sheet1.$A$1:.$B$10])", odf);

            // 2. ODF to Excel Formula
            string backExcel = OdfFormulaTranslator.OdfToExcelFormula("oooc:=SUM([.A1:.B2])");
            Assert.Equal("=SUM(A1:B2)", backExcel);

            backExcel = OdfFormulaTranslator.OdfToExcelFormula("oooc:=IF([.A1]>0; 1; 0)");
            Assert.Equal("=IF(A1>0, 1, 0)", backExcel);

            backExcel = OdfFormulaTranslator.OdfToExcelFormula("oooc:=AVERAGE([Sheet1.$A$1:.$B$10])");
            Assert.Equal("=AVERAGE(Sheet1!$A$1:$B$10)", backExcel);
        }

        [Fact]
        public void TestFormulaOffsetTranslation()
        {
            // Excel style shifts
            string original = "=A1+B2";
            string shifted = OdfFormulaTranslator.TranslateFormulaOffset(original, 1, 2);
            Assert.Equal("=C2+D3", shifted);

            // Keep absolute reference unchanged
            original = "=A1+$B$2";
            shifted = OdfFormulaTranslator.TranslateFormulaOffset(original, 1, 2);
            Assert.Equal("=C2+$B$2", shifted);

            // Out-of-bounds shifting should produce #REF!
            original = "=A1";
            shifted = OdfFormulaTranslator.TranslateFormulaOffset(original, -1, -1);
            Assert.Equal("=#REF!", shifted);

            // ODF style shifts
            original = "oooc:=SUM([.A1:.B2])";
            shifted = OdfFormulaTranslator.TranslateFormulaOffset(original, 2, 1);
            Assert.Equal("oooc:=SUM([.B3:.C4])", shifted);
        }

        #endregion

        #region DefaultFormulaEvaluator Tests

        [Fact]
        public void TestFormulaEvaluatorMathAndLogical()
        {
            var context = new MockEvaluationContext();
            var evaluator = new DefaultFormulaEvaluator();
            context.Evaluator = evaluator;

            // Setup basic cell values
            context.CellValues[OdfCellAddress.ParseExcel("A1")] = 5.0;
            context.CellValues[OdfCellAddress.ParseExcel("B1")] = 10.0;
            context.CellValues[OdfCellAddress.ParseExcel("C1")] = 2.0;

            // Math operators with precedence
            var result = evaluator.Evaluate("A1+B1*C1", context); // 5 + 10 * 2 = 25
            Assert.Equal(25.0, result);

            result = evaluator.Evaluate("(A1+B1)*C1", context); // (5 + 10) * 2 = 30
            Assert.Equal(30.0, result);

            result = evaluator.Evaluate("B1/C1", context); // 10 / 2 = 5
            Assert.Equal(5.0, result);

            result = evaluator.Evaluate("B1^C1", context); // 10 ^ 2 = 100
            Assert.Equal(100.0, result);

            // Division by zero
            result = evaluator.Evaluate("A1/0", context);
            Assert.True(result is OdfFormulaError);
            Assert.Equal(OdfFormulaErrorType.Div0, ((OdfFormulaError)result).ErrorType);

            // Logical operations
            result = evaluator.Evaluate("IF(A1>0, B1, C1)", context); // A1 > 0 is True -> B1 (10)
            Assert.Equal(10.0, result);

            result = evaluator.Evaluate("IF(A1<0, B1, C1)", context); // A1 < 0 is False -> C1 (2)
            Assert.Equal(2.0, result);

            result = evaluator.Evaluate("AND(A1>0, B1>A1)", context); // True & True -> True
            Assert.Equal(true, result);

            result = evaluator.Evaluate("OR(A1<0, B1>A1)", context); // False | True -> True
            Assert.Equal(true, result);
        }

        [Fact]
        public void TestFormulaEvaluatorStrings()
        {
            var context = new MockEvaluationContext();
            var evaluator = new DefaultFormulaEvaluator();

            context.CellValues[OdfCellAddress.ParseExcel("A1")] = "Hello";
            context.CellValues[OdfCellAddress.ParseExcel("B1")] = "World";

            var result = evaluator.Evaluate("CONCAT(A1, \" \", B1)", context);
            Assert.Equal("Hello World", result);

            result = evaluator.Evaluate("LEFT(A1, 2)", context);
            Assert.Equal("He", result);

            result = evaluator.Evaluate("RIGHT(B1, 3)", context);
            Assert.Equal("rld", result);

            result = evaluator.Evaluate("MID(A1, 2, 3)", context);
            Assert.Equal("ell", result);
        }

        [Fact]
        public void TestFormulaEvaluatorStats()
        {
            var context = new MockEvaluationContext();
            var evaluator = new DefaultFormulaEvaluator();

            context.CellValues[OdfCellAddress.ParseExcel("A1")] = 10.0;
            context.CellValues[OdfCellAddress.ParseExcel("A2")] = 20.0;
            context.CellValues[OdfCellAddress.ParseExcel("A3")] = 30.0;
            context.CellValues[OdfCellAddress.ParseExcel("A4")] = "Not a number";

            // SUM
            var result = evaluator.Evaluate("SUM(A1:A3)", context);
            Assert.Equal(60.0, result);

            // AVERAGE
            result = evaluator.Evaluate("AVERAGE(A1:A3)", context);
            Assert.Equal(20.0, result);

            // COUNT (should only count numbers or numeric-convertible strings)
            result = evaluator.Evaluate("COUNT(A1:A4)", context);
            Assert.Equal(3.0, result);

            // SUMIF
            result = evaluator.Evaluate("SUMIF(A1:A3, \">15\")", context); // 20 + 30 = 50
            Assert.Equal(50.0, result);

            // COUNTIF
            result = evaluator.Evaluate("COUNTIF(A1:A3, \">15\")", context); // 20 and 30 -> 2
            Assert.Equal(2.0, result);
        }

        [Fact]
        public void TestFormulaEvaluatorVLookup()
        {
            var context = new MockEvaluationContext();
            var evaluator = new DefaultFormulaEvaluator();

            // Set up a lookup table
            // Fruit  | Qty | Price
            // Apple  | 10  | 1.5
            // Banana | 20  | 0.8
            // Cherry | 15  | 2.5
            context.CellValues[OdfCellAddress.ParseExcel("A1")] = "Apple";
            context.CellValues[OdfCellAddress.ParseExcel("B1")] = 10.0;
            context.CellValues[OdfCellAddress.ParseExcel("C1")] = 1.5;

            context.CellValues[OdfCellAddress.ParseExcel("A2")] = "Banana";
            context.CellValues[OdfCellAddress.ParseExcel("B2")] = 20.0;
            context.CellValues[OdfCellAddress.ParseExcel("C2")] = 0.8;

            context.CellValues[OdfCellAddress.ParseExcel("A3")] = "Cherry";
            context.CellValues[OdfCellAddress.ParseExcel("B3")] = 15.0;
            context.CellValues[OdfCellAddress.ParseExcel("C3")] = 2.5;

            // Exact lookup for Banana Qty (Col 2)
            var result = evaluator.Evaluate("VLOOKUP(\"Banana\", A1:C3, 2, FALSE)", context);
            Assert.Equal(20.0, result);

            // Exact lookup for Cherry Price (Col 3)
            result = evaluator.Evaluate("VLOOKUP(\"Cherry\", A1:C3, 3, FALSE)", context);
            Assert.Equal(2.5, result);

            // Exact lookup missing element
            result = evaluator.Evaluate("VLOOKUP(\"Durian\", A1:C3, 2, FALSE)", context);
            Assert.True(result is OdfFormulaError);
            Assert.Equal(OdfFormulaErrorType.NA, ((OdfFormulaError)result).ErrorType);

            // Approximate lookup (should match best lower/equal bound for sorted data)
            // Banana (exact match)
            result = evaluator.Evaluate("VLOOKUP(\"Banana\", A1:C3, 2, TRUE)", context);
            Assert.Equal(20.0, result);

            // Apricot (no exact, sort-wise between Apple and Banana -> matches Apple)
            result = evaluator.Evaluate("VLOOKUP(\"Apricot\", A1:C3, 2, TRUE)", context);
            Assert.Equal(10.0, result);
        }

        [Fact]
        public void TestCircularDependencyDetection()
        {
            var context = new MockEvaluationContext();
            var evaluator = new DefaultFormulaEvaluator();
            context.Evaluator = evaluator;

            // A1 has formula =B1
            context.CellFormulas[OdfCellAddress.ParseExcel("A1")] = "=B1";
            // B1 has formula =A1
            context.CellFormulas[OdfCellAddress.ParseExcel("B1")] = "=A1";

            // Evaluate A1, should detect circular reference and return #REF!
            var result = evaluator.EvaluateCell(OdfCellAddress.ParseExcel("A1"), context);
            Assert.True(result is OdfFormulaError);
            Assert.Equal(OdfFormulaErrorType.Ref, ((OdfFormulaError)result).ErrorType);
        }

        [Fact]
        public void TestCalculationCacheAndClearing()
        {
            var context = new MockEvaluationContext();
            var evaluator = new DefaultFormulaEvaluator();
            context.Evaluator = evaluator;

            var a1 = OdfCellAddress.ParseExcel("A1");
            context.CellValues[a1] = 10.0;
            context.CellFormulas[a1] = "=2*5";

            // Evaluate first time (calculates)
            var result = evaluator.EvaluateCell(a1, context);
            Assert.Equal(10.0, result);

            // Modify raw value in context, but do not clear cache
            context.CellValues[a1] = 99.0;
            context.CellFormulas.Remove(a1); // remove formula to simulate change
            
            // Re-evaluating should return the cached 10.0 value
            result = evaluator.EvaluateCell(a1, context);
            Assert.Equal(10.0, result);

            // Clear cache
            evaluator.ClearCache();

            // Re-evaluating should fetch the new 99.0 value
            result = evaluator.EvaluateCell(a1, context);
            Assert.Equal(99.0, result);
        }

        #endregion

        #region OdfNumberFormatter Tests

        [Fact]
        public void TestNumberFormatterDeduplicationAndFallback()
        {
            // Setup minimal XML DOM nodes for styles
            var contentRoot = new OdfNode(OdfNodeType.Element, "document-content", OdfNamespaces.Office, "office");
            var stylesRoot = new OdfNode(OdfNodeType.Element, "document-styles", OdfNamespaces.Office, "office");

            var formatter = new OdfNumberFormatter(contentRoot, stylesRoot);

            // 1. Check style creation and ODF XML output
            string styleName1 = formatter.GetOrCreateNumberStyle("#,##0.00");
            Assert.NotNull(styleName1);

            // 2. Cache Deduplication: retrieving the same format should yield the same style name
            string styleName2 = formatter.GetOrCreateNumberStyle("#,##0.00");
            Assert.Equal(styleName1, styleName2);

            // 3. Fallback: referencing a non-existent style name should return a standard style node instead of crashing
            var resolvedNode = formatter.GetNumberStyleNode("NonExistentStyle");
            Assert.NotNull(resolvedNode);
            Assert.Equal("number-style", resolvedNode.LocalName);
            Assert.Equal("NonExistentStyle", resolvedNode.GetAttribute("name", OdfNamespaces.Style));
        }

        #endregion

        #region OdfFontResolver Tests

        [Fact]
        public void TestFontResolverRegistrationAndEmbedding()
        {
            // 1. Test explicit font registration and lookup
            string dummyPath = Path.Combine(Path.GetTempPath(), "dummy_test_font.ttf");
            try
            {
                // Write a dummy file to pass the exists validation
                File.WriteAllBytes(dummyPath, new byte[] { 0x00, 0x01, 0x00, 0x00 });
                
                OdfFontResolver.RegisterFont("MyDummyFont", dummyPath);
                
                string? resolved = OdfFontResolver.ResolveFontPath("MyDummyFont");
                Assert.Equal(dummyPath, resolved);
            }
            finally
            {
                try { File.Delete(dummyPath); } catch {}
            }

            // 2. Test Font Embedding ZIP package writing mock
            using var ms = new MemoryStream();
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                package.SetMimeType("application/vnd.oasis.opendocument.spreadsheet");
                
                // Construct a mock <style:font-face> node
                var fontFace = new OdfNode(OdfNodeType.Element, "font-face", OdfNamespaces.Style, "style");
                fontFace.SetAttribute("name", OdfNamespaces.Style, "Arial");
                
                // Let's register Arial with a temp file so the resolver will find it during embedding
                string tempArialPath = Path.Combine(Path.GetTempPath(), "temp_arial.ttf");
                try
                {
                    File.WriteAllBytes(tempArialPath, new byte[] { 0x00, 0x01, 0x00, 0x00 });
                    OdfFontResolver.RegisterFont("Arial", tempArialPath);
                    
                    OdfFontResolver.EmbedFonts(package, fontFace, fontFace);
                    
                    // Verify ZIP entry was written
                    Assert.True(package.HasEntry("Fonts/Arial.ttf"));
                    
                    // Verify manifest entry exists
                    Assert.Contains("Fonts/Arial.ttf", package.Manifest.Keys);
                }
                finally
                {
                    try { File.Delete(tempArialPath); } catch {}
                }
            }
        }

        [Fact]
        public void TestEvaluateFormulasOnSaveOption()
        {
            // Create an in-memory package representing a spreadsheet
            using var ms = new MemoryStream();
            var saveOptions = new OdfSaveOptions { EvaluateFormulasOnSave = true };
            
            using (var package = OdfPackage.Create(ms, leaveOpen: true, options: saveOptions))
            {
                package.SetMimeType("application/vnd.oasis.opendocument.spreadsheet");
                
                // Construct a simple spreadsheet content.xml with cells and formulas
                string contentXml = @"<office:document-content 
                    xmlns:office=""urn:oasis:names:tc:opendocument:xmlns:office:1.0"" 
                    xmlns:table=""urn:oasis:names:tc:opendocument:xmlns:table:1.0"" 
                    xmlns:text=""urn:oasis:names:tc:opendocument:xmlns:text:1.0"">
  <office:body>
    <office:spreadsheet>
      <table:table table:name=""Sheet1"">
        <table:table-row>
          <table:table-cell office:value-type=""float"" office:value=""12"">
            <text:p>12</text:p>
          </table:table-cell>
          <table:table-cell office:value-type=""float"" office:value=""3"">
            <text:p>3</text:p>
          </table:table-cell>
          <table:table-cell table:formula=""oooc:=SUM([.A1:.B1])"">
            <text:p>0</text:p>
          </table:table-cell>
        </table:table-row>
      </table:table>
    </office:spreadsheet>
  </office:body>
</office:document-content>";

                package.WriteEntry("content.xml", Encoding.UTF8.GetBytes(contentXml), "text/xml");
                package.Save();
            }

            // Reopen and check if formula was evaluated and stored in content.xml
            ms.Position = 0;
            using (var package = OdfPackage.Open(ms, leaveOpen: true))
            {
                using var stream = package.GetEntryStream("content.xml");
                var root = OdfXmlReader.Parse(stream);
                
                // Let's traverse to find the third cell
                OdfNode? table = FindNode(root, "table");
                Assert.NotNull(table);
                OdfNode? row = FindNode(table, "table-row");
                Assert.NotNull(row);
                
                var cells = new List<OdfNode>();
                foreach (var child in row.Children)
                {
                    if (child.NodeType == OdfNodeType.Element && child.LocalName == "table-cell")
                    {
                        cells.Add(child);
                    }
                }
                
                Assert.Equal(3, cells.Count);
                var formulaCell = cells[2];
                
                // Assert that formula was evaluated to 15.0
                Assert.Equal("float", formulaCell.GetAttribute("value-type", OdfNamespaces.Office));
                Assert.Equal("15", formulaCell.GetAttribute("value", OdfNamespaces.Office));
                
                OdfNode? p = FindNode(formulaCell, "p");
                Assert.NotNull(p);
                Assert.Equal("15", p.TextContent);
            }
        }

        [Fact]
        public void TestEmbedUsedFontsOnSaveOption()
        {
            using var ms = new MemoryStream();
            var saveOptions = new OdfSaveOptions { EmbedUsedFonts = true };
            
            // Register a dummy Arial font path for resolving
            string dummyArialPath = Path.Combine(Path.GetTempPath(), "dummy_save_arial.ttf");
            File.WriteAllBytes(dummyArialPath, new byte[] { 0x00, 0x01, 0x00, 0x00 });
            OdfFontResolver.RegisterFont("Arial", dummyArialPath);

            try
            {
                using (var package = OdfPackage.Create(ms, leaveOpen: true, options: saveOptions))
                {
                    package.SetMimeType("application/vnd.oasis.opendocument.text");
                    
                    // Simple content.xml with style:font-face Arial
                    string contentXml = @"<office:document-content 
                        xmlns:office=""urn:oasis:names:tc:opendocument:xmlns:office:1.0"" 
                        xmlns:style=""urn:oasis:names:tc:opendocument:xmlns:style:1.0""
                        xmlns:svg=""urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0"">
  <office:font-face-decls>
    <style:font-face style:name=""Arial"" svg:font-family=""Arial""/>
  </office:font-face-decls>
</office:document-content>";

                    package.WriteEntry("content.xml", Encoding.UTF8.GetBytes(contentXml), "text/xml");
                    package.Save();
                }

                // Reopen and check if font entry exists in the package
                ms.Position = 0;
                using (var package = OdfPackage.Open(ms, leaveOpen: true))
                {
                    Assert.True(package.HasEntry("Fonts/Arial.ttf"));
                    Assert.Contains("Fonts/Arial.ttf", package.Manifest.Keys);
                    Assert.Equal("application/x-font-truetype", package.Manifest["Fonts/Arial.ttf"]);

                    // Verify updated href in content.xml
                    using var stream = package.GetEntryStream("content.xml");
                    var root = OdfXmlReader.Parse(stream);
                    OdfNode? fontFace = FindNode(root, "font-face");
                    Assert.NotNull(fontFace);
                    
                    OdfNode? uriNode = FindNode(fontFace, "font-face-uri");
                    Assert.NotNull(uriNode);
                    Assert.Equal("Fonts/Arial.ttf", uriNode.GetAttribute("href", OdfNamespaces.XLink));
                }
            }
            finally
            {
                try { File.Delete(dummyArialPath); } catch {}
            }
        }

        private static OdfNode? FindNode(OdfNode parent, string name)
        {
            if (parent.NodeType == OdfNodeType.Element && parent.LocalName == name)
            {
                return parent;
            }
            foreach (var child in parent.Children)
            {
                var found = FindNode(child, name);
                if (found != null) return found;
            }
            return null;
        }

        #endregion
    }
}
