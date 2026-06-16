using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;

namespace OdfKit.Core;

public static partial class OdfSigner
{
    #region DER & CRL Utilities

    private static DerNode ParseDer(byte[] bytes)
    {
        int offset = 0;
        return ReadNode(bytes, ref offset, 0);
    }

    private static DerNode ReadNode(byte[] bytes, ref int offset, int depth)
    {
        if (depth > 16)
        {
            throw new CryptographicException("DER parser depth exceeded 16.");
        }

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
                {
                    node.Children.Add(ReadNode(val, ref childOffset, depth + 1));
                }
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
        {
            return b;
        }

        int numBytes = b & 0x7F;
        if (numBytes == 0 || numBytes > 4)
            throw new ArgumentException("Unsupported DER length encoding");

        int len = 0;
        for (int i = 0; i < numBytes; i++)
        {
            len = (len << 8) | bytes[offset++];
        }
        return len;
    }

    private static int ParseInteger(byte[] bytes)
    {
        if (bytes.Length == 0)
            return 0;
        int val = 0;
        for (int i = 0; i < bytes.Length; i++)
        {
            val = (val << 8) | bytes[i];
        }
        return val;
    }

    private static string NormalizeHexSerial(string serialHex)
    {
        if (string.IsNullOrWhiteSpace(serialHex))
            return "";
        if (BigInteger.TryParse("0" + serialHex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var bigInt))
        {
            if (bigInt.IsZero)
                return "0";
            return bigInt.ToString("X").TrimStart('0');
        }
        string normalized = serialHex.TrimStart('0').ToUpperInvariant();
        return normalized == "" ? "0" : normalized;
    }

    private static HashSet<string> GetRevokedSerialNumbers(byte[] crlBytes)
    {
        var revokedSerials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var root = ParseDer(crlBytes);
            if (root.Tag != 0x30 || root.Children.Count < 1)
                return revokedSerials;

            var tbsCertList = root.Children[0];
            if (tbsCertList.Tag != 0x30)
                return revokedSerials;

            foreach (var child in tbsCertList.Children)
            {
                if (child.Tag == 0x30)
                {
                    bool looksLikeRevoked = child.Children.Count > 0;
                    foreach (var item in child.Children)
                    {
                        if (item.Tag != 0x30 || item.Children.Count < 2 || item.Children[0].Tag != 0x02)
                        {
                            looksLikeRevoked = false;
                            break;
                        }
                    }

                    if (looksLikeRevoked)
                    {
                        foreach (var item in child.Children)
                        {
                            byte[] serialBytes = item.Children[0].Value;
                            string hex = BitConverter.ToString(serialBytes).Replace("-", "").ToUpperInvariant();
                            hex = hex.TrimStart('0');
                            if (hex == "")
                                hex = "0";
                            revokedSerials.Add(hex);
                        }
                        break;
                    }
                }
            }
        }
        catch
        {
        }
        return revokedSerials;
    }

    private static List<string> GetCrlUrls(X509Certificate2 certificate)
    {
        var urls = new List<string>();
        var ext = certificate.Extensions["2.5.29.31"];
        if (ext == null)
            return urls;

        try
        {
            var cdpNode = ParseDer(ext.RawData);
            FindUrlsInCdpNode(cdpNode, urls);
        }
        catch
        {
            string ascii = Encoding.ASCII.GetString(ext.RawData);
            int idx = 0;
            while ((idx = ascii.IndexOf("http://", idx, StringComparison.OrdinalIgnoreCase)) != -1)
            {
                int end = idx;
                while (end < ascii.Length && ascii[end] >= 33 && ascii[end] <= 126)
                {
                    end++;
                }
                string url = ascii.Substring(idx, end - idx);
                if (!urls.Contains(url))
                    urls.Add(url);
                idx = end;
            }
            idx = 0;
            while ((idx = ascii.IndexOf("https://", idx, StringComparison.OrdinalIgnoreCase)) != -1)
            {
                int end = idx;
                while (end < ascii.Length && ascii[end] >= 33 && ascii[end] <= 126)
                {
                    end++;
                }
                string url = ascii.Substring(idx, end - idx);
                if (!urls.Contains(url))
                    urls.Add(url);
                idx = end;
            }
        }
        return urls;
    }

    private static void FindUrlsInCdpNode(DerNode node, List<string> urls)
    {
        if (node.Tag == 0x86)
        {
            string url = Encoding.ASCII.GetString(node.Value);
            if ((url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                 url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) &&
                !urls.Contains(url))
            {
                urls.Add(url);
            }
        }

        foreach (var child in node.Children)
        {
            FindUrlsInCdpNode(child, urls);
        }
    }

    private static byte[]? GetCrlIssuerDer(DerNode tbsCertList)
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

    private static DerNode? GetTbsNode(byte[] crlBytes)
    {
        try
        {
            var root = ParseDer(crlBytes);
            if (root.Tag == 0x30 && root.Children.Count >= 1)
            {
                return root.Children[0];
            }
        }
        catch
        {
        }
        return null;
    }

    #endregion
}
