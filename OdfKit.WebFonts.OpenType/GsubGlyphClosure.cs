namespace OdfKit.WebFonts.OpenType;

internal static class GsubGlyphClosure
{
    internal static void Add(
        byte[] table,
        HashSet<ushort> glyphs,
        ushort glyphCount,
        CancellationToken cancellationToken = default)
    {
        SfntFont.EnsureRange(table, 0, 10, "GSUB-header");
        ushort majorVersion = SfntFont.ReadUInt16(table, 0, "GSUB-version");
        if (majorVersion != 1)
        {
            throw SfntFont.DataInvalid("GSUB-version");
        }

        ushort lookupListOffset = SfntFont.ReadUInt16(table, 8, "GSUB-lookupList");
        SfntFont.EnsureRange(table, lookupListOffset, 2, "GSUB-lookupList");
        ushort lookupCount = SfntFont.ReadUInt16(table, lookupListOffset, "GSUB-lookupCount");
        if (lookupCount > 16_384)
        {
            throw SfntFont.DataInvalid("GSUB-lookupCount");
        }

        SfntFont.EnsureRange(table, lookupListOffset + 2, checked(lookupCount * 2), "GSUB-lookups");
        int maximumIterations = Math.Min(glyphCount, (ushort)4096);
        for (int iteration = 0; iteration < maximumIterations; iteration++)
        {
            int before = glyphs.Count;
            for (int lookupIndex = 0; lookupIndex < lookupCount; lookupIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ushort relativeOffset = SfntFont.ReadUInt16(
                    table,
                    lookupListOffset + 2 + (lookupIndex * 2),
                    "GSUB-lookupOffset");
                int lookupOffset = checked(lookupListOffset + relativeOffset);
                ApplyLookup(table, lookupOffset, glyphs, glyphCount);
            }

            if (glyphs.Count == before)
            {
                return;
            }
        }

        throw SfntFont.DataInvalid("GSUB-closure");
    }

    private static void ApplyLookup(
        byte[] table,
        int lookupOffset,
        HashSet<ushort> glyphs,
        ushort glyphCount)
    {
        SfntFont.EnsureRange(table, lookupOffset, 6, "GSUB-lookup");
        ushort lookupType = SfntFont.ReadUInt16(table, lookupOffset, "GSUB-lookupType");
        ushort subtableCount = SfntFont.ReadUInt16(table, lookupOffset + 4, "GSUB-subtableCount");
        if (subtableCount > 16_384)
        {
            throw SfntFont.DataInvalid("GSUB-subtableCount");
        }

        SfntFont.EnsureRange(table, lookupOffset + 6, checked(subtableCount * 2), "GSUB-subtables");
        for (int index = 0; index < subtableCount; index++)
        {
            ushort relativeOffset = SfntFont.ReadUInt16(
                table,
                lookupOffset + 6 + (index * 2),
                "GSUB-subtableOffset");
            ApplySubtable(table, lookupType, checked(lookupOffset + relativeOffset), glyphs, glyphCount, 0);
        }
    }

    private static void ApplySubtable(
        byte[] table,
        ushort lookupType,
        int offset,
        HashSet<ushort> glyphs,
        ushort glyphCount,
        int extensionDepth)
    {
        if (extensionDepth > 4)
        {
            throw SfntFont.DataInvalid("GSUB-extensionDepth");
        }

        switch (lookupType)
        {
            case 1:
                ApplySingle(table, offset, glyphs, glyphCount);
                break;
            case 2:
                ApplyMultiple(table, offset, glyphs, glyphCount);
                break;
            case 3:
                ApplyAlternate(table, offset, glyphs, glyphCount);
                break;
            case 4:
                ApplyLigature(table, offset, glyphs, glyphCount);
                break;
            case 5:
            case 6:
                ValidateContextHeader(table, offset);
                break;
            case 7:
                ApplyExtension(table, offset, glyphs, glyphCount, extensionDepth);
                break;
            case 8:
                ApplyReverseChain(table, offset, glyphs, glyphCount);
                break;
            default:
                throw SfntFont.DataInvalid("GSUB-lookupType");
        }
    }

    private static void ApplySingle(byte[] table, int offset, HashSet<ushort> glyphs, ushort glyphCount)
    {
        ushort format = SfntFont.ReadUInt16(table, offset, "GSUB-singleFormat");
        ushort coverageOffset = SfntFont.ReadUInt16(table, offset + 2, "GSUB-coverage");
        IReadOnlyList<ushort> coverage = ReadCoverage(table, checked(offset + coverageOffset), glyphCount);
        if (format == 1)
        {
            short delta = SfntFont.ReadInt16(table, offset + 4, "GSUB-singleDelta");
            foreach (ushort input in coverage)
            {
                if (glyphs.Contains(input))
                {
                    AddGlyph(glyphs, unchecked((ushort)(input + delta)), glyphCount);
                }
            }
        }
        else if (format == 2)
        {
            ushort count = SfntFont.ReadUInt16(table, offset + 4, "GSUB-singleCount");
            if (count != coverage.Count)
            {
                throw SfntFont.DataInvalid("GSUB-singleCount");
            }

            SfntFont.EnsureRange(table, offset + 6, checked(count * 2), "GSUB-singleGlyphs");
            for (int index = 0; index < count; index++)
            {
                if (glyphs.Contains(coverage[index]))
                {
                    AddGlyph(
                        glyphs,
                        SfntFont.ReadUInt16(table, offset + 6 + (index * 2), "GSUB-singleGlyph"),
                        glyphCount);
                }
            }
        }
        else
        {
            throw SfntFont.DataInvalid("GSUB-singleFormat");
        }
    }

    private static void ApplyMultiple(byte[] table, int offset, HashSet<ushort> glyphs, ushort glyphCount)
    {
        if (SfntFont.ReadUInt16(table, offset, "GSUB-multipleFormat") != 1)
        {
            throw SfntFont.DataInvalid("GSUB-multipleFormat");
        }

        ushort coverageOffset = SfntFont.ReadUInt16(table, offset + 2, "GSUB-coverage");
        IReadOnlyList<ushort> coverage = ReadCoverage(table, checked(offset + coverageOffset), glyphCount);
        ushort count = SfntFont.ReadUInt16(table, offset + 4, "GSUB-sequenceCount");
        if (count != coverage.Count)
        {
            throw SfntFont.DataInvalid("GSUB-sequenceCount");
        }

        SfntFont.EnsureRange(table, offset + 6, checked(count * 2), "GSUB-sequences");
        for (int index = 0; index < count; index++)
        {
            if (!glyphs.Contains(coverage[index]))
            {
                continue;
            }

            ushort sequenceOffset = SfntFont.ReadUInt16(table, offset + 6 + (index * 2), "GSUB-sequence");
            int sequence = checked(offset + sequenceOffset);
            ushort glyphCountInSequence = SfntFont.ReadUInt16(table, sequence, "GSUB-sequenceGlyphCount");
            SfntFont.EnsureRange(table, sequence + 2, checked(glyphCountInSequence * 2), "GSUB-sequenceGlyphs");
            for (int glyphIndex = 0; glyphIndex < glyphCountInSequence; glyphIndex++)
            {
                AddGlyph(
                    glyphs,
                    SfntFont.ReadUInt16(table, sequence + 2 + (glyphIndex * 2), "GSUB-sequenceGlyph"),
                    glyphCount);
            }
        }
    }

    private static void ApplyAlternate(byte[] table, int offset, HashSet<ushort> glyphs, ushort glyphCount)
    {
        if (SfntFont.ReadUInt16(table, offset, "GSUB-alternateFormat") != 1)
        {
            throw SfntFont.DataInvalid("GSUB-alternateFormat");
        }

        ushort coverageOffset = SfntFont.ReadUInt16(table, offset + 2, "GSUB-coverage");
        IReadOnlyList<ushort> coverage = ReadCoverage(table, checked(offset + coverageOffset), glyphCount);
        ushort count = SfntFont.ReadUInt16(table, offset + 4, "GSUB-alternateCount");
        if (count != coverage.Count)
        {
            throw SfntFont.DataInvalid("GSUB-alternateCount");
        }

        SfntFont.EnsureRange(table, offset + 6, checked(count * 2), "GSUB-alternateSets");
        for (int index = 0; index < count; index++)
        {
            if (!glyphs.Contains(coverage[index]))
            {
                continue;
            }

            ushort setOffset = SfntFont.ReadUInt16(table, offset + 6 + (index * 2), "GSUB-alternateSet");
            int set = checked(offset + setOffset);
            ushort alternateCount = SfntFont.ReadUInt16(table, set, "GSUB-alternateGlyphCount");
            SfntFont.EnsureRange(table, set + 2, checked(alternateCount * 2), "GSUB-alternateGlyphs");
            for (int glyphIndex = 0; glyphIndex < alternateCount; glyphIndex++)
            {
                AddGlyph(
                    glyphs,
                    SfntFont.ReadUInt16(table, set + 2 + (glyphIndex * 2), "GSUB-alternateGlyph"),
                    glyphCount);
            }
        }
    }

    private static void ApplyLigature(byte[] table, int offset, HashSet<ushort> glyphs, ushort glyphCount)
    {
        if (SfntFont.ReadUInt16(table, offset, "GSUB-ligatureFormat") != 1)
        {
            throw SfntFont.DataInvalid("GSUB-ligatureFormat");
        }

        ushort coverageOffset = SfntFont.ReadUInt16(table, offset + 2, "GSUB-coverage");
        IReadOnlyList<ushort> coverage = ReadCoverage(table, checked(offset + coverageOffset), glyphCount);
        ushort setCount = SfntFont.ReadUInt16(table, offset + 4, "GSUB-ligatureSetCount");
        if (setCount != coverage.Count)
        {
            throw SfntFont.DataInvalid("GSUB-ligatureSetCount");
        }

        SfntFont.EnsureRange(table, offset + 6, checked(setCount * 2), "GSUB-ligatureSets");
        for (int index = 0; index < setCount; index++)
        {
            if (!glyphs.Contains(coverage[index]))
            {
                continue;
            }

            int set = checked(offset + SfntFont.ReadUInt16(table, offset + 6 + (index * 2), "GSUB-ligatureSet"));
            ushort ligatureCount = SfntFont.ReadUInt16(table, set, "GSUB-ligatureCount");
            SfntFont.EnsureRange(table, set + 2, checked(ligatureCount * 2), "GSUB-ligatures");
            for (int ligatureIndex = 0; ligatureIndex < ligatureCount; ligatureIndex++)
            {
                int ligature = checked(set + SfntFont.ReadUInt16(
                    table,
                    set + 2 + (ligatureIndex * 2),
                    "GSUB-ligature"));
                ushort ligatureGlyph = SfntFont.ReadUInt16(table, ligature, "GSUB-ligatureGlyph");
                ushort componentCount = SfntFont.ReadUInt16(table, ligature + 2, "GSUB-componentCount");
                if (componentCount == 0)
                {
                    throw SfntFont.DataInvalid("GSUB-componentCount");
                }

                SfntFont.EnsureRange(table, ligature + 4, checked((componentCount - 1) * 2), "GSUB-components");
                bool allSelected = true;
                for (int componentIndex = 0; componentIndex < componentCount - 1; componentIndex++)
                {
                    ushort component = SfntFont.ReadUInt16(
                        table,
                        ligature + 4 + (componentIndex * 2),
                        "GSUB-component");
                    if (component >= glyphCount)
                    {
                        throw SfntFont.DataInvalid("GSUB-component");
                    }

                    allSelected &= glyphs.Contains(component);
                }

                if (allSelected)
                {
                    AddGlyph(glyphs, ligatureGlyph, glyphCount);
                }
            }
        }
    }

    private static void ApplyExtension(
        byte[] table,
        int offset,
        HashSet<ushort> glyphs,
        ushort glyphCount,
        int extensionDepth)
    {
        if (SfntFont.ReadUInt16(table, offset, "GSUB-extensionFormat") != 1)
        {
            throw SfntFont.DataInvalid("GSUB-extensionFormat");
        }

        ushort extensionType = SfntFont.ReadUInt16(table, offset + 2, "GSUB-extensionType");
        uint extensionOffset = SfntFont.ReadUInt32(table, offset + 4, "GSUB-extensionOffset");
        if (extensionOffset > int.MaxValue)
        {
            throw SfntFont.DataInvalid("GSUB-extensionOffset");
        }

        ApplySubtable(
            table,
            extensionType,
            checked(offset + (int)extensionOffset),
            glyphs,
            glyphCount,
            extensionDepth + 1);
    }

    private static void ApplyReverseChain(byte[] table, int offset, HashSet<ushort> glyphs, ushort glyphCount)
    {
        if (SfntFont.ReadUInt16(table, offset, "GSUB-reverseFormat") != 1)
        {
            throw SfntFont.DataInvalid("GSUB-reverseFormat");
        }

        ushort coverageOffset = SfntFont.ReadUInt16(table, offset + 2, "GSUB-coverage");
        IReadOnlyList<ushort> coverage = ReadCoverage(table, checked(offset + coverageOffset), glyphCount);
        int position = offset + 4;
        ushort backtrackCount = SfntFont.ReadUInt16(table, position, "GSUB-backtrackCount");
        position = checked(position + 2 + (backtrackCount * 2));
        ushort lookaheadCount = SfntFont.ReadUInt16(table, position, "GSUB-lookaheadCount");
        position = checked(position + 2 + (lookaheadCount * 2));
        ushort glyphMappingCount = SfntFont.ReadUInt16(table, position, "GSUB-reverseGlyphCount");
        position += 2;
        if (glyphMappingCount != coverage.Count)
        {
            throw SfntFont.DataInvalid("GSUB-reverseGlyphCount");
        }

        SfntFont.EnsureRange(table, position, checked(glyphMappingCount * 2), "GSUB-reverseGlyphs");
        for (int index = 0; index < glyphMappingCount; index++)
        {
            if (glyphs.Contains(coverage[index]))
            {
                AddGlyph(
                    glyphs,
                    SfntFont.ReadUInt16(table, position + (index * 2), "GSUB-reverseGlyph"),
                    glyphCount);
            }
        }
    }

    private static IReadOnlyList<ushort> ReadCoverage(byte[] table, int offset, ushort glyphCount)
    {
        ushort format = SfntFont.ReadUInt16(table, offset, "GSUB-coverageFormat");
        if (format == 1)
        {
            ushort count = SfntFont.ReadUInt16(table, offset + 2, "GSUB-coverageCount");
            SfntFont.EnsureRange(table, offset + 4, checked(count * 2), "GSUB-coverageGlyphs");
            var glyphs = new ushort[count];
            ushort previous = 0;
            for (int index = 0; index < count; index++)
            {
                ushort glyph = SfntFont.ReadUInt16(table, offset + 4 + (index * 2), "GSUB-coverageGlyph");
                if (glyph >= glyphCount || (index > 0 && glyph <= previous))
                {
                    throw SfntFont.DataInvalid("GSUB-coverageGlyph");
                }

                glyphs[index] = glyph;
                previous = glyph;
            }

            return glyphs;
        }

        if (format == 2)
        {
            ushort rangeCount = SfntFont.ReadUInt16(table, offset + 2, "GSUB-coverageRangeCount");
            SfntFont.EnsureRange(table, offset + 4, checked(rangeCount * 6), "GSUB-coverageRanges");
            var glyphs = new List<ushort>();
            ushort previousEnd = 0;
            for (int index = 0; index < rangeCount; index++)
            {
                int range = offset + 4 + (index * 6);
                ushort first = SfntFont.ReadUInt16(table, range, "GSUB-coverageStart");
                ushort end = SfntFont.ReadUInt16(table, range + 2, "GSUB-coverageEnd");
                ushort startIndex = SfntFont.ReadUInt16(table, range + 4, "GSUB-coverageIndex");
                if (first > end || end >= glyphCount || (index > 0 && first <= previousEnd) || startIndex != glyphs.Count)
                {
                    throw SfntFont.DataInvalid("GSUB-coverageRange");
                }

                for (int glyph = first; glyph <= end; glyph++)
                {
                    glyphs.Add((ushort)glyph);
                }

                previousEnd = end;
            }

            return glyphs;
        }

        throw SfntFont.DataInvalid("GSUB-coverageFormat");
    }

    private static void ValidateContextHeader(byte[] table, int offset)
    {
        ushort format = SfntFont.ReadUInt16(table, offset, "GSUB-contextFormat");
        if (format is < 1 or > 3)
        {
            throw SfntFont.DataInvalid("GSUB-contextFormat");
        }
    }

    private static void AddGlyph(HashSet<ushort> glyphs, ushort glyph, ushort glyphCount)
    {
        if (glyph == 0 || glyph >= glyphCount)
        {
            throw SfntFont.DataInvalid("GSUB-glyph");
        }

        glyphs.Add(glyph);
    }
}
