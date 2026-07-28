using System;
using OdfKit.Compliance;

namespace OdfKit.Formula;

/// <summary>
/// Defines security, resource, and scheduling limits for spreadsheet formula evaluation.
/// 定義試算表公式評估的安全性、資源與排程限制。
/// </summary>
public sealed class OdfFormulaEvaluationOptions
{
    private int _maxFormulaCount = 100_000;
    private int _maxFormulaLength = 32_768;
    private int _maxAstDepth = 256;
    private long _maxDependencyEdges = 2_000_000;
    private long _maxOperations = 10_000_000;
    private long _maxCellReads = 10_000_000;
    private long _maxArrayResultCells = 1_000_000;
    private TimeSpan _timeLimit = TimeSpan.FromSeconds(30);
    private int _maxDegreeOfParallelism;

    /// <summary>
    /// Gets or sets the maximum number of formulas scanned in one evaluation.
    /// 取得或設定單次評估可掃描的公式數量上限。
    /// </summary>
    public int MaxFormulaCount
    {
        get => _maxFormulaCount;
        set => _maxFormulaCount = EnsurePositive(value, nameof(MaxFormulaCount));
    }

    /// <summary>
    /// Gets or sets the maximum formula length in characters.
    /// 取得或設定單一公式的字元長度上限。
    /// </summary>
    public int MaxFormulaLength
    {
        get => _maxFormulaLength;
        set => _maxFormulaLength = EnsurePositive(value, nameof(MaxFormulaLength));
    }

    /// <summary>
    /// Gets or sets the maximum nested syntax depth.
    /// 取得或設定語法巢狀深度上限。
    /// </summary>
    public int MaxAstDepth
    {
        get => _maxAstDepth;
        set => _maxAstDepth = EnsurePositive(value, nameof(MaxAstDepth));
    }

    /// <summary>
    /// Gets or sets the maximum number of formula dependency edges.
    /// 取得或設定公式相依邊數量上限。
    /// </summary>
    public long MaxDependencyEdges
    {
        get => _maxDependencyEdges;
        set => _maxDependencyEdges = EnsurePositive(value, nameof(MaxDependencyEdges));
    }

    /// <summary>
    /// Gets or sets the maximum number of evaluation operations.
    /// 取得或設定評估操作次數上限。
    /// </summary>
    public long MaxOperations
    {
        get => _maxOperations;
        set => _maxOperations = EnsurePositive(value, nameof(MaxOperations));
    }

    /// <summary>
    /// Gets or sets the maximum cumulative number of cell reads.
    /// 取得或設定累計儲存格讀取次數上限。
    /// </summary>
    public long MaxCellReads
    {
        get => _maxCellReads;
        set => _maxCellReads = EnsurePositive(value, nameof(MaxCellReads));
    }

    /// <summary>
    /// Gets or sets the maximum cumulative number of array result cells.
    /// 取得或設定累計陣列結果儲存格數量上限。
    /// </summary>
    public long MaxArrayResultCells
    {
        get => _maxArrayResultCells;
        set => _maxArrayResultCells = EnsurePositive(value, nameof(MaxArrayResultCells));
    }

    /// <summary>
    /// Gets or sets the cooperative wall-clock time limit.
    /// 取得或設定協作式經過時間上限。
    /// </summary>
    public TimeSpan TimeLimit
    {
        get => _timeLimit;
        set
        {
            if (value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(TimeLimit),
                    OdfLocalizer.GetMessage("Err_OdfFormulaEvaluationOptions_PositiveTimeLimit"));
            }

            _timeLimit = value;
        }
    }

    /// <summary>
    /// Gets or sets the maximum worker count, where zero selects the shared scheduler default.
    /// 取得或設定最大工作執行緒數；零表示使用共用排程器預設值。
    /// </summary>
    public int MaxDegreeOfParallelism
    {
        get => _maxDegreeOfParallelism;
        set
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(MaxDegreeOfParallelism),
                    OdfLocalizer.GetMessage(
                        "Err_OdfFormulaEvaluationOptions_NonNegative",
                        nameof(MaxDegreeOfParallelism)));
            }

            _maxDegreeOfParallelism = value;
        }
    }

    /// <summary>
    /// Gets or sets the policy for external spreadsheet references.
    /// 取得或設定外部試算表參照政策。
    /// </summary>
    public OdfFormulaExternalReferencePolicy ExternalReferencePolicy { get; set; } =
        OdfFormulaExternalReferencePolicy.CachedOnly;

    /// <summary>
    /// Gets or sets the evaluator instance, or null to use the built-in evaluator.
    /// 取得或設定評估器執行個體；若為 null 則使用內建評估器。
    /// </summary>
    public DefaultFormulaEvaluator? Evaluator { get; set; }

    private static int EnsurePositive(int value, string parameterName)
    {
        if (value < 1)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                OdfLocalizer.GetMessage(
                    "Err_OdfFormulaEvaluationOptions_Positive",
                    parameterName));
        }

        return value;
    }

    private static long EnsurePositive(long value, string parameterName)
    {
        if (value < 1)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                OdfLocalizer.GetMessage(
                    "Err_OdfFormulaEvaluationOptions_Positive",
                    parameterName));
        }

        return value;
    }
}
