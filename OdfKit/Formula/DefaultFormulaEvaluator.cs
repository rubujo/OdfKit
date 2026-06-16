using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Formula.AST;
using OdfKit.Spreadsheet;

namespace OdfKit.Formula;

/// <summary>
/// 預設的 ODF 公式評估器實現。
/// </summary>
public partial class DefaultFormulaEvaluator : IOdfFormulaEvaluator
{
    private readonly Dictionary<OdfCellAddress, object> _cache = new();
    private readonly HashSet<OdfCellAddress> _evaluatingStack = new();

    /// <summary>
    /// 評估特定儲長格的公式。使用循環參照檢查與快取機制。
    /// </summary>
    /// <param name="cellAddress">儲存格位址</param>
    /// <param name="context">評估內容模型</param>
    /// <returns>評估後的儲存格值</returns>
    public object EvaluateCell(OdfCellAddress cellAddress, IEvaluationContext context)
    {
        if (_cache.TryGetValue(cellAddress, out var cachedValue))
        {
            return cachedValue;
        }

        if (_evaluatingStack.Contains(cellAddress))
        {
            OdfKitDiagnostics.Warn($"Circular dependency detected at cell {cellAddress.ToExcelString()}.");
            return OdfFormulaError.Ref;
        }

        _evaluatingStack.Add(cellAddress);
        try
        {
            string? formula = context.GetCellFormula(cellAddress);
            if (string.IsNullOrEmpty(formula))
            {
                return context.GetCellValue(cellAddress);
            }

            if (formula!.StartsWith("oooc:=", StringComparison.OrdinalIgnoreCase) ||
                formula.StartsWith("of:=", StringComparison.OrdinalIgnoreCase))
            {
                formula = OdfFormulaTranslator.OdfToExcelFormula(formula);
            }

            formula = CleanFormulaPrefix(formula!);

            object result = Evaluate(formula!, context);
            _cache[cellAddress] = result;
            return result;
        }
        catch (Exception ex)
        {
            OdfKitDiagnostics.Error($"Evaluation failed for cell {cellAddress.ToExcelString()}: {ex.Message}", ex);
            return OdfFormulaError.Value;
        }
        finally
        {
            _evaluatingStack.Remove(cellAddress);
        }
    }

    private static string CleanFormulaPrefix(string formula)
    {
        if (formula.StartsWith("oooc:=", StringComparison.OrdinalIgnoreCase))
            return formula.Substring(6);
        if (formula.StartsWith("of:=", StringComparison.OrdinalIgnoreCase))
            return formula.Substring(4);
        if (formula.StartsWith("="))
            return formula.Substring(1);
        return formula;
    }

    /// <summary>
    /// 評估公式字串並傳回結果。
    /// </summary>
    /// <param name="formula">公式字串</param>
    /// <param name="context">評估內容模型</param>
    /// <returns>公式計算後的結果</returns>
    public object Evaluate(string formula, IEvaluationContext context)
    {
        try
        {
            var parser = new FormulaParser(formula);
            var ast = parser.Parse();
            return ast.Evaluate(context);
        }
        catch (Exception ex)
        {
            OdfKitDiagnostics.Warn($"Parser failed on formula '{formula}': {ex.Message}");
            return OdfFormulaError.Value;
        }
    }

    /// <summary>
    /// 清除評估快取與循環相依性追蹤堆疊。
    /// </summary>
    public void ClearCache()
    {
        _cache.Clear();
        _evaluatingStack.Clear();
    }

    /// <summary>
    /// 評估指定內容根節點下的所有文件公式，並更新其顯示文字與屬性。
    /// </summary>
    /// <param name="contentRoot">文件的內容根節點</param>
    public void EvaluateFormulasInDocument(OdfNode contentRoot)
    {
        var context = new OdfDomEvaluationContext(contentRoot, this);
        var addresses = new List<OdfCellAddress>(context.CellFormulas.Keys);

        foreach (var addr in addresses)
        {
            object result = context.GetCellValue(addr);
            if (context.CellNodes.TryGetValue(addr, out var cellNode))
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
                    cellNode.SetAttribute("value-type", OdfNamespaces.Office, "float", "office");
                    cellNode.SetAttribute("value", OdfNamespaces.Office, d.ToString(System.Globalization.CultureInfo.InvariantCulture), "office");
                    cellNode.RemoveAttribute("string-value", OdfNamespaces.Office);
                    cellNode.RemoveAttribute("boolean-value", OdfNamespaces.Office);
                    UpdateCellDisplayText(cellNode, d.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
                else if (result is bool b)
                {
                    cellNode.SetAttribute("value-type", OdfNamespaces.Office, "boolean", "office");
                    cellNode.SetAttribute("boolean-value", OdfNamespaces.Office, b ? "true" : "false", "office");
                    cellNode.RemoveAttribute("value", OdfNamespaces.Office);
                    cellNode.RemoveAttribute("string-value", OdfNamespaces.Office);
                    UpdateCellDisplayText(cellNode, b ? "TRUE" : "FALSE");
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
        }
    }

    private void UpdateCellDisplayText(OdfNode cellNode, string text)
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
