using System;
using System.Text;
using Xunit;
using OdfKit.Spreadsheet;
using OdfKit.Formula;

namespace OdfKit.Tests
{
    public class FormulaTranslationStressTests
    {
        #region 1. Coordinate Parsing and Boundary Stress Tests

        [Fact]
        public void TestExcelCoordinatesMaxBoundaries()
        {
            // Row 1048576 (0-based 1048575), Column 16384 (XFD, 0-based 16383)
            var addr = OdfCellAddress.ParseExcel("XFD1048576");
            Assert.Equal(1048575, addr.Row);
            Assert.Equal(16383, addr.Column);
            Assert.Null(addr.SheetName);
            Assert.False(addr.IsRowAbsolute);
            Assert.False(addr.IsColumnAbsolute);

            Assert.Equal("XFD1048576", addr.ToExcelString());
            Assert.Equal(".XFD1048576", addr.ToOdfString(false));
            Assert.Equal("[.XFD1048576]", addr.ToOdfString(true));
        }

        [Fact]
        public void TestOdfCoordinatesMaxBoundaries()
        {
            var addr = OdfCellAddress.ParseOdf(".XFD1048576");
            Assert.Equal(1048575, addr.Row);
            Assert.Equal(16383, addr.Column);
            Assert.Null(addr.SheetName);
            Assert.False(addr.IsRowAbsolute);
            Assert.False(addr.IsColumnAbsolute);

            Assert.Equal("XFD1048576", addr.ToExcelString());
            Assert.Equal(".XFD1048576", addr.ToOdfString(false));
        }

        [Fact]
        public void TestColumnLetterParsingVariations()
        {
            // Test single, double, triple, and quadruple letter columns
            var colA = OdfCellAddress.ParseExcel("A1");
            Assert.Equal(0, colA.Column);

            var colZ = OdfCellAddress.ParseExcel("Z1");
            Assert.Equal(25, colZ.Column);

            var colAA = OdfCellAddress.ParseExcel("AA1");
            Assert.Equal(26, colAA.Column);

            var colAZ = OdfCellAddress.ParseExcel("AZ1");
            Assert.Equal(51, colAZ.Column);

            var colBA = OdfCellAddress.ParseExcel("BA1");
            Assert.Equal(52, colBA.Column);

            var colZZ = OdfCellAddress.ParseExcel("ZZ1");
            Assert.Equal(701, colZZ.Column);

            var colAAA = OdfCellAddress.ParseExcel("AAA1");
            Assert.Equal(702, colAAA.Column);

            var colZZZ = OdfCellAddress.ParseExcel("ZZZ1");
            Assert.Equal(18277, colZZZ.Column);

            var colAAAA = OdfCellAddress.ParseExcel("AAAA1");
            Assert.Equal(18278, colAAAA.Column);

            // Case insensitivity
            var colLower = OdfCellAddress.ParseExcel("xfd1048576");
            Assert.Equal(16383, colLower.Column);
            Assert.Equal(1048575, colLower.Row);
        }

        [Fact]
        public void TestRowAndColumnOverflowParsing()
        {
            // Row number greater than int.MaxValue should throw FormatException or OverflowException
            Assert.Throws<OverflowException>(() => OdfCellAddress.ParseExcel("A2147483648"));
            Assert.Throws<OverflowException>(() => OdfCellAddress.ParseExcel("A999999999999"));

            // Column names that cause integer overflow
            // e.g. a huge column name like 20 Z's will overflow standard 32-bit int calculations
            string hugeCol = new string('Z', 20) + "1";
            try
            {
                var addr = OdfCellAddress.ParseExcel(hugeCol);
                // If it doesn't throw, verify it did not result in a negative column index
                Assert.True(addr.Column >= 0);
            }
            catch (ArgumentOutOfRangeException)
            {
                // Thrown because wrapped value was negative
            }
            catch (OverflowException)
            {
                // Thrown if checked arithmetic was used
            }
        }

        [Fact]
        public void TestInvalidCoordinates()
        {
            // Negative rows/columns cannot exist in the standard syntax (hyphen '-' is not a letter or digit)
            Assert.Throws<FormatException>(() => OdfCellAddress.ParseExcel("A-1"));
            Assert.Throws<FormatException>(() => OdfCellAddress.ParseExcel("-A1"));

            // Row index must be positive (0 is parsed but throws ArgumentOutOfRangeException in constructor)
            Assert.Throws<ArgumentOutOfRangeException>(() => OdfCellAddress.ParseExcel("A0"));
            Assert.Throws<ArgumentOutOfRangeException>(() => OdfCellAddress.ParseOdf(".A0"));

            // Incomplete/malformed address strings
            Assert.Throws<FormatException>(() => OdfCellAddress.ParseExcel("XFD"));
            Assert.Throws<FormatException>(() => OdfCellAddress.ParseExcel("1048576"));
            Assert.Throws<FormatException>(() => OdfCellAddress.ParseExcel("$$A1"));
            Assert.Throws<FormatException>(() => OdfCellAddress.ParseExcel("A$$1"));
            Assert.Throws<FormatException>(() => OdfCellAddress.ParseExcel(""));
        }

        [Fact]
        public void TestSheetNameEscapingAndFormatting()
        {
            // Sheet name with spaces
            var addr1 = OdfCellAddress.ParseExcel("'Sheet One'!B2");
            Assert.Equal("Sheet One", addr1.SheetName);
            Assert.Equal("'Sheet One'!B2", addr1.ToExcelString());
            Assert.Equal("'Sheet One'.B2", addr1.ToOdfString(false));

            // Sheet name with single quotes escaped as double single-quotes
            var addr2 = OdfCellAddress.ParseExcel("'My ''Special'' Sheet'!C3");
            Assert.Equal("My 'Special' Sheet", addr2.SheetName);
            Assert.Equal("'My ''Special'' Sheet'!C3", addr2.ToExcelString());
            Assert.Equal("'My ''Special'' Sheet'.C3", addr2.ToOdfString(false));

            // Sheet name with dots, exclamations, hyphens
            var addr3 = OdfCellAddress.ParseExcel("Sheet.Dot!D4");
            Assert.Equal("Sheet.Dot", addr3.SheetName);
            Assert.Equal("Sheet.Dot!D4", addr3.ToExcelString()); // Dot does not trigger quotes in Excel
            Assert.Equal("'Sheet.Dot'.D4", addr3.ToOdfString(false)); // Dot triggers quotes in ODF

            var addr4 = OdfCellAddress.ParseExcel("'Sheet!Ex'!E5");
            Assert.Equal("Sheet!Ex", addr4.SheetName);
            Assert.Equal("'Sheet!Ex'!E5", addr4.ToExcelString());
            Assert.Equal("Sheet!Ex.E5", addr4.ToOdfString(false)); // Exclamation does not trigger quotes in ODF

            // Absolute sheet prefix ($)
            var addr5 = OdfCellAddress.ParseOdf("$Sheet1.A1");
            Assert.Equal("Sheet1", addr5.SheetName);
            Assert.True(addr5.IsSheetAbsolute);
            Assert.Equal("Sheet1!A1", addr5.ToExcelString());
            Assert.Equal("$Sheet1.A1", addr5.ToOdfString(false));
            Assert.Equal("[$Sheet1.A1]", addr5.ToOdfString(true));
        }

        [Fact]
        public void TestUnbalancedSheetQuotes()
        {
            // The parser doesn't enforce balanced quotes on sheet names; it simply preserves the single quote as part of the sheet name.
            var addr1 = OdfCellAddress.ParseExcel("'Sheet1!A1");
            Assert.Equal("'Sheet1", addr1.SheetName);
            Assert.Equal(0, addr1.Row);
            Assert.Equal(0, addr1.Column);

            var addr2 = OdfCellAddress.ParseExcel("Sheet1'!A1");
            Assert.Equal("Sheet1'", addr2.SheetName);
            Assert.Equal(0, addr2.Row);
            Assert.Equal(0, addr2.Column);
        }

        #endregion

        #region 2. Range Parsing and Boundary Stress Tests

        [Fact]
        public void TestReversedCoordinatesInRanges()
        {
            // Standard Range: A1:C3
            var normalRange = OdfCellRange.ParseExcel("A1:C3");
            Assert.Equal(0, normalRange.StartAddress.Row);
            Assert.Equal(0, normalRange.StartAddress.Column);
            Assert.Equal(2, normalRange.EndAddress.Row);
            Assert.Equal(2, normalRange.EndAddress.Column);

            // Reversed Range: C3:A1
            var revRange = OdfCellRange.ParseExcel("C3:A1");
            Assert.Equal(2, revRange.StartAddress.Row);
            Assert.Equal(2, revRange.StartAddress.Column);
            Assert.Equal(0, revRange.EndAddress.Row);
            Assert.Equal(0, revRange.EndAddress.Column);

            // Verify contains and intersects are order-invariant
            var checkCell1 = OdfCellAddress.ParseExcel("B2");
            var checkCell2 = OdfCellAddress.ParseExcel("D4");

            Assert.True(normalRange.Contains(checkCell1));
            Assert.True(revRange.Contains(checkCell1));

            Assert.False(normalRange.Contains(checkCell2));
            Assert.False(revRange.Contains(checkCell2));

            // Intersection with fully inside range
            var insideRange = OdfCellRange.ParseExcel("B2:B2");
            Assert.True(normalRange.Intersects(insideRange));
            Assert.True(revRange.Intersects(insideRange));

            // Intersection with edge-overlapping range
            var overlapRange = OdfCellRange.ParseExcel("C3:E5");
            Assert.True(normalRange.Intersects(overlapRange));
            Assert.True(revRange.Intersects(overlapRange));
        }

        [Fact]
        public void TestRangeMaxBoundaries()
        {
            // When start and end are equal, formatting to Excel representation simplifies to a single cell address, but Odf representations do not.
            var range = OdfCellRange.ParseExcel("XFD1048576:XFD1048576");
            Assert.Equal(1048575, range.StartAddress.Row);
            Assert.Equal(16383, range.StartAddress.Column);
            Assert.Equal(1048575, range.EndAddress.Row);
            Assert.Equal(16383, range.EndAddress.Column);

            Assert.Equal("XFD1048576", range.ToExcelString());
            Assert.Equal(".XFD1048576:.XFD1048576", range.ToOdfString(false));
            Assert.Equal("[.XFD1048576:.XFD1048576]", range.ToOdfString(true));
        }

        [Fact]
        public void TestRangeDifferentSheetsIntersection()
        {
            var range1 = OdfCellRange.ParseExcel("Sheet1!A1:B2");
            var range2 = OdfCellRange.ParseExcel("Sheet2!A1:B2");

            // Even if coordinates overlap, different sheets should mean NO intersection or containment
            Assert.False(range1.Intersects(range2));
            Assert.False(range1.Contains(range2.StartAddress));
        }

        [Fact]
        public void TestMalformedRangeStrings()
        {
            Assert.Throws<FormatException>(() => OdfCellRange.ParseExcel("A1::B2"));
            Assert.Throws<FormatException>(() => OdfCellRange.ParseExcel(":"));
            Assert.Throws<FormatException>(() => OdfCellRange.ParseExcel("A1:B"));
            Assert.Throws<FormatException>(() => OdfCellRange.ParseExcel("A1:2"));
        }

        #endregion

        #region 3. Formula Translation Stress Tests (Excel <-> ODF)

        [Fact]
        public void TestDeeplyNestedFormulas()
        {
            // Test deeply nested IF statements to verify stack/recursion limits are handled
            int nestingDepth = 150;
            var sbExcel = new StringBuilder("=IF(A1=1, 1");
            for (int i = 2; i <= nestingDepth; i++)
            {
                sbExcel.Append($", IF(A1={i}, {i}");
            }
            sbExcel.Append(", 0");
            for (int i = 1; i <= nestingDepth; i++)
            {
                sbExcel.Append(")");
            }

            string originalExcel = sbExcel.ToString();

            // Translate to ODF
            string odfFormula = OdfFormulaTranslator.ExcelToOdfFormula(originalExcel);
            Assert.StartsWith("oooc:=IF([.A1]=1; 1; IF([.A1]=2; 2;", odfFormula);

            // Translate back and assert round-trip equality
            string roundTripExcel = OdfFormulaTranslator.OdfToExcelFormula(odfFormula);
            Assert.Equal(originalExcel, roundTripExcel);
        }

        [Fact]
        public void TestLargeFormulaExpression()
        {
            // Generate a formula with 800 addition operations
            int additionTerms = 800;
            var sb = new StringBuilder("=A1");
            for (int i = 2; i <= additionTerms; i++)
            {
                sb.Append($"+A{i}");
            }
            string originalExcel = sb.ToString();

            string odfFormula = OdfFormulaTranslator.ExcelToOdfFormula(originalExcel);
            Assert.StartsWith("oooc:=[.A1]+[.A2]", odfFormula);

            string roundTripExcel = OdfFormulaTranslator.OdfToExcelFormula(odfFormula);
            Assert.Equal(originalExcel, roundTripExcel);
        }

        [Fact]
        public void TestFunctionCaseNormalization()
        {
            // Lowercase function names should be normalized to uppercase when followed by '('
            string excelInput = "=sum(a1:b2) + average(c3, d4) + lowercasefunc(1, 2)";
            string expectedOdf = "oooc:=SUM([.A1:.B2]) + AVERAGE([.C3]; [.D4]) + LOWERCASEFUNC(1; 2)";

            string odfResult = OdfFormulaTranslator.ExcelToOdfFormula(excelInput);
            Assert.Equal(expectedOdf, odfResult);

            // Translate back
            string roundTrip = OdfFormulaTranslator.OdfToExcelFormula(odfResult);
            Assert.Equal("=SUM(A1:B2) + AVERAGE(C3, D4) + LOWERCASEFUNC(1, 2)", roundTrip);
        }

        [Fact]
        public void TestLiteralStringSeparatorsNotConverted()
        {
            // Commas/semicolons inside string literals MUST NOT be converted
            string excelInput = "=CONCAT(\"Hello, World!\", \";\", \"foo,bar\")";
            string expectedOdf = "oooc:=CONCAT(\"Hello, World!\"; \";\"; \"foo,bar\")";

            string odfResult = OdfFormulaTranslator.ExcelToOdfFormula(excelInput);
            Assert.Equal(expectedOdf, odfResult);

            string roundTrip = OdfFormulaTranslator.OdfToExcelFormula(odfResult);
            Assert.Equal(excelInput, roundTrip);
        }

        [Fact]
        public void TestEscapedQuotesInStringLiterals()
        {
            string excelInput = "=IF(A1=\"\", \"Empty \"\"Value\"\"\", \"Non-Empty\")";
            string expectedOdf = "oooc:=IF([.A1]=\"\"; \"Empty \"\"Value\"\"\"; \"Non-Empty\")";

            string odfResult = OdfFormulaTranslator.ExcelToOdfFormula(excelInput);
            Assert.Equal(expectedOdf, odfResult);

            string roundTrip = OdfFormulaTranslator.OdfToExcelFormula(odfResult);
            Assert.Equal(excelInput, roundTrip);
        }

        [Fact]
        public void TestInlineArraysRoundTrip()
        {
            // Inline array constants in Excel use curly braces {}
            string excelInput = "=SUM({1,2;3,4} * A1)";
            string expectedOdf = "oooc:=SUM({1;2;3;4} * [.A1])"; // Note: the separator converter currently maps both ',' and ';' to ';'

            string odfResult = OdfFormulaTranslator.ExcelToOdfFormula(excelInput);
            Assert.Equal(expectedOdf, odfResult);

            // Since both were mapped to ';', the round trip will convert all to ','
            string roundTrip = OdfFormulaTranslator.OdfToExcelFormula(odfResult);
            Assert.Equal("=SUM({1,2,3,4} * A1)", roundTrip);
        }

        [Fact]
        public void TestFallbackOnMalformedCellReferences()
        {
            // References that fail to parse (e.g. invalid syntax) should fall back gracefully
            string excelInput = "=A1 + [InvalidRef] + B2";
            string odfResult = OdfFormulaTranslator.ExcelToOdfFormula(excelInput);
            Assert.Contains("[InvalidRef]", odfResult);
            Assert.Contains("[.A1]", odfResult);
            Assert.Contains("[.B2]", odfResult);
        }

        #endregion

        #region 4. TranslateFormulaOffset (Absolute/Relative Shift) Stress Tests

        [Fact]
        public void TestOffsetShiftingAbsoluteAndRelativeCombinations()
        {
            // A1 is fully relative -> should shift row and column
            Assert.Equal("=B2", OdfFormulaTranslator.TranslateFormulaOffset("=A1", 1, 1));

            // $A$1 is fully absolute -> should NOT shift
            Assert.Equal("=$A$1", OdfFormulaTranslator.TranslateFormulaOffset("=$A$1", 5, 10));

            // A$1 has absolute row -> shifts column, row remains 1
            Assert.Equal("=K$1", OdfFormulaTranslator.TranslateFormulaOffset("=A$1", 15, 10));

            // $A1 has absolute col -> shifts row, col remains A
            Assert.Equal("=$A16", OdfFormulaTranslator.TranslateFormulaOffset("=$A1", 15, 10));
        }

        [Fact]
        public void TestOffsetShiftingNegativeValid()
        {
            // Shifts that decrease coordinates but remain within bounds (>=0)
            Assert.Equal("=A1", OdfFormulaTranslator.TranslateFormulaOffset("=C3", -2, -2));
            Assert.Equal("=Sheet1!A1", OdfFormulaTranslator.TranslateFormulaOffset("=Sheet1!B2", -1, -1));
        }

        [Fact]
        public void TestOffsetShiftingOutOfBoundsRefPropagation()
        {
            // Shifts that go negative (out of bounds) must return REF error
            Assert.Equal("=#REF!", OdfFormulaTranslator.TranslateFormulaOffset("=A1", -1, 0));
            Assert.Equal("=#REF!", OdfFormulaTranslator.TranslateFormulaOffset("=A1", 0, -1));
            Assert.Equal("=#REF!", OdfFormulaTranslator.TranslateFormulaOffset("=A1", -10, -10));

            // Absolute row/col shouldn't shift and shouldn't trigger REF error even if offset is negative
            Assert.Equal("=$A$1", OdfFormulaTranslator.TranslateFormulaOffset("=$A$1", -5, -5));
            Assert.Equal("=$A2", OdfFormulaTranslator.TranslateFormulaOffset("=$A3", -1, -5)); // col A is absolute, row shifts from 3 to 2. No REF.

            // Range offset out of bounds
            Assert.Equal("=#REF!", OdfFormulaTranslator.TranslateFormulaOffset("=B2:C3", -2, 0)); // B2 shifts to row -1 (out of bounds)
            Assert.Equal("=#REF!", OdfFormulaTranslator.TranslateFormulaOffset("=B2:C3", 0, -2)); // B2 shifts to col -1 (out of bounds)

            // ODF style out of bounds
            Assert.Equal("oooc:=[.#REF!]", OdfFormulaTranslator.TranslateFormulaOffset("oooc:=[.A1]", -1, 0));
            Assert.Equal("oooc:=[.#REF!]", OdfFormulaTranslator.TranslateFormulaOffset("oooc:=[.B2:.C3]", -2, 0));
        }

        [Fact]
        public void TestOffsetShiftingAtExcelMaxBoundaries()
        {
            // Row index 1048575, Column index 16383 is XFD1048576
            // Start at A1, shift to max boundary
            Assert.Equal("=XFD1048576", OdfFormulaTranslator.TranslateFormulaOffset("=A1", 1048575, 16383));

            // Shift beyond max boundary (XFD column index 16383 + 1 = 16384 = XFE)
            // Notice: the system allows columns beyond XFD since column indexing uses integer values
            Assert.Equal("=XFE1", OdfFormulaTranslator.TranslateFormulaOffset("=A1", 0, 16384));

            // Shifts resulting in extreme overflows (e.g. column index wraps to negative)
            // It should be caught by newCol < 0 and return REF error
            Assert.Equal("=#REF!", OdfFormulaTranslator.TranslateFormulaOffset("=A1", 0, int.MinValue));
            Assert.Equal("=#REF!", OdfFormulaTranslator.TranslateFormulaOffset("=A1", int.MinValue, 0));
        }

        #endregion
    }
}
