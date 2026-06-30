using System;
using System.Collections.Generic;

namespace OdfKit.Compliance;

/// <summary>
/// RELAX NG 模式比對期間的結構描述狀態與循環參照防護。
/// </summary>
internal sealed class OdfSchemaPatternMatchContext
{
    private const int MaxRecursiveDepth = 128;
    private readonly HashSet<string> _activeReferences = new(StringComparer.Ordinal);
    private readonly int _recursiveDepth;

    /// <summary>
    /// 建立比對內容。
    /// </summary>
    /// <param name="schema">結構描述集</param>
    public OdfSchemaPatternMatchContext(OdfSchemaSet schema) : this(schema, 0)
    {
    }

    private OdfSchemaPatternMatchContext(OdfSchemaSet schema, int recursiveDepth)
    {
        Schema = schema;
        _recursiveDepth = recursiveDepth;
    }

    /// <summary>
    /// 取得結構描述集。
    /// </summary>
    public OdfSchemaSet Schema { get; }

    /// <summary>
    /// 進入具名參照；若已於作用中堆疊則回傳 false（偵測循環參照）。
    /// </summary>
    public bool EnterReference(string referenceName) => _activeReferences.Add(referenceName);

    /// <summary>
    /// 建立允許合法巢狀 RNG 參照繼續比對的子內容。
    /// </summary>
    public OdfSchemaPatternMatchContext? CreateRecursiveContext() =>
        _recursiveDepth >= MaxRecursiveDepth
            ? null
            : new OdfSchemaPatternMatchContext(Schema, _recursiveDepth + 1);

    /// <summary>
    /// 離開具名參照。
    /// </summary>
    public void LeaveReference(string referenceName) => _activeReferences.Remove(referenceName);
}
