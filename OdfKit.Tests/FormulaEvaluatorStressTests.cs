using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Xunit;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Spreadsheet;
using OdfKit.Formula;
using OdfKit.Styles;

namespace OdfKit.Tests
{
    public class FormulaEvaluatorStressTests
    {
        #region Stress Mock IEvaluationContext

        private class StressMockEvaluationContext : IEvaluationContext
        {
            public OdfCellAddress CurrentCell { get; set; }
            public Dictionary<OdfCellAddress, object?> CellValues { get; } = new();
            public Dictionary<OdfCellAddress, string> CellFormulas { get; } = new();
            public DefaultFormulaEvaluator? Evaluator { get; set; }

            public object GetCellValue(OdfCellAddress address)
            {
                // If there's an uncalculated formula, trigger its evaluation.
                if (CellFormulas.TryGetValue(address, out var formula))
                {
                    if (Evaluator != null)
                    {
                        var oldCell = CurrentCell;
                        CurrentCell = address;
                        try
                        {
                            return Evaluator.EvaluateCell(address, this);
                        }
                        finally
                        {
                            CurrentCell = oldCell;
                        }
                    }
                }
                if (CellValues.TryGetValue(address, out var val))
                {
                    return val!;
                }
                return null!; // Return null for empty/blank cell
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

        #region 1. Circular Reference Detection

        [Fact]
        public void TestCircularDependency_SelfReference()
        {
            var context = new StressMockEvaluationContext();
            var evaluator = new DefaultFormulaEvaluator();
            context.Evaluator = evaluator;

            var a1 = OdfCellAddress.ParseExcel("A1");
            context.CellFormulas[a1] = "=A1";

            var result = evaluator.EvaluateCell(a1, context);
            Assert.True(result is OdfFormulaError, "Should return OdfFormulaError");
            Assert.Equal(OdfFormulaErrorType.Ref, ((OdfFormulaError)result).ErrorType);
        }

        [Fact]
        public void TestCircularDependency_IndirectCycle_Length3()
        {
            var context = new StressMockEvaluationContext();
            var evaluator = new DefaultFormulaEvaluator();
            context.Evaluator = evaluator;

            var a1 = OdfCellAddress.ParseExcel("A1");
            var b1 = OdfCellAddress.ParseExcel("B1");
            var c1 = OdfCellAddress.ParseExcel("C1");

            context.CellFormulas[a1] = "=B1";
            context.CellFormulas[b1] = "=C1";
            context.CellFormulas[c1] = "=A1";

            var result = evaluator.EvaluateCell(a1, context);
            Assert.True(result is OdfFormulaError);
            Assert.Equal(OdfFormulaErrorType.Ref, ((OdfFormulaError)result).ErrorType);

            // B1 and C1 should also evaluate to REF error
            var resultB = evaluator.EvaluateCell(b1, context);
            Assert.True(resultB is OdfFormulaError);
            Assert.Equal(OdfFormulaErrorType.Ref, ((OdfFormulaError)resultB).ErrorType);
        }

        [Fact]
        public void TestCircularDependency_IndirectCycle_Length10()
        {
            var context = new StressMockEvaluationContext();
            var evaluator = new DefaultFormulaEvaluator();
            context.Evaluator = evaluator;

            for (int i = 1; i <= 9; i++)
            {
                var current = OdfCellAddress.ParseExcel($"A{i}");
                var next = OdfCellAddress.ParseExcel($"A{i + 1}");
                context.CellFormulas[current] = $"=A{i + 1}";
            }
            context.CellFormulas[OdfCellAddress.ParseExcel("A10")] = "=A1";

            var result = evaluator.EvaluateCell(OdfCellAddress.ParseExcel("A1"), context);
            Assert.True(result is OdfFormulaError);
            Assert.Equal(OdfFormulaErrorType.Ref, ((OdfFormulaError)result).ErrorType);
        }

        [Fact]
        public void TestCircularDependency_MultiplePaths()
        {
            var context = new StressMockEvaluationContext();
            var evaluator = new DefaultFormulaEvaluator();
            context.Evaluator = evaluator;

            var a1 = OdfCellAddress.ParseExcel("A1");
            var b1 = OdfCellAddress.ParseExcel("B1");
            var c1 = OdfCellAddress.ParseExcel("C1");

            context.CellFormulas[a1] = "=B1 + C1";
            context.CellFormulas[b1] = "=C1";
            context.CellFormulas[c1] = "=A1";

            var result = evaluator.EvaluateCell(a1, context);
            Assert.True(result is OdfFormulaError);
            Assert.Equal(OdfFormulaErrorType.Ref, ((OdfFormulaError)result).ErrorType);
        }

        [Fact]
        public void TestCircularDependency_Propagation()
        {
            var context = new StressMockEvaluationContext();
            var evaluator = new DefaultFormulaEvaluator();
            context.Evaluator = evaluator;

            var a1 = OdfCellAddress.ParseExcel("A1");
            var b1 = OdfCellAddress.ParseExcel("B1");
            var c1 = OdfCellAddress.ParseExcel("C1"); // propagation cell

            context.CellFormulas[a1] = "=B1";
            context.CellFormulas[b1] = "=A1";
            context.CellFormulas[c1] = "=A1 + 5";

            var result = evaluator.EvaluateCell(c1, context);
            // c1 references A1 which is circular. Does c1 receive the REF error or propagate?
            Assert.True(result is OdfFormulaError);
            Assert.Equal(OdfFormulaErrorType.Ref, ((OdfFormulaError)result).ErrorType);
        }

        [Fact]
        public void TestCircularDependency_RecoveryAfterClearCache()
        {
            var context = new StressMockEvaluationContext();
            var evaluator = new DefaultFormulaEvaluator();
            context.Evaluator = evaluator;

            var a1 = OdfCellAddress.ParseExcel("A1");
            var b1 = OdfCellAddress.ParseExcel("B1");

            context.CellFormulas[a1] = "=B1";
            context.CellFormulas[b1] = "=A1";

            // 1. Evaluate circular ref (should return REF)
            var result1 = evaluator.EvaluateCell(a1, context);
            Assert.True(result1 is OdfFormulaError);
            Assert.Equal(OdfFormulaErrorType.Ref, ((OdfFormulaError)result1).ErrorType);

            // 2. Break cycle by setting B1 to static
            context.CellFormulas.Remove(b1);
            context.CellValues[b1] = 10.0;

            // 3. Clear cache and re-evaluate
            evaluator.ClearCache();
            var result2 = evaluator.EvaluateCell(a1, context);
            Assert.Equal(10.0, result2);
        }

        #endregion

        #region 2. Division by Zero

        [Fact]
        public void TestDivisionByZero_DirectAndIndirect()
        {
            var context = new StressMockEvaluationContext();
            var evaluator = new DefaultFormulaEvaluator();
            context.Evaluator = evaluator;

            // Direct literal division
            var res1 = evaluator.Evaluate("5 / 0", context);
            Assert.True(res1 is OdfFormulaError);
            Assert.Equal(OdfFormulaErrorType.Div0, ((OdfFormulaError)res1).ErrorType);

            // Division of zero by zero
            var res2 = evaluator.Evaluate("0 / 0", context);
            Assert.True(res2 is OdfFormulaError);
            Assert.Equal(OdfFormulaErrorType.Div0, ((OdfFormulaError)res2).ErrorType);

            // Division of negative by zero
            var res3 = evaluator.Evaluate("-10 / 0", context);
            Assert.True(res3 is OdfFormulaError);
            Assert.Equal(OdfFormulaErrorType.Div0, ((OdfFormulaError)res3).ErrorType);

            // Indirect cell reference division
            var a1 = OdfCellAddress.ParseExcel("A1");
            var b1 = OdfCellAddress.ParseExcel("B1");
            context.CellValues[a1] = 15.0;
            context.CellValues[b1] = 0.0;
            context.CellFormulas[OdfCellAddress.ParseExcel("C1")] = "=A1/B1";

            var resC = evaluator.EvaluateCell(OdfCellAddress.ParseExcel("C1"), context);
            Assert.True(resC is OdfFormulaError);
            Assert.Equal(OdfFormulaErrorType.Div0, ((OdfFormulaError)resC).ErrorType);
        }

        [Fact]
        public void TestDivisionByZero_Nested()
        {
            var context = new StressMockEvaluationContext();
            var evaluator = new DefaultFormulaEvaluator();
            context.Evaluator = evaluator;

            var result = evaluator.Evaluate("10 / (2 * 5 - 10)", context);
            Assert.True(result is OdfFormulaError);
            Assert.Equal(OdfFormulaErrorType.Div0, ((OdfFormulaError)result).ErrorType);
        }

        [Fact]
        public void TestDivisionByZero_PropagationInMath()
        {
            var context = new StressMockEvaluationContext();
            var evaluator = new DefaultFormulaEvaluator();
            context.Evaluator = evaluator;

            // Propagation through addition
            var res1 = evaluator.Evaluate("(5 / 0) + 10", context);
            Assert.True(res1 is OdfFormulaError);
            Assert.Equal(OdfFormulaErrorType.Div0, ((OdfFormulaError)res1).ErrorType);

            // Propagation through multiplication
            var res2 = evaluator.Evaluate("0 * (5 / 0)", context);
            Assert.True(res2 is OdfFormulaError);
            Assert.Equal(OdfFormulaErrorType.Div0, ((OdfFormulaError)res2).ErrorType);
        }

        [Fact]
        public void TestDivisionByZero_InStatisticalFunctions()
        {
            var context = new StressMockEvaluationContext();
            var evaluator = new DefaultFormulaEvaluator();
            context.Evaluator = evaluator;

            var a1 = OdfCellAddress.ParseExcel("A1");
            var a2 = OdfCellAddress.ParseExcel("A2");
            var a3 = OdfCellAddress.ParseExcel("A3");

            context.CellValues[a1] = 10.0;
            context.CellFormulas[a2] = "=5/0"; // Div0 error inside range
            context.CellValues[a3] = 20.0;

            // Evaluate A2 to populate context formula
            var a2Val = evaluator.EvaluateCell(a2, context);
            Assert.True(a2Val is OdfFormulaError);

            // SUM(A1:A3)
            var sumRes = evaluator.Evaluate("SUM(A1:A3)", context);
            // Note: Under the current implementation, SUM skips OdfFormulaError values inside cell ranges!
            // Let's document this behavior. Currently, it evaluates to 30.0 because it skips A2.
            Assert.Equal(30.0, sumRes);

            // AVERAGE(A1:A3)
            var avgRes = evaluator.Evaluate("AVERAGE(A1:A3)", context);
            // Currently AVERAGE skips A2 too, so sum is 30, count is 2 -> 15.0
            Assert.Equal(15.0, avgRes);
        }

        #endregion

        #region 3. Type Mismatches

        [Fact]
        public void TestTypeMismatch_MathOperations()
        {
            var context = new StressMockEvaluationContext();
            var evaluator = new DefaultFormulaEvaluator();
            context.Evaluator = evaluator;

            // String addition (non-coercible)
            var res1 = evaluator.Evaluate("\"hello\" + 5", context);
            Assert.True(res1 is OdfFormulaError);
            Assert.Equal(OdfFormulaErrorType.Value, ((OdfFormulaError)res1).ErrorType);

            // Coercible string addition
            var res2 = evaluator.Evaluate("\"123\" + 5", context);
            Assert.Equal(128.0, res2);

            // Boolean addition
            var res3 = evaluator.Evaluate("TRUE + 5", context);
            Assert.Equal(6.0, res3); // TRUE coerces to 1.0

            var res4 = evaluator.Evaluate("FALSE * 10", context);
            Assert.Equal(0.0, res4); // FALSE coerces to 0.0
        }

        [Fact]
        public void TestTypeMismatch_Comparisons()
        {
            var context = new StressMockEvaluationContext();
            var evaluator = new DefaultFormulaEvaluator();
            context.Evaluator = evaluator;

            // String vs Number (non-coercible)
            // Let's check how the system behaves. We predict it falls back to string comparison of "abc" vs "5".
            // "abc" > "5" is True
            var res1 = evaluator.Evaluate("\"abc\" > 5", context);
            Assert.Equal(true, res1);

            // Coercible string vs Number
            // "10" vs 5 -> "10" is coerced to 10.0, 10.0 > 5.0 is True
            var res2 = evaluator.Evaluate("\"10\" > 5", context);
            Assert.Equal(true, res2);

            // Boolean vs Boolean
            var res3 = evaluator.Evaluate("TRUE > FALSE", context);
            Assert.Equal(true, res3);
        }

        [Fact]
        public void TestTypeMismatch_LogicalFunctions()
        {
            var context = new StressMockEvaluationContext();
            var evaluator = new DefaultFormulaEvaluator();
            context.Evaluator = evaluator;

            // AND with non-logical string literal
            var res1 = evaluator.Evaluate("AND(\"hello\", TRUE)", context);
            // Current behavior: "hello" is skipped because TryCoerceToBool returns false. 
            // TRUE is coerced to true. So hasLogical=true, result=true.
            Assert.Equal(true, res1);

            // AND with only non-logical values
            var res2 = evaluator.Evaluate("AND(\"hello\", \"world\")", context);
            Assert.True(res2 is OdfFormulaError);
            Assert.Equal(OdfFormulaErrorType.Value, ((OdfFormulaError)res2).ErrorType);
        }

        #endregion

        #region 4. Empty Values (Nulls)

        [Fact]
        public void TestEmptyValues_Arithmetic()
        {
            var context = new StressMockEvaluationContext();
            var evaluator = new DefaultFormulaEvaluator();
            context.Evaluator = evaluator;

            var a1 = OdfCellAddress.ParseExcel("A1"); // empty
            var b1 = OdfCellAddress.ParseExcel("B1"); // empty

            // CellAddressNode.Evaluate returns null for empty cells.
            // Let's verify that adding two null cells returns #VALUE! because TryCoerceDouble returns false.
            context.CellValues.Remove(a1);
            context.CellValues.Remove(b1);

            var res1 = evaluator.Evaluate("A1 + B1", context);
            Assert.True(res1 is OdfFormulaError);
            Assert.Equal(OdfFormulaErrorType.Value, ((OdfFormulaError)res1).ErrorType);
        }

        [Fact]
        public void TestEmptyValues_Concatenation_ThrowsException()
        {
            var context = new StressMockEvaluationContext();
            var evaluator = new DefaultFormulaEvaluator();
            context.Evaluator = evaluator;

            var a1 = OdfCellAddress.ParseExcel("A1");
            context.CellValues[a1] = null; // simulate blank cell returning null

            // Evaluating "A1 & \"abc\""
            // Let's verify if this throws NullReferenceException. 
            // In C# we can catch standard exception or verify it handles it by returning OdfFormulaError.Value inside Parse/Evaluate block.
            // Wait, Evaluate method catches exceptions and returns OdfFormulaError.Value!
            // Let's check DefaultFormulaEvaluator.Evaluate:
            // catch (Exception ex) { return OdfFormulaError.Value; }
            // So indeed, it doesn't crash the program; it gracefully returns OdfFormulaError.Value.
            var result = evaluator.Evaluate("A1 & \"abc\"", context);
            Assert.True(result is OdfFormulaError);
            Assert.Equal(OdfFormulaErrorType.Value, ((OdfFormulaError)result).ErrorType);
        }

        [Fact]
        public void TestEmptyValues_Comparisons_ThrowsException()
        {
            var context = new StressMockEvaluationContext();
            var evaluator = new DefaultFormulaEvaluator();
            context.Evaluator = evaluator;

            var a1 = OdfCellAddress.ParseExcel("A1");
            context.CellValues[a1] = null; // blank

            // Evaluating "A1 > 0"
            // In BinaryNode.EvaluateComparison, if A1 is null, it goes to string comparison and calls left.ToString() which throws NullReferenceException.
            // It is caught by Evaluate and returns OdfFormulaError.Value.
            var result = evaluator.Evaluate("A1 > 0", context);
            Assert.True(result is OdfFormulaError);
            Assert.Equal(OdfFormulaErrorType.Value, ((OdfFormulaError)result).ErrorType);
        }

        #endregion

        #region 5. Exact and Approximate VLOOKUP

        [Fact]
        public void TestVLookup_ExactMatch()
        {
            var context = new StressMockEvaluationContext();
            var evaluator = new DefaultFormulaEvaluator();
            context.Evaluator = evaluator;

            // Setup table
            // ID | Name | Score
            // 1  | Apple | 90
            // 2  | Banana | 80
            // 3  | Apple | 95 (Duplicate Key)
            context.CellValues[OdfCellAddress.ParseExcel("A1")] = 1.0;
            context.CellValues[OdfCellAddress.ParseExcel("B1")] = "Apple";
            context.CellValues[OdfCellAddress.ParseExcel("C1")] = 90.0;

            context.CellValues[OdfCellAddress.ParseExcel("A2")] = 2.0;
            context.CellValues[OdfCellAddress.ParseExcel("B2")] = "Banana";
            context.CellValues[OdfCellAddress.ParseExcel("C2")] = 80.0;

            context.CellValues[OdfCellAddress.ParseExcel("A3")] = 3.0;
            context.CellValues[OdfCellAddress.ParseExcel("B3")] = "Apple";
            context.CellValues[OdfCellAddress.ParseExcel("C3")] = 95.0;

            // Exact match for Banana (Score, column 3)
            var res1 = evaluator.Evaluate("VLOOKUP(2, A1:C3, 3, FALSE)", context);
            Assert.Equal(80.0, res1);

            // Case insensitivity lookup
            var res2 = evaluator.Evaluate("VLOOKUP(\"banana\", B1:C3, 2, FALSE)", context);
            Assert.Equal(80.0, res2);

            // Duplicate key lookup: should match the first occurrence (Score for Apple in B column)
            var res3 = evaluator.Evaluate("VLOOKUP(\"Apple\", B1:C3, 2, FALSE)", context);
            Assert.Equal(90.0, res3);

            // Not found
            var res4 = evaluator.Evaluate("VLOOKUP(\"Cherry\", B1:C3, 2, FALSE)", context);
            Assert.True(res4 is OdfFormulaError);
            Assert.Equal(OdfFormulaErrorType.NA, ((OdfFormulaError)res4).ErrorType);
        }

        [Fact]
        public void TestVLookup_ApproximateMatch()
        {
            var context = new StressMockEvaluationContext();
            var evaluator = new DefaultFormulaEvaluator();
            context.Evaluator = evaluator;

            // Sorted Lookup Table
            // 10 | A
            // 20 | B
            // 30 | C
            context.CellValues[OdfCellAddress.ParseExcel("A1")] = 10.0;
            context.CellValues[OdfCellAddress.ParseExcel("B1")] = "A";
            context.CellValues[OdfCellAddress.ParseExcel("A2")] = 20.0;
            context.CellValues[OdfCellAddress.ParseExcel("B2")] = "B";
            context.CellValues[OdfCellAddress.ParseExcel("A3")] = 30.0;
            context.CellValues[OdfCellAddress.ParseExcel("B3")] = "C";

            // Exact match with TRUE
            var res1 = evaluator.Evaluate("VLOOKUP(20, A1:B3, 2, TRUE)", context);
            Assert.Equal("B", res1);

            // Exact match (default approximate lookup when rangeLookup is omitted)
            var res2 = evaluator.Evaluate("VLOOKUP(20, A1:B3, 2)", context);
            Assert.Equal("B", res2);

            // Approximate lookup (25 is between 20 and 30 -> matches 20)
            var res3 = evaluator.Evaluate("VLOOKUP(25, A1:B3, 2, TRUE)", context);
            Assert.Equal("B", res3);

            // Lookup value smaller than first element -> NA error
            var res4 = evaluator.Evaluate("VLOOKUP(5, A1:B3, 2, TRUE)", context);
            Assert.True(res4 is OdfFormulaError);
            Assert.Equal(OdfFormulaErrorType.NA, ((OdfFormulaError)res4).ErrorType);

            // Lookup value larger than last element -> matches last element (30)
            var res5 = evaluator.Evaluate("VLOOKUP(50, A1:B3, 2, TRUE)", context);
            Assert.Equal("C", res5);
        }

        #endregion

        #region 6. Cache Correctness

        [Fact]
        public void TestCacheCorrectness_TransitiveCaching()
        {
            var context = new StressMockEvaluationContext();
            var evaluator = new DefaultFormulaEvaluator();
            context.Evaluator = evaluator;

            var a1 = OdfCellAddress.ParseExcel("A1");
            var b1 = OdfCellAddress.ParseExcel("B1");
            var c1 = OdfCellAddress.ParseExcel("C1");

            context.CellFormulas[a1] = "=B1+1";
            context.CellFormulas[b1] = "=C1+1";
            context.CellValues[c1] = 10.0;

            // 1. Evaluate A1 (B1 and A1 will be calculated and cached)
            var res1 = evaluator.EvaluateCell(a1, context);
            Assert.Equal(12.0, res1);

            // 2. Change C1 directly in context (cache is not cleared)
            context.CellValues[c1] = 20.0;

            // 3. Re-evaluate B1 -> should hit cache and return 11.0, not 21.0
            var resB = evaluator.EvaluateCell(b1, context);
            Assert.Equal(11.0, resB);

            // 4. Re-evaluate A1 -> should hit cache and return 12.0
            var resA = evaluator.EvaluateCell(a1, context);
            Assert.Equal(12.0, resA);

            // 5. Clear Cache
            evaluator.ClearCache();

            // 6. Re-evaluate A1 -> should calculate new values and return 22.0
            var resNew = evaluator.EvaluateCell(a1, context);
            Assert.Equal(22.0, resNew);
        }

        #endregion

        #region 7. Number Formatter

        [Fact]
        public void TestNumberFormatter_DeduplicationHighLoad()
        {
            var contentRoot = new OdfNode(OdfNodeType.Element, "document-content", OdfNamespaces.Office, "office");
            var stylesRoot = new OdfNode(OdfNodeType.Element, "document-styles", OdfNamespaces.Office, "office");

            var formatter = new OdfNumberFormatter(contentRoot, stylesRoot);

            // Check deduplication under 100 duplicate requests
            string firstStyle = formatter.GetOrCreateNumberStyle("#,##0.00");
            for (int i = 0; i < 100; i++)
            {
                string style = formatter.GetOrCreateNumberStyle("#,##0.00");
                Assert.Equal(firstStyle, style);
            }

            // Create 100 different styles to stress unique naming and DOM insertions
            var styleNames = new HashSet<string>();
            for (int i = 1; i <= 100; i++)
            {
                string format = $"0.{new string('0', i)}";
                string style = formatter.GetOrCreateNumberStyle(format);
                Assert.True(styleNames.Add(style), $"Generated style name '{style}' should be unique.");
            }
            Assert.Equal(100, styleNames.Count);
        }

        [Fact]
        public void TestNumberFormatter_StandardFormatResolution()
        {
            var contentRoot = new OdfNode(OdfNodeType.Element, "document-content", OdfNamespaces.Office, "office");
            var stylesRoot = new OdfNode(OdfNodeType.Element, "document-styles", OdfNamespaces.Office, "office");

            var formatter = new OdfNumberFormatter(contentRoot, stylesRoot);

            // Test standard formats
            string currencyStyle = formatter.GetOrCreateNumberStyle("C2");
            Assert.StartsWith("C", currencyStyle);

            string percentStyle = formatter.GetOrCreateNumberStyle("P1");
            Assert.StartsWith("P", percentStyle);

            string numberStyle = formatter.GetOrCreateNumberStyle("N0");
            Assert.StartsWith("N", numberStyle);
        }

        #endregion
    }
}
