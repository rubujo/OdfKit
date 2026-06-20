using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using BcX509Crl = Org.BouncyCastle.X509.X509Crl;

namespace OdfKit.Core;

/// <summary>
/// CRL 擷取與驗證工具（內部協作者）。底層 DER 解析委派至 BouncyCastle 的
/// <see cref="Org.BouncyCastle.Asn1.X509"/> 型別模型，取代自製遞迴下降剖析器。
/// </summary>
internal static class OdfSignatureCrlUtilities
{
    internal static HashSet<string> GetRevokedSerialNumbers(byte[] crlBytes)
    {
        var revokedSerials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            BcX509Crl crl = new X509CrlParser().ReadCrl(crlBytes)
                ?? throw new CryptographicException("無法解析 CRL 內容。");
            ISet<X509CrlEntry>? entries = crl.GetRevokedCertificates();
            if (entries is null)
            {
                return revokedSerials;
            }

            foreach (X509CrlEntry entry in entries)
            {
                revokedSerials.Add(OdfSignatureDerCodec.NormalizeHexSerial(entry.SerialNumber.ToString(16)));
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
            var crlDistPoint = CrlDistPoint.GetInstance(Asn1Object.FromByteArray(ext.RawData));
            foreach (DistributionPoint distributionPoint in crlDistPoint.GetDistributionPoints())
            {
                DistributionPointName? pointName = distributionPoint.DistributionPointName;
                if (pointName is null || pointName.Type != DistributionPointName.FullName)
                {
                    continue;
                }

                var generalNames = GeneralNames.GetInstance(pointName.Name);
                foreach (GeneralName generalName in generalNames.GetNames())
                {
                    if (generalName.TagNo != GeneralName.UniformResourceIdentifier)
                    {
                        continue;
                    }

                    string url = DerIA5String.GetInstance(generalName.Name).GetString();
                    if (!urls.Contains(url))
                    {
                        urls.Add(url);
                    }
                }
            }
        }
        catch
        {
            ExtractUrlsFromRawAscii(ext.RawData, urls);
        }

        return urls;
    }

    private static void ExtractUrlsFromRawAscii(byte[] rawData, List<string> urls)
    {
        string ascii = System.Text.Encoding.ASCII.GetString(rawData);
        foreach (string scheme in new[] { "http://", "https://" })
        {
            int idx = 0;
            while ((idx = ascii.IndexOf(scheme, idx, StringComparison.OrdinalIgnoreCase)) != -1)
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
    }

    /// <summary>
    /// 取得 CRL TBSCertList 內 issuer 欄位的 DER 編碼原始位元組，用於與 <see cref="X509Certificate2.IssuerName"/> 比對。
    /// </summary>
    internal static byte[]? GetCrlIssuerRawData(byte[] crlBytes)
    {
        try
        {
            CertificateList certificateList = CertificateList.GetInstance(Asn1Object.FromByteArray(crlBytes));
            return certificateList.TbsCertList.Issuer.GetEncoded();
        }
        catch
        {
            return null;
        }
    }

    internal static bool VerifyCrlSignature(byte[] crlBytes, X509Certificate2 issuerCert)
    {
        try
        {
            BcX509Crl crl = new X509CrlParser().ReadCrl(crlBytes)
                ?? throw new CryptographicException("無法解析 CRL 內容。");
            AsymmetricKeyParameter issuerPublicKey =
                DotNetUtilities.FromX509Certificate(issuerCert).GetPublicKey();
            crl.Verify(issuerPublicKey);
            return true;
        }
        catch (Exception ex)
        {
            OdfKitDiagnostics.Warn($"CRL signature verification exception: {ex.Message}");
            return false;
        }
    }
}
