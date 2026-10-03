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
    // 每個子元素都會建立一個比對內容，多數是沒有子層的葉節點；以下集合與字典延遲到第一次使用才配置。
    private HashSet<string>? _activeReferences;
    private readonly int _recursiveDepth;
    private readonly Dictionary<(OdfSchemaPatternNode Node, XElement Element), bool> _elementMatchCache;
    private readonly Dictionary<OdfSchemaPatternNode, OdfSchemaPatternNode?> _strippedRootCache;
    private Dictionary<int, OdfPositionSet>? _singletonSets;
    private Dictionary<ReachKey, OdfPositionSet>? _reachMemo;
    private Dictionary<ReachKey, OdfPositionSet>? _contentMemo;
    private const int MaxReachMemoEntries = 2_000_000;
    private Dictionary<(object Nodes, int Position), SequenceStepEntry>? _sequenceMemo;

    /// <summary>
    /// 序列中某個節點上一次推進的輸入與輸出（輸入位置集合 → 各位置比對結果的聯集）。
    /// </summary>
    internal sealed class SequenceStepEntry(object childElements, OdfPositionSet input, OdfPositionSet output)
    {
        internal object ChildElements { get; } = childElements;

        internal OdfPositionSet Input { get; } = input;

        internal OdfPositionSet Output { get; } = output;
    }

    private readonly struct ReachKey(OdfSchemaPatternNode node, object childElements, int start) : IEquatable<ReachKey>
    {
        private readonly OdfSchemaPatternNode _node = node;
        private readonly object _childElements = childElements;
        private readonly int _start = start;

        public bool Equals(ReachKey other) =>
            ReferenceEquals(_node, other._node) &&
            ReferenceEquals(_childElements, other._childElements) &&
            _start == other._start;

        public override bool Equals(object? obj) => obj is ReachKey other && Equals(other);

        public override int GetHashCode() =>
            System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(_node) * 31 +
            System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(_childElements) * 17 +
            _start;
    }

    private static bool TryGet<TKey, TValue>(Dictionary<TKey, TValue>? map, TKey key, out TValue value)
        where TKey : notnull
    {
        if (map is not null && map.TryGetValue(key, out TValue? found))
        {
            value = found;
            return true;
        }

        value = default!;
        return false;
    }

    /// <summary>
    /// 取得循環參照防護攔下重複進入的累計次數。
    /// </summary>
    /// <remarks>
    /// 比對結果在防護介入時可能被截斷，只有計數在計算前後相同（防護從未介入）的結果才可安全記憶並重用。
    /// </remarks>
    public long GuardHits { get; private set; }

    /// <summary>
    /// 取得先前記錄的「由某位置起重複比對（至少一次）可到達的位置集合」；回傳的集合唯讀。
    /// </summary>
    public bool TryGetReach(OdfSchemaPatternNode node, object childElements, int start, out OdfPositionSet reach) =>
        TryGet(_reachMemo, new ReachKey(node, childElements, start), out reach);

    /// <summary>
    /// 取得先前記錄的「模式節點由某位置起比對子元素內容」的結果集；回傳的集合唯讀。
    /// </summary>
    public bool TryGetContentMatch(OdfSchemaPatternNode node, object childElements, int start, out OdfPositionSet matches) =>
        TryGet(_contentMemo, new ReachKey(node, childElements, start), out matches);

    /// <summary>
    /// 記錄「模式節點由某位置起比對子元素內容」的結果集；超過上限後不再記錄以限制記憶體。
    /// </summary>
    public void StoreContentMatch(OdfSchemaPatternNode node, object childElements, int start, OdfPositionSet matches)
    {
        _contentMemo ??= new();
        if (_contentMemo.Count < MaxReachMemoEntries)
        {
            _contentMemo[new ReachKey(node, childElements, start)] = matches;
        }
    }

    /// <summary>
    /// 取得序列節點上一次推進的記錄；子元素清單不同時視為沒有記錄。
    /// </summary>
    public bool TryGetSequenceStep(object nodes, int position, object childElements, out SequenceStepEntry entry) =>
        TryGet(_sequenceMemo, (nodes, position), out entry) && ReferenceEquals(entry.ChildElements, childElements);

    /// <summary>
    /// 記錄序列節點這一次推進的輸入與輸出，每個節點只保留最近一次，記憶體隨節點數而不隨輸入數成長。
    /// </summary>
    public void StoreSequenceStep(object nodes, int position, object childElements, OdfPositionSet input, OdfPositionSet output) =>
        (_sequenceMemo ??= new())[(nodes, position)] = new SequenceStepEntry(childElements, input, output);

    /// <summary>
    /// 記錄「由某位置起重複比對（至少一次）可到達的位置集合」；超過上限後不再記錄以限制記憶體。
    /// </summary>
    public void StoreReach(OdfSchemaPatternNode node, object childElements, int start, OdfPositionSet reach)
    {
        _reachMemo ??= new();
        if (_reachMemo.Count < MaxReachMemoEntries)
        {
            _reachMemo[new ReachKey(node, childElements, start)] = reach;
        }
    }

    /// <summary>
    /// 建立比對內容。
    /// </summary>
    /// <param name="schema">結構描述集</param>
    public OdfSchemaPatternMatchContext(OdfSchemaSet schema)
        : this(
            schema,
            0,
            new Dictionary<(OdfSchemaPatternNode, XElement), bool>(),
            new Dictionary<OdfSchemaPatternNode, OdfSchemaPatternNode?>())
    {
    }

    private OdfSchemaPatternMatchContext(
        OdfSchemaSet schema,
        int recursiveDepth,
        Dictionary<(OdfSchemaPatternNode Node, XElement Element), bool> elementMatchCache,
        Dictionary<OdfSchemaPatternNode, OdfSchemaPatternNode?> strippedRootCache)
    {
        Schema = schema;
        _recursiveDepth = recursiveDepth;
        _elementMatchCache = elementMatchCache;
        _strippedRootCache = strippedRootCache;
    }

    /// <summary>
    /// 取得結構描述集。
    /// </summary>
    public OdfSchemaSet Schema { get; }

    /// <summary>
    /// 進入具名參照；若已於作用中堆疊則回傳 false（偵測循環參照）。
    /// </summary>
    public bool EnterReference(string referenceName)
    {
        _activeReferences ??= new HashSet<string>(StringComparer.Ordinal);
        if (_activeReferences.Add(referenceName))
        {
            return true;
        }

        GuardHits++;
        return false;
    }

    /// <summary>
    /// 建立允許合法巢狀 RNG 參照繼續比對的子內容。
    /// </summary>
    public OdfSchemaPatternMatchContext? CreateRecursiveContext() =>
        _recursiveDepth >= MaxRecursiveDepth
            ? null
            : new OdfSchemaPatternMatchContext(Schema, _recursiveDepth + 1, _elementMatchCache, _strippedRootCache);

    /// <summary>
    /// 建立比對子元素用的新內容：循環參照防護從空白開始，但共用「模式節點對元素」的比對快取。
    /// </summary>
    public OdfSchemaPatternMatchContext CreateChildContext() => new(Schema, 0, _elementMatchCache, _strippedRootCache);

    /// <summary>
    /// 取得（必要時建立）指定定義根節點剝離屬性模式後的結果。
    /// </summary>
    /// <remarks>
    /// 剝離會為每個群組、選擇與重複節點建立新物件；每次比對都重做，不但配置大量物件，
    /// 也使以節點物件為鍵的所有快取失效。結果只取決於定義本身，因此可在整次驗證共用。
    /// </remarks>
    public OdfSchemaPatternNode? GetStrippedRoot(
        OdfSchemaPatternNode root,
        Func<OdfSchemaPatternNode, OdfSchemaPatternMatchContext, OdfSchemaPatternNode?> strip)
    {
        if (!_strippedRootCache.TryGetValue(root, out OdfSchemaPatternNode? stripped))
        {
            stripped = strip(root, this);
            _strippedRootCache[root] = stripped;
        }

        return stripped;
    }

    /// <summary>
    /// 取得只含單一位置的唯讀結果集（呼叫端不得修改）。
    /// </summary>
    public OdfPositionSet GetSingletonSet(int position)
    {
        _singletonSets ??= new();
        if (!_singletonSets.TryGetValue(position, out OdfPositionSet? set))
        {
            set = new OdfPositionSet { position };
            _singletonSets[position] = set;
        }

        return set;
    }

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
    public void LeaveReference(string referenceName) => _activeReferences?.Remove(referenceName);
}
