namespace OdfKit.Database;

/// <summary>
/// Represents a summary of a query sort or filter statement (<c>db:order-statement</c>／<c>db:filter-statement</c>).
/// 表示查詢的排序或篩選陳述式摘要（<c>db:order-statement</c>／<c>db:filter-statement</c>）。
/// </summary>
/// <param name="command">The statement command text. / 陳述式命令文字。</param>
/// <param name="applyCommand">Whether to apply this statement. / 是否套用此陳述式。</param>
public sealed class OdfDatabaseQueryStatementInfo(string command, bool? applyCommand)
{
    /// <summary>
    /// Gets the statement command text.
    /// 取得陳述式命令文字。
    /// </summary>
    public string Command { get; } = command;

    /// <summary>
    /// Gets whether to apply this statement.
    /// 取得是否套用此陳述式。
    /// </summary>
    public bool? ApplyCommand { get; } = applyCommand;
}
