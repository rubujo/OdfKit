using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.Buffers;
using System.IO;
using System.IO.Compression;
using System.Text;
using OdfKit.Core;
using OdfKit.Spreadsheet;

namespace OdfKit.DOM;

/// <summary>
/// Partial: table data import APIs for TableTableElement.
/// Partial：TableTableElement 的資料匯入 API。
/// </summary>
public partial class TableTableElement
{
    /// <summary>
    /// Imports rows from a <see cref="DbDataReader"/> into the current table.
    /// 將 <see cref="DbDataReader"/> 的資料逐列匯入至目前表格。
    /// </summary>
    /// <param name="reader">The data reader. / 資料讀取器。</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="reader"/> is <see langword="null"/>. / 當 <paramref name="reader"/> 為 <see langword="null"/> 時擲出。</exception>
    public void ImportData(DbDataReader reader)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(reader, nameof(reader));

        int rowIndex = 0;
        while (reader.Read())
        {
            for (int columnIndex = 0; columnIndex < reader.FieldCount; columnIndex++)
            {
                object? value = reader.IsDBNull(columnIndex) ? null : reader.GetValue(columnIndex);
                SetSparseCellValue(rowIndex, columnIndex, value);
            }

            rowIndex++;
        }
        CompressColdPages();
    }

    /// <summary>
    /// Imports data.
    /// 將 <see cref="DataTable"/> 的資料逐列匯入至目前表格。
    /// </summary>
    /// <param name="table">資料表</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="table"/> 為 <see langword="null"/> 時擲出</exception>
    public void ImportData(DataTable table)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(table, nameof(table));

        for (int rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
        {
            DataRow row = table.Rows[rowIndex];
            for (int columnIndex = 0; columnIndex < table.Columns.Count; columnIndex++)
            {
                object? value = row.IsNull(columnIndex) ? null : row[columnIndex];
                SetSparseCellValue(rowIndex, columnIndex, value);
            }
        }
        CompressColdPages();
    }

    /// <summary>
    /// Imports data.
    /// 將實體集合逐列匯入至目前表格。
    /// </summary>
    /// <typeparam name="T">資料列型別</typeparam>
    /// <param name="collection">來源資料集合</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="collection"/> 為 <see langword="null"/> 時擲出</exception>
    public void ImportData<T>(IEnumerable<T> collection)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(collection, nameof(collection));

        Func<T, object?>[] accessors = ValueAccessorCache<T>.Accessors;
        int rowIndex = 0;
        foreach (T item in collection)
        {
            for (int columnIndex = 0; columnIndex < accessors.Length; columnIndex++)
            {
                SetSparseCellValue(rowIndex, columnIndex, accessors[columnIndex](item));
            }

            rowIndex++;
        }
        CompressColdPages();
    }

    private TableTableCellElement GetOrCreateCell(int rowIndex, int columnIndex)
    {
        if (rowIndex < 0 || rowIndex > OdfSpreadsheetLimits.MaxRowIndex)
            throw new ArgumentOutOfRangeException(nameof(rowIndex));

        if (columnIndex < 0 || columnIndex > OdfSpreadsheetLimits.MaxColumnIndex)
            throw new ArgumentOutOfRangeException(nameof(columnIndex));

        TableTableRowElement row = GetOrCreateRow(rowIndex);
        List<TableTableCellElement> cells = row.TableTableCellChildElements.ToList();
        while (cells.Count <= columnIndex)
        {
            TableTableCellElement newCell = row.AppendElement(new TableTableCellElement("table"));
            cells.Add(newCell);
        }

        TableTableCellElement cell = cells[columnIndex];

        if (TryGetSparseCellData(rowIndex, columnIndex, out byte type, out double dVal, out bool bVal, out long ticks, out string? text, out string? style, out string? formula))
        {
            ClearSparseCell(rowIndex, columnIndex);

            cell.RemoveAttribute("value", OdfNamespaces.Office);
            cell.RemoveAttribute("string-value", OdfNamespaces.Office);
            cell.RemoveAttribute("date-value", OdfNamespaces.Office);
            cell.RemoveAttribute("boolean-value", OdfNamespaces.Office);
            cell.Children.Clear();

            if (type == 1)
            {
                cell.ValueType = "float";
                cell.SetAttribute("value", OdfNamespaces.Office, Convert.ToString(dVal, CultureInfo.InvariantCulture), "office");
                cell.AppendElement(new TextPElement("text")).TextContent = Convert.ToString(dVal, CultureInfo.InvariantCulture);
            }
            else if (type == 2)
            {
                cell.ValueType = "boolean";
                cell.SetAttribute("boolean-value", OdfNamespaces.Office, bVal ? "true" : "false", "office");
                cell.AppendElement(new TextPElement("text")).TextContent = bVal ? "TRUE" : "FALSE";
            }
            else if (type == 3)
            {
                var dt = new DateTime(ticks);
                string isoDate = dt.Kind == DateTimeKind.Utc
                    ? dt.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
                    : dt.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
                cell.ValueType = "date";
                cell.SetAttribute("date-value", OdfNamespaces.Office, isoDate, "office");
                cell.AppendElement(new TextPElement("text")).TextContent = isoDate;
            }
            else
            {
                cell.ValueType = "string";
                cell.SetAttribute("string-value", OdfNamespaces.Office, text ?? string.Empty, "office");
                cell.AppendElement(new TextPElement("text")).TextContent = text ?? string.Empty;
            }

            if (style is not null)
                cell.StyleName = style;
            if (formula is not null)
                cell.SetAttribute("formula", OdfNamespaces.Table, formula, "table");
        }

        return cell;
    }

    private TableTableRowElement GetOrCreateRow(int rowIndex)
    {
        List<TableTableRowElement> rows = TableTableRowChildElements.ToList();
        while (rows.Count <= rowIndex)
        {
            rows.Add(AppendRow());
        }

        return rows[rowIndex];
    }

    private const int PageSize = 128;
    internal IntPtr[][]? _nativePages;

}
