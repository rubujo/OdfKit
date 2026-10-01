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
    /// 傳回依遞增順序列舉位置的列舉器。
    /// </summary>
    public IEnumerator<int> GetEnumerator()
    {
        ulong[] words = _words;
        int baseWord = _baseWord;
        for (int i = 0; i < words.Length; i++)
        {
            ulong value = words[i];
            while (value != 0)
            {
                int bit = TrailingZeroCount(value);
                yield return ((baseWord + i) << 6) + bit;
                value &= value - 1;
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

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
