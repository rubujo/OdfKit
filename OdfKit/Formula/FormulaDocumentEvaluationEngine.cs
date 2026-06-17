using System;
using System.Collections.Generic;
using System.Globalization;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Spreadsheet;

namespace OdfKit.Formula;

/// <summary>
/// 文件層級公式評估與儲存格 DOM 結果寫回引擎（內部協作者）。
/// </summary>
internal static class FormulaDocumentEvaluationEngine
{
    /// <summary>
    /// 評估指定內容根節點下的所有文件公式，並更新其顯示文字與屬性。
    /// </summary>
    internal static void EvaluateFormulasInDocument(OdfNode contentRoot, DefaultFormulaEvaluator evaluator)
    {
        var context = new OdfDomEvaluationContext(contentRoot, evaluator);
        var addresses = new List<OdfCellAddress>(context.CellFormulas.Keys);

        foreach (var addr in addresses)
        {
            object result = context.GetCellValue(addr);
            if (context.CellNodes.TryGetValue(addr, out var cellNode))
                ApplyResultToCell(cellNode, addr, result);
        }
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
