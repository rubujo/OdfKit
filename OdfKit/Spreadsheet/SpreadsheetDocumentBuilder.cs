using System.Globalization;
using OdfKit.Styles;

using OdfKit.Chart;
using OdfKit.Compliance;
using OdfKit.DOM;
using OdfKit.Text;
namespace OdfKit.Spreadsheet;

/// <summary>
/// Provides a fluent creation API for <see cref="SpreadsheetDocument"/>.
/// 提供 <see cref="SpreadsheetDocument"/> 的 Fluent 建立 API。
/// </summary>
public sealed class SpreadsheetDocumentBuilder
{
    private readonly SpreadsheetDocument _document;
    private OdfStyleSet? _styles;

    internal SpreadsheetDocumentBuilder(SpreadsheetDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>
    /// Configures document metadata.
    /// 設定文件中繼資料。
    /// </summary>
    /// <param name="configure">The delegate to invoke. / 中繼資料設定委派</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public SpreadsheetDocumentBuilder WithMetadata(Action<TextDocumentMetadataBuilder> configure)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));

        configure(new TextDocumentMetadataBuilder(new OdfDocumentMetadata(_document)));
        return this;
    }

    /// <summary>
    /// Configures the style set applied to content subsequently created by this builder.
    /// 設定此 builder 後續建立內容會套用的樣式集合。
    /// </summary>
    /// <param name="styles">The value to use. / 樣式集合</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public SpreadsheetDocumentBuilder WithStyles(OdfStyleSet styles)
    {
        _styles = styles ?? throw new ArgumentNullException(nameof(styles));
        return this;
    }

    /// <summary>
    /// Configures the style set applied to content subsequently created by this builder.
    /// 設定此 builder 後續建立內容會套用的樣式集合。
    /// </summary>
    /// <param name="configure">The delegate to invoke. / 樣式集合設定委派</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public SpreadsheetDocumentBuilder WithStyles(Action<OdfStyleSet> configure)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));

        var styles = new OdfStyleSet();
        configure(styles);
        return WithStyles(styles);
    }

    /// <summary>
    /// Configures the design theme applied to content subsequently created by this builder.
    /// 設定此 builder 後續建立內容會套用的設計主題。
    /// </summary>
    /// <param name="theme">The value to use. / 設計主題</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public SpreadsheetDocumentBuilder WithTheme(OdfDesignTheme theme)
    {
        _styles = OdfStyleSet.FromTheme(theme);
        return this;
    }

    /// <summary>
    /// Adds a worksheet and configures its content.
    /// 新增工作表並設定其內容。
    /// </summary>
    /// <param name="name">The name or identifier. / 工作表名稱</param>
    /// <param name="configure">The delegate to invoke. / 工作表設定委派</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public SpreadsheetDocumentBuilder AddSheet(string name, Action<OdfSheetBuilder> configure)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));
        OdfTableSheet sheet = _document.Worksheets.Add(name);
        configure(new OdfSheetBuilder(_document, sheet, _styles));
        return this;
    }

    /// <summary>
    /// Builds and returns the spreadsheet document.
    /// 建立並傳回試算表文件。
    /// </summary>
    /// <returns>The result. / 建立完成的試算表文件</returns>
    public SpreadsheetDocument Build()
    {
        return _document;
    }
}

/// <summary>
/// Provides a fluent creation API for worksheet content.
/// 提供工作表內容的 Fluent 建立 API。
/// </summary>
public sealed class OdfSheetBuilder
{
    private readonly SpreadsheetDocument _document;
    private readonly OdfTableSheet _sheet;
    private readonly OdfStyleSet? _styles;

    internal OdfSheetBuilder(SpreadsheetDocument document, OdfTableSheet sheet, OdfStyleSet? styles)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
        _styles = styles;
    }

    /// <summary>
    /// Sets the value of the specified cell.
    /// 設定指定儲存格的值。
    /// </summary>
    /// <param name="address">The cell address. / 儲存格位址，例如 <c>A1</c></param>
    /// <param name="value">The value to use. / 儲存格值</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public OdfSheetBuilder SetCell(string address, object? value)
    {
        OdfCell cell = _sheet.Cells[address];
        cell.CellValue = value;
        ApplyBodyStyle(cell);
        return this;
    }

    /// <summary>
    /// Sets the formula for the specified cell.
    /// 設定指定儲存格的公式。
    /// </summary>
    /// <param name="address">The cell address. / 儲存格位址，例如 <c>A1</c></param>
    /// <param name="formula">The value to use. / ODF 公式文字</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public OdfSheetBuilder SetFormula(string address, string formula)
    {
        OdfCell cell = _sheet.Cells[address];
        cell.Formula = formula;
        ApplyBodyStyle(cell);
        return this;
    }

    /// <summary>
    /// Sets formulas cell by cell across the specified cell range.
    /// 對指定儲存格範圍逐格設定公式。
    /// </summary>
    /// <param name="range">The cell range. / 要設定公式的儲存格範圍</param>
    /// <param name="formulaFactory">The delegate to invoke. / 依 1-based 列號與欄號產生 ODF 公式文字的委派</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public OdfSheetBuilder SetFormulaRange(OdfCellRange range, Func<int, int, string> formulaFactory)
    {
        if (formulaFactory is null)
            throw new ArgumentNullException(nameof(formulaFactory));

        OdfCellRange normalizedRange = EnsureSheetName(range);
        int startRow = Math.Min(normalizedRange.StartAddress.Row, normalizedRange.EndAddress.Row);
        int endRow = Math.Max(normalizedRange.StartAddress.Row, normalizedRange.EndAddress.Row);
        int startColumn = Math.Min(normalizedRange.StartAddress.Column, normalizedRange.EndAddress.Column);
        int endColumn = Math.Max(normalizedRange.StartAddress.Column, normalizedRange.EndAddress.Column);

        for (int row = startRow; row <= endRow; row++)
        {
            for (int column = startColumn; column <= endColumn; column++)
            {
                OdfCell cell = _sheet.Cells[row, column];
                cell.Formula = formulaFactory(row + 1, column + 1);
                ApplyBodyStyle(cell);
            }
        }

        return this;
    }

    /// <summary>
    /// Sets formulas cell by cell across the specified cell range.
    /// 對指定儲存格範圍逐格設定公式。
    /// </summary>
    /// <param name="range">The cell range. / 儲存格範圍字串，例如 <c>D2:D20</c></param>
    /// <param name="formulaFactory">The delegate to invoke. / 依 1-based 列號與欄號產生 ODF 公式文字的委派</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public OdfSheetBuilder SetFormulaRange(string range, Func<int, int, string> formulaFactory)
        => SetFormulaRange(ParseRange(range), formulaFactory);

    /// <summary>
    /// Adds a formula column with a header cell and formulas for the specified data rows.
    /// 新增公式欄，包含標題儲存格與指定資料列的公式。
    /// </summary>
    /// <param name="columnName">The name or identifier. / 欄位名稱，例如 <c>D</c></param>
    /// <param name="header">The name or identifier. / 標題列文字</param>
    /// <param name="firstDataRow">The numeric value. / 第一筆資料列，採 1 為基準</param>
    /// <param name="lastDataRow">The numeric value. / 最後一筆資料列，採 1 為基準</param>
    /// <param name="formulaFactory">The delegate to invoke. / 依 1-based 列號產生 ODF 公式文字的委派</param>
    /// <param name="headerRow">The name or identifier. / 標題列，採 1 為基準</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public OdfSheetBuilder AddFormulaColumn(
        string columnName,
        string header,
        int firstDataRow,
        int lastDataRow,
        Func<int, string> formulaFactory,
        int headerRow = 1)
    {
        if (string.IsNullOrWhiteSpace(columnName))
            throw new ArgumentNullException(nameof(columnName));
        if (formulaFactory is null)
            throw new ArgumentNullException(nameof(formulaFactory));
        EnsureOneBasedIndex(headerRow, nameof(headerRow));
        EnsureOneBasedIndex(firstDataRow, nameof(firstDataRow));
        EnsureOneBasedIndex(lastDataRow, nameof(lastDataRow));

        int columnIndex = OdfCellAddress.ParseExcel(columnName.Trim() + headerRow.ToString(CultureInfo.InvariantCulture)).Column;
        OdfCell headerCell = _sheet.Cells[headerRow - 1, columnIndex];
        headerCell.CellValue = header;
        ApplyHeaderStyle(headerCell);
        var range = new OdfCellRange(firstDataRow - 1, columnIndex, lastDataRow - 1, columnIndex, _sheet.Name);
        return SetFormulaRange(range, (row, _) => formulaFactory(row));
    }

    /// <summary>
    /// Imports row data.
    /// 匯入列資料。
    /// </summary>
    /// <typeparam name="T">The type of item. / 資料專案型別</typeparam>
    /// <param name="items">The value to use. / 要匯入的資料專案</param>
    /// <param name="selector">The delegate to invoke. / 將資料專案轉為儲存格值陣列的委派</param>
    /// <param name="startRow">The numeric value. / 起始列，採 1 為基準</param>
    /// <param name="startColumn">The numeric value. / 起始欄，採 1 為基準</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public OdfSheetBuilder ImportRows<T>(
        IEnumerable<T> items,
        Func<T, object?[]> selector,
        int startRow = 1,
        int startColumn = 1)
    {
        if (items is null)
            throw new ArgumentNullException(nameof(items));
        if (selector is null)
            throw new ArgumentNullException(nameof(selector));
        EnsureOneBasedIndex(startRow, nameof(startRow));
        EnsureOneBasedIndex(startColumn, nameof(startColumn));

        int rowIndex = startRow - 1;
        int columnOffset = startColumn - 1;
        foreach (T item in items)
        {
            object?[] values = selector(item) ?? throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_SpreadsheetDocumentBuilder_ColumnSelectorsCannotReturn"));
            for (int i = 0; i < values.Length; i++)
            {
                OdfCell cell = _sheet.Cells[rowIndex, columnOffset + i];
                cell.CellValue = values[i];
                ApplyBodyStyle(cell);
            }

            rowIndex++;
        }

        return this;
    }

    /// <summary>
    /// Imports a table with a header row.
    /// 匯入含標題列的資料表。
    /// </summary>
    /// <typeparam name="T">The type of item. / 資料專案型別</typeparam>
    /// <param name="items">The value to use. / 要匯入的資料專案</param>
    /// <param name="rowSelector">The delegate to invoke. / 將資料專案轉為儲存格值陣列的委派</param>
    /// <param name="headers">The name or identifier. / 標題列文字</param>
    /// <param name="startRow">The numeric value. / 標題列起始列，採 1 為基準</param>
    /// <param name="startColumn">The numeric value. / 標題列起始欄，採 1 為基準</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public OdfSheetBuilder ImportTable<T>(
        IEnumerable<T> items,
        Func<T, object?[]> rowSelector,
        IEnumerable<string> headers,
        int startRow = 1,
        int startColumn = 1)
    {
        if (headers is null)
            throw new ArgumentNullException(nameof(headers));
        EnsureOneBasedIndex(startRow, nameof(startRow));
        EnsureOneBasedIndex(startColumn, nameof(startColumn));

        int columnIndex = startColumn - 1;
        foreach (string header in headers)
        {
            OdfCell cell = _sheet.Cells[startRow - 1, columnIndex];
            cell.CellValue = header;
            ApplyHeaderStyle(cell);
            columnIndex++;
        }

        return ImportRows(items, rowSelector, startRow + 1, startColumn);
    }

    private void ApplyHeaderStyle(OdfCell cell)
    {
        if (_styles is null)
            return;

        if (_styles.TableHeaderBackgroundColor is not null)
        {
            cell.Style.Fill.Color = _styles.TableHeaderBackgroundColor;
        }

        if (_styles.TableHeaderColor is not null)
        {
            cell.Style.Font.Color = _styles.TableHeaderColor;
        }

        cell.Style.Font.IsBold = _styles.TableHeaderBold;
    }

    private void ApplyBodyStyle(OdfCell cell)
    {
        if (_styles is null)
            return;

        if (_styles.BodyColor is not null)
        {
            cell.Style.Font.Color = _styles.BodyColor;
        }

        if (_styles.BodyFontSizePoints.HasValue)
        {
            cell.Style.Font.Size = ToPointString(_styles.BodyFontSizePoints.Value);
        }
    }

    /// <summary>
    /// Sets the column width.
    /// 設定欄寬。
    /// </summary>
    /// <param name="columnIndex">The numeric value. / 欄索引，採 1 為基準</param>
    /// <param name="widthCm">The value to use. / 欄寬，單位為公分</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public OdfSheetBuilder SetColumnWidth(int columnIndex, double widthCm)
    {
        EnsureOneBasedIndex(columnIndex, nameof(columnIndex));
        if (widthCm <= 0)
            throw new ArgumentOutOfRangeException(nameof(widthCm));

        _sheet.SetColumnWidth(columnIndex - 1, OdfLength.FromCentimeters(widthCm));
        return this;
    }

    /// <summary>
    /// Freezes panes above and to the left of the specified cell.
    /// 凍結指定儲存格上方與左側的窗格。
    /// </summary>
    /// <param name="row">The numeric value. / 作用儲存格列，採 1 為基準</param>
    /// <param name="column">The numeric value. / 作用儲存格欄，採 1 為基準</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public OdfSheetBuilder FreezeAt(int row, int column)
    {
        EnsureOneBasedIndex(row, nameof(row));
        EnsureOneBasedIndex(column, nameof(column));

        _sheet.FreezePanes(row - 1, column - 1);
        return this;
    }

    /// <summary>
    /// Adds a worksheet-level named range.
    /// 新增工作表層命名範圍。
    /// </summary>
    /// <param name="name">The name or identifier. / 命名範圍名稱</param>
    /// <param name="range">The cell range. / 儲存格範圍</param>
    /// <param name="baseCell">The cell address. / 基準儲存格位址</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public OdfSheetBuilder AddNamedRange(string name, OdfCellRange range, OdfCellAddress? baseCell = null)
    {
        _sheet.AddNamedRange(name, EnsureSheetName(range), baseCell);
        return this;
    }

    /// <summary>
    /// Adds a worksheet-level named range.
    /// 新增工作表層命名範圍。
    /// </summary>
    /// <param name="name">The name or identifier. / 命名範圍名稱</param>
    /// <param name="range">The cell range. / 儲存格範圍字串，例如 <c>A1:D10</c></param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public OdfSheetBuilder AddNamedRange(string name, string range)
        => AddNamedRange(name, ParseRange(range));

    /// <summary>
    /// Adds a worksheet-level named expression.
    /// 新增工作表層具名運算式。
    /// </summary>
    /// <param name="name">The name or identifier. / 具名運算式名稱</param>
    /// <param name="expression">The value to use. / 公式運算式字串</param>
    /// <param name="baseCell">The cell address. / 基準儲存格位址</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public OdfSheetBuilder AddNamedExpression(string name, string expression, OdfCellAddress? baseCell = null)
    {
        _sheet.AddNamedExpression(name, expression, baseCell);
        return this;
    }

    /// <summary>
    /// Adds an embedded chart to the current worksheet.
    /// 在目前工作表新增嵌入圖表。
    /// </summary>
    /// <param name="anchor">The cell address. / 圖表左上角錨定儲存格</param>
    /// <param name="chart">The value to use. / 圖表定義</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public OdfSheetBuilder AddChart(OdfCellAddress anchor, OdfChartDefinition chart)
    {
        _document.AddChart(_sheet.Name, EnsureSheetName(anchor), chart);
        return this;
    }

    /// <summary>
    /// Adds an embedded chart to the current worksheet.
    /// 在目前工作表新增嵌入圖表。
    /// </summary>
    /// <param name="anchor">The cell address. / 圖表左上角錨定儲存格，例如 <c>F1</c></param>
    /// <param name="chart">The value to use. / 圖表定義</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public OdfSheetBuilder AddChart(string anchor, OdfChartDefinition chart)
        => AddChart(ParseAddress(anchor), chart);

    /// <summary>
    /// Inserts an embedded chart into the current worksheet for immediate configuration.
    /// 在目前工作表插入可立即設定的嵌入圖表。
    /// </summary>
    /// <param name="dataRange">The cell range. / 圖表資料來源範圍</param>
    /// <param name="chartType">The value to use. / 圖表類型</param>
    /// <param name="configure">The delegate to invoke. / 圖表設定委派</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public OdfSheetBuilder InsertChart(
        OdfCellRange dataRange,
        OdfChartType chartType,
        Action<OdfChartDocument>? configure = null)
    {
        OdfChartDocument chart = _sheet.InsertChart(EnsureSheetName(dataRange), chartType);
        configure?.Invoke(chart);
        return this;
    }

    /// <summary>
    /// Inserts an embedded chart into the current worksheet for immediate configuration.
    /// 在目前工作表插入可立即設定的嵌入圖表。
    /// </summary>
    /// <param name="dataRange">The cell range. / 圖表資料來源範圍字串，例如 <c>A1:D10</c></param>
    /// <param name="chartType">The value to use. / 圖表類型</param>
    /// <param name="configure">The delegate to invoke. / 圖表設定委派</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public OdfSheetBuilder InsertChart(
        string dataRange,
        OdfChartType chartType,
        Action<OdfChartDocument>? configure = null)
        => InsertChart(ParseRange(dataRange), chartType, configure);

    /// <summary>
    /// Adds a data validation rule.
    /// 新增資料驗證規則。
    /// </summary>
    /// <param name="validation">The value to use. / 資料驗證設定</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public OdfSheetBuilder AddDataValidation(OdfDataValidation validation)
    {
        _document.AddDataValidation(_sheet.Name, validation);
        return this;
    }

    /// <summary>
    /// Adds a decimal numeric range data validation rule.
    /// 新增十進位數值範圍資料驗證規則。
    /// </summary>
    /// <param name="range">The cell range. / 套用驗證的儲存格範圍</param>
    /// <param name="minimum">The numeric value. / 允許的最小值</param>
    /// <param name="maximum">The numeric value. / 允許的最大值</param>
    /// <param name="errorTitle">The value to use. / 輸入錯誤時顯示的標題</param>
    /// <param name="errorMessage">The value to use. / 輸入錯誤時顯示的訊息內容</param>
    /// <param name="alertStyle">The value to use. / 輸入錯誤時的警告樣式等級</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public OdfSheetBuilder AddDecimalValidation(
        OdfCellRange range,
        double minimum,
        double maximum,
        string? errorTitle = null,
        string? errorMessage = null,
        OdfValidationAlertStyle alertStyle = OdfValidationAlertStyle.Stop)
    {
        _document.AddDataValidation(_sheet.Name, new OdfDataValidation
        {
            ApplyTo = EnsureSheetName(range),
            Condition = OdfValidationCondition.DecimalBetween,
            Formula1 = minimum.ToString(CultureInfo.InvariantCulture),
            Formula2 = maximum.ToString(CultureInfo.InvariantCulture),
            ErrorTitle = errorTitle ?? string.Empty,
            ErrorMessage = errorMessage ?? string.Empty,
            AlertStyle = alertStyle,
        });
        return this;
    }

    /// <summary>
    /// Adds a decimal numeric range data validation rule.
    /// 新增十進位數值範圍資料驗證規則。
    /// </summary>
    /// <param name="range">The cell range. / 套用驗證的儲存格範圍字串，例如 <c>B2:C20</c></param>
    /// <param name="minimum">The numeric value. / 允許的最小值</param>
    /// <param name="maximum">The numeric value. / 允許的最大值</param>
    /// <param name="errorTitle">The value to use. / 輸入錯誤時顯示的標題</param>
    /// <param name="errorMessage">The value to use. / 輸入錯誤時顯示的訊息內容</param>
    /// <param name="alertStyle">The value to use. / 輸入錯誤時的警告樣式等級</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public OdfSheetBuilder AddDecimalValidation(
        string range,
        double minimum,
        double maximum,
        string? errorTitle = null,
        string? errorMessage = null,
        OdfValidationAlertStyle alertStyle = OdfValidationAlertStyle.Stop)
        => AddDecimalValidation(ParseRange(range), minimum, maximum, errorTitle, errorMessage, alertStyle);

    /// <summary>
    /// Adds a conditional formatting rule.
    /// 新增條件格式規則。
    /// </summary>
    /// <param name="range">The cell range. / 儲存格範圍</param>
    /// <param name="conditionValue">The value to use. / 條件運算式</param>
    /// <param name="styleName">The name or identifier. / 要套用的格式樣式名稱</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public OdfSheetBuilder AddConditionalFormat(OdfCellRange range, string conditionValue, string styleName)
    {
        _sheet.AddConditionalFormat(EnsureSheetName(range), conditionValue, styleName);
        return this;
    }

    /// <summary>
    /// Adds a conditional formatting rule.
    /// 新增條件格式規則。
    /// </summary>
    /// <param name="range">The cell range. / 儲存格範圍字串，例如 <c>D2:D20</c></param>
    /// <param name="conditionValue">The value to use. / 條件運算式</param>
    /// <param name="styleName">The name or identifier. / 要套用的格式樣式名稱</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public OdfSheetBuilder AddConditionalFormat(string range, string conditionValue, string styleName)
        => AddConditionalFormat(ParseRange(range), conditionValue, styleName);

    /// <summary>
    /// Adds a data bar conditional format.
    /// 新增資料橫條條件格式。
    /// </summary>
    /// <param name="range">The cell range. / 儲存格範圍</param>
    /// <param name="positiveColor">The numeric value. / 正值橫條色彩</param>
    /// <param name="negativeColor">The numeric value. / 負值橫條色彩</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public OdfSheetBuilder AddDataBarFormat(OdfCellRange range, OdfColor positiveColor, OdfColor? negativeColor = null)
    {
        _sheet.AddDataBarFormat(EnsureSheetName(range), positiveColor, negativeColor);
        return this;
    }

    /// <summary>
    /// Adds a color scale conditional format.
    /// 新增色階條件格式。
    /// </summary>
    /// <param name="range">The cell range. / 儲存格範圍</param>
    /// <param name="minColor">The numeric value. / 最小值對應色彩</param>
    /// <param name="maxColor">The numeric value. / 最大值對應色彩</param>
    /// <param name="midColor">The numeric value. / 中間值對應色彩</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public OdfSheetBuilder AddColorScaleFormat(
        OdfCellRange range,
        OdfColor minColor,
        OdfColor maxColor,
        OdfColor? midColor = null)
    {
        _sheet.AddColorScaleFormat(EnsureSheetName(range), minColor, maxColor, midColor);
        return this;
    }

    /// <summary>
    /// Adds an icon set conditional format.
    /// 新增圖示集條件格式。
    /// </summary>
    /// <param name="range">The cell range. / 儲存格範圍</param>
    /// <param name="iconSet">The value to use. / 圖示集類型</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public OdfSheetBuilder AddIconSetFormat(OdfCellRange range, OdfIconSetType iconSet)
    {
        _sheet.AddIconSetFormat(EnsureSheetName(range), iconSet);
        return this;
    }

    /// <summary>
    /// Adds a pivot table.
    /// 新增樞紐分析表。
    /// </summary>
    /// <param name="sourceRange">The cell range. / 來源資料範圍</param>
    /// <param name="targetCell">The cell address. / 輸出起點儲存格</param>
    /// <param name="configure">The delegate to invoke. / 樞紐分析表設定委派</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public OdfSheetBuilder AddPivotTable(
        OdfCellRange sourceRange,
        OdfCellAddress targetCell,
        Action<OdfPivotTableBuilder> configure)
    {
        _sheet.CreatePivotTable(EnsureSheetName(sourceRange), EnsureSheetName(targetCell), configure);
        return this;
    }

    /// <summary>
    /// Adds a pivot table.
    /// 新增樞紐分析表。
    /// </summary>
    /// <param name="name">The name or identifier. / 樞紐分析表名稱</param>
    /// <param name="sourceRange">The cell range. / 來源資料範圍</param>
    /// <param name="targetCell">The cell address. / 輸出起點儲存格</param>
    /// <param name="configure">The delegate to invoke. / 樞紐分析表設定委派</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public OdfSheetBuilder AddPivotTable(
        string name,
        OdfCellRange sourceRange,
        OdfCellAddress targetCell,
        Action<OdfPivotTableBuilder> configure)
    {
        _sheet.CreatePivotTable(name, EnsureSheetName(sourceRange), EnsureSheetName(targetCell), configure);
        return this;
    }

    /// <summary>
    /// Adds a pivot table.
    /// 新增樞紐分析表。
    /// </summary>
    /// <param name="name">The name or identifier. / 樞紐分析表名稱</param>
    /// <param name="sourceRange">The cell range. / 來源資料範圍字串，例如 <c>A1:D20</c></param>
    /// <param name="targetCell">The cell address. / 輸出起點儲存格，例如 <c>G1</c></param>
    /// <param name="configure">The delegate to invoke. / 樞紐分析表設定委派</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public OdfSheetBuilder AddPivotTable(
        string name,
        string sourceRange,
        string targetCell,
        Action<OdfPivotTableBuilder> configure)
        => AddPivotTable(name, ParseRange(sourceRange), ParseAddress(targetCell), configure);

    private static void EnsureOneBasedIndex(int value, string parameterName)
    {
        if (value < 1)
        {
            throw new ArgumentOutOfRangeException(parameterName, OdfLocalizer.GetMessage("Err_SpreadsheetDocumentBuilder_IndexGreaterEqual1"));
        }
    }

    private OdfCellRange EnsureSheetName(OdfCellRange range)
    {
        string? startSheet = range.StartAddress.SheetName ?? _sheet.Name;
        string? endSheet = range.EndAddress.SheetName ?? startSheet;
        return new OdfCellRange(
            new OdfCellAddress(
                range.StartAddress.Row,
                range.StartAddress.Column,
                startSheet,
                range.StartAddress.IsRowAbsolute,
                range.StartAddress.IsColumnAbsolute,
                range.StartAddress.IsSheetAbsolute),
            new OdfCellAddress(
                range.EndAddress.Row,
                range.EndAddress.Column,
                endSheet,
                range.EndAddress.IsRowAbsolute,
                range.EndAddress.IsColumnAbsolute,
                range.EndAddress.IsSheetAbsolute));
    }

    private OdfCellAddress EnsureSheetName(OdfCellAddress address) =>
        address.SheetName is not null
            ? address
            : new OdfCellAddress(
                address.Row,
                address.Column,
                _sheet.Name,
                address.IsRowAbsolute,
                address.IsColumnAbsolute,
                address.IsSheetAbsolute);

    private static OdfCellRange ParseRange(string range)
    {
        if (!OdfCellRange.TryParse(range, out OdfCellRange parsedRange))
        {
            throw new FormatException(OdfLocalizer.GetMessage("Err_OdfTableSheet_InvalidCellRange", range));
        }

        return parsedRange;
    }

    private static OdfCellAddress ParseAddress(string address)
    {
        if (!OdfCellAddress.TryParse(address, out OdfCellAddress parsedAddress))
        {
            throw new FormatException(OdfLocalizer.GetMessage("Err_OdfCellAddress_InvalidAddress", address));
        }

        return parsedAddress;
    }

    private static string ToPointString(double points)
    {
        return points.ToString(CultureInfo.InvariantCulture) + "pt";
    }
}
