using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;
using OdfKit.Core;

namespace OdfKit.Core;

public static partial class OdfSigner
{
    #region X509 & Reference Utilities

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
    internal static void InjectReferenceStream(Reference reference, Stream stream)
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
