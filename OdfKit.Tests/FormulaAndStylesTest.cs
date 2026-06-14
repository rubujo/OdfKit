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

        public class MockEvaluationContext : IEvaluationContext
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

            public Dictionary<string, object> NamedValues { get; } = new();

            public object GetNamedRangeOrExpressionValue(string name)
            {
                if (NamedValues.TryGetValue(name, out var val)) return val;
                return 0.0;
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

        [Fact]
        public void TestNewOpenFormulaFunctions()
        {
            var context = new MockEvaluationContext();
            var evaluator = new DefaultFormulaEvaluator();
            context.Evaluator = evaluator;

            // Math Functions
            Assert.Equal(5.0, evaluator.Evaluate("ABS(-5)", context));
            Assert.Equal(4.0, evaluator.Evaluate("SQRT(16)", context));
            Assert.Equal(12.35, evaluator.Evaluate("ROUND(12.3456, 2)", context));
            Assert.Equal(120.0, evaluator.Evaluate("ROUND(123.45, -1)", context));
            Assert.Equal(1.0, evaluator.Evaluate("MOD(10, 3)", context));
            Assert.Equal(8.0, evaluator.Evaluate("POWER(2, 3)", context));
            Assert.Equal(0.0, evaluator.Evaluate("LN(1)", context));
            Assert.Equal(2.0, evaluator.Evaluate("LOG(100, 10)", context));
            Assert.Equal(Math.Exp(1), evaluator.Evaluate("EXP(1)", context));
            Assert.Equal(3.0, evaluator.Evaluate("CEILING(2.3)", context));
            Assert.Equal(2.0, evaluator.Evaluate("FLOOR(2.7)", context));
            Assert.Equal(Math.PI, evaluator.Evaluate("PI()", context));
            Assert.Equal(180.0, evaluator.Evaluate("DEGREES(PI())", context));
            Assert.Equal(Math.PI, evaluator.Evaluate("RADIANS(180)", context));
            Assert.Equal(0.0, evaluator.Evaluate("SIN(0)", context));
            Assert.Equal(1.0, evaluator.Evaluate("COS(0)", context));
            Assert.Equal(0.0, evaluator.Evaluate("TAN(0)", context));
            Assert.Equal(2.0, evaluator.Evaluate("TRUNC(2.7)", context));
            Assert.Equal(20.0, evaluator.Evaluate("TRUNC(27.8, -1)", context));

            // String Functions
            Assert.Equal(5.0, evaluator.Evaluate("LEN(\"Hello\")", context));
            Assert.Equal("hello", evaluator.Evaluate("LOWER(\"Hello\")", context));
            Assert.Equal("HELLO", evaluator.Evaluate("UPPER(\"Hello\")", context));
            Assert.Equal("A B C", evaluator.Evaluate("TRIM(\"  A  B  C  \")", context));
            Assert.Equal("A-D-C", evaluator.Evaluate("REPLACE(\"A-B-C\", 3, 1, \"D\")", context));

            // Statistical Functions
            var maxRange = OdfCellAddress.ParseExcel("A1");
            var maxRange2 = OdfCellAddress.ParseExcel("A2");
            var maxRange3 = OdfCellAddress.ParseExcel("A3");
            context.CellValues[maxRange] = 5.0;
            context.CellValues[maxRange2] = 10.0;
            context.CellValues[maxRange3] = 2.0;

            Assert.Equal(10.0, evaluator.Evaluate("MAX(A1:A3)", context));
            Assert.Equal(2.0, evaluator.Evaluate("MIN(A1:A3)", context));

            // Date/Time Functions
            double dateVal = (new DateTime(2026, 6, 12) - new DateTime(1899, 12, 30)).TotalDays;
            Assert.Equal(dateVal, evaluator.Evaluate("DATE(2026, 6, 12)", context));
            
            var dateCell = OdfCellAddress.ParseExcel("B1");
            context.CellValues[dateCell] = dateVal;
            Assert.Equal(12.0, evaluator.Evaluate("DAY(B1)", context));
            Assert.Equal(6.0, evaluator.Evaluate("MONTH(B1)", context));
            Assert.Equal(2026.0, evaluator.Evaluate("YEAR(B1)", context));

            var timeCell = OdfCellAddress.ParseExcel("B2");
            double timeVal = (12.0 * 3600.0 + 30.0 * 60.0 + 45.0) / 86400.0;
            context.CellValues[timeCell] = timeVal;
            Assert.Equal(12.0, evaluator.Evaluate("HOUR(B2)", context));
            Assert.Equal(30.0, evaluator.Evaluate("MINUTE(B2)", context));
            Assert.Equal(45.0, evaluator.Evaluate("SECOND(B2)", context));

            Assert.Equal(timeVal, evaluator.Evaluate("TIME(12, 30, 45)", context));

            var todayVal = evaluator.Evaluate("TODAY()", context);
            var nowVal = evaluator.Evaluate("NOW()", context);
            Assert.True(todayVal is double);
            Assert.True(nowVal is double);
            Assert.True((double)nowVal >= (double)todayVal);

            // Matrix Functions
            var transResult = evaluator.Evaluate("TRANSPOSE(A1:A3)", context);
            Assert.True(transResult is object[,]);
            var arr = (object[,])transResult;
            Assert.Equal(1, arr.GetLength(0));
            Assert.Equal(3, arr.GetLength(1));
            Assert.Equal(5.0, arr[0, 0]);
            Assert.Equal(10.0, arr[0, 1]);
            Assert.Equal(2.0, arr[0, 2]);

            // Database Functions
            // Setup a database range A10:C13
            context.CellValues[OdfCellAddress.ParseExcel("A10")] = "Name";
            context.CellValues[OdfCellAddress.ParseExcel("B10")] = "Age";
            context.CellValues[OdfCellAddress.ParseExcel("C10")] = "Salary";
            
            context.CellValues[OdfCellAddress.ParseExcel("A11")] = "Alice";
            context.CellValues[OdfCellAddress.ParseExcel("B11")] = 30.0;
            context.CellValues[OdfCellAddress.ParseExcel("C11")] = 1000.0;
            
            context.CellValues[OdfCellAddress.ParseExcel("A12")] = "Bob";
            context.CellValues[OdfCellAddress.ParseExcel("B12")] = 40.0;
            context.CellValues[OdfCellAddress.ParseExcel("C12")] = 2000.0;
            
            context.CellValues[OdfCellAddress.ParseExcel("A13")] = "Charlie";
            context.CellValues[OdfCellAddress.ParseExcel("B13")] = 30.0;
            context.CellValues[OdfCellAddress.ParseExcel("C13")] = 3000.0;

            // Setup criteria range E10:E11
            context.CellValues[OdfCellAddress.ParseExcel("E10")] = "Age";
            context.CellValues[OdfCellAddress.ParseExcel("E11")] = 30.0;

            Assert.Equal(4000.0, evaluator.Evaluate("DSUM(A10:C13, \"Salary\", E10:E11)", context));
            Assert.Equal(2000.0, evaluator.Evaluate("DAVERAGE(A10:C13, \"Salary\", E10:E11)", context));
            Assert.Equal(2.0, evaluator.Evaluate("DCOUNT(A10:C13, \"Salary\", E10:E11)", context));
            Assert.Equal(3000.0, evaluator.Evaluate("DMAX(A10:C13, \"Salary\", E10:E11)", context));
            Assert.Equal(1000.0, evaluator.Evaluate("DMIN(A10:C13, \"Salary\", E10:E11)", context));

            // Financial Functions
            double pmt = (double)evaluator.Evaluate("PMT(0.06/12, 360, 100000)", context);
            Assert.Equal(-599.55, Math.Round(pmt, 2));

            double fv = (double)evaluator.Evaluate("FV(0.05/12, 10, -100, -1000)", context);
            Assert.Equal(2061.42, Math.Round(fv, 2));

            double pv = (double)evaluator.Evaluate("PV(0.05/12, 10, -100, -1000)", context);
            Assert.Equal(1936.73, Math.Round(pv, 2));

            double nper = (double)evaluator.Evaluate("NPER(0.05/12, -100, 1000)", context);
            Assert.Equal(10.24, Math.Round(nper, 2));

            double rate = (double)evaluator.Evaluate("RATE(12, -100, 1000)", context);
            Assert.Equal(0.03, Math.Round(rate, 2));

            double ipmt = (double)evaluator.Evaluate("IPMT(0.06/12, 1, 360, 100000)", context);
            Assert.Equal(-500.0, Math.Round(ipmt, 2));

            double ppmt = (double)evaluator.Evaluate("PPMT(0.06/12, 1, 360, 100000)", context);
            Assert.Equal(-99.55, Math.Round(ppmt, 2));
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

        #region Row/Column Visibility Tests

        [Fact]
        public void TestRowColumnVisibility()
        {
            using (var ms = new MemoryStream())
            {
                using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                var doc = new SpreadsheetDocument(package);
                var sheet = doc.AddSheet("VisibilitySheet");
                
                sheet.SetRowVisible(0, false);
                sheet.SetRowVisible(1, true);

                sheet.SetColumnVisible(0, false);
                sheet.SetColumnVisible(1, true);

                doc.Save();
            }

            ms.Position = 0;
            using (var package = OdfPackage.Open(ms, leaveOpen: true))
            {
                var doc = new SpreadsheetDocument(package);
                var sheet = doc.GetSheet("VisibilitySheet");
                Assert.NotNull(sheet);

                // Let's check row visibility
                var rows = new List<OdfNode>();
                foreach (var child in sheet.TableNode.Children)
                {
                    if (child.LocalName == "table-row" && child.NamespaceUri == OdfNamespaces.Table)
                        rows.Add(child);
                }

                Assert.True(rows.Count > 1);
                Assert.Equal("collapse", rows[0].GetAttribute("visibility", OdfNamespaces.Table));
                Assert.Equal("visible", rows[1].GetAttribute("visibility", OdfNamespaces.Table));

                // Check column visibility
                var cols = new List<OdfNode>();
                foreach (var child in sheet.TableNode.Children)
                {
                    if (child.LocalName == "table-column" && child.NamespaceUri == OdfNamespaces.Table)
                        cols.Add(child);
                }
                Assert.True(cols.Count > 1);
                Assert.Equal("collapse", cols[0].GetAttribute("visibility", OdfNamespaces.Table));
                Assert.Equal("visible", cols[1].GetAttribute("visibility", OdfNamespaces.Table));
            }
        }
    }

        [Fact]
        public void TestReferenceModelUnionAndIntersection()
        {
            var context = new MockEvaluationContext();
            var evaluator = new DefaultFormulaEvaluator();
            context.Evaluator = evaluator;

            // Setup cell values
            context.CellValues[OdfCellAddress.ParseExcel("A1")] = 10.0;
            context.CellValues[OdfCellAddress.ParseExcel("B1")] = 20.0;
            context.CellValues[OdfCellAddress.ParseExcel("A2")] = 30.0;
            context.CellValues[OdfCellAddress.ParseExcel("B2")] = 40.0;

            context.CellValues[OdfCellAddress.ParseExcel("C3")] = 50.0;
            context.CellValues[OdfCellAddress.ParseExcel("D3")] = 60.0;

            // 1. Reference Union: SUM(A1:B2 ~ C3:D3) -> 10 + 20 + 30 + 40 + 50 + 60 = 210
            Assert.Equal(210.0, evaluator.Evaluate("SUM(A1:B2 ~ C3:D3)", context));

            // 2. Reference Intersection: SUM(A1:B2 ! A2:B2) -> 30 + 40 = 70
            Assert.Equal(70.0, evaluator.Evaluate("SUM(A1:B2 ! A2:B2)", context));

            // 3. No intersection returns #NULL!
            var nullResult = evaluator.Evaluate("SUM(A1:B2 ! C3:D3)", context);
            Assert.IsType<OdfFormulaError>(nullResult);
            Assert.Equal(OdfFormulaErrorType.Null, ((OdfFormulaError)nullResult).ErrorType);
        }

        [Fact]
        public void TestMilestoneM8SpreadsheetAndFormulaAPIs()
        {
            using (var pkg = OdfPackage.Create(new MemoryStream()))
            {
                var doc = new SpreadsheetDocument(pkg);
                var sheet = doc.AddSheet("Sheet1");

                sheet.GetCell(0, 0).SetValue(10.0);
                sheet.GetCell(1, 0).SetValue(20.0);
                sheet.GetCell(2, 0).SetValue(30.0);

                doc.AddNamedRange("MyGlobalRange", new OdfCellRange(new OdfCellAddress(0, 0, "Sheet1"), new OdfCellAddress(2, 0, "Sheet1")));
                sheet.AddNamedRange("MyLocalRange", new OdfCellRange(new OdfCellAddress(0, 0, "Sheet1"), new OdfCellAddress(1, 0, "Sheet1")));
                doc.AddNamedExpression("MyGlobalExpr", "SUM(Sheet1.A1:Sheet1.A3)");

                var evaluator = new DefaultFormulaEvaluator();
                var domContext = new OdfDomEvaluationContext(doc.ContentDom, evaluator);

                var valGlobal = domContext.GetNamedRangeOrExpressionValue("MyGlobalRange");
                Assert.NotNull(valGlobal);
                Assert.IsType<object[,]>(valGlobal);
                var arrGlobal = (object[,])valGlobal;
                Assert.Equal(3, arrGlobal.GetLength(0));
                Assert.Equal(10.0, arrGlobal[0, 0]);

                var valExpr = domContext.GetNamedRangeOrExpressionValue("MyGlobalExpr");
                Assert.Equal(60.0, valExpr);

                domContext.CurrentCell = new OdfCellAddress(5, 0, "Sheet1");
                var sumGlobalResult = evaluator.Evaluate("SUM(MyGlobalRange)", domContext);
                Assert.Equal(60.0, sumGlobalResult);

                var sumLocalResult = evaluator.Evaluate("SUM(MyLocalRange)", domContext);
                Assert.Equal(30.0, sumLocalResult);

                var dbRange = doc.AddDatabaseRange("SalesData", new OdfCellRange(new OdfCellAddress(0, 0, "Sheet1"), new OdfCellAddress(9, 2, "Sheet1")));
                dbRange.SetSort((0, true), (1, false));
                dbRange.SetFilter((0, "=", "Active"), (2, ">", "100"));

                var dbRangeNode = dbRange.Node;
                Assert.Equal("SalesData", dbRangeNode.GetAttribute("name", OdfNamespaces.Table));
                
                OdfNode? sortNode = null;
                OdfNode? filterNode = null;
                foreach (var child in dbRangeNode.Children)
                {
                    if (child.LocalName == "sort" && child.NamespaceUri == OdfNamespaces.Table) sortNode = child;
                    if (child.LocalName == "filter" && child.NamespaceUri == OdfNamespaces.Table) filterNode = child;
                }
                Assert.NotNull(sortNode);
                Assert.NotNull(filterNode);

                Assert.Equal(2, sortNode.Children.Count);
                Assert.Equal("0", sortNode.Children[0].GetAttribute("field-number", OdfNamespaces.Table));
                Assert.Equal("ascending", sortNode.Children[0].GetAttribute("order", OdfNamespaces.Table));

                Assert.Equal(2, filterNode.Children.Count);
                Assert.Equal("0", filterNode.Children[0].GetAttribute("field-number", OdfNamespaces.Table));
                Assert.Equal("=", filterNode.Children[0].GetAttribute("operator", OdfNamespaces.Table));
                Assert.Equal("Active", filterNode.Children[0].GetAttribute("value", OdfNamespaces.Table));

                var cell = sheet.GetCell(0, 0);
                cell.AddConditionalFormatMap("cell-content() > 50", "MyAlertStyle", new OdfCellAddress(0, 0, "Sheet1"));

                var styleNode = doc.StyleEngine.GetOrCreateLocalStyle(cell.Node, "table-cell");
                OdfNode? mapNode = null;
                foreach (var child in styleNode.Children)
                {
                    if (child.LocalName == "map" && child.NamespaceUri == OdfNamespaces.Style)
                    {
                        mapNode = child;
                        break;
                    }
                }
                Assert.NotNull(mapNode);
                Assert.Equal("cell-content() > 50", mapNode.GetAttribute("condition", OdfNamespaces.Style));
                Assert.Equal("MyAlertStyle", mapNode.GetAttribute("apply-style-name", OdfNamespaces.Style));
                Assert.Equal("Sheet1.A1", mapNode.GetAttribute("base-cell-address", OdfNamespaces.Style));

                var pivotBuilder = new OdfPivotTableBuilder(
                    "MyPivot", 
                    new OdfCellRange(new OdfCellAddress(0, 0, "Sheet1"), new OdfCellAddress(9, 3, "Sheet1")), 
                    new OdfCellAddress(12, 0, "Sheet1"), 
                    sheet
                );
                pivotBuilder
                    .AddRowField("Category")
                    .AddColumnField("Region")
                    .AddDataField("Sales", "sum")
                    .AddPageField("Year")
                    .Build();

                OdfNode? dataPilotTablesNode = null;
                foreach (var child in sheet.TableNode.Children)
                {
                    if (child.LocalName == "data-pilot-tables" && child.NamespaceUri == OdfNamespaces.Table)
                    {
                        dataPilotTablesNode = child;
                        break;
                    }
                }
                Assert.NotNull(dataPilotTablesNode);
                Assert.Single(dataPilotTablesNode.Children);
                var pivotNode = dataPilotTablesNode.Children[0];
                Assert.Equal("MyPivot", pivotNode.GetAttribute("name", OdfNamespaces.Table));
                
                OdfNode? srcRangeNode = null;
                var fieldNodes = new List<OdfNode>();
                foreach (var child in pivotNode.Children)
                {
                    if (child.LocalName == "source-cell-range" && child.NamespaceUri == OdfNamespaces.Table) srcRangeNode = child;
                    if (child.LocalName == "data-pilot-field" && child.NamespaceUri == OdfNamespaces.Table) fieldNodes.Add(child);
                }
                Assert.NotNull(srcRangeNode);
                Assert.Equal("Sheet1.A1:.D10", srcRangeNode.GetAttribute("cell-range-address", OdfNamespaces.Table));
                
                Assert.Equal(4, fieldNodes.Count);
                Assert.Equal("Category", fieldNodes[0].GetAttribute("source-field-name", OdfNamespaces.Table));
                Assert.Equal("row", fieldNodes[0].GetAttribute("orientation", OdfNamespaces.Table));

                Assert.Equal("Region", fieldNodes[1].GetAttribute("source-field-name", OdfNamespaces.Table));
                Assert.Equal("column", fieldNodes[1].GetAttribute("orientation", OdfNamespaces.Table));

                Assert.Equal("Sales", fieldNodes[2].GetAttribute("source-field-name", OdfNamespaces.Table));
                Assert.Equal("data", fieldNodes[2].GetAttribute("orientation", OdfNamespaces.Table));
                Assert.Equal("sum", fieldNodes[2].GetAttribute("function", OdfNamespaces.Table));

                Assert.Equal("Year", fieldNodes[3].GetAttribute("source-field-name", OdfNamespaces.Table));
                Assert.Equal("page", fieldNodes[3].GetAttribute("orientation", OdfNamespaces.Table));
            }
        }

        /// <summary>
        /// 驗證當公式遍歷表格遇到極大的 rows-repeated 時，不會發生 OOM 且在合理時間內返回。
        /// </summary>
        [Fact]
        public void TraverseTable_LargeRowRepeatCount_DoesNotOOM()
        {
            using var pkg = OdfPackage.Create(new MemoryStream());
            var doc = new SpreadsheetDocument(pkg);
            var sheet = doc.AddSheet("Sheet1");

            // 建立一個有極大 rows-repeated 的 table-row 元素，並附帶一個 active cell
            var tableNS = OdfNamespaces.Table;
            var rowNode = OdfNodeFactory.CreateElement("table-row", tableNS, "table");
            rowNode.SetAttribute("number-rows-repeated", tableNS, "2000000000", "table");
            
            var cellNode = OdfNodeFactory.CreateElement("table-cell", tableNS, "table");
            cellNode.SetAttribute("value-type", OdfNamespaces.Office, "string", "office");
            
            var pNode = OdfNodeFactory.CreateElement("p", OdfNamespaces.Text, "text");
            pNode.TextContent = "10";
            cellNode.AppendChild(pNode);
            rowNode.AppendChild(cellNode);
            
            sheet.TableNode.AppendChild(rowNode);

            var startTime = DateTime.UtcNow;
            var evaluator = new DefaultFormulaEvaluator();
            
            // 建立 OdfDomEvaluationContext 時會呼叫 TraverseTable，驗證其是否在合理時間內返回
            var domContext = new OdfDomEvaluationContext(doc.ContentDom, evaluator);
            
            var elapsed = DateTime.UtcNow - startTime;
            Assert.True(elapsed.TotalSeconds < 10, $"TraverseTable 花費了 {elapsed.TotalSeconds} 秒，疑似發生 OOM 或未限制重複次數。");
        }
    }

        #endregion

        #endregion
}
