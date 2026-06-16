using OdfKit.Styles;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 提供 <see cref="SpreadsheetDocument"/> 的 Fluent 建立 API。
/// </summary>
public sealed class SpreadsheetDocumentBuilder
{
    private readonly SpreadsheetDocument _document;

    internal SpreadsheetDocumentBuilder(SpreadsheetDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>
    /// 新增工作表並設定其內容。
    /// </summary>
    /// <param name="name">工作表名稱。</param>
    /// <param name="configure">工作表設定委派。</param>
    /// <returns>目前 builder 執行個體。</returns>
    public SpreadsheetDocumentBuilder AddSheet(string name, Action<OdfSheetBuilder> configure)
    {
        if (configure is null) throw new ArgumentNullException(nameof(configure));
        OdfTableSheet sheet = _document.Worksheets.Add(name);
        configure(new OdfSheetBuilder(sheet));
        return this;
    }

    /// <summary>
    /// 建立並傳回試算表文件。
    /// </summary>
    /// <returns>建立完成的試算表文件。</returns>
    public SpreadsheetDocument Build()
    {
        return _document;
    }
}

/// <summary>
/// 提供工作表內容的 Fluent 建立 API。
/// </summary>
public sealed class OdfSheetBuilder
{
    private readonly OdfTableSheet _sheet;

    internal OdfSheetBuilder(OdfTableSheet sheet)
    {
        _sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
    }

    /// <summary>
    /// 設定指定儲存格的值。
    /// </summary>
    /// <param name="address">儲存格位址，例如 <c>A1</c>。</param>
    /// <param name="value">儲存格值。</param>
    /// <returns>目前 builder 執行個體。</returns>
    public OdfSheetBuilder SetCell(string address, object? value)
    {
        _sheet.Cells[address].CellValue = value;
        return this;
    }

    /// <summary>
    /// 設定指定儲存格的公式。
    /// </summary>
    /// <param name="address">儲存格位址，例如 <c>A1</c>。</param>
    /// <param name="formula">ODF 公式文字。</param>
    /// <returns>目前 builder 執行個體。</returns>
    public OdfSheetBuilder SetFormula(string address, string formula)
    {
        _sheet.Cells[address].Formula = formula;
        return this;
    }

    /// <summary>
    /// 匯入列資料。
    /// </summary>
    /// <typeparam name="T">資料項目型別。</typeparam>
    /// <param name="items">要匯入的資料項目。</param>
    /// <param name="selector">將資料項目轉為儲存格值陣列的委派。</param>
    /// <param name="startRow">起始列，採 1 為基準。</param>
    /// <param name="startColumn">起始欄，採 1 為基準。</param>
    /// <returns>目前 builder 執行個體。</returns>
    public OdfSheetBuilder ImportRows<T>(
        IEnumerable<T> items,
        Func<T, object?[]> selector,
        int startRow = 1,
        int startColumn = 1)
    {
        if (items is null) throw new ArgumentNullException(nameof(items));
        if (selector is null) throw new ArgumentNullException(nameof(selector));
        EnsureOneBasedIndex(startRow, nameof(startRow));
        EnsureOneBasedIndex(startColumn, nameof(startColumn));

        int rowIndex = startRow - 1;
        int columnOffset = startColumn - 1;
        foreach (T item in items)
        {
            object?[] values = selector(item) ?? throw new InvalidOperationException("資料列選取器不可傳回 null。");
            for (int i = 0; i < values.Length; i++)
            {
                _sheet.Cells[rowIndex, columnOffset + i].CellValue = values[i];
            }

            rowIndex++;
        }

        return this;
    }

    /// <summary>
    /// 匯入含標題列的資料表。
    /// </summary>
    /// <typeparam name="T">資料項目型別。</typeparam>
    /// <param name="items">要匯入的資料項目。</param>
    /// <param name="rowSelector">將資料項目轉為儲存格值陣列的委派。</param>
    /// <param name="headers">標題列文字。</param>
    /// <param name="startRow">標題列起始列，採 1 為基準。</param>
    /// <param name="startColumn">標題列起始欄，採 1 為基準。</param>
    /// <returns>目前 builder 執行個體。</returns>
    public OdfSheetBuilder ImportTable<T>(
        IEnumerable<T> items,
        Func<T, object?[]> rowSelector,
        IEnumerable<string> headers,
        int startRow = 1,
        int startColumn = 1)
    {
        if (headers is null) throw new ArgumentNullException(nameof(headers));
        EnsureOneBasedIndex(startRow, nameof(startRow));
        EnsureOneBasedIndex(startColumn, nameof(startColumn));

        int columnIndex = startColumn - 1;
        foreach (string header in headers)
        {
            _sheet.Cells[startRow - 1, columnIndex].CellValue = header;
            columnIndex++;
        }

        return ImportRows(items, rowSelector, startRow + 1, startColumn);
    }

    /// <summary>
    /// 設定欄寬。
    /// </summary>
    /// <param name="columnIndex">欄索引，採 1 為基準。</param>
    /// <param name="widthCm">欄寬，單位為公分。</param>
    /// <returns>目前 builder 執行個體。</returns>
    public OdfSheetBuilder SetColumnWidth(int columnIndex, double widthCm)
    {
        EnsureOneBasedIndex(columnIndex, nameof(columnIndex));
        if (widthCm <= 0) throw new ArgumentOutOfRangeException(nameof(widthCm));

        _sheet.SetColumnWidth(columnIndex - 1, OdfLength.FromCentimeters(widthCm));
        return this;
    }

    /// <summary>
    /// 凍結指定儲存格上方與左側的窗格。
    /// </summary>
    /// <param name="row">作用儲存格列，採 1 為基準。</param>
    /// <param name="column">作用儲存格欄，採 1 為基準。</param>
    /// <returns>目前 builder 執行個體。</returns>
    public OdfSheetBuilder FreezeAt(int row, int column)
    {
        EnsureOneBasedIndex(row, nameof(row));
        EnsureOneBasedIndex(column, nameof(column));

        _sheet.FreezePanes(row - 1, column - 1);
        return this;
    }

    private static void EnsureOneBasedIndex(int value, string parameterName)
    {
        if (value < 1)
        {
            throw new ArgumentOutOfRangeException(parameterName, "索引必須大於或等於 1。");
        }
    }
}
