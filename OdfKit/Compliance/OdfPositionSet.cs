using System;
using System.Collections;
using System.Collections.Generic;

namespace OdfKit.Compliance;

/// <summary>
/// 以位元集合表示的子元素位置集合（內部協作者）。
/// </summary>
/// <remarks>
/// RELAX NG 內容比對會反覆建立「由某個起點可到達的子元素位置」集合。巢狀重複（例如 <c>table:table</c> 的
/// <c>oneOrMore(oneOrMore(table-row))</c>）的每一次展開都可能回傳與列數同階的集合，改用雜湊集合時
/// 每個元素都要付出雜湊、配置與擴容的成本，20,000 列的工作表需數百秒。位元集合每個元素只需
/// 一次位元運算，且記憶體只與位置的跨度（而不是絕對值）成正比。
/// 由各個起點記憶下來的集合多半是連續的後綴區間（例如第 5 列起可到達第 6 列至最後一列）；
/// 連續區間只記錄起點與數量，不配置位元圖，記憶體不隨跨度成長。區間被加入不相鄰的位置時才轉成位元圖。
/// 位置一律為非負整數；集合只供讀取比對結果，共用的實例不得修改。
/// </remarks>
internal sealed class OdfPositionSet : IEnumerable<int>
{
    // 三種表示：空集合（沒有字組、非區間）、連續區間（_range 為 true，只用 _low 與 _count）、位元圖。
    private ulong[] _words = [];
    private int _baseWord;
    private int _count;
    private bool _range;
    private int _low;

    /// <summary>
    /// 取得集合中的位置數量。
    /// </summary>
    public int Count => _count;

    // 區間表示的最大位置；只在 _range 為 true 時有意義。
    private int RangeHigh => _low + _count - 1;

    /// <summary>
    /// 加入位置；已存在時傳回 <see langword="false"/>。
    /// </summary>
    public bool Add(int position)
    {
        if (position < 0)
        {
            ThrowNegativePosition();
        }

        if (_range)
        {
            if (position >= _low && position <= RangeHigh)
            {
                return false;
            }

            if (position == RangeHigh + 1)
            {
                _count++;
                return true;
            }

            if (position == _low - 1)
            {
                _low--;
                _count++;
                return true;
            }

            ConvertToBitmap();
        }
        else if (_words.Length == 0)
        {
            _range = true;
            _low = position;
            _count = 1;
            return true;
        }

        int word = position >> 6;
        ulong bit = 1UL << (position & 63);
        GrowToCover(word, word);

        ref ulong slot = ref _words[word - _baseWord];
        if ((slot & bit) != 0)
        {
            return false;
        }

        slot |= bit;
        _count++;
        return true;
    }

    /// <summary>
    /// 把另一個集合的所有位置併入此集合。
    /// </summary>
    public void UnionWith(OdfPositionSet other)
    {
        if (other._count == 0 || ReferenceEquals(this, other))
        {
            return;
        }

        if (_count == 0)
        {
            CopyFrom(other);
            return;
        }

        if (_range)
        {
            // 兩個區間重疊或相鄰時合併成一個區間，否則轉成位元圖。
            if (other._range && other._low <= RangeHigh + 1 && _low <= other.RangeHigh + 1)
            {
                int high = Math.Max(RangeHigh, other.RangeHigh);
                _low = Math.Min(_low, other._low);
                _count = high - _low + 1;
                return;
            }

            ConvertToBitmap();
        }

        if (other._range)
        {
            AddRangeBits(other._low, other.RangeHigh);
            return;
        }

        ulong[] source = other._words;
        GrowToCover(other._baseWord, other._baseWord + source.Length - 1);
        int offset = other._baseWord - _baseWord;
        for (int i = 0; i < source.Length; i++)
        {
            ulong incoming = source[i];
            if (incoming == 0)
            {
                continue;
            }

            ulong existing = _words[offset + i];
            ulong merged = existing | incoming;
            if (merged != existing)
            {
                _count += PopCount(merged) - PopCount(existing);
                _words[offset + i] = merged;
            }
        }
    }

    private void CopyFrom(OdfPositionSet other)
    {
        _range = other._range;
        _low = other._low;
        _count = other._count;
        _baseWord = other._baseWord;
        _words = other._range ? [] : (ulong[])other._words.Clone();
    }

    // 讓位元圖涵蓋指定的字組範圍（含兩端）；空集合時直接配置。
    private void GrowToCover(int lowWord, int highWord)
    {
        if (_words.Length == 0)
        {
            _baseWord = lowWord;
            _words = new ulong[highWord - lowWord + 1];
            return;
        }

        int currentHigh = _baseWord + _words.Length - 1;
        int newLow = Math.Min(_baseWord, lowWord);
        int newHigh = Math.Max(currentHigh, highWord);
        if (newLow == _baseWord && newHigh == currentHigh)
        {
            return;
        }

        // 往高位擴充時成倍成長，逐一附加位置時不必每次重新配置。
        int length = newHigh - newLow + 1;
        if (newLow == _baseWord)
        {
            length = Math.Max(length, _words.Length * 2);
        }

        var grown = new ulong[length];
        Array.Copy(_words, 0, grown, _baseWord - newLow, _words.Length);
        _words = grown;
        _baseWord = newLow;
    }

    private void ConvertToBitmap()
    {
        int low = _low;
        int high = RangeHigh;
        _range = false;
        _count = 0;
        _words = [];
        AddRangeBits(low, high);
    }

    // 把 [low, high] 的所有位置設為已存在（位元圖表示），並更新數量。
    private void AddRangeBits(int low, int high)
    {
        int lowWord = low >> 6;
        int highWord = high >> 6;
        GrowToCover(lowWord, highWord);
        for (int word = lowWord; word <= highWord; word++)
        {
            ulong mask = RangeMask(word, low, high);
            ref ulong slot = ref _words[word - _baseWord];
            ulong merged = slot | mask;
            if (merged != slot)
            {
                _count += PopCount(merged) - PopCount(slot);
                slot = merged;
            }
        }
    }

    // 絕對字組 word 中屬於 [low, high] 的位元遮罩。
    private static ulong RangeMask(int word, int low, int high)
    {
        int wordStart = word << 6;
        int from = Math.Max(low, wordStart) - wordStart;
        int to = Math.Min(high, wordStart + 63) - wordStart;
        if (to < from)
        {
            return 0;
        }

        ulong mask = to == 63 ? ulong.MaxValue : (1UL << (to + 1)) - 1;
        return mask & (ulong.MaxValue << from);
    }

    // 以絕對字組編號取得此集合在該字組的位元（任一種表示）。
    private ulong WordAt(int word)
    {
        if (_range)
        {
            return RangeMask(word, _low, RangeHigh);
        }

        int index = word - _baseWord;
        return index >= 0 && index < _words.Length ? _words[index] : 0;
    }

    private int LowWord => _range ? _low >> 6 : _baseWord;

    private int HighWord => _range ? RangeHigh >> 6 : _baseWord + _words.Length - 1;

    private static int PopCount(ulong value)
    {
#if NETSTANDARD2_0
        value -= (value >> 1) & 0x5555555555555555UL;
        value = (value & 0x3333333333333333UL) + ((value >> 2) & 0x3333333333333333UL);
        value = (value + (value >> 4)) & 0x0F0F0F0F0F0F0F0FUL;
        return (int)((value * 0x0101010101010101UL) >> 56);
#else
        return System.Numerics.BitOperations.PopCount(value);
#endif
    }

    /// <summary>
    /// 判斷集合是否含有指定位置。
    /// </summary>
    public bool Contains(int position)
    {
        if (position < 0 || _count == 0)
        {
            return false;
        }

        if (_range)
        {
            return position >= _low && position <= RangeHigh;
        }

        int word = (position >> 6) - _baseWord;
        return word >= 0 && word < _words.Length && (_words[word] & (1UL << (position & 63))) != 0;
    }

    /// <summary>
    /// 複製此集合；複本可獨立修改。
    /// </summary>
    public OdfPositionSet Clone()
    {
        var copy = new OdfPositionSet();
        copy.CopyFrom(this);
        return copy;
    }

    /// <summary>
    /// 判斷此集合是否包含另一個集合的所有位置。
    /// </summary>
    public bool IsSupersetOf(OdfPositionSet other)
    {
        if (other._count == 0)
        {
            return true;
        }

        if (_count < other._count)
        {
            return false;
        }

        if (_range && other._range)
        {
            return other._low >= _low && other.RangeHigh <= RangeHigh;
        }

        int high = other.HighWord;
        for (int word = other.LowWord; word <= high; word++)
        {
            ulong needed = other.WordAt(word);
            if (needed != 0 && (needed & ~WordAt(word)) != 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 傳回在此集合但不在另一個集合內的位置（遞增順序）。
    /// </summary>
    public List<int> PositionsNotIn(OdfPositionSet other)
    {
        var result = new List<int>();
        if (_count == 0)
        {
            return result;
        }

        if (_range && other._range)
        {
            // 兩個區間的差最多是左右兩段；後綴區間逐一遞減時只有少數幾個位置。
            int high = RangeHigh;
            int otherHigh = other.RangeHigh;
            if (other._count == 0 || otherHigh < _low || other._low > high)
            {
                for (int position = _low; position <= high; position++)
                {
                    result.Add(position);
                }

                return result;
            }

            for (int position = _low; position < other._low; position++)
            {
                result.Add(position);
            }

            for (int position = otherHigh + 1; position <= high; position++)
            {
                result.Add(position);
            }

            return result;
        }

        int highWord = HighWord;
        for (int word = LowWord; word <= highWord; word++)
        {
            ulong value = WordAt(word) & ~other.WordAt(word);
            while (value != 0)
            {
                result.Add((word << 6) + TrailingZeroCount(value));
                value &= value - 1;
            }
        }

        return result;
    }

    /// <summary>
    /// 傳回依遞增順序列舉位置的列舉器（結構型別，避免每次列舉配置物件）。
    /// </summary>
    public Enumerator GetEnumerator() =>
        _range ? new Enumerator(_low, RangeHigh) : new Enumerator(_words, _baseWord);

    IEnumerator<int> IEnumerable<int>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// 位置集合的結構列舉器。
    /// </summary>
    internal struct Enumerator : IEnumerator<int>
    {
        private readonly ulong[] _words;
        private readonly int _baseWord;
        private readonly bool _isRange;
        private readonly int _rangeEnd;
        private int _index;
        private ulong _value;
        private int _current;

        internal Enumerator(ulong[] words, int baseWord)
        {
            _words = words;
            _baseWord = baseWord;
            _isRange = false;
            _rangeEnd = -1;
            _index = -1;
            _value = 0;
            _current = -1;
        }

        internal Enumerator(int low, int high)
        {
            _words = [];
            _baseWord = 0;
            _isRange = true;
            _rangeEnd = high;
            _index = low - 1;
            _value = 0;
            _current = -1;
        }

        public readonly int Current => _current;

        readonly object IEnumerator.Current => _current;

        public bool MoveNext()
        {
            if (_isRange)
            {
                if (_index >= _rangeEnd)
                {
                    return false;
                }

                _index++;
                _current = _index;
                return true;
            }

            while (_value == 0)
            {
                _index++;
                if (_index >= _words.Length)
                {
                    return false;
                }

                _value = _words[_index];
            }

            _current = ((_baseWord + _index) << 6) + TrailingZeroCount(_value);
            _value &= _value - 1;
            return true;
        }

        public void Reset() => throw new NotSupportedException();

        public readonly void Dispose()
        {
        }
    }

    // Add 在比對的內層迴圈中被大量呼叫，例外集中在不內嵌的輔助方法，讓熱路徑只剩一次比較。
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void ThrowNegativePosition() =>
        throw new ArgumentOutOfRangeException("position");

    private static int TrailingZeroCount(ulong value)
    {
#if NETSTANDARD2_0
        int count = 0;
        while ((value & 1UL) == 0)
        {
            value >>= 1;
            count++;
        }

        return count;
#else
        return System.Numerics.BitOperations.TrailingZeroCount(value);
#endif
    }
}
