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
    /// 取得或設定工作表的名稱。
    /// </summary>
    public string Name
    {
        get => TableNode.GetAttribute("name", OdfNamespaces.Table) ?? string.Empty;
        set => TableNode.SetAttribute("name", OdfNamespaces.Table, value, "table");
    }

    /// <summary>
    /// 取得此工作表的儲存格集合。
    /// </summary>
    public OdfCellCollection Cells => _cells ??= new OdfCellCollection(this);

    /// <summary>
    /// 取得此工作表的列集合。
    /// </summary>
    public OdfRowCollection Rows => _rows ??= new OdfRowCollection(this);

    /// <summary>
    /// 取得此工作表的欄集合。
    /// </summary>
    public OdfColumnCollection Columns => _columns ??= new OdfColumnCollection(this);

    /// <summary>
    /// 取得此工作表的儲存格範圍集合。
    /// </summary>
    public OdfRangeCollection Ranges => _ranges ??= new OdfRangeCollection(this);

    /// <summary>
    /// 取得此工作表中已使用的儲存格列舉。
    /// </summary>
    public IEnumerable<OdfCell> UsedCells => GetUsedCells();

    /// <summary>
    /// 將此工作表從試算表 DOM 樹中剪裁，並釋放其子樹與延遲載入 XML 參照。
    /// </summary>
    /// <param name="collectGarbage">是否在剪裁後要求執行一次最佳化 GC 收集</param>
    /// <returns>已剪裁的節點數，包含工作表節點本身</returns>
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
    /// 取得或設定工作表是否顯示。
    /// </summary>
    public bool Visible
    {
        get => TableNode.GetAttribute("visibility", OdfNamespaces.Table) != "collapse";
        set => TableNode.SetAttribute("visibility", OdfNamespaces.Table, value ? "visible" : "collapse", "table");
    }

    /// <summary>
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
    /// 取得一個值，指出此工作表是否啟用保護。
    /// </summary>
    public bool IsProtected
    {
        get => TableNode.GetAttribute("protected", OdfNamespaces.Table) == "true";
    }

    /// <summary>
    /// 啟用工作表保護，並設定雜湊後的密碼。
    /// </summary>
    /// <param name="password">密碼明文</param>
    public void Protect(string password)
    {
        OdfProtectionHelper.ProtectNode(TableNode, password, "table", OdfNamespaces.Table);
    }

    /// <summary>
    /// 驗證工作表保護密碼是否正確。
    /// </summary>
    /// <param name="password">要驗證的密碼</param>
    /// <returns>若驗證成功則為 true，否則為 false</returns>
    public bool VerifyPassword(string password)
    {
        return OdfProtectionHelper.VerifyPassword(TableNode, password, OdfNamespaces.Table);
    }

    /// <summary>
    /// 解除工作表保護。
    /// </summary>
    public void Unprotect()
    {
        OdfProtectionHelper.UnprotectNode(TableNode, OdfNamespaces.Table);
    }

    /// <summary>
    /// 嘗試以指定密碼解除工作表保護。
    /// </summary>
    /// <param name="password">密碼明文</param>
    /// <returns>若解除成功則為 true，否則為 false</returns>
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
    /// 取得指定列與欄索引的儲存格。
    /// </summary>
    /// <param name="row">以 0 為基準的列索引</param>
    /// <param name="col">以 0 為基準的欄索引</param>
    /// <returns>儲存格物件</returns>
    public OdfCell GetCell(int row, int col)
    {
        var cellNode = GetOrCreateCellNode(row, col);
        return new OdfCell(cellNode, row, col, _doc, Name);
    }

    /// <summary>
    /// 取得指定位址的儲存格。
    /// </summary>
    /// <param name="address">儲存格位址字串（例如 "A1" 或 "Sheet1.A1"）</param>
    /// <returns>儲存格物件</returns>
    /// <exception cref="FormatException">當儲存格格式無效時擲出</exception>
    public OdfCell GetCell(string address)
    {
        if (!OdfCellAddress.TryParse(address, out var addr))
        {
            throw new FormatException(OdfLocalizer.GetMessage("Err_OdfTableSheet_InvalidCellAddress", address));
        }
        return GetCell(addr.Row, addr.Column);
    }

    /// <summary>
    /// 取得指定範圍中的儲存格列舉。
    /// </summary>
    /// <param name="range">要列舉的儲存格範圍</param>
    /// <returns>範圍內的儲存格列舉</returns>
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
    /// 取得此工作表中已使用的儲存格列舉。
    /// </summary>
    /// <returns>已使用儲存格列舉</returns>
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
    /// 合併指定的儲存格範圍，並可選擇性套用外框線。
    /// </summary>
    /// <param name="range">儲存格範圍</param>
    /// <param name="outerBorder">套用於合併範圍外部的外框線格式</param>
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
    /// 取消合併指定的儲存格範圍。
    /// </summary>
    /// <param name="range">要取消合併的儲存格範圍。</param>
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
    /// 取消合併指定的 Excel 樣式儲存格範圍。
    /// </summary>
    /// <param name="rangeAddress">儲存格範圍位址，例如 <c>A1:C3</c>。</param>
    public void UnmergeCells(string rangeAddress)
    {
        UnmergeCells(OdfCellRange.ParseExcel(rangeAddress));
    }
}
