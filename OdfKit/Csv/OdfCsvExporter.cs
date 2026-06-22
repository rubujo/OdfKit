using System.Globalization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Spreadsheet;
using Sylvan.Data.Csv;

using OdfKit.Compliance;
namespace OdfKit.Csv;

/// <summary>
/// 將 SpreadsheetDocument 的工作表匯出為 CSV 的靜態工具類別。
/// </summary>
public static class OdfCsvExporter
{
    /// <summary>
    /// 將指定工作表的資料匯出為 CSV 並寫入資料流。
    /// </summary>
    /// <param name="workbook">來源 SpreadsheetDocument 執行個體</param>
    /// <param name="csvStream">要寫入的目標資料流（不得為 null）</param>
    /// <param name="options">CSV 選項；若為 null 則使用預設值</param>
    /// <exception cref="ArgumentNullException">當任一必要參數為 null 時引發</exception>
    /// <exception cref="ArgumentOutOfRangeException">當 ExportSheetIndex 超出範圍時引發</exception>
    public static void ExportToStream(SpreadsheetDocument workbook, Stream csvStream, OdfCsvOptions? options = null)
    {
        if (workbook is null)
            throw new ArgumentNullException(nameof(workbook));
        if (csvStream is null)
            throw new ArgumentNullException(nameof(csvStream));
        options ??= new OdfCsvOptions();

        if (options.ExportSheetIndex < 0 || options.ExportSheetIndex >= workbook.Worksheets.Count)
            throw new ArgumentOutOfRangeException(nameof(options), OdfLocalizer.GetMessage("Err_OdfCsvExporter_ExportsheetindexExceedsWorksheetRange"));

        var sheet = workbook.Worksheets[options.ExportSheetIndex];

        // 掃描已使用的維度
        var cellValues = ScanCellValues(sheet);
        int maxRow = 0, maxCol = 0;
        foreach (var pair in cellValues)
        {
            var val = pair.Value;
            if (val is not null && !(val is string s && s.Length == 0))
            {
                if (pair.Key.Row > maxRow)
                    maxRow = pair.Key.Row;
                if (pair.Key.Col > maxCol)
                    maxCol = pair.Key.Col;
            }
        }

        using var textWriter = new StreamWriter(csvStream, options.Encoding, 4096, leaveOpen: true);
        var csvOptions = new CsvDataWriterOptions { Delimiter = options.Delimiter, WriteHeaders = false };
        using var csvWriter = CsvDataWriter.Create(textWriter, csvOptions);

        using var reader = new OdfTableSheetDataReader(sheet, cellValues, maxRow, maxCol);
        csvWriter.Write(reader);
    }

    /// <summary>
    /// 將指定工作表的資料匯出為 CSV 檔案。
    /// </summary>
    /// <param name="workbook">來源 SpreadsheetDocument 執行個體</param>
    /// <param name="csvPath">目標 CSV 檔案路徑</param>
    /// <param name="options">CSV 選項；若為 null 則使用預設值</param>
    public static void ExportToFile(SpreadsheetDocument workbook, string csvPath, OdfCsvOptions? options = null)
    {
        if (csvPath is null)
            throw new ArgumentNullException(nameof(csvPath));
        using var stream = File.Create(csvPath);
        ExportToStream(workbook, stream, options);
    }

    private static Dictionary<(int Row, int Col), object> ScanCellValues(OdfTableSheet sheet)
    {
        var values = new Dictionary<(int Row, int Col), object>();
        int currentRowIndex = 0;
        foreach (var rowChild in sheet.TableNode.Children)
        {
            if (rowChild.LocalName == "table-row" && rowChild.NamespaceUri == OdfNamespaces.Table)
            {
                int rowRepeatedCount = 1;
                string? rowRepStr = rowChild.GetAttribute("number-rows-repeated", OdfNamespaces.Table);
                if (!string.IsNullOrEmpty(rowRepStr) && int.TryParse(rowRepStr, out int rrc) && rrc > 0)
                {
                    rowRepeatedCount = Math.Min(rrc, OdfSpreadsheetLimits.CsvMaxRepeat);
                }

                var rowCells = new List<(int Col, object Val)>();
                int currentColIndex = 0;
                foreach (var cellChild in rowChild.Children)
                {
                    if ((cellChild.LocalName == "table-cell" || cellChild.LocalName == "covered-table-cell") && cellChild.NamespaceUri == OdfNamespaces.Table)
                    {
                        int colRepeatedCount = 1;
                        string? colRepStr = cellChild.GetAttribute("number-columns-repeated", OdfNamespaces.Table);
                        if (!string.IsNullOrEmpty(colRepStr) && int.TryParse(colRepStr, out int crc) && crc > 0)
                        {
                            colRepeatedCount = Math.Min(crc, OdfSpreadsheetLimits.CsvMaxRepeat);
                        }

                        object? cellValue = GetCellValueFromNode(cellChild);
                        if (cellValue is not null)
                        {
                            for (int i = 0; i < colRepeatedCount; i++)
                            {
                                rowCells.Add((currentColIndex + i, cellValue));
                            }
                        }

                        currentColIndex += colRepeatedCount;
                    }
                }

                for (int r = 0; r < rowRepeatedCount; r++)
                {
                    int actualRow = currentRowIndex + r;
                    foreach (var cell in rowCells)
                    {
                        values[(actualRow, cell.Col)] = cell.Val;
                    }
                }

                currentRowIndex += rowRepeatedCount;
            }
        }
        return values;
    }

    private static object? GetCellValueFromNode(OdfNode node)
    {
        string valueType = node.GetAttribute("value-type", OdfNamespaces.Office) ?? string.Empty;
        string value = node.GetAttribute("value", OdfNamespaces.Office) ?? string.Empty;

        string textContent = GetTextContent(node);

        switch (valueType)
        {
            case "float":
                if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
                {
                    return number;
                }
                return null;
            case "boolean":
                string? boolVal = node.GetAttribute("boolean-value", OdfNamespaces.Office);
                if (bool.TryParse(boolVal, out bool flag))
                {
                    return flag;
                }
                return null;
            case "date":
                return node.GetAttribute("date-value", OdfNamespaces.Office);
            case "string":
                return textContent;
            default:
                return string.IsNullOrEmpty(textContent) ? null : textContent;
        }
    }

    private static string GetTextContent(OdfNode node)
    {
        foreach (var child in node.Children)
        {
            if (child.LocalName == "p" && child.NamespaceUri == OdfNamespaces.Text)
            {
                return child.TextContent;
            }
        }
        return node.TextContent;
    }
}

internal sealed class OdfTableSheetDataReader : DbDataReader
{
    private readonly OdfTableSheet _sheet;
    private readonly Dictionary<(int Row, int Col), object> _cellValues;
    private readonly int _maxRow;
    private readonly int _maxCol;
    private int _currentRow = -1;

    public OdfTableSheetDataReader(OdfTableSheet sheet, Dictionary<(int Row, int Col), object> cellValues, int maxRow, int maxCol)
    {
        _sheet = sheet;
        _cellValues = cellValues;
        _maxRow = maxRow;
        _maxCol = maxCol;
    }

    public override int FieldCount => _maxCol + 1;

    public override object GetValue(int ordinal)
    {
        return _cellValues.TryGetValue((_currentRow, ordinal), out var val) ? val : string.Empty;
    }

    public override string GetName(int ordinal)
    {
        return ordinal.ToString();
    }

    public override bool Read()
    {
        if (_currentRow < _maxRow)
        {
            _currentRow++;
            return true;
        }
        return false;
    }

    public override bool IsDBNull(int ordinal)
    {
        return !_cellValues.ContainsKey((_currentRow, ordinal));
    }

    public override string GetString(int ordinal)
    {
        return GetValue(ordinal).ToString() ?? string.Empty;
    }

    public override object this[int ordinal] => GetValue(ordinal);
    public override object this[string name] => GetValue(GetOrdinal(name));

    public override int Depth => 0;
    public override bool HasRows => _maxRow >= 0;
    public override bool IsClosed => false;
    public override int RecordsAffected => -1;
    public override System.Data.DataTable GetSchemaTable()
    {
        var dt = new System.Data.DataTable();
        dt.Columns.Add("ColumnName", typeof(string));
        dt.Columns.Add("ColumnOrdinal", typeof(int));
        dt.Columns.Add("DataType", typeof(Type));
        dt.Columns.Add("AllowDBNull", typeof(bool));

        for (int i = 0; i < FieldCount; i++)
        {
            dt.Rows.Add(GetName(i), i, typeof(string), true);
        }
        return dt;
    }

    public override bool GetBoolean(int ordinal) => Convert.ToBoolean(GetValue(ordinal));
    public override byte GetByte(int ordinal) => Convert.ToByte(GetValue(ordinal));
    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();
    public override char GetChar(int ordinal) => Convert.ToChar(GetValue(ordinal));
    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();
    public override string GetDataTypeName(int ordinal) => typeof(object).Name;
    public override DateTime GetDateTime(int ordinal) => Convert.ToDateTime(GetValue(ordinal));
    public override decimal GetDecimal(int ordinal) => Convert.ToDecimal(GetValue(ordinal));
    public override double GetDouble(int ordinal) => Convert.ToDouble(GetValue(ordinal));
    public override Type GetFieldType(int ordinal) => typeof(object);
    public override float GetFloat(int ordinal) => Convert.ToSingle(GetValue(ordinal));
    public override Guid GetGuid(int ordinal) => Guid.Parse(GetString(ordinal));
    public override short GetInt16(int ordinal) => Convert.ToInt16(GetValue(ordinal));
    public override int GetInt32(int ordinal) => Convert.ToInt32(GetValue(ordinal));
    public override long GetInt64(int ordinal) => Convert.ToInt64(GetValue(ordinal));
    public override int GetOrdinal(string name) => int.TryParse(name, out int o) ? o : -1;
    public override int GetValues(object[] values)
    {
        int count = Math.Min(FieldCount, values.Length);
        for (int i = 0; i < count; i++)
            values[i] = GetValue(i);
        return count;
    }
    public override bool NextResult() => false;
    public override IEnumerator GetEnumerator() => throw new NotSupportedException();
}
