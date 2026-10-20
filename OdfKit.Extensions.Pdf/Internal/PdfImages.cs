using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace OdfKit.Export.PdfInternal;

/// <summary>
/// 內嵌到 PDF 的點陣圖：JPEG 直接以 DCTDecode 通過，PNG、GIF（第一張畫面）與 BMP 解碼成未壓縮 RGB 後再以 Flate 壓縮（含透明度遮罩）。
/// Raster images embedded into PDF: JPEG passes through as DCTDecode; PNG, GIF (first frame) and BMP are decoded to RGB and re-compressed with Flate (with an alpha soft mask).
/// </summary>
internal sealed partial class PdfImage
{
    private const int MaxPixels = 64 * 1024 * 1024;

    private PdfImage(int width, int height, string colorSpace, byte[] data, bool isJpeg, string? decodeArray, byte[]? alpha)
    {
        Width = width;
        Height = height;
        ColorSpace = colorSpace;
        Data = data;
        IsJpeg = isJpeg;
        DecodeArray = decodeArray;
        Alpha = alpha;
    }

    internal int Width { get; }

    internal int Height { get; }

    internal string ColorSpace { get; }

    internal int BitsPerComponent { get; } = 8;

    internal byte[] Data { get; }

    internal bool IsJpeg { get; }

    internal string? DecodeArray { get; }

    internal byte[]? Alpha { get; }

    internal static PdfImage? TryLoad(byte[] data)
    {
        try
        {
            if (data.Length > 4 && data[0] == 0xFF && data[1] == 0xD8)
            {
                return TryLoadJpeg(data);
            }

            if (data.Length > 6 && data[0] == 'G' && data[1] == 'I' && data[2] == 'F' && data[3] == '8')
            {
                return TryLoadGif(data);
            }

            if (data.Length > 2 && data[0] == 'B' && data[1] == 'M')
            {
                return TryLoadBmp(data);
            }

            if (data.Length > 8 && data[0] == 0x89 && data[1] == 'P' && data[2] == 'N' && data[3] == 'G')
            {
                return TryLoadPng(data);
            }
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentException or OverflowException or InvalidDataException)
        {
            return null;
        }

        return null;
    }

    /// <summary>
    /// 寫入影像物件（含遮罩），回傳影像物件編號。
    /// </summary>
    internal int Write(PdfBuilder builder)
    {
        string common = "/Type /XObject /Subtype /Image /Width " + Width.ToString(CultureInfo.InvariantCulture)
            + " /Height " + Height.ToString(CultureInfo.InvariantCulture)
            + " /ColorSpace " + ColorSpace + " /BitsPerComponent " + BitsPerComponent.ToString(CultureInfo.InvariantCulture);
        if (DecodeArray is not null)
        {
            common += " /Decode " + DecodeArray;
        }

        if (IsJpeg)
        {
            return builder.AddStream(common + " /Filter /DCTDecode", Data, compress: false);
        }

        if (Alpha is not null)
        {
            int mask = builder.AddStream(
                "/Type /XObject /Subtype /Image /Width " + Width.ToString(CultureInfo.InvariantCulture)
                + " /Height " + Height.ToString(CultureInfo.InvariantCulture) + " /ColorSpace /DeviceGray /BitsPerComponent 8",
                Alpha,
                compress: true);
            common += " /SMask " + PdfBuilder.Ref(mask);
        }

        return builder.AddStream(common, Data, compress: true);
    }

    private static PdfImage? TryLoadJpeg(byte[] data)
    {
        int position = 2;
        while (position + 4 <= data.Length)
        {
            if (data[position] != 0xFF)
            {
                position++;
                continue;
            }

            int marker = data[position + 1];
            if (marker == 0xFF)
            {
                position++;
                continue;
            }

            if (marker == 0xD8 || (marker >= 0xD0 && marker <= 0xD7) || marker == 0x01)
            {
                position += 2;
                continue;
            }

            int length = (data[position + 2] << 8) | data[position + 3];
            bool isStartOfFrame = marker is >= 0xC0 and <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC;
            if (isStartOfFrame && position + 10 <= data.Length)
            {
                int precision = data[position + 4];
                int height = (data[position + 5] << 8) | data[position + 6];
                int width = (data[position + 7] << 8) | data[position + 8];
                int components = data[position + 9];
                if (precision != 8 || width <= 0 || height <= 0 || (long)width * height > MaxPixels)
                {
                    return null;
                }

                // 漸進式（0xC2）與一般基線式都能以 DCTDecode 直接嵌入。
                string colorSpace;
                string? decode = null;
                switch (components)
                {
                    case 1:
                        colorSpace = "/DeviceGray";
                        break;
                    case 3:
                        colorSpace = "/DeviceRGB";
                        break;
                    case 4:
                        colorSpace = "/DeviceCMYK";
                        // Adobe 產生的 CMYK JPEG 以反相儲存。
                        decode = "[1 0 1 0 1 0 1 0]";
                        break;
                    default:
                        return null;
                }

                return new PdfImage(width, height, colorSpace, data, true, decode, null);
            }

            position += 2 + length;
        }

        return null;
    }

    private static PdfImage? TryLoadPng(byte[] data)
    {
        int position = 8;
        int width = 0;
        int height = 0;
        int bitDepth = 0;
        int colorType = 0;
        int interlace = 0;
        byte[]? palette = null;
        byte[]? transparency = null;
        var idat = new MemoryStream();
        while (position + 8 <= data.Length)
        {
            long chunkLength = ((long)data[position] << 24) | ((long)data[position + 1] << 16) | ((long)data[position + 2] << 8) | data[position + 3];
            string type = System.Text.Encoding.ASCII.GetString(data, position + 4, 4);
            int body = position + 8;
            if (chunkLength < 0 || body + chunkLength + 4 > data.Length)
            {
                break;
            }

            int length = (int)chunkLength;
            switch (type)
            {
                case "IHDR":
                    width = (data[body] << 24) | (data[body + 1] << 16) | (data[body + 2] << 8) | data[body + 3];
                    height = (data[body + 4] << 24) | (data[body + 5] << 16) | (data[body + 6] << 8) | data[body + 7];
                    bitDepth = data[body + 8];
                    colorType = data[body + 9];
                    interlace = data[body + 12];
                    break;
                case "PLTE":
                    palette = new byte[length];
                    Buffer.BlockCopy(data, body, palette, 0, length);
                    break;
                case "tRNS":
                    transparency = new byte[length];
                    Buffer.BlockCopy(data, body, transparency, 0, length);
                    break;
                case "IDAT":
                    idat.Write(data, body, length);
                    break;
            }

            position = body + length + 4;
            if (type == "IEND")
            {
                break;
            }
        }

        if (width <= 0 || height <= 0 || (long)width * height > MaxPixels || interlace != 0)
        {
            return null;
        }

        int channels = colorType switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, 6 => 4, _ => 0 };
        if (channels == 0 || (bitDepth != 1 && bitDepth != 2 && bitDepth != 4 && bitDepth != 8 && bitDepth != 16))
        {
            return null;
        }

        long bitsPerPixel = (long)channels * bitDepth;
        long rowBytes = ((width * bitsPerPixel) + 7) / 8;
        long expected = (rowBytes + 1) * height;
        byte[] compressed = idat.ToArray();
        byte[]? raw = PdfCore.Inflate(compressed, 0, compressed.Length);
        if (raw is null || raw.Length < expected)
        {
            return null;
        }

        int bytesPerPixel = (int)Math.Max(1, bitsPerPixel / 8);
        var rows = new byte[height][];
        byte[] previous = new byte[rowBytes];
        for (int y = 0; y < height; y++)
        {
            long start = y * (rowBytes + 1);
            int filter = raw[start];
            var row = new byte[rowBytes];
            Buffer.BlockCopy(raw, (int)start + 1, row, 0, (int)rowBytes);
            Unfilter(filter, row, previous, bytesPerPixel);
            rows[y] = row;
            previous = row;
        }

        var rgb = new byte[(long)width * height * 3];
        byte[]? alpha = colorType is 4 or 6 || transparency is not null ? new byte[(long)width * height] : null;
        for (int y = 0; y < height; y++)
        {
            byte[] row = rows[y];
            for (int x = 0; x < width; x++)
            {
                long pixel = ((long)y * width) + x;
                int r;
                int g;
                int b;
                int a = 255;
                switch (colorType)
                {
                    case 0:
                        {
                            int value = Sample(row, x, 0, 1, bitDepth);
                            r = g = b = Scale(value, bitDepth);
                            if (transparency is { Length: >= 2 } && value == ((transparency[0] << 8) | transparency[1]))
                            {
                                a = 0;
                            }

                            break;
                        }

                    case 2:
                        {
                            int rv = Sample(row, x, 0, 3, bitDepth);
                            int gv = Sample(row, x, 1, 3, bitDepth);
                            int bv = Sample(row, x, 2, 3, bitDepth);
                            r = Scale(rv, bitDepth);
                            g = Scale(gv, bitDepth);
                            b = Scale(bv, bitDepth);
                            if (transparency is { Length: >= 6 }
                                && rv == ((transparency[0] << 8) | transparency[1])
                                && gv == ((transparency[2] << 8) | transparency[3])
                                && bv == ((transparency[4] << 8) | transparency[5]))
                            {
                                a = 0;
                            }

                            break;
                        }

                    case 3:
                        {
                            int index = Sample(row, x, 0, 1, bitDepth);
                            if (palette is null || (index * 3) + 2 >= palette.Length)
                            {
                                return null;
                            }

                            r = palette[index * 3];
                            g = palette[(index * 3) + 1];
                            b = palette[(index * 3) + 2];
                            if (transparency is not null && index < transparency.Length)
                            {
                                a = transparency[index];
                            }

                            break;
                        }

                    case 4:
                        r = g = b = Scale(Sample(row, x, 0, 2, bitDepth), bitDepth);
                        a = Scale(Sample(row, x, 1, 2, bitDepth), bitDepth);
                        break;
                    default:
                        r = Scale(Sample(row, x, 0, 4, bitDepth), bitDepth);
                        g = Scale(Sample(row, x, 1, 4, bitDepth), bitDepth);
                        b = Scale(Sample(row, x, 2, 4, bitDepth), bitDepth);
                        a = Scale(Sample(row, x, 3, 4, bitDepth), bitDepth);
                        break;
                }

                rgb[pixel * 3] = (byte)r;
                rgb[(pixel * 3) + 1] = (byte)g;
                rgb[(pixel * 3) + 2] = (byte)b;
                if (alpha is not null)
                {
                    alpha[pixel] = (byte)a;
                }
            }
        }

        if (alpha is not null)
        {
            bool opaque = true;
            foreach (byte value in alpha)
            {
                if (value != 255)
                {
                    opaque = false;
                    break;
                }
            }

            if (opaque)
            {
                alpha = null;
            }
        }

        return new PdfImage(width, height, "/DeviceRGB", rgb, false, null, alpha);
    }

    private static int Sample(byte[] row, int x, int channel, int channels, int bitDepth)
    {
        if (bitDepth == 8)
        {
            return row[(x * channels) + channel];
        }

        if (bitDepth == 16)
        {
            int offset = ((x * channels) + channel) * 2;
            return (row[offset] << 8) | row[offset + 1];
        }

        // 1／2／4 位元只出現在單一通道（灰階或調色盤）。
        int bit = x * bitDepth;
        int shift = 8 - bitDepth - (bit & 7);
        return (row[bit >> 3] >> shift) & ((1 << bitDepth) - 1);
    }

    private static int Scale(int value, int bitDepth) => bitDepth switch
    {
        8 => value,
        16 => value >> 8,
        _ => value * 255 / ((1 << bitDepth) - 1),
    };

    private static void Unfilter(int filter, byte[] row, byte[] previous, int bytesPerPixel)
    {
        for (int i = 0; i < row.Length; i++)
        {
            int left = i >= bytesPerPixel ? row[i - bytesPerPixel] : 0;
            int up = previous[i];
            int upLeft = i >= bytesPerPixel ? previous[i - bytesPerPixel] : 0;
            int add = filter switch
            {
                0 => 0,
                1 => left,
                2 => up,
                3 => (left + up) / 2,
                4 => Paeth(left, up, upLeft),
                _ => 0,
            };
            row[i] = unchecked((byte)(row[i] + add));
        }
    }

    private static int Paeth(int a, int b, int c)
    {
        int p = a + b - c;
        int pa = Math.Abs(p - a);
        int pb = Math.Abs(p - b);
        int pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : (pb <= pc ? b : c);
    }
}

/// <summary>
/// 以內容雜湊去重的影像表。
/// </summary>
internal sealed class PdfImageTable
{
    private readonly Dictionary<string, (PdfImage Image, string Name)> _byKey = [];
    private readonly List<(PdfImage Image, string Name)> _all = [];

    internal IReadOnlyList<(PdfImage Image, string Name)> All => _all;

    internal (PdfImage Image, string Name) Add(byte[] source, PdfImage image)
    {
#if NETSTANDARD2_0
        string key;
        using (var sha = System.Security.Cryptography.SHA256.Create())
        {
            key = PdfCore.Hex(sha.ComputeHash(source));
        }
#else
        string key = PdfCore.Hex(System.Security.Cryptography.SHA256.HashData(source));
#endif

        if (_byKey.TryGetValue(key, out (PdfImage Image, string Name) existing))
        {
            return existing;
        }

        var entry = (image, "Im" + (_all.Count + 1).ToString(CultureInfo.InvariantCulture));
        _byKey[key] = entry;
        _all.Add(entry);
        return entry;
    }
}
