using System;

namespace OdfKit.Compliance;

/// <summary>
/// 描述附加到 RELAX NG 資料模式的一個資料類型參數。
/// </summary>
/// <param name="name">參數名稱</param>
/// <param name="value">參數值</param>
public sealed class OdfSchemaDatatypeParameter(string name, string value)
{
    /// <summary>
    /// 取得參數名稱。
    /// </summary>
    public string Name { get; } = name ?? string.Empty;

    /// <summary>
    /// 取得參數值。
    /// </summary>
    public string Value { get; } = value ?? string.Empty;
}

