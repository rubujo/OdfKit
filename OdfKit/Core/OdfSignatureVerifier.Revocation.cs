using System;
using System.Collections.Generic;
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
            if (chainCert.Subject == chainCert.Issuer)
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

                foreach (var url in urls)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        byte[] crlBytes = await OdfSignatureTsaClient.DownloadCrlAsync(
                            url,
                            options.HttpClient,
                            cancellationToken).ConfigureAwait(false);
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
                    catch (Exception ex)
                    {
                        crlFailureMessages.Add($"{url}: {ex.Message}");
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
}
