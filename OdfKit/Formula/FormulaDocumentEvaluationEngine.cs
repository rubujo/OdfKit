using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
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
    internal static void EvaluateFormulasInDocument(
        OdfNode contentRoot,
        DefaultFormulaEvaluator evaluator,
        OdfExternalLinkManager? externalLinks = null)
    {
        var volatileSession = new OdfFormulaVolatileSession();
        var context = new OdfDomEvaluationContext(
            contentRoot,
            evaluator,
            externalLinks,
            volatileSession);
        var graph = new OdfFormulaDependencyGraph();

        foreach (var kvp in context.CellFormulas)
        {
            graph.UpdateFormulaDependencies(kvp.Key, kvp.Value, context);
        }

        List<List<OdfCellAddress>> levels = graph.GetTopologicalDirtyLevels();
        evaluator.ClearCache();

        LastParallelFormulaLevelCountForTests = levels.Count;
        LastParallelFormulaMaxLevelWidthForTests = 0;
        LastParallelFormulaWorkerDegreeForTests = 0;

        var completed = new ConcurrentDictionary<OdfCellAddress, object>();
        foreach (List<OdfCellAddress> level in levels)
        {
            if (level.Count > LastParallelFormulaMaxLevelWidthForTests)
            {
                LastParallelFormulaMaxLevelWidthForTests = level.Count;
            }

            int workerDegree = Math.Min(level.Count, OdfParallelScheduler.GetEffectiveConcurrency(level.Count));
            if (workerDegree > LastParallelFormulaWorkerDegreeForTests)
            {
                LastParallelFormulaWorkerDegreeForTests = workerDegree;
            }

            var levelResults = new ConcurrentDictionary<OdfCellAddress, object>();
            Parallel.ForEach(
                level,
                new ParallelOptions { MaxDegreeOfParallelism = workerDegree },
                addr =>
                {
                    object result = graph.CircularCells.Contains(addr)
                        ? OdfFormulaError.Ref
                        : EvaluateCellWithCompletedResults(
                            contentRoot,
                            evaluator,
                            externalLinks,
                            volatileSession,
                            completed,
                            addr);
                    levelResults[addr] = result;
                });

            foreach (OdfCellAddress addr in level)
            {
                object result = levelResults[addr];
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
    }

    private static void ApplyArrayResult(
        OdfDomEvaluationContext context,
        DefaultFormulaEvaluator evaluator,
        ConcurrentDictionary<OdfCellAddress, object> completed,
        OdfNode anchorNode,
        OdfCellAddress anchor,
        object[,] result)
    {
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

    private static object EvaluateCellWithCompletedResults(
        OdfNode contentRoot,
        DefaultFormulaEvaluator evaluator,
        OdfExternalLinkManager? externalLinks,
        IOdfFormulaVolatileContext volatileContext,
        ConcurrentDictionary<OdfCellAddress, object> completed,
        OdfCellAddress addr)
    {
        var localEvaluator = new DefaultFormulaEvaluator(evaluator.Functions, evaluator.Fallback);
        var localContext = new OdfDomEvaluationContext(
            contentRoot,
            localEvaluator,
            externalLinks,
            volatileContext);
        foreach (KeyValuePair<OdfCellAddress, object> pair in completed)
        {
            localContext.CellFormulas.Remove(pair.Key);
            localContext.CellValues[pair.Key] = pair.Value;
            localEvaluator.SetCachedValue(pair.Key, pair.Value);
        }

        return localEvaluator.EvaluateCell(addr, localContext);
    }

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
