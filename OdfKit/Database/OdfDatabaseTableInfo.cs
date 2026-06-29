using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Database;

/// <summary>
/// Represents an ODB table description.
/// 表示 ODB 資料表描述。
/// </summary>
/// <param name="name">The table name. / 資料表名稱。</param>
/// <param name="command">The table command or source name. / 資料表命令或來源名稱。</param>
public sealed class OdfDatabaseTableInfo(string name, string? command)
{
    /// <summary>
    /// Gets the table name.
    /// 取得資料表名稱。
    /// </summary>
    public string Name { get; } = name ?? string.Empty;

    /// <summary>
    /// Gets the table command or source name.
    /// 取得資料表命令或來源名稱。
    /// </summary>
    public string? Command { get; } = command;
}

