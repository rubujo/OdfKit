using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
using OdfKit.Compliance;

namespace OdfKit.Core;

/// <summary>
/// 提供 ODF 封裝的數位簽署與驗證功能。
/// </summary>
public static class OdfSigner
{
    private const string SignaturePath = "META-INF/documentsignatures.xml";
    private static readonly HttpClient s_httpClient = new();

    /// <summary>
    /// 對 ODF 封裝中的關鍵檔案進行數位簽署，支援同僚聯署。
    /// </summary>
    /// <param name="package">要簽署的 ODF 封裝</param>
    /// <param name="certificate">用於簽署的 X.509 憑證</param>
    public static void Sign(OdfPackage package, X509Certificate2 certificate)
    {
        Sign(package, certificate, new OdfSigningOptions { Level = XadesLevel.None });
    }

    /// <summary>
    /// 對 ODF 封裝中的關鍵檔案進行數位簽署，支援自訂選項。
    /// </summary>
    /// <param name="package">要簽署的 ODF 封裝</param>
    /// <param name="certificate">用於簽署的 X.509 憑證</param>
    /// <param name="options">簽署選項</param>
    public static void Sign(OdfPackage package, X509Certificate2 certificate, OdfSigningOptions options)
    {
        SignAsync(package, certificate, options).GetAwaiter().GetResult();
    }

    /// <summary>
    /// 對 ODF 封裝中的關鍵檔案進行數位簽署（非同步）。
    /// </summary>
    /// <param name="package">要簽署的 ODF 封裝</param>
    /// <param name="certificate">用於簽署的 X.509 憑證</param>
    /// <param name="options">簽署選項</param>
    /// <returns>代表非同步作業的工作</returns>
    public static async Task SignAsync(OdfPackage package, X509Certificate2 certificate, OdfSigningOptions options)
    {
        if (package == null)
            throw new ArgumentNullException(nameof(package));
        if (certificate == null)
            throw new ArgumentNullException(nameof(certificate));
        if (!certificate.HasPrivateKey)
            throw new ArgumentException("Certificate must contain a private key.", nameof(certificate));
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        // Use RSA or ECDsa private key directly from certificate to support HSM and enterprise certs without export
        using var privateKey = certificate.GetRSAPrivateKey() ?? (AsymmetricAlgorithm?)certificate.GetECDsaPrivateKey();
        if (privateKey == null)
        {
            throw new CryptographicException("Certificate does not have a supported RSA or ECDSA private key.");
        }

        // 1. Load or initialize documentsignatures.xml
        var doc = new XmlDocument();
        XmlElement root;
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };

        if (package.HasEntry(SignaturePath))
        {
            using var stream = package.GetEntryStream(SignaturePath);
            using var reader = XmlReader.Create(stream, settings);
            doc.Load(reader);
            root = doc.DocumentElement ?? throw new CryptographicException("Invalid signature file structure.");
        }
        else
        {
            root = doc.CreateElement("document-signatures", OdfNamespaces.Dsig);
            root.SetAttribute("version", GetSignatureDocumentVersion(package.Version));
            doc.AppendChild(root);
        }

        // Pre-register signature entry in the package manifest to stabilize META-INF/manifest.xml before signing
        package.WriteEntry(SignaturePath, Array.Empty<byte>(), "text/xml");
        package.SaveManifestToEntries();

        // 2. Setup XadesSignedXml with custom package resolver
        var signedXml = new XadesSignedXml(doc)
        {
            SigningKey = privateKey,
            Resolver = new OdfPackageXmlResolver(package)
        };
        signedXml.SignedInfo!.CanonicalizationMethod = SignedXml.XmlDsigC14NTransformUrl;

        // Generate unique IDs
        string signatureId = "xmldsig-" + Guid.NewGuid();
        string signedPropertiesId = "xades-signedproperties-" + Guid.NewGuid();

        signedXml.Signature.Id = signatureId;

        // Explicitly specify signature method to use SHA-256 (avoiding default SHA-1)
        if (privateKey is RSA)
        {
            signedXml.SignedInfo.SignatureMethod = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";
        }
        else if (privateKey is ECDsa)
        {
            signedXml.SignedInfo.SignatureMethod = "http://www.w3.org/2001/04/xmldsig-more#ecdsa-sha256";
        }

        // Setup KeyInfo with certificate
        var keyInfo = new KeyInfo();
        keyInfo.AddClause(new KeyInfoX509Data(certificate));
        signedXml.KeyInfo = keyInfo;

        // 3. Setup XAdES if requested
        XmlElement? qualifyingProperties = null;
        if (options.SignatureLevel != OdfSignatureLevel.None)
        {
            var dataObject = new DataObject();

            qualifyingProperties = doc.CreateElement("xades", "QualifyingProperties", "http://uri.etsi.org/01903/v1.3.2#");
            qualifyingProperties.SetAttribute("Target", "#" + signatureId);

            var signedProperties = doc.CreateElement("xades", "SignedProperties", "http://uri.etsi.org/01903/v1.3.2#");
            signedProperties.SetAttribute("Id", signedPropertiesId);

            var signedSignatureProperties = doc.CreateElement("xades", "SignedSignatureProperties", "http://uri.etsi.org/01903/v1.3.2#");

            // SigningTime
            var signingTime = doc.CreateElement("xades", "SigningTime", "http://uri.etsi.org/01903/v1.3.2#");
            signingTime.InnerText = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
            signedSignatureProperties.AppendChild(signingTime);

            // SigningCertificate
            var signingCertificate = doc.CreateElement("xades", "SigningCertificate", "http://uri.etsi.org/01903/v1.3.2#");
            var cert = doc.CreateElement("xades", "Cert", "http://uri.etsi.org/01903/v1.3.2#");

            var certDigest = doc.CreateElement("xades", "CertDigest", "http://uri.etsi.org/01903/v1.3.2#");
            var digestMethod = doc.CreateElement("ds", "DigestMethod", OdfNamespaces.Ds);
            digestMethod.SetAttribute("Algorithm", SignedXml.XmlDsigSHA256Url);
            certDigest.AppendChild(digestMethod);

            var digestValue = doc.CreateElement("ds", "DigestValue", OdfNamespaces.Ds);
            using (var sha256 = SHA256.Create())
            {
                digestValue.InnerText = Convert.ToBase64String(sha256.ComputeHash(certificate.RawData));
            }
            certDigest.AppendChild(digestValue);
            cert.AppendChild(certDigest);

            var issuerSerial = doc.CreateElement("xades", "IssuerSerial", "http://uri.etsi.org/01903/v1.3.2#");
            var issuerName = doc.CreateElement("ds", "X509IssuerName", OdfNamespaces.Ds);
            issuerName.InnerText = certificate.IssuerName.Name;
            issuerSerial.AppendChild(issuerName);

            var serialNumber = doc.CreateElement("ds", "X509SerialNumber", OdfNamespaces.Ds);
            var bigSerial = System.Numerics.BigInteger.Parse("0" + certificate.SerialNumber, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
            serialNumber.InnerText = bigSerial.ToString();
            issuerSerial.AppendChild(serialNumber);

            cert.AppendChild(issuerSerial);
            signingCertificate.AppendChild(cert);
            signedSignatureProperties.AppendChild(signingCertificate);
            signedProperties.AppendChild(signedSignatureProperties);
            qualifyingProperties.AppendChild(signedProperties);

            dataObject.Data = qualifyingProperties.SelectNodes(".")!;
            signedXml.AddObject(dataObject);

            // Add reference to SignedProperties
            var refProperties = new Reference("#" + signedPropertiesId)
            {
                Type = "http://uri.etsi.org/01903/v1.3.2#SignedProperties",
                DigestMethod = SignedXml.XmlDsigSHA256Url
            };
            refProperties.AddTransform(new XmlDsigExcC14NTransform());
            signedXml.AddReference(refProperties);
        }

        // 4. Compute hashes of key files in the package and add as References
        // Key files to sign: content.xml, styles.xml, meta.xml, settings.xml, and META-INF/manifest.xml
        string[] filesToSign = { "content.xml", "styles.xml", "meta.xml", "settings.xml", "META-INF/manifest.xml" };
        var openStreams = new List<Stream>();
        try
        {
            foreach (var file in filesToSign)
            {
                if (package.HasEntry(file))
                {
                    var reference = new Reference(file);
                    // Standard ODF 1.3 uses SHA-256
                    reference.DigestMethod = SignedXml.XmlDsigSHA256Url;
                    var stream = package.GetEntryStream(file);
                    openStreams.Add(stream);
                    InjectReferenceStream(reference, stream);
                    signedXml.AddReference(reference);
                }
            }

            // 5. Compute Signature
            signedXml.ComputeSignature();
        }
        finally
        {
            foreach (var stream in openStreams)
            {
                stream.Dispose();
            }
        }

        // Import signature element into doc
        var xmlSignature = signedXml.GetXml();

        bool requiresTimestamp = options.SignatureLevel is OdfSignatureLevel.XadesT or OdfSignatureLevel.XadesLT or OdfSignatureLevel.XadesA;
        bool requiresLongTermValues = options.SignatureLevel is OdfSignatureLevel.XadesLT or OdfSignatureLevel.XadesA;

        // 6. If XAdES-T/LT/A, fetch timestamp and build unsigned properties
        if (requiresTimestamp)
        {
            if (string.IsNullOrEmpty(options.TsaUrl))
            {
                throw new CryptographicException("TSA URL must be configured for XAdES-T, XAdES-LT, or XAdES-A.");
            }

            // Find the ds:SignatureValue element in the generated xmlSignature using relative XPath to support co-signing
            var nsManager = new XmlNamespaceManager(doc.NameTable);
            nsManager.AddNamespace("ds", OdfNamespaces.Ds);
            nsManager.AddNamespace("xades", "http://uri.etsi.org/01903/v1.3.2#");

            var sigValElem = xmlSignature.SelectSingleNode(".//ds:SignatureValue", nsManager) as XmlElement;
            if (sigValElem == null)
            {
                throw new CryptographicException("ds:SignatureValue element was not found in computed signature.");
            }

            // Canonicalize ds:SignatureValue consistently
            byte[] sigValueBytes = CanonicalizeSignatureValue(sigValElem);

            using var sha256 = SHA256.Create();
            byte[] sigHash = sha256.ComputeHash(sigValueBytes);

            // Fetch timestamp from TSA
            byte[] tsaResponse = await QueryTsaAsync(options.TsaUrl!, sigHash, options.HttpClient);
            byte[] tsToken = ExtractTimestampToken(tsaResponse);

            // Find qualifyingProperties in the imported xmlSignature XML node tree using relative XPath
            var importedQualProps = xmlSignature.SelectSingleNode(".//xades:QualifyingProperties", nsManager) as XmlElement;
            if (importedQualProps == null)
            {
                throw new CryptographicException("xades:QualifyingProperties element was not found in computed signature.");
            }

            var unsignedProps = doc.CreateElement("xades", "UnsignedProperties", "http://uri.etsi.org/01903/v1.3.2#");
            var unsignedSigProps = doc.CreateElement("xades", "UnsignedSignatureProperties", "http://uri.etsi.org/01903/v1.3.2#");

            var sigTimestamp = doc.CreateElement("xades", "SignatureTimeStamp", "http://uri.etsi.org/01903/v1.3.2#");
            sigTimestamp.SetAttribute("Id", "timestamp-" + Guid.NewGuid());

            var canonMethod = doc.CreateElement("ds", "CanonicalizationMethod", OdfNamespaces.Ds);
            canonMethod.SetAttribute("Algorithm", "http://www.w3.org/2001/10/xml-exc-c14n#");
            sigTimestamp.AppendChild(canonMethod);

            var encapTS = doc.CreateElement("xades", "EncapsulatedTimeStamp", "http://uri.etsi.org/01903/v1.3.2#");
            encapTS.InnerText = Convert.ToBase64String(tsToken);
            sigTimestamp.AppendChild(encapTS);

            unsignedSigProps.AppendChild(sigTimestamp);
            unsignedProps.AppendChild(unsignedSigProps);
            importedQualProps.AppendChild(unsignedProps);

            // 7. If XAdES-LT/A, add certificate values and revocation values
            if (requiresLongTermValues)
            {
                // Build certificate chain
                var chain = new X509Chain();
                chain.ChainPolicy.RevocationMode = options.CheckRevocation ? X509RevocationMode.Online : X509RevocationMode.NoCheck;
                foreach (var extraCertificate in options.ExtraCertificates)
                {
                    chain.ChainPolicy.ExtraStore.Add(extraCertificate);
                }

                chain.Build(certificate);

                var chainCerts = new List<X509Certificate2>();
                foreach (var element in chain.ChainElements)
                {
                    chainCerts.Add(element.Certificate);
                }

                // CertificateValues
                var certValues = doc.CreateElement("xades", "CertificateValues", "http://uri.etsi.org/01903/v1.3.2#");
                foreach (var chainCert in chainCerts)
                {
                    var encapCert = doc.CreateElement("xades", "EncapsulatedCertificate", "http://uri.etsi.org/01903/v1.3.2#");
                    encapCert.InnerText = Convert.ToBase64String(chainCert.RawData);
                    certValues.AppendChild(encapCert);
                }
                unsignedSigProps.AppendChild(certValues);

                // RevocationValues
                var revValues = doc.CreateElement("xades", "RevocationValues", "http://uri.etsi.org/01903/v1.3.2#");
                var crlValues = doc.CreateElement("xades", "CRLValues", "http://uri.etsi.org/01903/v1.3.2#");

                var downloadedCrls = new HashSet<string>();
                foreach (var chainCert in chainCerts)
                {
                    var urls = GetCrlUrls(chainCert);
                    foreach (var url in urls)
                    {
                        if (downloadedCrls.Add(url))
                        {
                            try
                            {
                                byte[] crlBytes = await DownloadCrlAsync(url, options.HttpClient);
                                if (crlBytes != null && crlBytes.Length > 0)
                                {
                                    var encapCrl = doc.CreateElement("xades", "EncapsulatedCRLValue", "http://uri.etsi.org/01903/v1.3.2#");
                                    encapCrl.InnerText = Convert.ToBase64String(crlBytes);
                                    crlValues.AppendChild(encapCrl);
                                }
                            }
                            catch (Exception ex)
                            {
                                OdfKitDiagnostics.Warn($"Failed to download CRL from {url}: {ex.Message}");
                            }
                        }
                    }
                }

                if (crlValues.HasChildNodes)
                {
                    revValues.AppendChild(crlValues);
                    unsignedSigProps.AppendChild(revValues);
                }
            }
        }

        var importedSignature = doc.ImportNode(xmlSignature, true);
        root.AppendChild(importedSignature);

        // 8. Write documentsignatures.xml back to package
        using var ms = new MemoryStream();
        var writerSettings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = false
        };
        using (var writer = XmlWriter.Create(ms, writerSettings))
        {
            doc.Save(writer);
        }

        package.WriteEntry(SignaturePath, ms.ToArray(), "text/xml");
        OdfKitDiagnostics.Info($"Added digital signature to package using certificate: {certificate.Subject}");
    }

    private static string GetSignatureDocumentVersion(OdfVersion packageVersion)
    {
        return packageVersion switch
        {
            OdfVersion.Odf12 => OdfVersionInfo.ToVersionString(OdfVersion.Odf12),
            OdfVersion.Odf13 or OdfVersion.Odf14 => OdfVersionInfo.ToVersionString(OdfVersion.Odf13),
            _ => OdfVersionInfo.ToVersionString(OdfVersion.Odf10)
        };
    }

    /// <summary>
    /// 驗證 ODF 封裝中的所有數位簽章。
    /// </summary>
    /// <param name="package">要驗證的 ODF 封裝</param>
    /// <param name="certificates">輸出參數，包含所有已驗證的憑證集合</param>
    /// <returns>若所有簽章皆有效，則為 <see langword="true"/>；否則為 <see langword="false"/></returns>
    public static bool VerifySignatures(OdfPackage package, out X509Certificate2Collection certificates)
    {
        certificates = new X509Certificate2Collection();
        var result = VerifySignatures(package, new OdfSigningOptions { Level = XadesLevel.None, AllowUntrustedRoot = true });
        foreach (var sig in result.Signatures)
        {
            if (sig.Certificate != null)
            {
                certificates.Add(sig.Certificate);
            }
        }
        return result.IsValid;
    }

    /// <summary>
    /// 驗證 ODF 封裝中的所有數位簽章，並傳回詳細的驗證結果。
    /// </summary>
    /// <param name="package">要驗證的 ODF 封裝</param>
    /// <param name="options">簽署選項</param>
    /// <returns>詳細的數位簽章驗證結果</returns>
    public static OdfSignatureValidationResult VerifySignatures(OdfPackage package, OdfSigningOptions? options = null)
    {
        return VerifySignaturesAsync(package, options).GetAwaiter().GetResult();
    }

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
                    // Extract certificate
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

                    // 1. Verify XML DSig signature
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

                    // 2. Validate certificate validity period
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

                    // 3. XAdES-BES CertDigest validation
                    singleResult.ValidationSteps.Add("3. Verifying signing certificate digest...");
                    if (!VerifySigningCertificateDigest((XmlElement)signatureNode, cert, out string? digestError))
                    {
                        singleResult.ErrorCode = "CERTIFICATE_DIGEST_MISMATCH";
                        singleResult.ErrorMessage = digestError;
                        overallValid = false;
                        continue;
                    }

                    // 4. Validate Certificate Chain
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
                        // Self-signed root fallback
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

                    // Gather all certs in chain
                    var chainCerts = new List<X509Certificate2>();
                    foreach (var el in chain.ChainElements)
                    {
                        chainCerts.Add(el.Certificate);
                    }

                    // 5. Revocation Check (offline CRLs & online CDP)
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

                        // Check embedded CRLs
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
                                        // Validate CRL cryptographic signature using issuer's public key
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

                        // If not found and revocation check is enabled, try online check
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
                                        // Validate CRL cryptographic signature using issuer's public key
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

                            // If online check was attempted but failed, and we did not successfully check any CRL
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

                    // 6. XAdES-T/A Timestamp validation
                    singleResult.ValidationSteps.Add("6. Verifying signature timestamp...");
                    var timestampNode = signatureNode.SelectSingleNode(".//xades:SignatureTimeStamp/xades:EncapsulatedTimeStamp", nsManager);
                    if (timestampNode != null)
                    {
                        var signedCms = new SignedCms();
                        // Wrap the decoding and signature verification of timestamp token in a try-catch, failing IsTimestampValid on exception
                        try
                        {
                            byte[] tsBytes = Convert.FromBase64String(timestampNode.InnerText.Trim());

                            signedCms.Decode(tsBytes);

                            try
                            {
                                // Verify signature and certificate trust chain
                                signedCms.CheckSignature(false);
                            }
                            catch (CryptographicException)
                            {
                                if (options.AllowUntrustedTimestamp)
                                {
                                    // Fall back to signature verification only
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

                        // Verify message imprint (hash of canonicalized ds:SignatureValue)
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

                    // Collect references
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

    #region Helper classes and ASN.1/DER/CRL/TSA utilities

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
                // Invalid embedded certificate values are ignored; chain validation will fail if they are required.
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
    /// Inject raw Stream directly into SignedXml's Reference using reflection to bypass modern .NET Core URI resolution limits.
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

        // ReferenceTargetType.Stream is 0
        var enumType = refTargetTypeField.FieldType;
        var streamVal = Enum.ToObject(enumType, 0); // 0 corresponds to Stream
        refTargetTypeField.SetValue(reference, streamVal);
    }

    #endregion
}

internal sealed class OdfPackageXmlResolver(OdfPackage package) : XmlResolver
{
    private readonly OdfPackage _package = package;

    public override System.Net.ICredentials Credentials
    {
        set { }
    }

    public override object? GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn)
    {
        if (absoluteUri == null)
            throw new ArgumentNullException(nameof(absoluteUri));

        if (string.Equals(absoluteUri.Scheme, "odf", StringComparison.OrdinalIgnoreCase))
        {
            string path = absoluteUri.AbsolutePath.TrimStart('/');
            if (_package.HasEntry(path))
            {
                return _package.GetEntryStream(path);
            }
        }
        else if (string.Equals(absoluteUri.Scheme, "file", StringComparison.OrdinalIgnoreCase))
        {
            string localPath = absoluteUri.LocalPath;

            // Try resolving relative to current working directory
            string currentDir = Directory.GetCurrentDirectory();
            string? relativePath = GetRelativePath(currentDir, localPath);
            if (relativePath != null && _package.HasEntry(relativePath))
            {
                return _package.GetEntryStream(relativePath);
            }

            // Try resolving relative to AppDomain base directory
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (!string.IsNullOrEmpty(baseDir))
            {
                relativePath = GetRelativePath(baseDir, localPath);
                if (relativePath != null && _package.HasEntry(relativePath))
                {
                    return _package.GetEntryStream(relativePath);
                }
            }

            // Fallback: check if the file name alone exists in the package
            string fileName = Path.GetFileName(localPath);
            if (_package.HasEntry(fileName))
            {
                return _package.GetEntryStream(fileName);
            }
        }

        return null;
    }

    public override Uri ResolveUri(Uri? baseUri, string? relativeUri)
    {
        if (baseUri == null)
        {
            return new Uri($"odf://package/{relativeUri?.TrimStart('/')}");
        }
        return new Uri(baseUri, relativeUri);
    }

    private static string? GetRelativePath(string baseDir, string fullPath)
    {
        try
        {
            if (!baseDir.EndsWith(Path.DirectorySeparatorChar.ToString()) &&
                !baseDir.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
            {
                baseDir += Path.DirectorySeparatorChar;
            }

            Uri baseUri = new Uri(baseDir);
            Uri fullUri = new Uri(fullPath);
            if (baseUri.Scheme != fullUri.Scheme)
                return null;

            Uri relativeUri = baseUri.MakeRelativeUri(fullUri);
            if (relativeUri.IsAbsoluteUri)
                return null;

            return Uri.UnescapeDataString(relativeUri.ToString()).Replace('\\', '/');
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// 自訂的 <see cref="SignedXml"/> 子類別，手動尋找符合參考 URI ID 的項目，以繞過 .NET Core 中 GetElementById 的結構描述解析限制。
/// </summary>
internal sealed class XadesSignedXml : SignedXml
{
    /// <summary>
    /// 使用指定的 XML 文件初始化 <see cref="XadesSignedXml"/> 類別的新執行個體。
    /// </summary>
    /// <param name="document">用於初始化 <see cref="XadesSignedXml"/> 的 XML 文件</param>
    public XadesSignedXml(XmlDocument document) : base(document)
    {
    }

    /// <summary>
    /// 使用指定的 XML 元素初始化 <see cref="XadesSignedXml"/> 類別的新執行個體。
    /// </summary>
    /// <param name="element">用於初始化 <see cref="XadesSignedXml"/> 的 XML 元素</param>
    public XadesSignedXml(XmlElement element) : base(element)
    {
    }

    /// <summary>
    /// 傳回指定 XML 文件中具有指定 ID 的 XML 元素。
    /// </summary>
    /// <param name="document">要搜尋的 XML 文件</param>
    /// <param name="idValue">要尋找的 ID 值</param>
    /// <returns>若找到具有指定 ID 的元素，則為該元素；否則為 <see langword="null"/></returns>
    public override XmlElement? GetIdElement(XmlDocument? document, string idValue)
    {
        if (document == null)
            return null;

        // 1. Search the signature's ObjectList
        foreach (var obj in this.m_signature.ObjectList)
        {
            if (obj is DataObject dataObj && dataObj.Data != null)
            {
                foreach (XmlNode node in dataObj.Data)
                {
                    var found = FindElementById(node, idValue);
                    if (found != null)
                        return found;
                }
            }
        }

        // 2. Search within the signature element tree (this.GetXml())
        // To prevent "A Reference must contain a DigestValue" exception when calling GetXml() during signing,
        // we temporarily assign dummy digest values to references that don't have one.
        var tempReferences = new List<(Reference Ref, byte[]? OrigValue)>();
        if (this.SignedInfo != null)
        {
            foreach (Reference reference in this.SignedInfo.References)
            {
                if (reference.DigestValue == null)
                {
                    tempReferences.Add((reference, null));
                    reference.DigestValue = Array.Empty<byte>();
                }
            }
        }

        try
        {
            var signatureElement = this.GetXml();
            var element = FindElementById(signatureElement, idValue);
            if (element != null)
                return element;
        }
        finally
        {
            foreach (var temp in tempReferences)
            {
                temp.Ref.DigestValue = temp.OrigValue;
            }
        }

        return null;
    }

    private static XmlElement? FindElementById(XmlNode? node, string idValue)
    {
        if (node == null)
            return null;
        if (node is XmlElement element)
        {
            if (element.GetAttribute("Id") == idValue || element.GetAttribute("id") == idValue)
                return element;
        }

        foreach (XmlNode child in node.ChildNodes)
        {
            var result = FindElementById(child, idValue);
            if (result != null)
                return result;
        }

        return null;
    }
}

internal class DerNode(byte tag, byte[] value)
{
    public byte Tag { get; set; } = tag;
    public byte[] Value { get; set; } = value;
    public List<DerNode> Children { get; } = [];
    public int StartOffset { get; set; }
    public int Length { get; set; }
    public byte[] RawBytes { get; set; } = [];
}
