using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Represents a range selection in a worksheet.
/// 表示工作表中的一個範圍選取。
/// </summary>
public sealed class OdfCellRangeSelection
{
    private readonly OdfTableSheet _sheet;
    private OdfRangeBorderProxy? _borders;

    /// <summary>
    /// Initializes a new instance of the <see cref="OdfCellRangeSelection"/> class.
    /// 初始化 <see cref="OdfCellRangeSelection"/> 類別的新執行個體。
    /// </summary>
    /// <param name="sheet">The owning worksheet. / 所屬工作表。</param>
    /// <param name="range">The cell range. / 儲存格範圍。</param>
    internal OdfCellRangeSelection(OdfTableSheet sheet, OdfCellRange range)
    {
        _sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
        Range = EnsureSheetName(range, sheet.Name);
    }

    /// <summary>
    /// Gets the cell range represented by this selection.
    /// 取得此選取代表的儲存格範圍。
    /// </summary>
    public OdfCellRange Range { get; }

    /// <summary>
    /// Gets the border configuration proxy for this range.
    /// 取得此範圍的框線設定代理。
    /// </summary>
    public OdfRangeBorderProxy Borders => _borders ??= new OdfRangeBorderProxy(_sheet, Range);

    /// <summary>
    /// Gets or sets the horizontal alignment for all cells in this range.
    /// 取得或設定此範圍所有儲存格的水平對齊方式。
    /// </summary>
    public string? HorizontalAlignment
    {
        get
        {
            OdfCell startCell = _sheet.GetCell(Range.StartAddress.Row, Range.StartAddress.Column);
            return startCell.Document.StyleEngine.GetStyleProperty(startCell.StyleName ?? string.Empty, "text-align", OdfNamespaces.Fo, "table-cell");
        }
        set
        {
            foreach (OdfCell cell in EnumerateCells())
            {
                cell.Document.StyleEngine.SetLocalStyleProperty(
                    cell.Node,
                    "table-cell",
                    "paragraph-properties",
                    "text-align",
                    OdfNamespaces.Fo,
                    value,
                    "fo");
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether this range is protected.
    /// 取得一個值，指出此範圍是否已啟用保護。
    /// </summary>
    public bool IsProtected
    {
        get
        {
            var node = FindProtectedRangeNode();
            if (node is null)
                return false;
            return node.GetAttribute("protected", OdfNamespaces.Table) == "true";
        }
    }

    /// <summary>
    /// Protects this range with the specified password and writes it to the protected range map of the worksheet.
    /// 以指定密碼保護此範圍，將其寫入工作表的受保護範圍對照表中。
    /// </summary>
    /// <param name="password">The plain text password. / 密碼明文。</param>
    public void Protect(string password)
    {
        var rangesNode = _sheet.TableNode.Children.Find(c =>
            c.NodeType == OdfNodeType.Element &&
            c.LocalName == "protected-ranges" &&
            c.NamespaceUri == OdfNamespaces.Table);

        if (rangesNode is null)
        {
            rangesNode = OdfNodeFactory.CreateElement("protected-ranges", OdfNamespaces.Table, "table");
            _sheet.TableNode.AppendChild(rangesNode);
        }

        var rangeNode = FindProtectedRangeNode();
        if (rangeNode is null)
        {
            rangeNode = OdfNodeFactory.CreateElement("protected-range", OdfNamespaces.Table, "table");
            string name = "range_protect_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            rangeNode.SetAttribute("name", OdfNamespaces.Table, name, "table");
            rangeNode.SetAttribute("cell-range-address", OdfNamespaces.Table, Range.ToOdfString(false), "table");
            rangesNode.AppendChild(rangeNode);
        }

        OdfKit.Core.OdfProtectionHelper.ProtectNode(rangeNode, password, "table", OdfNamespaces.Table);
    }

    /// <summary>
    /// Removes protection from this range.
    /// 解除此範圍的保護。
    /// </summary>
    public void Unprotect()
    {
        var node = FindProtectedRangeNode();
        if (node is not null)
        {
            var parent = node.Parent;
            parent?.RemoveChild(node);
            if (parent is not null && parent.Children.Count == 0)
            {
                parent.Parent?.RemoveChild(parent);
            }
        }
    }

    /// <summary>
    /// Verifies whether the given password can unlock this range.
    /// 驗證給定密碼是否能解除此範圍的保護。
    /// </summary>
    /// <param name="password">The password to verify. / 要驗證的密碼。</param>
    /// <returns><see langword="true"/> if verification succeeds; otherwise, <see langword="false"/>. / 若驗證成功則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool VerifyPassword(string password)
    {
        var node = FindProtectedRangeNode();
        if (node is null)
            return false;
        return OdfKit.Core.OdfProtectionHelper.VerifyPassword(node, password, OdfNamespaces.Table);
    }

    /// <summary>
    /// Attempts to remove protection from this range with the specified password.
    /// 嘗試以指定密碼解除此範圍的保護。
    /// </summary>
    /// <param name="password">The plain text password. / 密碼明文。</param>
    /// <returns><see langword="true"/> if protection is removed successfully; otherwise, <see langword="false"/>. / 若解除成功則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
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

    private OdfNode? FindProtectedRangeNode()
    {
        var rangesNode = _sheet.TableNode.Children.Find(c =>
            c.NodeType == OdfNodeType.Element &&
            c.LocalName == "protected-ranges" &&
            c.NamespaceUri == OdfNamespaces.Table);

        if (rangesNode is null)
            return null;

        string targetAddress = Range.ToOdfString(false);
        return rangesNode.Children.Find(c =>
            c.NodeType == OdfNodeType.Element &&
            c.LocalName == "protected-range" &&
            c.NamespaceUri == OdfNamespaces.Table &&
            c.GetAttribute("cell-range-address", OdfNamespaces.Table) == targetAddress);
    }

    /// <summary>
    /// Merges the cells in this range.
    /// 合併此範圍的儲存格。
    /// </summary>
    public void Merge()
    {
        _sheet.MergeCells(Range);
    }

    /// <summary>
    /// Unmerges the cells in this range.
    /// 取消合併此範圍的儲存格。
    /// </summary>
    public void Unmerge()
    {
        _sheet.UnmergeCells(Range);
    }

    /// <summary>
    /// Adds this range as a named range.
    /// 將此範圍加入命名範圍。
    /// </summary>
    /// <param name="name">The named range name. / 命名範圍名稱。</param>
    public void NameAs(string name)
    {
        _sheet.AddNamedRange(name, Range);
    }

    /// <summary>
    /// Adds a filter to this range.
    /// 為此範圍新增篩選。
    /// </summary>
    /// <param name="name">The database range name. / 資料庫範圍名稱。</param>
    /// <param name="conditions">The filter conditions. / 篩選條件。</param>
    public void AddFilter(string name, params (int fieldNumber, string op, string value)[] conditions)
    {
        _sheet.AddDatabaseRange(name, Range).SetFilter(conditions);
    }

    /// <summary>
    /// Enables auto-filter buttons for this range.
    /// 為此範圍啟用自動篩選按鈕。
    /// </summary>
    /// <returns>This range selection object for chaining. / 此範圍選取物件，方便鏈式呼叫。</returns>
    public OdfCellRangeSelection AutoFilter()
    {
        _sheet.AutoFilter(Range);
        return this;
    }

    /// <summary>
    /// Sets sort rules for this range.
    /// 為此範圍設定排序規則。
    /// </summary>
    /// <param name="rules">The sort rule array, containing field numbers and whether each field is ascending. / 排序規則陣列，包含欄位編號與是否遞增。</param>
    /// <returns>This range selection object for chaining. / 此範圍選取物件，方便鏈式呼叫。</returns>
    public OdfCellRangeSelection Sort(params (int fieldNumber, bool ascending)[] rules)
    {
        _sheet.Sort(Range, rules);
        return this;
    }

    /// <summary>
    /// Adds conditional formatting to this range.
    /// 為此範圍新增條件格式。
    /// </summary>
    /// <param name="condition">The condition expression. / 條件運算式。</param>
    /// <param name="styleName">The style name to apply. / 套用的樣式名稱。</param>
    public void AddConditionalFormat(string condition, string styleName)
    {
        _sheet.AddConditionalFormat(Range, condition, styleName);
    }

    /// <summary>
    /// Adds list-based data validation to this range.
    /// 為此範圍新增清單型資料驗證。
    /// </summary>
    /// <param name="name">The validation rule name. / 驗證規則名稱。</param>
    /// <param name="allowedValues">The allowed values. / 允許的值。</param>
    public void AddValidationList(string name, params string[] allowedValues)
    {
        _sheet.AddValidationList(Range, name, allowedValues);
    }

    internal IEnumerable<OdfCell> EnumerateCells()
    {
        int minRow = Math.Min(Range.StartAddress.Row, Range.EndAddress.Row);
        int maxRow = Math.Max(Range.StartAddress.Row, Range.EndAddress.Row);
        int minColumn = Math.Min(Range.StartAddress.Column, Range.EndAddress.Column);
        int maxColumn = Math.Max(Range.StartAddress.Column, Range.EndAddress.Column);

        for (int row = minRow; row <= maxRow; row++)
        {
            for (int column = minColumn; column <= maxColumn; column++)
            {
                yield return _sheet.GetCell(row, column);
            }
        }
    }

    private static OdfCellRange EnsureSheetName(OdfCellRange range, string sheetName)
    {
        var start = range.StartAddress;
        var end = range.EndAddress;

        if (start.SheetName is null)
        {
            start = new OdfCellAddress(start.Row, start.Column, sheetName, start.IsRowAbsolute, start.IsColumnAbsolute, start.IsSheetAbsolute);
        }

        if (end.SheetName is null)
        {
            end = new OdfCellAddress(end.Row, end.Column, sheetName, end.IsRowAbsolute, end.IsColumnAbsolute, end.IsSheetAbsolute);
        }

        return new OdfCellRange(start, end);
    }
}
