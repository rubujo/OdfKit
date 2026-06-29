using System;
using System.Collections.Generic;
using OdfKit.DOM;

namespace OdfKit.Text;

public partial class OdfTable
{
    /// <summary>
    /// Provides insert rows.
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

        OdfTextTableStructureEngine.InsertRows(Node, position, count);

        if (_doc.TrackedChanges)
        {
            TextDocumentTableStructuralChangeRecordingEngine.RecordRowInsertion(
                Node,
                position,
                count,
                "Author",
                DateTime.UtcNow);
        }
    }

    /// <summary>
    /// Removes delete rows.
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

        IReadOnlyList<OdfNode> deletedSnapshots = OdfTextTableStructureEngine.DeleteRows(Node, position, count);

        if (_doc.TrackedChanges && deletedSnapshots.Count > 0)
        {
            TextDocumentTableStructuralChangeRecordingEngine.RecordRowDeletion(
                Node,
                position,
                deletedSnapshots,
                "Author",
                DateTime.UtcNow);
        }
    }

    /// <summary>
    /// Provides insert columns.
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

        OdfTextTableStructureEngine.InsertColumns(Node, position, count);

        if (_doc.TrackedChanges)
        {
            TextDocumentTableStructuralChangeRecordingEngine.RecordColumnInsertion(
                Node,
                position,
                count,
                "Author",
                DateTime.UtcNow);
        }
    }

    /// <summary>
    /// Removes delete columns.
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

        ColumnDeletionSnapshots deletedSnapshots = OdfTextTableStructureEngine.DeleteColumns(Node, position, count);

        if (_doc.TrackedChanges &&
            (deletedSnapshots.ColumnSnapshots.Count > 0 || deletedSnapshots.RowCellSnapshots.Count > 0))
        {
            TextDocumentTableStructuralChangeRecordingEngine.RecordColumnDeletion(
                Node,
                position,
                deletedSnapshots,
                "Author",
                DateTime.UtcNow);
        }
    }
}
