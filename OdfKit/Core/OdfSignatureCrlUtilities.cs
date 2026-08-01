using System.Text;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using BcX509Crl = Org.BouncyCastle.X509.X509Crl;

using OdfKit.Compliance;
namespace OdfKit.Core;

/// <summary>
/// CRL 擷取與驗證工具（內部協作者）。底層 DER 解析委派至 BouncyCastle 的
/// <see cref="Org.BouncyCastle.Asn1.X509"/> 型別模型，取代自製遞迴下降剖析器。
/// </summary>
internal static class OdfSignatureCrlUtilities
{
    private const int MaxDistributionPointUrls = 16;
    /// <summary>
    /// 解析 CRL 並取得其中的已撤銷序號集合。呼叫端輸入可能來自不可信來源（例如 ODF 文件內嵌 CRL），
    /// 因此本方法不吞掉任何解析例外：CRL 內容無法解析或個別項目格式異常時一律向外拋出，
    /// 由呼叫端（<see cref="OdfSignatureVerifier"/> 的撤銷檢查邏輯）轉為「撤銷檢查失敗」，
    /// 絕不可靜默回傳部分或空集合而被誤判為「未撤銷」。
    /// </summary>
    internal static HashSet<string> GetRevokedSerialNumbers(byte[] crlBytes)
    {
        var revokedSerials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        BcX509Crl crl = new X509CrlParser().ReadCrl(crlBytes)
            ?? throw new CryptographicException(OdfLocalizer.GetMessage("Err_OdfSignatureCrlUtilities_UnableParseCrlContent_2"));
        ISet<X509CrlEntry>? entries = crl.GetRevokedCertificates();
        if (entries is null)
        {
            return revokedSerials;
        }

        foreach (X509CrlEntry entry in entries)
        {
            revokedSerials.Add(OdfSignatureDerCodec.NormalizeHexSerial(entry.SerialNumber.ToString(16)));
        }

        return revokedSerials;
    }

    /// <summary>
    /// 依 RFC 5280 §6.3.3 檢查 CRL 的有效期：目前時間必須落在 thisUpdate 與 nextUpdate 之間，
    /// CRL 內容才可被採信。<paramref name="crlBytes"/> 可能來自不可信來源（ODF 文件內嵌 CRL，
    /// 或線上下載但尚未經過額外信任驗證的 CRL），因此本方法採嚴格判定：尚未生效或已逾期一律視為
    /// 「無法採信」，絕不可被解讀為「未撤銷」。此處刻意不加入時鐘偏移容忍度：VerifyCertificateValidityPeriod
    /// （<c>OdfSignatureVerifier.Dsig.cs</c>）驗證憑證有效期時同樣採嚴格邊界比對，維持一致的從嚴風格。
    /// </summary>
    /// <param name="crlBytes">CRL 的 DER 編碼位元組。</param>
    /// <param name="referenceTimeUtc">用於比對的目前時間（UTC）。</param>
    /// <param name="invalidReason">CRL 有效期判定為無效時的說明文字；有效時為 <see langword="null"/>。</param>
    /// <returns>CRL 目前是否處於 thisUpdate／nextUpdate 有效期間內。</returns>
    internal static bool IsCrlTimeValid(byte[] crlBytes, DateTime referenceTimeUtc, out string? invalidReason)
    {
        invalidReason = null;
        try
        {
            BcX509Crl crl = new X509CrlParser().ReadCrl(crlBytes)
                ?? throw new CryptographicException(OdfLocalizer.GetMessage("Err_OdfSignatureCrlUtilities_UnableParseCrlContent_2"));

            // BouncyCastle 將 ASN.1 UTCTime／GeneralizedTime 解析為 DateTime，其 Kind 不保證為 Utc；
            // RFC 5280 規定憑證與 CRL 的時間欄位一律採 UTC（Zulu），這裡明確標記 Kind 以避免與
            // referenceTimeUtc 比對時被 .NET 誤判為當地時間而產生時區偏移。
            var thisUpdateUtc = DateTime.SpecifyKind(crl.ThisUpdate, DateTimeKind.Utc);
            if (thisUpdateUtc > referenceTimeUtc)
            {
                invalidReason = OdfLocalizer.GetMessage(
                    "Err_OdfSignatureCrlUtilities_CrlNotYetValid",
                    thisUpdateUtc.ToString("O", CultureInfo.InvariantCulture),
                    referenceTimeUtc.ToString("O", CultureInfo.InvariantCulture));
                return false;
            }

            DateTime? nextUpdate = crl.NextUpdate;
            if (nextUpdate is null)
            {
                // RFC 5280 §5.1.2.5：nextUpdate 為選用欄位，CA 可省略以代表「未承諾下一次更新排程」。
                // 但本方法的輸入來源包含不可信 CRL，攻擊者可任意省略 nextUpdate 以規避過期判定；
                // 因此對不可信輸入從嚴解讀：缺少 nextUpdate 一律視為「無法確認有效期」而拒絕採信，
                // 而非放行為「永久有效」。
                invalidReason = OdfLocalizer.GetMessage("Err_OdfSignatureCrlUtilities_CrlMissingNextUpdate");
                return false;
            }

            var nextUpdateUtc = DateTime.SpecifyKind(nextUpdate.Value, DateTimeKind.Utc);
            if (nextUpdateUtc < referenceTimeUtc)
            {
                invalidReason = OdfLocalizer.GetMessage(
                    "Err_OdfSignatureCrlUtilities_CrlExpired",
                    nextUpdateUtc.ToString("O", CultureInfo.InvariantCulture),
                    referenceTimeUtc.ToString("O", CultureInfo.InvariantCulture));
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            OdfKitDiagnostics.Warn($"CRL validity period check exception: {ex.Message}");
            invalidReason = ex.Message;
            return false;
        }
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
                        if (urls.Count == MaxDistributionPointUrls)
                        {
                            return urls;
                        }
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
        string ascii = Encoding.ASCII.GetString(rawData);
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
                {
                    urls.Add(url);
                    if (urls.Count == MaxDistributionPointUrls)
                        return;
                }
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
                ?? throw new CryptographicException(OdfLocalizer.GetMessage("Err_OdfSignatureCrlUtilities_UnableParseCrlContent_2"));
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
