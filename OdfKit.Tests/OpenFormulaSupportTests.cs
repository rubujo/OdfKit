using System;
using System.IO;
using System.Linq;
using OdfKit.Core;
using OdfKit.Formula;
using OdfKit.Spreadsheet;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定 OpenFormula 支援範圍與保真策略。
/// </summary>
public class OpenFormulaSupportTests
{
    /// <summary>
    /// 驗證支援表會揭露目前預設評估器可計算的常用函式。
    /// </summary>
    [Fact]
    public void SupportTableListsEvaluatorBackedFunctions()
    {
        Assert.True(OdfFormulaSupport.IsFunctionSupported("SUM"));
        Assert.True(OdfFormulaSupport.IsFunctionSupported("vlookup"));
        Assert.False(OdfFormulaSupport.IsFunctionSupported("XLOOKUP"));

        OdfFormulaFunctionInfo sum = OdfFormulaSupport.SupportedFunctions.Single(f => f.Name == "SUM");

        Assert.Equal("Statistical", sum.Category);
        Assert.Equal(OdfFormulaSupportLevel.Evaluated, sum.SupportLevel);
    }

    /// <summary>
    /// 驗證預設評估器不支援的函式會產生診斷，並在序列化時保留原公式。
    /// </summary>
    [Fact]
    public void UnsupportedFunctionIsDiagnosedAndPreserved()
    {
        const string formula = "of:=XLOOKUP([.A1];[.A1:.A3];[.B1:.B3])";

        OdfFormulaAnalysis analysis = OdfFormulaSupport.Analyze(formula);

        Assert.Contains("XLOOKUP", analysis.Functions);
        Assert.True(analysis.HasUnsupportedFunctions);
        Assert.Contains(analysis.Diagnostics, d => d.Code == "OF0002");
        Assert.Equal(formula, OdfFormulaSupport.SerializePreservingUnsupported(formula));
    }

    /// <summary>
    /// 驗證無效公式會產生剖析診斷，並在序列化時保留原公式。
    /// </summary>
    [Fact]
    public void InvalidFormulaIsDiagnosedAndPreserved()
    {
        const string formula = "SUM(A1:A3";

        OdfFormulaAnalysis analysis = OdfFormulaSupport.Analyze(formula);

        Assert.False(analysis.CanParse);
        Assert.Contains(analysis.Diagnostics, d => d.Code == "OF0001");
        Assert.Equal(formula, OdfFormulaSupport.SerializePreservingUnsupported(formula));
    }

    /// <summary>
    /// 驗證含未支援函式的儲存格公式可完整 round-trip。
    /// </summary>
    [Fact]
    public void SpreadsheetFormulaRoundTripPreservesUnsupportedFormula()
    {
        const string formula = "of:=XLOOKUP([.A1];[.A1:.A3];[.B1:.B3])";
        using var workbook = SpreadsheetDocument.Create();
        OdfTableSheet sheet = workbook.Worksheets.Add("Data");
        sheet.Cells["C1"].Formula = formula;

        using var stream = new MemoryStream();
        workbook.SaveToStream(stream);
        stream.Position = 0;

        using SpreadsheetDocument loaded = SpreadsheetDocument.Load(stream);

        Assert.Equal(formula, loaded.Worksheets["Data"].Cells["C1"].Formula);
    }

    /// <summary>
    /// 驗證已支援公式可安全重新序列化。
    /// </summary>
    [Fact]
    public void SupportedFormulaCanSerialize()
    {
        string serialized = OdfFormulaSupport.SerializePreservingUnsupported("=SUM(A1:A3)");

        Assert.Equal("=SUM(A1:A3)", serialized);
    }

    /// <summary>
    /// 驗證 LibreOffice EASTERSUNDAY 擴充函式可回傳 ODF 日期序號。
    /// </summary>
    [Fact]
    public void LibreOfficeEasterSundayEvaluatesToDateSerial()
    {
        var evaluator = new DefaultFormulaEvaluator();
        var context = new FormulaAndStylesTest.MockEvaluationContext();
        double expected = (new DateTime(2026, 4, 5) - new DateTime(1899, 12, 30)).TotalDays;

        object result = evaluator.Evaluate("ORG.OPENOFFICE.EASTERSUNDAY(2026)", context);

        Assert.Equal(expected, Assert.IsType<double>(result));
        Assert.True(OdfFormulaSupport.IsFunctionSupported("ORG.OPENOFFICE.EASTERSUNDAY"));
    }

    /// <summary>
    /// 驗證 LibreOffice ISOMITTED 擴充函式依引數數量回傳結果。
    /// </summary>
    [Fact]
    public void LibreOfficeIsOmittedEvaluatesByArgumentCount()
    {
        var evaluator = new DefaultFormulaEvaluator();
        var context = new FormulaAndStylesTest.MockEvaluationContext();

        Assert.Equal(true, evaluator.Evaluate("ORG.OPENOFFICE.ISOMITTED()", context));
        Assert.Equal(false, evaluator.Evaluate("ORG.OPENOFFICE.ISOMITTED(1)", context));
        Assert.True(OdfFormulaSupport.IsFunctionSupported("ORG.OPENOFFICE.ISOMITTED"));
    }
}
