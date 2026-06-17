using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;

namespace OdfKit.Core;

/// <summary>
/// X.509 憑證與 XML 簽章參照工具（內部協作者）。
/// </summary>
internal static class OdfSignatureX509Utilities
{
    internal static List<X509Certificate2> GetEmbeddedCertificates(XmlNode signatureNode, XmlNamespaceManager nsManager)
    {
        var certificates = new List<X509Certificate2>();
        var certificateNodes = signatureNode.SelectNodes(".//xades:CertificateValues/xades:EncapsulatedCertificate", nsManager);
        if (certificateNodes == null)
            return certificates;

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

    internal static bool VerifySigningCertificateDigest(XmlElement signatureElement, X509Certificate2 certificate, out string? errorMessage)
    {
        errorMessage = null;
        var nsManager = new XmlNamespaceManager(signatureElement.OwnerDocument.NameTable);
        nsManager.AddNamespace("ds", OdfNamespaces.Ds);
        nsManager.AddNamespace("xades", "http://uri.etsi.org/01903/v1.3.2#");

        var certNode = signatureElement.SelectSingleNode(".//xades:SigningCertificate/xades:Cert", nsManager);
        if (certNode == null)
            return true;

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
            string expectedSerialBase10 = serialNumberNode.InnerText.Trim();
            var bigSerial = BigInteger.Parse("0" + certificate.SerialNumber, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            string actualSerialBase10 = bigSerial.ToString();

            if (actualSerialBase10 != expectedSerialBase10)
            {
                errorMessage = $"Certificate serial number {actualSerialBase10} does not match XAdES IssuerSerial {expectedSerialBase10}.";
                return false;
            }
        }

        return true;
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

        var enumType = refTargetTypeField.FieldType;
        var streamVal = Enum.ToObject(enumType, 0);
        refTargetTypeField.SetValue(reference, streamVal);
    }
}
