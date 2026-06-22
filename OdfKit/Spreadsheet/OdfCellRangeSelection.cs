using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 表示工作表中的一個範圍選取。
/// </summary>
public sealed class OdfCellRangeSelection
{
    private readonly OdfTableSheet _sheet;

    /// <summary>
    /// 初始化 <see cref="OdfCellRangeSelection"/> 類別的新執行個體。
    /// </summary>
    /// <param name="sheet">所屬工作表</param>
    /// <param name="range">儲存格範圍</param>
    internal OdfCellRangeSelection(OdfTableSheet sheet, OdfCellRange range)
    {
        _sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
        Range = EnsureSheetName(range, sheet.Name);
    }

    /// <summary>
    /// 取得此選取代表的儲存格範圍。
    /// </summary>
    public OdfCellRange Range { get; }

    /// <summary>
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
    /// 以指定密碼保護此範圍，將其寫入工作表的受保護範圍對照表中。
    /// </summary>
    /// <param name="password">密碼明文</param>
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
    /// 驗證給定密碼是否能解除此範圍的保護。
    /// </summary>
    /// <param name="password">要驗證的密碼</param>
    /// <returns>若驗證成功則為 true，否則為 false</returns>
    public bool VerifyPassword(string password)
    {
        var node = FindProtectedRangeNode();
        if (node is null)
            return false;
        return OdfKit.Core.OdfProtectionHelper.VerifyPassword(node, password, OdfNamespaces.Table);
    }

    /// <summary>
    /// 嘗試以指定密碼解除此範圍的保護。
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
    /// 合併此範圍的儲存格。
    /// </summary>
    public void Merge()
    {
        _sheet.MergeCells(Range);
    }

    /// <summary>
    /// 將此範圍加入命名範圍。
    /// </summary>
    /// <param name="name">命名範圍名稱</param>
    public void NameAs(string name)
    {
        _sheet.AddNamedRange(name, Range);
    }

    /// <summary>
    /// 為此範圍新增篩選。
    /// </summary>
    /// <param name="name">資料庫範圍名稱</param>
    /// <param name="conditions">篩選條件</param>
    public void AddFilter(string name, params (int fieldNumber, string op, string value)[] conditions)
    {
        _sheet.AddDatabaseRange(name, Range).SetFilter(conditions);
    }

    /// <summary>
    /// 為此範圍新增條件格式。
    /// </summary>
    /// <param name="condition">條件運算式</param>
    /// <param name="styleName">套用的樣式名稱</param>
    public void AddConditionalFormat(string condition, string styleName)
    {
        _sheet.AddConditionalFormat(Range, condition, styleName);
    }

    /// <summary>
    /// 為此範圍新增清單型資料驗證。
    /// </summary>
    /// <param name="name">驗證規則名稱</param>
    /// <param name="allowedValues">允許的值</param>
    public void AddValidationList(string name, params string[] allowedValues)
    {
        _sheet.AddValidationList(Range, name, allowedValues);
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
