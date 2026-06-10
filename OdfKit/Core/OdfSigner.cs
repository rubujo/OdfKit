using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;

namespace OdfKit.Core
{
    public static class OdfSigner
    {
        private const string SignaturePath = "META-INF/documentsignatures.xml";

        /// <summary>
        /// 對 ODF 封裝中的關鍵檔案進行數位簽署，支援同僚聯署。
        /// </summary>
        public static void Sign(OdfPackage package, X509Certificate2 certificate)
        {
            if (package == null) throw new ArgumentNullException(nameof(package));
            if (certificate == null) throw new ArgumentNullException(nameof(certificate));
            if (!certificate.HasPrivateKey) throw new ArgumentException("Certificate must contain a private key.", nameof(certificate));

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
                root.SetAttribute("version", "1.3");
                doc.AppendChild(root);
            }
            
            // Pre-register signature entry in the package manifest to stabilize META-INF/manifest.xml before signing
            package.WriteEntry(SignaturePath, Array.Empty<byte>(), "text/xml");
            package.SaveManifestToEntries();

            // 2. Setup SignedXml
            var signedXml = new SignedXml(doc)
            {
                SigningKey = privateKey
            };
            signedXml.SignedInfo!.CanonicalizationMethod = SignedXml.XmlDsigC14NTransformUrl;

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

            // 3. Compute hashes of key files in the package and add as References
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

                        // Retrieve original unparsed stream from package and inject directly
                        var stream = package.GetEntryStream(file);
                        openStreams.Add(stream);
                        InjectReferenceStream(reference, stream);

                        signedXml.AddReference(reference);
                    }
                }

                // 4. Compute Signature
                signedXml.ComputeSignature();
            }
            finally
            {
                // Clean up and release streams after signature computation is completed to avoid locks
                foreach (var stream in openStreams)
                {
                    stream.Dispose();
                }
            }

            // Import signature element into doc
            var xmlSignature = signedXml.GetXml();
            var importedSignature = doc.ImportNode(xmlSignature, true);
            root.AppendChild(importedSignature);

            // 5. Write documentsignatures.xml back to package
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

        /// <summary>
        /// 驗證 ODF 封裝中的所有數位簽章。
        /// </summary>
        public static bool VerifySignatures(OdfPackage package, out X509Certificate2Collection certificates)
        {
            certificates = new X509Certificate2Collection();
            if (package == null) throw new ArgumentNullException(nameof(package));

            if (!package.HasEntry(SignaturePath))
            {
                OdfKitDiagnostics.Info("No digital signatures found in package.");
                return false;
            }

            try
            {
                var doc = new XmlDocument();
                using (var stream = package.GetEntryStream(SignaturePath))
                {
                    var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
                    using var reader = XmlReader.Create(stream, settings);
                    doc.Load(reader);
                }

                var nsManager = new XmlNamespaceManager(doc.NameTable);
                nsManager.AddNamespace("ds", OdfNamespaces.Ds);
                nsManager.AddNamespace("dsig", OdfNamespaces.Dsig);

                var signatureNodes = doc.SelectNodes("//ds:Signature", nsManager);
                if (signatureNodes == null || signatureNodes.Count == 0)
                {
                    return false;
                }

                bool allValid = true;

                foreach (XmlNode signatureNode in signatureNodes)
                {
                    var signedXml = new SignedXml(doc);
                    signedXml.LoadXml((XmlElement)signatureNode);

                    var openStreams = new List<Stream>();
                    try
                    {
                        if (signedXml.SignedInfo != null)
                        {
                            foreach (Reference reference in signedXml.SignedInfo.References)
                            {
                                string? uri = reference.Uri;
                                if (!string.IsNullOrEmpty(uri))
                                {
                                    string entryName = uri!.Replace('\\', '/').TrimStart('/');
                                    if (package.HasEntry(entryName))
                                    {
                                        var stream = package.GetEntryStream(entryName);
                                        openStreams.Add(stream);
                                        InjectReferenceStream(reference, stream);
                                    }
                                }
                            }
                        }

                        // Extract certificate from KeyInfo
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
                                if (cert != null) break;
                            }
                        }

                        if (cert == null)
                        {
                            OdfKitDiagnostics.Warn("Signature key info does not contain a valid X509 certificate.");
                            allValid = false;
                            continue;
                        }

                        if (!certificates.Contains(cert))
                        {
                            certificates.Add(cert);
                        }

                        // Verify signature cryptography and reference digests using injected streams
                        bool isSignatureValid = signedXml.CheckSignature(cert, true);
                        if (!isSignatureValid)
                        {
                            OdfKitDiagnostics.Warn($"Signature verification failed for cert: {cert.Subject}");
                            allValid = false;
                            continue;
                        }
                    }
                    finally
                    {
                        foreach (var stream in openStreams)
                        {
                            stream.Dispose();
                        }
                    }
                }

                return allValid;
            }
            catch (Exception ex)
            {
                OdfKitDiagnostics.Error("Error during digital signature verification", ex);
                return false;
            }
        }

        /// <summary>
        /// Inject raw Stream directly into SignedXml's Reference using reflection to bypass modern .NET Core URI resolution limits.
        /// </summary>
        private static void InjectReferenceStream(Reference reference, Stream stream)
        {
            if (reference == null) throw new ArgumentNullException(nameof(reference));
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            var type = typeof(Reference);
            var refTargetField = type.GetField("_refTarget", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? type.GetField("m_refTarget", BindingFlags.Instance | BindingFlags.NonPublic);
                
            var refTargetTypeField = type.GetField("_refTargetType", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? type.GetField("m_refTargetType", BindingFlags.Instance | BindingFlags.NonPublic);

            if (refTargetField == null || refTargetTypeField == null)
            {
                throw new CryptographicException("Failed to locate required internal fields (_refTarget / _refTargetType) in Reference.");
            }

            refTargetField.SetValue(reference, stream);
            
            // ReferenceTargetType.Stream is 0
            var enumType = refTargetTypeField.FieldType;
            var streamVal = Enum.ToObject(enumType, 0); // 0 corresponds to Stream
            refTargetTypeField.SetValue(reference, streamVal);
        }
    }
}
