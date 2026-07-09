using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OdfKit.Compliance;
using OdfKit.Styles;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Provides object-binding APIs for <see cref="OdfTableSheet"/>.
/// 提供 <see cref="OdfTableSheet"/> 的物件繫結 API。
/// </summary>
public partial class OdfTableSheet
{
    /// <summary>
    /// Convenience overload that uses default values for remaining parameters.
    /// 便利多載：其餘參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public OdfObjectBindingReport WriteObjects<T>(OdfCellAddress startAddress, IEnumerable<T> items) => WriteObjects(startAddress, items, null);

    /// <summary>
    /// Writes public readable object properties into this worksheet.
    /// 將物件的可讀公開屬性寫入此工作表。
    /// </summary>
    /// <typeparam name="T">The object type to write. / 要寫入的物件型別。</typeparam>
    /// <param name="startAddress">The top-left target cell. / 左上角目標儲存格。</param>
    /// <param name="items">The object sequence to write. / 要寫入的物件序列。</param>
    /// <param name="options">The object binding options. / 物件繫結選項。</param>
    /// <returns>The object binding report. / 物件繫結報告。</returns>
    public OdfObjectBindingReport WriteObjects<T>(OdfCellAddress startAddress, IEnumerable<T> items, OdfObjectBindingOptions? options)
    {
        if (items is null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        options ??= new OdfObjectBindingOptions();
        OdfCellAddress effectiveStart = options.StartColumn > 0
            ? new OdfCellAddress(startAddress.Row, options.StartColumn, Name)
            : new OdfCellAddress(startAddress.Row, startAddress.Column, Name);
        IReadOnlyList<OdfObjectColumn> columns = OdfObjectBindingEngine.GetReadableColumns<T>(options);
        List<List<object?>> rows = BuildRows(items, columns, options);
        OdfRangeWriteReport rangeReport = SetValues(effectiveStart, rows, OdfRangeWriteOptions.Default);
        var report = CreateReport(rangeReport.Range, Math.Max(0, rows.Count - (options.IncludeHeader ? 1 : 0)), columns);
        ApplyColumnFormats(rangeReport.Range, columns, options);
        CreateTableIfRequested(options, rangeReport.Range);
        return report;
    }

    /// <summary>
    /// Convenience overload that uses default values for remaining parameters.
    /// 便利多載：其餘參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public OdfObjectBindingReport AppendObjects<T>(IEnumerable<T> items) => AppendObjects(items, null);


    /// <summary>
    /// Appends public readable object properties after the worksheet used range.
    /// 將物件的可讀公開屬性附加到工作表已使用範圍之後。
    /// </summary>
    /// <typeparam name="T">The object type to append. / 要附加的物件型別。</typeparam>
    /// <param name="items">The object sequence to append. / 要附加的物件序列。</param>
    /// <param name="options">The object binding options. / 物件繫結選項。</param>
    /// <returns>The object binding report. / 物件繫結報告。</returns>
    public OdfObjectBindingReport AppendObjects<T>(IEnumerable<T> items, OdfObjectBindingOptions? options)
    {
        if (items is null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        options ??= new OdfObjectBindingOptions();
        IReadOnlyList<OdfObjectColumn> columns = OdfObjectBindingEngine.GetReadableColumns<T>(options);
        List<List<object?>> rows = BuildRows(items, columns, options);
        OdfRangeWriteReport rangeReport = AppendRows(rows, options.StartColumn, OdfRangeWriteOptions.Default);
        var report = CreateReport(rangeReport.Range, Math.Max(0, rows.Count - (options.IncludeHeader ? 1 : 0)), columns);
        ApplyColumnFormats(rangeReport.Range, columns, options);
        CreateTableIfRequested(options, rangeReport.Range);
        return report;
    }

    /// <summary>
    /// Convenience overload that uses default values for remaining parameters.
    /// 便利多載：其餘參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public IReadOnlyList<T> ReadObjects<T>(OdfCellRange range) where T : new() => ReadObjects<T>(range, null);


    /// <summary>
    /// Reads worksheet rows into objects using the header row as the property map.
    /// 使用標題列作為屬性對應，將工作表資料列讀成物件。
    /// </summary>
    /// <typeparam name="T">The object type to create. / 要建立的物件型別。</typeparam>
    /// <param name="range">The source cell range. / 來源儲存格範圍。</param>
    /// <param name="options">The object read options. / 物件讀取選項。</param>
    /// <returns>The materialized object list. / 具體化後的物件清單。</returns>
    public IReadOnlyList<T> ReadObjects<T>(OdfCellRange range, OdfObjectReadOptions? options) where T : new()
    {
        options ??= new OdfObjectReadOptions();
        CultureInfo culture = options.CultureInfo ?? CultureInfo.InvariantCulture;
        IReadOnlyList<OdfObjectColumn> columns = OdfObjectBindingEngine.GetWritableColumns<T>(options);
        int startRow = Math.Min(range.StartAddress.Row, range.EndAddress.Row);
        int endRow = Math.Max(range.StartAddress.Row, range.EndAddress.Row);
        int startColumn = Math.Min(range.StartAddress.Column, range.EndAddress.Column);
        int endColumn = Math.Max(range.StartAddress.Column, range.EndAddress.Column);
        int headerRow = startRow + options.HeaderRow;
        int dataStartRow = startRow + options.DataStartRow;
        HeaderScan headerScan = ReadHeaderScan(headerRow, startColumn, endColumn);
        var headerDiagnostics = new List<OdfObjectBindingDiagnostic>();
        Dictionary<string, int> headers = BuildHeaderMap(headerScan, options, headerDiagnostics);
        foreach (OdfObjectBindingDiagnostic diagnostic in headerDiagnostics)
        {
            options.Report?.Diagnostics.Add(diagnostic);
            if (diagnostic.Code == "ODSOBJ0005" &&
                options.DuplicateHeaderPolicy == OdfObjectDuplicateHeaderPolicy.Throw)
            {
                throw new InvalidOperationException(OdfLocalizer.GetMessage(
                    "Err_OdfObjectBinding_DuplicateHeader",
                    diagnostic.RawValue ?? string.Empty));
            }
        }

        List<(OdfObjectColumn Column, int Index)> mappedColumns = MapColumns(columns, headers, options);
        var results = new List<T>();

        for (int row = dataStartRow; row <= endRow; row++)
        {
            if (options.StopAtFirstEmptyRow && IsEmptyRow(row, mappedColumns, startColumn))
            {
                break;
            }

            var item = new T();
            bool skipRow = false;
            foreach ((OdfObjectColumn column, int index) in mappedColumns)
            {
                OdfCell cell = GetCell(row, startColumn + index);
                object? value = ResolveReadValue(cell, column);
                try
                {
                    object? converted = OdfObjectBindingEngine.ConvertReadValue(value, column.Property.PropertyType, culture);
                    column.Property.SetValue(item, converted);
                }
                catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException or ArgumentException)
                {
                    if (HandleConversionError(options, row, startColumn + index, column, value))
                    {
                        skipRow = true;
                        break;
                    }
                }
            }

            if (skipRow)
            {
                continue;
            }

            results.Add(item);
        }

        OdfObjectBindingReport? report = options.Report;
        if (report is not null)
        {
            report.Range = range;
            report.RowCount = results.Count;
            report.ColumnCount = mappedColumns.Count;
            foreach (OdfObjectColumn column in columns)
            {
                report.ColumnNames.Add(column.Header);
            }
        }

        return results;
    }

    /// <summary>
    /// Convenience overload that uses default values for remaining parameters.
    /// 便利多載：其餘參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public OdfObjectBindingValidationReport ValidateObjectBinding<T>(OdfCellRange range) where T : new() => ValidateObjectBinding<T>(range, null);


    /// <summary>
    /// Validates whether a spreadsheet range can be bound to the specified object type.
    /// 驗證試算表範圍是否可繫結至指定物件型別。
    /// </summary>
    /// <typeparam name="T">The object type to validate. / 要驗證的物件型別。</typeparam>
    /// <param name="range">The source cell range. / 來源儲存格範圍。</param>
    /// <param name="options">The validation options. / 驗證選項。</param>
    /// <returns>The validation report. / 驗證報告。</returns>
    public OdfObjectBindingValidationReport ValidateObjectBinding<T>(OdfCellRange range, OdfObjectReadOptions? options) where T : new()
    {
        options ??= new OdfObjectReadOptions();
        IReadOnlyList<OdfObjectColumn> columns = OdfObjectBindingEngine.GetWritableColumns<T>(options);
        RangeLayout layout = GetRangeLayout(range, options.HeaderRow, options.DataStartRow);
        HeaderScan headerScan = ReadHeaderScan(layout.HeaderRow, layout.StartColumn, layout.EndColumn);
        var report = new OdfObjectBindingValidationReport { Range = range };
        Dictionary<string, int> headers = BuildHeaderMap(headerScan, options, report.Diagnostics);
        List<(OdfObjectColumn Column, int Index)> mappedColumns = MapColumns(columns, headers, options);

        foreach (OdfObjectColumn column in columns)
        {
            if (column.RequiredColumn && !mappedColumns.Any(item => item.Column.Property.Name == column.Property.Name))
            {
                report.Diagnostics.Add(CreateDiagnostic(
                    OdfIssueSeverity.Error,
                    "Warn_OdfObjectBinding_MissingColumn",
                    "ODSOBJ0001",
                    -1,
                    -1,
                    column,
                    null,
                    null));
            }
        }

        RecordUnknownColumns(headerScan, mappedColumns, options, report.Diagnostics);
        ValidateDataRows(layout, mappedColumns, options, report.Diagnostics);
        return report;
    }


    /// <summary>
    /// Updates existing object-bound rows by key without inserting new rows.
    /// 依 key 更新既有物件繫結資料列且不新增資料列。
    /// </summary>
    /// <typeparam name="T">The object type to update. / 要更新的物件型別。</typeparam>
    /// <param name="range">The target table range. / 目標資料表範圍。</param>
    /// <param name="items">The object sequence to update. / 要更新的物件序列。</param>
    /// <param name="options">The update options. / 更新選項。</param>
    /// <returns>The object binding report. / 物件繫結報告。</returns>
    public OdfObjectBindingReport UpdateObjects<T>(
        OdfCellRange range,
        IEnumerable<T> items,
        OdfObjectUpdateOptions options) =>
        UpdateOrUpsertObjects(range, items, options, insertMissing: false);

    /// <summary>
    /// Updates object-bound rows by key and inserts rows for missing keys.
    /// 依 key 更新物件繫結資料列，並針對缺少的 key 新增資料列。
    /// </summary>
    /// <typeparam name="T">The object type to upsert. / 要 upsert 的物件型別。</typeparam>
    /// <param name="range">The target table range. / 目標資料表範圍。</param>
    /// <param name="items">The object sequence to upsert. / 要 upsert 的物件序列。</param>
    /// <param name="options">The update options. / 更新選項。</param>
    /// <returns>The object binding report. / 物件繫結報告。</returns>
    public OdfObjectBindingReport UpsertObjects<T>(
        OdfCellRange range,
        IEnumerable<T> items,
        OdfObjectUpdateOptions options) =>
        UpdateOrUpsertObjects(range, items, options, insertMissing: true);

    private static List<List<object?>> BuildRows<T>(
        IEnumerable<T> items,
        IReadOnlyList<OdfObjectColumn> columns,
        OdfObjectBindingOptions options)
    {
        var rows = new List<List<object?>>();
        if (options.IncludeHeader)
        {
            rows.Add(columns.Select(column => (object?)column.Header).ToList());
        }

        foreach (T item in items)
        {
            rows.Add(columns.Select(column =>
                OdfObjectBindingEngine.NormalizeWriteValue(column.Property.GetValue(item), options)).ToList());
        }

        return rows;
    }

    private static OdfObjectBindingReport CreateReport(
        OdfCellRange range,
        int rowCount,
        IReadOnlyList<OdfObjectColumn> columns)
    {
        var report = new OdfObjectBindingReport
        {
            Range = range,
            RowCount = rowCount,
            ColumnCount = columns.Count
        };

        foreach (OdfObjectColumn column in columns)
        {
            report.ColumnNames.Add(column.Header);
        }

        return report;
    }

    private Dictionary<string, int> ReadHeaderMap(int headerRow, int startColumn, int endColumn)
    {
        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int column = startColumn; column <= endColumn; column++)
        {
            string header = GetCell(headerRow, column).DisplayText;
            if (!string.IsNullOrWhiteSpace(header) && !headers.ContainsKey(header))
            {
                headers.Add(header, column - startColumn);
            }
        }

        return headers;
    }

    private static List<(OdfObjectColumn Column, int Index)> MapColumns(
        IReadOnlyList<OdfObjectColumn> columns,
        IReadOnlyDictionary<string, int> headers,
        OdfObjectReadOptions options)
    {
        var mappedColumns = new List<(OdfObjectColumn Column, int Index)>();
        foreach (OdfObjectColumn column in columns)
        {
            if (TryResolveHeader(headers, column, out int index))
            {
                mappedColumns.Add((column, index));
                continue;
            }

            options.Report?.SkippedColumns.Add(column.Header);
            if (options.MissingColumnPolicy == OdfObjectMissingColumnPolicy.Warn)
            {
                options.Report?.Warnings.Add("ODSOBJ0001:" + column.Header);
                options.Report?.Diagnostics.Add(new OdfObjectBindingDiagnostic(
                    OdfIssueSeverity.Warning,
                    "Warn_OdfObjectBinding_MissingColumn",
                    -1,
                    -1,
                    column.Property.Name,
                    null,
                    "ODSOBJ0001"));
            }
        }

        return mappedColumns;
    }

    private static bool TryResolveHeader(
        IReadOnlyDictionary<string, int> headers,
        OdfObjectColumn column,
        out int index)
    {
        if (headers.TryGetValue(column.Header, out index))
        {
            return true;
        }

        foreach (string alias in column.Aliases)
        {
            if (headers.TryGetValue(alias, out index))
            {
                return true;
            }
        }

        index = -1;
        return false;
    }

    private static bool HandleConversionError(
        OdfObjectReadOptions options,
        int row,
        int columnIndex,
        OdfObjectColumn column,
        object? value)
    {
        if (options.ConversionErrorPolicy == OdfObjectConversionErrorPolicy.Throw)
        {
            throw new FormatException(OdfLocalizer.GetMessage(
                "Err_OdfObjectBinding_ConversionFailed",
                column.Property.Name,
                value?.ToString() ?? string.Empty));
        }

        options.Report?.Warnings.Add("ODSOBJ0002:" + column.Property.Name);
        options.Report?.Diagnostics.Add(new OdfObjectBindingDiagnostic(
            OdfIssueSeverity.Warning,
            "Warn_OdfObjectBinding_ConversionFailed",
            row,
            columnIndex,
            column.Property.Name,
            value?.ToString(),
            "ODSOBJ0002",
            new OdfCellAddress(row, columnIndex).ToString(),
            column.Property.PropertyType.FullName,
            value?.GetType().FullName));
        return options.ConversionErrorPolicy == OdfObjectConversionErrorPolicy.WarnAndSkipRow;
    }

    private bool IsEmptyRow(int row, IReadOnlyList<(OdfObjectColumn Column, int Index)> mappedColumns, int startColumn)
    {
        if (mappedColumns.Count == 0)
        {
            return true;
        }

        foreach ((_, int index) in mappedColumns)
        {
            OdfCell cell = GetCell(row, startColumn + index);
            if (!string.IsNullOrWhiteSpace(cell.DisplayText) || cell.CellValue is not null)
            {
                return false;
            }
        }

        return true;
    }

    private void CreateTableIfRequested(OdfObjectBindingOptions options, OdfCellRange range)
    {
        string? tableName = options.CreateTableName;
        if (!string.IsNullOrWhiteSpace(tableName))
        {
            Document.CreateTable(tableName!, range, new OdfSpreadsheetTableOptions
            {
                FirstRowAsHeader = options.IncludeHeader,
                DisplayFilterButtons = options.IncludeHeader
            });
        }
    }

    private OdfObjectBindingReport UpdateOrUpsertObjects<T>(
        OdfCellRange range,
        IEnumerable<T> items,
        OdfObjectUpdateOptions options,
        bool insertMissing)
    {
        if (items is null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.KeyColumn))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfSpreadsheetTable_ColumnNotFound", string.Empty), nameof(options));
        }

        IReadOnlyList<OdfObjectColumn> columns = OdfObjectBindingEngine.GetReadableColumns<T>(options);
        RangeLayout layout = GetRangeLayout(range, headerRowOffset: 0, dataStartRowOffset: 1);
        HeaderScan headerScan = ReadHeaderScan(layout.HeaderRow, layout.StartColumn, layout.EndColumn);
        var headerDiagnostics = new List<OdfObjectBindingDiagnostic>();
        Dictionary<string, int> headers = BuildHeaderMap(headerScan, new OdfObjectReadOptions
        {
            DuplicateHeaderPolicy = OdfObjectDuplicateHeaderPolicy.Throw
        }, headerDiagnostics);
        if (headerDiagnostics.Any(diagnostic => diagnostic.Code == "ODSOBJ0005"))
        {
            throw new InvalidOperationException(OdfLocalizer.GetMessage(
                "Err_OdfObjectBinding_DuplicateHeader",
                options.KeyColumn ?? string.Empty));
        }

        List<(OdfObjectColumn Column, int Index)> mappedColumns = MapWritableColumns(columns, headers);
        OdfObjectColumn keyColumn = ResolveObjectKeyColumn(columns, options.KeyColumn!);
        int keyColumnIndex = ResolveHeaderKeyIndex(headers, columns, options.KeyColumn!);
        Dictionary<string, int> existingRows = BuildExistingKeyRows(layout, keyColumnIndex, options);
        var report = CreateReport(range, 0, columns);
        int lastRow = layout.EndRow;

        foreach (T item in items)
        {
            string key = ReadItemKey(item, keyColumn, options);
            if (string.IsNullOrWhiteSpace(key))
            {
                if (options.MissingKeyPolicy == OdfObjectMissingKeyPolicy.WarnAndSkip)
                {
                    report.SkippedRowCount++;
                    report.Diagnostics.Add(CreateDiagnostic(
                        OdfIssueSeverity.Warning,
                        "Warn_OdfObjectBinding_MissingKeySkipped",
                        "ODSOBJ0101",
                        -1,
                        -1,
                        keyColumn,
                        null,
                        null));
                    continue;
                }

                throw new FormatException(OdfLocalizer.GetMessage("Err_OdfObjectBinding_MissingKey", options.KeyColumn));
            }

            if (existingRows.TryGetValue(key, out int row))
            {
                WriteObjectToRow(item, row, layout.StartColumn, mappedColumns, options);
                report.UpdatedRowCount++;
                continue;
            }

            if (!insertMissing)
            {
                report.SkippedRowCount++;
                continue;
            }

            lastRow++;
            CopyTemplateRow(layout.EndRow, lastRow, layout.StartColumn, layout.EndColumn, mappedColumns, options, report);
            WriteObjectToRow(item, lastRow, layout.StartColumn, mappedColumns, options);
            existingRows[key] = lastRow;
            report.InsertedRowCount++;
        }

        report.RowCount = report.UpdatedRowCount + report.InsertedRowCount;
        report.Range = new OdfCellRange(layout.StartRow, layout.StartColumn, lastRow, layout.EndColumn, Name);
        if (insertMissing && options.ResizeTable && report.InsertedRowCount > 0)
        {
            ResizeMatchingTable(range, report.Range);
        }

        return report;
    }

    private static List<(OdfObjectColumn Column, int Index)> MapWritableColumns(
        IReadOnlyList<OdfObjectColumn> columns,
        IReadOnlyDictionary<string, int> headers)
    {
        var mappedColumns = new List<(OdfObjectColumn Column, int Index)>();
        foreach (OdfObjectColumn column in columns)
        {
            if (TryResolveHeader(headers, column, out int index))
            {
                mappedColumns.Add((column, index));
            }
        }

        return mappedColumns;
    }

    private Dictionary<string, int> BuildExistingKeyRows(
        RangeLayout layout,
        int keyColumnIndex,
        OdfObjectUpdateOptions options)
    {
        var rows = new Dictionary<string, int>(options.KeyComparer);
        for (int row = layout.DataStartRow; row <= layout.EndRow; row++)
        {
            string key = GetCell(row, layout.StartColumn + keyColumnIndex).DisplayText;
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            if (rows.ContainsKey(key))
            {
                throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfObjectBinding_DuplicateKey", key));
            }

            rows.Add(key, row);
        }

        return rows;
    }

    private static OdfObjectColumn ResolveObjectKeyColumn(IReadOnlyList<OdfObjectColumn> columns, string keyColumn)
    {
        foreach (OdfObjectColumn column in columns)
        {
            if (string.Equals(column.Property.Name, keyColumn, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(column.Header, keyColumn, StringComparison.OrdinalIgnoreCase))
            {
                return column;
            }
        }

        throw new KeyNotFoundException(OdfLocalizer.GetMessage("Err_OdfSpreadsheetTable_ColumnNotFound", keyColumn));
    }

    private static int ResolveHeaderKeyIndex(
        IReadOnlyDictionary<string, int> headers,
        IReadOnlyList<OdfObjectColumn> columns,
        string keyColumn)
    {
        if (headers.TryGetValue(keyColumn, out int index))
        {
            return index;
        }

        OdfObjectColumn column = ResolveObjectKeyColumn(columns, keyColumn);
        if (TryResolveHeader(headers, column, out index))
        {
            return index;
        }

        throw new KeyNotFoundException(OdfLocalizer.GetMessage("Err_OdfSpreadsheetTable_ColumnNotFound", keyColumn));
    }

    private static string ReadItemKey<T>(T item, OdfObjectColumn keyColumn, OdfObjectUpdateOptions options)
    {
        object? value = keyColumn.Property.GetValue(item);
        object? normalized = OdfObjectBindingEngine.NormalizeWriteValue(value, options);
        return normalized?.ToString() ?? string.Empty;
    }

    private void WriteObjectToRow<T>(
        T item,
        int row,
        int startColumn,
        IReadOnlyList<(OdfObjectColumn Column, int Index)> mappedColumns,
        OdfObjectUpdateOptions options)
    {
        foreach ((OdfObjectColumn column, int index) in mappedColumns)
        {
            OdfCell cell = GetCell(row, startColumn + index);
            string? styleName = cell.StyleName;
            object? value = column.Property.GetValue(item);
            object? normalized = OdfObjectBindingEngine.NormalizeWriteValue(value, options);
            cell.CellValue = normalized;
            if (options.PreserveDataStyles)
            {
                cell.StyleName = styleName;
            }
        }
    }

    private void CopyTemplateRow(
        int templateRow,
        int targetRow,
        int startColumn,
        int endColumn,
        IReadOnlyList<(OdfObjectColumn Column, int Index)> mappedColumns,
        OdfObjectUpdateOptions options,
        OdfObjectBindingReport report)
    {
        var mappedIndexes = new HashSet<int>(mappedColumns.Select(item => item.Index));
        for (int column = startColumn; column <= endColumn; column++)
        {
            int index = column - startColumn;
            bool mapped = mappedIndexes.Contains(index);
            OdfCell source = GetCell(templateRow, column);
            OdfCell target = GetCell(targetRow, column);
            if (options.CopyStylesFromTemplateRow)
            {
                target.StyleName = source.StyleName;
            }

            if (mapped)
            {
                continue;
            }

            if (options.FillFormulasFromTemplateRow && !string.IsNullOrEmpty(source.Formula))
            {
                string copiedFormula = options.FormulaCopyMode switch
                {
                    OdfFormulaCopyMode.Clear => string.Empty,
                    OdfFormulaCopyMode.ShiftRelativeReferences => OdfSpreadsheetFormulaReferenceShifter.ShiftRelativeRows(
                        source.Formula,
                        targetRow - templateRow),
                    _ => source.Formula
                };

                if (string.IsNullOrEmpty(copiedFormula))
                {
                    target.Formula = string.Empty;
                    continue;
                }

                target.Formula = copiedFormula;
                report.Diagnostics.Add(new OdfObjectBindingDiagnostic(
                    OdfIssueSeverity.Warning,
                    "Warn_OdfObjectBinding_FormulaCopied",
                    targetRow,
                    column,
                    string.Empty,
                    copiedFormula,
                    "ODSOBJ0200",
                    new OdfCellAddress(targetRow, column, Name).ToString(),
                    null,
                    null));
            }
        }
    }

    private void ResizeMatchingTable(OdfCellRange originalRange, OdfCellRange newRange)
    {
        foreach (OdfSpreadsheetTableInfo table in Document.GetTables())
        {
            if (OdfCellRange.TryParse(table.TargetRangeAddress, out OdfCellRange tableRange) &&
                tableRange.Equals(originalRange))
            {
                Document.ResizeTable(table.Name, newRange);
                return;
            }
        }
    }

    private void ValidateDataRows(
        RangeLayout layout,
        IReadOnlyList<(OdfObjectColumn Column, int Index)> mappedColumns,
        OdfObjectReadOptions options,
        IList<OdfObjectBindingDiagnostic> diagnostics)
    {
        CultureInfo culture = options.CultureInfo ?? CultureInfo.InvariantCulture;
        for (int row = layout.DataStartRow; row <= layout.EndRow; row++)
        {
            foreach ((OdfObjectColumn column, int index) in mappedColumns)
            {
                int absoluteColumn = layout.StartColumn + index;
                OdfCell cell = GetCell(row, absoluteColumn);
                object? value = ResolveReadValue(cell, column);
                bool blank = IsBlankValue(value);
                if (blank && column.RequiredValue)
                {
                    diagnostics.Add(CreateDiagnostic(
                        OdfIssueSeverity.Error,
                        "Warn_OdfObjectBinding_RequiredValueMissing",
                        "ODSOBJ0003",
                        row,
                        absoluteColumn,
                        column,
                        value,
                        column.Property.PropertyType));
                    continue;
                }

                if (blank)
                {
                    continue;
                }

                try
                {
                    _ = OdfObjectBindingEngine.ConvertReadValue(value, column.Property.PropertyType, culture);
                }
                catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException or ArgumentException)
                {
                    diagnostics.Add(CreateDiagnostic(
                        OdfIssueSeverity.Warning,
                        "Warn_OdfObjectBinding_ConversionFailed",
                        "ODSOBJ0002",
                        row,
                        absoluteColumn,
                        column,
                        value,
                        column.Property.PropertyType));
                }
            }
        }
    }

    private static void RecordUnknownColumns(
        HeaderScan headerScan,
        IReadOnlyList<(OdfObjectColumn Column, int Index)> mappedColumns,
        OdfObjectReadOptions options,
        IList<OdfObjectBindingDiagnostic> diagnostics)
    {
        if (options.UnknownColumnPolicy != OdfObjectUnknownColumnPolicy.Warn)
        {
            return;
        }

        var mappedIndexes = new HashSet<int>(mappedColumns.Select(item => item.Index));
        foreach ((string header, int index) in headerScan.Headers)
        {
            if (string.IsNullOrWhiteSpace(header) || mappedIndexes.Contains(index))
            {
                continue;
            }

            diagnostics.Add(new OdfObjectBindingDiagnostic(
                OdfIssueSeverity.Warning,
                "Warn_OdfObjectBinding_UnknownColumn",
                headerScan.HeaderRow,
                headerScan.StartColumn + index,
                header,
                header,
                "ODSOBJ0004",
                null,
                null,
                null));
        }
    }

    private static Dictionary<string, int> BuildHeaderMap(
        HeaderScan headerScan,
        OdfObjectReadOptions options,
        IList<OdfObjectBindingDiagnostic> diagnostics)
    {
        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach ((string header, int index) in headerScan.Headers)
        {
            if (string.IsNullOrWhiteSpace(header))
            {
                continue;
            }

            if (headers.ContainsKey(header))
            {
                diagnostics.Add(new OdfObjectBindingDiagnostic(
                    options.DuplicateHeaderPolicy == OdfObjectDuplicateHeaderPolicy.Throw
                        ? OdfIssueSeverity.Error
                        : OdfIssueSeverity.Warning,
                    "Warn_OdfObjectBinding_DuplicateHeader",
                    headerScan.HeaderRow,
                    headerScan.StartColumn + index,
                    header,
                    header,
                    "ODSOBJ0005",
                    null,
                    null,
                    null));
                continue;
            }

            headers.Add(header, index);
        }

        return headers;
    }

    private HeaderScan ReadHeaderScan(int headerRow, int startColumn, int endColumn)
    {
        var headers = new List<(string Header, int Index)>();
        for (int column = startColumn; column <= endColumn; column++)
        {
            headers.Add((GetCell(headerRow, column).DisplayText, column - startColumn));
        }

        return new HeaderScan(headerRow, startColumn, headers);
    }

    private static object? ResolveReadValue(OdfCell cell, OdfObjectColumn column)
    {
        object? value = cell.CellValue ?? cell.DisplayText;
        if (!IsBlankValue(value))
        {
            return value;
        }

        if (column.DefaultValueFactory is not null)
        {
            return column.DefaultValueFactory();
        }

        return column.DefaultValue;
    }

    private static RangeLayout GetRangeLayout(
        OdfCellRange range,
        int headerRowOffset,
        int dataStartRowOffset)
    {
        int startRow = Math.Min(range.StartAddress.Row, range.EndAddress.Row);
        int endRow = Math.Max(range.StartAddress.Row, range.EndAddress.Row);
        int startColumn = Math.Min(range.StartAddress.Column, range.EndAddress.Column);
        int endColumn = Math.Max(range.StartAddress.Column, range.EndAddress.Column);
        return new RangeLayout(
            startRow,
            endRow,
            startColumn,
            endColumn,
            startRow + headerRowOffset,
            startRow + dataStartRowOffset);
    }

    private OdfObjectBindingDiagnostic CreateDiagnostic(
        OdfIssueSeverity severity,
        string messageKey,
        string code,
        int row,
        int column,
        OdfObjectColumn columnInfo,
        object? rawValue,
        Type? expectedType) =>
        new(
            severity,
            messageKey,
            row,
            column,
            columnInfo.Property.Name,
            rawValue?.ToString(),
            code,
            row >= 0 && column >= 0 ? new OdfCellAddress(row, column, Name).ToString() : null,
            expectedType?.FullName,
            rawValue?.GetType().FullName);

    private static bool IsBlankValue(object? value) =>
        value is null || (value is string text && string.IsNullOrWhiteSpace(text));

    private readonly record struct RangeLayout(
        int StartRow,
        int EndRow,
        int StartColumn,
        int EndColumn,
        int HeaderRow,
        int DataStartRow);

    private sealed class HeaderScan(int headerRow, int startColumn, IReadOnlyList<(string Header, int Index)> headers)
    {
        internal int HeaderRow { get; } = headerRow;

        internal int StartColumn { get; } = startColumn;

        internal IReadOnlyList<(string Header, int Index)> Headers { get; } = headers;
    }

    private void ApplyColumnFormats(
        OdfCellRange range,
        IReadOnlyList<OdfObjectColumn> columns,
        OdfObjectBindingOptions options)
    {
        int startRow = Math.Min(range.StartAddress.Row, range.EndAddress.Row);
        int endRow = Math.Max(range.StartAddress.Row, range.EndAddress.Row);
        int startColumn = Math.Min(range.StartAddress.Column, range.EndAddress.Column);
        var formatter = new OdfNumberFormatter(Document.ContentDom, Document.StylesDom);

        for (int columnOffset = 0; columnOffset < columns.Count; columnOffset++)
        {
            OdfObjectColumnFormat? format = columns[columnOffset].Format;
            if (format is null)
            {
                continue;
            }

            int absoluteColumn = startColumn + columnOffset;
            if (format.Width.HasValue)
            {
                SetColumnWidth(absoluteColumn, format.Width.Value);
            }

            int dataStartRow = options.IncludeHeader ? startRow + 1 : startRow;
            if (options.IncludeHeader && !string.IsNullOrEmpty(format.HeaderStyleName))
            {
                GetCell(startRow, absoluteColumn).StyleName = format.HeaderStyleName;
            }

            string? numberStyleName = string.IsNullOrWhiteSpace(format.NumberFormat)
                ? null
                : formatter.GetOrCreateNumberStyle(format.NumberFormat!, options.CultureInfo);
            for (int row = dataStartRow; row <= endRow; row++)
            {
                OdfCell cell = GetCell(row, absoluteColumn);
                if (!string.IsNullOrEmpty(format.StyleName))
                {
                    cell.StyleName = format.StyleName;
                }

                if (!string.IsNullOrEmpty(numberStyleName))
                {
                    cell.Style.NumberFormat = numberStyleName;
                }
            }

            if (format.AutoFit)
            {
                AutoFitColumnWidth(absoluteColumn);
            }
        }
    }
}
