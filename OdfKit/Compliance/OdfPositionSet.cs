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
/// 位置一律為非負整數；集合只供讀取比對結果，共用的實例不得修改。
/// </remarks>
internal sealed class OdfPositionSet : IEnumerable<int>
{
    private ulong[] _words = [];
    private int _baseWord;
    private int _count;

    /// <summary>
    /// 取得集合中的位置數量。
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// 加入位置；已存在時傳回 <see langword="false"/>。
    /// </summary>
    public bool Add(int position)
    {
        if (position < 0)
        {
            ThrowNegativePosition();
        }

        int word = position >> 6;
        ulong bit = 1UL << (position & 63);
        if (_words.Length == 0)
        {
            _baseWord = word;
            _words = new ulong[2];
        }
        else if (word < _baseWord)
        {
            int shift = _baseWord - word;
            var grown = new ulong[_words.Length + shift];
            Array.Copy(_words, 0, grown, shift, _words.Length);
            _words = grown;
            _baseWord = word;
        }
        else if (word - _baseWord >= _words.Length)
        {
            int required = word - _baseWord + 1;
            Array.Resize(ref _words, Math.Max(required, _words.Length * 2));
        }

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
    /// 把另一個集合的所有位置併入此集合（逐字組做位元 OR）。
    /// </summary>
    public void UnionWith(OdfPositionSet other)
    {
        if (other._count == 0 || ReferenceEquals(this, other))
        {
            return;
        }

        ulong[] source = other._words;
        int lowWord = other._baseWord;
        int highWord = other._baseWord + source.Length - 1;
        if (_words.Length == 0)
        {
            _baseWord = lowWord;
            _words = new ulong[source.Length];
        }
        else
        {
            int currentHigh = _baseWord + _words.Length - 1;
            int newLow = Math.Min(_baseWord, lowWord);
            int newHigh = Math.Max(currentHigh, highWord);
            if (newLow < _baseWord || newHigh > currentHigh)
            {
                var grown = new ulong[newHigh - newLow + 1];
                Array.Copy(_words, 0, grown, _baseWord - newLow, _words.Length);
                _words = grown;
                _baseWord = newLow;
            }
        }

        int offset = lowWord - _baseWord;
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
        if (position < 0 || _words.Length == 0)
        {
            return false;
        }

        int word = (position >> 6) - _baseWord;
        return word >= 0 && word < _words.Length && (_words[word] & (1UL << (position & 63))) != 0;
    }

    /// <summary>
    /// 複製此集合；複本可獨立修改。
    /// </summary>
    public OdfPositionSet Clone()
    {
        var copy = new OdfPositionSet
        {
            _words = (ulong[])_words.Clone(),
            _baseWord = _baseWord,
            _count = _count,
        };
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

        for (int i = 0; i < other._words.Length; i++)
        {
            ulong needed = other._words[i];
            if (needed == 0)
            {
                continue;
            }

            int index = other._baseWord + i - _baseWord;
            if (index < 0 || index >= _words.Length || (needed & ~_words[index]) != 0)
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
        for (int i = 0; i < _words.Length; i++)
        {
            ulong value = _words[i];
            int otherIndex = _baseWord + i - other._baseWord;
            if (otherIndex >= 0 && otherIndex < other._words.Length)
            {
                value &= ~other._words[otherIndex];
            }

            while (value != 0)
            {
                result.Add(((_baseWord + i) << 6) + TrailingZeroCount(value));
                value &= value - 1;
            }
        }

        return result;
    }

    /// <summary>
    /// 傳回依遞增順序列舉位置的列舉器（結構型別，避免每次列舉配置物件）。
    /// </summary>
    public Enumerator GetEnumerator() => new(_words, _baseWord);

    IEnumerator<int> IEnumerable<int>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// 位置集合的結構列舉器。
    /// </summary>
    internal struct Enumerator : IEnumerator<int>
    {
        private readonly ulong[] _words;
        private readonly int _baseWord;
        private int _index;
        private ulong _value;
        private int _current;

        internal Enumerator(ulong[] words, int baseWord)
        {
            _words = words;
            _baseWord = baseWord;
            _index = -1;
            _value = 0;
            _current = -1;
        }

        public readonly int Current => _current;

        readonly object IEnumerator.Current => _current;

        public bool MoveNext()
        {
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

        public void Reset()
        {
            _index = -1;
            _value = 0;
            _current = -1;
        }

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
