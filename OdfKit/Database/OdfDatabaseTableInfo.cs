using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Database;

/// <summary>
/// 表示 ODB 資料表描述。
/// </summary>
/// <param name="name">資料表名稱。</param>
/// <param name="command">資料表命令或來源名稱。</param>
public sealed class OdfDatabaseTableInfo(string name, string? command)
{
    /// <summary>
    /// 取得資料表名稱。
    /// </summary>
    public string Name { get; } = name ?? string.Empty;

    /// <summary>
    /// 取得資料表命令或來源名稱。
    /// </summary>
    public string? Command { get; } = command;
}

