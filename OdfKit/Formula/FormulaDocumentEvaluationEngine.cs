using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Spreadsheet;

namespace OdfKit.Formula;

/// <summary>
/// 文件層級公式評估與儲存格 DOM 結果寫回引擎（內部協作者）。
/// </summary>
internal static class FormulaDocumentEvaluationEngine
{
    internal static int LastParallelFormulaLevelCountForTests { get; private set; }

    internal static int LastParallelFormulaMaxLevelWidthForTests { get; private set; }

    internal static int LastParallelFormulaWorkerDegreeForTests { get; private set; }

    /// <summary>
    /// 評估指定內容根節點下的所有文件公式，並更新其顯示文字與屬性。
    /// </summary>
    internal static OdfFormulaEvaluationReport EvaluateFormulasInDocument(
        OdfNode contentRoot,
        DefaultFormulaEvaluator evaluator,
        OdfExternalLinkManager? externalLinks,
        OdfFormulaEvaluationOptions options,
        CancellationToken cancellationToken)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(contentRoot, nameof(contentRoot));
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(evaluator, nameof(evaluator));
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(options, nameof(options));

        var diagnostics = new List<OdfFormulaDiagnostic>();
        var budget = new OdfFormulaEvaluationBudget(options, cancellationToken);
        var volatileSession = new OdfFormulaVolatileSession();
        var originalContext = new OdfDomEvaluationContext(
            contentRoot,
            evaluator,
            externalLinks,
            volatileSession,
            budget,
            options.ExternalReferencePolicy);

        try
        {
            Preflight(originalContext, evaluator, options, budget, diagnostics);
            var stagedRoot = contentRoot.CloneNode(deep: true);
            EvaluationStatistics statistics = EvaluateCore(
                stagedRoot,
                evaluator,
                externalLinks,
                volatileSession,
                options,
                budget);

            cancellationToken.ThrowIfCancellationRequested();
            budget.Checkpoint();

            var stagedContext = new OdfDomEvaluationContext(
                stagedRoot,
                evaluator,
                externalLinks,
                volatileSession,
                budget,
                options.ExternalReferencePolicy);
            int written = CommitFormulaResults(originalContext, stagedContext);
            return CreateReport(
                originalContext.CellFormulas.Count,
                statistics.EvaluatedFormulaCount,
                written,
                statistics.FormulaErrorCount,
                statistics.MaximumParallelism,
                budget,
                diagnostics);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (OdfFormulaEvaluationException)
        {
            throw;
        }
        catch (OdfFormulaExternalReferenceDeniedException ex)
        {
            diagnostics.Add(new OdfFormulaDiagnostic(
                "OF1002",
                ex.Message,
                OdfFormulaDiagnosticSeverity.Error));
            throw new OdfFormulaEvaluationException(
                OdfFormulaEvaluationFailureReason.UnsupportedFormula,
                CreateReport(
                    originalContext.CellFormulas.Count,
                    0,
                    0,
                    0,
                    0,
                    budget,
                    diagnostics),
                ex.Message);
        }
        catch (OdfFormulaResourceLimitException ex)
        {
            diagnostics.Add(new OdfFormulaDiagnostic(
                "OF1003",
                ex.Message,
                OdfFormulaDiagnosticSeverity.Error));
            throw new OdfFormulaEvaluationException(
                OdfFormulaEvaluationFailureReason.ResourceLimitExceeded,
                CreateReport(
                    originalContext.CellFormulas.Count,
                    0,
                    0,
                    0,
                    0,
                    budget,
                    diagnostics),
                ex.Message);
        }
        catch (Exception ex)
        {
            diagnostics.Add(new OdfFormulaDiagnostic(
                "OF1004",
                ex.Message,
                OdfFormulaDiagnosticSeverity.Error));
            throw new OdfFormulaEvaluationException(
                OdfFormulaEvaluationFailureReason.EvaluationFailed,
                CreateReport(
                    originalContext.CellFormulas.Count,
                    0,
                    0,
                    0,
                    0,
                    budget,
                    diagnostics),
                OdfLocalizer.GetMessage(
                    "Err_OdfFormulaEvaluation_Failed",
                    ex.Message));
        }
    }

    private static EvaluationStatistics EvaluateCore(
        OdfNode contentRoot,
        DefaultFormulaEvaluator evaluator,
        OdfExternalLinkManager? externalLinks,
        IOdfFormulaVolatileContext volatileSession,
        OdfFormulaEvaluationOptions options,
        OdfFormulaEvaluationBudget budget)
    {
        var context = new OdfDomEvaluationContext(
            contentRoot,
            evaluator,
            externalLinks,
            volatileSession,
            budget,
            options.ExternalReferencePolicy);
        var graph = new OdfFormulaDependencyGraph();

        foreach (var kvp in context.CellFormulas)
        {
            budget.Checkpoint();
            graph.UpdateFormulaDependencies(kvp.Key, kvp.Value, context);
        }

        List<List<OdfCellAddress>> levels = graph.GetTopologicalDirtyLevels();
        evaluator.ClearCache();

        LastParallelFormulaLevelCountForTests = levels.Count;
        LastParallelFormulaMaxLevelWidthForTests = 0;
        LastParallelFormulaWorkerDegreeForTests = 0;

        var completed = new ConcurrentDictionary<OdfCellAddress, object>();
        int formulaErrors = 0;
        int evaluated = 0;
        foreach (List<OdfCellAddress> level in levels)
        {
            budget.Checkpoint();
            if (level.Count > LastParallelFormulaMaxLevelWidthForTests)
            {
                LastParallelFormulaMaxLevelWidthForTests = level.Count;
            }

            int requestedConcurrency = options.MaxDegreeOfParallelism > 0
                ? options.MaxDegreeOfParallelism
                : level.Count;
            int workerDegree = Math.Min(
                level.Count,
                OdfParallelScheduler.GetEffectiveConcurrency(requestedConcurrency));
            if (workerDegree > LastParallelFormulaWorkerDegreeForTests)
            {
                LastParallelFormulaWorkerDegreeForTests = workerDegree;
            }

            var levelResults = new ConcurrentDictionary<OdfCellAddress, object>();
            if (workerDegree == 1)
            {
                bool previousStrict = evaluator.ThrowOnEvaluationFailure;
                evaluator.ThrowOnEvaluationFailure = true;
                try
                {
                    foreach (OdfCellAddress addr in level)
                    {
                        budget.ChargeOperation();
                        object result = graph.CircularCells.Contains(addr)
                            ? OdfFormulaError.Ref
                            : evaluator.EvaluateCell(addr, context);
                        levelResults[addr] = result;
                    }
                }
                finally
                {
                    evaluator.ThrowOnEvaluationFailure = previousStrict;
                }
            }
            else
            {
                var chunks = new List<(int Start, int End)>(workerDegree);
                int chunkSize = (level.Count + workerDegree - 1) / workerDegree;
                for (int start = 0; start < level.Count; start += chunkSize)
                {
                    chunks.Add((start, Math.Min(level.Count, start + chunkSize)));
                }

                Parallel.ForEach(
                    chunks,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = workerDegree,
                        CancellationToken = budget.CancellationToken
                    },
                    chunk => EvaluateChunkWithCompletedResults(
                        context,
                        evaluator,
                        level,
                        chunk.Start,
                        chunk.End,
                        levelResults,
                        graph,
                        budget));
            }

            foreach (OdfCellAddress addr in level)
            {
                object result = levelResults[addr];
                evaluated++;
                if (result is OdfFormulaError)
                {
                    formulaErrors++;
                }

                if (context.CellNodes.TryGetValue(addr, out var cellNode))
                {
                    if (result is object[,] array)
                    {
                        ApplyArrayResult(
                            context,
                            evaluator,
                            completed,
                            cellNode,
                            addr,
                            array);
                    }
                    else
                    {
                        completed[addr] = result;
                        evaluator.SetCachedValue(addr, result);
                        ApplyResultToCell(cellNode, addr, result);
                        context.CellValues[addr] = result;
                    }

                    context.CellFormulas.Remove(addr);
                }
                else
                {
                    completed[addr] = result;
                    evaluator.SetCachedValue(addr, result);
                }
            }
        }

        foreach (OdfCellAddress addr in graph.CircularCells)
        {
            object result = OdfFormulaError.Ref;
            completed[addr] = result;
            evaluator.SetCachedValue(addr, result);

            if (context.CellNodes.TryGetValue(addr, out var cellNode))
            {
                ApplyResultToCell(cellNode, addr, result);
            }
        }

        return new EvaluationStatistics(
            evaluated,
            formulaErrors,
            LastParallelFormulaWorkerDegreeForTests);
    }

    private static void ApplyArrayResult(
        OdfDomEvaluationContext context,
        DefaultFormulaEvaluator evaluator,
        ConcurrentDictionary<OdfCellAddress, object> completed,
        OdfNode anchorNode,
        OdfCellAddress anchor,
        object[,] result)
    {
        context.Budget?.ChargeArrayResult(checked((long)result.GetLength(0) * result.GetLength(1)));
        int declaredColumns = ParsePositiveSpan(
            anchorNode,
            "number-matrix-columns-spanned");
        int declaredRows = ParsePositiveSpan(
            anchorNode,
            "number-matrix-rows-spanned");
        if (declaredRows != result.GetLength(0) ||
            declaredColumns != result.GetLength(1))
        {
            ApplyArrayCell(
                context,
                evaluator,
                completed,
                anchorNode,
                anchor,
                OdfFormulaError.NA);
            return;
        }

        for (int row = 0; row < declaredRows; row++)
        {
            for (int column = 0; column < declaredColumns; column++)
            {
                var address = new OdfCellAddress(
                    anchor.Row + row,
                    anchor.Column + column,
                    anchor.SheetName);
                if (!context.CellNodes.ContainsKey(address) ||
                    address != anchor &&
                    context.CellFormulas.ContainsKey(address))
                {
                    ApplyArrayCell(
                        context,
                        evaluator,
                        completed,
                        anchorNode,
                        anchor,
                        OdfFormulaError.Ref);
                    return;
                }
            }
        }

        for (int row = 0; row < declaredRows; row++)
        {
            for (int column = 0; column < declaredColumns; column++)
            {
                var address = new OdfCellAddress(
                    anchor.Row + row,
                    anchor.Column + column,
                    anchor.SheetName);
                ApplyArrayCell(
                    context,
                    evaluator,
                    completed,
                    context.CellNodes[address],
                    address,
                    result[row, column]);
            }
        }
    }

    private static int ParsePositiveSpan(OdfNode cellNode, string localName)
    {
        string? text = cellNode.GetAttribute(localName, OdfNamespaces.Table);
        return int.TryParse(
            text,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out int value) &&
            value > 0
            ? value
            : 1;
    }

    private static void ApplyArrayCell(
        OdfDomEvaluationContext context,
        DefaultFormulaEvaluator evaluator,
        ConcurrentDictionary<OdfCellAddress, object> completed,
        OdfNode cellNode,
        OdfCellAddress address,
        object value)
    {
        completed[address] = value;
        evaluator.SetCachedValue(address, value);
        context.CellValues[address] = value;
        ApplyResultToCell(cellNode, address, value);
    }

    private static void EvaluateChunkWithCompletedResults(
        OdfDomEvaluationContext context,
        DefaultFormulaEvaluator evaluator,
        List<OdfCellAddress> level,
        int start,
        int end,
        ConcurrentDictionary<OdfCellAddress, object> levelResults,
        OdfFormulaDependencyGraph graph,
        OdfFormulaEvaluationBudget budget)
    {
        var localEvaluator = new DefaultFormulaEvaluator(evaluator.Functions, evaluator.Fallback);
        localEvaluator.ThrowOnEvaluationFailure = true;
        OdfDomEvaluationContext localContext = context.CreateWorkerView(localEvaluator);

        for (int index = start; index < end; index++)
        {
            budget.ChargeOperation();
            OdfCellAddress address = level[index];
            levelResults[address] = graph.CircularCells.Contains(address)
                ? OdfFormulaError.Ref
                : localEvaluator.EvaluateCell(address, localContext);
        }
    }

    private static void Preflight(
        OdfDomEvaluationContext context,
        DefaultFormulaEvaluator evaluator,
        OdfFormulaEvaluationOptions options,
        OdfFormulaEvaluationBudget budget,
        List<OdfFormulaDiagnostic> diagnostics)
    {
        if (context.CellFormulas.Count > options.MaxFormulaCount)
        {
            throw new OdfFormulaResourceLimitException(
                nameof(options.MaxFormulaCount),
                OdfLocalizer.GetMessage(
                    "Err_OdfFormulaEvaluation_ResourceLimitExceeded",
                    nameof(options.MaxFormulaCount)));
        }

        foreach (KeyValuePair<OdfCellAddress, string> pair in context.CellFormulas)
        {
            budget.Checkpoint();
            string formula = pair.Value;
            if (formula.Length > options.MaxFormulaLength)
            {
                throw new OdfFormulaResourceLimitException(
                    nameof(options.MaxFormulaLength),
                    OdfLocalizer.GetMessage(
                        "Err_OdfFormulaEvaluation_ResourceLimitExceeded",
                        nameof(options.MaxFormulaLength)));
            }

            if (GetMaximumNestingDepth(formula) > options.MaxAstDepth)
            {
                throw new OdfFormulaResourceLimitException(
                    nameof(options.MaxAstDepth),
                    OdfLocalizer.GetMessage(
                        "Err_OdfFormulaEvaluation_ResourceLimitExceeded",
                        nameof(options.MaxAstDepth)));
            }

            try
            {
                string normalized = formula;
                if (normalized.StartsWith("of:=", StringComparison.OrdinalIgnoreCase) ||
                    normalized.StartsWith("oooc:=", StringComparison.OrdinalIgnoreCase))
                {
                    normalized = OdfFormulaTranslator.OdfToExcelFormula(normalized);
                }

                normalized = FormulaPrefixNormalizer.RemovePrefix(normalized);
                _ = new FormulaParser(normalized).Parse();
            }
            catch (Exception ex)
            {
                diagnostics.Add(new OdfFormulaDiagnostic(
                    "OF1001",
                    ex.Message,
                    OdfFormulaDiagnosticSeverity.Error));
                throw new OdfFormulaEvaluationException(
                    OdfFormulaEvaluationFailureReason.InvalidFormula,
                    CreateReport(
                        context.CellFormulas.Count,
                        0,
                        0,
                        0,
                        0,
                        budget,
                        diagnostics),
                    OdfLocalizer.GetMessage(
                        "Err_OdfFormulaEvaluation_InvalidFormula",
                        pair.Key.ToExcelString()));
            }

            OdfFormulaAnalysis analysis = OdfFormulaSupport.Analyze(formula, evaluator.Functions);
            if (analysis.HasUnsupportedFunctions && evaluator.Fallback is null)
            {
                diagnostics.AddRange(analysis.Diagnostics);
                OdfFormulaEvaluationReport report = CreateReport(
                    context.CellFormulas.Count,
                    0,
                    0,
                    0,
                    0,
                    budget,
                    diagnostics);
                throw new OdfFormulaEvaluationException(
                    OdfFormulaEvaluationFailureReason.UnsupportedFormula,
                    report,
                    OdfLocalizer.GetMessage(
                        "Err_OdfFormulaEvaluation_UnsupportedFormula",
                        pair.Key.ToExcelString()));
            }

            budget.ChargeOperation();
        }
    }

    private static int GetMaximumNestingDepth(string formula)
    {
        int depth = 0;
        int maximum = 0;
        bool quoted = false;
        for (int index = 0; index < formula.Length; index++)
        {
            char current = formula[index];
            if (current == '"')
            {
                if (quoted && index + 1 < formula.Length && formula[index + 1] == '"')
                {
                    index++;
                    continue;
                }

                quoted = !quoted;
                continue;
            }

            if (quoted)
            {
                continue;
            }

            if (current is '(' or '{')
            {
                depth++;
                maximum = Math.Max(maximum, depth);
            }
            else if (current is ')' or '}')
            {
                depth = Math.Max(0, depth - 1);
            }
        }

        return maximum;
    }

    private static int CommitFormulaResults(
        OdfDomEvaluationContext originalContext,
        OdfDomEvaluationContext stagedContext)
    {
        int written = 0;
        foreach (KeyValuePair<OdfCellAddress, OdfNode> pair in stagedContext.CellNodes)
        {
            bool isFormulaCell = originalContext.CellFormulas.ContainsKey(pair.Key);
            bool valueChanged =
                stagedContext.CellValues.TryGetValue(pair.Key, out object? stagedValue) &&
                (!originalContext.CellValues.TryGetValue(pair.Key, out object? originalValue) ||
                 !Equals(stagedValue, originalValue));
            if ((!isFormulaCell && !valueChanged) ||
                !originalContext.CellNodes.TryGetValue(pair.Key, out OdfNode? originalCell))
            {
                continue;
            }

            OdfNode stagedCell = pair.Value;
            CopyResultAttribute(stagedCell, originalCell, "value-type");
            CopyResultAttribute(stagedCell, originalCell, "value");
            CopyResultAttribute(stagedCell, originalCell, "string-value");
            CopyResultAttribute(stagedCell, originalCell, "boolean-value");
            CopyResultAttribute(stagedCell, originalCell, "date-value");
            CopyResultAttribute(stagedCell, originalCell, "time-value");

            originalCell.Children.Clear();
            foreach (OdfNode child in stagedCell.Children)
            {
                originalCell.AppendChild(child.CloneNode(deep: true));
            }

            written++;
        }

        return written;
    }

    private static void CopyResultAttribute(
        OdfNode source,
        OdfNode destination,
        string localName)
    {
        string? value = source.GetAttribute(localName, OdfNamespaces.Office);
        if (value is null)
        {
            destination.RemoveAttribute(localName, OdfNamespaces.Office);
            return;
        }

        destination.SetAttribute(localName, OdfNamespaces.Office, value, "office");
    }

    private static OdfFormulaEvaluationReport CreateReport(
        int scanned,
        int evaluated,
        int written,
        int formulaErrors,
        int maximumParallelism,
        OdfFormulaEvaluationBudget budget,
        List<OdfFormulaDiagnostic> diagnostics)
        => new(
            scanned,
            evaluated,
            written,
            formulaErrors,
            budget.DependencyEdges,
            budget.Operations,
            budget.CellReads,
            maximumParallelism,
            budget.Elapsed,
            diagnostics.AsReadOnly());

    private readonly record struct EvaluationStatistics(
        int EvaluatedFormulaCount,
        int FormulaErrorCount,
        int MaximumParallelism);

    internal static void ApplyResultToCell(OdfNode cellNode, OdfCellAddress addr, object result)
    {
        if (result is OdfFormulaError err)
        {
            string errStr = err.ToErrorString();
            OdfKitDiagnostics.Warn($"Formula evaluation error at {addr.ToExcelString()}: {errStr}");

            cellNode.SetAttribute("value-type", OdfNamespaces.Office, "string", "office");
            cellNode.SetAttribute("string-value", OdfNamespaces.Office, errStr, "office");
            cellNode.RemoveAttribute("value", OdfNamespaces.Office);
            cellNode.RemoveAttribute("boolean-value", OdfNamespaces.Office);
            UpdateCellDisplayText(cellNode, errStr);
        }
        else if (result is double d)
        {
            string text = d.ToString(CultureInfo.InvariantCulture);
            cellNode.SetAttribute("value-type", OdfNamespaces.Office, "float", "office");
            cellNode.SetAttribute("value", OdfNamespaces.Office, text, "office");
            cellNode.RemoveAttribute("string-value", OdfNamespaces.Office);
            cellNode.RemoveAttribute("boolean-value", OdfNamespaces.Office);
            UpdateCellDisplayText(cellNode, text);
        }
        else if (result is bool b)
        {
            string text = b ? "TRUE" : "FALSE";
            cellNode.SetAttribute("value-type", OdfNamespaces.Office, "boolean", "office");
            cellNode.SetAttribute("boolean-value", OdfNamespaces.Office, b ? "true" : "false", "office");
            cellNode.RemoveAttribute("value", OdfNamespaces.Office);
            cellNode.RemoveAttribute("string-value", OdfNamespaces.Office);
            UpdateCellDisplayText(cellNode, text);
        }
        else
        {
            string str = result?.ToString() ?? "";
            cellNode.SetAttribute("value-type", OdfNamespaces.Office, "string", "office");
            cellNode.SetAttribute("string-value", OdfNamespaces.Office, str, "office");
            cellNode.RemoveAttribute("value", OdfNamespaces.Office);
            cellNode.RemoveAttribute("boolean-value", OdfNamespaces.Office);
            UpdateCellDisplayText(cellNode, str);
        }
    }

    internal static void UpdateCellDisplayText(OdfNode cellNode, string text)
    {
        OdfNode? pNode = null;
        foreach (var child in cellNode.Children)
        {
            if (child.NodeType == OdfNodeType.Element && child.LocalName == "p" && child.NamespaceUri == OdfNamespaces.Text)
            {
                pNode = child;
                break;
            }
        }

        if (pNode == null)
        {
            pNode = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
            cellNode.AppendChild(pNode);
        }

        pNode.TextContent = text;
    }
}
