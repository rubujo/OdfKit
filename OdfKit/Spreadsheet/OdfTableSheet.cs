using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

using OdfKit.Compliance;
namespace OdfKit.Spreadsheet;

/// <summary>
/// Represents a worksheet in an ODF spreadsheet.
/// 表示 ODF 試算表中的工作表。
/// </summary>
public partial class OdfTableSheet
{
    private OdfCellCollection? _cells;
    private OdfRowCollection? _rows;
    private OdfColumnCollection? _columns;
    private OdfRangeCollection? _ranges;

    internal OdfTableSheet(OdfNode tableNode, SpreadsheetDocument doc)
    {
        TableNode = tableNode ?? throw new ArgumentNullException(nameof(tableNode));
        _doc = doc ?? throw new ArgumentNullException(nameof(doc));
    }

    /// <summary>
    /// 取得代表此工作表的 XML 節點。
    /// </summary>
    internal OdfNode TableNode { get; }

    private readonly SpreadsheetDocument _doc;

    internal SpreadsheetDocument Document => _doc;

    /// <summary>
    /// Gets or sets the worksheet name.
    /// 取得或設定工作表的名稱。
    /// </summary>
    public string Name
    {
        get => TableNode.GetAttribute("name", OdfNamespaces.Table) ?? string.Empty;
        set => TableNode.SetAttribute("name", OdfNamespaces.Table, value, "table");
    }

    /// <summary>
    /// Gets the cell collection for this worksheet.
    /// 取得此工作表的儲存格集合。
    /// </summary>
    public OdfCellCollection Cells => _cells ??= new OdfCellCollection(this);

    /// <summary>
    /// Gets the row collection for this worksheet.
    /// 取得此工作表的列集合。
    /// </summary>
    public OdfRowCollection Rows => _rows ??= new OdfRowCollection(this);

    /// <summary>
    /// Gets the column collection for this worksheet.
    /// 取得此工作表的欄集合。
    /// </summary>
    public OdfColumnCollection Columns => _columns ??= new OdfColumnCollection(this);

    /// <summary>
    /// Gets the cell range collection for this worksheet.
    /// 取得此工作表的儲存格範圍集合。
    /// </summary>
    public OdfRangeCollection Ranges => _ranges ??= new OdfRangeCollection(this);

    /// <summary>
    /// Gets the used cells in this worksheet.
    /// 取得此工作表中已使用的儲存格列舉。
    /// </summary>
    public IEnumerable<OdfCell> UsedCells => GetUsedCells();

    /// <summary>
    /// Prunes this worksheet from the spreadsheet DOM tree and releases its subtree references.
    /// 將此工作表從試算表 DOM 樹中剪裁，並釋放其子樹與延遲載入 XML 參照。
    /// </summary>
    /// <param name="collectGarbage">Whether to request an optimized GC collection after pruning. / 是否在剪裁後要求執行一次最佳化 GC 收集。</param>
    /// <returns>The number of pruned nodes, including the worksheet node itself. / 已剪裁的節點數，包含工作表節點本身。</returns>
    public int PruneAndCollect(bool collectGarbage = false)
    {
        ReleaseFacadeCaches();
        return TableNode.PruneAndCollect(collectGarbage);
    }

    internal int FacadeCacheCount =>
        (_cells is null ? 0 : 1) +
        (_rows is null ? 0 : 1) +
        (_columns is null ? 0 : 1) +
        (_ranges is null ? 0 : 1);

    /// <summary>
    /// Releases cached high-level collection facades while preserving the underlying DOM and document content.
    /// 釋放此工作表已建立的高階集合 facade 快取，但保留底層 DOM 與文件內容不變。
    /// </summary>
    /// <remarks>
    /// 適合在處理大型工作表後主動解除 <see cref="Cells"/>、<see cref="Rows"/>、
    /// <see cref="Columns"/> 與 <see cref="Ranges"/> 的入口物件參照，降低長時間批次流程的
    /// wrapper 物件保留量。後續再次存取這些屬性時會按需重新建立 facade。
    /// </remarks>
    public void ReleaseFacadeCaches()
    {
        _cells = null;
        _rows = null;
        _columns = null;
        _ranges = null;
    }

    /// <summary>
    /// Gets or sets whether the worksheet is visible.
    /// 取得或設定工作表是否顯示。
    /// </summary>
    public bool Visible
    {
        get => TableNode.GetAttribute("visibility", OdfNamespaces.Table) != "collapse";
        set => TableNode.SetAttribute("visibility", OdfNamespaces.Table, value ? "visible" : "collapse", "table");
    }

    /// <summary>
    /// Gets or sets the accessibility summary for the worksheet, mapped to the ODF <c>table:summary</c> attribute.
    /// 取得或設定工作表的無障礙摘要說明（對應 ODF <c>table:summary</c> 屬性）。
    /// </summary>
    public string? Summary
    {
        get => TableNode.GetAttribute("summary", OdfNamespaces.Table);
        set
        {
            if (string.IsNullOrEmpty(value))
                TableNode.RemoveAttribute("summary", OdfNamespaces.Table);
            else
                TableNode.SetAttribute("summary", OdfNamespaces.Table, value!, "table");
        }
    }

    /// <summary>
    /// Gets or sets the worksheet writing mode.
    /// 取得或設定工作表的書寫方向。
    /// </summary>
    public OdfWritingMode WritingMode
    {
        get
        {
            string? directValue = TableNode.GetAttribute("writing-mode", OdfNamespaces.Style);
            if (!string.IsNullOrEmpty(directValue))
            {
                return OdfWritingModeExtensions.FromOdfToken(directValue);
            }

            return OdfWritingModeExtensions.FromOdfToken(_doc.StyleEngine.GetStyleProperty(
                TableNode.GetAttribute("style-name", OdfNamespaces.Table) ?? string.Empty,
                "writing-mode",
                OdfNamespaces.Style,
                "table"));
        }
        set
        {
            string token = value.ToOdfToken();
            TableNode.SetAttribute("writing-mode", OdfNamespaces.Style, token, "style");
            _doc.StyleEngine.SetLocalStyleProperty(
            TableNode,
            "table",
            "table-properties",
            "writing-mode",
            OdfNamespaces.Style,
            token,
            "style");
        }
    }

    /// <summary>
    /// Gets a value indicating whether worksheet protection is enabled.
    /// 取得一個值，指出此工作表是否啟用保護。
    /// </summary>
    public bool IsProtected
    {
        get => TableNode.GetAttribute("protected", OdfNamespaces.Table) == "true";
    }

    /// <summary>
    /// Enables worksheet protection and stores the hashed password.
    /// 啟用工作表保護，並設定雜湊後的密碼。
    /// </summary>
    /// <param name="password">The plain text password. / 密碼明文。</param>
    public void Protect(string password)
    {
        OdfProtectionHelper.ProtectNode(TableNode, password, "table", OdfNamespaces.Table);
    }

    /// <summary>
    /// Verifies whether the worksheet protection password is valid.
    /// 驗證工作表保護密碼是否正確。
    /// </summary>
    /// <param name="password">The password to verify. / 要驗證的密碼。</param>
    /// <returns><see langword="true"/> if verification succeeds; otherwise <see langword="false"/>. / 若驗證成功則為 true，否則為 false。</returns>
    public bool VerifyPassword(string password)
    {
        return OdfProtectionHelper.VerifyPassword(TableNode, password, OdfNamespaces.Table);
    }

    /// <summary>
    /// Disables worksheet protection.
    /// 解除工作表保護。
    /// </summary>
    public void Unprotect()
    {
        OdfProtectionHelper.UnprotectNode(TableNode, OdfNamespaces.Table);
    }

    /// <summary>
    /// Attempts to disable worksheet protection with the specified password.
    /// 嘗試以指定密碼解除工作表保護。
    /// </summary>
    /// <param name="password">The plain text password. / 密碼明文。</param>
    /// <returns><see langword="true"/> if unprotection succeeds; otherwise <see langword="false"/>. / 若解除成功則為 true，否則為 false。</returns>
    public bool TryUnprotect(string password)
    {
        if (!IsProtected)
            return true;
        if (VerifyPassword(password))
        {
            Unprotect();
            return true;
        }
        return false;
    }

    private static bool CompareBytes(byte[] a, byte[] b)
    {
        return OdfEncryption.ByteArrayEquals(a, b);
    }

    /// <summary>
    /// Gets the cell at the specified row and column indexes.
    /// 取得指定列與欄索引的儲存格。
    /// </summary>
    /// <param name="row">The zero-based row index. / 以 0 為基準的列索引。</param>
    /// <param name="col">The zero-based column index. / 以 0 為基準的欄索引。</param>
    /// <returns>The cell object. / 儲存格物件。</returns>
    public OdfCell GetCell(int row, int col)
    {
        var cellNode = GetOrCreateCellNode(row, col);
        return new OdfCell(cellNode, row, col, _doc, Name);
    }

    /// <summary>
    /// Gets the cell at the specified address.
    /// 取得指定位址的儲存格。
    /// </summary>
    /// <param name="address">The cell address string (e.g. "A1" or "Sheet1.A1"). / 儲存格位址字串（例如 "A1" 或 "Sheet1.A1"）。</param>
    /// <returns>The cell object. / 儲存格物件。</returns>
    /// <exception cref="FormatException">Thrown when the cell format is invalid. / 當儲存格格式無效時擲出。</exception>
    public OdfCell GetCell(string address)
    {
        if (!OdfCellAddress.TryParse(address, out var addr))
        {
            throw new FormatException(OdfLocalizer.GetMessage("Err_OdfTableSheet_InvalidCellAddress", address));
        }
        return GetCell(addr.Row, addr.Column);
    }

    /// <summary>
    /// Gets the cells in the specified range.
    /// 取得指定範圍中的儲存格列舉。
    /// </summary>
    /// <param name="range">The cell range to enumerate. / 要列舉的儲存格範圍。</param>
    /// <returns>The cell enumeration within the range. / 範圍內的儲存格列舉。</returns>
    public IEnumerable<OdfCell> GetRange(OdfCellRange range)
    {
        int startRow = Math.Min(range.StartAddress.Row, range.EndAddress.Row);
        int endRow = Math.Max(range.StartAddress.Row, range.EndAddress.Row);
        int startCol = Math.Min(range.StartAddress.Column, range.EndAddress.Column);
        int endCol = Math.Max(range.StartAddress.Column, range.EndAddress.Column);

        for (int row = startRow; row <= endRow; row++)
        {
            for (int col = startCol; col <= endCol; col++)
            {
                yield return GetCell(row, col);
            }
        }
    }

    /// <summary>
    /// Gets the used cells in this worksheet.
    /// 取得此工作表中已使用的儲存格列舉。
    /// </summary>
    /// <returns>The used cell enumeration. / 已使用儲存格列舉。</returns>
    public IEnumerable<OdfCell> GetUsedCells()
    {
        foreach ((OdfNode node, int row, int column) in OdfTableSheetDomAccessEngine.EnumerateExistingCells(TableNode))
        {
            if (OdfTableSheetDomAccessEngine.IsUsedCell(node))
            {
                yield return new OdfCell(node, row, column, _doc, Name);
            }
        }
    }

    /// <summary>
    /// Merges the specified cell range and optionally applies an outer border.
    /// 合併指定的儲存格範圍，並可選擇性套用外框線。
    /// </summary>
    /// <param name="range">The cell range. / 儲存格範圍。</param>
    /// <param name="outerBorder">The border format applied to the outside of the merged range. / 套用於合併範圍外部的外框線格式。</param>
    public void MergeCells(OdfCellRange range, OdfBorder? outerBorder = null)
    {
        int startRow = Math.Min(range.StartAddress.Row, range.EndAddress.Row);
        int endRow = Math.Max(range.StartAddress.Row, range.EndAddress.Row);
        int startCol = Math.Min(range.StartAddress.Column, range.EndAddress.Column);
        int endCol = Math.Max(range.StartAddress.Column, range.EndAddress.Column);

        var mainCell = GetCell(startRow, startCol);
        mainCell.Node.SetAttribute("number-columns-spanned", OdfNamespaces.Table, (endCol - startCol + 1).ToString(), "table");
        mainCell.Node.SetAttribute("number-rows-spanned", OdfNamespaces.Table, (endRow - startRow + 1).ToString(), "table");

        for (int r = startRow; r <= endRow; r++)
        {
            for (int c = startCol; c <= endCol; c++)
            {
                if (r == startRow && c == startCol)
                    continue;

                var coveredNode = new OdfNode(OdfNodeType.Element, "covered-table-cell", OdfNamespaces.Table, "table");
                ReplaceCellNode(r, c, coveredNode);

                if (outerBorder.HasValue)
                {
                    var cellBorderTop = (r == startRow) ? outerBorder.Value : OdfBorder.None;
                    var cellBorderBottom = (r == endRow) ? outerBorder.Value : OdfBorder.None;
                    var cellBorderLeft = (c == startCol) ? outerBorder.Value : OdfBorder.None;
                    var cellBorderRight = (c == endCol) ? outerBorder.Value : OdfBorder.None;

                    var coveredCell = new OdfCell(coveredNode, r, c, _doc, Name);
                    coveredCell.SetBorders(cellBorderTop, cellBorderBottom, cellBorderLeft, cellBorderRight);
                }
            }
        }
    }

    /// <summary>
    /// Unmerges the specified cell range.
    /// 取消合併指定的儲存格範圍。
    /// </summary>
    /// <param name="range">The cell range. / 要取消合併的儲存格範圍。</param>
    public void UnmergeCells(OdfCellRange range)
    {
        int startRow = Math.Min(range.StartAddress.Row, range.EndAddress.Row);
        int endRow = Math.Max(range.StartAddress.Row, range.EndAddress.Row);
        int startCol = Math.Min(range.StartAddress.Column, range.EndAddress.Column);
        int endCol = Math.Max(range.StartAddress.Column, range.EndAddress.Column);

        OdfNode mainCell = GetOrCreateCellNode(startRow, startCol);
        mainCell.RemoveAttribute("number-columns-spanned", OdfNamespaces.Table);
        mainCell.RemoveAttribute("number-rows-spanned", OdfNamespaces.Table);

        for (int row = startRow; row <= endRow; row++)
        {
            for (int column = startCol; column <= endCol; column++)
            {
                if (row == startRow && column == startCol)
                    continue;

                OdfNode cellNode = GetOrCreateCellNode(row, column);
                if (cellNode.LocalName == "covered-table-cell" && cellNode.NamespaceUri == OdfNamespaces.Table)
                {
                    ReplaceCellNode(row, column, new OdfNode(OdfNodeType.Element, "table-cell", OdfNamespaces.Table, "table"));
                }
            }
        }
    }

    /// <summary>
    /// Unmerges the specified Excel-style cell range.
    /// 取消合併指定的 Excel 樣式儲存格範圍。
    /// </summary>
    /// <param name="rangeAddress">The cell range. / 儲存格範圍位址，例如 <c>A1:C3</c>。</param>
    public void UnmergeCells(string rangeAddress)
    {
        UnmergeCells(OdfCellRange.ParseExcel(rangeAddress));
    }
}
