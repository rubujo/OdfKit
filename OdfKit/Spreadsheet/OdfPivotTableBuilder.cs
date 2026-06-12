using System;
using System.Collections.Generic;
using OdfKit.DOM;
using OdfKit.Core;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 用於建構 ODF 樞紐分析表（Data Pilot Table）的產生器。
/// </summary>
/// <remarks>
/// 初始化 <see cref="OdfPivotTableBuilder"/> 類別的新執行個體。
/// </remarks>
/// <param name="name">樞紐分析表的名稱</param>
/// <param name="sourceRange">來源資料範圍</param>
/// <param name="targetStart">目標位置起點</param>
/// <param name="sheet">所屬的工作表</param>
public class OdfPivotTableBuilder(string name, OdfCellRange sourceRange, OdfCellAddress targetStart, OdfTableSheet sheet)
{
    private readonly string _name = name;
    private readonly OdfCellRange _sourceRange = sourceRange;
    private readonly OdfCellAddress _targetStart = targetStart;
    private readonly OdfTableSheet _sheet = sheet;
    private readonly List<(string name, string orientation, string? function)> _fields = [];

    /// <summary>
    /// 新增資料列欄位至樞紐分析表。
    /// </summary>
    /// <param name="fieldName">欄位名稱</param>
    /// <returns>目前執行個體，以支援鏈結呼叫</returns>
    public OdfPivotTableBuilder AddRowField(string fieldName)
    {
        _fields.Add((fieldName, "row", null));
        return this;
    }

    /// <summary>
    /// 新增資料欄欄位至樞紐分析表。
    /// </summary>
    /// <param name="fieldName">欄位名稱</param>
    /// <returns>目前執行個體，以支援鏈結呼叫</returns>
    public OdfPivotTableBuilder AddColumnField(string fieldName)
    {
        _fields.Add((fieldName, "column", null));
        return this;
    }

    /// <summary>
    /// 新增資料值欄位與對應的計算函式至樞紐分析表。
    /// </summary>
    /// <param name="fieldName">欄位名稱</param>
    /// <param name="function">使用的計算函式，預設為 "sum"</param>
    /// <returns>目前執行個體，以支援鏈結呼叫</returns>
    public OdfPivotTableBuilder AddDataField(string fieldName, string function = "sum")
    {
        _fields.Add((fieldName, "data", function));
        return this;
    }

    /// <summary>
    /// 新增頁面/篩選欄位至樞紐分析表。
    /// </summary>
    /// <param name="fieldName">欄位名稱</param>
    /// <returns>目前執行個體，以支援鏈結呼叫</returns>
    public OdfPivotTableBuilder AddPageField(string fieldName)
    {
        _fields.Add((fieldName, "page", null));
        return this;
    }

    /// <summary>
    /// 建置並將樞紐分析表套用至工作表中。
    /// </summary>
    /// <returns>代表建置後之樞紐分析表的 XML 節點</returns>
    public OdfNode Build()
    {
        OdfNode? tablesContainer = null;
        foreach (var child in _sheet.TableNode.Children)
        {
            if (child.LocalName == "data-pilot-tables" && child.NamespaceUri == OdfNamespaces.Table)
            {
                tablesContainer = child;
                break;
            }
        }
        if (tablesContainer is null)
        {
            tablesContainer = new OdfNode(OdfNodeType.Element, "data-pilot-tables", OdfNamespaces.Table, "table");
            _sheet.TableNode.AppendChild(tablesContainer);
        }

        var tableNode = new OdfNode(OdfNodeType.Element, "data-pilot-table", OdfNamespaces.Table, "table");
        tableNode.SetAttribute("name", OdfNamespaces.Table, _name, "table");
        tableNode.SetAttribute("target-range-address", OdfNamespaces.Table, _targetStart.ToOdfString(false), "table");
        tableNode.SetAttribute("buttons", OdfNamespaces.Table, _targetStart.ToOdfString(false), "table");

        var sourceRangeNode = new OdfNode(OdfNodeType.Element, "source-cell-range", OdfNamespaces.Table, "table");
        sourceRangeNode.SetAttribute("cell-range-address", OdfNamespaces.Table, _sourceRange.ToOdfString(false), "table");
        tableNode.AppendChild(sourceRangeNode);

        foreach (var field in _fields)
        {
            var fieldNode = new OdfNode(OdfNodeType.Element, "data-pilot-field", OdfNamespaces.Table, "table");
            fieldNode.SetAttribute("source-field-name", OdfNamespaces.Table, field.name, "table");
            fieldNode.SetAttribute("orientation", OdfNamespaces.Table, field.orientation, "table");
            if (field.orientation == "data" && !string.IsNullOrEmpty(field.function))
            {
                fieldNode.SetAttribute("function", OdfNamespaces.Table, field.function!, "table");
            }
            tableNode.AppendChild(fieldNode);
        }

        tablesContainer.AppendChild(tableNode);
        return tableNode;
    }
}
