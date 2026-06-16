using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using OdfKit.Compliance;
using OdfKit.Core;

namespace OdfKit.Core;

public static partial class OdfSigner
{
    #region Signing

    /// <summary>
    /// 對 ODF 封裝中的關鍵檔案進行數位簽署（非同步）。
    /// </summary>
    /// <param name="package">要簽署的 ODF 封裝</param>
    /// <param name="certificate">用於簽署的 X.509 憑證</param>
    /// <param name="options">簽署選項</param>
    /// <returns>代表非同步作業的工作</returns>
    internal static async Task SignCoreAsync(OdfPackage package, X509Certificate2 certificate, OdfSigningOptions options)
    {
        if (package == null)
            throw new ArgumentNullException(nameof(package));
        if (certificate == null)
            throw new ArgumentNullException(nameof(certificate));
        if (!certificate.HasPrivateKey)
            throw new ArgumentException("Certificate must contain a private key.", nameof(certificate));
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        // 直接從憑證取得 RSA 或 ECDsa 私密金鑰，以支援 HSM 與企業憑證（無需匯出）
        using var privateKey = certificate.GetRSAPrivateKey() ?? (AsymmetricAlgorithm?)certificate.GetECDsaPrivateKey();
        if (privateKey == null)
        {
            throw new CryptographicException("Certificate does not have a supported RSA or ECDSA private key.");
        }

        // 1. 載入或初始化 documentsignatures.xml
        var doc = new XmlDocument();
        XmlElement root;
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };

        if (package.HasEntry(OdfSignerConstants.SignaturePath))
        {
            using var stream = package.GetEntryStream(OdfSignerConstants.SignaturePath);
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

        // 於簽署前在封裝資訊清單中預先註冊簽章項目，以穩定 META-INF/manifest.xml
        package.WriteEntry(OdfSignerConstants.SignaturePath, Array.Empty<byte>(), "text/xml");
        package.SaveManifestToEntries();

        // 2. 以自訂封裝解析器設定 XadesSignedXml
        var signedXml = new XadesSignedXml(doc)
        {
            SigningKey = privateKey,
            Resolver = new OdfPackageXmlResolver(package)
        };
        signedXml.SignedInfo!.CanonicalizationMethod = SignedXml.XmlDsigC14NTransformUrl;

        // 產生唯一 ID
        string signatureId = "xmldsig-" + Guid.NewGuid();
        string signedPropertiesId = "xades-signedproperties-" + Guid.NewGuid();

        signedXml.Signature.Id = signatureId;

        // 明確指定簽章方法使用 SHA-256（避免預設的 SHA-1）
        if (privateKey is RSA)
        {
            signedXml.SignedInfo.SignatureMethod = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";
        }
        else if (privateKey is ECDsa)
        {
            signedXml.SignedInfo.SignatureMethod = "http://www.w3.org/2001/04/xmldsig-more#ecdsa-sha256";
        }

        // 設定含憑證的 KeyInfo
        var keyInfo = new KeyInfo();
        keyInfo.AddClause(new KeyInfoX509Data(certificate));
        signedXml.KeyInfo = keyInfo;

        // 3. 若需要則設定 XAdES
        XmlElement? qualifyingProperties = null;
        if (options.SignatureLevel != OdfSignatureLevel.None)
        {
            var dataObject = new DataObject();

            qualifyingProperties = doc.CreateElement("xades", "QualifyingProperties", "http://uri.etsi.org/01903/v1.3.2#");
            qualifyingProperties.SetAttribute("Target", "#" + signatureId);

            var signedProperties = doc.CreateElement("xades", "SignedProperties", "http://uri.etsi.org/01903/v1.3.2#");
            signedProperties.SetAttribute("Id", signedPropertiesId);

            var signedSignatureProperties = doc.CreateElement("xades", "SignedSignatureProperties", "http://uri.etsi.org/01903/v1.3.2#");

            // 簽署時間 (SigningTime)
            var signingTime = doc.CreateElement("xades", "SigningTime", "http://uri.etsi.org/01903/v1.3.2#");
            signingTime.InnerText = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
            signedSignatureProperties.AppendChild(signingTime);

            // 簽署憑證 (SigningCertificate)
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

            // 新增對 SignedProperties 的 Reference
            var refProperties = new Reference("#" + signedPropertiesId)
            {
                Type = "http://uri.etsi.org/01903/v1.3.2#SignedProperties",
                DigestMethod = SignedXml.XmlDsigSHA256Url
            };
            refProperties.AddTransform(new XmlDsigExcC14NTransform());
            signedXml.AddReference(refProperties);
        }

        // 4. 計算封裝中關鍵檔案的雜湊值並加入為 Reference
        // 要簽署的關鍵檔案：content.xml、styles.xml、meta.xml、settings.xml 與 META-INF/manifest.xml
        string[] filesToSign = { "content.xml", "styles.xml", "meta.xml", "settings.xml", "META-INF/manifest.xml" };
        var openStreams = new List<Stream>();
        try
        {
            foreach (var file in filesToSign)
            {
                if (package.HasEntry(file))
                {
                    var reference = new Reference(file);
                    // 標準 ODF 1.3 使用 SHA-256
                    reference.DigestMethod = SignedXml.XmlDsigSHA256Url;
                    var stream = package.GetEntryStream(file);
                    openStreams.Add(stream);
                    InjectReferenceStream(reference, stream);
                    signedXml.AddReference(reference);
                }
            }

            // 5. 計算簽章
            signedXml.ComputeSignature();
        }
        finally
        {
            foreach (var stream in openStreams)
            {
                stream.Dispose();
            }
        }

        // 將簽章元素匯入文件
        var xmlSignature = signedXml.GetXml();

        bool requiresTimestamp = options.SignatureLevel is OdfSignatureLevel.XadesT or OdfSignatureLevel.XadesLT or OdfSignatureLevel.XadesA;
        bool requiresLongTermValues = options.SignatureLevel is OdfSignatureLevel.XadesLT or OdfSignatureLevel.XadesA;

        // 6. 若為 XAdES-T/LT/A，取得時間戳記並建構未簽署屬性
        if (requiresTimestamp)
        {
            if (string.IsNullOrEmpty(options.TsaUrl))
            {
                throw new CryptographicException("TSA URL must be configured for XAdES-T, XAdES-LT, or XAdES-A.");
            }

            // 使用相對 XPath 在產生的 xmlSignature 中尋找 ds:SignatureValue 元素，以支援同僚聯署
            var nsManager = new XmlNamespaceManager(doc.NameTable);
            nsManager.AddNamespace("ds", OdfNamespaces.Ds);
            nsManager.AddNamespace("xades", "http://uri.etsi.org/01903/v1.3.2#");

            var sigValElem = xmlSignature.SelectSingleNode(".//ds:SignatureValue", nsManager) as XmlElement;
            if (sigValElem == null)
            {
                throw new CryptographicException("ds:SignatureValue element was not found in computed signature.");
            }

            // 一致地正規化 ds:SignatureValue
            byte[] sigValueBytes = CanonicalizeSignatureValue(sigValElem);

            using var sha256 = SHA256.Create();
            byte[] sigHash = sha256.ComputeHash(sigValueBytes);

            // 從 TSA 取得時間戳記
            byte[] tsaResponse = await QueryTsaAsync(options.TsaUrl!, sigHash, options.HttpClient);
            byte[] tsToken = ExtractTimestampToken(tsaResponse);

            // 使用相對 XPath 在匯入的 xmlSignature XML 節點樹中尋找 QualifyingProperties
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

            // 7. 若為 XAdES-LT/A，新增憑證值與撤銷值
            if (requiresLongTermValues)
            {
                // 建構憑證鏈
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

                // 憑證值 (CertificateValues)
                var certValues = doc.CreateElement("xades", "CertificateValues", "http://uri.etsi.org/01903/v1.3.2#");
                foreach (var chainCert in chainCerts)
                {
                    var encapCert = doc.CreateElement("xades", "EncapsulatedCertificate", "http://uri.etsi.org/01903/v1.3.2#");
                    encapCert.InnerText = Convert.ToBase64String(chainCert.RawData);
                    certValues.AppendChild(encapCert);
                }
                unsignedSigProps.AppendChild(certValues);

                // 撤銷值 (RevocationValues)
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

        // 8. 將 documentsignatures.xml 寫回封裝
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

        package.WriteEntry(OdfSignerConstants.SignaturePath, ms.ToArray(), "text/xml");
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

    #endregion
}
