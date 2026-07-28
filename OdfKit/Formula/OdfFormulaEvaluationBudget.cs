using System;
using System.Diagnostics;
using System.Threading;
using OdfKit.Compliance;

namespace OdfKit.Formula;

internal sealed class OdfFormulaEvaluationBudget
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly OdfFormulaEvaluationOptions _options;
    private readonly CancellationToken _cancellationToken;
    private long _dependencyEdges;
    private long _operations;
    private long _cellReads;
    private long _arrayResultCells;

    internal OdfFormulaEvaluationBudget(
        OdfFormulaEvaluationOptions options,
        CancellationToken cancellationToken)
    {
        _options = options;
        _cancellationToken = cancellationToken;
    }

    internal long DependencyEdges => Interlocked.Read(ref _dependencyEdges);

    internal long Operations => Interlocked.Read(ref _operations);

    internal long CellReads => Interlocked.Read(ref _cellReads);

    internal TimeSpan Elapsed => _stopwatch.Elapsed;

    internal CancellationToken CancellationToken => _cancellationToken;

    internal void Checkpoint()
    {
        _cancellationToken.ThrowIfCancellationRequested();
        if (_stopwatch.Elapsed > _options.TimeLimit)
        {
            ThrowLimit(nameof(OdfFormulaEvaluationOptions.TimeLimit));
        }
    }

    internal void ChargeOperation(long count = 1)
    {
        long total = Interlocked.Add(ref _operations, count);
        if (total > _options.MaxOperations)
        {
            ThrowLimit(nameof(OdfFormulaEvaluationOptions.MaxOperations));
        }

        Checkpoint();
    }

    internal void ChargeCellReads(long count)
    {
        long total = Interlocked.Add(ref _cellReads, count);
        if (total > _options.MaxCellReads)
        {
            ThrowLimit(nameof(OdfFormulaEvaluationOptions.MaxCellReads));
        }

        ChargeOperation(count);
    }

    internal void ChargeDependencyEdge()
    {
        long total = Interlocked.Increment(ref _dependencyEdges);
        if (total > _options.MaxDependencyEdges)
        {
            ThrowLimit(nameof(OdfFormulaEvaluationOptions.MaxDependencyEdges));
        }

        Checkpoint();
    }

    internal void ChargeArrayResult(long count)
    {
        long total = Interlocked.Add(ref _arrayResultCells, count);
        if (total > _options.MaxArrayResultCells)
        {
            ThrowLimit(nameof(OdfFormulaEvaluationOptions.MaxArrayResultCells));
        }

        ChargeOperation(count);
    }

    internal void EnsureArrayResultCapacity(long count)
    {
        long current = Interlocked.Read(ref _arrayResultCells);
        if (count < 0 || count > _options.MaxArrayResultCells - current)
        {
            ThrowLimit(nameof(OdfFormulaEvaluationOptions.MaxArrayResultCells));
        }

        Checkpoint();
    }

    private static void ThrowLimit(string limitName)
    {
        throw new OdfFormulaResourceLimitException(
            limitName,
            OdfLocalizer.GetMessage(
                "Err_OdfFormulaEvaluation_ResourceLimitExceeded",
                limitName));
    }
}

internal sealed class OdfFormulaResourceLimitException(string limitName, string message) :
    Exception(message)
{
    internal string LimitName { get; } = limitName;
}

internal sealed class OdfFormulaExternalReferenceDeniedException(string message) :
    Exception(message);
