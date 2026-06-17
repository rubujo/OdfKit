using System;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;

namespace OdfKit.Core;

/// <summary>
/// DER 編解碼工具（內部協作者）。
/// </summary>
internal static class OdfSignatureDerCodec
{
    internal static DerNode Parse(byte[] bytes)
    {
        int offset = 0;
        return ReadNode(bytes, ref offset, 0);
    }

    internal static int ParseInteger(byte[] bytes)
    {
        if (bytes.Length == 0)
            return 0;
        int val = 0;
        for (int i = 0; i < bytes.Length; i++)
            val = (val << 8) | bytes[i];
        return val;
    }

    internal static string NormalizeHexSerial(string serialHex)
    {
        if (string.IsNullOrWhiteSpace(serialHex))
            return "";
        if (BigInteger.TryParse("0" + serialHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var bigInt))
        {
            if (bigInt.IsZero)
                return "0";
            return bigInt.ToString("X").TrimStart('0');
        }
        string normalized = serialHex.TrimStart('0').ToUpperInvariant();
        return normalized == "" ? "0" : normalized;
    }

    internal static DerNode? GetTbsNode(byte[] crlBytes)
    {
        try
        {
            var root = Parse(crlBytes);
            if (root.Tag == 0x30 && root.Children.Count >= 1)
                return root.Children[0];
        }
        catch
        {
        }
        return null;
    }

    internal static byte[]? GetCrlIssuerDer(DerNode tbsCertList)
    {
        if (tbsCertList.Children.Count < 2)
            return null;
        if (tbsCertList.Children[0].Tag == 0x02)
        {
            if (tbsCertList.Children.Count >= 3)
                return tbsCertList.Children[2].RawBytes;
        }
        else
        {
            return tbsCertList.Children[1].RawBytes;
        }
        return null;
    }

    private static DerNode ReadNode(byte[] bytes, ref int offset, int depth)
    {
        if (depth > 16)
            throw new CryptographicException("DER parser depth exceeded 16.");

        int start = offset;
        if (offset >= bytes.Length)
            throw new ArgumentException("Unexpected end of DER data");

        byte tag = bytes[offset++];
        int length = ReadLength(bytes, ref offset);

        if (offset + length > bytes.Length)
            throw new ArgumentException("DER element length exceeds remaining data");

        byte[] val = new byte[length];
        Buffer.BlockCopy(bytes, offset, val, 0, length);

        int totalLen = offset + length - start;
        byte[] raw = new byte[totalLen];
        Buffer.BlockCopy(bytes, start, raw, 0, totalLen);

        var node = new DerNode(tag, val)
        {
            StartOffset = start,
            Length = totalLen,
            RawBytes = raw
        };
        offset += length;

        if ((tag & 0x20) != 0)
        {
            int childOffset = 0;
            try
            {
                while (childOffset < val.Length)
                    node.Children.Add(ReadNode(val, ref childOffset, depth + 1));
            }
            catch (CryptographicException)
            {
                throw;
            }
            catch
            {
                node.Children.Clear();
            }
        }

        return node;
    }

    private static int ReadLength(byte[] bytes, ref int offset)
    {
        byte b = bytes[offset++];
        if ((b & 0x80) == 0)
            return b;

        int numBytes = b & 0x7F;
        if (numBytes == 0 || numBytes > 4)
            throw new ArgumentException("Unsupported DER length encoding");

        int len = 0;
        for (int i = 0; i < numBytes; i++)
            len = (len << 8) | bytes[offset++];
        return len;
    }
}
