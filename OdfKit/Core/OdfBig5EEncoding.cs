using System;
using System.Collections.Generic;
using System.Text;
using OdfKit.Compliance;

namespace OdfKit.Core;

/// <summary>
/// Provides a table-driven Big5E character encoding.
/// 提供由對照表驅動的 Big5E 字元編碼。
/// </summary>
/// <remarks>
/// The CLI does not provide this encoding. Build it from CNS mapping data by calling <see cref="OdfCns11643MappingTable.Parse"/> for two tables, then <see cref="OdfCns11643MappingTable.JoinOnCns"/>, and finally <see cref="Create"/>.
/// CLI 未內建此編碼。使用者可針對兩份全字庫對照表呼叫 <see cref="OdfCns11643MappingTable.Parse"/>，再依序呼叫 <see cref="OdfCns11643MappingTable.JoinOnCns"/> 與 <see cref="Create"/> 自行建立。
/// </remarks>
public sealed class OdfBig5EEncoding : Encoding
{
    private readonly IReadOnlyDictionary<int, int> _unicodeToBig5E;
    private readonly IReadOnlyDictionary<int, int> _big5EToUnicode;

    private OdfBig5EEncoding(
        IReadOnlyDictionary<int, int> unicodeToBig5E,
        IReadOnlyDictionary<int, int> big5EToUnicode)
    {
        _unicodeToBig5E = unicodeToBig5E;
        _big5EToUnicode = big5EToUnicode;
    }

    /// <summary>
    /// Gets the human-readable name of this encoding.
    /// 取得此編碼的人類可讀名稱。
    /// </summary>
    public override string EncodingName => "big5e";

    /// <summary>
    /// Gets the web name registered for this encoding.
    /// 取得此編碼所登錄的網頁名稱。
    /// </summary>
    public override string WebName => "big5e";

    /// <summary>
    /// Creates a Big5E encoding from a Unicode-to-Big5E mapping.
    /// 從 Unicode 至 Big5E 對應表建立 Big5E 編碼。
    /// </summary>
    /// <param name="unicodeToBig5E">The Unicode-scalar-to-Big5E-code mapping. / Unicode 純量值至 Big5E 碼的對應表。</param>
    /// <returns>A table-driven Big5E encoding. / 由對照表驅動的 Big5E 編碼。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="unicodeToBig5E"/> is <see langword="null"/>. / <paramref name="unicodeToBig5E"/> 為 <see langword="null"/>。</exception>
    /// <exception cref="ArgumentException">The mapping is empty or contains an invalid Unicode scalar or Big5E code. / 對應表為空，或包含無效的 Unicode 純量值或 Big5E 碼。</exception>
    public static OdfBig5EEncoding Create(IReadOnlyDictionary<int, int> unicodeToBig5E)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(unicodeToBig5E, nameof(unicodeToBig5E));

        if (unicodeToBig5E.Count == 0)
        {
            throw new ArgumentException(
                OdfLocalizer.GetMessage("Err_OdfBig5EEncoding_MappingEmpty"),
                nameof(unicodeToBig5E));
        }

        var forward = new Dictionary<int, int>();
        var reverse = new Dictionary<int, int>();
        foreach (KeyValuePair<int, int> pair in unicodeToBig5E)
        {
            if (!IsUnicodeScalar(pair.Key) || pair.Key <= 0x7F || !IsValidBig5Code(pair.Value))
            {
                throw new ArgumentException(
                    OdfLocalizer.GetMessage("Err_OdfBig5EEncoding_InvalidBig5Code"),
                    nameof(unicodeToBig5E));
            }

            forward[pair.Key] = pair.Value;
            if (!reverse.TryGetValue(pair.Value, out int canonicalCodePoint) || pair.Key < canonicalCodePoint)
            {
                reverse[pair.Value] = pair.Key;
            }
        }

        return new OdfBig5EEncoding(forward, reverse);
    }

    /// <summary>
    /// Calculates the number of bytes produced by encoding a character array segment.
    /// 計算字元陣列區段編碼後產生的位元組數量。
    /// </summary>
    /// <param name="chars">The character array to encode. / 要編碼的字元陣列。</param>
    /// <param name="index">The starting character index. / 起始字元索引。</param>
    /// <param name="count">The number of characters to encode. / 要編碼的字元數量。</param>
    /// <returns>The required byte count. / 所需的位元組數量。</returns>
    public override int GetByteCount(char[] chars, int index, int count)
    {
        ValidateArraySegment(chars, index, count, nameof(chars), nameof(index), nameof(count));
        return Encode(chars, index, count, null, 0, EncoderFallback);
    }

    /// <summary>
    /// Encodes a character array segment into a byte array.
    /// 將字元陣列區段編碼至位元組陣列。
    /// </summary>
    /// <param name="chars">The character array to encode. / 要編碼的字元陣列。</param>
    /// <param name="charIndex">The starting character index. / 起始字元索引。</param>
    /// <param name="charCount">The number of characters to encode. / 要編碼的字元數量。</param>
    /// <param name="bytes">The destination byte array. / 目的位元組陣列。</param>
    /// <param name="byteIndex">The starting destination index. / 目的陣列的起始索引。</param>
    /// <returns>The number of bytes written. / 寫入的位元組數量。</returns>
    public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex)
    {
        ValidateArraySegment(chars, charIndex, charCount, nameof(chars), nameof(charIndex), nameof(charCount));
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(bytes, nameof(bytes));

        if (byteIndex < 0 || byteIndex > bytes.Length)
            throw new ArgumentOutOfRangeException(nameof(byteIndex));

        return Encode(chars, charIndex, charCount, bytes, byteIndex, EncoderFallback);
    }

    /// <summary>
    /// Calculates the number of characters produced by decoding a byte array segment.
    /// 計算位元組陣列區段解碼後產生的字元數量。
    /// </summary>
    /// <param name="bytes">The byte array to decode. / 要解碼的位元組陣列。</param>
    /// <param name="index">The starting byte index. / 起始位元組索引。</param>
    /// <param name="count">The number of bytes to decode. / 要解碼的位元組數量。</param>
    /// <returns>The required character count. / 所需的字元數量。</returns>
    public override int GetCharCount(byte[] bytes, int index, int count)
    {
        ValidateArraySegment(bytes, index, count, nameof(bytes), nameof(index), nameof(count));
        return Decode(bytes, index, count, null, 0, DecoderFallback);
    }

    /// <summary>
    /// Decodes a byte array segment into a character array.
    /// 將位元組陣列區段解碼至字元陣列。
    /// </summary>
    /// <param name="bytes">The byte array to decode. / 要解碼的位元組陣列。</param>
    /// <param name="byteIndex">The starting byte index. / 起始位元組索引。</param>
    /// <param name="byteCount">The number of bytes to decode. / 要解碼的位元組數量。</param>
    /// <param name="chars">The destination character array. / 目的字元陣列。</param>
    /// <param name="charIndex">The starting destination index. / 目的陣列的起始索引。</param>
    /// <returns>The number of characters written. / 寫入的字元數量。</returns>
    public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
    {
        ValidateArraySegment(bytes, byteIndex, byteCount, nameof(bytes), nameof(byteIndex), nameof(byteCount));
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(chars, nameof(chars));

        if (charIndex < 0 || charIndex > chars.Length)
            throw new ArgumentOutOfRangeException(nameof(charIndex));

        return Decode(bytes, byteIndex, byteCount, chars, charIndex, DecoderFallback);
    }

    /// <summary>
    /// Creates a stateful encoder for block-based conversion.
    /// 建立用於區塊式轉換的具狀態編碼器。
    /// </summary>
    /// <returns>A stateful Big5E encoder. / 具狀態的 Big5E 編碼器。</returns>
    public override Encoder GetEncoder() => new Big5EEncoder(this);

    /// <summary>
    /// Creates a stateful decoder for block-based conversion.
    /// 建立用於區塊式轉換的具狀態解碼器。
    /// </summary>
    /// <returns>A stateful Big5E decoder. / 具狀態的 Big5E 解碼器。</returns>
    public override Decoder GetDecoder() => new Big5EDecoder(this);

    /// <summary>
    /// Gets a safe upper bound for bytes produced from a character count.
    /// 取得指定字元數量可能產生之位元組數量的安全上限。
    /// </summary>
    /// <param name="charCount">The input character count. / 輸入字元數量。</param>
    /// <returns>A safe maximum byte count. / 安全的最大位元組數量。</returns>
    public override int GetMaxByteCount(int charCount)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNegative(charCount, nameof(charCount));

        long multiplier = Math.Max(2L, EncoderFallback.MaxCharCount * 2L);
        long maximum = (charCount + 1L) * multiplier;
        if (maximum > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(charCount));
        }

        return (int)maximum;
    }

    /// <summary>
    /// Gets a safe upper bound for characters produced from a byte count.
    /// 取得指定位元組數量可能產生之字元數量的安全上限。
    /// </summary>
    /// <param name="byteCount">The input byte count. / 輸入位元組數量。</param>
    /// <returns>A safe maximum character count. / 安全的最大字元數量。</returns>
    public override int GetMaxCharCount(int byteCount)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNegative(byteCount, nameof(byteCount));

        long multiplier = Math.Max(1, DecoderFallback.MaxCharCount);
        long maximum = (byteCount + 1L) * multiplier;
        if (maximum > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(byteCount));
        }

        return (int)maximum;
    }

    private int Encode(
        char[] chars,
        int index,
        int count,
        byte[]? bytes,
        int byteIndex,
        EncoderFallback fallback)
    {
        int end = index + count;
        int written = 0;
        EncoderFallbackBuffer? fallbackBuffer = null;
        while (index < end)
        {
            int sourceIndex = index;
            char current = chars[index];
            int codePoint;
            bool isSurrogatePair = false;
            if (char.IsHighSurrogate(current) &&
                index + 1 < end &&
                char.IsLowSurrogate(chars[index + 1]))
            {
                codePoint = char.ConvertToUtf32(current, chars[index + 1]);
                isSurrogatePair = true;
                index += 2;
            }
            else
            {
                codePoint = current;
                index++;
            }

            if (codePoint <= 0x7F)
            {
                WriteByte(bytes, byteIndex + written, (byte)codePoint);
                written++;
            }
            else if (_unicodeToBig5E.TryGetValue(codePoint, out int big5Code))
            {
                WriteByte(bytes, byteIndex + written, (byte)(big5Code >> 8));
                WriteByte(bytes, byteIndex + written + 1, (byte)big5Code);
                written += 2;
            }
            else
            {
                fallbackBuffer ??= fallback.CreateFallbackBuffer();
                bool hasFallback = isSurrogatePair
                    ? fallbackBuffer.Fallback(current, chars[sourceIndex + 1], sourceIndex)
                    : fallbackBuffer.Fallback(current, sourceIndex);
                if (hasFallback)
                {
                    written += EncodeFallback(fallbackBuffer, bytes, byteIndex + written, sourceIndex);
                }
            }
        }

        return written;
    }

    private int Decode(
        byte[] bytes,
        int index,
        int count,
        char[]? chars,
        int charIndex,
        DecoderFallback fallback)
    {
        int end = index + count;
        int written = 0;
        DecoderFallbackBuffer? fallbackBuffer = null;
        while (index < end)
        {
            int sourceIndex = index;
            byte current = bytes[index++];
            if (current < 0x80)
            {
                WriteChar(chars, charIndex + written, (char)current);
                written++;
                continue;
            }

            if (current is >= 0x81 and <= 0xFE && index < end)
            {
                byte trail = bytes[index];
                int big5Code = (current << 8) | trail;
                if (IsValidBig5Code(big5Code) && _big5EToUnicode.TryGetValue(big5Code, out int codePoint))
                {
                    index++;
                    if (codePoint <= 0xFFFF)
                    {
                        WriteChar(chars, charIndex + written, (char)codePoint);
                        written++;
                    }
                    else
                    {
                        string surrogatePair = char.ConvertFromUtf32(codePoint);
                        WriteChar(chars, charIndex + written, surrogatePair[0]);
                        WriteChar(chars, charIndex + written + 1, surrogatePair[1]);
                        written += 2;
                    }

                    continue;
                }

                if (IsValidBig5Code(big5Code))
                {
                    index++;
                    fallbackBuffer ??= fallback.CreateFallbackBuffer();
                    written += DecodeFallback(
                        fallbackBuffer,
                        [current, trail],
                        sourceIndex,
                        chars,
                        charIndex + written);
                    continue;
                }
            }

            fallbackBuffer ??= fallback.CreateFallbackBuffer();
            written += DecodeFallback(
                fallbackBuffer,
                [current],
                sourceIndex,
                chars,
                charIndex + written);
        }

        return written;
    }

    private int EncodeFallback(
        EncoderFallbackBuffer fallbackBuffer,
        byte[]? bytes,
        int byteIndex,
        int sourceIndex)
    {
        int written = 0;
        while (fallbackBuffer.Remaining > 0)
        {
            char current = fallbackBuffer.GetNextChar();
            int codePoint = current;
            if (char.IsHighSurrogate(current) && fallbackBuffer.Remaining > 0)
            {
                char low = fallbackBuffer.GetNextChar();
                if (char.IsLowSurrogate(low))
                {
                    codePoint = char.ConvertToUtf32(current, low);
                }
                else
                {
                    fallbackBuffer.MovePrevious();
                }
            }

            if (codePoint <= 0x7F)
            {
                WriteByte(bytes, byteIndex + written, (byte)codePoint);
                written++;
            }
            else if (_unicodeToBig5E.TryGetValue(codePoint, out int big5Code))
            {
                WriteByte(bytes, byteIndex + written, (byte)(big5Code >> 8));
                WriteByte(bytes, byteIndex + written + 1, (byte)big5Code);
                written += 2;
            }
            else
            {
                throw new EncoderFallbackException();
            }
        }

        return written;
    }

    private static int DecodeFallback(
        DecoderFallbackBuffer fallbackBuffer,
        byte[] bytesUnknown,
        int sourceIndex,
        char[]? chars,
        int charIndex)
    {
        if (!fallbackBuffer.Fallback(bytesUnknown, sourceIndex))
        {
            return 0;
        }

        int written = 0;
        while (fallbackBuffer.Remaining > 0)
        {
            WriteChar(chars, charIndex + written, fallbackBuffer.GetNextChar());
            written++;
        }

        return written;
    }

    private static char[] PrepareEncoderInput(
        char[] chars,
        int index,
        int count,
        char pendingHighSurrogate,
        bool flush,
        out int inputIndex,
        out int inputCount,
        out char nextPendingHighSurrogate)
    {
        inputIndex = index;
        inputCount = count;
        nextPendingHighSurrogate = '\0';
        if (pendingHighSurrogate == '\0')
        {
            if (!flush && count > 0 && char.IsHighSurrogate(chars[index + count - 1]))
            {
                inputCount--;
                nextPendingHighSurrogate = chars[index + count - 1];
            }

            return chars;
        }

        int prefixLength = pendingHighSurrogate == '\0' ? 0 : 1;
        var input = new char[prefixLength + count];
        if (prefixLength != 0)
        {
            input[0] = pendingHighSurrogate;
        }

        Array.Copy(chars, index, input, prefixLength, count);
        inputIndex = 0;
        inputCount = input.Length;
        if (!flush && input.Length > 0 && char.IsHighSurrogate(input[input.Length - 1]))
        {
            nextPendingHighSurrogate = input[input.Length - 1];
            inputCount--;
        }

        return input;
    }

    private static byte[] PrepareDecoderInput(
        byte[] bytes,
        int index,
        int count,
        byte pendingLeadByte,
        bool flush,
        out int inputIndex,
        out int inputCount,
        out byte nextPendingLeadByte)
    {
        inputIndex = index;
        inputCount = count;
        nextPendingLeadByte = 0;
        if (pendingLeadByte == 0)
        {
            if (!flush && count > 0 && bytes[index + count - 1] is >= 0x81 and <= 0xFE)
            {
                inputCount--;
                nextPendingLeadByte = bytes[index + count - 1];
            }

            return bytes;
        }

        int prefixLength = pendingLeadByte == 0 ? 0 : 1;
        var input = new byte[prefixLength + count];
        if (prefixLength != 0)
        {
            input[0] = pendingLeadByte;
        }

        Array.Copy(bytes, index, input, prefixLength, count);
        inputIndex = 0;
        inputCount = input.Length;
        if (!flush && input.Length > 0 && input[input.Length - 1] is >= 0x81 and <= 0xFE)
        {
            nextPendingLeadByte = input[input.Length - 1];
            inputCount--;
        }

        return input;
    }

    private sealed class Big5EEncoder : Encoder
    {
        private readonly OdfBig5EEncoding _encoding;
        private char _pendingHighSurrogate;

        internal Big5EEncoder(OdfBig5EEncoding encoding)
        {
            _encoding = encoding;
            Fallback = encoding.EncoderFallback;
        }

        public override int GetByteCount(char[] chars, int index, int count, bool flush)
        {
            ValidateArraySegment(chars, index, count, nameof(chars), nameof(index), nameof(count));
            char[] input = PrepareEncoderInput(
                chars,
                index,
                count,
                _pendingHighSurrogate,
                flush,
                out int inputIndex,
                out int inputCount,
                out _);
            return _encoding.Encode(
                input,
                inputIndex,
                inputCount,
                null,
                0,
                Fallback ?? _encoding.EncoderFallback);
        }

        public override int GetBytes(
            char[] chars,
            int charIndex,
            int charCount,
            byte[] bytes,
            int byteIndex,
            bool flush)
        {
            ValidateArraySegment(chars, charIndex, charCount, nameof(chars), nameof(charIndex), nameof(charCount));
            global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(bytes, nameof(bytes));

            if (byteIndex < 0 || byteIndex > bytes.Length)
                throw new ArgumentOutOfRangeException(nameof(byteIndex));

            char[] input = PrepareEncoderInput(
                chars,
                charIndex,
                charCount,
                _pendingHighSurrogate,
                flush,
                out int inputIndex,
                out int inputCount,
                out char nextPendingHighSurrogate);
            EncoderFallback fallback = Fallback ?? _encoding.EncoderFallback;
            int written = _encoding.Encode(input, inputIndex, inputCount, bytes, byteIndex, fallback);
            _pendingHighSurrogate = nextPendingHighSurrogate;
            return written;
        }

        public override void Reset()
        {
            _pendingHighSurrogate = '\0';
            FallbackBuffer.Reset();
        }
    }

    private sealed class Big5EDecoder : Decoder
    {
        private readonly OdfBig5EEncoding _encoding;
        private byte _pendingLeadByte;

        internal Big5EDecoder(OdfBig5EEncoding encoding)
        {
            _encoding = encoding;
            Fallback = encoding.DecoderFallback;
        }

        public override int GetCharCount(byte[] bytes, int index, int count)
        {
            ValidateArraySegment(bytes, index, count, nameof(bytes), nameof(index), nameof(count));
            byte[] input = PrepareDecoderInput(
                bytes,
                index,
                count,
                _pendingLeadByte,
                flush: false,
                out int inputIndex,
                out int inputCount,
                out _);
            return _encoding.Decode(
                input,
                inputIndex,
                inputCount,
                null,
                0,
                Fallback ?? _encoding.DecoderFallback);
        }

        public override int GetCharCount(byte[] bytes, int index, int count, bool flush)
        {
            ValidateArraySegment(bytes, index, count, nameof(bytes), nameof(index), nameof(count));
            byte[] input = PrepareDecoderInput(
                bytes,
                index,
                count,
                _pendingLeadByte,
                flush,
                out int inputIndex,
                out int inputCount,
                out _);
            return _encoding.Decode(
                input,
                inputIndex,
                inputCount,
                null,
                0,
                Fallback ?? _encoding.DecoderFallback);
        }

        public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
            => GetChars(bytes, byteIndex, byteCount, chars, charIndex, flush: false);

        public override int GetChars(
            byte[] bytes,
            int byteIndex,
            int byteCount,
            char[] chars,
            int charIndex,
            bool flush)
        {
            ValidateArraySegment(bytes, byteIndex, byteCount, nameof(bytes), nameof(byteIndex), nameof(byteCount));
            global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(chars, nameof(chars));

            if (charIndex < 0 || charIndex > chars.Length)
                throw new ArgumentOutOfRangeException(nameof(charIndex));

            byte[] input = PrepareDecoderInput(
                bytes,
                byteIndex,
                byteCount,
                _pendingLeadByte,
                flush,
                out int inputIndex,
                out int inputCount,
                out byte nextPendingLeadByte);
            DecoderFallback fallback = Fallback ?? _encoding.DecoderFallback;
            int written = _encoding.Decode(input, inputIndex, inputCount, chars, charIndex, fallback);
            _pendingLeadByte = nextPendingLeadByte;
            return written;
        }

        public override void Reset()
        {
            _pendingLeadByte = 0;
            FallbackBuffer.Reset();
        }
    }

    private static void ValidateArraySegment<T>(
        T[] array,
        int index,
        int count,
        string arrayName,
        string indexName,
        string countName)
    {
        if (array is null)
        {
            throw new ArgumentNullException(arrayName);
        }

        if (index < 0 || index > array.Length)
        {
            throw new ArgumentOutOfRangeException(indexName);
        }

        if (count < 0 || count > array.Length - index)
        {
            throw new ArgumentOutOfRangeException(countName);
        }
    }

    private static void WriteByte(byte[]? bytes, int index, byte value)
    {
        if (bytes is not null)
        {
            if ((uint)index >= (uint)bytes.Length)
            {
                throw new ArgumentException(null, nameof(bytes));
            }

            bytes[index] = value;
        }
    }

    private static void WriteChar(char[]? chars, int index, char value)
    {
        if (chars is not null)
        {
            if ((uint)index >= (uint)chars.Length)
            {
                throw new ArgumentException(null, nameof(chars));
            }

            chars[index] = value;
        }
    }

    private static bool IsUnicodeScalar(int value) =>
        value is >= 0 and <= 0x10FFFF && value is not (>= 0xD800 and <= 0xDFFF);

    private static bool IsValidBig5Code(int value)
    {
        if (value is < 0x8140 or > 0xFEFE)
        {
            return false;
        }

        int lead = value >> 8;
        int trail = value & 0xFF;
        return lead is >= 0x81 and <= 0xFE &&
            (trail is >= 0x40 and <= 0x7E or >= 0xA1 and <= 0xFE);
    }
}
