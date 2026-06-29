using System;
using System.Collections.Generic;
using OdfKit.DOM;

namespace OdfKit.Text;

public partial class TextDocument
{
    #region Tracked Changes - Recording

    /// <summary>
    /// Gets or sets tracked changes.
    /// 取得或設定一個值，指出是否啟用修訂追蹤（追蹤修訂）。
    /// </summary>
    public bool TrackedChanges { get; set; }

    /// <summary>
    /// Gets or sets this member.
    /// 取得或設定一個值，指出是否啟用修訂追蹤。
    /// </summary>
    /// <remarks>
    /// 這是 <see cref="TrackedChanges"/> 的計畫名別名，供呼叫端以 <c>doc.TrackChanges = true</c>
    /// 啟用修訂追蹤。
    /// </remarks>
    public bool TrackChanges
    {
        get => TrackedChanges;
        set => TrackedChanges = value;
    }

    /// <summary>
    /// Provides record tracked change.
    /// 記錄修訂追蹤資訊。
    /// </summary>
    /// <param name="changeType">The value to use. / 修訂類型</param>
    /// <param name="extraContent">The text or value. / 修訂的附加內容節點</param>
    /// <param name="originalStyleName">The name or identifier. / 原本的樣式名稱</param>
    /// <param name="targetFamily">The stream or target object. / 目標樣式系列名稱</param>
    /// <returns>The result. / 產生的修訂識別碼</returns>
    public string RecordTrackedChange(string changeType, OdfNode? extraContent = null, string? originalStyleName = null, string? targetFamily = null) =>
        AddTrackedChange(changeType, "Author", DateTime.UtcNow, extraContent, originalStyleName, targetFamily);

    /// <summary>
    /// Adds add tracked change.
    /// 新增一個追蹤修訂記錄。
    /// </summary>
    /// <param name="changeType">The value to use. / 修訂類型（"insertion"、"deletion" 或 "format-change"）</param>
    /// <param name="creator">The value to use. / 建立者姓名</param>
    /// <param name="date">The value to use. / 修訂時間</param>
    /// <param name="extraContent">The text or value. / 修訂的附加內容節點</param>
    /// <param name="originalStyleName">The name or identifier. / 原本的樣式名稱</param>
    /// <param name="targetFamily">The stream or target object. / 目標樣式系列名稱</param>
    /// <returns>The result. / 產生的修訂識別碼</returns>
    public string AddTrackedChange(string changeType, string creator, DateTime date, OdfNode? extraContent = null, string? originalStyleName = null, string? targetFamily = null) =>
        TextDocumentTrackChangesRecordingEngine.AddTrackedChange(MutationContext, changeType, creator, date, extraContent, originalStyleName, targetFamily);

    /// <summary>
    /// Accepts accept all changes.
    /// 接受文件中所有的追蹤修訂。
    /// </summary>
    public void AcceptAllChanges() => AcceptAllTrackedChanges();

    /// <summary>
    /// Rejects reject all changes.
    /// 拒絕文件中所有的追蹤修訂。
    /// </summary>
    public void RejectAllChanges() => RejectAllTrackedChanges();

    /// <summary>
    /// Gets get tracked changes.
    /// 取得文件中所有的追蹤修訂。
    /// </summary>
    /// <returns>The result. / 追蹤修訂的集合</returns>
    public IEnumerable<OdfTrackedChange> GetTrackedChanges() =>
        TextDocumentTrackChangesRecordingEngine.GetTrackedChanges(this, MutationContext);

    /// <summary>
    /// Gets get table structural changes.
    /// 取得文件中所有表格結構修訂（列／欄插入刪除）的摘要清單。
    /// </summary>
    public IReadOnlyList<OdfTableStructuralChangeInfo> GetTableStructuralChanges() =>
        TextDocumentTableStructuralChangeReadEngine.GetTableStructuralChanges(BodyTextRoot);

    /// <summary>
    /// Provides track format change.
    /// 追蹤格式變更。
    /// </summary>
    /// <param name="node">The value to use. / 發生變更的 ODF 節點</param>
    /// <param name="family">The value to use. / 樣式系列名稱</param>
    public void TrackFormatChange(OdfNode node, string family) =>
        TextDocumentTrackChangesRecordingEngine.TrackFormatChange(this, node, family);

    /// <summary>
    /// Removes delete node.
    /// 刪除指定的節點並記錄刪除修訂（若啟用修訂追蹤）。
    /// </summary>
    /// <param name="node">The value to use. / 要刪除的 ODF 節點</param>
    public void DeleteNode(OdfNode node) =>
        TextDocumentTrackChangesRecordingEngine.DeleteNode(this, node);

    #endregion
}
