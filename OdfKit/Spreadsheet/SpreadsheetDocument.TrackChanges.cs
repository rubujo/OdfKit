using System;
using System.Collections.Generic;

namespace OdfKit.Spreadsheet;

public partial class SpreadsheetDocument
{
    #region Tracked Changes

    /// <summary>
    /// 取得或設定一個值，指出是否啟用試算表追蹤修訂。
    /// </summary>
    public bool TrackedChanges
    {
        get => SpreadsheetDocumentTrackedChangesEngine.IsTrackingEnabled(SheetsRoot);
        set => SpreadsheetDocumentTrackedChangesEngine.SetTrackingEnabled(SheetsRoot, value);
    }

    /// <summary>
    /// 取得文件中所有追蹤修訂摘要。
    /// </summary>
    /// <returns>追蹤修訂清單。</returns>
    public IReadOnlyList<OdfSpreadsheetTrackedChangeInfo> GetTrackedChanges() =>
        SpreadsheetDocumentTrackedChangesEngine.GetTrackedChanges(this);

    /// <summary>
    /// 接受指定的追蹤修訂。
    /// </summary>
    /// <param name="changeId">修訂識別碼。</param>
    public void AcceptChange(string changeId) =>
        SpreadsheetDocumentTrackedChangesEngine.AcceptChange(this, changeId);

    /// <summary>
    /// 拒絕指定的追蹤修訂。
    /// </summary>
    /// <param name="changeId">修訂識別碼。</param>
    public void RejectChange(string changeId) =>
        SpreadsheetDocumentTrackedChangesEngine.RejectChange(this, changeId);

    /// <summary>
    /// 接受所有待處理的追蹤修訂。
    /// </summary>
    public void AcceptAllChanges() =>
        SpreadsheetDocumentTrackedChangesEngine.AcceptAllChanges(this);

    /// <summary>
    /// 拒絕所有待處理的追蹤修訂。
    /// </summary>
    public void RejectAllChanges() =>
        SpreadsheetDocumentTrackedChangesEngine.RejectAllChanges(this);

    #endregion
}
