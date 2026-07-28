using System;
using System.Collections.Generic;

namespace OdfKit.Formula;

/// <summary>
/// Reports resource usage and diagnostics from a spreadsheet formula evaluation.
/// 報告試算表公式評估的資源使用量與診斷。
/// </summary>
public sealed class OdfFormulaEvaluationReport
{
    internal OdfFormulaEvaluationReport(
        int scannedFormulaCount,
        int evaluatedFormulaCount,
        int writtenFormulaCount,
        int formulaErrorCount,
        long dependencyEdgeCount,
        long operationCount,
        long cellReadCount,
        int maximumParallelism,
        TimeSpan elapsed,
        IReadOnlyList<OdfFormulaDiagnostic> diagnostics)
    {
        ScannedFormulaCount = scannedFormulaCount;
        EvaluatedFormulaCount = evaluatedFormulaCount;
        WrittenFormulaCount = writtenFormulaCount;
        FormulaErrorCount = formulaErrorCount;
        DependencyEdgeCount = dependencyEdgeCount;
        OperationCount = operationCount;
        CellReadCount = cellReadCount;
        MaximumParallelism = maximumParallelism;
        Elapsed = elapsed;
        Diagnostics = diagnostics;
    }

    /// <summary>
    /// Gets the number of formulas scanned.
    /// 取得已掃描公式數量。
    /// </summary>
    public int ScannedFormulaCount { get; }

    /// <summary>
    /// Gets the number of formulas evaluated.
    /// 取得已評估公式數量。
    /// </summary>
    public int EvaluatedFormulaCount { get; }

    /// <summary>
    /// Gets the number of formula results committed to the document.
    /// 取得已提交至文件的公式結果數量。
    /// </summary>
    public int WrittenFormulaCount { get; }

    /// <summary>
    /// Gets the number of standard formula error results.
    /// 取得標準公式錯誤結果數量。
    /// </summary>
    public int FormulaErrorCount { get; }

    /// <summary>
    /// Gets the number of formula dependency edges.
    /// 取得公式相依邊數量。
    /// </summary>
    public long DependencyEdgeCount { get; }

    /// <summary>
    /// Gets the number of charged evaluation operations.
    /// 取得已計入的評估操作次數。
    /// </summary>
    public long OperationCount { get; }

    /// <summary>
    /// Gets the number of charged cell reads.
    /// 取得已計入的儲存格讀取次數。
    /// </summary>
    public long CellReadCount { get; }

    /// <summary>
    /// Gets the maximum worker count used during evaluation.
    /// 取得評估期間使用的最大工作執行緒數。
    /// </summary>
    public int MaximumParallelism { get; }

    /// <summary>
    /// Gets the elapsed evaluation time.
    /// 取得評估經過時間。
    /// </summary>
    public TimeSpan Elapsed { get; }

    /// <summary>
    /// Gets structured evaluation diagnostics.
    /// 取得結構化評估診斷。
    /// </summary>
    public IReadOnlyList<OdfFormulaDiagnostic> Diagnostics { get; }
}
