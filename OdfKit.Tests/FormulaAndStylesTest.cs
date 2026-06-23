using System;
using System.Collections.Generic;
using System.Globalization;
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

        public class MockEvaluationContext : IEvaluationContext, IOdfBlankCheckableContext
        {
            public bool IsBlank(OdfCellAddress address) =>
                !CellValues.ContainsKey(address) && !CellFormulas.ContainsKey(address);

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
                if (CellValues.TryGetValue(address, out var val))
                    return val;
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
                if (NamedValues.TryGetValue(name, out var val))
                    return val;
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

        [Fact]
        public void TestOpenFormulaExtensions()
        {
            var context = new MockEvaluationContext();
            var evaluator = new DefaultFormulaEvaluator();
            context.Evaluator = evaluator;

            // 1. Logical & Bitwise Functions
            Assert.Equal(false, evaluator.Evaluate("NOT(TRUE)", context));
            Assert.Equal(true, evaluator.Evaluate("XOR(TRUE, FALSE)", context));
            Assert.Equal(false, evaluator.Evaluate("XOR(TRUE, TRUE)", context));
            Assert.Equal(5.0, evaluator.Evaluate("IFERROR(5, 10)", context));
            Assert.Equal(10.0, evaluator.Evaluate("IFERROR(NA(), 10)", context));
            Assert.Equal(10.0, evaluator.Evaluate("IFNA(NA(), 10)", context));
            Assert.Equal(5.0, evaluator.Evaluate("IFNA(5, 10)", context));
            Assert.Equal("A", evaluator.Evaluate("IFS(1=2, \"B\", 2=2, \"A\")", context));
            Assert.Equal("A", evaluator.Evaluate("SWITCH(2, 1, \"B\", 2, \"A\")", context));
            Assert.Equal(2.0, evaluator.Evaluate("BITAND(6, 3)", context));
            Assert.Equal(7.0, evaluator.Evaluate("BITOR(5, 3)", context));
            Assert.Equal(6.0, evaluator.Evaluate("BITXOR(5, 3)", context));
            Assert.Equal(8.0, evaluator.Evaluate("BITLSHIFT(2, 2)", context));
            Assert.Equal(2.0, evaluator.Evaluate("BITRSHIFT(8, 2)", context));

            // 2. Information Functions
            Assert.Equal(true, evaluator.Evaluate("ISNUMBER(123.45)", context));
            Assert.Equal(false, evaluator.Evaluate("ISNUMBER(\"test\")", context));
            Assert.Equal(true, evaluator.Evaluate("ISTEXT(\"test\")", context));
            Assert.Equal(false, evaluator.Evaluate("ISTEXT(123)", context));
            Assert.Equal(true, evaluator.Evaluate("ISBLANK(A99)", context));
            Assert.Equal(true, evaluator.Evaluate("ISERROR(NA())", context));
            Assert.Equal(true, evaluator.Evaluate("ISNA(NA())", context));
            Assert.Equal(false, evaluator.Evaluate("ISNA(5)", context));
            Assert.Equal(true, evaluator.Evaluate("ISLOGICAL(TRUE)", context));
            Assert.Equal(1.0, evaluator.Evaluate("TYPE(123)", context));
            Assert.Equal(2.0, evaluator.Evaluate("TYPE(\"test\")", context));
            Assert.Equal(true, evaluator.Evaluate("ISODD(3)", context));
            Assert.Equal(false, evaluator.Evaluate("ISODD(4)", context));
            Assert.Equal(true, evaluator.Evaluate("ISEVEN(4)", context));
            Assert.Equal(false, evaluator.Evaluate("ISEVEN(3)", context));

            // 3. String Functions
            Assert.Equal("ABC", evaluator.Evaluate("CONCATENATE(\"A\", \"B\", \"C\")", context));
            Assert.Equal("Abc", evaluator.Evaluate("SUBSTITUTE(\"abc\", \"a\", \"A\")", context));
            Assert.Equal(2.0, evaluator.Evaluate("FIND(\"b\", \"abc\")", context));
            Assert.Equal(2.0, evaluator.Evaluate("SEARCH(\"B\", \"abc\")", context));
            Assert.Equal("aaa", evaluator.Evaluate("REPT(\"a\", 3)", context));
            Assert.Equal(true, evaluator.Evaluate("EXACT(\"abc\", \"abc\")", context));
            Assert.Equal(false, evaluator.Evaluate("EXACT(\"abc\", \"ABC\")", context));
            Assert.Equal(65.0, evaluator.Evaluate("CODE(\"A\")", context));
            Assert.Equal("A", evaluator.Evaluate("CHAR(65)", context));
            Assert.Equal("123", evaluator.Evaluate("TEXT(123, \"0\")", context));

            // 4. Math Functions
            Assert.Equal(3.0, evaluator.Evaluate("INT(3.7)", context));
            Assert.Equal(-1.0, evaluator.Evaluate("SIGN(-5)", context));
            Assert.Equal(3.0, evaluator.Evaluate("ODD(1.2)", context));
            Assert.Equal(2.0, evaluator.Evaluate("EVEN(1.2)", context));
            Assert.Equal(24.0, evaluator.Evaluate("PRODUCT(2, 3, 4)", context));
            Assert.Equal(120.0, evaluator.Evaluate("FACT(5)", context));
            Assert.Equal(10.0, evaluator.Evaluate("MROUND(11, 5)", context));
            Assert.Equal(3.0, evaluator.Evaluate("ROUNDUP(2.1, 0)", context));
            Assert.Equal(2.0, evaluator.Evaluate("ROUNDDOWN(2.9, 0)", context));
            Assert.True(evaluator.Evaluate("RAND()", context) is double);
            Assert.Equal(5.0, evaluator.Evaluate("RANDBETWEEN(5, 5)", context));
            Assert.Equal(Math.PI / 2.0, evaluator.Evaluate("ASIN(1)", context));
            Assert.Equal(0.0, evaluator.Evaluate("ACOS(1)", context));
            Assert.Equal(Math.PI / 4.0, evaluator.Evaluate("ATAN(1)", context));
            Assert.Equal(Math.PI / 4.0, evaluator.Evaluate("ATAN2(1, 1)", context));
            Assert.Equal(2.0, evaluator.Evaluate("LOG10(100)", context));

            // 5. Statistical Functions
            var cellC1 = OdfCellAddress.ParseExcel("C1");
            var cellC2 = OdfCellAddress.ParseExcel("C2");
            var cellC3 = OdfCellAddress.ParseExcel("C3");
            context.CellValues[cellC1] = 10.0;
            context.CellValues[cellC2] = 20.0;
            context.CellValues[cellC3] = 30.0;

            Assert.Equal(3.0, evaluator.Evaluate("COUNTA(C1:C3)", context));
            Assert.Equal(0.0, evaluator.Evaluate("COUNTBLANK(C1:C3)", context));
            Assert.Equal(25.0, evaluator.Evaluate("AVERAGEIF(C1:C3, \">10\")", context));
            Assert.Equal(20.0, evaluator.Evaluate("MEDIAN(C1:C3)", context));
            Assert.Equal(10.0, evaluator.Evaluate("STDEV(C1:C3)", context));
            Assert.Equal(100.0, evaluator.Evaluate("VAR(C1:C3)", context));
            Assert.Equal(30.0, evaluator.Evaluate("LARGE(C1:C3, 1)", context));
            Assert.Equal(10.0, evaluator.Evaluate("SMALL(C1:C3, 1)", context));
            Assert.Equal(2.0, evaluator.Evaluate("RANK(20, C1:C3)", context));
            Assert.Equal(20.0, evaluator.Evaluate("PERCENTILE(C1:C3, 0.5)", context));
            Assert.Equal(15.0, evaluator.Evaluate("QUARTILE(C1:C3, 1)", context));

            // Setup multi criteria test
            var cellD1 = OdfCellAddress.ParseExcel("D1");
            var cellD2 = OdfCellAddress.ParseExcel("D2");
            var cellD3 = OdfCellAddress.ParseExcel("D3");
            context.CellValues[cellD1] = "A";
            context.CellValues[cellD2] = "B";
            context.CellValues[cellD3] = "A";

            Assert.Equal(40.0, evaluator.Evaluate("SUMIFS(C1:C3, D1:D3, \"A\")", context));
            Assert.Equal(20.0, evaluator.Evaluate("AVERAGEIFS(C1:C3, D1:D3, \"A\")", context));
            Assert.Equal(2.0, evaluator.Evaluate("COUNTIFS(D1:D3, \"A\")", context));

            // 6. Lookup Functions
            // Setup lookup table
            context.CellValues[OdfCellAddress.ParseExcel("E1")] = "X";
            context.CellValues[OdfCellAddress.ParseExcel("F1")] = "Y";
            context.CellValues[OdfCellAddress.ParseExcel("E2")] = 100.0;
            context.CellValues[OdfCellAddress.ParseExcel("F2")] = 200.0;

            Assert.Equal(200.0, evaluator.Evaluate("HLOOKUP(\"Y\", E1:F2, 2, FALSE)", context));
            Assert.Equal(100.0, evaluator.Evaluate("INDEX(E1:F2, 2, 1)", context));
            Assert.Equal(2.0, evaluator.Evaluate("MATCH(\"Y\", E1:F1, 0)", context));
            Assert.Equal(200.0, evaluator.Evaluate("OFFSET(E1, 1, 1)", context));
            Assert.Equal(2.0, evaluator.Evaluate("ROWS(E1:E2)", context));
            Assert.Equal(2.0, evaluator.Evaluate("COLUMNS(E1:F1)", context));
            Assert.Equal("Y", evaluator.Evaluate("CHOOSE(2, \"X\", \"Y\", \"Z\")", context));

            // 7. Date & Time Functions
            double date1 = (new DateTime(2026, 1, 1) - new DateTime(1899, 12, 30)).TotalDays;
            double date2 = (new DateTime(2026, 12, 31) - new DateTime(1899, 12, 30)).TotalDays;

            var cellH1 = OdfCellAddress.ParseExcel("H1");
            var cellH2 = OdfCellAddress.ParseExcel("H2");
            context.CellValues[cellH1] = date1;
            context.CellValues[cellH2] = date2;

            Assert.Equal(11.0, evaluator.Evaluate("DATEDIF(H1, H2, \"M\")", context));
            Assert.Equal(date1, evaluator.Evaluate("DATEVALUE(\"2026-01-01\")", context));
            Assert.Equal(0.5, evaluator.Evaluate("TIMEVALUE(\"12:00:00\")", context));
            Assert.Equal(5.0, evaluator.Evaluate("WEEKDAY(H1)", context));
            Assert.Equal(1.0, evaluator.Evaluate("WEEKNUM(H1)", context));
            Assert.Equal((new DateTime(2026, 1, 2) - new DateTime(1899, 12, 30)).TotalDays, evaluator.Evaluate("WORKDAY(H1, 1)", context));
            Assert.Equal(261.0, evaluator.Evaluate("NETWORKDAYS(H1, H2)", context));
            Assert.Equal((new DateTime(2026, 2, 1) - new DateTime(1899, 12, 30)).TotalDays, evaluator.Evaluate("EDATE(H1, 1)", context));
            Assert.Equal((new DateTime(2026, 1, 31) - new DateTime(1899, 12, 30)).TotalDays, evaluator.Evaluate("EOMONTH(H1, 0)", context));

            // 8. Financial Functions
            Assert.Equal(10.0, evaluator.Evaluate("SLN(100, 50, 5)", context));
            Assert.Equal(40.0, evaluator.Evaluate("DDB(100, 10, 5, 1)", context));

            // IRR / MIRR
            var cellI1 = OdfCellAddress.ParseExcel("I1");
            var cellI2 = OdfCellAddress.ParseExcel("I2");
            var cellI3 = OdfCellAddress.ParseExcel("I3");
            context.CellValues[cellI1] = -100.0;
            context.CellValues[cellI2] = 50.0;
            context.CellValues[cellI3] = 60.0;

            double irr = (double)evaluator.Evaluate("IRR(I1:I3)", context);
            Assert.Equal(0.06, Math.Round(irr, 2));

            double mirr = (double)evaluator.Evaluate("MIRR(I1:I3, 0.05, 0.05)", context);
            Assert.Equal(0.06, Math.Round(mirr, 2));
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

        /// <summary>
        /// 驗證 <see cref="OdfNumberFormatter.ResolveStandardFormat"/> 能將 .NET 標準格式說明符
        /// （C／N／F／P／d／D／t／T／g／G）正確展開為對應的自訂數值或日期時間格式字串；
        /// 對於已是自訂格式（長度大於 2）或空字串的輸入則應原樣或回退保留。
        /// </summary>
        [Fact]
        public void TestResolveStandardFormatExpandsAllDotNetStandardSpecifiers()
        {
            var culture = CultureInfo.InvariantCulture;

            Assert.Equal("Standard", OdfNumberFormatter.ResolveStandardFormat(string.Empty, culture));

            // 自訂格式（長度大於 2）應原樣傳回，不進行展開。
            Assert.Equal("#,##0.000", OdfNumberFormatter.ResolveStandardFormat("#,##0.000", culture));

            string currency = OdfNumberFormatter.ResolveStandardFormat("C2", culture);
            Assert.Contains("#,##0", currency);
            Assert.Contains(".00", currency);

            Assert.Equal("#,##0.00", OdfNumberFormatter.ResolveStandardFormat("N2", culture));
            Assert.Equal("0.00", OdfNumberFormatter.ResolveStandardFormat("F2", culture));
            Assert.Equal("0.00%", OdfNumberFormatter.ResolveStandardFormat("P2", culture));

            Assert.Equal(culture.DateTimeFormat.ShortDatePattern, OdfNumberFormatter.ResolveStandardFormat("d", culture));
            Assert.Equal(culture.DateTimeFormat.LongDatePattern, OdfNumberFormatter.ResolveStandardFormat("D", culture));
            Assert.Equal(culture.DateTimeFormat.ShortTimePattern, OdfNumberFormatter.ResolveStandardFormat("t", culture));
            Assert.Equal(culture.DateTimeFormat.LongTimePattern, OdfNumberFormatter.ResolveStandardFormat("T", culture));
            Assert.Equal(
                culture.DateTimeFormat.ShortDatePattern + " " + culture.DateTimeFormat.ShortTimePattern,
                OdfNumberFormatter.ResolveStandardFormat("g", culture));
            Assert.Equal(
                culture.DateTimeFormat.ShortDatePattern + " " + culture.DateTimeFormat.LongTimePattern,
                OdfNumberFormatter.ResolveStandardFormat("G", culture));

            // 不在已知說明符清單中的單字元應原樣回傳。
            Assert.Equal("Z", OdfNumberFormatter.ResolveStandardFormat("Z", culture));
        }

        /// <summary>
        /// 驗證 <see cref="OdfNumberFormatter.ParsePattern"/> 能正確判斷數值／貨幣／百分比／日期／時間
        /// 五種格式型別，並正確解析分組符號、最小整數位數與小數位數。
        /// </summary>
        [Fact]
        public void TestParsePatternRecognizesAllFormatTypes()
        {
            FormatInfo number = OdfNumberFormatter.ParsePattern("#,##0.00");
            Assert.Equal(FormatType.Number, number.Type);
            Assert.True(number.Grouping);
            Assert.Equal(2, number.DecimalPlaces);
            Assert.Equal(1, number.MinIntegerDigits);

            FormatInfo integerOnly = OdfNumberFormatter.ParsePattern("0000");
            Assert.Equal(FormatType.Number, integerOnly.Type);
            Assert.False(integerOnly.Grouping);
            Assert.Equal(0, integerOnly.DecimalPlaces);
            Assert.Equal(4, integerOnly.MinIntegerDigits);

            FormatInfo percentage = OdfNumberFormatter.ParsePattern("0.00%");
            Assert.Equal(FormatType.Percentage, percentage.Type);

            FormatInfo currency = OdfNumberFormatter.ParsePattern("$#,##0.00");
            Assert.Equal(FormatType.Currency, currency.Type);
            Assert.Equal("$", currency.CurrencySymbol);

            FormatInfo ntdCurrency = OdfNumberFormatter.ParsePattern("NT$#,##0");
            Assert.Equal(FormatType.Currency, ntdCurrency.Type);
            Assert.Equal("NT$", ntdCurrency.CurrencySymbol);

            FormatInfo date = OdfNumberFormatter.ParsePattern("yyyy/MM/dd");
            Assert.Equal(FormatType.Date, date.Type);
            Assert.NotEmpty(date.DateTimeTokens);

            FormatInfo time = OdfNumberFormatter.ParsePattern("HH:mm:ss");
            Assert.Equal(FormatType.Time, time.Type);
            Assert.NotEmpty(time.DateTimeTokens);
        }

        #endregion

        #region OdfLength Tests

        /// <summary>
        /// 驗證 <see cref="OdfLength"/> 的工廠方法（FromPixels／FromPercentage／FromEm）能正確建立
        /// 對應單位的結構，且 <see cref="OdfLength.ConvertTo"/> 在絕對單位之間換算正確（以點為樞紐），
        /// 對相對單位（百分比／Em）與絕對單位互轉則擲出 <see cref="InvalidOperationException"/>。
        /// </summary>
        [Fact]
        public void TestOdfLengthFactoryMethodsAndConvertToHandlesAbsoluteAndRelativeUnits()
        {
            OdfLength pixels = OdfLength.FromPixels(96);
            Assert.Equal(OdfUnit.Pixels, pixels.Unit);
            Assert.Equal(96, pixels.Value);

            OdfLength percentage = OdfLength.FromPercentage(50);
            Assert.Equal(OdfUnit.Percentage, percentage.Unit);

            OdfLength em = OdfLength.FromEm(1.5);
            Assert.Equal(OdfUnit.Em, em.Unit);

            // 96 像素於標準 96 DPI 換算下應等於 1 英吋（72 點）。
            Assert.Equal(72.0, pixels.ConvertTo(OdfUnit.Points), precision: 6);
            Assert.Equal(1.0, pixels.ToInches(), precision: 6);

            OdfLength oneInch = OdfLength.FromInches(1.0);
            Assert.Equal(2.54, oneInch.ToCentimeters(), precision: 6);
            Assert.Equal(25.4, oneInch.ToMillimeters(), precision: 6);

            Assert.Throws<InvalidOperationException>(() => percentage.ConvertTo(OdfUnit.Centimeters));
            Assert.Throws<InvalidOperationException>(() => em.ConvertTo(OdfUnit.Points));
        }

        /// <summary>
        /// 驗證 <see cref="OdfLength.FallbackTo"/> 僅在單位為 <see cref="OdfUnit.Unspecified"/> 時
        /// 套用指定的預設單位，已有明確單位的長度則維持原樣不變。
        /// </summary>
        [Fact]
        public void TestOdfLengthFallbackToOnlyAppliesWhenUnspecified()
        {
            OdfLength unspecified = new(5, OdfUnit.Unspecified);
            OdfLength fallenBack = unspecified.FallbackTo(OdfUnit.Centimeters);
            Assert.Equal(OdfUnit.Centimeters, fallenBack.Unit);
            Assert.Equal(5, fallenBack.Value);

            OdfLength explicitCm = OdfLength.FromCentimeters(3);
            OdfLength unchanged = explicitCm.FallbackTo(OdfUnit.Millimeters);
            Assert.Equal(OdfUnit.Centimeters, unchanged.Unit);
            Assert.Equal(3, unchanged.Value);
        }

        /// <summary>
        /// 驗證 <see cref="OdfLength.GetHashCode"/> 與 <see cref="OdfLength.Equals(OdfLength)"/> 的雜湊碼契約：
        /// 不同絕對單位但數值相等的長度（例如 1 英吋與 2.54 公分）應視為相等且雜湊碼相同；
        /// 相對單位（百分比）則維持原單位雜湊，不與絕對單位混淆。
        /// </summary>
        [Fact]
        public void TestOdfLengthGetHashCodeIsConsistentWithCrossUnitEquality()
        {
            OdfLength oneInch = OdfLength.FromInches(1.0);
            OdfLength equivalentCm = OdfLength.FromCentimeters(2.54);

            Assert.True(oneInch.Equals(equivalentCm));
            Assert.Equal(oneInch.GetHashCode(), equivalentCm.GetHashCode());

            OdfLength fiftyPercent = OdfLength.FromPercentage(50);
            OdfLength otherFiftyPercent = OdfLength.FromPercentage(50);
            Assert.True(fiftyPercent.Equals(otherFiftyPercent));
            Assert.Equal(fiftyPercent.GetHashCode(), otherFiftyPercent.GetHashCode());
        }

        /// <summary>
        /// 驗證 <see cref="OdfBorder.GetHashCode"/> 與 <see cref="OdfBorder.Equals(OdfBorder)"/> 的雜湊碼契約：
        /// 樣式、寬度、色彩皆相等的框線應產生相同雜湊碼；任一欄位不同則雜湊碼應有極高機率不同。
        /// </summary>
        [Fact]
        public void TestOdfBorderGetHashCodeIsConsistentWithEquals()
        {
            OdfBorder solidBlack = OdfBorder.Parse("1pt solid #000000");
            OdfBorder anotherSolidBlack = OdfBorder.Parse("1pt solid #000000");
            Assert.True(solidBlack.Equals(anotherSolidBlack));
            Assert.Equal(solidBlack.GetHashCode(), anotherSolidBlack.GetHashCode());

            OdfBorder dashedRed = OdfBorder.Parse("2pt dashed #FF0000");
            Assert.False(solidBlack.Equals(dashedRed));
            Assert.NotEqual(solidBlack.GetHashCode(), dashedRed.GetHashCode());

            Assert.Equal(OdfBorder.None.GetHashCode(), OdfBorder.None.GetHashCode());
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
                try
                { File.Delete(dummyPath); }
                catch { }
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
                    try
                    { File.Delete(tempArialPath); }
                    catch { }
                }
            }
        }

        /// <summary>
        /// 驗證 <see cref="OdfFontResolver.IsTrueTypeCollection"/> 能依 'ttcf' 幻數正確區分
        /// TrueType Collection（.ttc）與一般 TrueType（.ttf）字型檔案，並對不存在的路徑安全回傳 false。
        /// </summary>
        [Fact]
        public void TestIsTrueTypeCollectionDetectsTtcSignature()
        {
            string ttcPath = Path.Combine(Path.GetTempPath(), "odfkit_test_" + Guid.NewGuid().ToString("N") + ".ttc");
            string ttfPath = Path.Combine(Path.GetTempPath(), "odfkit_test_" + Guid.NewGuid().ToString("N") + ".ttf");
            try
            {
                // 'ttcf' 簽章的大端序位元組序列。
                File.WriteAllBytes(ttcPath, new byte[] { 0x74, 0x74, 0x63, 0x66, 0x00, 0x01, 0x00, 0x00 });
                File.WriteAllBytes(ttfPath, new byte[] { 0x00, 0x01, 0x00, 0x00 });

                Assert.True(OdfFontResolver.IsTrueTypeCollection(ttcPath));
                Assert.False(OdfFontResolver.IsTrueTypeCollection(ttfPath));
                Assert.False(OdfFontResolver.IsTrueTypeCollection(Path.Combine(Path.GetTempPath(), "odfkit_nonexistent_" + Guid.NewGuid().ToString("N") + ".ttc")));
            }
            finally
            {
                try
                { File.Delete(ttcPath); }
                catch { }
                try
                { File.Delete(ttfPath); }
                catch { }
            }
        }

        /// <summary>
        /// 驗證系統真實 TrueType Collection 字型檔案（Windows <c>Nirmala.ttc</c>）可被
        /// <see cref="OdfFontResolver.IsTrueTypeCollection"/> 正確識別，確保此偵測邏輯與真實字型檔案結構相容。
        /// </summary>
        [Fact]
        public void TestIsTrueTypeCollectionDetectsRealSystemTtcFile()
        {
            const string realTtcPath = @"C:\Windows\Fonts\Nirmala.ttc";
            if (!File.Exists(realTtcPath))
            {
                Assert.Skip("找不到系統真實 TTC 字型檔案，略過真機字型格式偵測測試。");
            }

            Assert.True(OdfFontResolver.IsTrueTypeCollection(realTtcPath));
        }

        /// <summary>
        /// 驗證 <see cref="OdfFontResolver.WarnIfUnresolvable"/> 對已註冊字型回傳 <see langword="true"/>，
        /// 對無法解析的字型名稱回傳 <see langword="false"/> 且同一名稱僅記錄一次警告（不重複觸發）。
        /// </summary>
        [Fact]
        public void TestWarnIfUnresolvableReturnsResolutionStatusAndDeduplicatesWarnings()
        {
            string registeredFontName = "OdfKit測試已註冊字型_" + Guid.NewGuid().ToString("N");
            string unresolvableFontName = "OdfKit測試無法解析字型_" + Guid.NewGuid().ToString("N");
            string dummyPath = Path.Combine(Path.GetTempPath(), "odfkit_warn_test_" + Guid.NewGuid().ToString("N") + ".ttf");
            try
            {
                File.WriteAllBytes(dummyPath, new byte[] { 0x00, 0x01, 0x00, 0x00 });
                OdfFontResolver.RegisterFont(registeredFontName, dummyPath);

                Assert.True(OdfFontResolver.WarnIfUnresolvable(registeredFontName, "單元測試"));

                // 重複呼叫同一個無法解析的字型名稱不應拋出例外（內部以 HashSet 去重警告）。
                Assert.False(OdfFontResolver.WarnIfUnresolvable(unresolvableFontName, "單元測試"));
                Assert.False(OdfFontResolver.WarnIfUnresolvable(unresolvableFontName, "單元測試"));
            }
            finally
            {
                try
                { File.Delete(dummyPath); }
                catch { }
            }
        }

        /// <summary>
        /// 驗證 <see cref="OdfFontResolver.RegisterFontDirectory"/> 註冊的自訂目錄會在下一次字型查詢時
        /// 被掃描，使該目錄中含有效字型名稱表的 TrueType 檔案可被 <see cref="OdfFontResolver.ResolveFontPath"/>
        /// 解析出正確路徑；並驗證對不存在的目錄路徑擲出 <see cref="DirectoryNotFoundException"/>。
        /// </summary>
        [Fact]
        public void TestRegisterFontDirectoryTriggersRescanAndResolvesFontsFromCustomDirectory()
        {
            Assert.Throws<DirectoryNotFoundException>(
                () => OdfFontResolver.RegisterFontDirectory(Path.Combine(Path.GetTempPath(), "odfkit_nonexistent_dir_" + Guid.NewGuid().ToString("N"))));

            string customDir = Path.Combine(Path.GetTempPath(), "odfkit_fontdir_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(customDir);
            try
            {
                // 複製一個系統真實 TrueType 字型至自訂目錄，確保 TtfFontNameReader 能讀出有效字型名稱表。
                const string realFontPath = @"C:\Windows\Fonts\arial.ttf";
                if (!File.Exists(realFontPath))
                {
                    Assert.Skip("找不到系統真實 TrueType 字型檔案，略過自訂字型目錄掃描測試。");
                }

                string copiedFontPath = Path.Combine(customDir, "arial.ttf");
                File.Copy(realFontPath, copiedFontPath);

                OdfFontResolver.RegisterFontDirectory(customDir);

                // TtfFontNameReader 讀出的內部名稱表固定為原始字型家族名稱「Arial」（與檔名無關），
                // 但目錄掃描僅在該鍵尚未登錄時才會寫入 _fontMap（不會覆寫既有專案，見原始碼
                // ScanDirectory 的 `if (!_fontMap.ContainsKey(name))` 判斷）。由於 "Arial" 鍵為整個
                // 測試組件共用的靜態狀態，可能已被 FormulaAndStylesTest.cs 中其他測試登錄過
                // 一個測試結束後即刪除的暫存路徑，故先以 RegisterFont 顯式覆寫為本次複製的真實路徑，
                // 確保以下解析結果必定對應到本測試剛建立、確實存在的檔案，而非殘留的舊暫存路徑。
                OdfFontResolver.RegisterFont("Arial", copiedFontPath);

                string? resolved = OdfFontResolver.ResolveFontPath("Arial");
                Assert.Equal(copiedFontPath, resolved);
                Assert.True(File.Exists(resolved), "RegisterFontDirectory 掃描後解析出的字型路徑必須實際存在。");
            }
            finally
            {
                try
                { Directory.Delete(customDir, recursive: true); }
                catch { }
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
                try
                { File.Delete(dummyArialPath); }
                catch { }
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
                if (found != null)
                    return found;
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

        /// <summary>
        /// 驗證括號運算式與具名範圍節點作為聯集／交集運算元時，能正確委派 GetRanges 取得範圍清單。
        /// </summary>
        [Fact]
        public void TestReferenceModelParenthesizedAndNamedRangeGetRanges()
        {
            var context = new MockEvaluationContext();
            var evaluator = new DefaultFormulaEvaluator();
            context.Evaluator = evaluator;

            context.CellValues[OdfCellAddress.ParseExcel("A1")] = 10.0;
            context.CellValues[OdfCellAddress.ParseExcel("B1")] = 20.0;
            context.CellValues[OdfCellAddress.ParseExcel("C3")] = 50.0;
            context.CellValues[OdfCellAddress.ParseExcel("D3")] = 60.0;

            // 1. 括號運算式作為聯集左運算元：(A1~B1) ~ C3:D3 -> 10 + 20 + 50 + 60 = 140
            // ParenthesizedNode.GetRanges 須委派內部節點才能取得正確範圍。
            Assert.Equal(140.0, evaluator.Evaluate("SUM((A1~B1) ~ C3:D3)", context));

            // 2. 具名範圍作為交集運算元：MyRange ! A1:A1 -> 僅 A1 在交集內，值為 10
            // NamedRangeNode.GetRanges 須能解析具名範圍字串並轉換為 OdfCellRange。
            context.NamedValues["MyRange"] = "A1:B1";
            Assert.Equal(10.0, evaluator.Evaluate("SUM(MyRange ! A1:A1)", context));

            // 3. 具名範圍無法解析為範圍時，GetRanges 應回傳空清單，交集自然無結果。
            context.NamedValues["MyScalar"] = 99.0;
            var scalarIntersection = evaluator.Evaluate("SUM(MyScalar ! A1:A1)", context);
            Assert.IsType<OdfFormulaError>(scalarIntersection);
            Assert.Equal(OdfFormulaErrorType.Null, ((OdfFormulaError)scalarIntersection).ErrorType);
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
                    if (child.LocalName == "sort" && child.NamespaceUri == OdfNamespaces.Table)
                        sortNode = child;
                    if (child.LocalName == "filter" && child.NamespaceUri == OdfNamespaces.Table)
                        filterNode = child;
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

                // 依 ODF 1.4 schema，table:data-pilot-tables 是 office:spreadsheet 的直接
                // 子節點（與所有 table:table 同層），而非個別工作表 table:table 的子節點。
                OdfNode? dataPilotTablesNode = null;
                foreach (var child in doc.SheetsRoot.Children)
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
                    if (child.LocalName == "source-cell-range" && child.NamespaceUri == OdfNamespaces.Table)
                        srcRangeNode = child;
                    if (child.LocalName == "data-pilot-field" && child.NamespaceUri == OdfNamespaces.Table)
                        fieldNodes.Add(child);
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
        /// 驗證當公式遍歷表格遇到極大的 rows-repeated 時，不會發生 OOM 且在合理時間內傳回。
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

            // 建立 OdfDomEvaluationContext 時會呼叫 TraverseTable，驗證其是否在合理時間內傳回
            var domContext = new OdfDomEvaluationContext(doc.ContentDom, evaluator);

            var elapsed = DateTime.UtcNow - startTime;
            Assert.True(elapsed.TotalSeconds < 10, $"TraverseTable 花費了 {elapsed.TotalSeconds} 秒，疑似發生 OOM 或未限制重複次數。");
        }
    }

        #endregion

        #endregion
}
