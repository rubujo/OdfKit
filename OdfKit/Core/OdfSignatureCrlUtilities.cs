using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace OdfKit.Core;

/// <summary>
/// CRL 擷取與驗證工具（內部協作者）。
/// </summary>
internal static class OdfSignatureCrlUtilities
{
    internal static HashSet<string> GetRevokedSerialNumbers(byte[] crlBytes)
    {
        var revokedSerials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var root = OdfSignatureDerCodec.Parse(crlBytes);
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

    internal static List<string> GetCrlUrls(X509Certificate2 certificate)
    {
        var urls = new List<string>();
        var ext = certificate.Extensions["2.5.29.31"];
        if (ext == null)
            return urls;

        try
        {
            var cdpNode = OdfSignatureDerCodec.Parse(ext.RawData);
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
                    end++;
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
                    end++;
                string url = ascii.Substring(idx, end - idx);
                if (!urls.Contains(url))
                    urls.Add(url);
                idx = end;
            }
        }
        return urls;
    }

    internal static bool VerifyCrlSignature(byte[] crlBytes, X509Certificate2 issuerCert)
    {
        try
        {
            var root = OdfSignatureDerCodec.Parse(crlBytes);
            if (root.Tag != 0x30 || root.Children.Count < 3)
                return false;

            var tbsCertList = root.Children[0];
            var signatureAlgorithm = root.Children[1];
            var signatureValue = root.Children[2];

            if (signatureValue.Tag != 0x03 || signatureValue.Value.Length < 2)
                return false;
            byte[] signature = new byte[signatureValue.Value.Length - 1];
            Buffer.BlockCopy(signatureValue.Value, 1, signature, 0, signature.Length);

            if (signatureAlgorithm.Tag != 0x30 || signatureAlgorithm.Children.Count < 1)
                return false;
            var oidNode = signatureAlgorithm.Children[0];
            if (oidNode.Tag != 0x06)
                return false;
            string oid = GetOidString(oidNode.Value);

            HashAlgorithmName hashAlg;
            if (oid == "1.2.840.113549.1.1.11" || oid == "1.2.840.10045.4.3.2")
                hashAlg = HashAlgorithmName.SHA256;
            else if (oid == "1.2.840.113549.1.1.12" || oid == "1.2.840.10045.4.3.3")
                hashAlg = HashAlgorithmName.SHA384;
            else if (oid == "1.2.840.113549.1.1.13" || oid == "1.2.840.10045.4.3.4")
                hashAlg = HashAlgorithmName.SHA512;
            else if (oid == "1.2.840.113549.1.1.5" || oid == "1.2.840.10045.4.1")
                hashAlg = HashAlgorithmName.SHA1;
            else
                throw new CryptographicException($"Unsupported CRL signature algorithm OID: {oid}");

            using var rsa = issuerCert.GetRSAPublicKey();
            if (rsa != null)
                return rsa.VerifyData(tbsCertList.RawBytes, signature, hashAlg, RSASignaturePadding.Pkcs1);

            using var ecdsa = issuerCert.GetECDsaPublicKey();
            if (ecdsa != null)
                return ecdsa.VerifyData(tbsCertList.RawBytes, signature, hashAlg);

            return false;
        }
        catch (Exception ex)
        {
            OdfKitDiagnostics.Warn($"CRL signature verification exception: {ex.Message}");
            return false;
        }
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
            FindUrlsInCdpNode(child, urls);
    }

    private static string GetOidString(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return "";
        var sb = new StringBuilder();
        byte first = bytes[0];
        int firstVal = first / 40;
        int secondVal = first % 40;
        sb.Append(firstVal).Append('.').Append(secondVal);

        long val = 0;
        for (int i = 1; i < bytes.Length; i++)
        {
            byte b = bytes[i];
            val = (val << 7) | (byte)(b & 0x7F);
            if ((b & 0x80) == 0)
            {
                sb.Append('.').Append(val);
                val = 0;
            }
        }
        return sb.ToString();
    }
}
