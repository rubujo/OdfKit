using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Database;

/// <summary>
/// Represents an ODB data source setting description.
/// 表示 ODB 資料來源設定描述。
/// </summary>
/// <param name="name">The setting name. / 設定名稱。</param>
/// <param name="type">The setting value type. / 設定值型別。</param>
/// <param name="isList">Whether the setting value is a list. / 設定值是否為清單。</param>
/// <param name="values">The list of setting values. / 設定值清單。</param>
public sealed class OdfDatabaseDataSourceSettingInfo(
    string name,
    OdfDatabaseDataSourceSettingType type,
    bool? isList,
    IReadOnlyList<string> values)
{
    /// <summary>
    /// Gets the setting name.
    /// 取得設定名稱。
    /// </summary>
    public string Name { get; } = name ?? string.Empty;

    /// <summary>
    /// Gets the setting value type.
    /// 取得設定值型別。
    /// </summary>
    public OdfDatabaseDataSourceSettingType Type { get; } = type;

    /// <summary>
    /// Gets whether the setting value is a list.
    /// 取得設定值是否為清單。
    /// </summary>
    public bool? IsList { get; } = isList;

    /// <summary>
    /// Gets the list of setting values.
    /// 取得設定值清單。
    /// </summary>
    public IReadOnlyList<string> Values { get; } = values ?? [];
}
