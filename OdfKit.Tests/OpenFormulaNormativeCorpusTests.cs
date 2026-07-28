using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml;
using OdfKit.Formula;
using OdfKit.Spreadsheet;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Verifies every Safe Large function against a traceable OASIS case and an independent result oracle.
/// 依可追溯的 OASIS 案例與獨立結果 oracle 驗證每個 Safe Large 函式。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Corpus)]
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public sealed class OpenFormulaNormativeCorpusTests
{
    private static readonly DateTime SpreadsheetEpoch = new(1899, 12, 30);

    /// <summary>
    /// Gets the normative case for every mandatory Large Group function.
    /// 取得每個 Large Group 強制函式的規範案例。
    /// </summary>
    public static IEnumerable<object[]> FunctionCases
    {
        get
        {
            using JsonDocument document = JsonDocument.Parse(
                File.ReadAllText(GetCorpusPath()));
            foreach (JsonElement item in document.RootElement
                         .GetProperty("cases")
                         .EnumerateArray())
            {
                JsonElement expected = item.GetProperty("expected");
                yield return
                [
                    item.GetProperty("function").GetString()!,
                    item.GetProperty("section").GetString()!,
                    item.GetProperty("formula").GetString()!,
                    expected.GetProperty("kind").GetString()!,
                    expected.GetProperty("value").GetString()!
                ];
            }
        }
    }

    /// <summary>
    /// Verifies the exact value or normative runtime property for every mandatory function.
    /// 驗證每個強制函式的精確值或規範執行期性質。
    /// </summary>
    /// <param name="functionName">The function name. / 函式名稱。</param>
    /// <param name="section">The OASIS section. / OASIS 條文。</param>
    /// <param name="formula">The formula under test. / 受測公式。</param>
    /// <param name="expectedKind">The expected result kind. / 預期結果種類。</param>
    /// <param name="expectedValue">The expected result value. / 預期結果值。</param>
    [Theory]
    [MemberData(nameof(FunctionCases))]
    public void EverySafeLargeFunctionMatchesNormativeOracle(
        string functionName,
        string section,
        string formula,
        string expectedKind,
        string expectedValue)
    {
        object result = new DefaultFormulaEvaluator().Evaluate(
            formula,
            new NormativeEvaluationContext());
        object scalarResult = result is object[,] array ? array[0, 0] : result;
        string because = $"{functionName} ({section}), {formula}";

        switch (expectedKind)
        {
            case "float":
            case "currency":
            case "percentage":
                AssertClose(
                    double.Parse(expectedValue, CultureInfo.InvariantCulture),
                    Assert.IsType<double>(scalarResult),
                    because);
                break;
            case "boolean":
                Assert.Equal(
                    bool.Parse(expectedValue),
                    Assert.IsType<bool>(scalarResult));
                break;
            case "string" when expectedValue.StartsWith('#'):
                Assert.Equal(
                    expectedValue,
                    Assert.IsType<OdfFormulaError>(scalarResult).ToErrorString());
                break;
            case "string":
                string actualText = Assert.IsType<string>(scalarResult);
                if (TryParseComplex(expectedValue, out double expectedReal, out double expectedImaginary) &&
                    TryParseComplex(actualText, out double actualReal, out double actualImaginary))
                {
                    AssertClose(expectedReal, actualReal, because);
                    AssertClose(expectedImaginary, actualImaginary, because);
                }
                else
                {
                    Assert.Equal(expectedValue, actualText);
                }

                break;
            case "date":
                DateTime date = XmlConvert.ToDateTime(
                    expectedValue,
                    XmlDateTimeSerializationMode.RoundtripKind);
                AssertClose(
                    (date - SpreadsheetEpoch).TotalDays,
                    Assert.IsType<double>(scalarResult),
                    because);
                break;
            case "time":
                AssertClose(
                    XmlConvert.ToTimeSpan(expectedValue).TotalDays,
                    Assert.IsType<double>(scalarResult),
                    because);
                break;
            case "":
                Assert.Equal(string.Empty, Assert.IsType<string>(scalarResult));
                break;
            case "predicate":
                AssertRuntimeProperty(expectedValue, scalarResult, because);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported oracle kind '{expectedKind}' for {because}.");
        }
    }

    /// <summary>
    /// Verifies corpus identity, OASIS traceability, and one-to-one Large Group coverage.
    /// 驗證 corpus 身分、OASIS 可追溯性及 Large Group 一對一覆蓋。
    /// </summary>
    [Fact]
    public void CorpusCoversEverySafeLargeFunctionExactlyOnce()
    {
        using JsonDocument document = JsonDocument.Parse(
            File.ReadAllText(GetCorpusPath()));
        JsonElement root = document.RootElement;
        Assert.Equal("OdfKit Safe Large", root.GetProperty("profile").GetString());
        Assert.Equal(388, root.GetProperty("requiredFunctionCount").GetInt32());
        Assert.Equal(
            "OpenDocument v1.4 Part 4 OpenFormula",
            root.GetProperty("normativeSource").GetProperty("title").GetString());

        string[] functions = root.GetProperty("cases")
            .EnumerateArray()
            .Select(item => item.GetProperty("function").GetString()!)
            .ToArray();
        Assert.Equal(functions.Length, functions.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            OdfFormulaSupport.GetRequiredFunctions(OdfFormulaConformanceGroup.Large),
            functions);
        Assert.All(
            root.GetProperty("cases").EnumerateArray(),
            item =>
            {
                Assert.Matches(@"^6\.\d+\.\d+$", item.GetProperty("section").GetString());
                Assert.StartsWith(
                    $"of:={item.GetProperty("function").GetString()}(",
                    item.GetProperty("formula").GetString(),
                    StringComparison.Ordinal);
            });
    }

    private static string GetCorpusPath() =>
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "docs",
            "openformula-normative-corpus.json"));

    private static void AssertClose(double expected, double actual, string because)
    {
        double tolerance = Math.Max(1e-9, Math.Abs(expected) * 2e-6);
        Assert.True(
            Math.Abs(expected - actual) <= tolerance,
            $"{because}: expected {expected:R}, actual {actual:R}, tolerance {tolerance:R}.");
    }

    private static void AssertRuntimeProperty(string property, object result, string because)
    {
        switch (property)
        {
            case "number-zero-inclusive-one-exclusive":
                double random = Assert.IsType<double>(result);
                Assert.True(random >= 0d && random < 1d, because);
                break;
            case "current-date":
                double date = Assert.IsType<double>(result);
                AssertClose((DateTime.Today - SpreadsheetEpoch).TotalDays, date, because);
                break;
            case "current-date-time":
                double dateTime = Assert.IsType<double>(result);
                Assert.InRange(
                    dateTime,
                    (DateTime.Now.AddMinutes(-1) - SpreadsheetEpoch).TotalDays,
                    (DateTime.Now.AddMinutes(1) - SpreadsheetEpoch).TotalDays);
                break;
            case "non-empty-host-text":
                Assert.False(string.IsNullOrWhiteSpace(Assert.IsType<string>(result)));
                break;
            case "workbook-data-table-contract":
                Assert.Equal(99d, Assert.IsType<double>(result));
                break;
            case "workbook-pivot-contract":
                Assert.Equal(42d, Assert.IsType<double>(result));
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported runtime property '{property}' for {because}.");
        }
    }

    private sealed class NormativeEvaluationContext : IOdfFormulaWorkbookContext
    {
        public OdfCellAddress CurrentCell => new(1, 0, "Oracle");

        public IReadOnlyList<string> SheetNames { get; } = ["Data", "Oracle"];

        public object GetCellValue(OdfCellAddress address) =>
            GetValue(address.SheetName, address.Column, address.Row);

        public object[,] GetRangeValues(OdfCellRange range)
        {
            var values = new object[
                range.EndAddress.Row - range.StartAddress.Row + 1,
                range.EndAddress.Column - range.StartAddress.Column + 1];
            for (int row = range.StartAddress.Row; row <= range.EndAddress.Row; row++)
            {
                for (int column = range.StartAddress.Column;
                     column <= range.EndAddress.Column;
                     column++)
                {
                    values[
                        row - range.StartAddress.Row,
                        column - range.StartAddress.Column] =
                        GetValue(range.StartAddress.SheetName, column, row);
                }
            }

            return values;
        }

        public string? GetCellFormula(OdfCellAddress address) => null;

        public object GetNamedRangeOrExpressionValue(string name) =>
            OdfFormulaError.Name;

        public bool TryGetPivotData(
            string dataField,
            OdfCellAddress pivotAnchor,
            IReadOnlyDictionary<string, object> filters,
            out object result)
        {
            result = 42d;
            return true;
        }

        public bool TryEvaluateMultipleOperations(
            IReadOnlyList<object> arguments,
            out object result)
        {
            result = 99d;
            return arguments.Count is 3 or 5;
        }

        private static object GetValue(string? sheetName, int column, int row)
        {
            if (!string.Equals(sheetName, "Data", StringComparison.OrdinalIgnoreCase))
            {
                return 0d;
            }

            return (column, row) switch
            {
                (0, 0) => "Label",
                (1, 0) => "Value",
                (3, 0) => "Value",
                (4, 0) => "Value",
                (0, 1) => "One",
                (1, 1) => 1d,
                (3, 1) => ">1",
                (4, 1) => "=2",
                (0, 2) => "Two",
                (1, 2) => 2d,
                (0, 3) => "Three",
                (1, 3) => 3d,
                (0, 5) => 1d,
                (1, 5) => 2d,
                (2, 5) => 3d,
                (0, 6) => 10d,
                (1, 6) => 20d,
                (2, 6) => 30d,
                _ => string.Empty
            };
        }
    }

    private static bool TryParseComplex(string value, out double real, out double imaginary)
    {
        real = 0d;
        imaginary = 0d;
        if (value.Length < 2 || value[^1] is not ('i' or 'j'))
        {
            return false;
        }

        string coefficients = value[..^1];
        int separator = -1;
        for (int index = coefficients.Length - 1; index > 0; index--)
        {
            if (coefficients[index] is ('+' or '-') &&
                coefficients[index - 1] is not ('e' or 'E'))
            {
                separator = index;
                break;
            }
        }

        if (separator < 0)
        {
            return double.TryParse(
                coefficients,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out imaginary);
        }

        return double.TryParse(
            coefficients[..separator],
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out real) &&
            double.TryParse(
                coefficients[separator..],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out imaginary);
    }

}
