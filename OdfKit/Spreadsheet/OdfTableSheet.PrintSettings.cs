using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

public partial class OdfTableSheet
{
    #region 列印設定


    /// <summary>設定列印範圍。</summary>
    /// <param name="range">列印範圍</param>
    public void SetPrintArea(OdfCellRange range)
    {
        var start = range.StartAddress.SheetName is null
            ? new OdfCellAddress(range.StartAddress.Row, range.StartAddress.Column, Name, true, true, true)
            : range.StartAddress;
        var end = range.EndAddress.SheetName is null
            ? new OdfCellAddress(range.EndAddress.Row, range.EndAddress.Column, Name, true, true, true)
            : range.EndAddress;
        TableNode.SetAttribute("print-ranges", OdfNamespaces.Table, new OdfCellRange(start, end).ToOdfString(), "table");
    }

    /// <summary>取得列印範圍，若未設定則傳回 null。</summary>
    public OdfCellRange? GetPrintArea()
    {
        string? attr = TableNode.GetAttribute("print-ranges", OdfNamespaces.Table);
        if (string.IsNullOrEmpty(attr))
            return null;
        return OdfCellRange.TryParse(attr!, out var r) ? r : (OdfCellRange?)null;
    }

    /// <summary>清除列印範圍設定。</summary>
    public void ClearPrintArea() => TableNode.RemoveAttribute("print-ranges", OdfNamespaces.Table);

    /// <summary>設定標題列（列印時每頁重複的列）。</summary>
    /// <param name="startRow">起始列索引（0 為基準）</param>
    /// <param name="endRow">結束列索引（包含，0 為基準）</param>
    public void SetPrintTitleRows(int startRow, int endRow)
    {
        ClearPrintTitleRows();

        // 收集要移入 header-rows 的列節點
        var rowsToWrap = new List<OdfNode>();
        for (int r = startRow; r <= endRow; r++)
            rowsToWrap.Add(GetOrCreateRowNodeInternal(r, forWrite: true));

        // 建立 header-rows 節點並插入所有列
        var headerRows = new OdfNode(OdfNodeType.Element, "header-rows", OdfNamespaces.Table, "table");
        foreach (var rowNode in rowsToWrap)
        {
            TableNode.RemoveChild(rowNode);
            headerRows.AppendChild(rowNode);
        }

        // 插入 header-rows 在第一個 table-row 之前（欄定義之後）
        OdfNode? insertBefore = null;
        foreach (var child in TableNode.Children)
        {
            if (child.LocalName == "table-row" && child.NamespaceUri == OdfNamespaces.Table)
            {
                insertBefore = child;
                break;
            }
        }
        if (insertBefore is not null)
            TableNode.InsertBefore(headerRows, insertBefore);
        else
            TableNode.AppendChild(headerRows);
    }

    /// <summary>清除標題列設定。</summary>
    public void ClearPrintTitleRows()
    {
        OdfNode? headerRows = FindChildElement(TableNode, "header-rows", OdfNamespaces.Table);
        if (headerRows is null)
            return;

        // 把 header-rows 內的列移回 TableNode 主體
        OdfNode? insertAfter = headerRows;
        foreach (var child in new List<OdfNode>(headerRows.Children))
        {
            if (child.LocalName == "table-row" && child.NamespaceUri == OdfNamespaces.Table)
            {
                headerRows.RemoveChild(child);
                TableNode.InsertAfter(child, insertAfter);
                insertAfter = child;
            }
        }
        TableNode.RemoveChild(headerRows);
    }

    /// <summary>設定標題欄（列印時每頁重複的欄）。</summary>
    /// <param name="startCol">起始欄索引（0 為基準）</param>
    /// <param name="endCol">結束欄索引（包含，0 為基準）</param>
    public void SetPrintTitleColumns(int startCol, int endCol)
    {
        ClearPrintTitleColumns();

        var colsToWrap = new List<OdfNode>();
        for (int c = startCol; c <= endCol; c++)
            colsToWrap.Add(GetOrCreateColumnNode(c));

        var headerCols = new OdfNode(OdfNodeType.Element, "header-columns", OdfNamespaces.Table, "table");
        foreach (var colNode in colsToWrap)
        {
            TableNode.RemoveChild(colNode);
            headerCols.AppendChild(colNode);
        }

        OdfNode? insertBefore = null;
        foreach (var child in TableNode.Children)
        {
            if (child.LocalName == "table-column" && child.NamespaceUri == OdfNamespaces.Table)
            {
                insertBefore = child;
                break;
            }
        }
        if (insertBefore is not null)
            TableNode.InsertBefore(headerCols, insertBefore);
        else
            TableNode.AppendChild(headerCols);
    }

    /// <summary>清除標題欄設定。</summary>
    public void ClearPrintTitleColumns()
    {
        OdfNode? headerCols = FindChildElement(TableNode, "header-columns", OdfNamespaces.Table);
        if (headerCols is null)
            return;

        OdfNode? insertAfter = headerCols;
        foreach (var child in new List<OdfNode>(headerCols.Children))
        {
            if (child.LocalName == "table-column" && child.NamespaceUri == OdfNamespaces.Table)
            {
                headerCols.RemoveChild(child);
                TableNode.InsertAfter(child, insertAfter);
                insertAfter = child;
            }
        }
        TableNode.RemoveChild(headerCols);
    }

    /// <summary>在指定列之後插入手動列分頁符。</summary>
    /// <param name="afterRow">分頁符位於此列之後（0 為基準）</param>
    public void InsertRowPageBreak(int afterRow)
    {
        var rowNode = GetOrCreateRowNodeInternal(afterRow + 1, forWrite: true);
        rowNode.SetAttribute("break-before", OdfNamespaces.Fo, "page", "fo");
    }

    /// <summary>移除指定列的手動分頁符。</summary>
    /// <param name="afterRow">分頁符位於此列之後（0 為基準）</param>
    public void RemoveRowPageBreak(int afterRow)
    {
        var rowNode = GetOrCreateRowNodeInternal(afterRow + 1, forWrite: false);
        rowNode.RemoveAttribute("break-before", OdfNamespaces.Fo);
    }

    /// <summary>在指定欄之後插入手動欄分頁符。</summary>
    /// <param name="afterCol">分頁符位於此欄之後（0 為基準）</param>
    public void InsertColumnPageBreak(int afterCol)
    {
        var colNode = GetOrCreateColumnNode(afterCol + 1);
        colNode.SetAttribute("break-before", OdfNamespaces.Fo, "page", "fo");
    }

    /// <summary>移除指定欄的手動分頁符。</summary>
    /// <param name="afterCol">分頁符位於此欄之後（0 為基準）</param>
    public void RemoveColumnPageBreak(int afterCol)
    {
        var colNode = GetOrCreateColumnNode(afterCol + 1);
        colNode.RemoveAttribute("break-before", OdfNamespaces.Fo);
    }

    /// <summary>設定列印縮放比例（1–400），傳入 0 代表恢復自動。</summary>
    /// <param name="percent">縮放比例（百分比）</param>
    public void SetPrintScale(int percent)
    {
        var props = GetOrCreatePageLayoutPropertiesForSheet();
        if (percent <= 0)
        {
            props.RemoveAttribute("scale-to", OdfNamespaces.Style);
            props.RemoveAttribute("scale-to-pages", OdfNamespaces.Style);
        }
        else
        {
            props.SetAttribute("scale-to", OdfNamespaces.Style, $"{percent}%", "style");
            props.RemoveAttribute("scale-to-pages", OdfNamespaces.Style);
        }
    }

    /// <summary>設定縮放以適合指定頁數。</summary>
    /// <param name="maxPagesWide">最大橫向頁數（0 代表不限制）</param>
    /// <param name="maxPagesTall">最大縱向頁數（0 代表不限制）</param>
    public void SetFitToPage(int maxPagesWide = 1, int maxPagesTall = 0)
    {
        var props = GetOrCreatePageLayoutPropertiesForSheet();
        props.RemoveAttribute("scale-to", OdfNamespaces.Style);
        if (maxPagesWide > 0 || maxPagesTall > 0)
            props.SetAttribute("scale-to-pages", OdfNamespaces.Style, (maxPagesWide > 0 ? maxPagesWide : maxPagesTall).ToString(), "style");
        else
            props.RemoveAttribute("scale-to-pages", OdfNamespaces.Style);
    }

    private OdfNode GetOrCreatePageLayoutPropertiesForSheet()
    {
        string masterPageName = TableNode.GetAttribute("master-page-name", OdfNamespaces.Table) ?? "Default";

        OdfNode? masterStylesSection = null;
        OdfNode? autoStylesSection = null;
        foreach (var child in _doc.StylesDom.Children)
        {
            if (child.LocalName == "master-styles" && child.NamespaceUri == OdfNamespaces.Office)
                masterStylesSection = child;
            else if (child.LocalName == "automatic-styles" && child.NamespaceUri == OdfNamespaces.Office)
                autoStylesSection = child;
        }

        autoStylesSection ??= new OdfNode(OdfNodeType.Element, "automatic-styles", OdfNamespaces.Office, "office");
        masterStylesSection ??= new OdfNode(OdfNodeType.Element, "master-styles", OdfNamespaces.Office, "office");

        // Find or create the page layout name
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
            pageLayoutName = "pm_" + Name;
            // Create master page entry
            var mp = new OdfNode(OdfNodeType.Element, "master-page", OdfNamespaces.Style, "style");
            mp.SetAttribute("name", OdfNamespaces.Style, masterPageName, "style");
            mp.SetAttribute("page-layout-name", OdfNamespaces.Style, pageLayoutName, "style");
            if (masterStylesSection.Parent is null)
                _doc.StylesDom.AppendChild(masterStylesSection);
            masterStylesSection.AppendChild(mp);
        }

        // Find or create the page layout in automatic-styles
        foreach (var pl in autoStylesSection.Children)
        {
            if (pl.LocalName == "page-layout" && pl.NamespaceUri == OdfNamespaces.Style
                && pl.GetAttribute("name", OdfNamespaces.Style) == pageLayoutName)
                return FindOrCreateChild(pl, "page-layout-properties", OdfNamespaces.Style, "style");
        }

        // Page layout not found — create it
        if (autoStylesSection.Parent is null)
            _doc.StylesDom.AppendChild(autoStylesSection);
        var pageLayout = new OdfNode(OdfNodeType.Element, "page-layout", OdfNamespaces.Style, "style");
        pageLayout.SetAttribute("name", OdfNamespaces.Style, pageLayoutName, "style");
        var plProps = new OdfNode(OdfNodeType.Element, "page-layout-properties", OdfNamespaces.Style, "style");
        pageLayout.AppendChild(plProps);
        autoStylesSection.AppendChild(pageLayout);
        return plProps;
    }


    #endregion
}
