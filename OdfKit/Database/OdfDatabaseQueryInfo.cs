using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Database;

/// <summary>
/// Represents an ODB query description.
/// 表示 ODB 查詢描述。
/// </summary>
/// <param name="name">The query name. / 查詢名稱。</param>
/// <param name="command">The query command or SQL content. / 查詢命令或 SQL 內容。</param>
/// <param name="title">The display title. / 顯示標題。</param>
/// <param name="description">The description text. / 描述文字。</param>
/// <param name="escapeProcessing">The SQL escape processing setting. / SQL escape processing 設定。</param>
public sealed class OdfDatabaseQueryInfo(
    string name,
    string command,
    string? title,
    string? description,
    bool? escapeProcessing)
{
    /// <summary>
    /// Gets the query name.
    /// 取得查詢名稱。
    /// </summary>
    public string Name { get; } = name ?? string.Empty;

    /// <summary>
    /// Gets the query command or SQL content.
    /// 取得查詢命令或 SQL 內容。
    /// </summary>
    public string Command { get; } = command ?? string.Empty;

    /// <summary>
    /// Gets the display title.
    /// 取得顯示標題。
    /// </summary>
    public string? Title { get; } = title;

    /// <summary>
    /// Gets the description text.
    /// 取得描述文字。
    /// </summary>
    public string? Description { get; } = description;

    /// <summary>
    /// Gets the SQL escape processing setting.
    /// 取得 SQL escape processing 設定。
    /// </summary>
    public bool? EscapeProcessing { get; } = escapeProcessing;
}

