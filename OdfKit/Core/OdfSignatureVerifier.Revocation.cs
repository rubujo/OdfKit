using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

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
                    singleResult.IsRevocationValid = false;
                    singleResult.ErrorCode = "REVOCATION_CHECK_FAILED";
                    singleResult.ErrorMessage = $"Issuer certificate for {chainCert.Subject} not found in chain.";
                    return false;
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
                            throw new CryptographicException("Embedded CRL signature is invalid.");
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
                        singleResult.ErrorMessage = $"Embedded CRL validation failed: {ex.Message}";
                        return false;
                    }
                }
            }

            if (options.CheckRevocation && !singleResult.IsRevocationValid)
                return false;

            if (isRevoked)
            {
                singleResult.IsRevocationValid = false;
                singleResult.ErrorMessage = $"Certificate {chainCert.Subject} has been revoked.";
                return false;
            }

            if (options.CheckRevocation)
            {
                var urls = OdfSignatureCrlUtilities.GetCrlUrls(chainCert);
                if (urls.Count == 0 && !checkedAnyCrl)
                {
                    singleResult.IsRevocationValid = false;
                    singleResult.ErrorCode = "REVOCATION_CHECK_FAILED";
                    singleResult.ErrorMessage = $"No CRL distribution points found for certificate {chainCert.Subject}.";
                    return false;
                }

                bool onlineCrlCheckedSuccessfully = false;
                Exception? lastCrlException = null;

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
                                throw new CryptographicException("Downloaded CRL signature is invalid.");
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
                            throw new CryptographicException("Failed to parse downloaded CRL.");
                        }
                    }
                    catch (Exception ex)
                    {
                        lastCrlException = ex;
                    }
                }

                if (isRevoked)
                {
                    singleResult.IsRevocationValid = false;
                    if (string.IsNullOrEmpty(singleResult.ErrorCode))
                        singleResult.ErrorCode = "CERTIFICATE_REVOKED";
                    singleResult.ErrorMessage = $"Certificate {chainCert.Subject} has been revoked online.";
                    return false;
                }

                if (!onlineCrlCheckedSuccessfully && !checkedAnyCrl)
                {
                    singleResult.IsRevocationValid = false;
                    singleResult.ErrorCode = "REVOCATION_CHECK_FAILED";
                    singleResult.ErrorMessage = $"CRL retrieval or validation failed for certificate {chainCert.Subject}. Last error: {lastCrlException?.Message}";
                    return false;
                }
            }
        }

        return true;
    }
}
