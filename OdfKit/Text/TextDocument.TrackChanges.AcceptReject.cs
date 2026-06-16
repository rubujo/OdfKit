namespace OdfKit.Text;

public partial class TextDocument
{
    #region Tracked Changes - Accept/Reject

    /// <summary>
    /// 接受文件中所有的追蹤修訂。
    /// </summary>
    public void AcceptAllTrackedChanges()
        => OdfTrackedChangesEngine.AcceptAll(BodyTextRoot);

    /// <summary>
    /// 拒絕文件中所有的追蹤修訂。
    /// </summary>
    public void RejectAllTrackedChanges()
        => OdfTrackedChangesEngine.RejectAll(BodyTextRoot, Package);

    /// <summary>
    /// 接受指定的追蹤修訂。
    /// </summary>
    /// <param name="changeId">修訂識別碼</param>
    public void AcceptChange(string changeId)
        => OdfTrackedChangesEngine.AcceptChange(BodyTextRoot, changeId);

    /// <summary>
    /// 拒絕指定的追蹤修訂。
    /// </summary>
    /// <param name="changeId">修訂識別碼</param>
    public void RejectChange(string changeId)
        => OdfTrackedChangesEngine.RejectChange(BodyTextRoot, changeId, Package);

    #endregion
}
