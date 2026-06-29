using System;
using System.Globalization;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Represents a database range in an ODF spreadsheet.
/// 表示 ODF 試算表中的資料庫範圍。
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="OdfDatabaseRange"/> class.
/// 初始化 <see cref="OdfDatabaseRange"/> 類別的新執行個體。
/// </remarks>
/// <param name="node">The XML node. / XML 節點。</param>
/// <param name="doc">The spreadsheet document. / 試算表文件。</param>
public class OdfDatabaseRange(OdfNode node, SpreadsheetDocument doc)
{
    /// <summary>
    /// Gets the XML node of the database range.
    /// 取得資料庫範圍的 XML 節點。
    /// </summary>
    public OdfNode Node { get; } = node;

    private readonly SpreadsheetDocument _doc = doc;

    /// <summary>
    /// Gets or sets the database range name.
    /// 取得或設定資料庫範圍的名稱。
    /// </summary>
    public string Name
    {
        get => Node.GetAttribute("name", OdfNamespaces.Table) ?? string.Empty;
        set => Node.SetAttribute("name", OdfNamespaces.Table, value, "table");
    }

    /// <summary>
    /// Gets or sets the target range address.
    /// 取得或設定目標範圍位址。
    /// </summary>
    public string TargetRangeAddress
    {
        get => Node.GetAttribute("target-range-address", OdfNamespaces.Table) ?? string.Empty;
        set => Node.SetAttribute("target-range-address", OdfNamespaces.Table, value, "table");
    }

    /// <summary>
    /// Gets or sets whether auto-filter buttons are displayed.
    /// 取得或設定是否顯示自動篩選按鈕。
    /// </summary>
    public bool DisplayFilterButtons
    {
        get => Node.GetAttribute("display-filter-buttons", OdfNamespaces.Table) == "true";
        set => Node.SetAttribute("display-filter-buttons", OdfNamespaces.Table, value ? "true" : "false", "table");
    }

    /// <summary>
    /// Sets sort rules for this database range.
    /// 設定此資料庫範圍的排序規則。
    /// </summary>
    /// <param name="rules">The sort rule array, containing field numbers and whether each field is ascending. / 排序規則陣列，包含欄位編號與是否遞增。</param>
    public void SetSort(params (int fieldNumber, bool ascending)[] rules)
    {
        OdfNode? existingSort = null;
        foreach (var child in Node.Children)
        {
            if (child.LocalName == "sort" && child.NamespaceUri == OdfNamespaces.Table)
            {
                existingSort = child;
                break;
            }
        }
        if (existingSort is not null)
        {
            Node.RemoveChild(existingSort);
        }

        if (rules is null || rules.Length == 0)
            return;

        var sortNode = new OdfNode(OdfNodeType.Element, "sort", OdfNamespaces.Table, "table");
        foreach (var rule in rules)
        {
            var sortBy = new OdfNode(OdfNodeType.Element, "sort-by", OdfNamespaces.Table, "table");
            sortBy.SetAttribute("field-number", OdfNamespaces.Table, rule.fieldNumber.ToString(CultureInfo.InvariantCulture), "table");
            sortBy.SetAttribute("order", OdfNamespaces.Table, rule.ascending ? "ascending" : "descending", "table");
            sortNode.AppendChild(sortBy);
        }
        Node.AppendChild(sortNode);
    }

    /// <summary>
    /// Sets filter conditions for this database range.
    /// 設定此資料庫範圍的篩選條件。
    /// </summary>
    /// <param name="conditions">The filter condition array, containing field numbers, operators, and values. / 篩選條件陣列，包含欄位編號、運算子與值。</param>
    public void SetFilter(params (int fieldNumber, string op, string value)[] conditions)
    {
        OdfNode? existingFilter = null;
        foreach (var child in Node.Children)
        {
            if (child.LocalName == "filter" && child.NamespaceUri == OdfNamespaces.Table)
            {
                existingFilter = child;
                break;
            }
        }
        if (existingFilter is not null)
        {
            Node.RemoveChild(existingFilter);
        }

        if (conditions is null || conditions.Length == 0)
            return;

        var filterNode = new OdfNode(OdfNodeType.Element, "filter", OdfNamespaces.Table, "table");
        foreach (var cond in conditions)
        {
            var filterCond = new OdfNode(OdfNodeType.Element, "filter-condition", OdfNamespaces.Table, "table");
            filterCond.SetAttribute("field-number", OdfNamespaces.Table, cond.fieldNumber.ToString(CultureInfo.InvariantCulture), "table");
            filterCond.SetAttribute("operator", OdfNamespaces.Table, cond.op, "table");
            filterCond.SetAttribute("value", OdfNamespaces.Table, cond.value, "table");
            filterNode.AppendChild(filterCond);
        }
        Node.AppendChild(filterNode);
    }
}
