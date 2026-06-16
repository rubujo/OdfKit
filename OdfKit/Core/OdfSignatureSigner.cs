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

namespace OdfKit.Core;

/// <summary>
/// ODF 封裝數位簽章簽署管線（內部協作者）。
/// </summary>
internal static class OdfSignatureSigner
{
    /// <summary>
    /// 對 ODF 封裝中的關鍵檔案進行數位簽署（非同步）。
    /// </summary>
    /// <param name="package">要簽署的 ODF 封裝</param>
    /// <param name="certificate">用於簽署的 X.509 憑證</param>
    /// <param name="options">簽署選項</param>
    /// <returns>代表非同步作業的工作</returns>
    internal static async Task SignAsync(
        OdfPackage package,
        X509Certificate2 certificate,
        OdfSigningOptions options)
    {
        if (package is null)
            throw new ArgumentNullException(nameof(package));
        if (certificate is null)
            throw new ArgumentNullException(nameof(certificate));
        if (!certificate.HasPrivateKey)
            throw new ArgumentException("Certificate must contain a private key.", nameof(certificate));
        if (options is null)
            throw new ArgumentNullException(nameof(options));

        using AsymmetricAlgorithm? privateKey = certificate.GetRSAPrivateKey() ?? (AsymmetricAlgorithm?)certificate.GetECDsaPrivateKey();
        if (privateKey is null)
            throw new CryptographicException("Certificate does not have a supported RSA or ECDSA private key.");

        var doc = new XmlDocument();
        XmlElement root;
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };

        if (package.HasEntry(OdfSignerConstants.SignaturePath))
        {
            using Stream stream = package.GetEntryStream(OdfSignerConstants.SignaturePath);
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

        package.WriteEntry(OdfSignerConstants.SignaturePath, Array.Empty<byte>(), "text/xml");
        package.SaveManifestToEntries();

        var signedXml = new XadesSignedXml(doc)
        {
            SigningKey = privateKey,
            Resolver = new OdfPackageXmlResolver(package)
        };
        signedXml.SignedInfo!.CanonicalizationMethod = SignedXml.XmlDsigC14NTransformUrl;

        string signatureId = "xmldsig-" + Guid.NewGuid();
        string signedPropertiesId = "xades-signedproperties-" + Guid.NewGuid();

        signedXml.Signature.Id = signatureId;

        if (privateKey is RSA)
            signedXml.SignedInfo.SignatureMethod = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";
        else if (privateKey is ECDsa)
            signedXml.SignedInfo.SignatureMethod = "http://www.w3.org/2001/04/xmldsig-more#ecdsa-sha256";

        var keyInfo = new KeyInfo();
        keyInfo.AddClause(new KeyInfoX509Data(certificate));
        signedXml.KeyInfo = keyInfo;

        if (options.SignatureLevel != OdfSignatureLevel.None)
        {
            var dataObject = new DataObject();

            XmlElement qualifyingProperties = doc.CreateElement("xades", "QualifyingProperties", "http://uri.etsi.org/01903/v1.3.2#");
            qualifyingProperties.SetAttribute("Target", "#" + signatureId);

            var signedProperties = doc.CreateElement("xades", "SignedProperties", "http://uri.etsi.org/01903/v1.3.2#");
            signedProperties.SetAttribute("Id", signedPropertiesId);

            var signedSignatureProperties = doc.CreateElement("xades", "SignedSignatureProperties", "http://uri.etsi.org/01903/v1.3.2#");

            var signingTime = doc.CreateElement("xades", "SigningTime", "http://uri.etsi.org/01903/v1.3.2#");
            signingTime.InnerText = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
            signedSignatureProperties.AppendChild(signingTime);

            var signingCertificate = doc.CreateElement("xades", "SigningCertificate", "http://uri.etsi.org/01903/v1.3.2#");
            var cert = doc.CreateElement("xades", "Cert", "http://uri.etsi.org/01903/v1.3.2#");

            var certDigest = doc.CreateElement("xades", "CertDigest", "http://uri.etsi.org/01903/v1.3.2#");
            var digestMethod = doc.CreateElement("ds", "DigestMethod", OdfNamespaces.Ds);
            digestMethod.SetAttribute("Algorithm", SignedXml.XmlDsigSHA256Url);
            certDigest.AppendChild(digestMethod);

            var digestValue = doc.CreateElement("ds", "DigestValue", OdfNamespaces.Ds);
            using (var sha256 = SHA256.Create())
                digestValue.InnerText = Convert.ToBase64String(sha256.ComputeHash(certificate.RawData));
            certDigest.AppendChild(digestValue);
            cert.AppendChild(certDigest);

            var issuerSerial = doc.CreateElement("xades", "IssuerSerial", "http://uri.etsi.org/01903/v1.3.2#");
            var issuerName = doc.CreateElement("ds", "X509IssuerName", OdfNamespaces.Ds);
            issuerName.InnerText = certificate.IssuerName.Name;
            issuerSerial.AppendChild(issuerName);

            var serialNumber = doc.CreateElement("ds", "X509SerialNumber", OdfNamespaces.Ds);
            var bigSerial = BigInteger.Parse("0" + certificate.SerialNumber, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            serialNumber.InnerText = bigSerial.ToString();
            issuerSerial.AppendChild(serialNumber);

            cert.AppendChild(issuerSerial);
            signingCertificate.AppendChild(cert);
            signedSignatureProperties.AppendChild(signingCertificate);
            signedProperties.AppendChild(signedSignatureProperties);
            qualifyingProperties.AppendChild(signedProperties);

            dataObject.Data = qualifyingProperties.SelectNodes(".")!;
            signedXml.AddObject(dataObject);

            var refProperties = new Reference("#" + signedPropertiesId)
            {
                Type = "http://uri.etsi.org/01903/v1.3.2#SignedProperties",
                DigestMethod = SignedXml.XmlDsigSHA256Url
            };
            refProperties.AddTransform(new XmlDsigExcC14NTransform());
            signedXml.AddReference(refProperties);
        }

        string[] filesToSign = { "content.xml", "styles.xml", "meta.xml", "settings.xml", "META-INF/manifest.xml" };
        var openStreams = new List<Stream>();
        try
        {
            foreach (string file in filesToSign)
            {
                if (!package.HasEntry(file))
                    continue;

                var reference = new Reference(file) { DigestMethod = SignedXml.XmlDsigSHA256Url };
                Stream stream = package.GetEntryStream(file);
                openStreams.Add(stream);
                OdfSigner.InjectReferenceStream(reference, stream);
                signedXml.AddReference(reference);
            }

            signedXml.ComputeSignature();
        }
        finally
        {
            foreach (Stream stream in openStreams)
                stream.Dispose();
        }

        XmlElement xmlSignature = signedXml.GetXml();

        bool requiresTimestamp = options.SignatureLevel is OdfSignatureLevel.XadesT or OdfSignatureLevel.XadesLT or OdfSignatureLevel.XadesA;
        bool requiresLongTermValues = options.SignatureLevel is OdfSignatureLevel.XadesLT or OdfSignatureLevel.XadesA;

        if (requiresTimestamp)
        {
            if (string.IsNullOrEmpty(options.TsaUrl))
                throw new CryptographicException("TSA URL must be configured for XAdES-T, XAdES-LT, or XAdES-A.");

            var nsManager = new XmlNamespaceManager(doc.NameTable);
            nsManager.AddNamespace("ds", OdfNamespaces.Ds);
            nsManager.AddNamespace("xades", "http://uri.etsi.org/01903/v1.3.2#");

            var sigValElem = xmlSignature.SelectSingleNode(".//ds:SignatureValue", nsManager) as XmlElement
                ?? throw new CryptographicException("ds:SignatureValue element was not found in computed signature.");

            byte[] sigValueBytes = OdfSigner.CanonicalizeSignatureValue(sigValElem);

            using var sha256 = SHA256.Create();
            byte[] sigHash = sha256.ComputeHash(sigValueBytes);

            byte[] tsaResponse = await OdfSigner.QueryTsaAsync(options.TsaUrl!, sigHash, options.HttpClient);
            byte[] tsToken = OdfSigner.ExtractTimestampToken(tsaResponse);

            var importedQualProps = xmlSignature.SelectSingleNode(".//xades:QualifyingProperties", nsManager) as XmlElement
                ?? throw new CryptographicException("xades:QualifyingProperties element was not found in computed signature.");

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

            if (requiresLongTermValues)
            {
                var chain = new X509Chain();
                chain.ChainPolicy.RevocationMode = options.CheckRevocation ? X509RevocationMode.Online : X509RevocationMode.NoCheck;
                foreach (X509Certificate2 extraCertificate in options.ExtraCertificates)
                    chain.ChainPolicy.ExtraStore.Add(extraCertificate);

                chain.Build(certificate);

                var chainCerts = new List<X509Certificate2>();
                foreach (X509ChainElement element in chain.ChainElements)
                    chainCerts.Add(element.Certificate);

                var certValues = doc.CreateElement("xades", "CertificateValues", "http://uri.etsi.org/01903/v1.3.2#");
                foreach (X509Certificate2 chainCert in chainCerts)
                {
                    var encapCert = doc.CreateElement("xades", "EncapsulatedCertificate", "http://uri.etsi.org/01903/v1.3.2#");
                    encapCert.InnerText = Convert.ToBase64String(chainCert.RawData);
                    certValues.AppendChild(encapCert);
                }
                unsignedSigProps.AppendChild(certValues);

                var revValues = doc.CreateElement("xades", "RevocationValues", "http://uri.etsi.org/01903/v1.3.2#");
                var crlValues = doc.CreateElement("xades", "CRLValues", "http://uri.etsi.org/01903/v1.3.2#");

                var downloadedCrls = new HashSet<string>();
                foreach (X509Certificate2 chainCert in chainCerts)
                {
                    foreach (string url in OdfSigner.GetCrlUrls(chainCert))
                    {
                        if (!downloadedCrls.Add(url))
                            continue;

                        try
                        {
                            byte[] crlBytes = await OdfSigner.DownloadCrlAsync(url, options.HttpClient);
                            if (crlBytes is { Length: > 0 })
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

                if (crlValues.HasChildNodes)
                {
                    revValues.AppendChild(crlValues);
                    unsignedSigProps.AppendChild(revValues);
                }
            }
        }

        var importedSignature = doc.ImportNode(xmlSignature, true);
        root.AppendChild(importedSignature);

        using var ms = new MemoryStream();
        var writerSettings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = false
        };
        using (var writer = XmlWriter.Create(ms, writerSettings))
            doc.Save(writer);

        package.WriteEntry(OdfSignerConstants.SignaturePath, ms.ToArray(), "text/xml");
        OdfKitDiagnostics.Info($"Added digital signature to package using certificate: {certificate.Subject}");
    }

    private static string GetSignatureDocumentVersion(OdfVersion packageVersion) =>
        packageVersion switch
        {
            OdfVersion.Odf12 => OdfVersionInfo.ToVersionString(OdfVersion.Odf12),
            OdfVersion.Odf13 or OdfVersion.Odf14 => OdfVersionInfo.ToVersionString(OdfVersion.Odf13),
            _ => OdfVersionInfo.ToVersionString(OdfVersion.Odf10)
        };
}
