using System;
using System.Collections.Generic;
using OdfKit.DOM;

namespace OdfKit.Text;
/// <summary>
/// Provides the TextDocument API.
/// 提供 TextDocument API。
/// </summary>

public partial class TextDocument
{
    #region Tracked Changes - Recording

    /// <summary>
    /// Gets or sets a value indicating whether change tracking (track changes) is enabled.
    /// 取得或設定一個值，指出是否啟用修訂追蹤（追蹤修訂）。
    /// </summary>
    public bool TrackedChanges { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether change tracking is enabled.
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
    /// Short overload of RecordTrackedChange that accepts changeType; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 changeType；其餘可選參數使用預設值並轉呼叫最長 RecordTrackedChange 多載。
    /// </summary>
    public string RecordTrackedChange(string changeType) => RecordTrackedChange(changeType, null, null, null);

    /// <summary>
    /// Short overload of RecordTrackedChange that accepts changeType and extraContent; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 changeType 與 extraContent；其餘可選參數使用預設值並轉呼叫最長 RecordTrackedChange 多載。
    /// </summary>
    public string RecordTrackedChange(string changeType, OdfNode? extraContent) => RecordTrackedChange(changeType, extraContent, null, null);

    /// <summary>
    /// Short overload of RecordTrackedChange that accepts changeType, extraContent, and originalStyleName; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 changeType、extraContent 與 originalStyleName；其餘可選參數使用預設值並轉呼叫最長 RecordTrackedChange 多載。
    /// </summary>
    public string RecordTrackedChange(string changeType, OdfNode? extraContent, string? originalStyleName) => RecordTrackedChange(changeType, extraContent, originalStyleName, null);


    /// <summary>
    /// Records change tracking information.
    /// 記錄修訂追蹤資訊。
    /// </summary>
    /// <param name="changeType">The change type. / 修訂類型。</param>
    /// <param name="extraContent">The change's extra content node. / 修訂的附加內容節點。</param>
    /// <param name="originalStyleName">The original style name. / 原本的樣式名稱。</param>
    /// <param name="targetFamily">The target style family name. / 目標樣式系列名稱。</param>
    /// <returns>The generated change identifier. / 產生的修訂識別碼。</returns>
    public string RecordTrackedChange(string changeType, OdfNode? extraContent, string? originalStyleName, string? targetFamily) =>
        AddTrackedChange(changeType, "Author", DateTime.UtcNow, extraContent, originalStyleName, targetFamily);

    /// <summary>
    /// Short overload of AddTrackedChange that accepts changeType, creator, and date; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 changeType、creator 與 date；其餘可選參數使用預設值並轉呼叫最長 AddTrackedChange 多載。
    /// </summary>
    public string AddTrackedChange(string changeType, string creator, DateTime date) => AddTrackedChange(changeType, creator, date, null, null, null);

    /// <summary>
    /// Short overload of AddTrackedChange that accepts changeType, creator, date, and extraContent; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 changeType、creator、date 與 extraContent；其餘可選參數使用預設值並轉呼叫最長 AddTrackedChange 多載。
    /// </summary>
    public string AddTrackedChange(string changeType, string creator, DateTime date, OdfNode? extraContent) => AddTrackedChange(changeType, creator, date, extraContent, null, null);

    /// <summary>
    /// Short overload of AddTrackedChange that accepts changeType, creator, date, extraContent, and originalStyleName; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 changeType、creator、date、extraContent 與 originalStyleName；其餘可選參數使用預設值並轉呼叫最長 AddTrackedChange 多載。
    /// </summary>
    public string AddTrackedChange(string changeType, string creator, DateTime date, OdfNode? extraContent, string? originalStyleName) => AddTrackedChange(changeType, creator, date, extraContent, originalStyleName, null);


    /// <summary>
    /// Adds a tracked change record.
    /// 新增一個追蹤修訂記錄。
    /// </summary>
    /// <param name="changeType">The change type ("insertion", "deletion", or "format-change"). / 修訂類型（"insertion"、"deletion" 或 "format-change"）。</param>
    /// <param name="creator">The creator's name. / 建立者姓名。</param>
    /// <param name="date">The change time. / 修訂時間。</param>
    /// <param name="extraContent">The change's extra content node. / 修訂的附加內容節點。</param>
    /// <param name="originalStyleName">The original style name. / 原本的樣式名稱。</param>
    /// <param name="targetFamily">The target style family name. / 目標樣式系列名稱。</param>
    /// <returns>The generated change identifier. / 產生的修訂識別碼。</returns>
    public string AddTrackedChange(string changeType, string creator, DateTime date, OdfNode? extraContent, string? originalStyleName, string? targetFamily) =>
        TextDocumentTrackChangesRecordingEngine.AddTrackedChange(MutationContext, changeType, creator, date, extraContent, originalStyleName, targetFamily);


    /// <summary>
    /// Accepts all tracked changes in the document.
    /// 接受文件中所有的追蹤修訂。
    /// </summary>
    public void AcceptAllChanges() => AcceptAllTrackedChanges();

    /// <summary>
    /// Rejects all tracked changes in the document.
    /// 拒絕文件中所有的追蹤修訂。
    /// </summary>
    public void RejectAllChanges() => RejectAllTrackedChanges();

    /// <summary>
    /// Gets all tracked changes in the document.
    /// 取得文件中所有的追蹤修訂。
    /// </summary>
    /// <returns>The collection of tracked changes. / 追蹤修訂的集合。</returns>
    public IEnumerable<OdfTrackedChange> GetTrackedChanges() =>
        TextDocumentTrackChangesRecordingEngine.GetTrackedChanges(this, MutationContext);

    /// <summary>
    /// Gets a summary list of all table structural changes (row/column insertions and deletions) in the document.
    /// 取得文件中所有表格結構修訂（列／欄插入刪除）的摘要清單。
    /// </summary>
    public IReadOnlyList<OdfTableStructuralChangeInfo> GetTableStructuralChanges() =>
        TextDocumentTableStructuralChangeReadEngine.GetTableStructuralChanges(BodyTextRoot);

    /// <summary>
    /// Tracks a format change.
    /// 追蹤格式變更。
    /// </summary>
    /// <param name="node">The ODF node where the change occurred. / 發生變更的 ODF 節點。</param>
    /// <param name="family">The style family name. / 樣式系列名稱。</param>
    public void TrackFormatChange(OdfNode node, string family) =>
        TextDocumentTrackChangesRecordingEngine.TrackFormatChange(this, node, family);

    /// <summary>
    /// Deletes the specified node and records a deletion change (if change tracking is enabled).
    /// 刪除指定的節點並記錄刪除修訂（若啟用修訂追蹤）。
    /// </summary>
    /// <param name="node">The ODF node to delete. / 要刪除的 ODF 節點。</param>
    public void DeleteNode(OdfNode node) =>
        TextDocumentTrackChangesRecordingEngine.DeleteNode(this, node);

    #endregion
}
