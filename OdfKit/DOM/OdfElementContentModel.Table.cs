using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using OdfKit.Core;
using OdfKit.Spreadsheet;

namespace OdfKit.DOM;

/// <summary>
/// 為 <see cref="TableTableElement"/> 提供 <c>table:table</c> 結構 content model facade。
/// </summary>
public partial class TableTableElement
{
    /// <summary>
    /// 以零為基準的列與欄索引存取儲存格，若目標位置不存在則自動補齊列與儲存格。
    /// </summary>
    /// <param name="row">以零為基準的列索引</param>
    /// <param name="column">以零為基準的欄索引</param>
    /// <returns>指定位置的 <c>table:table-cell</c> 元素</returns>
    public TableTableCellElement this[int row, int column]
    {
        get => GetOrCreateCell(row, column);
    }

    /// <summary>
    /// 以 Excel 位址（例如 <c>A1</c>）存取儲存格，若目標位置不存在則自動補齊列與儲存格。
    /// </summary>
    /// <param name="address">Excel 樣式儲存格位址</param>
    /// <returns>指定位置的 <c>table:table-cell</c> 元素</returns>
    public TableTableCellElement this[string address]
    {
        get
        {
            OdfCellAddress cellAddress = OdfCellAddress.ParseExcel(address);
            return GetOrCreateCell(cellAddress.Row, cellAddress.Column);
        }
    }

    /// <summary>
    /// 依文件順序列舉 <c>table:table</c> 欄位結構 choice group 中的直接子元素。
    /// </summary>
    public IEnumerable<OdfElement> ColumnStructureChildElements
    {
        get
        {
            foreach (OdfNode child in Children)
            {
                if (child is OdfElement element && OdfElementContentModel.IsTableColumnStructure(element))
                {
                    yield return element;
                }
            }
        }
    }

    /// <summary>
    /// 依文件順序列舉 <c>table:table</c> 列結構 choice group 中的直接子元素。
    /// </summary>
    public IEnumerable<OdfElement> RowStructureChildElements
    {
        get
        {
            foreach (OdfNode child in Children)
            {
                if (child is OdfElement element && OdfElementContentModel.IsTableRowStructure(element))
                {
                    yield return element;
                }
            }
        }
    }

    /// <summary>
    /// 取得或建立 <c>table:table-columns</c> 容器，供後續新增欄定義使用。
    /// </summary>
    /// <returns>表格欄位容器元素</returns>
    public TableTableColumnsElement EnsureTableColumns()
    {
        TableTableColumnsElement? existing = TableTableColumnsChildElements.FirstOrDefault();
        if (existing is not null)
        {
            return existing;
        }

        return InsertColumnStructure(new TableTableColumnsElement("table"));
    }

    /// <summary>
    /// 在欄位結構區段末尾新增單一欄定義。
    /// </summary>
    /// <returns>新增的 <c>table:table-column</c> 元素</returns>
    public TableTableColumnElement AppendColumn()
    {
        TableTableColumnsElement columns = EnsureTableColumns();
        return columns.AppendElement(new TableTableColumnElement("table"));
    }

    /// <summary>
    /// 在列結構區段末尾新增表格列。
    /// </summary>
    /// <returns>新增的 <c>table:table-row</c> 元素</returns>
    public TableTableRowElement AppendRow()
    {
        return AppendElement(new TableTableRowElement("table"));
    }

    /// <summary>
    /// 新增表頭列容器；表頭列固定置於所有一般資料列（<c>table:table-row</c>／
    /// <c>table:table-rows</c>／<c>table:table-row-group</c>）之前，即使呼叫時已存在資料列，
    /// 仍會插入在第一個資料列之前，而非單純附加於列結構區段末尾。
    /// </summary>
    /// <returns>新增的 <c>table:table-header-rows</c> 元素</returns>
    public TableTableHeaderRowsElement AppendHeaderRows()
    {
        var headerRows = new TableTableHeaderRowsElement("table");
        OdfNode? firstNonHeaderRow = Children.FirstOrDefault(child =>
            child is OdfElement rowElement &&
            OdfElementContentModel.IsTableRowStructure(rowElement) &&
            rowElement is not TableTableHeaderRowsElement);
        if (firstNonHeaderRow is null)
        {
            return AppendElement(headerRows);
        }

        return InsertElementBefore(headerRows, firstNonHeaderRow);
    }

    /// <summary>
    /// 在欄位結構 choice group 的語意位置插入子元素。
    /// </summary>
    /// <typeparam name="TElement">欄位結構元素型別</typeparam>
    /// <param name="element">要插入的元素</param>
    /// <returns>已插入的元素</returns>
    public TElement InsertColumnStructure<TElement>(TElement element)
        where TElement : OdfElement
    {
        OdfNode? firstRowStructure = Children.FirstOrDefault(child =>
            child is OdfElement rowElement && OdfElementContentModel.IsTableRowStructure(rowElement));
        if (firstRowStructure is null)
        {
            return AppendElement(element);
        }

        return InsertElementBefore(element, firstRowStructure);
    }

    /// <summary>
    /// 將 <see cref="DbDataReader"/> 的資料逐列匯入至目前表格。
    /// </summary>
    /// <param name="reader">資料讀取器</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="reader"/> 為 <see langword="null"/> 時擲出</exception>
    public void ImportData(DbDataReader reader)
    {
        if (reader is null)
        {
            throw new ArgumentNullException(nameof(reader));
        }

        int rowIndex = 0;
        while (reader.Read())
        {
            for (int columnIndex = 0; columnIndex < reader.FieldCount; columnIndex++)
            {
                object? value = reader.IsDBNull(columnIndex) ? null : reader.GetValue(columnIndex);
                SetCellValue(GetOrCreateCell(rowIndex, columnIndex), value);
            }

            rowIndex++;
        }
    }

    /// <summary>
    /// 將 <see cref="DataTable"/> 的資料逐列匯入至目前表格。
    /// </summary>
    /// <param name="table">資料表</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="table"/> 為 <see langword="null"/> 時擲出</exception>
    public void ImportData(DataTable table)
    {
        if (table is null)
        {
            throw new ArgumentNullException(nameof(table));
        }

        for (int rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
        {
            DataRow row = table.Rows[rowIndex];
            for (int columnIndex = 0; columnIndex < table.Columns.Count; columnIndex++)
            {
                object? value = row.IsNull(columnIndex) ? null : row[columnIndex];
                SetCellValue(GetOrCreateCell(rowIndex, columnIndex), value);
            }
        }
    }

    /// <summary>
    /// 將實體集合逐列匯入至目前表格。
    /// </summary>
    /// <typeparam name="T">資料列型別</typeparam>
    /// <param name="collection">來源資料集合</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="collection"/> 為 <see langword="null"/> 時擲出</exception>
    public void ImportData<T>(IEnumerable<T> collection)
    {
        if (collection is null)
        {
            throw new ArgumentNullException(nameof(collection));
        }

        Func<T, object?>[] accessors = ValueAccessorCache<T>.Accessors;
        int rowIndex = 0;
        foreach (T item in collection)
        {
            for (int columnIndex = 0; columnIndex < accessors.Length; columnIndex++)
            {
                SetCellValue(GetOrCreateCell(rowIndex, columnIndex), accessors[columnIndex](item));
            }

            rowIndex++;
        }
    }

    private TableTableCellElement GetOrCreateCell(int rowIndex, int columnIndex)
    {
        if (rowIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rowIndex));
        }

        if (columnIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(columnIndex));
        }

        TableTableRowElement row = GetOrCreateRow(rowIndex);
        List<TableTableCellElement> cells = row.TableTableCellChildElements.ToList();
        while (cells.Count <= columnIndex)
        {
            TableTableCellElement newCell = row.AppendElement(new TableTableCellElement("table"));
            cells.Add(newCell);
        }

        return cells[columnIndex];
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

    private static void SetCellValue(TableTableCellElement cell, object? value)
    {
        cell.RemoveAttribute("value", OdfNamespaces.Office);
        cell.RemoveAttribute("string-value", OdfNamespaces.Office);
        cell.RemoveAttribute("date-value", OdfNamespaces.Office);
        cell.RemoveAttribute("boolean-value", OdfNamespaces.Office);
        cell.Children.Clear();

        if (value is null)
        {
            cell.ValueType = "string";
            cell.SetAttribute("string-value", OdfNamespaces.Office, string.Empty, "office");
            cell.AppendElement(new TextPElement("text")).TextContent = string.Empty;
            return;
        }

        switch (value)
        {
            case bool boolValue:
                cell.ValueType = "boolean";
                cell.SetAttribute("boolean-value", OdfNamespaces.Office, boolValue ? "true" : "false", "office");
                cell.AppendElement(new TextPElement("text")).TextContent = boolValue ? "TRUE" : "FALSE";
                return;
            case DateTime dateTimeValue:
                string isoDate = dateTimeValue.Kind == DateTimeKind.Utc
                    ? dateTimeValue.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
                    : dateTimeValue.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
                cell.ValueType = "date";
                cell.SetAttribute("date-value", OdfNamespaces.Office, isoDate, "office");
                cell.AppendElement(new TextPElement("text")).TextContent = isoDate;
                return;
            case sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal:
                string numeric = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0";
                cell.ValueType = "float";
                cell.SetAttribute("value", OdfNamespaces.Office, numeric, "office");
                cell.AppendElement(new TextPElement("text")).TextContent = numeric;
                return;
            default:
                string text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
                cell.ValueType = "string";
                cell.SetAttribute("string-value", OdfNamespaces.Office, text, "office");
                cell.AppendElement(new TextPElement("text")).TextContent = text;
                return;
        }
    }

    private static class ValueAccessorCache<T>
    {
        public static readonly Func<T, object?>[] Accessors = BuildAccessors();

        private static Func<T, object?>[] BuildAccessors()
        {
            PropertyInfo[] properties = typeof(T)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(static property => property.CanRead && property.GetMethod is not null)
                .ToArray();

            if (properties.Length == 0)
            {
                return [static _ => null];
            }

            Func<T, object?>[] accessors = new Func<T, object?>[properties.Length];
            for (int i = 0; i < properties.Length; i++)
            {
                ParameterExpression parameter = Expression.Parameter(typeof(T), "item");
                Expression member = Expression.Property(parameter, properties[i]);
                UnaryExpression cast = Expression.Convert(member, typeof(object));
                accessors[i] = Expression.Lambda<Func<T, object?>>(cast, parameter).Compile();
            }

            return accessors;
        }
    }
}
