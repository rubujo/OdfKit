using OdfKit.Core;
using OdfKit.Formula;
using OdfKit.Spreadsheet;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Verifies transactional formula evaluation, save strategies, and resource limits.
/// 驗證交易式公式評估、儲存策略與資源限制。
/// </summary>
public sealed class OpenFormulaSafetyAndSaveStrategyTests
{
    /// <summary>
    /// Verifies the default save strategy preserves cached values.
    /// 驗證預設儲存策略會保留快取值。
    /// </summary>
    [Fact]
    public void PreserveCachedValuesIsTheDefaultSaveStrategy()
    {
        using SpreadsheetDocument document = CreateFormulaDocument("of:=1+1", 99d);
        using var stream = new MemoryStream();

        document.SaveToStream(stream);
        stream.Position = 0;

        using SpreadsheetDocument loaded = SpreadsheetDocument.Load(stream, "preserve.ods");
        OdfCell cell = loaded.FindSheet("Data")!.Cells["A1"];
        Assert.Equal("of:=1+1", cell.Formula);
        Assert.Equal(99d, cell.CellValue);
    }

    /// <summary>
    /// Verifies the mark strategy clears formula caches and requests automatic calculation.
    /// 驗證標記策略會清除公式快取並要求自動計算。
    /// </summary>
    [Fact]
    public void MarkForRecalculationClearsCachedResults()
    {
        using SpreadsheetDocument document = CreateFormulaDocument("of:=1+1", 99d);
        using var stream = new MemoryStream();

        document.SaveToStream(
            stream,
            new OdfSaveOptions
            {
                FormulaStrategy = OdfFormulaSaveStrategy.MarkForRecalculation
            });
        stream.Position = 0;

        using SpreadsheetDocument loaded = SpreadsheetDocument.Load(stream, "mark.ods");
        OdfCell cell = loaded.FindSheet("Data")!.Cells["A1"];
        Assert.Equal("of:=1+1", cell.Formula);
        Assert.True(string.IsNullOrEmpty(cell.RawValue));
        Assert.True(string.IsNullOrEmpty(cell.DisplayText));
        Assert.True(loaded.AutoCalculate);
    }

    /// <summary>
    /// Verifies the calculate strategy writes evaluated results.
    /// 驗證計算策略會寫入評估結果。
    /// </summary>
    [Fact]
    public void CalculateSaveStrategyWritesEvaluatedResult()
    {
        using SpreadsheetDocument document = CreateFormulaDocument("of:=1+1", 99d);
        using var stream = new MemoryStream();

        document.SaveToStream(
            stream,
            new OdfSaveOptions
            {
                FormulaStrategy = OdfFormulaSaveStrategy.Calculate
            });
        stream.Position = 0;

        using SpreadsheetDocument loaded = SpreadsheetDocument.Load(stream, "calculate.ods");
        Assert.Equal(2d, loaded.FindSheet("Data")!.Cells["A1"].CellValue);
    }

    /// <summary>
    /// Verifies unsupported formulas do not partially commit prior results.
    /// 驗證不支援的公式不會部分提交先前結果。
    /// </summary>
    [Fact]
    public void UnsupportedFormulaLeavesDocumentUnchanged()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Data");
        sheet.Cells["A1"].SetFormula("of:=1+1", 99d);
        sheet.Cells["A2"].SetFormula("of:=UNSUPPORTED.TEST()", 88d);

        OdfFormulaEvaluationException exception = Assert.Throws<OdfFormulaEvaluationException>(
            () => document.EvaluateFormulas());

        Assert.Equal(
            OdfFormulaEvaluationFailureReason.UnsupportedFormula,
            exception.Reason);
        Assert.Equal(99d, sheet.Cells["A1"].CellValue);
        Assert.Equal(88d, sheet.Cells["A2"].CellValue);
        Assert.Equal(0, exception.Report.WrittenFormulaCount);
    }

    /// <summary>
    /// Verifies formula count limits fail before modifying the document.
    /// 驗證公式數量上限會在修改文件前失敗。
    /// </summary>
    [Fact]
    public void FormulaCountLimitIsTransactional()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Data");
        sheet.Cells["A1"].SetFormula("of:=1+1", 99d);
        sheet.Cells["A2"].SetFormula("of:=2+2", 88d);

        OdfFormulaEvaluationException exception = Assert.Throws<OdfFormulaEvaluationException>(
            () => document.EvaluateFormulas(new OdfFormulaEvaluationOptions
            {
                MaxFormulaCount = 1
            }, TestContext.Current.CancellationToken));

        Assert.Equal(
            OdfFormulaEvaluationFailureReason.ResourceLimitExceeded,
            exception.Reason);
        Assert.Equal(99d, sheet.Cells["A1"].CellValue);
        Assert.Equal(88d, sheet.Cells["A2"].CellValue);
    }

    /// <summary>
    /// Verifies array result limits abort without writing partial values.
    /// 驗證陣列結果上限會中止且不寫入部分值。
    /// </summary>
    [Fact]
    public void ArrayResultLimitIsTransactional()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Data");
        sheet.Ranges["A1:B2"].SetArrayFormula("of:={1;2|3;4}");

        Assert.Throws<OdfFormulaEvaluationException>(
            () => document.EvaluateFormulas(new OdfFormulaEvaluationOptions
            {
                MaxArrayResultCells = 3
            }, TestContext.Current.CancellationToken));

        Assert.True(string.IsNullOrEmpty(sheet.Cells["A1"].DisplayText));
        Assert.True(string.IsNullOrEmpty(sheet.Cells["B2"].DisplayText));
    }

    /// <summary>
    /// Verifies cancellation is observed before evaluation commits.
    /// 驗證取消會在評估提交前生效。
    /// </summary>
    [Fact]
    public void PreCancelledEvaluationDoesNotModifyDocument()
    {
        using SpreadsheetDocument document = CreateFormulaDocument("of:=1+1", 99d);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(
            () => document.EvaluateFormulas(
                new OdfFormulaEvaluationOptions(),
                cancellation.Token));
        Assert.Equal(99d, document.FindSheet("Data")!.Cells["A1"].CellValue);
    }

    /// <summary>
    /// Verifies asynchronous calculate-on-save observes cancellation before formula commit.
    /// 驗證非同步儲存時計算會在提交公式前遵守取消。
    /// </summary>
    [Fact]
    public async Task PreCancelledAsyncSaveDoesNotModifyDocument()
    {
        using SpreadsheetDocument document = CreateFormulaDocument("of:=1+1", 99d);
        using var stream = new MemoryStream();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => document.SaveToStreamAsync(
                stream,
                new OdfSaveOptions
                {
                    FormulaStrategy = OdfFormulaSaveStrategy.Calculate
                },
                cancellation.Token));

        Assert.Equal(99d, document.FindSheet("Data")!.Cells["A1"].CellValue);
    }

    /// <summary>
    /// Verifies external document resolvers are denied unless explicitly enabled.
    /// 驗證外部文件解析器未明確啟用時會被拒絕。
    /// </summary>
    [Fact]
    public void ExternalResolverRequiresExplicitPolicy()
    {
        using SpreadsheetDocument external = SpreadsheetDocument.Create();
        external.AddSheet("Sheet1").Cells["A1"].CellValue = 41d;
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Data");
        sheet.Cells["A1"].SetFormula(
            "='file:///external.ods#$Sheet1'!A1+1",
            99d);
        document.ExternalLinks.DocumentResolver = id =>
            id == "file:///external.ods" ? external : null;

        OdfFormulaEvaluationException exception = Assert.Throws<OdfFormulaEvaluationException>(
            () => document.EvaluateFormulas());
        Assert.Equal(
            OdfFormulaEvaluationFailureReason.UnsupportedFormula,
            exception.Reason);
        Assert.Equal(99d, sheet.Cells["A1"].CellValue);
    }

    /// <summary>
    /// Verifies application function failures preserve all cached results.
    /// 驗證應用程式函式失敗時會保留全部快取結果。
    /// </summary>
    [Fact]
    public void CustomFunctionFailureIsTransactional()
    {
        using SpreadsheetDocument document = CreateFormulaDocument(
            "of:=ACME.FAIL()",
            99d);
        var functions = new OdfFormulaFunctionRegistry();
        functions.Register(
            "ACME.FAIL",
            static (_, _) => throw new InvalidOperationException("expected"));

        OdfFormulaEvaluationException exception = Assert.Throws<OdfFormulaEvaluationException>(
            () => document.EvaluateFormulas(new OdfFormulaEvaluationOptions
            {
                Evaluator = new DefaultFormulaEvaluator(functions)
            }, TestContext.Current.CancellationToken));

        Assert.Equal(
            OdfFormulaEvaluationFailureReason.EvaluationFailed,
            exception.Reason);
        Assert.Equal(99d, document.FindSheet("Data")!.Cells["A1"].CellValue);
    }

    /// <summary>
    /// Verifies successful evaluation reports measured work.
    /// 驗證成功評估報告會呈現已量測的工作量。
    /// </summary>
    [Fact]
    public void EvaluationReportContainsResourceUsage()
    {
        using SpreadsheetDocument document = CreateFormulaDocument("of:=SUM([.B1:.B2])", 0d);
        OdfTableSheet sheet = document.FindSheet("Data")!;
        sheet.Cells["B1"].CellValue = 20d;
        sheet.Cells["B2"].CellValue = 22d;

        OdfFormulaEvaluationReport report = document.EvaluateFormulas();

        Assert.Equal(1, report.ScannedFormulaCount);
        Assert.Equal(1, report.EvaluatedFormulaCount);
        Assert.Equal(1, report.WrittenFormulaCount);
        Assert.True(report.OperationCount > 0);
        Assert.True(report.CellReadCount >= 2);
        Assert.True(report.MaximumParallelism >= 1);
        Assert.Equal(42d, sheet.Cells["A1"].CellValue);
    }

    /// <summary>
    /// Verifies large ranges create formula-to-formula edges rather than one edge per cell.
    /// 驗證大型範圍只建立公式對公式相依邊，而非每個儲存格一條邊。
    /// </summary>
    [Fact]
    public void LargeRangeDependencyGraphDoesNotExpandByArea()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Data");
        sheet.Cells["A1"].SetFormula("of:=1", 0d);
        sheet.Cells["B1"].SetFormula("of:=SUM([.A1:.A1048576])", 0d);

        OdfFormulaEvaluationException exception = Assert.Throws<OdfFormulaEvaluationException>(
            () => document.EvaluateFormulas(new OdfFormulaEvaluationOptions
            {
                MaxCellReads = 1
            }, TestContext.Current.CancellationToken));

        Assert.Equal(
            OdfFormulaEvaluationFailureReason.ResourceLimitExceeded,
            exception.Reason);
        Assert.Equal(1, exception.Report.DependencyEdgeCount);
        Assert.Equal(0, exception.Report.WrittenFormulaCount);
    }

    /// <summary>
    /// Verifies a large matrix is rejected before its result array is allocated.
    /// 驗證大型矩陣會在配置結果陣列前遭拒絕。
    /// </summary>
    [Fact]
    public void MatrixAllocationIsRejectedBeforeCommit()
    {
        using SpreadsheetDocument document = CreateFormulaDocument(
            "of:=MUNIT(10000)",
            99d);

        OdfFormulaEvaluationException exception = Assert.Throws<OdfFormulaEvaluationException>(
            () => document.EvaluateFormulas(new OdfFormulaEvaluationOptions
            {
                MaxArrayResultCells = 10_000
            }, TestContext.Current.CancellationToken));

        Assert.Equal(
            OdfFormulaEvaluationFailureReason.ResourceLimitExceeded,
            exception.Reason);
        Assert.Equal(99d, document.FindSheet("Data")!.Cells["A1"].CellValue);
    }

    private static SpreadsheetDocument CreateFormulaDocument(
        string formula,
        double cachedValue)
    {
        SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Data");
        sheet.Cells["A1"].SetFormula(formula, cachedValue);
        return document;
    }
}
