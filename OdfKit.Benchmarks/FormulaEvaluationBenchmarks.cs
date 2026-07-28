using BenchmarkDotNet.Attributes;
using OdfKit.Formula;
using OdfKit.Spreadsheet;

namespace OdfKit.Benchmarks;

/// <summary>
/// Formula workbook evaluation, dependency propagation, range, and array benchmarks.
/// 公式活頁簿評估、相依傳播、範圍與陣列效能基準。
/// </summary>
[MemoryDiagnoser]
public class FormulaEvaluationBenchmarks
{
    private SpreadsheetDocument? _fullDocument1000;
    private SpreadsheetDocument? _fullDocument10000;
    private SpreadsheetDocument? _linearDocument;
    private SpreadsheetDocument? _wideDocument;
    private SpreadsheetDocument? _rangeDocument;
    private SpreadsheetDocument? _arrayDocument;
    private SpreadsheetDocument? _incrementalDocument;
    private OdfFormulaEvaluationSession? _incrementalSession;
    private OdfCell? _incrementalInput;
    private double _incrementalValue;

    /// <summary>
    /// Builds the independent-formula workbook.
    /// 建立獨立公式活頁簿。
    /// </summary>
    [GlobalSetup(Target = nameof(FullRecalculation))]
    public void SetupFullRecalculation()
    {
        _fullDocument1000 = CreateIndependentDocument(1_000);
        _fullDocument10000 = CreateIndependentDocument(10_000);
    }

    /// <summary>
    /// Builds the linear dependency workbook.
    /// 建立線性相依活頁簿。
    /// </summary>
    [GlobalSetup(Target = nameof(LinearDependencyChain))]
    public void SetupLinearDependencyChain() =>
        _linearDocument = CreateLinearDocument(10_000);

    /// <summary>
    /// Builds the wide dependency workbook.
    /// 建立寬相依活頁簿。
    /// </summary>
    [GlobalSetup(Targets =
        [nameof(WideDagSingleThread), nameof(WideDagAutomaticParallelism)])]
    public void SetupWideDag() =>
        _wideDocument = CreateWideDocument(10_000);

    /// <summary>
    /// Builds and initially evaluates the incremental workbook.
    /// 建立並初次評估增量活頁簿。
    /// </summary>
    [GlobalSetup(Target = nameof(IncrementalOnePercentRecalculation))]
    public void SetupIncrementalRecalculation()
    {
        (_incrementalDocument, _incrementalInput) = CreateIncrementalDocument();
        _incrementalSession =
            _incrementalDocument.CreateFormulaEvaluationSession();
        _incrementalSession.Recalculate();
    }

    /// <summary>
    /// Builds the large-range workbook.
    /// 建立大型範圍活頁簿。
    /// </summary>
    [GlobalSetup(Target = nameof(LargeRangeSum))]
    public void SetupLargeRange() =>
        _rangeDocument = CreateRangeDocument(100_000);

    /// <summary>
    /// Builds the array-result workbook.
    /// 建立陣列結果活頁簿。
    /// </summary>
    [GlobalSetup(Target = nameof(ArrayResult100By100))]
    public void SetupArrayResult() =>
        _arrayDocument = CreateArrayDocument();

    /// <summary>
    /// Evaluates 1,000 or 10,000 independent formulas with automatic scheduling.
    /// 使用自動排程評估 1,000 或 10,000 個獨立公式。
    /// </summary>
    /// <param name="formulaCount">The formula count. / 公式數量。</param>
    /// <returns>The evaluation report. / 評估報告。</returns>
    [Benchmark(Baseline = true)]
    [Arguments(1_000)]
    [Arguments(10_000)]
    public OdfFormulaEvaluationReport FullRecalculation(int formulaCount) =>
        (formulaCount == 1_000 ? _fullDocument1000 : _fullDocument10000)!
            .EvaluateFormulas();

    /// <summary>
    /// Evaluates a 10,000-formula linear dependency chain.
    /// 評估含 10,000 個公式的線性相依鏈。
    /// </summary>
    /// <returns>The evaluation report. / 評估報告。</returns>
    [Benchmark]
    public OdfFormulaEvaluationReport LinearDependencyChain() =>
        _linearDocument!.EvaluateFormulas();

    /// <summary>
    /// Evaluates a 10,000-formula wide DAG on one worker.
    /// 使用單一工作執行緒評估含 10,000 個公式的寬 DAG。
    /// </summary>
    /// <returns>The evaluation report. / 評估報告。</returns>
    [Benchmark]
    public OdfFormulaEvaluationReport WideDagSingleThread() =>
        _wideDocument!.EvaluateFormulas(new OdfFormulaEvaluationOptions
        {
            MaxDegreeOfParallelism = 1
        });

    /// <summary>
    /// Evaluates a 10,000-formula wide DAG using automatic parallelism.
    /// 使用自動平行度評估含 10,000 個公式的寬 DAG。
    /// </summary>
    /// <returns>The evaluation report. / 評估報告。</returns>
    [Benchmark]
    public OdfFormulaEvaluationReport WideDagAutomaticParallelism() =>
        _wideDocument!.EvaluateFormulas();

    /// <summary>
    /// Changes one input and transactionally recalculates one percent of 10,000 formulas.
    /// 變更單一輸入，並以交易方式重算 10,000 個公式中的百分之一。
    /// </summary>
    /// <returns>The incremental evaluation report. / 增量評估報告。</returns>
    [Benchmark]
    public OdfFormulaEvaluationReport IncrementalOnePercentRecalculation()
    {
        _incrementalValue = _incrementalValue == 1d ? 2d : 1d;
        _incrementalInput!.CellValue = _incrementalValue;
        return _incrementalSession!.Recalculate();
    }

    /// <summary>
    /// Evaluates a SUM over 100,000 cells.
    /// 評估涵蓋 100,000 個儲存格的 SUM。
    /// </summary>
    /// <returns>The evaluation report. / 評估報告。</returns>
    [Benchmark]
    public OdfFormulaEvaluationReport LargeRangeSum() =>
        _rangeDocument!.EvaluateFormulas();

    /// <summary>
    /// Evaluates and writes a 100 by 100 array result.
    /// 評估並寫回 100 × 100 陣列結果。
    /// </summary>
    /// <returns>The evaluation report. / 評估報告。</returns>
    [Benchmark]
    public OdfFormulaEvaluationReport ArrayResult100By100() =>
        _arrayDocument!.EvaluateFormulas();

    /// <summary>
    /// Releases benchmark documents.
    /// 釋放基準文件。
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _fullDocument1000?.Dispose();
        _fullDocument10000?.Dispose();
        _linearDocument?.Dispose();
        _wideDocument?.Dispose();
        _rangeDocument?.Dispose();
        _arrayDocument?.Dispose();
        _incrementalDocument?.Dispose();
    }

    private static SpreadsheetDocument CreateIndependentDocument(int count)
    {
        SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Data");
        for (int row = 1; row <= count; row++)
        {
            sheet.Cells[$"A{row}"].SetFormula("of:=1+1", 0d);
        }

        return document;
    }

    private static SpreadsheetDocument CreateLinearDocument(int count)
    {
        SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Data");
        sheet.Cells["A1"].SetFormula("of:=1", 0d);
        for (int row = 2; row <= count; row++)
        {
            sheet.Cells[$"A{row}"].SetFormula($"of:=[.A{row - 1}]+1", 0d);
        }

        return document;
    }

    private static SpreadsheetDocument CreateWideDocument(int count)
    {
        SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Data");
        sheet.Cells["A1"].CellValue = 1d;
        for (int row = 1; row <= count; row++)
        {
            sheet.Cells[$"B{row}"].SetFormula("of:=[.A1]+1", 0d);
        }

        return document;
    }

    private static SpreadsheetDocument CreateRangeDocument(int count)
    {
        SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Data");
        for (int row = 1; row <= count; row++)
        {
            sheet.Cells[$"A{row}"].CellValue = 1d;
        }

        sheet.Cells["B1"].SetFormula($"of:=SUM([.A1:.A{count}])", 0d);
        return document;
    }

    private static SpreadsheetDocument CreateArrayDocument()
    {
        SpreadsheetDocument document = SpreadsheetDocument.Create();
        document.AddSheet("Data")
            .Ranges["A1:CV100"]
            .SetArrayFormula("of:=MUNIT(100)");
        return document;
    }

    private static (SpreadsheetDocument Document, OdfCell Input)
        CreateIncrementalDocument()
    {
        SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Data");
        for (int chain = 0; chain < 100; chain++)
        {
            int firstRow = (chain * 100) + 1;
            sheet.Cells[$"A{firstRow}"].CellValue = 1d;
            for (int offset = 0; offset < 100; offset++)
            {
                int row = firstRow + offset;
                string formula = offset == 0
                    ? $"of:=[.A{firstRow}]+1"
                    : $"of:=[.B{row - 1}]+1";
                sheet.Cells[$"B{row}"].SetFormula(formula, 0d);
            }
        }

        return (document, sheet.Cells["A1"]);
    }
}
