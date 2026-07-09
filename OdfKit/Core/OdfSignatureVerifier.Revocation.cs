using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

using OdfKit.Compliance;
namespace OdfKit.Core;

internal static partial class OdfSignatureVerifier
{
    private static async Task<bool> VerifyRevocationStatusAsync(
        List<X509Certificate2> chainCerts,
        List<byte[]> embeddedCrls,
        OdfSigningOptions options,
        OdfSingleSignatureValidationResult singleResult,
        CancellationToken cancellationToken = default)
    {
        singleResult.ValidationSteps.Add("5. Verifying revocation status...");
        foreach (var chainCert in chainCerts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (OdfEncryption.ByteArrayEquals(chainCert.SubjectName.RawData, chainCert.IssuerName.RawData))
                continue;

            X509Certificate2? issuerCert = null;
            foreach (var c in chainCerts)
            {
                if (OdfEncryption.ByteArrayEquals(c.SubjectName.RawData, chainCert.IssuerName.RawData))
                {
                    issuerCert = c;
                    break;
                }
            }

            if (issuerCert == null)
            {
                if (options.CheckRevocation)
                {
                    return Fail(
                        () => singleResult.IsRevocationValid = false,
                        singleResult,
                        "REVOCATION_CHECK_FAILED",
                        "Err_OdfSignatureVerifier_IssuerCertificateNotFound",
                        chainCert.Subject);
                }

                continue;
            }

            bool isRevoked = false;
            bool checkedAnyCrl = false;

            foreach (var crlBytes in embeddedCrls)
            {
                try
                {
                    var crlIssuer = OdfSignatureCrlUtilities.GetCrlIssuerRawData(crlBytes);
                    if (crlIssuer != null && OdfEncryption.ByteArrayEquals(crlIssuer, chainCert.IssuerName.RawData))
                    {
                        if (!OdfSignatureCrlUtilities.VerifyCrlSignature(crlBytes, issuerCert))
                        {
                            singleResult.ErrorCode = "CRL_SIGNATURE_INVALID";
                            throw new CryptographicException(OdfLocalizer.GetMessage("Err_OdfSignatureVerifier_InvalidEmbeddedCrlSignature"));
                        }

                        checkedAnyCrl = true;
                        var revoked = OdfSignatureCrlUtilities.GetRevokedSerialNumbers(crlBytes);
                        if (revoked.Contains(OdfSignatureDerCodec.NormalizeHexSerial(chainCert.SerialNumber)))
                        {
                            isRevoked = true;
                            singleResult.ErrorCode = "CERTIFICATE_REVOKED";
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (options.CheckRevocation)
                    {
                        singleResult.IsRevocationValid = false;
                        if (string.IsNullOrEmpty(singleResult.ErrorCode))
                            singleResult.ErrorCode = "REVOCATION_CHECK_FAILED";
                        singleResult.ErrorMessage = OdfLocalizer.GetMessage("Err_OdfSignatureVerifier_EmbeddedCrlValidationFailed");
                        singleResult.Warnings.Add(ex.Message);
                        return false;
                    }
                }
            }

            if (isRevoked)
            {
                return Fail(
                    () => singleResult.IsRevocationValid = false,
                    singleResult,
                    "CERTIFICATE_REVOKED",
                    "Err_OdfSignatureVerifier_CertificateRevoked",
                    chainCert.Subject);
            }

            if (options.CheckRevocation)
            {
                var urls = OdfSignatureCrlUtilities.GetCrlUrls(chainCert);
                if (urls.Count == 0 && !checkedAnyCrl)
                {
                    return Fail(
                        () => singleResult.IsRevocationValid = false,
                        singleResult,
                        "REVOCATION_CHECK_FAILED",
                        "Err_OdfSignatureVerifier_NoCrlDistributionPoints",
                        chainCert.Subject);
                }

                bool onlineCrlCheckedSuccessfully = false;
                var crlFailureMessages = new List<string>();

                if (urls.Count > 0)
                {
                    // 撤銷檢查的正確性優先於速度：原本的邏輯會逐一檢查「每一個」分發點，只要任何一個分發點回報撤銷即判定撤銷，
                    // 即使更早的分發點已成功驗證且未回報撤銷，仍會繼續檢查其餘分發點（而非「取第一個成功結果即停止」）。
                    // 因此這裡只將「下載」這個網路 I/O 動作平行發出以縮短總延遲（原本為逐一 await，延遲會線性疊加），
                    // 結果仍依原始 URL 順序逐一處理，完整保留上述語意與失敗訊息彙整順序；只有在提早判定撤銷而 break 迴圈時，
                    // 才會取消尚未完成的其餘下載以節省資源。
                    // Revocation-check correctness takes priority over speed: the original logic checks *every*
                    // distribution point and treats a revocation reported by any one of them as authoritative, even
                    // after an earlier point has already been successfully verified with no revocation found (i.e.
                    // this is not a "take the first successful result and stop" strategy). Therefore only the
                    // network I/O (the download itself) is issued concurrently to cut the total latency that used to
                    // accumulate linearly from sequential awaits; results are still processed in the original URL
                    // order, fully preserving the semantics above and the failure-message aggregation order. Only
                    // when the loop breaks early because revocation was found are the remaining in-flight downloads
                    // cancelled, to free up resources.
                    using var remainingDownloadsCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    var downloadTasks = new Task<(byte[]? Bytes, Exception? Error)>[urls.Count];
                    for (int i = 0; i < urls.Count; i++)
                    {
                        downloadTasks[i] = DownloadCrlSafeAsync(urls[i], options.HttpClient, remainingDownloadsCts.Token);
                    }

                    for (int i = 0; i < urls.Count; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        string url = urls[i];

                        try
                        {
                            (byte[]? bytes, Exception? error) = await downloadTasks[i].ConfigureAwait(false);
                            cancellationToken.ThrowIfCancellationRequested();
                            if (error != null)
                            {
                                throw error;
                            }

                            byte[] crlBytes = bytes!;
                            bool crlIsParseable = OdfSignatureCrlUtilities.GetCrlIssuerRawData(crlBytes) != null;
                            if (crlIsParseable)
                            {
                                if (!OdfSignatureCrlUtilities.VerifyCrlSignature(crlBytes, issuerCert))
                                {
                                    singleResult.ErrorCode = "CRL_SIGNATURE_INVALID";
                                    throw new CryptographicException(OdfLocalizer.GetMessage("Err_OdfSignatureVerifier_InvalidDownloadedCrlSignature"));
                                }

                                onlineCrlCheckedSuccessfully = true;
                                var revoked = OdfSignatureCrlUtilities.GetRevokedSerialNumbers(crlBytes);
                                if (revoked.Contains(OdfSignatureDerCodec.NormalizeHexSerial(chainCert.SerialNumber)))
                                {
                                    isRevoked = true;
                                    singleResult.ErrorCode = "CERTIFICATE_REVOKED";
                                    break;
                                }
                            }
                            else
                            {
                                throw new CryptographicException(OdfLocalizer.GetMessage("Err_OdfSignatureVerifier_FailedToParseDownloadedCrl"));
                            }
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            crlFailureMessages.Add($"{url}: {ex.Message}");
                        }
                    }

                    if (isRevoked)
                    {
                        remainingDownloadsCts.Cancel();
                    }
                }

                if (isRevoked)
                {
                    singleResult.IsRevocationValid = false;
                    if (string.IsNullOrEmpty(singleResult.ErrorCode))
                        singleResult.ErrorCode = "CERTIFICATE_REVOKED";
                    singleResult.ErrorMessage = OdfLocalizer.GetMessage(
                        "Err_OdfSignatureVerifier_CertificateRevokedOnline",
                        chainCert.Subject);
                    return false;
                }

                if (!onlineCrlCheckedSuccessfully && !checkedAnyCrl)
                {
                    string combinedCrlErrorMessage = string.Join("; ", crlFailureMessages);
                    singleResult.IsRevocationValid = false;
                    singleResult.ErrorCode = "REVOCATION_CHECK_FAILED";
                    singleResult.ErrorMessage = OdfLocalizer.GetMessage(
                        "Err_OdfSignatureVerifier_CrlCheckFailed",
                        chainCert.Subject,
                        combinedCrlErrorMessage);
                    foreach (string failureMessage in crlFailureMessages)
                        singleResult.Warnings.Add(failureMessage);
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// 下載單一分發點的 CRL，並將所有例外（含取消）包裝為傳回值，永不向外拋出。
    /// </summary>
    /// <remarks>
    /// 供多分發點平行下載使用：呼叫端會平行啟動多個此工作，並依原始 URL 順序逐一 await 其結果；
    /// 由於例外一律被包裝為傳回值而非拋出，即使呼叫端因提早判定撤銷而不再等待某些工作，也不會產生未觀察例外。
    /// </remarks>
    private static async Task<(byte[]? Bytes, Exception? Error)> DownloadCrlSafeAsync(
        string url,
        HttpClient? httpClient,
        CancellationToken cancellationToken)
    {
        try
        {
            byte[] bytes = await OdfSignatureTsaClient.DownloadCrlAsync(url, httpClient, cancellationToken).ConfigureAwait(false);
            return (bytes, null);
        }
        catch (Exception ex)
        {
            return (null, ex);
        }
    }
}
