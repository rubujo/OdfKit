using System.Collections;
using System.Collections.Generic;
using System.Numerics;

namespace OdfKit.Compliance;

/// <summary>
/// 屬性消耗狀態（位元遮罩）的小型集合。元素通常只有個位數個狀態，線性搜尋的陣列比 <see cref="HashSet{T}"/>
/// 少配置一組 bucket／entry 陣列（驗證每個元素每個圖樣節點都會建立一個，是每列垃圾配置的主要來源之一）；
/// 狀態超過 <see cref="LargeThreshold"/> 個時改用 <see cref="HashSet{T}"/>，避免最壞情況的平方成本。
/// </summary>
internal sealed class AttributeStateSet : IEnumerable<BigInteger>
{
    private const int LargeThreshold = 24;

    private BigInteger[]? _items;
    private int _count;
    private HashSet<BigInteger>? _large;

    internal int Count => _large?.Count ?? _count;

    internal bool Add(BigInteger value)
    {
        if (_large is not null)
        {
            return _large.Add(value);
        }

        for (int i = 0; i < _count; i++)
        {
            if (_items![i] == value)
            {
                return false;
            }
        }

        if (_count == LargeThreshold)
        {
            _large = new HashSet<BigInteger>();
            for (int i = 0; i < _count; i++)
            {
                _large.Add(_items![i]);
            }

            _items = null;
            _count = 0;
            return _large.Add(value);
        }

        _items ??= new BigInteger[4];
        if (_count == _items.Length)
        {
            System.Array.Resize(ref _items, System.Math.Min(_items.Length * 2, LargeThreshold));
        }

        _items[_count++] = value;
        return true;
    }

    internal bool Contains(BigInteger value)
    {
        if (_large is not null)
        {
            return _large.Contains(value);
        }

        for (int i = 0; i < _count; i++)
        {
            if (_items![i] == value)
            {
                return true;
            }
        }

        return false;
    }

    internal bool Remove(BigInteger value)
    {
        if (_large is not null)
        {
            return _large.Remove(value);
        }

        for (int i = 0; i < _count; i++)
        {
            if (_items![i] == value)
            {
                _items[i] = _items[--_count];
                return true;
            }
        }

        return false;
    }

    public Enumerator GetEnumerator() => new(this);

    IEnumerator<BigInteger> IEnumerable<BigInteger>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public struct Enumerator : IEnumerator<BigInteger>
    {
        private readonly AttributeStateSet _set;
        private HashSet<BigInteger>.Enumerator _largeEnumerator;
        private int _index;
        private readonly bool _usesLarge;

        internal Enumerator(AttributeStateSet set)
        {
            _set = set;
            _index = -1;
            _usesLarge = set._large is not null;
            _largeEnumerator = _usesLarge ? set._large!.GetEnumerator() : default;
            Current = default;
        }

        public BigInteger Current { get; private set; }

        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            if (_usesLarge)
            {
                if (!_largeEnumerator.MoveNext())
                {
                    return false;
                }

                Current = _largeEnumerator.Current;
                return true;
            }

            _index++;
            if (_index >= _set._count)
            {
                return false;
            }

            Current = _set._items![_index];
            return true;
        }

        public void Reset() => throw new System.NotSupportedException();

        public void Dispose()
        {
        }
    }
}
