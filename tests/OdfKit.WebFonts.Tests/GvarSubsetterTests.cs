using System.Buffers.Binary;
using OdfKit.WebFonts.OpenType;

namespace OdfKit.WebFonts.Tests;

public sealed class GvarSubsetterTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Build_ClearsUnselectedGlyphVariationData(bool longOffsets)
    {
        byte[][] glyphData = longOffsets
            ? [[0x10], [0x20, 0x21, 0x22], [0x30, 0x31]]
            : [[0x10, 0x11], [0x20, 0x21, 0x22, 0x23], [0x30, 0x31]];
        byte[] source = CreateGvar(longOffsets, glyphData);

        byte[] subset = GvarSubsetter.Build(
            source,
            CreateFvar(axisCount: 1),
            glyphCount: 3,
            new HashSet<ushort> { 0, 2 },
            TestContext.Current.CancellationToken);

        uint[] offsets = ReadGvarOffsets(subset, longOffsets, glyphCount: 3);
        Assert.Equal((uint)0, offsets[0]);
        Assert.Equal((uint)glyphData[0].Length, offsets[1]);
        Assert.Equal(offsets[1], offsets[2]);
        Assert.Equal((uint)(glyphData[0].Length + glyphData[2].Length), offsets[3]);
        int dataOffset = checked((int)BinaryPrimitives.ReadUInt32BigEndian(subset.AsSpan(16, 4)));
        Assert.Equal(
            glyphData[0].Concat(glyphData[2]).ToArray(),
            subset.AsSpan(dataOffset).ToArray());
    }

    [Fact]
    public void Build_RejectsAxisCountMismatch()
    {
        byte[] source = CreateGvar(false, [[0x10, 0x11]]);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => GvarSubsetter.Build(
            source,
            CreateFvar(axisCount: 2),
            glyphCount: 1,
            new HashSet<ushort> { 0 },
            TestContext.Current.CancellationToken));

        Assert.Contains("variation-axis", exception.Message, StringComparison.Ordinal);
    }

    private static byte[] CreateFvar(ushort axisCount)
    {
        int length = 16 + (axisCount * 20);
        var output = new byte[length];
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(0, 2), 1);
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(4, 2), 16);
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(6, 2), 2);
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(8, 2), axisCount);
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(10, 2), 20);
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(14, 2), checked((ushort)((axisCount * 4) + 4)));
        return output;
    }

    private static byte[] CreateGvar(bool longOffsets, IReadOnlyList<byte[]> glyphData)
    {
        int offsetSize = longOffsets ? 4 : 2;
        int dataOffset = 20 + ((glyphData.Count + 1) * offsetSize);
        int dataLength = glyphData.Sum(item => item.Length);
        var output = new byte[dataOffset + dataLength];
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(0, 2), 1);
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(4, 2), 1);
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(12, 2), checked((ushort)glyphData.Count));
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(14, 2), longOffsets ? (ushort)1 : (ushort)0);
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(16, 4), checked((uint)dataOffset));
        int position = dataOffset;
        uint offset = 0;
        for (int index = 0; index <= glyphData.Count; index++)
        {
            WriteOffset(output, 20 + (index * offsetSize), offset, longOffsets);
            if (index < glyphData.Count)
            {
                glyphData[index].CopyTo(output, position);
                position += glyphData[index].Length;
                offset += checked((uint)glyphData[index].Length);
            }
        }

        return output;
    }

    private static uint[] ReadGvarOffsets(byte[] value, bool longOffsets, int glyphCount)
    {
        int offsetSize = longOffsets ? 4 : 2;
        var result = new uint[glyphCount + 1];
        for (int index = 0; index < result.Length; index++)
        {
            int position = 20 + (index * offsetSize);
            result[index] = longOffsets
                ? BinaryPrimitives.ReadUInt32BigEndian(value.AsSpan(position, 4))
                : checked((uint)BinaryPrimitives.ReadUInt16BigEndian(value.AsSpan(position, 2)) * 2);
        }

        return result;
    }

    private static void WriteOffset(byte[] output, int position, uint offset, bool longOffsets)
    {
        if (longOffsets)
        {
            BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(position, 4), offset);
            return;
        }

        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(position, 2), checked((ushort)(offset / 2)));
    }
}
