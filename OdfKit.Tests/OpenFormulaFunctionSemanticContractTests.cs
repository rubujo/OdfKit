using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using OdfKit.Formula;
using OdfKit.Spreadsheet;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Executes the seven-dimensional safe semantic contract for every Large Group function.
/// 對每個 Large Group 函式執行七維安全語意契約。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Corpus)]
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public sealed class OpenFormulaFunctionSemanticContractTests
{
    private static readonly string[] Dimensions =
    [
        "arity",
        "normal-types",
        "implicit-conversion",
        "blank-values",
        "error-propagation",
        "boundaries",
        "version-differences"
    ];

    /// <summary>
    /// Gets one executable case for every Large function and required semantic dimension.
    /// 取得每個 Large 函式及必要語意維度的一個可執行案例。
    /// </summary>
    public static IEnumerable<object[]> SafeSemanticCases
    {
        get
        {
            foreach (string functionName in OdfFormulaSupport.GetRequiredFunctions(
                         OdfFormulaConformanceGroup.Large))
            {
                foreach (string dimension in Dimensions)
                {
                    yield return
                    [
                        functionName,
                        dimension,
                        CreateFormula(functionName, dimension)
                    ];
                }
            }
        }
    }

    /// <summary>
    /// Verifies every contract case parses, dispatches, and returns a closed safe value.
    /// 驗證每個契約案例皆可剖析、派送，並回傳封閉的安全值。
    /// </summary>
    /// <param name="functionName">The function name. / 函式名稱。</param>
    /// <param name="dimension">The semantic dimension. / 語意維度。</param>
    /// <param name="formula">The executable formula. / 可執行公式。</param>
    [Theory]
    [MemberData(nameof(SafeSemanticCases))]
    public void EveryLargeFunctionHasExecutableSafeSemanticContract(
        string functionName,
        string dimension,
        string formula)
    {
        OdfFormulaAnalysis analysis = OdfFormulaSupport.Analyze(formula);
        Assert.True(
            analysis.CanParse,
            $"{functionName}/{dimension} did not parse: {formula}");
        Assert.False(
            analysis.HasUnsupportedFunctions,
            $"{functionName}/{dimension} was not dispatched: {formula}");

        object result = new DefaultFormulaEvaluator().Evaluate(
            formula,
            new ContractEvaluationContext());
        Assert.True(
            result is double or bool or string or OdfFormulaError or object[,],
            $"{functionName}/{dimension} returned unsafe type {result?.GetType()}.");

        if (functionName == "DDE")
        {
            Assert.Equal(
                OdfFormulaErrorType.NA,
                Assert.IsType<OdfFormulaError>(result).ErrorType);
        }
    }

    /// <summary>
    /// Verifies the generated manifest maps every function to all seven executable dimensions.
    /// 驗證產生的 manifest 會將每個函式對應至全部七個可執行維度。
    /// </summary>
    [Fact]
    public void ManifestMapsEveryLargeFunctionToSevenContractCases()
    {
        string path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "docs",
            "openformula-conformance-manifest.json"));
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = document.RootElement;
        Assert.Equal(388, root.GetProperty("requiredFunctionCount").GetInt32());
        Assert.Equal(2716, root.GetProperty("safeSemanticContractCaseCount").GetInt32());

        foreach (JsonElement function in root.GetProperty("functions").EnumerateArray())
        {
            string[] dimensions = function
                .GetProperty("semanticCases")
                .EnumerateArray()
                .Select(item => item.GetProperty("dimension").GetString()!)
                .ToArray();
            Assert.Equal(Dimensions, dimensions);
        }
    }

    private static string CreateFormula(string functionName, string dimension)
    {
        if (functionName == "DDE")
        {
            return "of:=DDE(1/0;1/0;1/0)";
        }

        string arguments = dimension switch
        {
            "arity" => string.Empty,
            "normal-types" => "1;2;3",
            "implicit-conversion" => "\"1\";TRUE();2",
            "blank-values" => "[.A1];1;2",
            "error-propagation" => "1/0;1;2",
            "boundaries" => "0;-1;1E100",
            "version-differences" => "1",
            _ => throw new InvalidOperationException(dimension)
        };
        return $"of:={functionName}({arguments})";
    }

    private sealed class ContractEvaluationContext : IEvaluationContext
    {
        public OdfCellAddress CurrentCell => new(0, 0, "Data");

        public object GetCellValue(OdfCellAddress address) => 0d;

        public object[,] GetRangeValues(OdfCellRange range) => new object[1, 1];

        public string? GetCellFormula(OdfCellAddress address) => null;

        public object GetNamedRangeOrExpressionValue(string name) =>
            OdfFormulaError.Name;
    }
}
