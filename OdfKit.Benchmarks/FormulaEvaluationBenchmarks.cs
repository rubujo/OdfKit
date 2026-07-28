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
    private SpreadsheetDocument? _fullDocument;
    private SpreadsheetDocument? _linearDocument;
    private SpreadsheetDocument? _wideDocument;
    private SpreadsheetDocument? _rangeDocument;
    private SpreadsheetDocument? _arrayDocument;
    private OdfFormulaDependencyGraph? _incrementalGraph;
    private OdfCellAddress _incrementalRoot;

    /// <summary>
    /// Gets or sets the number of independent formulas used by the full recalculation benchmark.
    /// 取得或設定全量重算基準使用的獨立公式數量。
    /// </summary>
    [Params(1_000, 10_000)]
    public int FormulaCount { get; set; }

    /// <summary>
    /// Builds the independent-formula workbook.
    /// 建立獨立公式活頁簿。
    /// </summary>
    [GlobalSetup(Target = nameof(FullRecalculation))]
    public void SetupFullRecalculation() =>
        _fullDocument = CreateIndependentDocument(FormulaCount);

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
    /// Builds the incremental dependency graph.
    /// 建立增量相依圖。
    /// </summary>
    [GlobalSetup(Target = nameof(IncrementalOnePercentDirtyPropagation))]
    public void SetupIncrementalGraph() =>
        (_incrementalGraph, _incrementalRoot) = CreateIncrementalGraph();

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
    /// <returns>The evaluation report. / 評估報告。</returns>
    [Benchmark(Baseline = true)]
    public OdfFormulaEvaluationReport FullRecalculation() =>
        _fullDocument!.EvaluateFormulas();

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
    /// Propagates one changed input through exactly one percent of a 10,000-formula graph.
    /// 將單一輸入變更傳播至 10,000 個公式圖中恰好百分之一的公式。
    /// </summary>
    /// <returns>The number of dirty cells. / Dirty 儲存格數量。</returns>
    [Benchmark]
    public int IncrementalOnePercentDirtyPropagation()
    {
        OdfFormulaDependencyGraph graph = _incrementalGraph!;
        ClearDirtyFlags(graph);
        graph.MarkDirty(_incrementalRoot);
        return graph.DirtyCells.Count;
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
        _fullDocument?.Dispose();
        _linearDocument?.Dispose();
        _wideDocument?.Dispose();
        _rangeDocument?.Dispose();
        _arrayDocument?.Dispose();
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

    private static (OdfFormulaDependencyGraph Graph, OdfCellAddress Root)
        CreateIncrementalGraph()
    {
        var graph = new OdfFormulaDependencyGraph();
        var context = new EmptyEvaluationContext();
        for (int chain = 0; chain < 100; chain++)
        {
            int firstRow = (chain * 100) + 1;
            for (int offset = 0; offset < 100; offset++)
            {
                int row = firstRow + offset;
                string formula = offset == 0
                    ? "of:=1"
                    : $"of:=[.A{row - 1}]+1";
                graph.UpdateFormulaDependencies(
                    new OdfCellAddress(row - 1, 0, "Data"),
                    formula,
                    context);
            }
        }

        ClearDirtyFlags(graph);
        return (graph, new OdfCellAddress(0, 0, "Data"));
    }

    private static void ClearDirtyFlags(OdfFormulaDependencyGraph graph)
    {
        foreach (OdfCellAddress address in graph.DirtyCells.ToArray())
        {
            graph.ClearDirty(address);
        }
    }

    private sealed class EmptyEvaluationContext : IEvaluationContext
    {
        public OdfCellAddress CurrentCell => default;

        public object GetCellValue(OdfCellAddress address) => 0d;

        public object[,] GetRangeValues(OdfCellRange range) => new object[0, 0];

        public string? GetCellFormula(OdfCellAddress address) => null;

        public object GetNamedRangeOrExpressionValue(string name) =>
            OdfFormulaError.Name;
    }
}
