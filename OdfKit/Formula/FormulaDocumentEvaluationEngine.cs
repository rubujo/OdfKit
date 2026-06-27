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
        var context = new OdfDomEvaluationContext(contentRoot, evaluator, externalLinks);
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
                        : EvaluateCellWithCompletedResults(contentRoot, externalLinks, completed, addr);
                    levelResults[addr] = result;
                });

            foreach (OdfCellAddress addr in level)
            {
                object result = levelResults[addr];
                completed[addr] = result;
                evaluator.SetCachedValue(addr, result);

                if (context.CellNodes.TryGetValue(addr, out var cellNode))
                {
                    ApplyResultToCell(cellNode, addr, result);
                    context.CellFormulas.Remove(addr);
                    context.CellValues[addr] = result;
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

    private static object EvaluateCellWithCompletedResults(
        OdfNode contentRoot,
        OdfExternalLinkManager? externalLinks,
        ConcurrentDictionary<OdfCellAddress, object> completed,
        OdfCellAddress addr)
    {
        var localEvaluator = new DefaultFormulaEvaluator();
        var localContext = new OdfDomEvaluationContext(contentRoot, localEvaluator, externalLinks);
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
