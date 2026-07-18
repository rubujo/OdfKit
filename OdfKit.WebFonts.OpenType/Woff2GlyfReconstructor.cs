#if NET10_0_OR_GREATER
using System.Buffers.Binary;

namespace OdfKit.WebFonts.OpenType;

internal readonly record struct Woff2GlyfReconstruction(byte[] Glyf, byte[] Loca);

internal static class Woff2GlyfReconstructor
{
    private const ushort ArgWords = 0x0001;
    private const ushort MoreComponents = 0x0020;
    private const ushort HaveScale = 0x0008;
    private const ushort HaveXyScale = 0x0040;
    private const ushort HaveTwoByTwo = 0x0080;
    private const ushort HaveInstructions = 0x0100;

    internal static Woff2GlyfReconstruction Reconstruct(
        ReadOnlySpan<byte> transformed,
        int originalGlyfLength,
        int transformedLocaLength,
        int originalLocaLength,
        IReadOnlyDictionary<string, byte[]> tables,
        int maximumExpandedBytes)
    {
        if (maximumExpandedBytes <= 0
            || transformedLocaLength != 0
            || !tables.TryGetValue("head", out byte[]? head)
            || !tables.TryGetValue("maxp", out byte[]? maxp))
        {
            throw SfntFont.DataInvalid("WOFF2-glyf-dependencies");
        }

        SfntFont.EnsureRange(transformed, 0, 36, "WOFF2-glyf-header");
        SfntFont.EnsureRange(head, 0, 54, "WOFF2-head");
        SfntFont.EnsureRange(maxp, 0, 6, "WOFF2-maxp");
        ushort reserved = SfntFont.ReadUInt16(transformed, 0, "WOFF2-glyf-reserved");
        ushort optionFlags = SfntFont.ReadUInt16(transformed, 2, "WOFF2-glyf-options");
        ushort glyphCount = SfntFont.ReadUInt16(transformed, 4, "WOFF2-glyf-count");
        ushort indexFormat = SfntFont.ReadUInt16(transformed, 6, "WOFF2-glyf-indexFormat");
        ushort expectedGlyphCount = SfntFont.ReadUInt16(maxp, 4, "WOFF2-maxp-count");
        short expectedIndexFormat = SfntFont.ReadInt16(head, 50, "WOFF2-head-indexFormat");
        if (reserved != 0
            || (optionFlags & 0xFFFE) != 0
            || glyphCount == 0
            || glyphCount != expectedGlyphCount
            || indexFormat > 1
            || indexFormat != expectedIndexFormat)
        {
            throw SfntFont.DataInvalid("WOFF2-glyf-header");
        }

        int[] sizes = new int[7];
        int streamsLength = 0;
        for (int index = 0; index < sizes.Length; index++)
        {
            sizes[index] = CheckedInt(
                SfntFont.ReadUInt32(transformed, 8 + (index * 4), "WOFF2-glyf-stream-size"),
                "WOFF2-glyf-stream-size");
            if (sizes[index] > transformed.Length - 36 - streamsLength)
            {
                throw SfntFont.DataInvalid("WOFF2-glyf-stream-size");
            }

            streamsLength += sizes[index];
        }

        int bitmapLength = checked(4 * ((glyphCount + 31) / 32));
        int overlapLength = (optionFlags & 1) != 0 ? bitmapLength : 0;
        if (streamsLength != transformed.Length - 36 - overlapLength
            || sizes[0] != glyphCount * 2
            || sizes[5] < bitmapLength)
        {
            throw SfntFont.DataInvalid("WOFF2-glyf-streams");
        }

        int position = 36;
        ReadOnlySpan<byte> nContourStream = TakeStream(transformed, ref position, sizes[0]);
        ReadOnlySpan<byte> nPointsStream = TakeStream(transformed, ref position, sizes[1]);
        ReadOnlySpan<byte> flagStream = TakeStream(transformed, ref position, sizes[2]);
        ReadOnlySpan<byte> glyphStream = TakeStream(transformed, ref position, sizes[3]);
        ReadOnlySpan<byte> compositeStream = TakeStream(transformed, ref position, sizes[4]);
        ReadOnlySpan<byte> bboxData = TakeStream(transformed, ref position, sizes[5]);
        ReadOnlySpan<byte> instructionStream = TakeStream(transformed, ref position, sizes[6]);
        ReadOnlySpan<byte> overlapBitmap = TakeStream(transformed, ref position, overlapLength);
        if (position != transformed.Length)
        {
            throw SfntFont.DataInvalid("WOFF2-glyf-length");
        }

        ReadOnlySpan<byte> bboxBitmap = bboxData[..bitmapLength];
        ReadOnlySpan<byte> bboxStream = bboxData[bitmapLength..];
        int nPointsPosition = 0;
        int flagPosition = 0;
        int glyphPosition = 0;
        int compositePosition = 0;
        int bboxPosition = 0;
        int instructionPosition = 0;
        int capacity = Math.Min(Math.Max(originalGlyfLength, glyphCount * 10), maximumExpandedBytes);
        using var glyf = new MemoryStream(capacity);
        var locations = new uint[glyphCount + 1];
        for (int glyphIndex = 0; glyphIndex < glyphCount; glyphIndex++)
        {
            PadGlyphForLoca(glyf, indexFormat, maximumExpandedBytes);
            locations[glyphIndex] = CheckedUInt(glyf.Length, "WOFF2-glyf-offset");
            short contourCount = SfntFont.ReadInt16(
                nContourStream,
                glyphIndex * 2,
                "WOFF2-glyf-contours");
            bool hasBoundingBox = IsBitSet(bboxBitmap, glyphIndex);
            if (contourCount == 0)
            {
                if (hasBoundingBox || IsBitSet(overlapBitmap, glyphIndex))
                {
                    throw SfntFont.DataInvalid("WOFF2-glyf-empty");
                }

                continue;
            }

            if (contourCount > 0)
            {
                ReconstructSimpleGlyph(
                    glyf,
                    contourCount,
                    hasBoundingBox,
                    IsBitSet(overlapBitmap, glyphIndex),
                    nPointsStream,
                    ref nPointsPosition,
                    flagStream,
                    ref flagPosition,
                    glyphStream,
                    ref glyphPosition,
                    bboxStream,
                    ref bboxPosition,
                    instructionStream,
                    ref instructionPosition,
                    maximumExpandedBytes);
            }
            else if (contourCount == -1)
            {
                ReconstructCompositeGlyph(
                    glyf,
                    hasBoundingBox,
                    compositeStream,
                    ref compositePosition,
                    glyphStream,
                    ref glyphPosition,
                    bboxStream,
                    ref bboxPosition,
                    instructionStream,
                    ref instructionPosition,
                    maximumExpandedBytes);
            }
            else
            {
                throw SfntFont.DataInvalid("WOFF2-glyf-contours");
            }
        }

        PadGlyphForLoca(glyf, indexFormat, maximumExpandedBytes);
        locations[glyphCount] = CheckedUInt(glyf.Length, "WOFF2-glyf-offset");
        if (nPointsPosition != nPointsStream.Length
            || flagPosition != flagStream.Length
            || glyphPosition != glyphStream.Length
            || compositePosition != compositeStream.Length
            || bboxPosition != bboxStream.Length
            || instructionPosition != instructionStream.Length)
        {
            throw SfntFont.DataInvalid("WOFF2-glyf-trailing-data");
        }

        byte[] loca = CreateLoca(locations, indexFormat, originalLocaLength);
        return new Woff2GlyfReconstruction(glyf.ToArray(), loca);
    }

    private static void ReconstructSimpleGlyph(
        MemoryStream output,
        short contourCount,
        bool hasBoundingBox,
        bool overlaps,
        ReadOnlySpan<byte> nPointsStream,
        ref int nPointsPosition,
        ReadOnlySpan<byte> flagStream,
        ref int flagPosition,
        ReadOnlySpan<byte> glyphStream,
        ref int glyphPosition,
        ReadOnlySpan<byte> bboxStream,
        ref int bboxPosition,
        ReadOnlySpan<byte> instructionStream,
        ref int instructionPosition,
        int maximumExpandedBytes)
    {
        var endPoints = new ushort[contourCount];
        int pointCount = 0;
        for (int contour = 0; contour < contourCount; contour++)
        {
            ushort count = Read255UInt16(nPointsStream, ref nPointsPosition);
            if (count == 0 || count > ushort.MaxValue - pointCount)
            {
                throw SfntFont.DataInvalid("WOFF2-glyf-points");
            }

            pointCount += count;
            endPoints[contour] = checked((ushort)(pointCount - 1));
        }

        SfntFont.EnsureRange(flagStream, flagPosition, pointCount, "WOFF2-glyf-flags");
        var flags = new byte[pointCount];
        var xDeltas = new short[pointCount];
        var yDeltas = new short[pointCount];
        int x = 0;
        int y = 0;
        short xMin = short.MaxValue;
        short yMin = short.MaxValue;
        short xMax = short.MinValue;
        short yMax = short.MinValue;
        for (int point = 0; point < pointCount; point++)
        {
            byte transformedFlag = flagStream[flagPosition++];
            (short deltaX, short deltaY) = DecodeTriplet(
                transformedFlag,
                glyphStream,
                ref glyphPosition);
            x = checked(x + deltaX);
            y = checked(y + deltaY);
            if (x is < short.MinValue or > short.MaxValue
                || y is < short.MinValue or > short.MaxValue)
            {
                throw SfntFont.DataInvalid("WOFF2-glyf-coordinate");
            }

            xDeltas[point] = deltaX;
            yDeltas[point] = deltaY;
            flags[point] = (transformedFlag & 0x80) == 0 ? (byte)0x01 : (byte)0;
            if (deltaX == 0)
            {
                flags[point] |= 0x10;
            }
            if (deltaY == 0)
            {
                flags[point] |= 0x20;
            }

            xMin = Math.Min(xMin, (short)x);
            yMin = Math.Min(yMin, (short)y);
            xMax = Math.Max(xMax, (short)x);
            yMax = Math.Max(yMax, (short)y);
        }

        if (overlaps)
        {
            flags[0] |= 0x40;
        }

        if (hasBoundingBox)
        {
            ReadBoundingBox(
                bboxStream,
                ref bboxPosition,
                out xMin,
                out yMin,
                out xMax,
                out yMax);
        }

        ushort instructionLength = Read255UInt16(glyphStream, ref glyphPosition);
        SfntFont.EnsureRange(
            instructionStream,
            instructionPosition,
            instructionLength,
            "WOFF2-glyf-instructions");
        EnsureOutput(output, checked(12 + (contourCount * 2) + instructionLength + (pointCount * 5)), maximumExpandedBytes);
        WriteInt16(output, contourCount);
        WriteInt16(output, xMin);
        WriteInt16(output, yMin);
        WriteInt16(output, xMax);
        WriteInt16(output, yMax);
        foreach (ushort endPoint in endPoints)
        {
            WriteUInt16(output, endPoint);
        }

        WriteUInt16(output, instructionLength);
        output.Write(instructionStream.Slice(instructionPosition, instructionLength));
        instructionPosition += instructionLength;
        output.Write(flags);
        WriteCoordinateDeltas(output, xDeltas);
        WriteCoordinateDeltas(output, yDeltas);
        EnsureOutput(output, 0, maximumExpandedBytes);
    }

    private static void ReconstructCompositeGlyph(
        MemoryStream output,
        bool hasBoundingBox,
        ReadOnlySpan<byte> compositeStream,
        ref int compositePosition,
        ReadOnlySpan<byte> glyphStream,
        ref int glyphPosition,
        ReadOnlySpan<byte> bboxStream,
        ref int bboxPosition,
        ReadOnlySpan<byte> instructionStream,
        ref int instructionPosition,
        int maximumExpandedBytes)
    {
        if (!hasBoundingBox)
        {
            throw SfntFont.DataInvalid("WOFF2-glyf-composite-bbox");
        }

        ReadBoundingBox(
            bboxStream,
            ref bboxPosition,
            out short xMin,
            out short yMin,
            out short xMax,
            out short yMax);
        EnsureOutput(output, 10, maximumExpandedBytes);
        WriteInt16(output, -1);
        WriteInt16(output, xMin);
        WriteInt16(output, yMin);
        WriteInt16(output, xMax);
        WriteInt16(output, yMax);

        bool instructions = false;
        int componentCount = 0;
        ushort flags;
        do
        {
            if (++componentCount > 4096)
            {
                throw SfntFont.DataInvalid("WOFF2-glyf-components");
            }

            SfntFont.EnsureRange(compositeStream, compositePosition, 4, "WOFF2-glyf-component");
            flags = SfntFont.ReadUInt16(compositeStream, compositePosition, "WOFF2-glyf-component-flags");
            int scaleFlagCount = ((flags & HaveScale) != 0 ? 1 : 0)
                + ((flags & HaveXyScale) != 0 ? 1 : 0)
                + ((flags & HaveTwoByTwo) != 0 ? 1 : 0);
            if (scaleFlagCount > 1)
            {
                throw SfntFont.DataInvalid("WOFF2-glyf-component-scale");
            }

            int argumentLength = (flags & ArgWords) != 0 ? 4 : 2;
            int scaleLength = (flags & HaveScale) != 0
                ? 2
                : (flags & HaveXyScale) != 0
                    ? 4
                    : (flags & HaveTwoByTwo) != 0 ? 8 : 0;
            int componentLength = checked(4 + argumentLength + scaleLength);
            SfntFont.EnsureRange(
                compositeStream,
                compositePosition,
                componentLength,
                "WOFF2-glyf-component");
            EnsureOutput(output, componentLength, maximumExpandedBytes);
            output.Write(compositeStream.Slice(compositePosition, componentLength));
            compositePosition += componentLength;
            instructions |= (flags & HaveInstructions) != 0;
        }
        while ((flags & MoreComponents) != 0);

        if (instructions)
        {
            ushort instructionLength = Read255UInt16(glyphStream, ref glyphPosition);
            SfntFont.EnsureRange(
                instructionStream,
                instructionPosition,
                instructionLength,
                "WOFF2-glyf-instructions");
            EnsureOutput(output, checked(2 + instructionLength), maximumExpandedBytes);
            WriteUInt16(output, instructionLength);
            output.Write(instructionStream.Slice(instructionPosition, instructionLength));
            instructionPosition += instructionLength;
        }
    }

    private static (short X, short Y) DecodeTriplet(
        byte transformedFlag,
        ReadOnlySpan<byte> glyphStream,
        ref int position)
    {
        int index = transformedFlag & 0x7F;
        int x;
        int y;
        if (index < 10)
        {
            SfntFont.EnsureRange(glyphStream, position, 1, "WOFF2-glyf-triplet");
            x = 0;
            y = ((index >> 1) * 256) + glyphStream[position++];
        }
        else if (index < 20)
        {
            SfntFont.EnsureRange(glyphStream, position, 1, "WOFF2-glyf-triplet");
            int value = index - 10;
            x = ((value >> 1) * 256) + glyphStream[position++];
            y = 0;
        }
        else if (index < 84)
        {
            SfntFont.EnsureRange(glyphStream, position, 1, "WOFF2-glyf-triplet");
            int value = index - 20;
            byte packed = glyphStream[position++];
            x = ((value >> 4) * 16) + (packed >> 4) + 1;
            y = (((value >> 2) & 3) * 16) + (packed & 0x0F) + 1;
        }
        else if (index < 120)
        {
            SfntFont.EnsureRange(glyphStream, position, 2, "WOFF2-glyf-triplet");
            int value = index - 84;
            x = ((value / 12) * 256) + glyphStream[position++] + 1;
            y = (((value % 12) >> 2) * 256) + glyphStream[position++] + 1;
        }
        else if (index < 124)
        {
            SfntFont.EnsureRange(glyphStream, position, 3, "WOFF2-glyf-triplet");
            byte first = glyphStream[position++];
            byte middle = glyphStream[position++];
            byte last = glyphStream[position++];
            x = (first << 4) | (middle >> 4);
            y = ((middle & 0x0F) << 8) | last;
        }
        else
        {
            SfntFont.EnsureRange(glyphStream, position, 4, "WOFF2-glyf-triplet");
            x = SfntFont.ReadUInt16(glyphStream, position, "WOFF2-glyf-triplet-x");
            y = SfntFont.ReadUInt16(glyphStream, position + 2, "WOFF2-glyf-triplet-y");
            position += 4;
        }

        if (index < 10)
        {
            y = ApplySign(y, (index & 1) != 0);
        }
        else if (index < 20)
        {
            x = ApplySign(x, ((index - 10) & 1) != 0);
        }
        else
        {
            int signBits = index < 84
                ? index - 20
                : index < 120 ? index - 84 : index - 120;
            x = ApplySign(x, (signBits & 1) != 0);
            y = ApplySign(y, (signBits & 2) != 0);
        }

        if (x is < short.MinValue or > short.MaxValue
            || y is < short.MinValue or > short.MaxValue)
        {
            throw SfntFont.DataInvalid("WOFF2-glyf-triplet-range");
        }

        return ((short)x, (short)y);
    }

    private static int ApplySign(int magnitude, bool positive)
        => positive ? magnitude : -magnitude;

    private static void WriteCoordinateDeltas(Stream output, ReadOnlySpan<short> deltas)
    {
        foreach (short delta in deltas)
        {
            if (delta != 0)
            {
                WriteInt16(output, delta);
            }
        }
    }

    private static void ReadBoundingBox(
        ReadOnlySpan<byte> bboxStream,
        ref int position,
        out short xMin,
        out short yMin,
        out short xMax,
        out short yMax)
    {
        SfntFont.EnsureRange(bboxStream, position, 8, "WOFF2-glyf-bbox");
        xMin = SfntFont.ReadInt16(bboxStream, position, "WOFF2-glyf-xMin");
        yMin = SfntFont.ReadInt16(bboxStream, position + 2, "WOFF2-glyf-yMin");
        xMax = SfntFont.ReadInt16(bboxStream, position + 4, "WOFF2-glyf-xMax");
        yMax = SfntFont.ReadInt16(bboxStream, position + 6, "WOFF2-glyf-yMax");
        position += 8;
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

    private static byte[] CreateLoca(uint[] locations, ushort indexFormat, int originalLength)
    {
        int entrySize = indexFormat == 0 ? 2 : 4;
        int requiredLength = checked(locations.Length * entrySize);
        if (originalLength != requiredLength)
        {
            throw SfntFont.DataInvalid("WOFF2-loca-length");
        }

        var loca = new byte[requiredLength];
        for (int index = 0; index < locations.Length; index++)
        {
            if (indexFormat == 0)
            {
                if ((locations[index] & 1) != 0 || locations[index] / 2 > ushort.MaxValue)
                {
                    throw SfntFont.DataInvalid("WOFF2-loca-offset");
                }

                BinaryPrimitives.WriteUInt16BigEndian(
                    loca.AsSpan(index * 2, 2),
                    checked((ushort)(locations[index] / 2)));
            }
            else
            {
                BinaryPrimitives.WriteUInt32BigEndian(loca.AsSpan(index * 4, 4), locations[index]);
            }
        }

        return loca;
    }

    private static void PadGlyphForLoca(MemoryStream output, ushort indexFormat, int maximumExpandedBytes)
    {
        if (indexFormat == 0 && (output.Length & 1) != 0)
        {
            EnsureOutput(output, 1, maximumExpandedBytes);
            output.WriteByte(0);
        }
    }

    private static void EnsureOutput(MemoryStream output, int additionalLength, int maximumExpandedBytes)
    {
        if (additionalLength < 0 || output.Length > maximumExpandedBytes - additionalLength)
        {
            throw SfntFont.DataInvalid("WOFF2-glyf-expanded-size");
        }
    }

    private static ReadOnlySpan<byte> TakeStream(ReadOnlySpan<byte> data, ref int position, int length)
    {
        SfntFont.EnsureRange(data, position, length, "WOFF2-glyf-stream");
        ReadOnlySpan<byte> result = data.Slice(position, length);
        position += length;
        return result;
    }

    private static bool IsBitSet(ReadOnlySpan<byte> bitmap, int index)
        => !bitmap.IsEmpty && (bitmap[index >> 3] & (0x80 >> (index & 7))) != 0;

    private static void WriteUInt16(Stream stream, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        stream.Write(bytes);
    }

    private static void WriteInt16(Stream stream, short value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteInt16BigEndian(bytes, value);
        stream.Write(bytes);
    }

    private static int CheckedInt(uint value, string detail)
        => value <= int.MaxValue ? (int)value : throw SfntFont.DataInvalid(detail);

    private static uint CheckedUInt(long value, string detail)
        => value is >= 0 and <= uint.MaxValue ? (uint)value : throw SfntFont.DataInvalid(detail);
}
#endif
