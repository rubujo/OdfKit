using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Specifies pivot table value aggregate functions.
/// 樞紐分析表值彙總函式。
/// </summary>
public enum OdfPivotFunction
{
    /// <summary>
    /// Sum.
    /// 加總。
    /// </summary>
    Sum,

    /// <summary>
    /// Count.
    /// 計數。
    /// </summary>
    Count,

    /// <summary>
    /// Average.
    /// 平均值。
    /// </summary>
    Average,

    /// <summary>
    /// Maximum.
    /// 最大值。
    /// </summary>
    Max,

    /// <summary>
    /// Minimum.
    /// 最小值。
    /// </summary>
    Min,

    /// <summary>
    /// Calculated formula, used with <see cref="OdfPivotTableBuilder.AddCalculatedField"/>.
    /// 計算公式，搭配 <see cref="OdfPivotTableBuilder.AddCalculatedField"/> 使用。
    /// </summary>
    Formula,
}

/// <summary>
/// Specifies pivot table filter condition operators.
/// 樞紐分析表篩選條件運算子。
/// </summary>
public enum OdfPivotFilterOperator
{
    /// <summary>
    /// Equal to.
    /// 等於。
    /// </summary>
    Equal,

    /// <summary>
    /// Not equal to.
    /// 不等於。
    /// </summary>
    NotEqual,

    /// <summary>
    /// Greater than.
    /// 大於。
    /// </summary>
    GreaterThan,

    /// <summary>
    /// Greater than or equal to.
    /// 大於或等於。
    /// </summary>
    GreaterThanOrEqual,

    /// <summary>
    /// Less than.
    /// 小於。
    /// </summary>
    LessThan,

    /// <summary>
    /// Less than or equal to.
    /// 小於或等於。
    /// </summary>
    LessThanOrEqual,
}

/// <summary>
/// Builds ODF pivot tables, also known as data pilot tables.
/// 用於建構 ODF 樞紐分析表（Data Pilot Table）的產生器。
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="OdfPivotTableBuilder"/> class.
/// 初始化 <see cref="OdfPivotTableBuilder"/> 類別的新執行個體。
/// </remarks>
/// <param name="name">The pivot table name. / 樞紐分析表的名稱。</param>
/// <param name="sourceRange">The source data range. / 來源資料範圍。</param>
/// <param name="targetStart">The target position start. / 目標位置起點。</param>
/// <param name="sheet">The owning worksheet. / 所屬的工作表。</param>
public class OdfPivotTableBuilder(string name, OdfCellRange sourceRange, OdfCellAddress targetStart, OdfTableSheet sheet)
{
    private readonly string _name = name;
    private readonly OdfCellRange _sourceRange = sourceRange;
    private readonly OdfCellAddress _targetStart = targetStart;
    private readonly OdfTableSheet _sheet = sheet;
    private readonly List<(string name, string orientation, string? function, string? formula)> _fields = [];
    private readonly List<(string fieldName, bool ascending)> _sortInfos = [];
    private readonly List<(string fieldName, OdfPivotFilterOperator op, string value)> _filters = [];
    private bool _hasColumnHeaders = true;
    private bool _hasRowHeaders = true;

    /// <summary>
    /// Sets whether the source data contains a column header row. The default is <see langword="true"/>.
    /// 設定來源資料是否包含欄標題列（預設為 <see langword="true"/>）。
    /// </summary>
    public OdfPivotTableBuilder WithColumnHeaders(bool hasHeaders = true)
    {
        _hasColumnHeaders = hasHeaders;
        return this;
    }

    /// <summary>
    /// Sets whether the source data contains a row header column. The default is <see langword="true"/>.
    /// 設定來源資料是否包含列標題欄（預設為 <see langword="true"/>）。
    /// </summary>
    public OdfPivotTableBuilder WithRowHeaders(bool hasHeaders = true)
    {
        _hasRowHeaders = hasHeaders;
        return this;
    }

    /// <summary>
    /// Adds a row field to the pivot table.
    /// 新增資料列欄位至樞紐分析表。
    /// </summary>
    /// <param name="fieldName">The field name. / 欄位名稱。</param>
    /// <returns>The current instance for chaining. / 目前執行個體，以支援鏈結呼叫。</returns>
    public OdfPivotTableBuilder AddRowField(string fieldName)
    {
        _fields.Add((fieldName, "row", null, null));
        return this;
    }

    /// <summary>
    /// Adds a column field to the pivot table.
    /// 新增資料欄欄位至樞紐分析表。
    /// </summary>
    /// <param name="fieldName">The field name. / 欄位名稱。</param>
    /// <returns>The current instance for chaining. / 目前執行個體，以支援鏈結呼叫。</returns>
    public OdfPivotTableBuilder AddColumnField(string fieldName)
    {
        _fields.Add((fieldName, "column", null, null));
        return this;
    }

    /// <summary>
    /// Adds a data value field and its calculation function to the pivot table.
    /// 新增資料值欄位與對應的計算函式至樞紐分析表。
    /// </summary>
    /// <param name="fieldName">The field name. / 欄位名稱。</param>
    /// <param name="function">The calculation function to use; the default is <see cref="OdfPivotFunction.Sum"/>. / 使用的計算函式，預設為 <see cref="OdfPivotFunction.Sum"/>。</param>
    /// <returns>The current instance for chaining. / 目前執行個體，以支援鏈結呼叫。</returns>
    public OdfPivotTableBuilder AddDataField(string fieldName, OdfPivotFunction function = OdfPivotFunction.Sum)
    {
        _fields.Add((fieldName, "data", FunctionToString(function), null));
        return this;
    }

    /// <summary>
    /// Adds a data value field using a raw function name string for legacy API compatibility.
    /// 新增資料值欄位，使用原始函式名稱字串（相容舊版 API）。
    /// </summary>
    /// <param name="fieldName">The field name. / 欄位名稱。</param>
    /// <param name="function">The ODF function name string, such as <c>sum</c> or <c>count</c>. / ODF 函式名稱字串，例如 <c>sum</c>、<c>count</c>。</param>
    /// <returns>The current instance for chaining. / 目前執行個體，以支援鏈結呼叫。</returns>
    public OdfPivotTableBuilder AddDataField(string fieldName, string function)
    {
        _fields.Add((fieldName, "data", function, null));
        return this;
    }

    /// <summary>
    /// Adds a page or filter field to the pivot table.
    /// 新增頁面/篩選欄位至樞紐分析表。
    /// </summary>
    /// <param name="fieldName">The field name. / 欄位名稱。</param>
    /// <returns>The current instance for chaining. / 目前執行個體，以支援鏈結呼叫。</returns>
    public OdfPivotTableBuilder AddPageField(string fieldName)
    {
        _fields.Add((fieldName, "page", null, null));
        return this;
    }

    /// <summary>
    /// Adds a calculated field using a formula to the pivot table.
    /// 新增計算欄位（使用公式）至樞紐分析表。
    /// </summary>
    /// <param name="fieldName">The calculated field name. / 計算欄位名稱。</param>
    /// <param name="formula">The ODF formula, such as <c>of:[.Sales]/[.Count]</c>. / ODF 公式，例如 <c>of:[.Sales]/[.Count]</c>。</param>
    /// <returns>The current instance for chaining. / 目前執行個體，以支援鏈結呼叫。</returns>
    public OdfPivotTableBuilder AddCalculatedField(string fieldName, string formula)
    {
        _fields.Add((fieldName, "data", "formula", formula));
        return this;
    }

    /// <summary>
    /// Sets the sort direction for the specified field.
    /// 為指定欄位設定排序方向。
    /// </summary>
    /// <param name="fieldName">The sort field name. / 排序欄位名稱。</param>
    /// <param name="ascending">Whether sorting is ascending; the default is <see langword="true"/>. / 是否升冪排序，預設為 <see langword="true"/>。</param>
    /// <returns>The current instance for chaining. / 目前執行個體，以支援鏈結呼叫。</returns>
    public OdfPivotTableBuilder AddSortInfo(string fieldName, bool ascending = true)
    {
        _sortInfos.Add((fieldName, ascending));
        return this;
    }

    /// <summary>
    /// Adds a field filter condition to the pivot table.
    /// 新增欄位篩選條件至樞紐分析表。
    /// </summary>
    /// <param name="fieldName">The filter field name. / 篩選欄位名稱。</param>
    /// <param name="op">The comparison operator. / 比較運算子。</param>
    /// <param name="value">The filter value string. / 篩選值字串。</param>
    /// <returns>The current instance for chaining. / 目前執行個體，以支援鏈結呼叫。</returns>
    public OdfPivotTableBuilder AddFilter(string fieldName, OdfPivotFilterOperator op, string value)
    {
        _filters.Add((fieldName, op, value));
        return this;
    }

    /// <summary>
    /// Builds and applies the pivot table to the worksheet.
    /// 建置並將樞紐分析表套用至工作表中。
    /// </summary>
    /// <returns>The XML node that represents the built pivot table. / 代表建置後之樞紐分析表的 XML 節點。</returns>
    public OdfNode Build()
    {
        // 依 ODF 1.4 schema（table-functions／office-spreadsheet-content-epilogue），
        // table:data-pilot-tables 必須是 office:spreadsheet 的直接子節點（與所有 table:table
        // 同層、置於其後），而非個別 table:table 的子節點；否則 LibreOffice 等應用程式重新儲存
        // 文件時會視為結構不符規格而捨棄整段樞紐分析表定義。
        OdfNode spreadsheetRoot = _sheet.Document.SheetsRoot;
        OdfNode? tablesContainer = null;
        foreach (var child in spreadsheetRoot.Children)
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
            spreadsheetRoot.AppendChild(tablesContainer);
        }

        // 依 ODF 1.4 schema，table:target-range-address 與 table:buttons 屬性型別皆為
        // cellRangeAddress／cellRangeAddressList（範圍），並非單一儲存格位址；雖然樞紐分析表的
        // 目標起點在語意上是單一格，仍必須寫成起點與終點相同的範圍字串（例如 "Sheet1.A6:.A6"），
        // 否則嚴格遵循 schema 的應用程式（如 LibreOffice 重新儲存時）會將其正規化為範圍格式，
        // 導致前後格式不一致。
        var targetRange = new OdfCellRange(_targetStart, _targetStart);
        var tableNode = new OdfNode(OdfNodeType.Element, "data-pilot-table", OdfNamespaces.Table, "table");
        tableNode.SetAttribute("name", OdfNamespaces.Table, _name, "table");
        tableNode.SetAttribute("target-range-address", OdfNamespaces.Table, targetRange.ToOdfString(false), "table");
        tableNode.SetAttribute("buttons", OdfNamespaces.Table, targetRange.ToOdfString(false), "table");
        tableNode.SetAttribute("has-column-headers", OdfNamespaces.Table, _hasColumnHeaders ? "true" : "false", "table");
        tableNode.SetAttribute("has-row-headers", OdfNamespaces.Table, _hasRowHeaders ? "true" : "false", "table");

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
                if (field.function == "formula" && !string.IsNullOrEmpty(field.formula))
                {
                    fieldNode.SetAttribute("formula", OdfNamespaces.Table, field.formula!, "table");
                }
            }
            tableNode.AppendChild(fieldNode);
        }

        if (_sortInfos.Count > 0)
        {
            var sortNode = new OdfNode(OdfNodeType.Element, "sort-info", OdfNamespaces.Table, "table");
            foreach (var (fieldName, ascending) in _sortInfos)
            {
                var sortField = new OdfNode(OdfNodeType.Element, "sort-field", OdfNamespaces.Table, "table");
                sortField.SetAttribute("source-field-name", OdfNamespaces.Table, fieldName, "table");
                sortField.SetAttribute("order", OdfNamespaces.Table, ascending ? "ascending" : "descending", "table");
                sortNode.AppendChild(sortField);
            }
            tableNode.AppendChild(sortNode);
        }

        if (_filters.Count > 0)
        {
            var filterNode = new OdfNode(OdfNodeType.Element, "filter", OdfNamespaces.Table, "table");
            foreach (var (fieldName, op, value) in _filters)
            {
                var condNode = new OdfNode(OdfNodeType.Element, "filter-condition", OdfNamespaces.Table, "table");
                condNode.SetAttribute("source-field-name", OdfNamespaces.Table, fieldName, "table");
                condNode.SetAttribute("operator", OdfNamespaces.Table, OperatorToString(op), "table");
                condNode.SetAttribute("value", OdfNamespaces.Table, value, "table");
                filterNode.AppendChild(condNode);
            }
            tableNode.AppendChild(filterNode);
        }

        tablesContainer.AppendChild(tableNode);
        return tableNode;
    }

    private static string FunctionToString(OdfPivotFunction function) => function switch
    {
        OdfPivotFunction.Sum => "sum",
        OdfPivotFunction.Count => "count",
        OdfPivotFunction.Average => "average",
        OdfPivotFunction.Max => "max",
        OdfPivotFunction.Min => "min",
        OdfPivotFunction.Formula => "formula",
        _ => "sum",
    };

    private static string OperatorToString(OdfPivotFilterOperator op) => op switch
    {
        OdfPivotFilterOperator.Equal => "=",
        OdfPivotFilterOperator.NotEqual => "!=",
        OdfPivotFilterOperator.GreaterThan => ">",
        OdfPivotFilterOperator.GreaterThanOrEqual => ">=",
        OdfPivotFilterOperator.LessThan => "<",
        OdfPivotFilterOperator.LessThanOrEqual => "<=",
        _ => "=",
    };
}
