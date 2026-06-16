using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using OdfKit.Core;

namespace OdfKit.Core;

public static partial class OdfSigner
{
    #region 輔助類別與 ASN.1/DER/CRL/TSA 工具


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

    private static bool StructuralEqual(byte[] a, byte[] b)
    {
        return OdfEncryption.ByteArrayEquals(a, b);
    }

    private static async Task<byte[]> DownloadCrlAsync(string url, HttpClient? httpClient)
    {
        var client = httpClient ?? s_httpClient;
        return await client.GetByteArrayAsync(url);
    }

    private static byte[] CreateTsaRequest(byte[] hash)
    {
        if (hash == null || hash.Length != 32)
            throw new ArgumentException("Hash must be 32 bytes (SHA-256).", nameof(hash));

        byte[] request = new byte[59];
        request[0] = 0x30;
        request[1] = 57;
        request[2] = 0x02;
        request[3] = 0x01;
        request[4] = 0x01;
        request[5] = 0x30;
        request[6] = 49;
        request[7] = 0x30;
        request[8] = 13;
        request[9] = 0x06;
        request[10] = 0x09;
        byte[] sha256Oid = { 0x60, 0x86, 0x48, 0x01, 0x65, 0x03, 0x04, 0x02, 0x01 };
        Buffer.BlockCopy(sha256Oid, 0, request, 11, 9);
        request[20] = 0x05;
        request[21] = 0x00;
        request[22] = 0x04;
        request[23] = 32;
        Buffer.BlockCopy(hash, 0, request, 24, 32);
        request[56] = 0x01;
        request[57] = 0x01;
        request[58] = 0xff;

        return request;
    }

    private static async Task<byte[]> QueryTsaAsync(string tsaUrl, byte[] hash, HttpClient? httpClient)
    {
        byte[] requestBytes = CreateTsaRequest(hash);

        var client = httpClient ?? s_httpClient;
        using var request = new HttpRequestMessage(HttpMethod.Post, tsaUrl);
        request.Content = new ByteArrayContent(requestBytes);
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/timestamp-query");

        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync();
    }

    private static byte[] ExtractTimestampToken(byte[] responseBytes)
    {
        var root = ParseDer(responseBytes);
        if (root.Tag != 0x30 || root.Children.Count < 1)
        {
            throw new CryptographicException("Invalid TSA response structure (expected SEQUENCE).");
        }

        var statusInfo = root.Children[0];
        if (statusInfo.Tag != 0x30 || statusInfo.Children.Count < 1)
        {
            throw new CryptographicException("Invalid PKIStatusInfo structure.");
        }

        int status = ParseInteger(statusInfo.Children[0].Value);
        if (status != 0 && status != 1)
        {
            throw new CryptographicException($"TSA request was rejected with status: {status}.");
        }

        if (root.Children.Count < 2)
        {
            throw new CryptographicException("TSA response does not contain a TimeStampToken.");
        }

        var token = root.Children[1];
        return token.RawBytes;
    }

    private static List<X509Certificate2> GetEmbeddedCertificates(XmlNode signatureNode, XmlNamespaceManager nsManager)
    {
        var certificates = new List<X509Certificate2>();
        var certificateNodes = signatureNode.SelectNodes(".//xades:CertificateValues/xades:EncapsulatedCertificate", nsManager);
        if (certificateNodes == null)
        {
            return certificates;
        }

        foreach (XmlNode certificateNode in certificateNodes)
        {
            try
            {
                byte[] rawData = Convert.FromBase64String(certificateNode.InnerText.Trim());
#if NET9_0_OR_GREATER
                certificates.Add(X509CertificateLoader.LoadCertificate(rawData));
#else
                    certificates.Add(new X509Certificate2(rawData));
#endif
            }
            catch
            {
                // 無效的內嵌憑證值將被忽略；若為必要項目，鏈驗證將失敗
            }
        }

        return certificates;
    }

    private static bool VerifySigningCertificateDigest(XmlElement signatureElement, X509Certificate2 certificate, out string? errorMessage)
    {
        errorMessage = null;
        var nsManager = new XmlNamespaceManager(signatureElement.OwnerDocument.NameTable);
        nsManager.AddNamespace("ds", OdfNamespaces.Ds);
        nsManager.AddNamespace("xades", "http://uri.etsi.org/01903/v1.3.2#");

        var certNode = signatureElement.SelectSingleNode(".//xades:SigningCertificate/xades:Cert", nsManager);
        if (certNode == null)
        {
            return true;
        }

        var digestMethodNode = certNode.SelectSingleNode("xades:CertDigest/ds:DigestMethod", nsManager);
        var digestValueNode = certNode.SelectSingleNode("xades:CertDigest/ds:DigestValue", nsManager);
        if (digestMethodNode == null || digestValueNode == null)
        {
            errorMessage = "XAdES-BES CertDigest elements are missing.";
            return false;
        }

        string alg = digestMethodNode.Attributes?["Algorithm"]?.Value ?? "";
        string expectedB64 = digestValueNode.InnerText.Trim();

        byte[] actualDigest;
        if (alg == SignedXml.XmlDsigSHA256Url || alg == "http://www.w3.org/2001/04/xmlenc#sha256")
        {
            using var sha = SHA256.Create();
            actualDigest = sha.ComputeHash(certificate.RawData);
        }
        else if (alg == SignedXml.XmlDsigSHA1Url || alg == "http://www.w3.org/2000/09/xmldsig#sha1")
        {
#pragma warning disable SYSLIB0021
            using var sha = SHA1.Create();
            actualDigest = sha.ComputeHash(certificate.RawData);
#pragma warning restore SYSLIB0021
        }
        else
        {
            errorMessage = $"Unsupported certificate digest algorithm: {alg}";
            return false;
        }

        string actualB64 = Convert.ToBase64String(actualDigest);
        if (actualB64 != expectedB64)
        {
            errorMessage = "Signing certificate digest does not match the XAdES CertDigest value.";
            return false;
        }

        var issuerNameNode = certNode.SelectSingleNode("xades:IssuerSerial/ds:X509IssuerName", nsManager);
        var serialNumberNode = certNode.SelectSingleNode("xades:IssuerSerial/ds:X509SerialNumber", nsManager);
        if (issuerNameNode != null && serialNumberNode != null)
        {
            string expectedIssuer = issuerNameNode.InnerText.Trim();
            string expectedSerialBase10 = serialNumberNode.InnerText.Trim();

            var bigSerial = System.Numerics.BigInteger.Parse("0" + certificate.SerialNumber, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
            string actualSerialBase10 = bigSerial.ToString();

            if (actualSerialBase10 != expectedSerialBase10)
            {
                errorMessage = $"Certificate serial number {actualSerialBase10} does not match XAdES IssuerSerial {expectedSerialBase10}.";
                return false;
            }
        }

        return true;
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

    private static bool VerifyCrlSignature(byte[] crlBytes, X509Certificate2 issuerCert)
    {
        try
        {
            var root = ParseDer(crlBytes);
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
            {
                hashAlg = HashAlgorithmName.SHA256;
            }
            else if (oid == "1.2.840.113549.1.1.12" || oid == "1.2.840.10045.4.3.3")
            {
                hashAlg = HashAlgorithmName.SHA384;
            }
            else if (oid == "1.2.840.113549.1.1.13" || oid == "1.2.840.10045.4.3.4")
            {
                hashAlg = HashAlgorithmName.SHA512;
            }
            else if (oid == "1.2.840.113549.1.1.5" || oid == "1.2.840.10045.4.1")
            {
                hashAlg = HashAlgorithmName.SHA1;
            }
            else
            {
                throw new CryptographicException($"Unsupported CRL signature algorithm OID: {oid}");
            }

            using var rsa = issuerCert.GetRSAPublicKey();
            if (rsa != null)
            {
                return rsa.VerifyData(tbsCertList.RawBytes, signature, hashAlg, RSASignaturePadding.Pkcs1);
            }

            using var ecdsa = issuerCert.GetECDsaPublicKey();
            if (ecdsa != null)
            {
                return ecdsa.VerifyData(tbsCertList.RawBytes, signature, hashAlg);
            }

            return false;
        }
        catch (Exception ex)
        {
            OdfKitDiagnostics.Warn($"CRL signature verification exception: {ex.Message}");
            return false;
        }
    }
    /// <summary>
    /// 使用反射將原始 Stream 直接注入 SignedXml 的 Reference，以繞過新版 .NET Core 的 URI 解析限制。
    /// </summary>
    private static void InjectReferenceStream(Reference reference, Stream stream)
    {
        if (reference == null)
            throw new ArgumentNullException(nameof(reference));
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));

        var type = typeof(Reference);
        var refTargetField = type.GetField("_refTarget", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? type.GetField("m_refTarget", BindingFlags.Instance | BindingFlags.NonPublic);

        var refTargetTypeField = type.GetField("_refTargetType", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? type.GetField("m_refTargetType", BindingFlags.Instance | BindingFlags.NonPublic);

        if (refTargetField == null || refTargetTypeField == null)
        {
            throw new CryptographicException(
                "Failed to locate required internal fields (_refTarget / _refTargetType) in Reference. " +
                $"This reflection-based workaround may need updating for the current .NET runtime ({Environment.Version}).");
        }

        refTargetField.SetValue(reference, stream);

        // ReferenceTargetType.Stream 為 0
        var enumType = refTargetTypeField.FieldType;
        var streamVal = Enum.ToObject(enumType, 0); // 0 對應 Stream
        refTargetTypeField.SetValue(reference, streamVal);
    }

    #endregion
}
