namespace OdfKit.Database;

/// <summary>
/// 表示查詢的排序或篩選陳述式摘要（<c>db:order-statement</c>／<c>db:filter-statement</c>）。
/// </summary>
/// <param name="command">陳述式命令文字。</param>
/// <param name="applyCommand">是否套用此陳述式。</param>
public sealed class OdfDatabaseQueryStatementInfo(string command, bool? applyCommand)
{
    /// <summary>
    /// 取得陳述式命令文字。
    /// </summary>
    public string Command { get; } = command;

    /// <summary>
    /// 取得是否套用此陳述式。
    /// </summary>
    public bool? ApplyCommand { get; } = applyCommand;
}
