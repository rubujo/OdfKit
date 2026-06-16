using System;
using System.Collections.Generic;

namespace OdfKit.Compliance;

/// <summary>
/// RELAX NG 模式比對期間的結構描述狀態與循環參照防護。
/// </summary>
internal sealed class OdfSchemaPatternMatchContext
{
    private readonly HashSet<string> _activeReferences = new(StringComparer.Ordinal);

    /// <summary>
    /// 建立比對內容。
    /// </summary>
    /// <param name="schema">結構描述集</param>
    public OdfSchemaPatternMatchContext(OdfSchemaSet schema) => Schema = schema;

    /// <summary>
    /// 取得結構描述集。
    /// </summary>
    public OdfSchemaSet Schema { get; }

    /// <summary>
    /// 進入具名參照；若已於作用中堆疊則回傳 false（偵測循環參照）。
    /// </summary>
    public bool EnterReference(string referenceName) => _activeReferences.Add(referenceName);

    /// <summary>
    /// 離開具名參照。
    /// </summary>
    public void LeaveReference(string referenceName) => _activeReferences.Remove(referenceName);
}
