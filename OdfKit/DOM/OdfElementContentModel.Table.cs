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
/// Provides the TableTableElement API.
/// 為 <see cref="TableTableElement"/> 提供 <c>table:table</c> 結構 content model facade。
/// </summary>
public partial class TableTableElement
{
    /// <summary>
    /// Provides the member member.
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
    /// Provides the member member.
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
    /// Executes the EnumerateCellViews operation.
    /// 以輕量檢視列舉表格儲存格，避免為稀疏儲存格建立 <see cref="TableTableCellElement"/> facade。
    /// </summary>
    /// <returns>儲存格檢視列舉器</returns>
    public OdfCellViewEnumerable EnumerateCellViews()
        => new(this);

    /// <summary>
    /// Inserts one blank row at the specified position.
    /// 在指定位置插入一列空白列。
    /// </summary>
    /// <param name="position">Zero-based insertion index. / 以零為基準的插入位置。</param>
    public void InsertRows(int position) => InsertRows(position, 1);

    /// <summary>
    /// Inserts blank rows at the specified position and shifts sparse cell data.
    /// 在指定位置插入空白列，並同步位移尚未具現化的稀疏儲存格資料。
    /// </summary>
    /// <param name="position">Zero-based insertion index. / 以零為基準的插入位置。</param>
    /// <param name="count">Number of rows to insert. / 要插入的列數。</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="position"/> or <paramref name="count"/> is negative. / 當 <paramref name="position"/> 或 <paramref name="count"/> 小於 0 時擲出。</exception>
    public void InsertRows(int position, int count)
    {
        if (position < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }

        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        if (count == 0)
        {
            return;
        }

        ShiftSparseRows(position, count);
        OdfTableSheetStructureEngine.InsertRows(this, position, count);
    }

    /// <summary>
    /// Deletes one row at the specified position.
    /// 刪除指定位置的一列。
    /// </summary>
    /// <param name="position">Zero-based deletion index. / 以零為基準的刪除位置。</param>
    public void DeleteRows(int position) => DeleteRows(position, 1);

    /// <summary>
    /// Deletes rows starting at the specified position and shifts sparse cell data.
    /// 刪除指定位置起算的列，並同步位移尚未具現化的稀疏儲存格資料。
    /// </summary>
    /// <param name="position">Zero-based deletion index. / 以零為基準的刪除位置。</param>
    /// <param name="count">Number of rows to delete. / 要刪除的列數。</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="position"/> or <paramref name="count"/> is negative. / 當 <paramref name="position"/> 或 <paramref name="count"/> 小於 0 時擲出。</exception>
    public void DeleteRows(int position, int count)
    {
        if (position < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }

        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        if (count == 0)
        {
            return;
        }

        ShiftSparseRows(position + count, -count, clearFromRow: position);
        OdfTableSheetStructureEngine.DeleteRows(this, position, count);
    }

    /// <summary>
    /// Executes the CopyRows operation.
    /// 將指定範圍的列複製到目標位置，並同步複製尚未具現化的稀疏儲存格資料。
    /// </summary>
    /// <param name="sourcePosition">以零為基準的來源起始列索引</param>
    /// <param name="count">要複製的列數</param>
    /// <param name="targetPosition">以零為基準的目標插入列索引</param>
    /// <exception cref="ArgumentOutOfRangeException">當 <paramref name="sourcePosition"/>、<paramref name="count"/> 或 <paramref name="targetPosition"/> 小於 0 時擲出</exception>
    public void CopyRows(int sourcePosition, int count, int targetPosition)
    {
        if (sourcePosition < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourcePosition));
        }

        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        if (targetPosition < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetPosition));
        }

        if (count == 0)
        {
            return;
        }

        List<SparseCellSnapshot> snapshots = SnapshotSparseRows(sourcePosition, count);
        ShiftSparseRows(targetPosition, count);
        RestoreSparseRowSnapshots(snapshots, sourcePosition, targetPosition);
        OdfTableSheetStructureEngine.CopyRows(this, sourcePosition, count, targetPosition);
    }

    /// <summary>
    /// Executes the MoveRows operation.
    /// 將指定範圍的列移動到目標位置，並同步移動尚未具現化的稀疏儲存格資料。
    /// </summary>
    /// <param name="sourcePosition">以零為基準的來源起始列索引</param>
    /// <param name="count">要移動的列數</param>
    /// <param name="targetPosition">移除來源列後，以零為基準的目標插入列索引</param>
    /// <exception cref="ArgumentOutOfRangeException">當 <paramref name="sourcePosition"/>、<paramref name="count"/> 或 <paramref name="targetPosition"/> 小於 0 時擲出</exception>
    public void MoveRows(int sourcePosition, int count, int targetPosition)
    {
        if (sourcePosition < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourcePosition));
        }

        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        if (targetPosition < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetPosition));
        }

        if (count == 0)
        {
            return;
        }

        List<SparseCellSnapshot> snapshots = SnapshotSparseRows(sourcePosition, count);
        ShiftSparseRows(sourcePosition + count, -count, clearFromRow: sourcePosition);
        ShiftSparseRows(targetPosition, count);
        RestoreSparseRowSnapshots(snapshots, sourcePosition, targetPosition);
        OdfTableSheetStructureEngine.MoveRows(this, sourcePosition, count, targetPosition);
    }

    /// <summary>
    /// Provides the member member.
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
    /// Provides the member member.
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
    /// Executes the EnsureTableColumns operation.
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
    /// Executes the AppendColumn operation.
    /// 在欄位結構區段末尾新增單一欄定義。
    /// </summary>
    /// <returns>新增的 <c>table:table-column</c> 元素</returns>
    public TableTableColumnElement AppendColumn()
    {
        TableTableColumnsElement columns = EnsureTableColumns();
        return columns.AppendElement(new TableTableColumnElement("table"));
    }

    /// <summary>
    /// Executes the AppendRow operation.
    /// 在列結構區段末尾新增表格列。
    /// </summary>
    /// <returns>新增的 <c>table:table-row</c> 元素</returns>
    public TableTableRowElement AppendRow()
    {
        return AppendElement(new TableTableRowElement("table"));
    }

    /// <summary>
    /// Executes the AppendHeaderRows operation.
    /// 新增表頭列容器；表頭列固定置於所有一般資料列（<c>table:table-row</c>／
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
    /// Executes the InsertColumnStructure operation.
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
}
