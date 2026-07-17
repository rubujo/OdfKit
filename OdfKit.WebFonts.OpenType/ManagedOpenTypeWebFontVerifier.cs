using System.Buffers.Binary;
#if NET10_0_OR_GREATER
using System.IO.Compression;
#endif
using System.Text;
using OdfKit.Compliance;

namespace OdfKit.WebFonts.OpenType;

/// <summary>
/// Verifies bounded WebFont output without invoking an external validator.
/// 不啟動外部驗證器，直接驗證有界的 WebFont 輸出。
/// </summary>
public static class ManagedOpenTypeWebFontVerifier
{
    /// <summary>
    /// Verifies a WebFont with the default 32 MiB input limit.
    /// 使用預設的 32 MiB 輸入上限驗證 WebFont。
    /// </summary>
    /// <param name="font">The readable font stream. / 可讀取的字型資料流。</param>
    /// <param name="format">The declared WebFont format. / 宣告的 WebFont 格式。</param>
    public static void Verify(Stream font, WebFontFormat format)
        => Verify(font, format, 32L * 1024 * 1024);

    /// <summary>
    /// Verifies a bounded WebFont.
    /// 驗證有界的 WebFont。
    /// </summary>
    /// <param name="font">The readable font stream. / 可讀取的字型資料流。</param>
    /// <param name="format">The declared WebFont format. / 宣告的 WebFont 格式。</param>
    /// <param name="maximumBytes">The maximum accepted byte count. / 可接受的最大位元組數。</param>
    public static void Verify(Stream font, WebFontFormat format, long maximumBytes)
        => _ = Parse(font, format, maximumBytes);

    /// <summary>
    /// Verifies a WebFont and confirms that every requested Unicode scalar is mapped.
    /// 驗證 WebFont，並確認每個要求的 Unicode 純量值均有對應。
    /// </summary>
    /// <param name="font">The readable font stream. / 可讀取的字型資料流。</param>
    /// <param name="format">The declared WebFont format. / 宣告的 WebFont 格式。</param>
    /// <param name="unicodeScalars">The required Unicode scalars. / 必須存在的 Unicode 純量值。</param>
    public static void VerifyContainsScalars(
        Stream font,
        WebFontFormat format,
        IEnumerable<int> unicodeScalars)
    {
        if (unicodeScalars is null)
        {
            throw new ArgumentNullException(
                nameof(unicodeScalars),
                OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"));
        }

        SfntFont parsed = Parse(font, format, 32L * 1024 * 1024);
        foreach (int scalar in unicodeScalars.Distinct())
        {
            if (!parsed.ContainsUnicodeScalar(scalar))
            {
                throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
            }
        }
    }

    /// <summary>
    /// Verifies a WebFont and confirms that its Unicode and variation sequences are mapped.
    /// 驗證 WebFont，並確認其 Unicode 與變異序列均有對應。
    /// </summary>
    /// <param name="font">The readable font stream. / 可讀取的字型資料流。</param>
    /// <param name="format">The declared WebFont format. / 宣告的 WebFont 格式。</param>
    /// <param name="sequences">The required text sequences. / 必須存在的文字序列。</param>
    public static void VerifyContainsSequences(
        Stream font,
        WebFontFormat format,
        IEnumerable<WebFontTextSequence> sequences)
    {
        if (sequences is null)
        {
            throw new ArgumentNullException(
                nameof(sequences),
                OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"));
        }

        WebFontTextSequence[] values = sequences.ToArray();
        if (values.Length == 0)
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"));
        }

        SfntFont parsed = Parse(font, format, 32L * 1024 * 1024);
        foreach (WebFontTextSequence sequence in values)
        {
            for (int index = 0; index < sequence.UnicodeScalars.Count; index++)
            {
                int scalar = sequence.UnicodeScalars[index];
                if (IsVariationSelector(scalar))
                {
                    if (index == 0
                        || !parsed.ContainsVariationSequence(sequence.UnicodeScalars[index - 1], scalar))
                    {
                        throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
                    }

                    continue;
                }

                if (RequiresGlyph(scalar) && !parsed.ContainsUnicodeScalar(scalar))
                {
                    throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
                }
            }
        }
    }

    internal static void VerifyRetainsGlyphIds(
        byte[] source,
        int faceIndex,
        Stream subset,
        WebFontFormat format,
        IEnumerable<int> unicodeScalars)
    {
        SfntFont sourceFont = SfntFont.Parse(source, faceIndex, 256, validateChecksums: true);
        SfntFont subsetFont = Parse(subset, format, 32L * 1024 * 1024);
        if (sourceFont.GlyphCount != subsetFont.GlyphCount)
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
        }

        foreach (int scalar in unicodeScalars.Distinct())
        {
            ushort sourceGlyph = sourceFont.GetGlyphId(scalar);
            if (sourceGlyph == 0 || sourceGlyph != subsetFont.GetGlyphId(scalar))
            {
                throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
            }
        }
    }

    internal static void VerifyRetainsLayoutTables(
        byte[] source,
        int faceIndex,
        Stream subset,
        WebFontFormat format)
    {
        SfntFont sourceFont = SfntFont.Parse(source, faceIndex, 256, validateChecksums: true);
        SfntFont subsetFont = Parse(subset, format, 32L * 1024 * 1024);
        foreach (string tag in new[] { "GDEF", "GPOS", "GSUB" })
        {
            bool sourceHasTable = sourceFont.TryGetTable(tag, out ReadOnlyMemory<byte> sourceTable);
            bool subsetHasTable = subsetFont.TryGetTable(tag, out ReadOnlyMemory<byte> subsetTable);
            if (sourceHasTable != subsetHasTable
                || (sourceHasTable && !sourceTable.Span.SequenceEqual(subsetTable.Span)))
            {
                throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
            }
        }
    }

    private static bool IsVariationSelector(int scalar)
        => scalar is >= 0xFE00 and <= 0xFE0F or >= 0xE0100 and <= 0xE01EF;

    private static bool RequiresGlyph(int scalar)
        => scalar != 0xFEFF
            && scalar is not (>= 0x0000 and <= 0x001F)
            && scalar is not (>= 0x007F and <= 0x009F);

    private static SfntFont Parse(Stream font, WebFontFormat format, long maximumBytes)
    {
        if (font is null)
        {
            throw new ArgumentNullException(nameof(font), OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"));
        }

        if (!font.CanRead || maximumBytes <= 0 || maximumBytes > int.MaxValue)
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"));
        }

        byte[] bytes = ReadBounded(font, (int)maximumBytes);
        byte[] sfnt = format switch
        {
            WebFontFormat.TrueType => bytes,
            WebFontFormat.Woff => DecodeWoff(bytes, (int)maximumBytes),
#if NET10_0_OR_GREATER
            WebFontFormat.Woff2 => DecodeWoff2(bytes, (int)maximumBytes),
#else
            WebFontFormat.Woff2 => throw new NotSupportedException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid")),
#endif
            _ => throw new NotSupportedException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"))
        };
        return SfntFont.Parse(sfnt, 0, 256, validateChecksums: true);
    }

    private static byte[] DecodeWoff(byte[] bytes, int maximumExpandedBytes)
    {
        ReadOnlySpan<byte> data = bytes;
        SfntFont.EnsureRange(data, 0, 44, "WOFF-header");
        if (!data.Slice(0, 4).SequenceEqual("wOFF"u8))
        {
            throw SfntFont.DataInvalid("WOFF-signature");
        }

        uint flavor = SfntFont.ReadUInt32(data, 4, "WOFF-flavor");
        uint declaredLength = SfntFont.ReadUInt32(data, 8, "WOFF-length");
        ushort tableCount = SfntFont.ReadUInt16(data, 12, "WOFF-tableCount");
        uint totalSfntSize = SfntFont.ReadUInt32(data, 16, "WOFF-sfntSize");
        if (declaredLength != bytes.Length
            || totalSfntSize > maximumExpandedBytes
            || tableCount == 0
            || tableCount > 256)
        {
            throw SfntFont.DataInvalid("WOFF-header");
        }

        SfntFont.EnsureRange(data, 44, checked(tableCount * 20), "WOFF-directory");
        var tables = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        for (int index = 0; index < tableCount; index++)
        {
            int record = 44 + (index * 20);
            string tag = Encoding.ASCII.GetString(bytes, record, 4);
            int offset = CheckedInt(SfntFont.ReadUInt32(data, record + 4, "WOFF-offset"), "WOFF-offset");
            int compressedLength = CheckedInt(SfntFont.ReadUInt32(data, record + 8, "WOFF-compressedLength"), "WOFF-compressedLength");
            int originalLength = CheckedInt(SfntFont.ReadUInt32(data, record + 12, "WOFF-originalLength"), "WOFF-originalLength");
            uint checksum = SfntFont.ReadUInt32(data, record + 16, "WOFF-checksum");
            if (compressedLength > originalLength)
            {
                throw SfntFont.DataInvalid("WOFF-compressedLength");
            }

            SfntFont.EnsureRange(data, offset, compressedLength, "WOFF-table");
            ReadOnlySpan<byte> stored = data.Slice(offset, compressedLength);
            byte[] table = compressedLength == originalLength
                ? stored.ToArray()
                : WebFontWriters.DecompressZlib(stored, originalLength);
            if (tables.ContainsKey(tag) || SfntFont.CalculateTableChecksum(tag, table) != checksum)
            {
                throw SfntFont.DataInvalid("WOFF-table");
            }

            tables.Add(tag, table);
        }

        byte[] sfnt = WebFontWriters.WriteTrueType(new SfntSubset(flavor, tables));
        if (totalSfntSize != sfnt.Length)
        {
            throw SfntFont.DataInvalid("WOFF-sfntSize");
        }

        return sfnt;
    }

#if NET10_0_OR_GREATER
    private static byte[] DecodeWoff2(byte[] bytes, int maximumExpandedBytes)
    {
        ReadOnlySpan<byte> data = bytes;
        SfntFont.EnsureRange(data, 0, 48, "WOFF2-header");
        if (!data.Slice(0, 4).SequenceEqual("wOF2"u8))
        {
            throw SfntFont.DataInvalid("WOFF2-signature");
        }

        uint flavor = SfntFont.ReadUInt32(data, 4, "WOFF2-flavor");
        uint declaredLength = SfntFont.ReadUInt32(data, 8, "WOFF2-length");
        ushort tableCount = SfntFont.ReadUInt16(data, 12, "WOFF2-tableCount");
        uint totalSfntSize = SfntFont.ReadUInt32(data, 16, "WOFF2-sfntSize");
        int compressedLength = CheckedInt(SfntFont.ReadUInt32(data, 20, "WOFF2-compressedLength"), "WOFF2-compressedLength");
        if (declaredLength != bytes.Length
            || totalSfntSize > maximumExpandedBytes
            || tableCount == 0
            || tableCount > 256)
        {
            throw SfntFont.DataInvalid("WOFF2-header");
        }

        int position = 48;
        var entries = new List<(string Tag, int Length)>(tableCount);
        int uncompressedLength = 0;
        for (int index = 0; index < tableCount; index++)
        {
            SfntFont.EnsureRange(data, position, 1, "WOFF2-flags");
            byte flags = data[position++];
            int tagIndex = flags & 0x3F;
            int transformVersion = flags >> 6;
            string tag;
            if (tagIndex == 63)
            {
                SfntFont.EnsureRange(data, position, 4, "WOFF2-tag");
                tag = Encoding.ASCII.GetString(bytes, position, 4);
                position += 4;
            }
            else
            {
                tag = GetKnownWoff2Tag(tagIndex);
            }

            bool nullTransform = tag is "glyf" or "loca" ? transformVersion == 3 : transformVersion == 0;
            if (!nullTransform)
            {
                throw new NotSupportedException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
            }

            uint originalLength = ReadUIntBase128(data, ref position);
            int length = CheckedInt(originalLength, "WOFF2-tableLength");
            if (length > maximumExpandedBytes - uncompressedLength)
            {
                throw SfntFont.DataInvalid("WOFF2-expandedSize");
            }

            uncompressedLength += length;
            entries.Add((tag, length));
        }

        SfntFont.EnsureRange(data, position, compressedLength, "WOFF2-compressedData");
        int compressedEnd = checked(position + compressedLength);
        int paddingLength = bytes.Length - compressedEnd;
        if (paddingLength is < 0 or > 3
            || (bytes.Length & 3) != 0
            || data.Slice(compressedEnd, paddingLength).ContainsAnyExcept((byte)0))
        {
            throw SfntFont.DataInvalid("WOFF2-trailingData");
        }

        var uncompressed = new byte[uncompressedLength];
        if (!BrotliDecoder.TryDecompress(
                data.Slice(position, compressedLength),
                uncompressed,
                out int written)
            || written != uncompressedLength)
        {
            throw SfntFont.DataInvalid("WOFF2-Brotli");
        }

        var tables = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        int tableOffset = 0;
        foreach ((string tag, int length) in entries)
        {
            byte[] table = uncompressed.AsSpan(tableOffset, length).ToArray();
            if (tables.ContainsKey(tag))
            {
                throw SfntFont.DataInvalid("WOFF2-table");
            }

            tables.Add(tag, table);

            tableOffset += length;
        }

        byte[] sfnt = WebFontWriters.WriteTrueType(new SfntSubset(flavor, tables));
        if (totalSfntSize != sfnt.Length)
        {
            throw SfntFont.DataInvalid("WOFF2-sfntSize");
        }

        return sfnt;
    }
#endif

    private static byte[] ReadBounded(Stream stream, int maximumBytes)
    {
        using var memory = new MemoryStream();
        var buffer = new byte[81920];
        int total = 0;
        int read;
        while (total <= maximumBytes)
        {
            int remaining = (int)Math.Min(buffer.Length, ((long)maximumBytes + 1) - total);
            read = stream.Read(buffer, 0, remaining);
            if (read == 0)
            {
                break;
            }

            total = checked(total + read);
            if (total > maximumBytes)
            {
                throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
            }

            memory.Write(buffer, 0, read);
        }

        if (total == 0)
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
        }

        return memory.ToArray();
    }

#if NET10_0_OR_GREATER
    private static uint ReadUIntBase128(ReadOnlySpan<byte> data, ref int position)
    {
        uint result = 0;
        for (int index = 0; index < 5; index++)
        {
            SfntFont.EnsureRange(data, position, 1, "UIntBase128");
            byte current = data[position++];
            if (index == 0 && current == 0x80)
            {
                throw SfntFont.DataInvalid("UIntBase128-leadingZero");
            }

            if ((result & 0xFE000000) != 0)
            {
                throw SfntFont.DataInvalid("UIntBase128-overflow");
            }

            result = (result << 7) | (uint)(current & 0x7F);
            if ((current & 0x80) == 0)
            {
                return result;
            }
        }

        throw SfntFont.DataInvalid("UIntBase128-length");
    }

    private static string GetKnownWoff2Tag(int index)
    {
        string[] tags =
        [
            "cmap", "head", "hhea", "hmtx", "maxp", "name", "OS/2", "post",
            "cvt ", "fpgm", "glyf", "loca", "prep", "CFF ", "VORG", "EBDT",
            "EBLC", "gasp", "hdmx", "kern", "LTSH", "PCLT", "VDMX", "vhea",
            "vmtx", "BASE", "GDEF", "GPOS", "GSUB", "EBSC", "JSTF", "MATH",
            "CBDT", "CBLC", "COLR", "CPAL", "SVG ", "sbix", "acnt", "avar",
            "bdat", "bloc", "bsln", "cvar", "fdsc", "feat", "fmtx", "fvar",
            "gvar", "hsty", "just", "lcar", "mort", "morx", "opbd", "prop",
            "trak", "Zapf", "Silf", "Glat", "Gloc", "Feat", "Sill"
        ];
        if ((uint)index >= tags.Length)
        {
            throw SfntFont.DataInvalid("WOFF2-tagIndex");
        }

        return tags[index];
    }
#endif

    private static int CheckedInt(uint value, string detail)
    {
        if (value > int.MaxValue)
        {
            throw SfntFont.DataInvalid(detail);
        }

        return (int)value;
    }
}
