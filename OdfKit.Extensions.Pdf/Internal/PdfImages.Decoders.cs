using System;
using System.IO;

namespace OdfKit.Export.PdfInternal;

/// <summary>
/// GIF 與 BMP 解碼：依 GIF89a 規格（LZW 與區塊結構）與 Windows BMP 的 BITMAPINFOHEADER 格式撰寫，
/// 只解第一張畫面的像素並轉成未壓縮 RGB 與可選的透明度遮罩。
/// GIF and BMP decoding written from the GIF89a specification (LZW and block structure) and the Windows
/// BITMAPINFOHEADER layout; only the first frame is decoded into plain RGB with an optional alpha mask.
/// </summary>
internal sealed partial class PdfImage
{
    private static PdfImage? TryLoadGif(byte[] data)
    {
        if (data.Length < 13 || data[0] != 'G' || data[1] != 'I' || data[2] != 'F')
        {
            return null;
        }

        int width = data[6] | (data[7] << 8);
        int height = data[8] | (data[9] << 8);
        if (width <= 0 || height <= 0 || (long)width * height > MaxPixels)
        {
            return null;
        }

        int packed = data[10];
        int position = 13;
        byte[]? globalPalette = null;
        if ((packed & 0x80) != 0)
        {
            int size = 3 * (1 << ((packed & 7) + 1));
            globalPalette = new byte[size];
            Buffer.BlockCopy(data, position, globalPalette, 0, size);
            position += size;
        }

        int transparentIndex = -1;
        while (position < data.Length)
        {
            byte block = data[position++];
            if (block == 0x21)
            {
                byte label = data[position++];
                if (label == 0xF9 && data[position] >= 4 && (data[position + 1] & 1) != 0)
                {
                    transparentIndex = data[position + 4];
                }

                position = SkipSubBlocks(data, position);
            }
            else if (block == 0x2C)
            {
                return DecodeGifFrame(data, position, width, height, globalPalette, transparentIndex);
            }
            else
            {
                return null;
            }
        }

        return null;
    }

    private static int SkipSubBlocks(byte[] data, int position)
    {
        while (data[position] != 0)
        {
            position += data[position] + 1;
        }

        return position + 1;
    }

    private static PdfImage? DecodeGifFrame(byte[] data, int position, int width, int height, byte[]? globalPalette, int transparentIndex)
    {
        int left = data[position] | (data[position + 1] << 8);
        int top = data[position + 2] | (data[position + 3] << 8);
        int frameWidth = data[position + 4] | (data[position + 5] << 8);
        int frameHeight = data[position + 6] | (data[position + 7] << 8);
        int flags = data[position + 8];
        position += 9;
        if (frameWidth <= 0 || frameHeight <= 0 || (long)frameWidth * frameHeight > MaxPixels)
        {
            return null;
        }

        byte[]? palette = globalPalette;
        if ((flags & 0x80) != 0)
        {
            int size = 3 * (1 << ((flags & 7) + 1));
            palette = new byte[size];
            Buffer.BlockCopy(data, position, palette, 0, size);
            position += size;
        }

        if (palette is null)
        {
            return null;
        }

        int minCodeSize = data[position++];
        if (minCodeSize < 2 || minCodeSize > 11)
        {
            return null;
        }

        using var packedStream = new MemoryStream();
        while (data[position] != 0)
        {
            packedStream.Write(data, position + 1, data[position]);
            position += data[position] + 1;
        }

        byte[] indices = LzwDecode(packedStream.ToArray(), minCodeSize, frameWidth * frameHeight);
        bool interlaced = (flags & 0x40) != 0;
        var rgb = new byte[width * height * 3];
        var alpha = new byte[width * height];
        int paletteColors = palette.Length / 3;
        for (int row = 0; row < frameHeight; row++)
        {
            int y = top + (interlaced ? InterlacedRow(row, frameHeight) : row);
            if (y >= height)
            {
                continue;
            }

            for (int column = 0; column < frameWidth; column++)
            {
                int x = left + column;
                int source = (row * frameWidth) + column;
                if (x >= width || source >= indices.Length)
                {
                    continue;
                }

                int index = indices[source];
                if (index == transparentIndex || index >= paletteColors)
                {
                    continue;
                }

                int target = (y * width) + x;
                rgb[target * 3] = palette[index * 3];
                rgb[(target * 3) + 1] = palette[(index * 3) + 1];
                rgb[(target * 3) + 2] = palette[(index * 3) + 2];
                alpha[target] = 255;
            }
        }

        return new PdfImage(width, height, "/DeviceRGB", rgb, false, null, AllOpaque(alpha) ? null : alpha);
    }

    // GIF 交錯順序：第 0 遍每 8 列、第 1 遍每 8 列（從 4 起）、第 2 遍每 4 列（從 2 起）、第 3 遍每 2 列（從 1 起）。
    private static int InterlacedRow(int sequence, int height)
    {
        int pass1 = (height + 7) / 8;
        int pass2 = (height + 3) / 8;
        int pass3 = (height + 1) / 4;
        if (sequence < pass1)
        {
            return sequence * 8;
        }

        sequence -= pass1;
        if (sequence < pass2)
        {
            return 4 + (sequence * 8);
        }

        sequence -= pass2;
        if (sequence < pass3)
        {
            return 2 + (sequence * 4);
        }

        return 1 + ((sequence - pass3) * 2);
    }

    private static byte[] LzwDecode(byte[] packed, int minCodeSize, int expected)
    {
        int clear = 1 << minCodeSize;
        int end = clear + 1;
        var prefix = new short[4096];
        var suffix = new byte[4096];
        var stack = new byte[4097];
        var output = new byte[expected];
        int outputLength = 0;
        int codeSize = minCodeSize + 1;
        int next = end + 1;
        int previous = -1;
        int first = 0;
        int bits = 0;
        int bitCount = 0;
        int bytePosition = 0;
        for (int i = 0; i < clear; i++)
        {
            suffix[i] = (byte)i;
        }

        while (outputLength < expected)
        {
            while (bitCount < codeSize)
            {
                if (bytePosition >= packed.Length)
                {
                    return output;
                }

                bits |= packed[bytePosition++] << bitCount;
                bitCount += 8;
            }

            int code = bits & ((1 << codeSize) - 1);
            bits >>= codeSize;
            bitCount -= codeSize;
            if (code == clear)
            {
                codeSize = minCodeSize + 1;
                next = end + 1;
                previous = -1;
                continue;
            }

            if (code == end)
            {
                break;
            }

            int current = code;
            int depth = 0;
            if (previous == -1)
            {
                if (code >= clear)
                {
                    return output;
                }

                output[outputLength++] = suffix[code];
                previous = code;
                first = code;
                continue;
            }

            if (code >= next)
            {
                if (code > next)
                {
                    return output;
                }

                stack[depth++] = (byte)first;
                current = previous;
            }

            while (current >= clear)
            {
                if (depth >= stack.Length - 1)
                {
                    return output;
                }

                stack[depth++] = suffix[current];
                current = prefix[current];
            }

            first = suffix[current];
            stack[depth++] = (byte)first;
            while (depth > 0 && outputLength < expected)
            {
                output[outputLength++] = stack[--depth];
            }

            if (next < 4096)
            {
                prefix[next] = (short)previous;
                suffix[next] = (byte)first;
                next++;
                if (next == (1 << codeSize) && codeSize < 12)
                {
                    codeSize++;
                }
            }

            previous = code;
        }

        return output;
    }

    private static PdfImage? TryLoadBmp(byte[] data)
    {
        if (data.Length < 26 || data[0] != 'B' || data[1] != 'M')
        {
            return null;
        }

        int pixelOffset = ReadInt32(data, 10);
        int headerSize = ReadInt32(data, 14);
        int width;
        int height;
        int bitCount;
        int compression = 0;
        int colorsUsed = 0;
        int paletteEntrySize = 4;
        if (headerSize == 12)
        {
            width = data[18] | (data[19] << 8);
            height = data[20] | (data[21] << 8);
            bitCount = data[24] | (data[25] << 8);
            paletteEntrySize = 3;
        }
        else if (headerSize >= 40)
        {
            width = ReadInt32(data, 18);
            height = ReadInt32(data, 22);
            bitCount = data[28] | (data[29] << 8);
            compression = ReadInt32(data, 30);
            colorsUsed = ReadInt32(data, 46);
        }
        else
        {
            return null;
        }

        bool topDown = height < 0;
        height = Math.Abs(height);
        if (width <= 0 || height <= 0 || (long)width * height > MaxPixels || pixelOffset < 0)
        {
            return null;
        }

        bool supportedDepth = bitCount is 1 or 4 or 8 or 24 or 32;
        bool supportedCompression = compression == 0 || (compression == 3 && bitCount == 32);
        if (!supportedDepth || !supportedCompression)
        {
            return null;
        }

        byte[]? palette = null;
        if (bitCount <= 8)
        {
            int colors = colorsUsed > 0 ? Math.Min(colorsUsed, 1 << bitCount) : 1 << bitCount;
            palette = new byte[colors * 4];
            int paletteStart = 14 + headerSize;
            for (int i = 0; i < colors; i++)
            {
                int source = paletteStart + (i * paletteEntrySize);
                palette[i * 4] = data[source + 2];
                palette[(i * 4) + 1] = data[source + 1];
                palette[(i * 4) + 2] = data[source];
            }
        }

        int stride = (((width * bitCount) + 31) / 32) * 4;
        if ((long)stride * height > data.Length - pixelOffset)
        {
            return null;
        }

        var rgb = new byte[width * height * 3];
        for (int row = 0; row < height; row++)
        {
            int source = pixelOffset + ((topDown ? row : height - 1 - row) * stride);
            for (int x = 0; x < width; x++)
            {
                int target = ((row * width) + x) * 3;
                if (bitCount == 24 || bitCount == 32)
                {
                    int p = source + (x * (bitCount / 8));
                    rgb[target] = data[p + 2];
                    rgb[target + 1] = data[p + 1];
                    rgb[target + 2] = data[p];
                }
                else
                {
                    int bitPosition = x * bitCount;
                    int value = (data[source + (bitPosition / 8)] >> (8 - bitCount - (bitPosition % 8))) & ((1 << bitCount) - 1);
                    if (value * 4 >= palette!.Length)
                    {
                        continue;
                    }

                    rgb[target] = palette[value * 4];
                    rgb[target + 1] = palette[(value * 4) + 1];
                    rgb[target + 2] = palette[(value * 4) + 2];
                }
            }
        }

        return new PdfImage(width, height, "/DeviceRGB", rgb, false, null, null);
    }

    private static int ReadInt32(byte[] data, int offset) =>
        data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24);

    private static bool AllOpaque(byte[] alpha)
    {
        foreach (byte value in alpha)
        {
            if (value != 255)
            {
                return false;
            }
        }

        return true;
    }
}
