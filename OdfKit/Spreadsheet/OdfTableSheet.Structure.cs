using System;
using System.Collections.Generic;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

public partial class OdfTableSheet
{
    /// <summary>
    /// 於指定位置插入列；若啟用追蹤修訂則記錄 <c>table:insertion</c>。
    /// </summary>
    /// <param name="position">以 0 為基準的插入列索引。</param>
    /// <param name="count">要插入的列數。</param>
    public void InsertRows(int position, int count = 1)
    {
        if (position < 0)
            throw new ArgumentOutOfRangeException(nameof(position));

        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count));

        OdfTableSheetStructureEngine.InsertRows(TableNode, position, count);

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
    /// 刪除指定位置的列；若啟用追蹤修訂則記錄 <c>table:deletion</c>。
    /// </summary>
    /// <param name="position">以 0 為基準的起始列索引。</param>
    /// <param name="count">要刪除的列數。</param>
    public void DeleteRows(int position, int count = 1)
    {
        if (position < 0)
            throw new ArgumentOutOfRangeException(nameof(position));

        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count));

        IReadOnlyList<OdfNode> deletedSnapshots = OdfTableSheetStructureEngine.DeleteRows(TableNode, position, count);

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
    /// 於指定位置插入欄；若啟用追蹤修訂則記錄 <c>table:insertion</c>。
    /// </summary>
    /// <param name="position">以 0 為基準的插入欄索引。</param>
    /// <param name="count">要插入的欄數。</param>
    public void InsertColumns(int position, int count = 1)
    {
        if (position < 0)
            throw new ArgumentOutOfRangeException(nameof(position));

        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count));

        OdfTableSheetStructureEngine.InsertColumns(TableNode, position, count);

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
    /// 刪除指定位置的欄；若啟用追蹤修訂則記錄 <c>table:deletion</c>。
    /// </summary>
    /// <param name="position">以 0 為基準的起始欄索引。</param>
    /// <param name="count">要刪除的欄數。</param>
    public void DeleteColumns(int position, int count = 1)
    {
        if (position < 0)
            throw new ArgumentOutOfRangeException(nameof(position));

        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count));

        ColumnDeletionSnapshots deletedSnapshots = OdfTableSheetStructureEngine.DeleteColumns(TableNode, position, count);

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
    /// 將儲存格內容由來源位址移至目標位址；若啟用追蹤修訂則記錄 <c>table:movement</c>。
    /// </summary>
    /// <param name="sourceRow">來源列索引（以 0 為基準）。</param>
    /// <param name="sourceColumn">來源欄索引（以 0 為基準）。</param>
    /// <param name="targetRow">目標列索引（以 0 為基準）。</param>
    /// <param name="targetColumn">目標欄索引（以 0 為基準）。</param>
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
