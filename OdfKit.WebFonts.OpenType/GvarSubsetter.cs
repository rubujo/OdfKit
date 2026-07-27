using System.Buffers.Binary;

namespace OdfKit.WebFonts.OpenType;

internal static class GvarSubsetter
{
    private const ushort LongOffsets = 0x0001;

    internal static byte[] Build(
        byte[] source,
        byte[] fvar,
        ushort glyphCount,
        ISet<ushort> selectedGlyphs,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SfntFont.EnsureRange(source, 0, 20, "gvar-header");
        SfntFont.EnsureRange(fvar, 0, 16, "fvar-header");
        if (SfntFont.ReadUInt16(source, 0, "gvar-version") != 1
            || SfntFont.ReadUInt16(source, 2, "gvar-version") != 0
            || SfntFont.ReadUInt16(fvar, 0, "fvar-version") != 1
            || SfntFont.ReadUInt16(fvar, 2, "fvar-version") != 0)
        {
            throw SfntFont.DataInvalid("variation-version");
        }

        ushort axisCount = SfntFont.ReadUInt16(source, 4, "gvar-axisCount");
        ushort fvarAxisCount = SfntFont.ReadUInt16(fvar, 8, "fvar-axisCount");
        ushort fvarAxisSize = SfntFont.ReadUInt16(fvar, 10, "fvar-axisSize");
        ushort instanceCount = SfntFont.ReadUInt16(fvar, 12, "fvar-instanceCount");
        ushort instanceSize = SfntFont.ReadUInt16(fvar, 14, "fvar-instanceSize");
        int axesOffset = SfntFont.ReadUInt16(fvar, 4, "fvar-axesOffset");
        int instanceSizeWithoutPostScriptName = (axisCount * 4) + 4;
        int instanceSizeWithPostScriptName = instanceSizeWithoutPostScriptName + 2;
        if (axisCount == 0
            || axisCount != fvarAxisCount
            || SfntFont.ReadUInt16(fvar, 6, "fvar-reserved") != 2
            || axesOffset < 16
            || fvarAxisSize != 20
            || instanceSize != instanceSizeWithoutPostScriptName
                && instanceSize != instanceSizeWithPostScriptName)
        {
            throw SfntFont.DataInvalid("variation-axis");
        }

        int fvarRecordsLength = ToInt(
            ((long)axisCount * fvarAxisSize) + ((long)instanceCount * instanceSize),
            "fvar-records");
        SfntFont.EnsureRange(fvar, axesOffset, fvarRecordsLength, "fvar-records");
        if (SfntFont.ReadUInt16(source, 12, "gvar-glyphCount") != glyphCount)
        {
            throw SfntFont.DataInvalid("gvar-glyphCount");
        }

        ushort flags = SfntFont.ReadUInt16(source, 14, "gvar-flags");
        if ((flags & ~LongOffsets) != 0)
        {
            throw SfntFont.DataInvalid("gvar-flags");
        }

        bool usesLongOffsets = (flags & LongOffsets) != 0;
        int offsetSize = usesLongOffsets ? 4 : 2;
        int offsetCount = glyphCount + 1;
        int offsetsLength = offsetCount * offsetSize;
        SfntFont.EnsureRange(source, 20, offsetsLength, "gvar-offsets");
        int dataOffset = ToInt(SfntFont.ReadUInt32(source, 16, "gvar-dataOffset"), "gvar-dataOffset");
        if (dataOffset < 20 + offsetsLength || dataOffset > source.Length)
        {
            throw SfntFont.DataInvalid("gvar-dataOffset");
        }

        uint sharedTuplesOffset = SfntFont.ReadUInt32(source, 8, "gvar-sharedTuplesOffset");
        ushort sharedTupleCount = SfntFont.ReadUInt16(source, 6, "gvar-sharedTupleCount");
        long sharedTuplesLength = (long)sharedTupleCount * axisCount * 2;
        if (sharedTupleCount > 0)
        {
            SfntFont.EnsureRange(
                source,
                ToInt(sharedTuplesOffset, "gvar-sharedTuples"),
                ToInt(sharedTuplesLength, "gvar-sharedTuples"),
                "gvar-sharedTuples");
        }

        uint[] offsets = ReadOffsets(source, offsetCount, usesLongOffsets, dataOffset);
        long selectedDataLength = 0;
        for (int glyph = 0; glyph < glyphCount; glyph++)
        {
            if (selectedGlyphs.Contains((ushort)glyph))
            {
                selectedDataLength += offsets[glyph + 1] - offsets[glyph];
            }
        }

        int outputLength = ToInt(dataOffset + selectedDataLength, "gvar-output");
        var output = new byte[outputLength];
        Buffer.BlockCopy(source, 0, output, 0, dataOffset);
        var rebuiltOffsets = new uint[offsetCount];
        int destination = dataOffset;
        for (int glyph = 0; glyph < glyphCount; glyph++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            rebuiltOffsets[glyph] = checked((uint)(destination - dataOffset));
            uint start = offsets[glyph];
            uint end = offsets[glyph + 1];
            if (selectedGlyphs.Contains((ushort)glyph) && end > start)
            {
                int absoluteStart = ToInt(dataOffset + start, "gvar-copy");
                int length = ToInt(end - start, "gvar-copy");
                Buffer.BlockCopy(source, absoluteStart, output, destination, length);
                destination += length;
            }
        }

        rebuiltOffsets[glyphCount] = checked((uint)(destination - dataOffset));
        WriteOffsets(output, rebuiltOffsets, usesLongOffsets);
        return output;
    }

    private static uint[] ReadOffsets(byte[] source, int count, bool usesLongOffsets, int dataOffset)
    {
        var offsets = new uint[count];
        uint previous = 0;
        uint dataLength = checked((uint)(source.Length - dataOffset));
        for (int index = 0; index < count; index++)
        {
            uint offset = usesLongOffsets
                ? SfntFont.ReadUInt32(source, 20 + (index * 4), "gvar-offset")
                : checked((uint)SfntFont.ReadUInt16(source, 20 + (index * 2), "gvar-offset") * 2);
            if (offset < previous || offset > dataLength)
            {
                throw SfntFont.DataInvalid("gvar-offset-order");
            }

            offsets[index] = offset;
            previous = offset;
        }

        return offsets;
    }

    private static void WriteOffsets(byte[] output, uint[] offsets, bool usesLongOffsets)
    {
        for (int index = 0; index < offsets.Length; index++)
        {
            uint offset = offsets[index];
            if (usesLongOffsets)
            {
                BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(20 + (index * 4), 4), offset);
            }
            else
            {
                if ((offset & 1) != 0 || offset / 2 > ushort.MaxValue)
                {
                    throw SfntFont.DataInvalid("gvar-short-offset");
                }

                BinaryPrimitives.WriteUInt16BigEndian(
                    output.AsSpan(20 + (index * 2), 2),
                    checked((ushort)(offset / 2)));
            }
        }
    }

    private static int ToInt(long value, string detail)
    {
        if (value < 0 || value > int.MaxValue)
        {
            throw SfntFont.DataInvalid(detail);
        }

        return (int)value;
    }
}
