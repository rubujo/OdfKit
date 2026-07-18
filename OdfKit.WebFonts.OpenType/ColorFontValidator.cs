using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace OdfKit.WebFonts.OpenType;

internal static class ColorFontValidator
{
    internal static bool Validate(IReadOnlyDictionary<string, byte[]> tables, ushort glyphCount)
    {
        bool hasColor = false;
        bool hasCpal = tables.TryGetValue("CPAL", out byte[]? cpal);
        if (hasCpal)
        {
            ValidateCpal(cpal!);
            hasColor = true;
        }

        if (tables.TryGetValue("COLR", out byte[]? colr))
        {
            if (!hasCpal)
            {
                throw SfntFont.DataInvalid("COLR-CPAL-pair");
            }

            ValidateColr(colr, glyphCount);
            hasColor = true;
        }

        hasColor |= ValidateBitmapPair(tables, "CBDT", "CBLC", glyphCount);
        hasColor |= ValidateBitmapPair(tables, "EBDT", "EBLC", glyphCount);
        if (tables.TryGetValue("EBSC", out byte[]? ebsc))
        {
            ValidateVersionAndCount(ebsc, "EBSC", 0x00020000u, 8, 28);
            hasColor = true;
        }

        if (tables.TryGetValue("SVG ", out byte[]? svg))
        {
            ValidateSvg(svg, glyphCount);
            hasColor = true;
        }

        if (tables.TryGetValue("sbix", out byte[]? sbix))
        {
            ValidateSbix(sbix, glyphCount);
            hasColor = true;
        }

        return hasColor;
    }

    private static void ValidateCpal(byte[] table)
    {
        SfntFont.EnsureRange(table, 0, 12, "CPAL-header");
        ushort version = ReadUInt16(table, 0);
        if (version > 1)
        {
            throw SfntFont.DataInvalid("CPAL-version");
        }

        ushort entriesPerPalette = ReadUInt16(table, 2);
        ushort paletteCount = ReadUInt16(table, 4);
        ushort colorCount = ReadUInt16(table, 6);
        uint colorOffset = ReadUInt32(table, 8);
        if (entriesPerPalette == 0 || paletteCount == 0 || colorCount == 0)
        {
            throw SfntFont.DataInvalid("CPAL-count");
        }

        SfntFont.EnsureRange(table, 12, checked(paletteCount * 2), "CPAL-indices");
        for (int index = 0; index < paletteCount; index++)
        {
            ushort firstColor = ReadUInt16(table, 12 + (index * 2));
            if (firstColor > colorCount || entriesPerPalette > colorCount - firstColor)
            {
                throw SfntFont.DataInvalid("CPAL-index");
            }
        }

        EnsureOffsetRange(table, colorOffset, checked((uint)colorCount * 4), "CPAL-colors");
        if (version == 1)
        {
            int extension = checked(12 + (paletteCount * 2));
            SfntFont.EnsureRange(table, extension, 12, "CPAL-v1");
            ValidateOptionalArray(table, ReadUInt32(table, extension), paletteCount, 4, "CPAL-paletteTypes");
            ValidateOptionalArray(table, ReadUInt32(table, extension + 4), paletteCount, 2, "CPAL-paletteLabels");
            ValidateOptionalArray(table, ReadUInt32(table, extension + 8), entriesPerPalette, 2, "CPAL-entryLabels");
        }
    }

    private static void ValidateColr(byte[] table, ushort glyphCount)
    {
        SfntFont.EnsureRange(table, 0, 14, "COLR-header");
        ushort version = ReadUInt16(table, 0);
        if (version > 1)
        {
            throw SfntFont.DataInvalid("COLR-version");
        }

        ushort baseCount = ReadUInt16(table, 2);
        uint baseOffset = ReadUInt32(table, 4);
        uint layerOffset = ReadUInt32(table, 8);
        ushort layerCount = ReadUInt16(table, 12);
        EnsureOffsetRange(table, baseOffset, checked((uint)baseCount * 6), "COLR-baseRecords");
        EnsureOffsetRange(table, layerOffset, checked((uint)layerCount * 4), "COLR-layerRecords");
        for (int index = 0; index < baseCount; index++)
        {
            int record = checked((int)baseOffset + (index * 6));
            ValidateGlyph(ReadUInt16(table, record), glyphCount, "COLR-baseGlyph");
            ushort firstLayer = ReadUInt16(table, record + 2);
            ushort count = ReadUInt16(table, record + 4);
            if (firstLayer > layerCount || count > layerCount - firstLayer)
            {
                throw SfntFont.DataInvalid("COLR-layerRange");
            }
        }

        for (int index = 0; index < layerCount; index++)
        {
            ValidateGlyph(ReadUInt16(table, checked((int)layerOffset + (index * 4))), glyphCount, "COLR-layerGlyph");
        }

        if (version == 1)
        {
            SfntFont.EnsureRange(table, 14, 20, "COLR-v1-header");
            uint baseGlyphListOffset = ReadUInt32(table, 14);
            uint layerListOffset = ReadUInt32(table, 18);
            ValidateColrV1ListOffset(table, baseGlyphListOffset, 4, "COLR-baseGlyphList");
            ValidateColrV1ListOffset(table, layerListOffset, 4, "COLR-layerList");
            ValidateOptionalOffset(table, ReadUInt32(table, 22), "COLR-clipList");
            ValidateOptionalOffset(table, ReadUInt32(table, 26), "COLR-varIndexMap");
            ValidateOptionalOffset(table, ReadUInt32(table, 30), "COLR-itemVariationStore");

            if (baseGlyphListOffset != 0)
            {
                int offset = checked((int)baseGlyphListOffset);
                uint count = ReadUInt32(table, offset);
                if (count > glyphCount)
                {
                    throw SfntFont.DataInvalid("COLR-baseGlyphPaintCount");
                }

                EnsureOffsetRange(table, baseGlyphListOffset + 4, checked(count * 6), "COLR-baseGlyphPaintRecords");
                for (int index = 0; index < count; index++)
                {
                    int record = checked(offset + 4 + (index * 6));
                    ValidateGlyph(ReadUInt16(table, record), glyphCount, "COLR-v1-baseGlyph");
                    ValidateRelativeOffset(
                        table,
                        baseGlyphListOffset,
                        ReadUInt32(table, record + 2),
                        "COLR-paint");
                }
            }
        }
    }

    private static bool ValidateBitmapPair(
        IReadOnlyDictionary<string, byte[]> tables,
        string dataTag,
        string locationTag,
        ushort glyphCount)
    {
        bool hasData = tables.TryGetValue(dataTag, out byte[]? data);
        bool hasLocation = tables.TryGetValue(locationTag, out byte[]? location);
        if (hasData != hasLocation)
        {
            throw SfntFont.DataInvalid($"{dataTag}-{locationTag}-pair");
        }

        if (!hasData)
        {
            return false;
        }

        SfntFont.EnsureRange(data!, 0, 4, $"{dataTag}-header");
        SfntFont.EnsureRange(location!, 0, 8, $"{locationTag}-header");
        if (ReadUInt32(data!, 0) != 0x00030000u || ReadUInt32(location!, 0) != 0x00030000u)
        {
            throw SfntFont.DataInvalid($"{dataTag}-version");
        }

        uint strikeCount = ReadUInt32(location!, 4);
        if (strikeCount > 4096)
        {
            throw SfntFont.DataInvalid($"{locationTag}-strikeCount");
        }

        EnsureOffsetRange(location!, 8, checked(strikeCount * 48), $"{locationTag}-strikes");
        for (int strike = 0; strike < strikeCount; strike++)
        {
            int record = checked(8 + (strike * 48));
            uint arrayOffset = ReadUInt32(location!, record);
            uint arraySize = ReadUInt32(location!, record + 4);
            uint subtableCount = ReadUInt32(location!, record + 8);
            ushort firstGlyph = ReadUInt16(location!, record + 40);
            ushort lastGlyph = ReadUInt16(location!, record + 42);
            if (firstGlyph > lastGlyph || lastGlyph >= glyphCount || subtableCount > glyphCount)
            {
                throw SfntFont.DataInvalid($"{locationTag}-glyphRange");
            }

            EnsureOffsetRange(location!, arrayOffset, arraySize, $"{locationTag}-indexTables");
            if (arraySize < checked(subtableCount * 8))
            {
                throw SfntFont.DataInvalid($"{locationTag}-indexArray");
            }

            for (int index = 0; index < subtableCount; index++)
            {
                int entry = checked((int)arrayOffset + (index * 8));
                ushort first = ReadUInt16(location!, entry);
                ushort last = ReadUInt16(location!, entry + 2);
                uint additionalOffset = ReadUInt32(location!, entry + 4);
                if (first > last || last >= glyphCount)
                {
                    throw SfntFont.DataInvalid($"{locationTag}-subtableRange");
                }

                uint subtableOffset = checked(arrayOffset + additionalOffset);
                EnsureOffsetRange(location!, subtableOffset, 8, $"{locationTag}-subtable");
                uint imageOffset = ReadUInt32(location!, checked((int)subtableOffset + 4));
                if (imageOffset > data!.Length)
                {
                    throw SfntFont.DataInvalid($"{dataTag}-imageOffset");
                }
            }
        }

        return true;
    }

    private static void ValidateSvg(byte[] table, ushort glyphCount)
    {
        SfntFont.EnsureRange(table, 0, 10, "SVG-header");
        if (ReadUInt16(table, 0) != 0)
        {
            throw SfntFont.DataInvalid("SVG-version");
        }

        uint indexOffset = ReadUInt32(table, 2);
        EnsureOffsetRange(table, indexOffset, 2, "SVG-index");
        int index = checked((int)indexOffset);
        ushort count = ReadUInt16(table, index);
        if (count == 0)
        {
            throw SfntFont.DataInvalid("SVG-entryCount");
        }

        EnsureOffsetRange(table, indexOffset + 2, checked((uint)count * 12), "SVG-entries");
        int previousEnd = -1;
        for (int entryIndex = 0; entryIndex < count; entryIndex++)
        {
            int entry = checked(index + 2 + (entryIndex * 12));
            ushort first = ReadUInt16(table, entry);
            ushort last = ReadUInt16(table, entry + 2);
            if (first > last || last >= glyphCount || first <= previousEnd)
            {
                throw SfntFont.DataInvalid("SVG-glyphRange");
            }

            previousEnd = last;

            uint documentOffset = ReadUInt32(table, entry + 4);
            uint documentLength = ReadUInt32(table, entry + 8);
            if (documentOffset == 0 || documentLength == 0)
            {
                throw SfntFont.DataInvalid("SVG-documentRange");
            }

            EnsureOffsetRange(table, checked(indexOffset + documentOffset), documentLength, "SVG-document");
            try
            {
                ValidateSvgDocument(table.AsSpan(
                    checked((int)(indexOffset + documentOffset)),
                    checked((int)documentLength)));
            }
            catch (Exception exception) when (exception is IOException or XmlException)
            {
                throw SfntFont.DataInvalid("SVG-document");
            }
        }
    }

    private static void ValidateSvgDocument(ReadOnlySpan<byte> encoded)
    {
        const int maximumDocumentBytes = 4 * 1024 * 1024;
        byte[] document;
        if (encoded.Length >= 3 && encoded[0] == 0x1F && encoded[1] == 0x8B && encoded[2] == 0x08)
        {
            using var input = new MemoryStream(encoded.ToArray(), writable: false);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            var buffer = new byte[81920];
            int total = 0;
            int read;
            while ((read = gzip.Read(buffer, 0, Math.Min(buffer.Length, maximumDocumentBytes + 1 - total))) != 0)
            {
                total = checked(total + read);
                if (total > maximumDocumentBytes)
                {
                    throw SfntFont.DataInvalid("SVG-expandedSize");
                }

                output.Write(buffer, 0, read);
            }

            document = output.ToArray();
        }
        else
        {
            if (encoded.Length > maximumDocumentBytes)
            {
                throw SfntFont.DataInvalid("SVG-documentSize");
            }

            document = encoded.ToArray();
        }

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = maximumDocumentBytes,
            IgnoreComments = true,
            IgnoreProcessingInstructions = false
        };
        using var stream = new MemoryStream(document, writable: false);
        using XmlReader reader = XmlReader.Create(stream, settings);
        bool sawRoot = false;
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.ProcessingInstruction)
            {
                throw SfntFont.DataInvalid("SVG-processingInstruction");
            }

            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            if (!sawRoot)
            {
                if (reader.LocalName != "svg" || reader.NamespaceURI != "http://www.w3.org/2000/svg")
                {
                    throw SfntFont.DataInvalid("SVG-root");
                }

                sawRoot = true;
            }

            if (reader.NamespaceURI != "http://www.w3.org/2000/svg")
            {
                throw SfntFont.DataInvalid("SVG-elementNamespace");
            }

            if (reader.LocalName is "script" or "text" or "font" or "foreignObject" or "switch" or "a"
                or "view" or "image" or "style")
            {
                throw SfntFont.DataInvalid("SVG-restrictedElement");
            }

            if (!reader.HasAttributes)
            {
                continue;
            }

            while (reader.MoveToNextAttribute())
            {
                string value = reader.Value;
                bool isHref = reader.LocalName == "href";
                if (isHref && !value.StartsWith("#", StringComparison.Ordinal)
                    || value.Contains("javascript:", StringComparison.OrdinalIgnoreCase)
                    || value.Contains("@import", StringComparison.OrdinalIgnoreCase)
                    || value.Contains("url(", StringComparison.OrdinalIgnoreCase)
                       && !value.Contains("url(#", StringComparison.OrdinalIgnoreCase))
                {
                    throw SfntFont.DataInvalid("SVG-externalContent");
                }
            }

            reader.MoveToElement();
        }

        if (!sawRoot)
        {
            throw SfntFont.DataInvalid("SVG-root");
        }
    }

    private static void ValidateSbix(byte[] table, ushort glyphCount)
    {
        SfntFont.EnsureRange(table, 0, 8, "sbix-header");
        if (ReadUInt16(table, 0) != 1)
        {
            throw SfntFont.DataInvalid("sbix-version");
        }

        uint strikeCount = ReadUInt32(table, 4);
        if (strikeCount == 0 || strikeCount > 4096)
        {
            throw SfntFont.DataInvalid("sbix-strikeCount");
        }

        EnsureOffsetRange(table, 8, checked(strikeCount * 4), "sbix-strikeOffsets");
        for (int index = 0; index < strikeCount; index++)
        {
            uint strikeOffset = ReadUInt32(table, checked(8 + (index * 4)));
            uint offsetCount = checked((uint)glyphCount + 1);
            EnsureOffsetRange(table, strikeOffset, checked(4 + (offsetCount * 4)), "sbix-strike");
            uint previous = 0;
            for (int glyph = 0; glyph <= glyphCount; glyph++)
            {
                uint current = ReadUInt32(table, checked((int)strikeOffset + 4 + (glyph * 4)));
                if (current < previous || strikeOffset + current > table.Length)
                {
                    throw SfntFont.DataInvalid("sbix-glyphOffset");
                }

                previous = current;
            }
        }
    }

    private static void ValidateVersionAndCount(
        byte[] table,
        string tag,
        uint version,
        int headerLength,
        int recordLength)
    {
        SfntFont.EnsureRange(table, 0, headerLength, $"{tag}-header");
        if (ReadUInt32(table, 0) != version)
        {
            throw SfntFont.DataInvalid($"{tag}-version");
        }

        uint count = ReadUInt32(table, 4);
        EnsureOffsetRange(table, (uint)headerLength, checked(count * (uint)recordLength), $"{tag}-records");
    }

    private static void ValidateColrV1ListOffset(byte[] table, uint offset, int minimum, string detail)
    {
        if (offset != 0)
        {
            EnsureOffsetRange(table, offset, (uint)minimum, detail);
        }
    }

    private static void ValidateOptionalArray(
        byte[] table,
        uint offset,
        int count,
        int elementSize,
        string detail)
    {
        if (offset != 0)
        {
            EnsureOffsetRange(table, offset, checked((uint)(count * elementSize)), detail);
        }
    }

    private static void ValidateOptionalOffset(byte[] table, uint offset, string detail)
    {
        if (offset != 0)
        {
            EnsureOffsetRange(table, offset, 1, detail);
        }
    }

    private static void ValidateRelativeOffset(byte[] table, uint origin, uint offset, string detail)
    {
        if (origin > int.MaxValue || offset == 0 || offset > int.MaxValue
            || origin >= table.Length || offset >= table.Length - origin)
        {
            throw SfntFont.DataInvalid(detail);
        }
    }

    private static void ValidateGlyph(ushort glyph, ushort glyphCount, string detail)
    {
        if (glyph >= glyphCount)
        {
            throw SfntFont.DataInvalid(detail);
        }
    }

    private static void EnsureOffsetRange(byte[] table, uint offset, uint length, string detail)
    {
        if (offset > int.MaxValue || length > int.MaxValue)
        {
            throw SfntFont.DataInvalid(detail);
        }

        SfntFont.EnsureRange(table, (int)offset, (int)length, detail);
    }

    private static ushort ReadUInt16(byte[] table, int offset)
        => BinaryPrimitives.ReadUInt16BigEndian(table.AsSpan(offset, 2));

    private static uint ReadUInt32(byte[] table, int offset)
        => BinaryPrimitives.ReadUInt32BigEndian(table.AsSpan(offset, 4));
}
