using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
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
/// Configures bounded pivot result materialization.
/// 設定具資源上限的樞紐結果物化。
/// </summary>
public sealed class OdfPivotRefreshOptions
{
    /// <summary>
    /// Gets or sets the maximum source cells inspected.
    /// 取得或設定最多檢查的來源儲存格數。
    /// </summary>
    public int MaximumSourceCells { get; set; } = 5_000_000;

    /// <summary>
    /// Gets or sets the maximum aggregate groups retained in memory.
    /// 取得或設定記憶體中保留的彙總群組數上限。
    /// </summary>
    public int MaximumGroups { get; set; } = 250_000;

    /// <summary>
    /// Gets or sets the maximum output cells written.
    /// 取得或設定最多寫入的輸出儲存格數。
    /// </summary>
    public int MaximumOutputCells { get; set; } = 1_000_000;

    /// <summary>
    /// Gets or sets the maximum total pivot fields inspected.
    /// 取得或設定最多可檢查的樞紐欄位總數。
    /// </summary>
    public int MaximumFields { get; set; } = 4_096;

    /// <summary>
    /// Gets or sets the maximum data fields aggregated.
    /// 取得或設定最多可彙總的資料欄位數。
    /// </summary>
    public int MaximumDataFields { get; set; } = 1_024;

    /// <summary>
    /// Gets or sets the maximum aggregate accumulators retained in memory.
    /// 取得或設定記憶體中最多可保留的彙總累加器數。
    /// </summary>
    public int MaximumAggregateSlots { get; set; } = 1_000_000;

    /// <summary>
    /// Gets or sets the maximum calculated fields compiled for one refresh.
    /// 取得或設定單次刷新最多可編譯的計算欄位數。
    /// </summary>
    public int MaximumCalculatedFields { get; set; } = 128;

    /// <summary>
    /// Gets or sets the maximum characters in one calculated-field formula.
    /// 取得或設定單一計算欄位公式的最大字元數。
    /// </summary>
    public int MaximumFormulaCharacters { get; set; } = 16_384;

    /// <summary>
    /// Gets or sets the maximum syntax nodes in one calculated-field formula.
    /// 取得或設定單一計算欄位公式的最大語法節點數。
    /// </summary>
    public int MaximumFormulaNodes { get; set; } = 512;

    /// <summary>
    /// Gets or sets the maximum nesting depth in one calculated-field formula.
    /// 取得或設定單一計算欄位公式的最大巢狀深度。
    /// </summary>
    public int MaximumFormulaDepth { get; set; } = 64;

    /// <summary>
    /// Gets or sets the maximum calculated-field evaluations performed.
    /// 取得或設定最多可執行的計算欄位求值次數。
    /// </summary>
    public long MaximumFormulaEvaluations { get; set; } = 10_000_000;
}

/// <summary>
/// Reports a completed pivot materialization.
/// 回報已完成的樞紐結果物化。
/// </summary>
public readonly struct OdfPivotRefreshResult(int sourceRows, int groupCount, int outputCells)
{
    /// <summary>
    /// Gets the number of source data rows read.
    /// 取得讀取的來源資料列數。
    /// </summary>
    public int SourceRows { get; } = sourceRows;

    /// <summary>
    /// Gets the number of aggregate groups.
    /// 取得彙總群組數。
    /// </summary>
    public int GroupCount { get; } = groupCount;

    /// <summary>
    /// Gets the number of output cells written.
    /// 取得寫入的輸出儲存格數。
    /// </summary>
    public int OutputCells { get; } = outputCells;
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
    private readonly Dictionary<string, OdfPivotGroupingOptions> _groupings = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, OdfPivotValueOptions> _valueOptions = new(StringComparer.OrdinalIgnoreCase);
    private OdfPivotGrandTotal _grandTotals;
    private OdfPivotLayout _layout = OdfPivotLayout.Tabular;
    private OdfPivotOutputStyleOptions? _outputStyles;
    private bool _showFilterButton = true;
    private bool _drillDown;
    /// <summary>
    /// Short overload of WithColumnHeaders that uses default values for all optional parameters and forwards to the full overload.
    /// 便利多載：WithColumnHeaders 的所有可選參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public OdfPivotTableBuilder WithColumnHeaders() => WithColumnHeaders(true);


    /// <summary>
    /// Keeps source column header intent for fluent API compatibility without emitting nonstandard attributes.
    /// 保留來源欄標題意圖以相容 Fluent API，但不輸出非標準屬性。
    /// </summary>
    public OdfPivotTableBuilder WithColumnHeaders(bool hasHeaders)
    {
        return this;
    }

    /// <summary>
    /// Short overload of WithRowHeaders that uses default values for all optional parameters and forwards to the full overload.
    /// 便利多載：WithRowHeaders 的所有可選參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public OdfPivotTableBuilder WithRowHeaders() => WithRowHeaders(true);


    /// <summary>
    /// Keeps source row header intent for fluent API compatibility without emitting nonstandard attributes.
    /// 保留來源列標題意圖以相容 Fluent API，但不輸出非標準屬性。
    /// </summary>
    public OdfPivotTableBuilder WithRowHeaders(bool hasHeaders)
    {
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
    /// <returns>The current instance for chaining. / 目前執行個體，以支援鏈結呼叫。</returns>
    public OdfPivotTableBuilder AddDataField(string fieldName) => AddDataField(fieldName, OdfPivotFunction.Sum);

    /// <summary>
    /// Full overload of AddDataField that accepts fieldName and function.
    /// AddDataField 完整多載：接受 fieldName 與 function。
    /// </summary>
    public OdfPivotTableBuilder AddDataField(string fieldName, OdfPivotFunction function)
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
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(fieldName, nameof(fieldName));
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(formula, nameof(formula));
        if (string.IsNullOrWhiteSpace(fieldName) || formula.Length > 16_384)
            throw new ArgumentOutOfRangeException(nameof(formula));
        OdfPivotCalculatedFormula.ValidateSyntax(formula, 512, 64);
        _fields.Add((fieldName, "data", "formula", formula));
        return this;
    }
    /// <summary>
    /// Short overload of AddSortInfo that accepts fieldName; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 fieldName；其餘可選參數使用預設值並轉呼叫最長 AddSortInfo 多載。
    /// </summary>
    public OdfPivotTableBuilder AddSortInfo(string fieldName) => AddSortInfo(fieldName, true);


    /// <summary>
    /// Sets the sort direction for the specified field.
    /// 為指定欄位設定排序方向。
    /// </summary>
    /// <param name="fieldName">The sort field name. / 排序欄位名稱。</param>
    /// <param name="ascending">Whether sorting is ascending; the default is <see langword="true"/>. / 是否升冪排序，預設為 <see langword="true"/>。</param>
    /// <returns>The current instance for chaining. / 目前執行個體，以支援鏈結呼叫。</returns>
    public OdfPivotTableBuilder AddSortInfo(string fieldName, bool ascending)
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
    /// Configures grand totals for materialization and ODF persistence.
    /// 設定物化結果與 ODF 持久化的總計。
    /// </summary>
    /// <param name="grandTotals">The axes that receive grand totals. / 要顯示總計的軸。</param>
    /// <returns>The current instance for chaining. / 目前執行個體，以支援鏈結呼叫。</returns>
    public OdfPivotTableBuilder WithGrandTotals(OdfPivotGrandTotal grandTotals)
    {
        if (!IsDefined(grandTotals))
            throw new ArgumentOutOfRangeException(nameof(grandTotals));
        _grandTotals = grandTotals;
        return this;
    }

    /// <summary>
    /// Configures the ODF field layout.
    /// 設定 ODF 欄位版面。
    /// </summary>
    /// <param name="layout">The layout mode. / 版面模式。</param>
    /// <returns>The current instance for chaining. / 目前執行個體，以支援鏈結呼叫。</returns>
    public OdfPivotTableBuilder WithLayout(OdfPivotLayout layout)
    {
        if (!IsDefined(layout))
            throw new ArgumentOutOfRangeException(nameof(layout));
        _layout = layout;
        return this;
    }

    /// <summary>
    /// Configures the ODF filter button.
    /// 設定 ODF 篩選按鈕。
    /// </summary>
    /// <param name="show">Whether the filter button is shown. / 是否顯示篩選按鈕。</param>
    /// <returns>The current instance for chaining. / 目前執行個體，以支援鏈結呼叫。</returns>
    public OdfPivotTableBuilder WithFilterButton(bool show)
    {
        _showFilterButton = show;
        return this;
    }

    /// <summary>
    /// Configures the consumer drill-down hint.
    /// 設定閱讀器的向下鑽研提示。
    /// </summary>
    /// <param name="enabled">Whether drill-down is enabled. / 是否啟用向下鑽研。</param>
    /// <returns>The current instance for chaining. / 目前執行個體，以支援鏈結呼叫。</returns>
    public OdfPivotTableBuilder WithDrillDown(bool enabled)
    {
        _drillDown = enabled;
        return this;
    }

    /// <summary>
    /// Configures bounded grouping for a row or column field.
    /// 設定資料列或資料欄欄位的有界分組。
    /// </summary>
    /// <param name="fieldName">The source field name. / 來源欄位名稱。</param>
    /// <param name="options">The grouping options. / 分組選項。</param>
    /// <returns>The current instance for chaining. / 目前執行個體，以支援鏈結呼叫。</returns>
    public OdfPivotTableBuilder GroupField(string fieldName, OdfPivotGroupingOptions options)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(fieldName, nameof(fieldName));
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(options, nameof(options));
        ValidateGrouping(options);
        _groupings[fieldName] = CopyGrouping(options);
        return this;
    }

    /// <summary>
    /// Configures a derived display calculation for a data field.
    /// 設定資料欄位的衍生顯示計算。
    /// </summary>
    /// <param name="fieldName">The data field name. / 資料欄位名稱。</param>
    /// <param name="options">The value options. / 值選項。</param>
    /// <returns>The current instance for chaining. / 目前執行個體，以支援鏈結呼叫。</returns>
    public OdfPivotTableBuilder ConfigureValueField(string fieldName, OdfPivotValueOptions options)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(fieldName, nameof(fieldName));
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(options, nameof(options));
        ValidateValueOptions(options);
        _valueOptions[fieldName] = new OdfPivotValueOptions
        {
            ShowValuesAs = options.ShowValuesAs,
            BaseFieldName = options.BaseFieldName,
            BaseMemberName = options.BaseMemberName,
        };
        return this;
    }

    /// <summary>
    /// Configures existing cell styles used by materialized output.
    /// 設定物化輸出使用的既有儲存格樣式。
    /// </summary>
    /// <param name="options">The output style options. / 輸出樣式選項。</param>
    /// <returns>The current instance for chaining. / 目前執行個體，以支援鏈結呼叫。</returns>
    public OdfPivotTableBuilder WithOutputStyles(OdfPivotOutputStyleOptions options)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(options, nameof(options));
        _outputStyles = new OdfPivotOutputStyleOptions
        {
            HeaderStyleName = options.HeaderStyleName,
            DataStyleName = options.DataStyleName,
            GrandTotalStyleName = options.GrandTotalStyleName,
        };
        return this;
    }

    /// <summary>
    /// Computes and materializes the current pivot result at the configured target.
    /// 計算目前樞紐結果並物化至設定的目標位置。
    /// </summary>
    /// <param name="options">The resource limits, or null for defaults. / 資源限制；null 表示使用預設值。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消權杖。</param>
    /// <returns>The refresh report. / 刷新報告。</returns>
    public OdfPivotRefreshResult Refresh(
        OdfPivotRefreshOptions? options,
        CancellationToken cancellationToken)
    {
        options ??= new OdfPivotRefreshOptions();
        if (options.MaximumSourceCells < 1 ||
            options.MaximumGroups < 1 ||
            options.MaximumOutputCells < 1 ||
            options.MaximumFields < 1 ||
            options.MaximumDataFields < 1 ||
            options.MaximumAggregateSlots < 1 ||
            options.MaximumCalculatedFields < 1 ||
            options.MaximumFormulaCharacters < 1 ||
            options.MaximumFormulaNodes < 1 ||
            options.MaximumFormulaDepth < 1 ||
            options.MaximumFormulaEvaluations < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }
        if (_fields.Count > options.MaximumFields)
            throw new InvalidOperationException();
        ValidateOutputStyles();

        int firstRow = Math.Min(_sourceRange.StartAddress.Row, _sourceRange.EndAddress.Row);
        int lastRow = Math.Max(_sourceRange.StartAddress.Row, _sourceRange.EndAddress.Row);
        int firstColumn = Math.Min(_sourceRange.StartAddress.Column, _sourceRange.EndAddress.Column);
        int lastColumn = Math.Max(_sourceRange.StartAddress.Column, _sourceRange.EndAddress.Column);
        OdfTableSheet sourceSheet = string.IsNullOrEmpty(_sourceRange.StartAddress.SheetName)
            ? _sheet
            : _sheet.Document.Worksheets[_sourceRange.StartAddress.SheetName!];
        int columnCount = checked(lastColumn - firstColumn + 1);
        long sourceCells = checked((long)(lastRow - firstRow + 1) * columnCount);
        if (sourceCells > options.MaximumSourceCells)
            throw new InvalidOperationException();

        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int column = 0; column < columnCount; column++)
        {
            string header = sourceSheet.Cells[firstRow, firstColumn + column].FormattedValue;
            if (string.IsNullOrWhiteSpace(header) || headers.ContainsKey(header))
                throw new InvalidDataException();
            headers.Add(header, column);
        }
        var rowFields = _fields.Where(field => field.orientation == "row").ToList();
        var columnFields = _fields.Where(field => field.orientation == "column").ToList();
        var dataFields = _fields.Where(field => field.orientation == "data").ToList();
        ValidateAdvancedFields(rowFields, columnFields, dataFields);
        if (dataFields.Count == 0)
            throw new NotSupportedException();
        if (dataFields.Count > options.MaximumDataFields)
            throw new InvalidOperationException();
        int[] rowIndexes = ResolveIndexes(rowFields, headers);
        int[] columnIndexes = ResolveIndexes(columnFields, headers);
        List<(string name, string orientation, string? function, string? formula)> calculatedFields =
            dataFields.Where(field => field.function == "formula").ToList();
        if (calculatedFields.Count > options.MaximumCalculatedFields)
            throw new InvalidOperationException();
        long formulaEvaluations = checked((long)(lastRow - firstRow) * calculatedFields.Count);
        if (formulaEvaluations > options.MaximumFormulaEvaluations)
            throw new InvalidOperationException();
        Dictionary<string, int> valueSlots = new(headers, StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < calculatedFields.Count; index++)
        {
            if (valueSlots.ContainsKey(calculatedFields[index].name))
                throw new InvalidDataException();
            valueSlots.Add(calculatedFields[index].name, columnCount + index);
        }
        CompiledCalculatedField[] compiledCalculatedFields = CompileCalculatedFields(
            calculatedFields,
            valueSlots,
            columnCount,
            options);
        int[] calculatedOrder = BuildCalculatedEvaluationOrder(
            compiledCalculatedFields,
            columnCount);
        int[] dataIndexes = ResolveDataIndexes(dataFields, valueSlots);
        var groups = new Dictionary<string, PivotAggregate[]>(StringComparer.Ordinal);
        var rowLabels = new Dictionary<string, string>(StringComparer.Ordinal);
        var columnLabels = new Dictionary<string, string>(StringComparer.Ordinal);
        int rowsRead = 0;
        var values = new object?[columnCount + calculatedFields.Count];
        for (int row = firstRow + 1; row <= lastRow; row++)
        {
            if ((rowsRead++ & 0xff) == 0)
                cancellationToken.ThrowIfCancellationRequested();
            for (int column = 0; column < columnCount; column++)
                values[column] = sourceSheet.Cells[row, firstColumn + column].CellValue;
            if (!MatchesFilters(values, headers))
                continue;
            foreach (int calculatedIndex in calculatedOrder)
            {
                CompiledCalculatedField calculated = compiledCalculatedFields[calculatedIndex];
                values[calculated.Slot] = calculated.Formula.Evaluate(values);
            }
            string[] rowMembers = BuildAxisMembers(values, rowIndexes, rowFields);
            string[] columnMembers = BuildAxisMembers(values, columnIndexes, columnFields);
            string rowKey = BuildEncodedKey(rowMembers);
            string columnKey = BuildEncodedKey(columnMembers);
            string groupKey = BuildGroupKey(rowKey, columnKey);
            if (!groups.TryGetValue(groupKey, out PivotAggregate[]? aggregates))
            {
                if (groups.Count >= options.MaximumGroups ||
                    checked((long)(groups.Count + 1) * dataFields.Count) >
                        options.MaximumAggregateSlots)
                {
                    throw new InvalidOperationException();
                }
                aggregates = new PivotAggregate[dataFields.Count];
                for (int index = 0; index < aggregates.Length; index++)
                    aggregates[index] = new PivotAggregate();
                groups.Add(groupKey, aggregates);
                rowLabels[rowKey] = string.Join(" / ", rowMembers);
                columnLabels[columnKey] = string.Join(" / ", columnMembers);
            }
            for (int index = 0; index < dataFields.Count; index++)
                aggregates[index].Add(values[dataIndexes[index]]);
        }

        KeyValuePair<string, string>[] columns = columnLabels.Count == 0
            ? [new KeyValuePair<string, string>(string.Empty, string.Empty)]
            : columnLabels.OrderBy(pair => pair.Value, StringComparer.Ordinal).ThenBy(pair => pair.Key, StringComparer.Ordinal).ToArray();
        KeyValuePair<string, string>[] rows = rowLabels.Count == 0
            ? [new KeyValuePair<string, string>(string.Empty, string.Empty)]
            : rowLabels.OrderBy(pair => pair.Value, StringComparer.Ordinal).ThenBy(pair => pair.Key, StringComparer.Ordinal).ToArray();
        Dictionary<string, (bool IsRow, string Key)> relativeBases = ResolveRelativeBases(
            dataFields,
            rowFields,
            columnFields,
            rows,
            columns);
        bool includeRowGrandTotal = _grandTotals is OdfPivotGrandTotal.Row or OdfPivotGrandTotal.Both;
        bool includeColumnGrandTotal = _grandTotals is OdfPivotGrandTotal.Column or OdfPivotGrandTotal.Both;
        int valueColumnGroups = checked(columns.Length + (includeRowGrandTotal ? 1 : 0));
        long valuesPerRow = checked(1L + (long)valueColumnGroups * dataFields.Count);
        long projectedOutputCells = checked(
            valuesPerRow +
            (long)rows.Length * valuesPerRow +
            (includeColumnGrandTotal ? valuesPerRow : 0));
        if (projectedOutputCells > options.MaximumOutputCells ||
            projectedOutputCells > int.MaxValue)
        {
            throw new InvalidOperationException();
        }
        long totalAggregateSlots = checked(
            ((long)rows.Length + columns.Length + 1) * dataFields.Count);
        if (totalAggregateSlots > options.MaximumAggregateSlots)
            throw new InvalidOperationException();
        Dictionary<string, PivotAggregate[]> rowTotals = CreateAxisTotals(
            rows.Select(row => row.Key),
            dataFields.Count);
        Dictionary<string, PivotAggregate[]> columnTotals = CreateAxisTotals(
            columns.Select(column => column.Key),
            dataFields.Count);
        PivotAggregate[] grandTotals = CreateAggregates(dataFields.Count);
        foreach (KeyValuePair<string, string> row in rows)
        {
            foreach (KeyValuePair<string, string> column in columns)
            {
                if (!groups.TryGetValue(BuildGroupKey(row.Key, column.Key), out PivotAggregate[]? aggregates))
                    continue;
                for (int index = 0; index < dataFields.Count; index++)
                {
                    rowTotals[row.Key][index].Merge(aggregates[index]);
                    columnTotals[column.Key][index].Merge(aggregates[index]);
                    grandTotals[index].Merge(aggregates[index]);
                }
            }
        }

        var output = new List<(int Row, int Column, object? Value, PivotOutputKind Kind)>((int)projectedOutputCells);
        output.Add((_targetStart.Row, _targetStart.Column, string.Join(" / ", rowFields.Select(field => field.name)), PivotOutputKind.Header));
        int outputColumn = _targetStart.Column + 1;
        foreach (KeyValuePair<string, string> column in columns)
        {
            foreach (var dataField in dataFields)
            {
                output.Add((_targetStart.Row, outputColumn++, string.IsNullOrEmpty(column.Value)
                    ? dataField.name
                    : column.Value + " / " + dataField.name,
                    PivotOutputKind.Header));
            }
        }
        if (includeRowGrandTotal)
        {
            foreach (var dataField in dataFields)
                output.Add((_targetStart.Row, outputColumn++, "Grand Total / " + dataField.name, PivotOutputKind.GrandTotal));
        }
        int outputRow = _targetStart.Row + 1;
        var runningTotals = new double[columns.Length, dataFields.Count];
        foreach (KeyValuePair<string, string> rowLabel in rows)
        {
            output.Add((outputRow, _targetStart.Column, rowLabel.Value, PivotOutputKind.Header));
            outputColumn = _targetStart.Column + 1;
            for (int columnIndex = 0; columnIndex < columns.Length; columnIndex++)
            {
                KeyValuePair<string, string> columnLabel = columns[columnIndex];
                groups.TryGetValue(BuildGroupKey(rowLabel.Key, columnLabel.Key), out PivotAggregate[]? aggregates);
                for (int index = 0; index < dataFields.Count; index++)
                {
                    object? rawValue = aggregates is null
                        ? null
                        : aggregates[index].GetValue(dataFields[index].function);
                    object? value = TransformValue(
                        rawValue,
                        rowTotals[rowLabel.Key][index].GetValue(dataFields[index].function),
                        columnTotals[columnLabel.Key][index].GetValue(dataFields[index].function),
                        grandTotals[index].GetValue(dataFields[index].function),
                        dataFields[index].name,
                        rowLabel,
                        columnLabel,
                        relativeBases,
                        groups,
                        dataFields[index].function,
                        index,
                        runningTotals,
                        columnIndex);
                    output.Add((outputRow, outputColumn++, value, PivotOutputKind.Data));
                }
            }
            if (includeRowGrandTotal)
            {
                for (int index = 0; index < dataFields.Count; index++)
                {
                    output.Add((
                        outputRow,
                        outputColumn++,
                        rowTotals[rowLabel.Key][index].GetValue(dataFields[index].function),
                        PivotOutputKind.GrandTotal));
                }
            }
            outputRow++;
        }
        if (includeColumnGrandTotal)
        {
            output.Add((outputRow, _targetStart.Column, "Grand Total", PivotOutputKind.GrandTotal));
            outputColumn = _targetStart.Column + 1;
            foreach (KeyValuePair<string, string> columnLabel in columns)
            {
                for (int index = 0; index < dataFields.Count; index++)
                {
                    output.Add((
                        outputRow,
                        outputColumn++,
                        columnTotals[columnLabel.Key][index].GetValue(dataFields[index].function),
                        PivotOutputKind.GrandTotal));
                }
            }
            if (includeRowGrandTotal)
            {
                for (int index = 0; index < dataFields.Count; index++)
                {
                    output.Add((
                        outputRow,
                        outputColumn++,
                        grandTotals[index].GetValue(dataFields[index].function),
                        PivotOutputKind.GrandTotal));
                }
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        foreach ((int row, int column, object? value, PivotOutputKind kind) in output)
        {
            OdfCell cell = _sheet.Cells[row, column];
            cell.CellValue = value;
            cell.StyleName = ResolveOutputStyle(kind);
        }
        return new OdfPivotRefreshResult(rowsRead, groups.Count, output.Count);
    }

    /// <summary>
    /// Computes and materializes the current pivot result with default limits.
    /// 以預設限制計算並物化目前的樞紐結果。
    /// </summary>
    /// <returns>The refresh report. / 刷新報告。</returns>
    public OdfPivotRefreshResult Refresh() => Refresh(null, default);

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
        tableNode.SetAttribute("grand-total", OdfNamespaces.Table, GrandTotalToString(_grandTotals), "table");
        tableNode.SetAttribute("show-filter-button", OdfNamespaces.Table, _showFilterButton ? "true" : "false", "table");
        tableNode.SetAttribute("drill-down-on-double-click", OdfNamespaces.Table, _drillDown ? "true" : "false", "table");

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
            if (field.orientation is "row" or "column")
            {
                var levelNode = new OdfNode(OdfNodeType.Element, "data-pilot-level", OdfNamespaces.Table, "table");
                levelNode.SetAttribute("show-empty", OdfNamespaces.Table, "false", "table");
                var layoutNode = new OdfNode(OdfNodeType.Element, "data-pilot-layout-info", OdfNamespaces.Table, "table");
                layoutNode.SetAttribute("layout-mode", OdfNamespaces.Table, LayoutToString(_layout), "table");
                layoutNode.SetAttribute("add-empty-lines", OdfNamespaces.Table, "false", "table");
                levelNode.AppendChild(layoutNode);
                fieldNode.AppendChild(levelNode);
            }
            if (_valueOptions.TryGetValue(field.name, out OdfPivotValueOptions? valueOptions) &&
                valueOptions.ShowValuesAs != OdfPivotShowValuesAs.None)
            {
                var referenceNode = new OdfNode(
                    OdfNodeType.Element,
                    "data-pilot-field-reference",
                    OdfNamespaces.Table,
                    "table");
                referenceNode.SetAttribute(
                    "field-name",
                    OdfNamespaces.Table,
                    ResolveReferenceField(field.name, valueOptions),
                    "table");
                referenceNode.SetAttribute(
                    "type",
                    OdfNamespaces.Table,
                    ShowValuesAsToString(valueOptions.ShowValuesAs),
                    "table");
                // ODF 1.0～1.4 的 data-pilot-field-reference 即使不是成員相對運算，
                // 仍要求 member-type；僅 named 形狀可同時寫入 member-name。
                bool memberRelative = valueOptions.ShowValuesAs is
                    OdfPivotShowValuesAs.DifferenceFrom or
                    OdfPivotShowValuesAs.PercentageDifferenceFrom;
                referenceNode.SetAttribute(
                    "member-type",
                    OdfNamespaces.Table,
                    memberRelative ? "named" : "previous",
                    "table");
                if (memberRelative)
                {
                    referenceNode.SetAttribute(
                        "member-name",
                        OdfNamespaces.Table,
                        valueOptions.BaseMemberName!,
                        "table");
                }
                fieldNode.AppendChild(referenceNode);
            }
            if (_groupings.TryGetValue(field.name, out OdfPivotGroupingOptions? grouping))
            {
                var groupsNode = new OdfNode(
                    OdfNodeType.Element,
                    "data-pilot-groups",
                    OdfNamespaces.Table,
                    "table");
                groupsNode.SetAttribute("source-field-name", OdfNamespaces.Table, field.name, "table");
                if (grouping.DateGroup is OdfPivotDateGroup dateGroup)
                {
                    // ODF 1.0～1.2 將日期分組邊界與 step 規定為必填；
                    // 1.3 起雖放寬為選填，保留完整形狀可維持跨版本相容。
                    groupsNode.SetAttribute("date-start", OdfNamespaces.Table, "auto", "table");
                    groupsNode.SetAttribute("date-end", OdfNamespaces.Table, "auto", "table");
                    groupsNode.SetAttribute("step", OdfNamespaces.Table, "1", "table");
                    groupsNode.SetAttribute("grouped-by", OdfNamespaces.Table, DateGroupToString(dateGroup), "table");
                }
                else
                {
                    groupsNode.SetAttribute(
                        "start",
                        OdfNamespaces.Table,
                        grouping.Start!.Value.ToString("R", CultureInfo.InvariantCulture),
                        "table");
                    groupsNode.SetAttribute(
                        "end",
                        OdfNamespaces.Table,
                        grouping.End!.Value.ToString("R", CultureInfo.InvariantCulture),
                        "table");
                    groupsNode.SetAttribute(
                        "step",
                        OdfNamespaces.Table,
                        grouping.Interval!.Value.ToString("R", CultureInfo.InvariantCulture),
                        "table");
                    // ODF 1.0～1.2 的 schema 亦要求 grouped-by；數值分組以 days
                    // 作為不參與數值桶計算的相容性值，1.3+ 閱讀器依 start/end/step 判定數值分組。
                    groupsNode.SetAttribute("grouped-by", OdfNamespaces.Table, "days", "table");
                }
                AppendGroupingDefinitions(groupsNode, field.name, grouping);
                fieldNode.AppendChild(groupsNode);
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

    private void AppendGroupingDefinitions(
        OdfNode groupsNode,
        string fieldName,
        OdfPivotGroupingOptions grouping)
    {
        const int maximumRows = 1_000_000;
        const int maximumMembers = 250_000;
        int firstRow = Math.Min(_sourceRange.StartAddress.Row, _sourceRange.EndAddress.Row);
        int lastRow = Math.Max(_sourceRange.StartAddress.Row, _sourceRange.EndAddress.Row);
        if ((long)lastRow - firstRow > maximumRows)
            throw new InvalidOperationException();
        int firstColumn = Math.Min(_sourceRange.StartAddress.Column, _sourceRange.EndAddress.Column);
        int lastColumn = Math.Max(_sourceRange.StartAddress.Column, _sourceRange.EndAddress.Column);
        OdfTableSheet sourceSheet = string.IsNullOrEmpty(_sourceRange.StartAddress.SheetName)
            ? _sheet
            : _sheet.Document.Worksheets[_sourceRange.StartAddress.SheetName!];
        int sourceColumn = -1;
        for (int column = firstColumn; column <= lastColumn; column++)
        {
            if (string.Equals(
                    sourceSheet.Cells[firstRow, column].FormattedValue,
                    fieldName,
                    StringComparison.OrdinalIgnoreCase))
            {
                if (sourceColumn >= 0)
                    throw new InvalidDataException();
                sourceColumn = column;
            }
        }
        if (sourceColumn < 0)
            throw new InvalidDataException();

        var definitions = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        int memberCount = 0;
        for (int row = firstRow + 1; row <= lastRow; row++)
        {
            object? value = sourceSheet.Cells[row, sourceColumn].CellValue;
            string groupName = GroupValue(value, grouping);
            string memberName = FormatGroupingMember(value);
            if (!definitions.TryGetValue(groupName, out HashSet<string>? members))
            {
                members = new HashSet<string>(StringComparer.Ordinal);
                definitions.Add(groupName, members);
            }
            if (members.Add(memberName) && ++memberCount > maximumMembers)
                throw new InvalidOperationException();
        }
        if (definitions.Count == 0)
            definitions.Add("(blank)", new HashSet<string>(StringComparer.Ordinal) { string.Empty });

        foreach (KeyValuePair<string, HashSet<string>> definition in
            definitions.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var groupNode = new OdfNode(
                OdfNodeType.Element,
                "data-pilot-group",
                OdfNamespaces.Table,
                "table");
            groupNode.SetAttribute("name", OdfNamespaces.Table, definition.Key, "table");
            foreach (string member in definition.Value.OrderBy(value => value, StringComparer.Ordinal))
            {
                var memberNode = new OdfNode(
                    OdfNodeType.Element,
                    "data-pilot-group-member",
                    OdfNamespaces.Table,
                    "table");
                memberNode.SetAttribute("name", OdfNamespaces.Table, member, "table");
                groupNode.AppendChild(memberNode);
            }
            groupsNode.AppendChild(groupNode);
        }
    }

    private static string FormatGroupingMember(object? value) => value switch
    {
        null => string.Empty,
        DateTime date => date.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
        DateTimeOffset date => date.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
    };

    private static int[] ResolveIndexes(
        List<(string name, string orientation, string? function, string? formula)> fields,
        Dictionary<string, int> headers)
    {
        var result = new int[fields.Count];
        for (int index = 0; index < fields.Count; index++)
        {
            if (!headers.TryGetValue(fields[index].name, out result[index]))
                throw new InvalidDataException();
        }
        return result;
    }

    private static int[] ResolveDataIndexes(
        List<(string name, string orientation, string? function, string? formula)> fields,
        Dictionary<string, int> slots)
    {
        var result = new int[fields.Count];
        for (int index = 0; index < fields.Count; index++)
        {
            if (!slots.TryGetValue(fields[index].name, out result[index]))
                throw new InvalidDataException();
        }
        return result;
    }

    private static CompiledCalculatedField[] CompileCalculatedFields(
        List<(string name, string orientation, string? function, string? formula)> fields,
        Dictionary<string, int> slots,
        int sourceColumnCount,
        OdfPivotRefreshOptions options)
    {
        var result = new CompiledCalculatedField[fields.Count];
        for (int index = 0; index < fields.Count; index++)
        {
            string? formula = fields[index].formula;
            if (formula is null ||
                string.IsNullOrWhiteSpace(formula) ||
                formula.Length > options.MaximumFormulaCharacters)
            {
                throw new InvalidDataException();
            }
            result[index] = new CompiledCalculatedField(
                sourceColumnCount + index,
                OdfPivotCalculatedFormula.Compile(
                    formula,
                    slots,
                    options.MaximumFormulaNodes,
                    options.MaximumFormulaDepth));
        }
        return result;
    }

    private static int[] BuildCalculatedEvaluationOrder(
        CompiledCalculatedField[] fields,
        int sourceColumnCount)
    {
        var result = new List<int>(fields.Length);
        var states = new byte[fields.Length];
        for (int index = 0; index < fields.Length; index++)
            VisitCalculatedField(index, fields, sourceColumnCount, states, result);
        return result.ToArray();
    }

    private static void VisitCalculatedField(
        int index,
        CompiledCalculatedField[] fields,
        int sourceColumnCount,
        byte[] states,
        List<int> result)
    {
        if (states[index] == 2)
            return;
        if (states[index] == 1)
            throw new InvalidDataException();
        states[index] = 1;
        foreach (int reference in fields[index].Formula.References)
        {
            if (reference < sourceColumnCount)
                continue;
            int dependency = reference - sourceColumnCount;
            if ((uint)dependency >= (uint)fields.Length)
                throw new InvalidDataException();
            VisitCalculatedField(dependency, fields, sourceColumnCount, states, result);
        }
        states[index] = 2;
        result.Add(index);
    }

    private sealed class CompiledCalculatedField(
        int slot,
        OdfPivotCalculatedFormula formula)
    {
        internal int Slot { get; } = slot;

        internal OdfPivotCalculatedFormula Formula { get; } = formula;
    }

    private string[] BuildAxisMembers(
        object?[] values,
        int[] indexes,
        List<(string name, string orientation, string? function, string? formula)> fields)
    {
        var members = new string[indexes.Length];
        for (int index = 0; index < indexes.Length; index++)
        {
            object? value = values[indexes[index]];
            members[index] = _groupings.TryGetValue(fields[index].name, out OdfPivotGroupingOptions? grouping)
                ? GroupValue(value, grouping)
                : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }
        return members;
    }

    private static string BuildEncodedKey(IEnumerable<string> values)
    {
        var key = new System.Text.StringBuilder();
        foreach (string value in values)
        {
            key.Append(value.Length.ToString(CultureInfo.InvariantCulture));
            key.Append(':');
            key.Append(value);
        }
        return key.ToString();
    }

    private static string BuildGroupKey(string rowKey, string columnKey) =>
        rowKey.Length.ToString(CultureInfo.InvariantCulture) + ":" + rowKey + columnKey;

    private static string GroupValue(object? value, OdfPivotGroupingOptions grouping)
    {
        if (grouping.DateGroup is OdfPivotDateGroup dateGroup)
        {
            if (!TryConvertDate(value, out DateTime date))
                return "(blank)";
            return dateGroup switch
            {
                OdfPivotDateGroup.Years => date.ToString("yyyy", CultureInfo.InvariantCulture),
                OdfPivotDateGroup.Quarters => string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:yyyy}-Q{1}",
                    date,
                    ((date.Month - 1) / 3) + 1),
                OdfPivotDateGroup.Months => date.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                OdfPivotDateGroup.Days => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                OdfPivotDateGroup.Hours => date.ToString("yyyy-MM-dd HH:00", CultureInfo.InvariantCulture),
                OdfPivotDateGroup.Minutes => date.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                OdfPivotDateGroup.Seconds => date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                _ => throw new ArgumentOutOfRangeException(nameof(grouping)),
            };
        }

        if (!PivotAggregate.TryConvertNumber(value, out double number))
            return "(blank)";
        double start = grouping.Start!.Value;
        double end = grouping.End!.Value;
        double interval = grouping.Interval!.Value;
        if (number < start)
            return "< " + start.ToString("G17", CultureInfo.InvariantCulture);
        if (number > end)
            return "> " + end.ToString("G17", CultureInfo.InvariantCulture);
        double bucket = start + (Math.Floor((number - start) / interval) * interval);
        double bucketEnd = Math.Min(end, bucket + interval);
        return string.Format(
            CultureInfo.InvariantCulture,
            "[{0:G17}, {1:G17}{2}",
            bucket,
            bucketEnd,
            bucketEnd == end ? "]" : ")");
    }

    private static bool TryConvertDate(object? value, out DateTime date)
    {
        switch (value)
        {
            case DateTime dateTime:
                date = dateTime;
                return true;
            case DateTimeOffset dateTimeOffset:
                date = dateTimeOffset.DateTime;
                return true;
            case string text when DateTime.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.RoundtripKind,
                out DateTime parsed):
                date = parsed;
                return true;
            default:
                date = default;
                return false;
        }
    }

    private static Dictionary<string, PivotAggregate[]> CreateAxisTotals(
        IEnumerable<string> keys,
        int dataFieldCount)
    {
        var totals = new Dictionary<string, PivotAggregate[]>(StringComparer.Ordinal);
        foreach (string key in keys)
            totals.Add(key, CreateAggregates(dataFieldCount));
        return totals;
    }

    private static PivotAggregate[] CreateAggregates(int count)
    {
        var result = new PivotAggregate[count];
        for (int index = 0; index < count; index++)
            result[index] = new PivotAggregate();
        return result;
    }

    private object? TransformValue(
        object? rawValue,
        object? rowTotal,
        object? columnTotal,
        object? grandTotal,
        string dataFieldName,
        KeyValuePair<string, string> row,
        KeyValuePair<string, string> column,
        Dictionary<string, (bool IsRow, string Key)> relativeBases,
        Dictionary<string, PivotAggregate[]> groups,
        string? function,
        int dataIndex,
        double[,] runningTotals,
        int columnIndex)
    {
        if (!_valueOptions.TryGetValue(dataFieldName, out OdfPivotValueOptions? options) ||
            options.ShowValuesAs == OdfPivotShowValuesAs.None)
        {
            return rawValue;
        }
        if (!PivotAggregate.TryConvertNumber(rawValue, out double value))
            return null;

        return options.ShowValuesAs switch
        {
            OdfPivotShowValuesAs.PercentageOfRowTotal => Divide(value, rowTotal),
            OdfPivotShowValuesAs.PercentageOfColumnTotal => Divide(value, columnTotal),
            OdfPivotShowValuesAs.PercentageOfGrandTotal => Divide(value, grandTotal),
            OdfPivotShowValuesAs.Index => CalculateIndex(value, rowTotal, columnTotal, grandTotal),
            OdfPivotShowValuesAs.RunningTotal =>
                runningTotals[columnIndex, dataIndex] =
                    checked(runningTotals[columnIndex, dataIndex] + value),
            OdfPivotShowValuesAs.DifferenceFrom =>
                DifferenceFromBase(
                    value,
                    dataFieldName,
                    row,
                    column,
                    relativeBases,
                    groups,
                    function,
                    dataIndex,
                    false),
            OdfPivotShowValuesAs.PercentageDifferenceFrom =>
                DifferenceFromBase(
                    value,
                    dataFieldName,
                    row,
                    column,
                    relativeBases,
                    groups,
                    function,
                    dataIndex,
                    true),
            _ => rawValue,
        };
    }

    private static object? DifferenceFromBase(
        double value,
        string dataFieldName,
        KeyValuePair<string, string> row,
        KeyValuePair<string, string> column,
        Dictionary<string, (bool IsRow, string Key)> relativeBases,
        Dictionary<string, PivotAggregate[]> groups,
        string? function,
        int dataIndex,
        bool percentage)
    {
        if (!relativeBases.TryGetValue(dataFieldName, out (bool IsRow, string Key) relativeBase))
            throw new InvalidOperationException();
        string baseRowKey = relativeBase.IsRow ? relativeBase.Key : row.Key;
        string baseColumnKey = relativeBase.IsRow ? column.Key : relativeBase.Key;
        if (string.IsNullOrEmpty(baseRowKey) ||
            string.IsNullOrEmpty(baseColumnKey) ||
            !groups.TryGetValue(BuildGroupKey(baseRowKey, baseColumnKey), out PivotAggregate[]? aggregates) ||
            !PivotAggregate.TryConvertNumber(aggregates[dataIndex].GetValue(function), out double baseValue))
        {
            return null;
        }
        double difference = value - baseValue;
        return percentage ? Divide(difference, baseValue) : difference;
    }

    private Dictionary<string, (bool IsRow, string Key)> ResolveRelativeBases(
        List<(string name, string orientation, string? function, string? formula)> dataFields,
        List<(string name, string orientation, string? function, string? formula)> rowFields,
        List<(string name, string orientation, string? function, string? formula)> columnFields,
        KeyValuePair<string, string>[] rows,
        KeyValuePair<string, string>[] columns)
    {
        var result = new Dictionary<string, (bool IsRow, string Key)>(StringComparer.OrdinalIgnoreCase);
        foreach (var dataField in dataFields)
        {
            if (!_valueOptions.TryGetValue(dataField.name, out OdfPivotValueOptions? options) ||
                options.ShowValuesAs is not (
                    OdfPivotShowValuesAs.DifferenceFrom or
                    OdfPivotShowValuesAs.PercentageDifferenceFrom))
            {
                continue;
            }
            bool isRow;
            KeyValuePair<string, string>[] axis;
            if (rowFields.Count == 1 &&
                string.Equals(rowFields[0].name, options.BaseFieldName, StringComparison.OrdinalIgnoreCase))
            {
                isRow = true;
                axis = rows;
            }
            else if (columnFields.Count == 1 &&
                string.Equals(columnFields[0].name, options.BaseFieldName, StringComparison.OrdinalIgnoreCase))
            {
                isRow = false;
                axis = columns;
            }
            else
            {
                throw new InvalidOperationException();
            }
            string key = axis.FirstOrDefault(
                item => string.Equals(item.Value, options.BaseMemberName, StringComparison.Ordinal)).Key ?? string.Empty;
            result.Add(dataField.name, (isRow, key));
        }
        return result;
    }

    private static object? Divide(double numerator, object? denominator) =>
        PivotAggregate.TryConvertNumber(denominator, out double value) && value != 0
            ? numerator / value
            : null;

    private static object? CalculateIndex(
        double value,
        object? rowTotal,
        object? columnTotal,
        object? grandTotal)
    {
        if (!PivotAggregate.TryConvertNumber(rowTotal, out double row) ||
            !PivotAggregate.TryConvertNumber(columnTotal, out double column) ||
            !PivotAggregate.TryConvertNumber(grandTotal, out double grand) ||
            row == 0 ||
            column == 0)
        {
            return null;
        }
        return value * grand / (row * column);
    }

    private void ValidateOutputStyles()
    {
        if (_outputStyles is null)
            return;
        foreach (string? styleName in new[]
        {
            _outputStyles.HeaderStyleName,
            _outputStyles.DataStyleName,
            _outputStyles.GrandTotalStyleName,
        })
        {
            if (!string.IsNullOrWhiteSpace(styleName) &&
                !_sheet.Document.Styles.StyleExists(styleName!))
            {
                throw new InvalidDataException();
            }
        }
    }

    private void ValidateAdvancedFields(
        List<(string name, string orientation, string? function, string? formula)> rowFields,
        List<(string name, string orientation, string? function, string? formula)> columnFields,
        List<(string name, string orientation, string? function, string? formula)> dataFields)
    {
        foreach (string fieldName in _groupings.Keys)
        {
            if (!rowFields.Concat(columnFields).Any(
                    field => string.Equals(field.name, fieldName, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidDataException();
            }
        }
        foreach (KeyValuePair<string, OdfPivotValueOptions> pair in _valueOptions)
        {
            if (!dataFields.Any(
                    field => string.Equals(field.name, pair.Key, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidDataException();
            }
            if (pair.Value.ShowValuesAs == OdfPivotShowValuesAs.RunningTotal &&
                rowFields.Count == 0)
            {
                throw new InvalidOperationException();
            }
        }
    }

    private string? ResolveOutputStyle(PivotOutputKind kind) => kind switch
    {
        PivotOutputKind.Header => _outputStyles?.HeaderStyleName,
        PivotOutputKind.Data => _outputStyles?.DataStyleName,
        PivotOutputKind.GrandTotal => _outputStyles?.GrandTotalStyleName,
        _ => null,
    };

    private bool MatchesFilters(object?[] values, Dictionary<string, int> headers)
    {
        foreach ((string fieldName, OdfPivotFilterOperator op, string expected) in _filters)
        {
            if (!headers.TryGetValue(fieldName, out int index))
                throw new InvalidDataException();
            string actual = Convert.ToString(values[index], CultureInfo.InvariantCulture) ?? string.Empty;
            int comparison;
            if (double.TryParse(actual, NumberStyles.Float, CultureInfo.InvariantCulture, out double actualNumber) &&
                double.TryParse(expected, NumberStyles.Float, CultureInfo.InvariantCulture, out double expectedNumber))
            {
                comparison = actualNumber.CompareTo(expectedNumber);
            }
            else
            {
                comparison = string.Compare(actual, expected, StringComparison.Ordinal);
            }
            bool matches = op switch
            {
                OdfPivotFilterOperator.Equal => comparison == 0,
                OdfPivotFilterOperator.NotEqual => comparison != 0,
                OdfPivotFilterOperator.GreaterThan => comparison > 0,
                OdfPivotFilterOperator.GreaterThanOrEqual => comparison >= 0,
                OdfPivotFilterOperator.LessThan => comparison < 0,
                OdfPivotFilterOperator.LessThanOrEqual => comparison <= 0,
                _ => false,
            };
            if (!matches)
                return false;
        }
        return true;
    }

    private sealed class PivotAggregate
    {
        private double _sum;
        private double _minimum = double.PositiveInfinity;
        private double _maximum = double.NegativeInfinity;
        private int _numericCount;
        private int _count;

        internal void Add(object? value)
        {
            if (value is null)
                return;
            _count++;
            if (!TryConvertNumber(value, out double number))
            {
                return;
            }
            _sum += number;
            if (double.IsNaN(_sum) || double.IsInfinity(_sum))
                throw new OverflowException();
            _minimum = Math.Min(_minimum, number);
            _maximum = Math.Max(_maximum, number);
            _numericCount++;
        }

        internal void Merge(PivotAggregate other)
        {
            _sum += other._sum;
            if (double.IsNaN(_sum) || double.IsInfinity(_sum))
                throw new OverflowException();
            _minimum = Math.Min(_minimum, other._minimum);
            _maximum = Math.Max(_maximum, other._maximum);
            _numericCount = checked(_numericCount + other._numericCount);
            _count = checked(_count + other._count);
        }

        internal static bool TryConvertNumber(object? value, out double number)
        {
            switch (value)
            {
                case double doubleValue:
                    number = doubleValue;
                    break;
                case float floatValue:
                    number = floatValue;
                    break;
                case decimal decimalValue:
                    number = (double)decimalValue;
                    break;
                case long longValue:
                    number = longValue;
                    break;
                case ulong ulongValue:
                    number = ulongValue;
                    break;
                case int intValue:
                    number = intValue;
                    break;
                case uint uintValue:
                    number = uintValue;
                    break;
                case short shortValue:
                    number = shortValue;
                    break;
                case ushort ushortValue:
                    number = ushortValue;
                    break;
                case byte byteValue:
                    number = byteValue;
                    break;
                case sbyte sbyteValue:
                    number = sbyteValue;
                    break;
                case string text when double.TryParse(
                    text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double parsed):
                    number = parsed;
                    break;
                default:
                    number = 0d;
                    return false;
            }
            return !double.IsNaN(number) && !double.IsInfinity(number);
        }

        internal object? GetValue(string? function) => function switch
        {
            "count" => (double)_count,
            "average" => _numericCount == 0 ? null : _sum / _numericCount,
            "max" => _numericCount == 0 ? null : _maximum,
            "min" => _numericCount == 0 ? null : _minimum,
            _ => _sum,
        };
    }

    private enum PivotOutputKind
    {
        Header,
        Data,
        GrandTotal,
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

    private static void ValidateGrouping(OdfPivotGroupingOptions options)
    {
        if (options.DateGroup is not null)
        {
            if (!IsDefined(options.DateGroup.Value) ||
                options.Start is not null ||
                options.End is not null ||
                options.Interval is not null)
            {
                throw new ArgumentException(null, nameof(options));
            }
            return;
        }

        if (options.Start is not double start ||
            options.End is not double end ||
            options.Interval is not double interval ||
            !IsFinite(start) ||
            !IsFinite(end) ||
            !IsFinite(interval) ||
            interval <= 0 ||
            start >= end ||
            Math.Ceiling((end - start) / interval) > 1_000_000)
        {
            throw new ArgumentException(null, nameof(options));
        }
    }

    private static OdfPivotGroupingOptions CopyGrouping(OdfPivotGroupingOptions options) =>
        new()
        {
            DateGroup = options.DateGroup,
            Start = options.Start,
            End = options.End,
            Interval = options.Interval,
        };

    private static void ValidateValueOptions(OdfPivotValueOptions options)
    {
        if (!IsDefined(options.ShowValuesAs))
            throw new ArgumentOutOfRangeException(nameof(options));
        bool memberRelative = options.ShowValuesAs is
            OdfPivotShowValuesAs.DifferenceFrom or
            OdfPivotShowValuesAs.PercentageDifferenceFrom;
        if (memberRelative &&
            (string.IsNullOrWhiteSpace(options.BaseFieldName) ||
             string.IsNullOrWhiteSpace(options.BaseMemberName)))
        {
            throw new ArgumentException(null, nameof(options));
        }
    }

    private static bool IsFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value);

    private static bool IsDefined<T>(T value)
        where T : struct, Enum
    {
#if NETSTANDARD2_0
        return Enum.IsDefined(typeof(T), value);
#else
        return Enum.IsDefined(value);
#endif
    }

    private string ResolveReferenceField(string valueFieldName, OdfPivotValueOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.BaseFieldName))
            return options.BaseFieldName!;
        List<(string name, string orientation, string? function, string? formula)> axis =
            _fields.Where(field => field.orientation == "row").ToList();
        if (axis.Count == 0)
            axis = _fields.Where(field => field.orientation == "column").ToList();
        if (axis.Count == 0)
            throw new InvalidOperationException();
        return axis[0].name;
    }

    private static string GrandTotalToString(OdfPivotGrandTotal value) => value switch
    {
        OdfPivotGrandTotal.None => "none",
        OdfPivotGrandTotal.Row => "row",
        OdfPivotGrandTotal.Column => "column",
        OdfPivotGrandTotal.Both => "both",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static string LayoutToString(OdfPivotLayout value) => value switch
    {
        OdfPivotLayout.OutlineSubtotalsBottom => "outline-subtotals-bottom",
        OdfPivotLayout.OutlineSubtotalsTop => "outline-subtotals-top",
        OdfPivotLayout.Tabular => "tabular-layout",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static string ShowValuesAsToString(OdfPivotShowValuesAs value) => value switch
    {
        OdfPivotShowValuesAs.PercentageOfRowTotal => "row-percentage",
        OdfPivotShowValuesAs.PercentageOfColumnTotal => "column-percentage",
        OdfPivotShowValuesAs.PercentageOfGrandTotal => "total-percentage",
        OdfPivotShowValuesAs.RunningTotal => "running-total",
        OdfPivotShowValuesAs.DifferenceFrom => "member-difference",
        OdfPivotShowValuesAs.PercentageDifferenceFrom => "member-percentage-difference",
        OdfPivotShowValuesAs.Index => "index",
        _ => "none",
    };

    private static string DateGroupToString(OdfPivotDateGroup value) => value switch
    {
        OdfPivotDateGroup.Years => "years",
        OdfPivotDateGroup.Quarters => "quarters",
        OdfPivotDateGroup.Months => "months",
        OdfPivotDateGroup.Days => "days",
        OdfPivotDateGroup.Hours => "hours",
        OdfPivotDateGroup.Minutes => "minutes",
        OdfPivotDateGroup.Seconds => "seconds",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };
}
