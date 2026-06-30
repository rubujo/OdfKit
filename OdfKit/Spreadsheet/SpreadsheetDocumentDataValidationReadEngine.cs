using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 試算表資料驗證讀取引擎（內部協作者）。
/// </summary>
internal static class SpreadsheetDocumentDataValidationReadEngine
{
    internal static IReadOnlyList<OdfDataValidationInfo> GetDataValidations(SpreadsheetDocument document)
    {
        OdfNode? validationsNode = OdfTableSheetDomHelper.FindChildElement(
            document.SheetsRoot, "content-validations", OdfNamespaces.Table);
        if (validationsNode is null)
            return [];

        Dictionary<string, List<OdfCellRange>> rangesByName = CollectAppliedRanges(document);
        List<OdfDataValidationInfo> results = [];

        foreach (OdfNode validationNode in validationsNode.Children)
        {
            if (validationNode.NodeType is not OdfNodeType.Element ||
                validationNode.LocalName is not "content-validation" ||
                validationNode.NamespaceUri != OdfNamespaces.Table)
                continue;

            string? name = validationNode.GetAttribute("name", OdfNamespaces.Table);
            if (string.IsNullOrEmpty(name))
                continue;

            string condition = validationNode.GetAttribute("condition", OdfNamespaces.Table) ?? string.Empty;
            string? errorMessage = null;
            string? errorTitle = null;
            string? alertStyle = null;

            foreach (OdfNode child in validationNode.Children)
            {
                if (child.NodeType is not OdfNodeType.Element ||
                    child.LocalName is not "error-message" ||
                    child.NamespaceUri != OdfNamespaces.Table)
                    continue;

                errorMessage = child.GetAttribute("message", OdfNamespaces.Table) ?? ReadElementText(child);
                errorTitle = child.GetAttribute("title", OdfNamespaces.Table);
                alertStyle = child.GetAttribute("message-type", OdfNamespaces.Table);
            }

            rangesByName.TryGetValue(name!, out List<OdfCellRange>? ranges);
            results.Add(new OdfDataValidationInfo(
                name!,
                condition,
                errorMessage,
                errorTitle,
                alertStyle,
                (ranges ?? []).AsReadOnly()));
        }

        return results.AsReadOnly();
    }

    private static Dictionary<string, List<OdfCellRange>> CollectAppliedRanges(SpreadsheetDocument document)
    {
        Dictionary<string, List<OdfCellRange>> rangesByName = new(StringComparer.Ordinal);

        foreach (OdfTableSheet sheet in document.Worksheets)
        {
            foreach ((OdfNode cellNode, int row, int column) in OdfTableSheetDomAccessEngine.EnumerateExistingCells(sheet.TableNode))
            {
                string? validationName = cellNode.GetAttribute("content-validation-name", OdfNamespaces.Table);
                if (string.IsNullOrEmpty(validationName))
                    continue;

                if (!rangesByName.TryGetValue(validationName!, out List<OdfCellRange>? ranges))
                {
                    ranges = [];
                    rangesByName[validationName!] = ranges;
                }

                var address = new OdfCellAddress(row, column, sheet.Name);
                ranges.Add(new OdfCellRange(address, address));
            }
        }

        return rangesByName;
    }

    private static string ReadElementText(OdfNode node)
    {
        if (!string.IsNullOrEmpty(node.TextContent))
            return node.TextContent;

        foreach (OdfNode child in node.Children)
        {
            string text = ReadElementText(child);
            if (!string.IsNullOrEmpty(text))
                return text;
        }

        return string.Empty;
    }
}
