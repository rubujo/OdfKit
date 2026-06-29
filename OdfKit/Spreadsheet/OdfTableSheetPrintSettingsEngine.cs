using System.Collections.Generic;
using System.Globalization;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 工作表列印設定引擎（內部協作者）。
/// </summary>
internal static class OdfTableSheetPrintSettingsEngine
{
    internal static void SetPrintArea(OdfTableSheetMutationContext context, OdfCellRange range)
    {
        var start = range.StartAddress.SheetName is null
            ? new OdfCellAddress(range.StartAddress.Row, range.StartAddress.Column, context.SheetName, true, true, true)
            : range.StartAddress;
        var end = range.EndAddress.SheetName is null
            ? new OdfCellAddress(range.EndAddress.Row, range.EndAddress.Column, context.SheetName, true, true, true)
            : range.EndAddress;
        context.TableNode.SetAttribute("print-ranges", OdfNamespaces.Table, new OdfCellRange(start, end).ToOdfString(), "table");
    }

    internal static OdfCellRange? GetPrintArea(OdfTableSheetMutationContext context)
    {
        string? attr = context.TableNode.GetAttribute("print-ranges", OdfNamespaces.Table);
        if (string.IsNullOrEmpty(attr))
            return null;
        return OdfCellRange.TryParse(attr!, out var r) ? r : (OdfCellRange?)null;
    }

    internal static void ClearPrintArea(OdfTableSheetMutationContext context) =>
        context.TableNode.RemoveAttribute("print-ranges", OdfNamespaces.Table);

    internal static void SetPrintTitleRows(OdfTableSheetMutationContext context, int startRow, int endRow)
    {
        ClearPrintTitleRows(context);
        var rowsToWrap = new List<OdfNode>();
        for (int r = startRow; r <= endRow; r++)
            rowsToWrap.Add(context.GetOrCreateRow(r, forWrite: true));

        var headerRows = new OdfNode(OdfNodeType.Element, "header-rows", OdfNamespaces.Table, "table");
        foreach (var rowNode in rowsToWrap)
        {
            context.TableNode.RemoveChild(rowNode);
            headerRows.AppendChild(rowNode);
        }

        OdfNode? insertBefore = null;
        foreach (var child in context.TableNode.Children)
        {
            if (child.LocalName == "table-row" && child.NamespaceUri == OdfNamespaces.Table)
            {
                insertBefore = child;
                break;
            }
        }
        if (insertBefore is not null)
            context.TableNode.InsertBefore(headerRows, insertBefore);
        else
            context.TableNode.AppendChild(headerRows);
    }

    internal static void ClearPrintTitleRows(OdfTableSheetMutationContext context)
    {
        OdfNode? headerRows = OdfTableSheetDomHelper.FindChildElement(context.TableNode, "header-rows", OdfNamespaces.Table);
        if (headerRows is null)
            return;

        OdfNode? insertAfter = headerRows;
        foreach (var child in new List<OdfNode>(headerRows.Children))
        {
            if (child.LocalName == "table-row" && child.NamespaceUri == OdfNamespaces.Table)
            {
                headerRows.RemoveChild(child);
                context.TableNode.InsertAfter(child, insertAfter);
                insertAfter = child;
            }
        }
        context.TableNode.RemoveChild(headerRows);
    }

    internal static void SetPrintTitleColumns(OdfTableSheetMutationContext context, int startCol, int endCol)
    {
        ClearPrintTitleColumns(context);
        var colsToWrap = new List<OdfNode>();
        for (int c = startCol; c <= endCol; c++)
            colsToWrap.Add(context.GetOrCreateColumn(c));

        var headerCols = new OdfNode(OdfNodeType.Element, "header-columns", OdfNamespaces.Table, "table");
        foreach (var colNode in colsToWrap)
        {
            context.TableNode.RemoveChild(colNode);
            headerCols.AppendChild(colNode);
        }

        OdfNode? insertBefore = null;
        foreach (var child in context.TableNode.Children)
        {
            if (child.LocalName == "table-column" && child.NamespaceUri == OdfNamespaces.Table)
            {
                insertBefore = child;
                break;
            }
        }
        if (insertBefore is not null)
            context.TableNode.InsertBefore(headerCols, insertBefore);
        else
            context.TableNode.AppendChild(headerCols);
    }

    internal static void ClearPrintTitleColumns(OdfTableSheetMutationContext context)
    {
        OdfNode? headerCols = OdfTableSheetDomHelper.FindChildElement(context.TableNode, "header-columns", OdfNamespaces.Table);
        if (headerCols is null)
            return;

        OdfNode? insertAfter = headerCols;
        foreach (var child in new List<OdfNode>(headerCols.Children))
        {
            if (child.LocalName == "table-column" && child.NamespaceUri == OdfNamespaces.Table)
            {
                headerCols.RemoveChild(child);
                context.TableNode.InsertAfter(child, insertAfter);
                insertAfter = child;
            }
        }
        context.TableNode.RemoveChild(headerCols);
    }

    internal static void InsertRowPageBreak(OdfTableSheetMutationContext context, int afterRow)
    {
        var rowNode = context.GetOrCreateRow(afterRow + 1, forWrite: true);
        rowNode.SetAttribute("break-before", OdfNamespaces.Fo, "page", "fo");
    }

    internal static bool RemoveRowPageBreak(OdfTableSheetMutationContext context, int afterRow)
    {
        var rowNode = OdfTableSheetDomAccessEngine.TryFindRowNode(context.TableNode, afterRow + 1);
        return rowNode is not null && rowNode.RemoveAttribute("break-before", OdfNamespaces.Fo);
    }

    internal static void InsertColumnPageBreak(OdfTableSheetMutationContext context, int afterCol)
    {
        var colNode = context.GetOrCreateColumn(afterCol + 1);
        colNode.SetAttribute("break-before", OdfNamespaces.Fo, "page", "fo");
    }

    internal static bool RemoveColumnPageBreak(OdfTableSheetMutationContext context, int afterCol)
    {
        var colNode = OdfTableSheetDomAccessEngine.TryFindColumnNode(context.TableNode, afterCol + 1);
        return colNode is not null && colNode.RemoveAttribute("break-before", OdfNamespaces.Fo);
    }

    internal static void SetPrintScale(OdfTableSheetMutationContext context, int percent)
    {
        var props = GetOrCreatePageLayoutPropertiesForSheet(context);
        if (percent <= 0)
        {
            props.RemoveAttribute("scale-to", OdfNamespaces.Style);
            props.RemoveAttribute("scale-to-pages", OdfNamespaces.Style);
        }
        else
        {
            props.SetAttribute("scale-to", OdfNamespaces.Style, $"{percent.ToString(CultureInfo.InvariantCulture)}%", "style");
            props.RemoveAttribute("scale-to-pages", OdfNamespaces.Style);
        }
    }

    internal static void SetFitToPage(OdfTableSheetMutationContext context, int maxPagesWide, int maxPagesTall)
    {
        var props = GetOrCreatePageLayoutPropertiesForSheet(context);
        props.RemoveAttribute("scale-to", OdfNamespaces.Style);
        if (maxPagesWide > 0 || maxPagesTall > 0)
            props.SetAttribute("scale-to-pages", OdfNamespaces.Style, (maxPagesWide > 0 ? maxPagesWide : maxPagesTall).ToString(), "style");
        else
            props.RemoveAttribute("scale-to-pages", OdfNamespaces.Style);
    }

    private static OdfNode GetOrCreatePageLayoutPropertiesForSheet(OdfTableSheetMutationContext context)
    {
        string masterPageName = context.TableNode.GetAttribute("master-page-name", OdfNamespaces.Table) ?? "Default";

        OdfNode? masterStylesSection = null;
        OdfNode? autoStylesSection = null;
        foreach (var child in context.Document.StylesDom.Children)
        {
            if (child.LocalName == "master-styles" && child.NamespaceUri == OdfNamespaces.Office)
                masterStylesSection = child;
            else if (child.LocalName == "automatic-styles" && child.NamespaceUri == OdfNamespaces.Office)
                autoStylesSection = child;
        }

        autoStylesSection ??= new OdfNode(OdfNodeType.Element, "automatic-styles", OdfNamespaces.Office, "office");
        masterStylesSection ??= new OdfNode(OdfNodeType.Element, "master-styles", OdfNamespaces.Office, "office");

        string? pageLayoutName = null;
        if (masterStylesSection.Parent is not null)
        {
            foreach (var mp in masterStylesSection.Children)
            {
                if (mp.LocalName == "master-page" && mp.NamespaceUri == OdfNamespaces.Style
                    && mp.GetAttribute("name", OdfNamespaces.Style) == masterPageName)
                {
                    pageLayoutName = mp.GetAttribute("page-layout-name", OdfNamespaces.Style);
                    break;
                }
            }
        }

        if (pageLayoutName is null)
        {
            pageLayoutName = "pm_" + context.SheetName;
            var mp = new OdfNode(OdfNodeType.Element, "master-page", OdfNamespaces.Style, "style");
            mp.SetAttribute("name", OdfNamespaces.Style, masterPageName, "style");
            mp.SetAttribute("page-layout-name", OdfNamespaces.Style, pageLayoutName, "style");
            if (masterStylesSection.Parent is null)
                context.Document.StylesDom.AppendChild(masterStylesSection);
            masterStylesSection.AppendChild(mp);
        }

        foreach (var pl in autoStylesSection.Children)
        {
            if (pl.LocalName == "page-layout" && pl.NamespaceUri == OdfNamespaces.Style
                && pl.GetAttribute("name", OdfNamespaces.Style) == pageLayoutName)
                return OdfTableSheetDomHelper.FindOrCreateChild(pl, "page-layout-properties", OdfNamespaces.Style, "style");
        }

        if (autoStylesSection.Parent is null)
            context.Document.StylesDom.AppendChild(autoStylesSection);
        var pageLayout = new OdfNode(OdfNodeType.Element, "page-layout", OdfNamespaces.Style, "style");
        pageLayout.SetAttribute("name", OdfNamespaces.Style, pageLayoutName, "style");
        var plProps = new OdfNode(OdfNodeType.Element, "page-layout-properties", OdfNamespaces.Style, "style");
        pageLayout.AppendChild(plProps);
        autoStylesSection.AppendChild(pageLayout);
        return plProps;
    }
}
