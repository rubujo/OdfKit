using System;
using System.Collections.Generic;

namespace OdfKit.Spreadsheet;

public partial class SpreadsheetDocument
{
    #region Tracked Changes

    /// <summary>
    /// Gets or sets a value indicating whether spreadsheet change tracking is enabled.
    /// 取得或設定一個值，指出是否啟用試算表追蹤修訂。
    /// </summary>
    public bool TrackedChanges
    {
        get => SpreadsheetDocumentTrackedChangesEngine.IsTrackingEnabled(SheetsRoot);
        set => SpreadsheetDocumentTrackedChangesEngine.SetTrackingEnabled(SheetsRoot, value);
    }

    /// <summary>
    /// Gets summaries for all tracked changes in the document.
    /// 取得文件中所有追蹤修訂摘要。
    /// </summary>
    /// <returns>The list of tracked changes. / 追蹤修訂清單。</returns>
    public IReadOnlyList<OdfSpreadsheetTrackedChangeInfo> GetTrackedChanges() =>
        SpreadsheetDocumentTrackedChangesEngine.GetTrackedChanges(this);

    /// <summary>
    /// Accepts the specified tracked change.
    /// 接受指定的追蹤修訂。
    /// </summary>
    /// <param name="changeId">The change identifier. / 修訂識別碼。</param>
    public void AcceptChange(string changeId) =>
        SpreadsheetDocumentTrackedChangesEngine.AcceptChange(this, changeId);

    /// <summary>
    /// Rejects the specified tracked change.
    /// 拒絕指定的追蹤修訂。
    /// </summary>
    /// <param name="changeId">The change identifier. / 修訂識別碼。</param>
    public void RejectChange(string changeId) =>
        SpreadsheetDocumentTrackedChangesEngine.RejectChange(this, changeId);

    /// <summary>
    /// Accepts all pending tracked changes.
    /// 接受所有待處理的追蹤修訂。
    /// </summary>
    public void AcceptAllChanges() =>
        SpreadsheetDocumentTrackedChangesEngine.AcceptAllChanges(this);

    /// <summary>
    /// Rejects all pending tracked changes.
    /// 拒絕所有待處理的追蹤修訂。
    /// </summary>
    public void RejectAllChanges() =>
        SpreadsheetDocumentTrackedChangesEngine.RejectAllChanges(this);

    #endregion
}
