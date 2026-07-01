using System;
using System.Collections.Generic;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

public partial class OdfTableSheet
{
    /// <summary>
    /// Inserts rows at the specified position and records <c>table:insertion</c> when change tracking is enabled.
    /// 於指定位置插入列；若啟用追蹤修訂則記錄 <c>table:insertion</c>。
    /// </summary>
    /// <param name="position">The numeric value. / 以 0 為基準的插入列索引</param>
    /// <param name="count">The numeric value. / 要插入的列數</param>
    public void InsertRows(int position, int count = 1)
    {
        if (position < 0)
            throw new ArgumentOutOfRangeException(nameof(position));

        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count));

        if (TableNode is TableTableElement tableElement)
            tableElement.InsertRows(position, count);
        else
            OdfTableSheetStructureEngine.InsertRows(TableNode, position, count);

        InvalidateAccessCache();

        if (_doc.TrackedChanges)
        {
            SpreadsheetDocumentTrackedChangesEngine.RecordRowInsertion(
                _doc,
                Name,
                position,
                count);
        }
    }

    /// <summary>
    /// Deletes rows at the specified position and records <c>table:deletion</c> when change tracking is enabled.
    /// 刪除指定位置的列；若啟用追蹤修訂則記錄 <c>table:deletion</c>。
    /// </summary>
    /// <param name="position">The numeric value. / 以 0 為基準的起始列索引</param>
    /// <param name="count">The numeric value. / 要刪除的列數</param>
    public void DeleteRows(int position, int count = 1)
    {
        if (position < 0)
            throw new ArgumentOutOfRangeException(nameof(position));

        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count));

        IReadOnlyList<OdfNode> deletedSnapshots;
        if (TableNode is TableTableElement tableElement)
        {
            deletedSnapshots = OdfTableSheetStructureEngine.GetRowSnapshots(TableNode, position, count);
            tableElement.DeleteRows(position, count);
        }
        else
        {
            deletedSnapshots = OdfTableSheetStructureEngine.DeleteRows(TableNode, position, count);
        }

        InvalidateAccessCache();

        if (_doc.TrackedChanges && deletedSnapshots.Count > 0)
        {
            SpreadsheetDocumentTrackedChangesEngine.RecordRowDeletion(
                _doc,
                Name,
                position,
                deletedSnapshots);
        }
    }

    /// <summary>
    /// Copies the specified row range to the target position.
    /// 將指定範圍的列複製到目標位置。
    /// </summary>
    /// <param name="sourcePosition">The numeric value. / 以 0 為基準的來源起始列索引</param>
    /// <param name="count">The numeric value. / 要複製的列數</param>
    /// <param name="targetPosition">The numeric value. / 以 0 為基準的目標插入列索引</param>
    public void CopyRows(int sourcePosition, int count, int targetPosition)
    {
        if (sourcePosition < 0)
            throw new ArgumentOutOfRangeException(nameof(sourcePosition));

        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count));

        if (targetPosition < 0)
            throw new ArgumentOutOfRangeException(nameof(targetPosition));

        if (TableNode is TableTableElement tableElement)
            tableElement.CopyRows(sourcePosition, count, targetPosition);
        else
            OdfTableSheetStructureEngine.CopyRows(TableNode, sourcePosition, count, targetPosition);

        InvalidateAccessCache();
    }

    /// <summary>
    /// Moves the specified row range to the target position, using the table state after the source rows are removed.
    /// 將指定範圍的列移動到目標位置；目標索引以移除來源列後的表格狀態為準。
    /// </summary>
    /// <param name="sourcePosition">The numeric value. / 以 0 為基準的來源起始列索引</param>
    /// <param name="count">The numeric value. / 要移動的列數</param>
    /// <param name="targetPosition">The numeric value. / 移除來源列後，以 0 為基準的目標插入列索引</param>
    public void MoveRows(int sourcePosition, int count, int targetPosition)
    {
        if (sourcePosition < 0)
            throw new ArgumentOutOfRangeException(nameof(sourcePosition));

        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count));

        if (targetPosition < 0)
            throw new ArgumentOutOfRangeException(nameof(targetPosition));

        if (TableNode is TableTableElement tableElement)
            tableElement.MoveRows(sourcePosition, count, targetPosition);
        else
            OdfTableSheetStructureEngine.MoveRows(TableNode, sourcePosition, count, targetPosition);

        InvalidateAccessCache();
    }

    /// <summary>
    /// Inserts columns at the specified position and records <c>table:insertion</c> when change tracking is enabled.
    /// 於指定位置插入欄；若啟用追蹤修訂則記錄 <c>table:insertion</c>。
    /// </summary>
    /// <param name="position">The numeric value. / 以 0 為基準的插入欄索引</param>
    /// <param name="count">The numeric value. / 要插入的欄數</param>
    public void InsertColumns(int position, int count = 1)
    {
        if (position < 0)
            throw new ArgumentOutOfRangeException(nameof(position));

        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count));

        OdfTableSheetStructureEngine.InsertColumns(TableNode, position, count);
        InvalidateAccessCache();

        if (_doc.TrackedChanges)
        {
            SpreadsheetDocumentTrackedChangesEngine.RecordColumnInsertion(
                _doc,
                Name,
                position,
                count);
        }
    }

    /// <summary>
    /// Deletes columns at the specified position and records <c>table:deletion</c> when change tracking is enabled.
    /// 刪除指定位置的欄；若啟用追蹤修訂則記錄 <c>table:deletion</c>。
    /// </summary>
    /// <param name="position">The numeric value. / 以 0 為基準的起始欄索引</param>
    /// <param name="count">The numeric value. / 要刪除的欄數</param>
    public void DeleteColumns(int position, int count = 1)
    {
        if (position < 0)
            throw new ArgumentOutOfRangeException(nameof(position));

        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count));

        ColumnDeletionSnapshots deletedSnapshots = OdfTableSheetStructureEngine.DeleteColumns(TableNode, position, count);
        InvalidateAccessCache();

        if (_doc.TrackedChanges &&
            (deletedSnapshots.ColumnSnapshots.Count > 0 || deletedSnapshots.RowCellSnapshots.Count > 0))
        {
            SpreadsheetDocumentTrackedChangesEngine.RecordColumnDeletion(
                _doc,
                Name,
                position,
                deletedSnapshots);
        }
    }

    /// <summary>
    /// Moves cell content from the source address to the target address and records <c>table:movement</c> when change tracking is enabled.
    /// 將儲存格內容由來源位址移至目標位址；若啟用追蹤修訂則記錄 <c>table:movement</c>。
    /// </summary>
    /// <param name="sourceRow">The numeric value. / 來源列索引（以 0 為基準）</param>
    /// <param name="sourceColumn">The numeric value. / 來源欄索引（以 0 為基準）</param>
    /// <param name="targetRow">The numeric value. / 目標列索引（以 0 為基準）</param>
    /// <param name="targetColumn">The numeric value. / 目標欄索引（以 0 為基準）</param>
    public void MoveCell(int sourceRow, int sourceColumn, int targetRow, int targetColumn)
    {
        if (sourceRow < 0)
            throw new ArgumentOutOfRangeException(nameof(sourceRow));

        if (sourceColumn < 0)
            throw new ArgumentOutOfRangeException(nameof(sourceColumn));

        if (targetRow < 0)
            throw new ArgumentOutOfRangeException(nameof(targetRow));

        if (targetColumn < 0)
            throw new ArgumentOutOfRangeException(nameof(targetColumn));

        OdfTableSheetStructureEngine.MoveCell(TableNode, sourceRow, sourceColumn, targetRow, targetColumn);
        InvalidateAccessCache();

        if (_doc.TrackedChanges)
        {
            SpreadsheetDocumentTrackedChangesEngine.RecordCellMovement(
                _doc,
                Name,
                sourceRow,
                sourceColumn,
                targetRow,
                targetColumn);
        }
    }
}
