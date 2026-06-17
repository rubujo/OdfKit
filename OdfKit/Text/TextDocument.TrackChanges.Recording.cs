using System;
using System.Collections.Generic;
using OdfKit.DOM;

namespace OdfKit.Text;

public partial class TextDocument
{
    #region Tracked Changes - Recording

    /// <summary>
    /// 取得或設定一個值，指出是否啟用修訂追蹤（追蹤修訂）。
    /// </summary>
    public bool TrackedChanges { get; set; }

    /// <summary>
    /// 記錄修訂追蹤資訊。
    /// </summary>
    /// <param name="changeType">修訂類型</param>
    /// <param name="extraContent">修訂的附加內容節點</param>
    /// <param name="originalStyleName">原本的樣式名稱</param>
    /// <param name="targetFamily">目標樣式系列名稱</param>
    /// <returns>產生的修訂識別碼</returns>
    public string RecordTrackedChange(string changeType, OdfNode? extraContent = null, string? originalStyleName = null, string? targetFamily = null) =>
        AddTrackedChange(changeType, "Author", DateTime.UtcNow, extraContent, originalStyleName, targetFamily);

    /// <summary>
    /// 新增一個追蹤修訂記錄。
    /// </summary>
    /// <param name="changeType">修訂類型（"insertion"、"deletion" 或 "format-change"）。</param>
    /// <param name="creator">建立者姓名。</param>
    /// <param name="date">修訂時間。</param>
    /// <param name="extraContent">修訂的附加內容節點。</param>
    /// <param name="originalStyleName">原本的樣式名稱。</param>
    /// <param name="targetFamily">目標樣式系列名稱。</param>
    /// <returns>產生的修訂識別碼。</returns>
    public string AddTrackedChange(string changeType, string creator, DateTime date, OdfNode? extraContent = null, string? originalStyleName = null, string? targetFamily = null) =>
        TextDocumentTrackChangesRecordingEngine.AddTrackedChange(MutationContext, changeType, creator, date, extraContent, originalStyleName, targetFamily);

    /// <summary>
    /// 接受文件中所有的追蹤修訂。
    /// </summary>
    public void AcceptAllChanges() => AcceptAllTrackedChanges();

    /// <summary>
    /// 拒絕文件中所有的追蹤修訂。
    /// </summary>
    public void RejectAllChanges() => RejectAllTrackedChanges();

    /// <summary>
    /// 取得文件中所有的追蹤修訂。
    /// </summary>
    /// <returns>追蹤修訂的集合。</returns>
    public IEnumerable<OdfTrackedChange> GetTrackedChanges() =>
        TextDocumentTrackChangesRecordingEngine.GetTrackedChanges(this, MutationContext);

    /// <summary>
    /// 取得文件中所有表格結構修訂（列／欄插入刪除）的摘要清單。
    /// </summary>
    public IReadOnlyList<OdfTableStructuralChangeInfo> GetTableStructuralChanges() =>
        TextDocumentTableStructuralChangeReadEngine.GetTableStructuralChanges(BodyTextRoot);

    /// <summary>
    /// 追蹤格式變更。
    /// </summary>
    /// <param name="node">發生變更的 ODF 節點</param>
    /// <param name="family">樣式系列名稱</param>
    public void TrackFormatChange(OdfNode node, string family) =>
        TextDocumentTrackChangesRecordingEngine.TrackFormatChange(this, node, family);

    /// <summary>
    /// 刪除指定的節點並記錄刪除修訂（若啟用修訂追蹤）。
    /// </summary>
    /// <param name="node">要刪除的 ODF 節點</param>
    public void DeleteNode(OdfNode node) =>
        TextDocumentTrackChangesRecordingEngine.DeleteNode(this, node);

    #endregion
}
