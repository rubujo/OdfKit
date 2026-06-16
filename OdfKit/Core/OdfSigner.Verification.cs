using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Threading.Tasks;
using System.Xml;
using OdfKit.Core;

namespace OdfKit.Core;

public static partial class OdfSigner
{
    #region Verification

    /// <summary>
    /// 驗證 ODF 封裝中的所有數位簽章，並傳回詳細的驗證結果（非同步）。
    /// </summary>
    /// <param name="package">要驗證的 ODF 封裝</param>
    /// <param name="options">簽署選項</param>
    /// <returns>代表非同步作業的工作，其結果包含詳細的數位簽章驗證結果</returns>
    public static async Task<OdfSignatureValidationResult> VerifySignaturesAsync(OdfPackage package, OdfSigningOptions? options = null)
    {
        options ??= new OdfSigningOptions();
        var result = new OdfSignatureValidationResult { IsValid = true };

        if (package == null)
            throw new ArgumentNullException(nameof(package));

        if (!package.HasEntry(SignaturePath))
        {
            result.IsValid = false;
            return result;
        }

        try
        {
            var doc = new XmlDocument();
            using (var stream = package.GetEntryStream(SignaturePath))
            {
                var readerSettings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
                using var reader = XmlReader.Create(stream, readerSettings);
                doc.Load(reader);
            }

            var nsManager = new XmlNamespaceManager(doc.NameTable);
            nsManager.AddNamespace("ds", OdfNamespaces.Ds);
            nsManager.AddNamespace("xades", "http://uri.etsi.org/01903/v1.3.2#");

            var signatureNodes = doc.SelectNodes("//ds:Signature", nsManager);
            if (signatureNodes == null || signatureNodes.Count == 0)
            {
                result.IsValid = false;
                return result;
            }

            bool overallValid = true;

            foreach (XmlNode signatureNode in signatureNodes)
            {
                var singleResult = new OdfSingleSignatureValidationResult
                {
                    SignatureId = (signatureNode as XmlElement)?.GetAttribute("Id"),
                    IsSignatureValid = false,
                    IsCertificateValid = false,
                    IsChainValid = false,
                    IsTimestampValid = true,
                    IsRevocationValid = true
                };
                result.Signatures.Add(singleResult);
                singleResult.ValidationSteps.Add($"Starting verification for signature ID: {singleResult.SignatureId}");

                var signedXml = new XadesSignedXml(doc)
                {
                    Resolver = new OdfPackageXmlResolver(package)
                };
                signedXml.LoadXml((XmlElement)signatureNode);

                bool singleValid = true;
                var embeddedCrls = new List<byte[]>();
                var crlValueNodes = signatureNode.SelectNodes(".//xades:EncapsulatedCRLValue", nsManager);
                if (crlValueNodes != null)
                {
                    foreach (XmlNode crlNode in crlValueNodes)
                    {
                        try
                        {
                            embeddedCrls.Add(Convert.FromBase64String(crlNode.InnerText.Trim()));
                        }
                        catch (FormatException ex)
                        {
                            singleResult.IsRevocationValid = false;
                            singleResult.ErrorCode = "CRL_INVALID_FORMAT";
                            singleResult.ErrorMessage = $"Embedded CRL is not valid Base64: {ex.Message}";
                            singleResult.Warnings.Add(singleResult.ErrorMessage);
                            singleValid = false;
                            overallValid = false;
                            break;
                        }
                    }
                }

                if (!singleValid)
                {
                    continue;
                }

                try
                {
                    // 擷取憑證
                    X509Certificate2? cert = null;
                    if (signedXml.KeyInfo != null)
                    {
                        foreach (KeyInfoClause clause in signedXml.KeyInfo)
                        {
                            if (clause is KeyInfoX509Data x509Data && x509Data.Certificates != null)
                            {
                                foreach (var certObj in x509Data.Certificates)
                                {
                                    if (certObj is X509Certificate2 x509Cert)
                                    {
                                        cert = x509Cert;
                                        break;
                                    }
                                }
                            }
                            if (cert != null)
                                break;
                        }
                    }

                    if (cert == null)
                    {
                        singleResult.ErrorCode = "CERTIFICATE_MISSING";
                        singleResult.ErrorMessage = "Signature key info does not contain a valid X509 certificate.";
                        overallValid = false;
                        continue;
                    }

                    singleResult.Certificate = cert;

                    // 1. 驗證 XML DSig 簽章
                    singleResult.ValidationSteps.Add("1. Verifying cryptographic XMLDSig signature...");

                    var openStreams = new List<Stream>();
                    bool isSignatureValid = false;
                    try
                    {
                        if (signedXml.SignedInfo != null)
                        {
                            foreach (Reference reference in signedXml.SignedInfo.References)
                            {
                                string? uri = reference.Uri;
                                if (!string.IsNullOrEmpty(uri) && !uri!.StartsWith("#"))
                                {
                                    string entryName = uri.Replace('\\', '/').TrimStart('/');
                                    if (package.HasEntry(entryName))
                                    {
                                        var stream = package.GetEntryStream(entryName);
                                        openStreams.Add(stream);
                                        InjectReferenceStream(reference, stream);
                                    }
                                }
                            }
                        }

                        isSignatureValid = signedXml.CheckSignature(cert, true);
                    }
                    finally
                    {
                        foreach (var stream in openStreams)
                        {
                            stream.Dispose();
                        }
                    }

                    singleResult.IsSignatureValid = isSignatureValid;
                    if (!isSignatureValid)
                    {
                        singleResult.ErrorCode = "CRYPTOGRAPHIC_SIGNATURE_INVALID";
                        singleResult.ErrorMessage = "XML signature verification failed (cryptographically invalid).";
                        overallValid = false;
                        continue;
                    }

                    // 2. 驗證憑證有效期間
                    singleResult.ValidationSteps.Add("2. Verifying certificate validity period...");
                    var now = DateTime.UtcNow;
                    var notBeforeUtc = cert.NotBefore.ToUniversalTime();
                    var notAfterUtc = cert.NotAfter.ToUniversalTime();
                    singleResult.IsCertificateValid = (now >= notBeforeUtc && now <= notAfterUtc);
                    if (!singleResult.IsCertificateValid)
                    {
                        singleResult.ErrorCode = "CERTIFICATE_EXPIRED";
                        singleResult.ErrorMessage = "Signing certificate is expired or not yet valid.";
                        overallValid = false;
                        continue;
                    }

                    // 3. XAdES-BES CertDigest 驗證
                    singleResult.ValidationSteps.Add("3. Verifying signing certificate digest...");
                    if (!VerifySigningCertificateDigest((XmlElement)signatureNode, cert, out string? digestError))
                    {
                        singleResult.ErrorCode = "CERTIFICATE_DIGEST_MISMATCH";
                        singleResult.ErrorMessage = digestError;
                        overallValid = false;
                        continue;
                    }

                    // 4. 驗證憑證鏈
                    singleResult.ValidationSteps.Add("4. Verifying certificate trust chain...");
                    var chain = new X509Chain();
                    chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                    foreach (var embeddedCertificate in GetEmbeddedCertificates(signatureNode, nsManager))
                    {
                        if (!StructuralEqual(embeddedCertificate.RawData, cert.RawData))
                        {
                            chain.ChainPolicy.ExtraStore.Add(embeddedCertificate);
                        }
                    }

                    foreach (var extraCertificate in options.ExtraCertificates)
                    {
                        if (!StructuralEqual(extraCertificate.RawData, cert.RawData))
                        {
                            chain.ChainPolicy.ExtraStore.Add(extraCertificate);
                        }
                    }

                    bool isChainValid = chain.Build(cert);
                    if (!isChainValid && options.AllowUntrustedRoot)
                    {
                        // 自簽署根憑證後援
                        bool onlyUntrustedRoot = true;
                        foreach (var status in chain.ChainStatus)
                        {
                            if (status.Status != X509ChainStatusFlags.UntrustedRoot &&
                                status.Status != X509ChainStatusFlags.PartialChain &&
                                status.Status != X509ChainStatusFlags.NoError)
                            {
                                onlyUntrustedRoot = false;
                                break;
                            }
                        }
                        isChainValid = onlyUntrustedRoot;
                    }
                    singleResult.IsChainValid = isChainValid;
                    if (!isChainValid)
                    {
                        singleResult.ErrorCode = "CERTIFICATE_CHAIN_INVALID";
                        singleResult.ErrorMessage = "Certificate chain validation failed.";
                        overallValid = false;
                        continue;
                    }

                    // 收集鏈中所有憑證
                    var chainCerts = new List<X509Certificate2>();
                    foreach (var el in chain.ChainElements)
                    {
                        chainCerts.Add(el.Certificate);
                    }

                    // 5. 撤銷檢查（離線 CRL 與線上 CDP）
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
                                singleValid = false;
                                overallValid = false;
                                break;
                            }
                            continue;
                        }

                        bool isRevoked = false;
                        bool checkedAnyCrl = false;

                        // 檢查內嵌 CRL
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
                                        // 使用簽發者公鑰驗證 CRL 密碼學簽章
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
                                    {
                                        singleResult.ErrorCode = "REVOCATION_CHECK_FAILED";
                                    }
                                    singleResult.ErrorMessage = $"Embedded CRL validation failed: {ex.Message}";
                                    singleValid = false;
                                    overallValid = false;
                                    break;
                                }
                            }
                        }

                        if (options.CheckRevocation && !singleResult.IsRevocationValid)
                        {
                            break;
                        }

                        if (isRevoked)
                        {
                            singleResult.IsRevocationValid = false;
                            singleResult.ErrorMessage = $"Certificate {chainCert.Subject} has been revoked.";
                            singleValid = false;
                            overallValid = false;
                            break;
                        }

                        // 若未找到且已啟用撤銷檢查，則嘗試線上檢查
                        if (options.CheckRevocation)
                        {
                            var urls = GetCrlUrls(chainCert);
                            if (urls.Count == 0 && !checkedAnyCrl)
                            {
                                singleResult.IsRevocationValid = false;
                                singleResult.ErrorCode = "REVOCATION_CHECK_FAILED";
                                singleResult.ErrorMessage = $"No CRL distribution points found for certificate {chainCert.Subject}.";
                                singleValid = false;
                                overallValid = false;
                                break;
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
                                        // 使用簽發者公鑰驗證 CRL 密碼學簽章
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
                                {
                                    singleResult.ErrorCode = "CERTIFICATE_REVOKED";
                                }
                                singleResult.ErrorMessage = $"Certificate {chainCert.Subject} has been revoked online.";
                                singleValid = false;
                                overallValid = false;
                                break;
                            }

                            // 若已嘗試線上檢查但失敗，且未成功檢查任何 CRL
                            if (!onlineCrlCheckedSuccessfully && !checkedAnyCrl)
                            {
                                singleResult.IsRevocationValid = false;
                                singleResult.ErrorCode = "REVOCATION_CHECK_FAILED";
                                singleResult.ErrorMessage = $"CRL retrieval or validation failed for certificate {chainCert.Subject}. Last error: {lastCrlException?.Message}";
                                singleValid = false;
                                overallValid = false;
                                break;
                            }
                        }
                    }

                    if (!singleValid)
                    {
                        continue;
                    }

                    // 6. XAdES-T/A 時間戳記驗證
                    singleResult.ValidationSteps.Add("6. Verifying signature timestamp...");
                    var timestampNode = signatureNode.SelectSingleNode(".//xades:SignatureTimeStamp/xades:EncapsulatedTimeStamp", nsManager);
                    if (timestampNode != null)
                    {
                        var signedCms = new SignedCms();
                        // 以 try-catch 包裝時間戳記權杖的解碼與簽章驗證，發生例外時將 IsTimestampValid 設為失敗
                        try
                        {
                            byte[] tsBytes = Convert.FromBase64String(timestampNode.InnerText.Trim());

                            signedCms.Decode(tsBytes);

                            try
                            {
                                // 驗證簽章與憑證信任鏈
                                signedCms.CheckSignature(false);
                            }
                            catch (CryptographicException)
                            {
                                if (options.AllowUntrustedTimestamp)
                                {
                                    // 後援僅驗證簽章
                                    signedCms.CheckSignature(true);
                                }
                                else
                                {
                                    throw;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            singleResult.IsTimestampValid = false;
                            singleResult.ErrorCode = "TIMESTAMP_SIGNATURE_INVALID";
                            singleResult.ErrorMessage = $"Timestamp signature verification failed: {ex.Message}";
                            overallValid = false;
                            continue;
                        }

                        // 驗證訊息印記（正規化後 ds:SignatureValue 的雜湊值）
                        var signatureValueElem = signatureNode.SelectSingleNode("ds:SignatureValue", nsManager) as XmlElement;
                        if (signatureValueElem == null)
                        {
                            singleResult.IsTimestampValid = false;
                            singleResult.ErrorCode = "TIMESTAMP_IMPRINT_MISMATCH";
                            singleResult.ErrorMessage = "Missing SignatureValue for timestamp verification.";
                            overallValid = false;
                            continue;
                        }

                        byte[] sigBytes = CanonicalizeSignatureValue(signatureValueElem);

                        using var sha256 = SHA256.Create();
                        byte[] calculatedHash = sha256.ComputeHash(sigBytes);

                        byte[]? embeddedHash = null;
                        var tstInfo = ParseDer(signedCms.ContentInfo.Content);
                        if (tstInfo.Tag == 0x30 && tstInfo.Children.Count >= 3)
                        {
                            var messageImprint = tstInfo.Children[2];
                            if (messageImprint.Tag == 0x30 && messageImprint.Children.Count >= 2)
                            {
                                var hashedMessageNode = messageImprint.Children[1];
                                embeddedHash = hashedMessageNode.Value;
                            }
                        }

                        if (embeddedHash == null || !StructuralEqual(calculatedHash, embeddedHash))
                        {
                            singleResult.IsTimestampValid = false;
                            singleResult.ErrorCode = "TIMESTAMP_IMPRINT_MISMATCH";
                            singleResult.ErrorMessage = "Timestamp message imprint does not match the signature value.";
                            overallValid = false;
                            continue;
                        }
                    }

                    // 收集 Reference
                    if (signedXml.SignedInfo != null)
                    {
                        foreach (Reference reference in signedXml.SignedInfo.References)
                        {
                            string? uri = reference.Uri;
                            if (uri != null && !uri.StartsWith("#"))
                            {
                                string entryName = uri.Replace('\\', '/').TrimStart('/');
                                singleResult.CheckedReferences.Add(entryName);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    singleResult.ErrorMessage = $"Verification error: {ex.Message}";
                    if (string.IsNullOrEmpty(singleResult.ErrorCode))
                    {
                        singleResult.ErrorCode = "VERIFICATION_ERROR";
                    }
                    overallValid = false;
                }
            }

            result.IsValid = overallValid;
            return result;
        }
        catch (Exception ex)
        {
            OdfKitDiagnostics.Error("Error during digital signature verification", ex);
            result.IsValid = false;
            return result;
        }
    }

    private static byte[] CanonicalizeSignatureValue(XmlElement signatureValueElem)
    {
        var cleanDoc = new XmlDocument();
        var imported = (XmlElement)cleanDoc.ImportNode(signatureValueElem, true);
        cleanDoc.AppendChild(imported);

        var transform = new XmlDsigExcC14NTransform();
        transform.LoadInput(imported.SelectNodes("descendant-or-self::node()")!);
        using var tsStream = (Stream)transform.GetOutput(typeof(Stream));
        using var tsMs = new MemoryStream();
        tsStream.CopyTo(tsMs);
        return tsMs.ToArray();
    }

    #endregion
}
