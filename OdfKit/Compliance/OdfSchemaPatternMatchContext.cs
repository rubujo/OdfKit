using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace OdfKit.Compliance;

/// <summary>
/// RELAX NG 模式比對期間的結構描述狀態與循環參照防護。
/// </summary>
internal sealed class OdfSchemaPatternMatchContext
{
    private const int MaxRecursiveDepth = 128;
    private readonly HashSet<string> _activeReferences = new(StringComparer.Ordinal);
    private readonly int _recursiveDepth;
    private readonly Dictionary<(OdfSchemaPatternNode Node, XElement Element), bool> _elementMatchCache;

    /// <summary>
    /// 建立比對內容。
    /// </summary>
    /// <param name="schema">結構描述集</param>
    public OdfSchemaPatternMatchContext(OdfSchemaSet schema)
        : this(schema, 0, new Dictionary<(OdfSchemaPatternNode, XElement), bool>())
    {
    }

    private OdfSchemaPatternMatchContext(
        OdfSchemaSet schema,
        int recursiveDepth,
        Dictionary<(OdfSchemaPatternNode Node, XElement Element), bool> elementMatchCache)
    {
        Schema = schema;
        _recursiveDepth = recursiveDepth;
        _elementMatchCache = elementMatchCache;
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
            : new OdfSchemaPatternMatchContext(Schema, _recursiveDepth + 1, _elementMatchCache);

    /// <summary>
    /// 建立比對子元素用的新內容：循環參照防護從空白開始，但共用「模式節點對元素」的比對快取。
    /// </summary>
    public OdfSchemaPatternMatchContext CreateChildContext() => new(Schema, 0, _elementMatchCache);

    /// <summary>
    /// 取得先前記錄的「模式節點對元素」比對結果。
    /// </summary>
    /// <remarks>
    /// 元素是否符合某個模式節點只取決於該元素的子樹，與它在父層內容中的位置無關。重複與選擇模式
    /// （例如 <c>table-row</c> 的巢狀 <c>oneOrMore</c>）會從許多起點重複嘗試同一個子元素；沒有此快取時，
    /// 每次嘗試都重新驗證整個子樹，使列數 N 的工作表驗證時間呈 O(N²) 成長（2,000 列約 70 秒）。
    /// </remarks>
    public bool TryGetCachedElementMatch(OdfSchemaPatternNode node, XElement element, out bool result) =>
        _elementMatchCache.TryGetValue((node, element), out result);

    /// <summary>
    /// 記錄「模式節點對元素」的比對結果。
    /// </summary>
    public void CacheElementMatch(OdfSchemaPatternNode node, XElement element, bool result) =>
        _elementMatchCache[(node, element)] = result;

    /// <summary>
    /// 離開具名參照。
    /// </summary>
    public void LeaveReference(string referenceName) => _activeReferences.Remove(referenceName);
}
