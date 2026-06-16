using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace OdfKit.Core;

public static partial class OdfSigner
{
    #region Verification - Revocation

    private static async Task<bool> VerifyRevocationStatusAsync(
        List<X509Certificate2> chainCerts,
        List<byte[]> embeddedCrls,
        OdfSigningOptions options,
        OdfSingleSignatureValidationResult singleResult)
    {
        singleResult.ValidationSteps.Add("5. Verifying revocation status...");
        foreach (var chainCert in chainCerts)
        {
            if (chainCert.Subject == chainCert.Issuer)
                continue;

            X509Certificate2? issuerCert = null;
            foreach (var c in chainCerts)
            {
                if (StructuralEqual(c.SubjectName.RawData, chainCert.IssuerName.RawData))
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
                    var tbsNode = GetTbsNode(crlBytes);
                    if (tbsNode != null)
                    {
                        var crlIssuer = GetCrlIssuerDer(tbsNode);
                        if (crlIssuer != null && StructuralEqual(crlIssuer, chainCert.IssuerName.RawData))
                        {
                            if (!VerifyCrlSignature(crlBytes, issuerCert))
                            {
                                singleResult.ErrorCode = "CRL_SIGNATURE_INVALID";
                                throw new CryptographicException("Embedded CRL signature is invalid.");
                            }

                            checkedAnyCrl = true;
                            var revoked = GetRevokedSerialNumbers(crlBytes);
                            if (revoked.Contains(NormalizeHexSerial(chainCert.SerialNumber)))
                            {
                                isRevoked = true;
                                singleResult.ErrorCode = "CERTIFICATE_REVOKED";
                                break;
                            }
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
                var urls = GetCrlUrls(chainCert);
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
                    try
                    {
                        byte[] crlBytes = await DownloadCrlAsync(url, options.HttpClient);
                        var tbsNode = GetTbsNode(crlBytes);
                        if (tbsNode != null)
                        {
                            if (!VerifyCrlSignature(crlBytes, issuerCert))
                            {
                                singleResult.ErrorCode = "CRL_SIGNATURE_INVALID";
                                throw new CryptographicException("Downloaded CRL signature is invalid.");
                            }

                            onlineCrlCheckedSuccessfully = true;
                            var revoked = GetRevokedSerialNumbers(crlBytes);
                            if (revoked.Contains(NormalizeHexSerial(chainCert.SerialNumber)))
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

    #endregion
}
