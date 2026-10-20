using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace OdfKit.Export.PdfInternal;

/// <summary>
/// PDF 輸出的底層工具：zlib 壓縮／解壓縮、字串與名稱跳脫。
/// Low-level helpers for PDF output: zlib compression, string and name escaping.
/// </summary>
internal static class PdfCore
{
    internal const long MaxInflatedBytes = 256L * 1024 * 1024;

    internal static byte[] Deflate(byte[] data)
    {
        using var output = new MemoryStream(data.Length / 2 + 16);
        output.WriteByte(0x78);
        output.WriteByte(0x9C);
        using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflate.Write(data, 0, data.Length);
        }

        uint adler = Adler32(data);
        output.WriteByte((byte)(adler >> 24));
        output.WriteByte((byte)(adler >> 16));
        output.WriteByte((byte)(adler >> 8));
        output.WriteByte((byte)adler);
        return output.ToArray();
    }

    /// <summary>
    /// 解開 zlib 資料；格式錯誤或超過大小上限時回傳 <see langword="null"/>。
    /// </summary>
    internal static byte[]? Inflate(byte[] data, int offset, int length)
    {
        if (length < 2 || offset < 0 || offset + length > data.Length)
        {
            return null;
        }

        // zlib 標頭：壓縮方法 8（deflate）且 CMF*256+FLG 能被 31 整除。
        int cmf = data[offset];
        int flg = data[offset + 1];
        if ((cmf & 0x0F) != 8 || ((cmf << 8) | flg) % 31 != 0)
        {
            return null;
        }

        try
        {
            // 跳過 2 位元組 zlib 標頭；DeflateStream 只處理原始 deflate。
            using var input = new MemoryStream(data, offset + 2, length - 2, writable: false);
            using var deflate = new DeflateStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            var buffer = new byte[16 * 1024];
            int read;
            while ((read = deflate.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (output.Length + read > MaxInflatedBytes)
                {
                    return null;
                }

                output.Write(buffer, 0, read);
            }

            return output.ToArray();
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    private static uint Adler32(byte[] data)
    {
        uint a = 1;
        uint b = 0;
        foreach (byte value in data)
        {
            a = (a + value) % 65521;
            b = (b + a) % 65521;
        }

        return (b << 16) | a;
    }

    /// <summary>
    /// 以 PDF 文字字串表示：純 ASCII 用字面值，其餘用帶 BOM 的 UTF-16BE 十六進位。
    /// </summary>
    internal static string TextString(string value)
    {
        bool ascii = true;
        foreach (char c in value)
        {
            if (c < 0x20 || c > 0x7E)
            {
                ascii = false;
                break;
            }
        }

        if (ascii)
        {
            var sb = new StringBuilder(value.Length + 2);
            sb.Append('(');
            foreach (char c in value)
            {
                if (c == '\\' || c == '(' || c == ')')
                {
                    sb.Append('\\');
                }

                sb.Append(c);
            }

            sb.Append(')');
            return sb.ToString();
        }

        var hex = new StringBuilder(value.Length * 4 + 8);
        hex.Append("<FEFF");
        foreach (char c in value)
        {
            hex.Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
        }

        hex.Append('>');
        return hex.ToString();
    }

    /// <summary>
    /// 以 PDF 名稱表示（不含字母數字的字元以 #XX 跳脫）。
    /// </summary>
    internal static string Name(string value)
    {
        var sb = new StringBuilder(value.Length + 1);
        sb.Append('/');
        foreach (byte b in Encoding.UTF8.GetBytes(value))
        {
            if ((b >= (byte)'A' && b <= (byte)'Z') || (b >= (byte)'a' && b <= (byte)'z') || (b >= (byte)'0' && b <= (byte)'9')
                || b == (byte)'-' || b == (byte)'_' || b == (byte)'.' || b == (byte)'+')
            {
                sb.Append((char)b);
            }
            else
            {
                sb.Append('#').Append(b.ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        return sb.ToString();
    }

    internal static string Number(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return "0";
        }

        string text = value.ToString("0.####", CultureInfo.InvariantCulture);
        return text == "-0" ? "0" : text;
    }

    internal static string Hex(byte[] data)
    {
        var sb = new StringBuilder(data.Length * 2);
        foreach (byte b in data)
        {
            sb.Append(b.ToString("X2", CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }

    internal static string Date(DateTime utc) =>
        "D:" + utc.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + "Z";

    internal static void WriteAscii(Stream stream, string text)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(text);
        stream.Write(bytes, 0, bytes.Length);
    }
}

/// <summary>
/// 以記憶體物件表組成新的 PDF：物件編號、串流、傳統交叉參照表與 trailer。
/// Builds a new PDF from an in-memory object table with a classic cross-reference table and trailer.
/// </summary>
internal sealed class PdfBuilder
{
    private readonly List<byte[]?> _bodies = [null];

    /// <summary>
    /// 取得下一個物件編號但先不寫入內容。
    /// </summary>
    internal int Reserve()
    {
        _bodies.Add(null);
        return _bodies.Count - 1;
    }

    internal int Add(string body)
    {
        int number = Reserve();
        Set(number, body);
        return number;
    }

    internal void Set(int number, string body) => _bodies[number] = Encoding.ASCII.GetBytes(body);

    /// <summary>
    /// 新增串流物件；<paramref name="dictionary"/> 為不含 Length／Filter 的字典項目。
    /// </summary>
    internal int AddStream(string dictionary, byte[] data, bool compress)
    {
        int number = Reserve();
        SetStream(number, dictionary, data, compress);
        return number;
    }

    internal void SetStream(int number, string dictionary, byte[] data, bool compress)
    {
        byte[] payload = compress ? PdfCore.Deflate(data) : data;
        string head = "<< " + dictionary + (compress ? " /Filter /FlateDecode" : string.Empty)
            + " /Length " + payload.Length.ToString(CultureInfo.InvariantCulture) + " >>\nstream\n";
        using var ms = new MemoryStream(payload.Length + head.Length + 16);
        PdfCore.WriteAscii(ms, head);
        ms.Write(payload, 0, payload.Length);
        PdfCore.WriteAscii(ms, "\nendstream");
        _bodies[number] = ms.ToArray();
    }

    internal static string Ref(int number) => number.ToString(CultureInfo.InvariantCulture) + " 0 R";

    internal void Write(Stream destination, int rootObject, int? infoObject, byte[] documentId)
    {
        using var ms = new MemoryStream();
        ms.Write([(byte)'%', (byte)'P', (byte)'D', (byte)'F', (byte)'-', (byte)'1', (byte)'.', (byte)'7', (byte)'\n', (byte)'%', 0xE2, 0xE3, 0xCF, 0xD3, (byte)'\n'], 0, 15);
        var offsets = new long[_bodies.Count];
        for (int number = 1; number < _bodies.Count; number++)
        {
            byte[] body = _bodies[number] ?? throw new InvalidOperationException("PDF object " + number.ToString(CultureInfo.InvariantCulture) + " was reserved but never written.");
            offsets[number] = ms.Position;
            PdfCore.WriteAscii(ms, number.ToString(CultureInfo.InvariantCulture) + " 0 obj\n");
            ms.Write(body, 0, body.Length);
            PdfCore.WriteAscii(ms, "\nendobj\n");
        }

        long xrefOffset = ms.Position;
        var xref = new StringBuilder();
        xref.Append("xref\n0 ").Append(_bodies.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');
        xref.Append("0000000000 65535 f \n");
        for (int number = 1; number < _bodies.Count; number++)
        {
            xref.Append(offsets[number].ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        }

        string id = PdfCore.Hex(documentId);
        xref.Append("trailer\n<< /Size ").Append(_bodies.Count.ToString(CultureInfo.InvariantCulture))
            .Append(" /Root ").Append(Ref(rootObject));
        if (infoObject is int info)
        {
            xref.Append(" /Info ").Append(Ref(info));
        }

        xref.Append(" /ID [<").Append(id).Append("><").Append(id).Append(">] >>\nstartxref\n")
            .Append(xrefOffset.ToString(CultureInfo.InvariantCulture)).Append("\n%%EOF\n");
        PdfCore.WriteAscii(ms, xref.ToString());
        ms.Position = 0;
        ms.CopyTo(destination);
    }
}
