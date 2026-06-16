using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Database;

/// <summary>
/// 表示 ODB 資料來源設定描述。
/// </summary>
/// <param name="name">設定名稱。</param>
/// <param name="type">設定值型別。</param>
/// <param name="isList">設定值是否為清單。</param>
/// <param name="values">設定值清單。</param>
public sealed class OdfDatabaseDataSourceSettingInfo(
    string name,
    OdfDatabaseDataSourceSettingType type,
    bool? isList,
    IReadOnlyList<string> values)
{
    /// <summary>
    /// 取得設定名稱。
    /// </summary>
    public string Name { get; } = name ?? string.Empty;

    /// <summary>
    /// 取得設定值型別。
    /// </summary>
    public OdfDatabaseDataSourceSettingType Type { get; } = type;

    /// <summary>
    /// 取得設定值是否為清單。
    /// </summary>
    public bool? IsList { get; } = isList;

    /// <summary>
    /// 取得設定值清單。
    /// </summary>
    public IReadOnlyList<string> Values { get; } = values ?? [];
}
