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
    {
        SfntFont parsed = Parse(font, format, maximumBytes);
        parsed.ValidateAllCffGlyphs();
    }

    internal static void VerifyStructure(Stream font, WebFontFormat format)
    {
        SfntFont parsed = Parse(font, format, 32L * 1024 * 1024);
        parsed.ValidateCffGlyphs(new HashSet<ushort>());
    }

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
        parsed.ValidateAllCffGlyphs();
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
        parsed.ValidateAllCffGlyphs();
    }

    internal static void VerifyRetainsGlyphIds(
        byte[] source,
        int faceIndex,
        Stream subset,
        WebFontFormat format,
        IEnumerable<int> unicodeScalars)
    {
        SfntFont sourceFont = ParseSourceFace(source, faceIndex);
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
        SfntFont sourceFont = ParseSourceFace(source, faceIndex);
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

    private static SfntFont ParseSourceFace(byte[] source, int faceIndex)
    {
        int maximumBytes = checked((int)Math.Min(source.LongLength * 16, 256L * 1024 * 1024));
#if NET10_0_OR_GREATER
        if (source.Length >= 4 && source.AsSpan(0, 4).SequenceEqual("wOF2"u8))
        {
            byte[] selectedFace = DecodeWoff2(source, maximumBytes, faceIndex);
            return SfntFont.Parse(selectedFace, 0, 256, validateChecksums: true);
        }
#endif

        byte[] sourceSfnt = DecodeSource(source, maximumBytes);
        return SfntFont.Parse(sourceSfnt, faceIndex, 256, validateChecksums: true);
    }

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
            WebFontFormat.TrueType or WebFontFormat.OpenType => bytes,
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

    internal static byte[] DecodeSource(byte[] bytes, int maximumExpandedBytes)
    {
        if (bytes.Length < 4)
        {
            throw SfntFont.DataInvalid("source-signature");
        }

        ReadOnlySpan<byte> signature = bytes.AsSpan(0, 4);
        if (signature.SequenceEqual("wOFF"u8))
        {
            return DecodeWoff(bytes, maximumExpandedBytes);
        }

#if NET10_0_OR_GREATER
        if (signature.SequenceEqual("wOF2"u8))
        {
            return DecodeWoff2(bytes, maximumExpandedBytes);
        }
#else
        if (signature.SequenceEqual("wOF2"u8))
        {
            throw new NotSupportedException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
        }
#endif

        return bytes;
    }

    internal static byte[] DecodeWoff(byte[] bytes, int maximumExpandedBytes)
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
    internal static byte[] DecodeWoff2(byte[] bytes, int maximumExpandedBytes)
        => DecodeWoff2(bytes, maximumExpandedBytes, 0);

    internal static byte[] DecodeWoff2(byte[] bytes, int maximumExpandedBytes, int faceIndex)
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
        _ = SfntFont.ReadUInt32(data, 16, "WOFF2-sfntSize");
        int compressedLength = CheckedInt(SfntFont.ReadUInt32(data, 20, "WOFF2-compressedLength"), "WOFF2-compressedLength");
        uint metadataOffset = SfntFont.ReadUInt32(data, 28, "WOFF2-metaOffset");
        uint metadataLength = SfntFont.ReadUInt32(data, 32, "WOFF2-metaLength");
        uint metadataOriginalLength = SfntFont.ReadUInt32(data, 36, "WOFF2-metaOrigLength");
        uint privateOffset = SfntFont.ReadUInt32(data, 40, "WOFF2-privOffset");
        uint privateLength = SfntFont.ReadUInt32(data, 44, "WOFF2-privLength");
        if (declaredLength != bytes.Length
            || tableCount == 0
            || tableCount > 256)
        {
            throw SfntFont.DataInvalid("WOFF2-header");
        }
        bool isCollection = flavor == 0x74746366;
        if (!isCollection && flavor is not (0x00010000 or 0x74727565 or 0x4F54544F))
        {
            throw SfntFont.DataInvalid("WOFF2-flavor");
        }

        int position = 48;
        var entries = new List<Woff2TableEntry>(tableCount);
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

            bool nullTransform = tag is "glyf" or "loca"
                ? transformVersion == 3
                : transformVersion == 0;
            bool supportedTransform = tag switch
            {
                "glyf" or "loca" => transformVersion == 0,
                "hmtx" => transformVersion == 1,
                _ => false
            };
            if (!nullTransform && !supportedTransform)
            {
                throw new NotSupportedException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
            }

            uint originalLength = ReadUIntBase128(data, ref position);
            int originalTableLength = CheckedInt(originalLength, "WOFF2-tableLength");
            int storedLength = originalTableLength;
            if (supportedTransform)
            {
                storedLength = CheckedInt(
                    ReadUIntBase128(data, ref position),
                    "WOFF2-transformLength");
            }

            if (storedLength > maximumExpandedBytes - uncompressedLength)
            {
                throw SfntFont.DataInvalid("WOFF2-expandedSize");
            }

            uncompressedLength += storedLength;
            entries.Add(new Woff2TableEntry(
                tag,
                originalTableLength,
                storedLength,
                transformVersion));
        }

        Woff2CollectionFace? collectionFace = null;
        if (isCollection)
        {
            IReadOnlyList<Woff2CollectionFace> faces = ReadWoff2Collection(
                data,
                ref position,
                entries);
            if (faceIndex < 0 || faceIndex >= faces.Count)
            {
                throw SfntFont.DataInvalid("WOFF2-collection-face");
            }

            collectionFace = faces[faceIndex];
        }
        else if (faceIndex != 0)
        {
            throw SfntFont.DataInvalid("WOFF2-face");
        }

        SfntFont.EnsureRange(data, position, compressedLength, "WOFF2-compressedData");
        int compressedEnd = checked(position + compressedLength);
        ValidateWoff2TrailingBlocks(
            data,
            compressedEnd,
            metadataOffset,
            metadataLength,
            metadataOriginalLength,
            privateOffset,
            privateLength);

        var uncompressed = new byte[uncompressedLength];
        if (!BrotliDecoder.TryDecompress(
                data.Slice(position, compressedLength),
                uncompressed,
                out int written)
            || written != uncompressedLength)
        {
            throw SfntFont.DataInvalid("WOFF2-Brotli");
        }

        var storedOffsets = new int[entries.Count];
        int tableOffset = 0;
        for (int index = 0; index < entries.Count; index++)
        {
            Woff2TableEntry entry = entries[index];
            storedOffsets[index] = tableOffset;
            tableOffset += entry.StoredLength;
        }

        IReadOnlyList<int> selectedTableIndices = collectionFace?.TableIndices
            ?? Enumerable.Range(0, entries.Count).ToArray();
        var tables = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        var transformedTables = new List<(Woff2TableEntry Entry, byte[] Data)>();
        foreach (int index in selectedTableIndices)
        {
            Woff2TableEntry entry = entries[index];
            byte[] table = uncompressed.AsSpan(storedOffsets[index], entry.StoredLength).ToArray();
            if (tables.ContainsKey(entry.Tag)
                || transformedTables.Any(item => item.Entry.Tag == entry.Tag))
            {
                throw SfntFont.DataInvalid("WOFF2-table");
            }

            if (entry.IsTransformed)
            {
                transformedTables.Add((entry, table));
            }
            else
            {
                tables.Add(entry.Tag, table);
            }
        }

        (Woff2TableEntry Entry, byte[] Data)? transformedGlyf = transformedTables
            .Where(item => item.Entry.Tag == "glyf")
            .Cast<(Woff2TableEntry Entry, byte[] Data)?>()
            .SingleOrDefault();
        (Woff2TableEntry Entry, byte[] Data)? transformedLoca = transformedTables
            .Where(item => item.Entry.Tag == "loca")
            .Cast<(Woff2TableEntry Entry, byte[] Data)?>()
            .SingleOrDefault();
        if (transformedGlyf.HasValue != transformedLoca.HasValue)
        {
            throw SfntFont.DataInvalid("WOFF2-glyf-loca-pair");
        }
        if (transformedGlyf.HasValue)
        {
            Woff2GlyfReconstruction reconstructed = Woff2GlyfReconstructor.Reconstruct(
                transformedGlyf.Value.Data,
                transformedGlyf.Value.Entry.OriginalLength,
                transformedLoca!.Value.Entry.StoredLength,
                transformedLoca.Value.Entry.OriginalLength,
                tables,
                maximumExpandedBytes);
            tables.Add("glyf", reconstructed.Glyf);
            tables.Add("loca", reconstructed.Loca);
        }

        foreach ((Woff2TableEntry entry, byte[] transformed) in transformedTables
                     .Where(item => item.Entry.Tag == "hmtx"))
        {
            byte[] table = ReconstructTransformedHmtx(transformed, tables);
            if (table.Length != entry.OriginalLength)
            {
                throw SfntFont.DataInvalid("WOFF2-transformLength");
            }

            tables.Add(entry.Tag, table);
        }

        uint selectedFlavor = collectionFace?.Flavor ?? flavor;
        byte[] sfnt = WebFontWriters.WriteTrueType(new SfntSubset(selectedFlavor, tables));
        if (sfnt.Length > maximumExpandedBytes)
        {
            throw SfntFont.DataInvalid("WOFF2-sfntSize");
        }

        return sfnt;
    }

    private static void ValidateWoff2TrailingBlocks(
        ReadOnlySpan<byte> data,
        int compressedEnd,
        uint metadataOffset,
        uint metadataLength,
        uint metadataOriginalLength,
        uint privateOffset,
        uint privateLength)
    {
        bool hasMetadata = metadataOffset != 0 || metadataLength != 0 || metadataOriginalLength != 0;
        if (hasMetadata
            && (metadataOffset == 0 || metadataLength == 0 || metadataOriginalLength == 0))
        {
            throw SfntFont.DataInvalid("WOFF2-metadata");
        }

        bool hasPrivateData = privateOffset != 0 || privateLength != 0;
        if (hasPrivateData && (privateOffset == 0 || privateLength == 0))
        {
            throw SfntFont.DataInvalid("WOFF2-privateData");
        }

        int cursor = compressedEnd;
        if (hasMetadata)
        {
            cursor = ValidateWoff2Block(
                data,
                cursor,
                metadataOffset,
                metadataLength,
                "WOFF2-metadata");
        }
        if (hasPrivateData)
        {
            cursor = ValidateWoff2Block(
                data,
                cursor,
                privateOffset,
                privateLength,
                "WOFF2-privateData");
        }

        int trailingLength = data.Length - cursor;
        if (trailingLength is < 0 or > 3
            || data.Slice(cursor, trailingLength).ContainsAnyExcept((byte)0))
        {
            throw SfntFont.DataInvalid("WOFF2-trailingData");
        }
    }

    private static int ValidateWoff2Block(
        ReadOnlySpan<byte> data,
        int cursor,
        uint declaredOffset,
        uint declaredLength,
        string detail)
    {
        int alignedOffset = checked((cursor + 3) & ~3);
        int offset = CheckedInt(declaredOffset, detail);
        int length = CheckedInt(declaredLength, detail);
        if (offset != alignedOffset
            || alignedOffset - cursor > 3
            || data.Slice(cursor, alignedOffset - cursor).ContainsAnyExcept((byte)0))
        {
            throw SfntFont.DataInvalid(detail);
        }

        SfntFont.EnsureRange(data, offset, length, detail);
        return checked(offset + length);
    }

    private static byte[] ReconstructTransformedHmtx(
        ReadOnlySpan<byte> transformed,
        IReadOnlyDictionary<string, byte[]> tables)
    {
        if (!tables.TryGetValue("maxp", out byte[]? maxp)
            || !tables.TryGetValue("hhea", out byte[]? hhea)
            || !tables.TryGetValue("head", out byte[]? head)
            || !tables.TryGetValue("loca", out byte[]? loca)
            || !tables.TryGetValue("glyf", out byte[]? glyf))
        {
            throw SfntFont.DataInvalid("WOFF2-hmtx-dependencies");
        }

        SfntFont.EnsureRange(maxp, 0, 6, "WOFF2-maxp");
        SfntFont.EnsureRange(hhea, 0, 36, "WOFF2-hhea");
        SfntFont.EnsureRange(head, 0, 54, "WOFF2-head");
        ushort glyphCount = SfntFont.ReadUInt16(maxp, 4, "WOFF2-numGlyphs");
        ushort metricCount = SfntFont.ReadUInt16(hhea, 34, "WOFF2-numHMetrics");
        short indexFormat = SfntFont.ReadInt16(head, 50, "WOFF2-indexToLocFormat");
        if (glyphCount == 0 || metricCount == 0 || metricCount > glyphCount)
        {
            throw SfntFont.DataInvalid("WOFF2-hmtx-counts");
        }

        uint[] locations = ReadWoff2Locations(loca, glyphCount, indexFormat, glyf.Length);
        var xMins = new short[glyphCount];
        for (int glyph = 0; glyph < glyphCount; glyph++)
        {
            uint start = locations[glyph];
            uint end = locations[glyph + 1];
            if (start == end)
            {
                continue;
            }

            int glyphOffset = CheckedInt(start, "WOFF2-glyf-offset");
            SfntFont.EnsureRange(glyf, glyphOffset, 10, "WOFF2-glyf");
            xMins[glyph] = SfntFont.ReadInt16(glyf, glyphOffset + 2, "WOFF2-glyf-xMin");
        }

        SfntFont.EnsureRange(transformed, 0, 1, "WOFF2-hmtx-flags");
        byte flags = transformed[0];
        if ((flags & 0xFC) != 0 || (flags & 0x03) == 0)
        {
            throw SfntFont.DataInvalid("WOFF2-hmtx-flags");
        }

        int position = 1;
        var advances = new ushort[metricCount];
        for (int metric = 0; metric < metricCount; metric++)
        {
            SfntFont.EnsureRange(transformed, position, 2, "WOFF2-hmtx-advance");
            advances[metric] = SfntFont.ReadUInt16(transformed, position, "WOFF2-hmtx-advance");
            position += 2;
        }

        short[] proportionalBearings = ReadWoff2Bearings(
            transformed,
            ref position,
            metricCount,
            omitted: (flags & 0x01) != 0,
            xMins.AsSpan(0, metricCount));
        int additionalCount = glyphCount - metricCount;
        short[] additionalBearings = ReadWoff2Bearings(
            transformed,
            ref position,
            additionalCount,
            omitted: (flags & 0x02) != 0,
            xMins.AsSpan(metricCount, additionalCount));
        if (position != transformed.Length)
        {
            throw SfntFont.DataInvalid("WOFF2-hmtx-length");
        }

        var result = new byte[checked((metricCount * 4) + (additionalCount * 2))];
        int output = 0;
        for (int metric = 0; metric < metricCount; metric++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(result.AsSpan(output, 2), advances[metric]);
            BinaryPrimitives.WriteInt16BigEndian(result.AsSpan(output + 2, 2), proportionalBearings[metric]);
            output += 4;
        }
        for (int glyph = 0; glyph < additionalCount; glyph++)
        {
            BinaryPrimitives.WriteInt16BigEndian(result.AsSpan(output, 2), additionalBearings[glyph]);
            output += 2;
        }

        return result;
    }

    private static short[] ReadWoff2Bearings(
        ReadOnlySpan<byte> transformed,
        ref int position,
        int count,
        bool omitted,
        ReadOnlySpan<short> xMins)
    {
        if (omitted)
        {
            return xMins.ToArray();
        }

        var result = new short[count];
        for (int index = 0; index < count; index++)
        {
            SfntFont.EnsureRange(transformed, position, 2, "WOFF2-hmtx-bearing");
            result[index] = SfntFont.ReadInt16(transformed, position, "WOFF2-hmtx-bearing");
            position += 2;
        }

        return result;
    }

    private static uint[] ReadWoff2Locations(
        ReadOnlySpan<byte> loca,
        ushort glyphCount,
        short indexFormat,
        int glyfLength)
    {
        int entrySize = indexFormat switch
        {
            0 => 2,
            1 => 4,
            _ => throw SfntFont.DataInvalid("WOFF2-indexToLocFormat")
        };
        int requiredLength = checked((glyphCount + 1) * entrySize);
        if (loca.Length != requiredLength)
        {
            throw SfntFont.DataInvalid("WOFF2-loca-length");
        }

        var result = new uint[glyphCount + 1];
        uint previous = 0;
        for (int index = 0; index <= glyphCount; index++)
        {
            int offset = index * entrySize;
            uint current = indexFormat == 0
                ? checked((uint)SfntFont.ReadUInt16(loca, offset, "WOFF2-loca") * 2)
                : SfntFont.ReadUInt32(loca, offset, "WOFF2-loca");
            if (current < previous || current > glyfLength)
            {
                throw SfntFont.DataInvalid("WOFF2-loca-order");
            }

            result[index] = current;
            previous = current;
        }

        return result;
    }

    private static IReadOnlyList<Woff2CollectionFace> ReadWoff2Collection(
        ReadOnlySpan<byte> data,
        ref int position,
        IReadOnlyList<Woff2TableEntry> entries)
    {
        SfntFont.EnsureRange(data, position, 4, "WOFF2-collection-version");
        uint version = SfntFont.ReadUInt32(data, position, "WOFF2-collection-version");
        position += 4;
        if (version is not (0x00010000 or 0x00020000))
        {
            throw SfntFont.DataInvalid("WOFF2-collection-version");
        }

        ushort faceCount = Read255UInt16(data, ref position);
        if (faceCount is 0 or > 256)
        {
            throw SfntFont.DataInvalid("WOFF2-collection-count");
        }

        var faces = new List<Woff2CollectionFace>(faceCount);
        var glyfToLoca = new Dictionary<int, int>();
        var locaToGlyf = new Dictionary<int, int>();
        for (int faceIndex = 0; faceIndex < faceCount; faceIndex++)
        {
            ushort tableCount = Read255UInt16(data, ref position);
            SfntFont.EnsureRange(data, position, 4, "WOFF2-collection-flavor");
            uint flavor = SfntFont.ReadUInt32(data, position, "WOFF2-collection-flavor");
            position += 4;
            if (tableCount == 0
                || tableCount > entries.Count
                || flavor is not (0x00010000 or 0x74727565 or 0x4F54544F))
            {
                throw SfntFont.DataInvalid("WOFF2-collection-face");
            }

            var indices = new int[tableCount];
            var uniqueIndices = new HashSet<int>();
            var uniqueTags = new HashSet<string>(StringComparer.Ordinal);
            int glyfIndex = -1;
            int locaIndex = -1;
            for (int table = 0; table < tableCount; table++)
            {
                int tableIndex = Read255UInt16(data, ref position);
                if (tableIndex >= entries.Count
                    || !uniqueIndices.Add(tableIndex)
                    || !uniqueTags.Add(entries[tableIndex].Tag))
                {
                    throw SfntFont.DataInvalid("WOFF2-collection-table");
                }

                indices[table] = tableIndex;
                if (entries[tableIndex].Tag == "glyf")
                {
                    glyfIndex = tableIndex;
                }
                else if (entries[tableIndex].Tag == "loca")
                {
                    locaIndex = tableIndex;
                }
            }

            ValidateWoff2CollectionGlyfLocaPair(
                entries,
                glyfIndex,
                locaIndex,
                glyfToLoca,
                locaToGlyf);
            faces.Add(new Woff2CollectionFace(flavor, indices));
        }

        return faces;
    }

    private static void ValidateWoff2CollectionGlyfLocaPair(
        IReadOnlyList<Woff2TableEntry> entries,
        int glyfIndex,
        int locaIndex,
        IDictionary<int, int> glyfToLoca,
        IDictionary<int, int> locaToGlyf)
    {
        if ((glyfIndex >= 0) != (locaIndex >= 0))
        {
            throw SfntFont.DataInvalid("WOFF2-glyf-loca-pair");
        }
        if (glyfIndex < 0)
        {
            return;
        }

        if (entries[glyfIndex].IsTransformed != entries[locaIndex].IsTransformed
            || (glyfToLoca.TryGetValue(glyfIndex, out int existingLoca)
                && existingLoca != locaIndex)
            || (locaToGlyf.TryGetValue(locaIndex, out int existingGlyf)
                && existingGlyf != glyfIndex))
        {
            throw SfntFont.DataInvalid("WOFF2-glyf-loca-pair");
        }

        glyfToLoca[glyfIndex] = locaIndex;
        locaToGlyf[locaIndex] = glyfIndex;
    }

    private static ushort Read255UInt16(ReadOnlySpan<byte> data, ref int position)
    {
        SfntFont.EnsureRange(data, position, 1, "WOFF2-255UInt16");
        byte code = data[position++];
        if (code == 253)
        {
            SfntFont.EnsureRange(data, position, 2, "WOFF2-255UInt16");
            ushort value = SfntFont.ReadUInt16(data, position, "WOFF2-255UInt16");
            position += 2;
            return value;
        }

        if (code is 254 or 255)
        {
            SfntFont.EnsureRange(data, position, 1, "WOFF2-255UInt16");
            int offset = code == 255 ? 253 : 506;
            return checked((ushort)(data[position++] + offset));
        }

        return code;
    }

    private readonly record struct Woff2CollectionFace(
        uint Flavor,
        IReadOnlyList<int> TableIndices);

    private readonly record struct Woff2TableEntry(
        string Tag,
        int OriginalLength,
        int StoredLength,
        int TransformVersion)
    {
        internal bool IsTransformed
            => Tag switch
            {
                "glyf" or "loca" => TransformVersion == 0,
                "hmtx" => TransformVersion == 1,
                _ => false
            };
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
