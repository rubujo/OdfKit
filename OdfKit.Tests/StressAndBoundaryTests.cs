using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using OdfKit.Spreadsheet;
using OdfKit.Formula;

namespace OdfKit.Tests
{
    public class StressAndBoundaryTests
    {
        #region 1. Coordinate Parsing Boundaries and Edge Cases

        [Fact]
        public void TestExcelBoundaryCoordinates()
        {
            // Test Excel max row and column
            // Row 1048576 (0-based 1048575)
            // Column 16384 (XFD, 0-based 16383)
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
        public void TestAbsoluteExcelBoundaryCoordinates()
        {
            var addr = OdfCellAddress.ParseExcel("$XFD$1048576");
            Assert.Equal(1048575, addr.Row);
            Assert.Equal(16383, addr.Column);
            Assert.True(addr.IsRowAbsolute);
            Assert.True(addr.IsColumnAbsolute);

            Assert.Equal("$XFD$1048576", addr.ToExcelString());
            Assert.Equal(".$XFD$1048576", addr.ToOdfString(false));
        }

        [Fact]
        public void TestLargeCoordinatesRoundTrip()
        {
            // Row 5,000,000, Column 500,000
            var original = new OdfCellAddress(4999999, 499999);
            string excelStr = original.ToExcelString();

            var parsed = OdfCellAddress.ParseExcel(excelStr);
            Assert.Equal(original.Row, parsed.Row);
            Assert.Equal(original.Column, parsed.Column);

            string odfStr = original.ToOdfString(false);
            var parsedOdf = OdfCellAddress.ParseOdf(odfStr);
            Assert.Equal(original.Row, parsedOdf.Row);
            Assert.Equal(original.Column, parsedOdf.Column);
        }

        [Fact]
        public void TestInvalidCoordinateParsing()
        {
            // Empty
            Assert.Throws<FormatException>(() => OdfCellAddress.ParseExcel(""));
            // Missing row
            Assert.Throws<FormatException>(() => OdfCellAddress.ParseExcel("A"));
            // Missing column
            Assert.Throws<FormatException>(() => OdfCellAddress.ParseExcel("1"));
            // Invalid characters
            Assert.Throws<FormatException>(() => OdfCellAddress.ParseExcel("A-1"));
            Assert.Throws<FormatException>(() => OdfCellAddress.ParseExcel("-A1"));
            Assert.Throws<FormatException>(() => OdfCellAddress.ParseExcel("A1$"));
            Assert.Throws<FormatException>(() => OdfCellAddress.ParseExcel("$A$1$1"));
            Assert.Throws<FormatException>(() => OdfCellAddress.ParseExcel("A1:B2"));

            // Out of bounds (row 0 or negative rows/columns)
            Assert.Throws<ArgumentOutOfRangeException>(() => OdfCellAddress.ParseExcel("A0"));
            Assert.Throws<FormatException>(() => OdfCellAddress.ParseExcel("A-5")); // '-' is not parsed as digit, throws FormatException
        }

        [Fact]
        public void TestTryParseBehavior()
        {
            Assert.True(OdfCellAddress.TryParse("A1", out var addr1));
            Assert.Equal(0, addr1.Row);
            Assert.Equal(0, addr1.Column);

            Assert.True(OdfCellAddress.TryParse(".B2", out var addr2));
            Assert.Equal(1, addr2.Row);
            Assert.Equal(1, addr2.Column);

            Assert.False(OdfCellAddress.TryParse("invalid", out _));
            Assert.False(OdfCellAddress.TryParse("A0", out _));
            Assert.False(OdfCellAddress.TryParse("", out _));
        }

        [Fact]
        public void TestColumnLettersOverflow()
        {
            // Verify what happens when parsing extreme column strings.
            // E.g., a column name of 15 Z's will exceed int.MaxValue.
            // Since column index is 32-bit signed integer, unchecked arithmetic wraps.
            // Under silent overflow, it may result in a negative or positive column index.
            // If it wraps to a negative value, OdfCellAddress constructor throws ArgumentOutOfRangeException.
            // If it wraps to a non-negative value, it returns an incorrect/wrapped OdfCellAddress without throwing.
            // Let's assert that it either throws ArgumentOutOfRangeException due to negative index, or returns a wrapped address.
            string hugeCol = new string('Z', 15) + "1";
            try
            {
                var addr = OdfCellAddress.ParseExcel(hugeCol);
                // If it doesn't throw, the column index must be non-negative
                Assert.True(addr.Column >= 0);
            }
            catch (ArgumentOutOfRangeException)
            {
                // Threw as expected due to negative wrapped index
            }
        }

        #endregion

        #region 2. Range Parsing and Operations

        [Fact]
        public void TestInvalidRangeParsing()
        {
            Assert.Throws<FormatException>(() => OdfCellRange.ParseExcel(""));
            Assert.Throws<FormatException>(() => OdfCellRange.ParseExcel("A1:"));
            Assert.Throws<FormatException>(() => OdfCellRange.ParseExcel(":B2"));
            Assert.Throws<ArgumentOutOfRangeException>(() => OdfCellRange.ParseExcel("A0:B2"));
            Assert.Throws<ArgumentOutOfRangeException>(() => OdfCellRange.ParseExcel("A1:B0"));
        }

        [Fact]
        public void TestSingleCellRangeParsing()
        {
            // A single cell address should be parseable as a range (start == end)
            var range = OdfCellRange.ParseExcel("A1");
            Assert.Equal(range.StartAddress, range.EndAddress);
            Assert.Equal(0, range.StartAddress.Row);
            Assert.Equal(0, range.StartAddress.Column);
            Assert.Equal("A1", range.ToExcelString());
        }

        [Fact]
        public void TestQuotedSheetsInRanges()
        {
            var range = OdfCellRange.ParseExcel("'My ''Awesome'' Sheet'!A1:B2");
            Assert.Equal("My 'Awesome' Sheet", range.StartAddress.SheetName);
            Assert.Equal("My 'Awesome' Sheet", range.EndAddress.SheetName);
            Assert.Equal("'My ''Awesome'' Sheet'!A1:B2", range.ToExcelString());
        }

        [Fact]
        public void TestRangeContainsAndIntersects()
        {
            var range = OdfCellRange.ParseExcel("Sheet1!B2:D4");

            // Containment
            Assert.True(range.Contains(OdfCellAddress.ParseExcel("Sheet1!B2")));
            Assert.True(range.Contains(OdfCellAddress.ParseExcel("Sheet1!C3")));
            Assert.True(range.Contains(OdfCellAddress.ParseExcel("Sheet1!D4")));
            Assert.False(range.Contains(OdfCellAddress.ParseExcel("Sheet1!A1")));
            Assert.False(range.Contains(OdfCellAddress.ParseExcel("Sheet1!E5")));
            // Different sheet
            Assert.False(range.Contains(OdfCellAddress.ParseExcel("Sheet2!C3")));

            // Intersection
            Assert.True(range.Intersects(OdfCellRange.ParseExcel("Sheet1!A1:B2")));
            Assert.True(range.Intersects(OdfCellRange.ParseExcel("Sheet1!D4:E5")));
            Assert.True(range.Intersects(OdfCellRange.ParseExcel("Sheet1!C3:C3")));
            Assert.False(range.Intersects(OdfCellRange.ParseExcel("Sheet1!A1:A2")));
            Assert.False(range.Intersects(OdfCellRange.ParseExcel("Sheet2!B2:D4")));
        }

        #endregion

        #region 3. Structural Shifting Stress Tests

        [Fact]
        public void TestStructuralShiftingInsertions()
        {
            // Base range B2:D4
            var range = OdfCellRange.ParseExcel("B2:D4");

            // 1. Insert 2 rows at row 0 (before range) -> shifts down to B4:D6
            var shifted1 = range.ShiftStructural(insertRowIndex: 0, rowCount: 2, insertColIndex: 0, colCount: 0);
            Assert.Equal("B4:D6", shifted1.ToExcelString());

            // 2. Insert 2 columns at col 0 (before range) -> shifts right to D2:F4
            var shifted2 = range.ShiftStructural(insertRowIndex: 0, rowCount: 0, insertColIndex: 0, colCount: 2);
            Assert.Equal("D2:F4", shifted2.ToExcelString());

            // 3. Insert 2 rows at row 2 (inside range) -> start B2 stays, end D4 becomes D6 -> B2:D6
            var shifted3 = range.ShiftStructural(insertRowIndex: 2, rowCount: 2, insertColIndex: 0, colCount: 0);
            Assert.Equal("B2:D6", shifted3.ToExcelString());

            // 4. Insert 2 rows at row 4 (after range) -> stays B2:D4
            var shifted4 = range.ShiftStructural(insertRowIndex: 4, rowCount: 2, insertColIndex: 0, colCount: 0);
            Assert.Equal("B2:D4", shifted4.ToExcelString());
        }

        [Fact]
        public void TestStructuralShiftingDeletions()
        {
            // Base range B2:D4 (Row index 1 to 3, Column index 1 to 3)
            var range = OdfCellRange.ParseExcel("B2:D4");

            // 1. Delete 1 row at row 0 (before range) -> shifts up to B1:D3
            var shifted1 = range.ShiftStructural(insertRowIndex: 0, rowCount: -1, insertColIndex: 0, colCount: 0);
            Assert.Equal("B1:D3", shifted1.ToExcelString());

            // 2. Delete 1 row at row 1 (the start row) -> range shrinks to B2:D3 (index 1 to 2)
            var shifted2 = range.ShiftStructural(insertRowIndex: 1, rowCount: -1, insertColIndex: 0, colCount: 0);
            Assert.Equal("B2:D3", shifted2.ToExcelString());

            // 3. Delete 3 rows at row 1 (deletes entire range region and more) -> collapses to B2:D2 (single row 1)
            var shifted3 = range.ShiftStructural(insertRowIndex: 1, rowCount: -3, insertColIndex: 0, colCount: 0);
            Assert.Equal("B2:D2", shifted3.ToExcelString());

            // 4. Delete 1 row at row 4 (after range) -> stays B2:D4
            var shifted4 = range.ShiftStructural(insertRowIndex: 4, rowCount: -1, insertColIndex: 0, colCount: 0);
            Assert.Equal("B2:D4", shifted4.ToExcelString());
        }

        #endregion

        #region 4. Absolute and Relative Offset Shifting

        [Fact]
        public void TestFormulaOffsetShiftingRelativeAndAbsolute()
        {
            // Normal shift
            Assert.Equal("=B2", OdfFormulaTranslator.TranslateFormulaOffset("=A1", 1, 1));

            // Absolute row remains absolute, relative col shifts
            Assert.Equal("=B$1", OdfFormulaTranslator.TranslateFormulaOffset("=A$1", 5, 1));
            // Absolute col remains absolute, relative row shifts
            Assert.Equal("=$A2", OdfFormulaTranslator.TranslateFormulaOffset("=$A1", 1, 5));
            // Both absolute remain unchanged
            Assert.Equal("=$A$1", OdfFormulaTranslator.TranslateFormulaOffset("=$A$1", 5, 5));

            // ODF style shifts
            Assert.Equal("oooc:=[.B2]", OdfFormulaTranslator.TranslateFormulaOffset("oooc:=[.A1]", 1, 1));
            Assert.Equal("oooc:=[.$A$1]", OdfFormulaTranslator.TranslateFormulaOffset("oooc:=[.$A$1]", 2, 2));
        }

        [Fact]
        public void TestFormulaOffsetOutOfBoundsRefPropagation()
        {
            // Relative shifts out of bounds (negative coordinates)
            Assert.Equal("=#REF!", OdfFormulaTranslator.TranslateFormulaOffset("=A1", -1, 0));
            Assert.Equal("=#REF!", OdfFormulaTranslator.TranslateFormulaOffset("=A1", 0, -1));
            Assert.Equal("=#REF!", OdfFormulaTranslator.TranslateFormulaOffset("=A1", -1, -1));
            Assert.Equal("=#REF!", OdfFormulaTranslator.TranslateFormulaOffset("=B2:C3", -2, 0)); // B2 (row 1) - 2 = -1 (out of bounds)

            // ODF style out of bounds
            Assert.Equal("oooc:=[.#REF!]", OdfFormulaTranslator.TranslateFormulaOffset("oooc:=[.A1]", -1, 0));
            Assert.Equal("oooc:=[.#REF!]", OdfFormulaTranslator.TranslateFormulaOffset("oooc:=[.A1:.B2]", 0, -1));
        }

        [Fact]
        public void TestFormulaOffsetShiftingAtBoundaries()
        {
            // Shift to max Excel row/column
            // Row index 1048575, Column index 16383 is XFD1048576
            Assert.Equal("=XFD1048576", OdfFormulaTranslator.TranslateFormulaOffset("=A1", 1048575, 16383));

            // Shift beyond max Excel column (XFD is index 16383, shifting to 16384 doesn't throw in address construction, but let's see)
            string shiftedCol = OdfFormulaTranslator.TranslateFormulaOffset("=A1", 0, 16384);
            // Index 16384 column letters should be XFE
            Assert.Equal("=XFE1", shiftedCol);

            // Shifting extremely large index that triggers IndexToColumnName integer overflow
            // Column index int.MaxValue - 1 = 2147483646. If shifted by colOffset = int.MaxValue, we get an overflow to negative or huge number.
            // Let's verify that a shift resulting in negative column/row index is caught and returns #REF!
            Assert.Equal("=#REF!", OdfFormulaTranslator.TranslateFormulaOffset("=A1", 0, int.MinValue));
        }

        #endregion

        #region 5. Formula Parsing and Translation Stress Tests

        [Fact]
        public void TestDeepNestedFormulaTranslation()
        {
            // Generate a formula with 200 levels of nested IF statements
            // E.g., =IF(A1>0, IF(A1>1, IF(A1>2, ... 200, 0) ... 0)
            int levels = 200;
            var sbExcel = new StringBuilder("=IF(A1>0");
            for (int l = 1; l < levels; l++)
            {
                sbExcel.Append($", IF(A1>{l}");
            }
            sbExcel.Append($", {levels}");
            for (int l = 0; l < levels; l++)
            {
                sbExcel.Append(", 0)");
            }

            string originalExcel = sbExcel.ToString();

            // Translate to ODF
            string odfFormula = OdfFormulaTranslator.ExcelToOdfFormula(originalExcel);
            Assert.StartsWith("oooc:=IF([.A1]>0;", odfFormula);
            Assert.EndsWith("; 0)", odfFormula);

            // Translate back to Excel
            string roundTripExcel = OdfFormulaTranslator.OdfToExcelFormula(odfFormula);
            Assert.Equal(originalExcel, roundTripExcel);
        }

        [Fact]
        public void TestLargeFormulaExpressionTranslation()
        {
            // Generate a formula with 500 additions: =A1+A2+A3+...+A500
            int termCount = 500;
            var sb = new StringBuilder("=A1");
            for (int t = 2; t <= termCount; t++)
            {
                sb.Append($"+A{t}");
            }
            string originalExcel = sb.ToString();

            // Translate to ODF
            string odfFormula = OdfFormulaTranslator.ExcelToOdfFormula(originalExcel);
            Assert.StartsWith("oooc:=[.A1]+[.A2]", odfFormula);

            // Translate back
            string roundTripExcel = OdfFormulaTranslator.OdfToExcelFormula(odfFormula);
            Assert.Equal(originalExcel, roundTripExcel);
        }

        [Fact]
        public void TestFormulaQuotesAndEscaping()
        {
            // Excel formula with string literals and double quote escaping
            string excel = "=CONCAT(\"Hello, \"\"world\"\"!\", A1, \" ; [B2] \")";
            string odf = OdfFormulaTranslator.ExcelToOdfFormula(excel);

            // Brackets inside string literals should not be parsed as cell references!
            // Let's verify that the translated ODF formula does not convert [B2] into a reference
            Assert.Contains("\" ; [B2] \"", odf);
            Assert.Equal("oooc:=CONCAT(\"Hello, \"\"world\"\"!\"; [.A1]; \" ; [B2] \")", odf);

            string roundTrip = OdfFormulaTranslator.OdfToExcelFormula(odf);
            Assert.Equal(excel, roundTrip);
        }

        [Fact]
        public void TestComplexFormulaTranslationRoundTrip()
        {
            string[] complexFormulas = {
                "=SUM(AVERAGE(A1:B2), IF(C1>0, D1, E1))",
                "=VLOOKUP(\"Banana\", 'My Sheet'!$A$1:$C$100, 3, FALSE)",
                "=COUNTIF(Sheet1!A1:C10, \">15\")",
                "=AND(A1>0, OR(B1<0, C1=10))",
                "=CONCAT(LEFT(A1, 3), MID(B2, 2, 4), RIGHT(C3, 1))"
            };

            foreach (var excel in complexFormulas)
            {
                string odf = OdfFormulaTranslator.ExcelToOdfFormula(excel);
                string roundTrip = OdfFormulaTranslator.OdfToExcelFormula(odf);
                Assert.Equal(excel, roundTrip);
            }
        }

        #endregion
    }
}
