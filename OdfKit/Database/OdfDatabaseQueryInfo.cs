using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Database;

/// <summary>
/// 表示 ODB 查詢描述。
/// </summary>
/// <param name="name">查詢名稱</param>
/// <param name="command">查詢命令或 SQL 內容</param>
/// <param name="title">顯示標題</param>
/// <param name="description">描述文字</param>
/// <param name="escapeProcessing">SQL escape processing 設定</param>
public sealed class OdfDatabaseQueryInfo(
    string name,
    string command,
    string? title,
    string? description,
    bool? escapeProcessing)
{
    /// <summary>
    /// 取得查詢名稱。
    /// </summary>
    public string Name { get; } = name ?? string.Empty;

    /// <summary>
    /// 取得查詢命令或 SQL 內容。
    /// </summary>
    public string Command { get; } = command ?? string.Empty;

    /// <summary>
    /// 取得顯示標題。
    /// </summary>
    public string? Title { get; } = title;

    /// <summary>
    /// 取得描述文字。
    /// </summary>
    public string? Description { get; } = description;

    /// <summary>
    /// 取得 SQL escape processing 設定。
    /// </summary>
    public bool? EscapeProcessing { get; } = escapeProcessing;
}

