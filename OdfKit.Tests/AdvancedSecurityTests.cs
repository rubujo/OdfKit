using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using OdfKit.Core;
using Xunit;

namespace OdfKit.Tests
{
    public class AdvancedSecurityTests
    {
#if NET9_0_OR_GREATER
        private static X509Certificate2 LoadCertificateFromPfx(byte[] pfxData)
        {
            return X509CertificateLoader.LoadPkcs12Collection(pfxData, (string?)null, X509KeyStorageFlags.Exportable)[0];
        }
#else
        private static X509Certificate2 LoadCertificateFromPfx(byte[] pfxData)
        {
            return new X509Certificate2(pfxData, (string?)null, X509KeyStorageFlags.Exportable);
        }
#endif
        [Fact]
        public async Task TestXmlDsigSigningAndVerification()
        {
            using var cert = GenerateSelfSignedCertificate("XmlDsigTestSigner", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(5));
            using var ms = new MemoryStream();
            
            // Create a package and write content to be signed
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                package.SetMimeType("application/vnd.oasis.opendocument.text");
                package.WriteEntry("content.xml", Encoding.UTF8.GetBytes("<content/>"), "text/xml");
                package.WriteEntry("styles.xml", Encoding.UTF8.GetBytes("<styles/>"), "text/xml");
                
                await OdfSigner.SignAsync(package, cert, new OdfSigningOptions { Level = XadesLevel.None });
                package.Save();
            }

            ms.Position = 0;
            using (var package = OdfPackage.Open(ms))
            {
                var result = await OdfSigner.VerifySignaturesAsync(package, new OdfSigningOptions { AllowUntrustedRoot = true });
                Assert.True(result.IsValid, result.Signatures.FirstOrDefault()?.ErrorMessage);
                Assert.Single(result.Signatures);
                
                var sig = result.Signatures[0];
                Assert.True(sig.IsSignatureValid);
                Assert.True(sig.IsCertificateValid);
                Assert.True(sig.IsChainValid);
                Assert.NotNull(sig.Certificate);
                Assert.Equal("CN=XmlDsigTestSigner", sig.Certificate.Subject);
            }
        }

        [Fact]
        public async Task TestXadesBesSigningAndVerification()
        {
            using var cert = GenerateSelfSignedCertificate("XadesBesTestSigner", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(5));
            using var ms = new MemoryStream();
            
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                package.SetMimeType("application/vnd.oasis.opendocument.text");
                package.WriteEntry("content.xml", Encoding.UTF8.GetBytes("<content/>"), "text/xml");
                
                await OdfSigner.SignAsync(package, cert, new OdfSigningOptions { Level = XadesLevel.BES });
                package.Save();
            }

            ms.Position = 0;
            using (var package = OdfPackage.Open(ms))
            {
                // Verify META-INF/documentsignatures.xml contains the BES QualifyingProperties
                using (var sigStream = package.GetEntryStream("META-INF/documentsignatures.xml"))
                {
                    var doc = new XmlDocument();
                    doc.Load(sigStream);
                    
                    var ns = new XmlNamespaceManager(doc.NameTable);
                    ns.AddNamespace("ds", OdfNamespaces.Ds);
                    ns.AddNamespace("xades", "http://uri.etsi.org/01903/v1.3.2#");
                    
                    var qualProps = doc.SelectSingleNode("//xades:QualifyingProperties", ns);
                    Assert.NotNull(qualProps);
                    
                    var signedProps = doc.SelectSingleNode("//xades:SignedProperties", ns);
                    Assert.NotNull(signedProps);
                    
                    var certDigest = doc.SelectSingleNode("//xades:SigningCertificate/xades:Cert/xades:CertDigest", ns);
                    Assert.NotNull(certDigest);
                }

                // Verify validation result
                var result = await OdfSigner.VerifySignaturesAsync(package, new OdfSigningOptions { AllowUntrustedRoot = true });
                Assert.True(result.IsValid, result.Signatures.FirstOrDefault()?.ErrorMessage);
                Assert.Single(result.Signatures);
                
                var sig = result.Signatures[0];
                Assert.True(sig.IsSignatureValid);
                Assert.True(sig.IsCertificateValid);
                Assert.True(sig.IsChainValid);
            }
        }

        [Fact]
        public async Task TestXadesTSigningAndVerification()
        {
            using var signerCert = GenerateSelfSignedCertificate("XadesTTestSigner", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(5));
            using var tsaCert = GenerateSelfSignedCertificate("MockTSA", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(5));
            
            // Setup Mock HTTP handler to intercept TSA requests and return valid timestamp responses
            var mockHandler = new MockHttpMessageHandler(request =>
            {
                if (request.RequestUri?.AbsoluteUri == "http://mocktsa.com/tsa")
                {
                    byte[] reqBytes = request.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                    
                    // Extract hash from TSA request
                    var root = ParseDer(reqBytes);
                    var imprint = root.Children[1];
                    byte[] hash = imprint.Children[1].Value;
                    
                    // Generate Mock TSTInfo DER bytes
                    byte[] tstInfoBytes = CreateMockTstInfoBytes(hash);
                    
                    // Construct CMS signed message
                    var contentInfo = new ContentInfo(new Oid("1.2.840.113549.1.9.16.1.4"), tstInfoBytes);
                    var signedCms = new SignedCms(contentInfo, false);
                    var signer = new CmsSigner(tsaCert);
                    signedCms.ComputeSignature(signer);
                    
                    byte[] tokenBytes = signedCms.Encode();
                    byte[] tsaResponse = CreateTsaResponse(tokenBytes);
                    
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(tsaResponse)
                    };
                }
                return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
            });

            using var httpClient = new HttpClient(mockHandler);
            using var ms = new MemoryStream();
            
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                package.SetMimeType("application/vnd.oasis.opendocument.text");
                package.WriteEntry("content.xml", Encoding.UTF8.GetBytes("<content/>"), "text/xml");
                
                var options = new OdfSigningOptions
                {
                    Level = XadesLevel.T,
                    TsaUrl = "http://mocktsa.com/tsa",
                    HttpClient = httpClient
                };
                
                await OdfSigner.SignAsync(package, signerCert, options);
                package.Save();
            }

            ms.Position = 0;
            using (var package = OdfPackage.Open(ms))
            {
                // Verify XML contains SignatureTimeStamp and EncapsulatedTimeStamp
                using (var sigStream = package.GetEntryStream("META-INF/documentsignatures.xml"))
                {
                    var doc = new XmlDocument();
                    doc.Load(sigStream);
                    
                    var ns = new XmlNamespaceManager(doc.NameTable);
                    ns.AddNamespace("xades", "http://uri.etsi.org/01903/v1.3.2#");
                    
                    var timestamp = doc.SelectSingleNode("//xades:SignatureTimeStamp", ns);
                    Assert.NotNull(timestamp);
                    
                    var encap = doc.SelectSingleNode("//xades:SignatureTimeStamp/xades:EncapsulatedTimeStamp", ns);
                    Assert.NotNull(encap);
                }

                // Verify signature and timestamp validate successfully
                var options = new OdfSigningOptions { HttpClient = httpClient, AllowUntrustedRoot = true, AllowUntrustedTimestamp = true };
                var result = await OdfSigner.VerifySignaturesAsync(package, options);
                
                Assert.True(result.IsValid, result.Signatures.FirstOrDefault()?.ErrorMessage);
                Assert.Single(result.Signatures);
                Assert.True(result.Signatures[0].IsTimestampValid);
            }
        }

        [Fact]
        public async Task TestXadesASigningAndVerificationWithRevocation()
        {
            // Build CDP DER bytes for the test URL "http://mockcrl.com/revocation.crl"
            var cdpBytes = new byte[] {
                0x30, 0x29, 0x30, 0x27, 0xa0, 0x25, 0xa0, 0x23, 0x86, 0x21,
                (byte)'h', (byte)'t', (byte)'t', (byte)'p', (byte)':', (byte)'/', (byte)'/', 
                (byte)'m', (byte)'o', (byte)'c', (byte)'k', (byte)'c', (byte)'r', (byte)'l', (byte)'.', (byte)'c', (byte)'o', (byte)'m', 
                (byte)'/', (byte)'r', (byte)'e', (byte)'v', (byte)'o', (byte)'c', (byte)'a', (byte)'t', (byte)'i', (byte)'o', (byte)'n', 
                (byte)'.', (byte)'c', (byte)'r', (byte)'l'
            };

            var (rootCert, leafCert) = GenerateCertificateChain("XadesARootCA", "XadesATestSigner", cdpBytes);
            using var signerCA = rootCert;
            using var signerCert = leafCert;
            using var tsaCert = GenerateSelfSignedCertificate("MockTSA", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(5));

            // Generate mock CRL containing no revoked serials first, issued by CA (signerCA)
            byte[] cleanCrlBytes = CreateMockCrlBytes(signerCA, new List<string>());
            
            // Generate mock CRL containing the leafCert's serial to mock a revoked certificate, issued by CA (signerCA)
            byte[] revokedCrlBytes = CreateMockCrlBytes(signerCA, new List<string> { signerCert.SerialNumber });

            bool triggerRevocation = false;

            var mockHandler = new MockHttpMessageHandler(request =>
            {
                string url = request.RequestUri?.AbsoluteUri ?? "";
                if (url == "http://mocktsa.com/tsa")
                {
                    byte[] reqBytes = request.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                    var root = ParseDer(reqBytes);
                    byte[] hash = root.Children[1].Children[1].Value;
                    byte[] tstInfoBytes = CreateMockTstInfoBytes(hash);
                    
                    var contentInfo = new ContentInfo(new Oid("1.2.840.113549.1.9.16.1.4"), tstInfoBytes);
                    var signedCms = new SignedCms(contentInfo, false);
                    signedCms.ComputeSignature(new CmsSigner(tsaCert));
                    
                    byte[] tsaResponse = CreateTsaResponse(signedCms.Encode());
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(tsaResponse) };
                }
                else if (url == "http://mockcrl.com/revocation.crl")
                {
                    byte[] crl = triggerRevocation ? revokedCrlBytes : cleanCrlBytes;
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(crl) };
                }
                return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
            });

            using var httpClient = new HttpClient(mockHandler);
            using var ms = new MemoryStream();
            
            // 1. Sign package as XAdES-A (which downloads and embeds the CRL)
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                package.SetMimeType("application/vnd.oasis.opendocument.text");
                package.WriteEntry("content.xml", Encoding.UTF8.GetBytes("<content/>"), "text/xml");
                
                var options = new OdfSigningOptions
                {
                    Level = XadesLevel.A,
                    TsaUrl = "http://mocktsa.com/tsa",
                    HttpClient = httpClient,
                    CheckRevocation = true,
                    AllowUntrustedRoot = true
                };
                options.ExtraCertificates.Add(signerCA);

                await OdfSigner.SignAsync(package, signerCert, options);
                package.Save();
            }

            // 2. Open package and verify validation
            ms.Position = 0;
            using (var package = OdfPackage.Open(ms))
            {
                // Verify XML contains EncapsulatedCertificate and EncapsulatedCRLValue
                using (var sigStream = package.GetEntryStream("META-INF/documentsignatures.xml"))
                {
                    var doc = new XmlDocument();
                    doc.Load(sigStream);
                    
                    var ns = new XmlNamespaceManager(doc.NameTable);
                    ns.AddNamespace("xades", "http://uri.etsi.org/01903/v1.3.2#");
                    
                    var certValues = doc.SelectSingleNode("//xades:CertificateValues/xades:EncapsulatedCertificate", ns);
                    Assert.NotNull(certValues);
                    
                    var revValues = doc.SelectSingleNode("//xades:RevocationValues/xades:CRLValues/xades:EncapsulatedCRLValue", ns);
                    Assert.NotNull(revValues);
                }

                // Verify package validation passes when certificate is clean
                var options = new OdfSigningOptions { HttpClient = httpClient, CheckRevocation = true, AllowUntrustedRoot = true, AllowUntrustedTimestamp = true };
                options.ExtraCertificates.Add(signerCA);
                var result = await OdfSigner.VerifySignaturesAsync(package, options);
                Assert.True(result.IsValid, result.Signatures.FirstOrDefault()?.ErrorMessage);
                Assert.True(result.Signatures[0].IsRevocationValid);

                // 3. Verify revocation check fails offline when the embedded CRL lists the cert as revoked
                // We recreate package with the revoked CRL embedded
                using var msRevoked = new MemoryStream();
                triggerRevocation = true;
                using (var packageRevoked = OdfPackage.Create(msRevoked, leaveOpen: true))
                {
                    packageRevoked.SetMimeType("application/vnd.oasis.opendocument.text");
                    packageRevoked.WriteEntry("content.xml", Encoding.UTF8.GetBytes("<content/>"), "text/xml");
                    
                    var optionsRevoked = new OdfSigningOptions
                    {
                        Level = XadesLevel.A,
                        TsaUrl = "http://mocktsa.com/tsa",
                        HttpClient = httpClient,
                        CheckRevocation = true,
                        AllowUntrustedRoot = true
                    };
                    optionsRevoked.ExtraCertificates.Add(signerCA);
                    await OdfSigner.SignAsync(packageRevoked, signerCert, optionsRevoked);
                    packageRevoked.Save();
                }

                msRevoked.Position = 0;
                using (var packageRevoked = OdfPackage.Open(msRevoked))
                {
                    var resultRevoked = await OdfSigner.VerifySignaturesAsync(packageRevoked, options);
                    Assert.False(resultRevoked.IsValid);
                    Assert.False(resultRevoked.Signatures[0].IsRevocationValid);
                    Assert.Contains("revoked", resultRevoked.Signatures[0].ErrorMessage ?? "", StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        [Fact]
        public void TestCrlUrlExtraction()
        {
            var cdpBytes = new byte[] {
                0x30, 0x29, 0x30, 0x27, 0xa0, 0x25, 0xa0, 0x23, 0x86, 0x21,
                (byte)'h', (byte)'t', (byte)'t', (byte)'p', (byte)':', (byte)'/', (byte)'/', 
                (byte)'m', (byte)'o', (byte)'c', (byte)'k', (byte)'c', (byte)'r', (byte)'l', (byte)'.', (byte)'c', (byte)'o', (byte)'m', 
                (byte)'/', (byte)'r', (byte)'e', (byte)'v', (byte)'o', (byte)'c', (byte)'a', (byte)'t', (byte)'i', (byte)'o', (byte)'n', 
                (byte)'.', (byte)'c', (byte)'r', (byte)'l'
            };
            var (rootCert, leafCert) = GenerateCertificateChain("XadesARootCA", "XadesATestSigner", cdpBytes);
            
            var ext = leafCert.Extensions["2.5.29.31"];
            Assert.NotNull(ext);
            
            var urls = new List<string>();
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
                    if (!urls.Contains(url)) urls.Add(url);
                    idx = end;
                }
            }
            
            Assert.Single(urls);
            Assert.Equal("http://mockcrl.com/revocation.crl", urls[0]);
        }

        [Fact]
        public void TestCrlParsingAndVerification()
        {
            var (rootCert, leafCert) = GenerateCertificateChain("XadesARootCA", "XadesATestSigner");
            byte[] revokedCrlBytes = CreateMockCrlBytes(rootCert, new List<string> { leafCert.SerialNumber });

            var signerType = typeof(OdfSigner);
            
            var getTbsNodeMethod = signerType.GetMethod("GetTbsNode", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(getTbsNodeMethod);
            var tbsNode = getTbsNodeMethod.Invoke(null, new object?[] { revokedCrlBytes });
            Assert.NotNull(tbsNode);

            var getCrlIssuerDerMethod = signerType.GetMethod("GetCrlIssuerDer", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(getCrlIssuerDerMethod);
            var crlIssuer = (byte[]?)getCrlIssuerDerMethod.Invoke(null, new object?[] { tbsNode });
            Assert.NotNull(crlIssuer);
            
            var structuralEqualMethod = signerType.GetMethod("StructuralEqual", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(structuralEqualMethod);
            var isIssuerEqualVal = structuralEqualMethod.Invoke(null, new object?[] { crlIssuer, leafCert.IssuerName.RawData });
            Assert.NotNull(isIssuerEqualVal);
            bool isIssuerEqual = (bool)isIssuerEqualVal;
            Assert.True(isIssuerEqual);

            var getRevokedSerialNumbersMethod = signerType.GetMethod("GetRevokedSerialNumbers", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(getRevokedSerialNumbersMethod);
            var revoked = (HashSet<string>?)getRevokedSerialNumbersMethod.Invoke(null, new object?[] { revokedCrlBytes });
            Assert.NotNull(revoked);
            
            var normalizeHexSerialMethod = signerType.GetMethod("NormalizeHexSerial", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(normalizeHexSerialMethod);
            var normalizedSerial = (string?)normalizeHexSerialMethod.Invoke(null, new object?[] { leafCert.SerialNumber });
            Assert.NotNull(normalizedSerial);
                
            Assert.Contains(normalizedSerial!, revoked);
        }

        [Fact]
        public async Task TestMultipleSignaturesCoSigningDifferentCerts()
        {
            using var certA = GenerateSelfSignedCertificate("SignerA", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(5));
            using var certB = GenerateSelfSignedCertificate("SignerB", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(5));
            using var ms = new MemoryStream();

            // 1. Create a package and sign with Cert A
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                package.SetMimeType("application/vnd.oasis.opendocument.text");
                package.WriteEntry("content.xml", Encoding.UTF8.GetBytes("<content/>"), "text/xml");
                
                await OdfSigner.SignAsync(package, certA, new OdfSigningOptions { Level = XadesLevel.BES });
                package.Save();
            }

            // 2. Open package and co-sign with Cert B
            ms.Position = 0;
            using (var package = OdfPackage.Open(ms, leaveOpen: true))
            {
                await OdfSigner.SignAsync(package, certB, new OdfSigningOptions { Level = XadesLevel.BES });
                package.Save();
            }

            // 3. Open package and verify signatures
            ms.Position = 0;
            using (var package = OdfPackage.Open(ms))
            {
                var result = await OdfSigner.VerifySignaturesAsync(package, new OdfSigningOptions { AllowUntrustedRoot = true });
                
                Assert.Equal(2, result.Signatures.Count);
                
                var sigA = result.Signatures[0];
                var sigB = result.Signatures[1];
                
                Assert.True(sigA.IsSignatureValid, "Signature A should be valid");
                
                Assert.True(sigB.IsSignatureValid, "Signature B should be valid");
                Assert.True(result.IsValid, result.Signatures.FirstOrDefault(s => !s.IsSignatureValid)?.ErrorMessage);
            }
        }

        [Fact]
        public async Task TestSerialZeroRevocationBypass()
        {
            // Build CDP DER bytes for the test URL "http://mockcrl.com/revocation.crl"
            var cdpBytes = new byte[] {
                0x30, 0x29, 0x30, 0x27, 0xa0, 0x25, 0xa0, 0x23, 0x86, 0x21,
                (byte)'h', (byte)'t', (byte)'t', (byte)'p', (byte)':', (byte)'/', (byte)'/', 
                (byte)'m', (byte)'o', (byte)'c', (byte)'k', (byte)'c', (byte)'r', (byte)'l', (byte)'.', (byte)'c', (byte)'o', (byte)'m', 
                (byte)'/', (byte)'r', (byte)'e', (byte)'v', (byte)'o', (byte)'c', (byte)'a', (byte)'t', (byte)'i', (byte)'o', (byte)'n', 
                (byte)'.', (byte)'c', (byte)'r', (byte)'l'
            };

            byte[] serialZero = new byte[] { 0 };
            var (rootCert, leafCert) = GenerateCertificateChainWithSerial("XadesARootCA", "XadesATestSigner", serialZero, cdpBytes);
            using var signerCA = rootCert;
            using var signerCert = leafCert;
            using var tsaCert = GenerateSelfSignedCertificate("MockTSA", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(5));

            byte[] revokedCrlBytes = CreateMockCrlBytes(signerCA, new List<string> { "00" });

            var mockHandler = new MockHttpMessageHandler(request =>
            {
                string url = request.RequestUri?.AbsoluteUri ?? "";
                if (url == "http://mocktsa.com/tsa")
                {
                    byte[] reqBytes = request.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                    var root = ParseDer(reqBytes);
                    byte[] hash = root.Children[1].Children[1].Value;
                    byte[] tstInfoBytes = CreateMockTstInfoBytes(hash);
                    
                    var contentInfo = new ContentInfo(new Oid("1.2.840.113549.1.9.16.1.4"), tstInfoBytes);
                    var signedCms = new SignedCms(contentInfo, false);
                    signedCms.ComputeSignature(new CmsSigner(tsaCert));
                    
                    byte[] tsaResponse = CreateTsaResponse(signedCms.Encode());
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(tsaResponse) };
                }
                else if (url == "http://mockcrl.com/revocation.crl")
                {
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(revokedCrlBytes) };
                }
                return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
            });

            using var httpClient = new HttpClient(mockHandler);
            using var ms = new MemoryStream();
            
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                package.SetMimeType("application/vnd.oasis.opendocument.text");
                package.WriteEntry("content.xml", Encoding.UTF8.GetBytes("<content/>"), "text/xml");
                
                var options = new OdfSigningOptions
                {
                    Level = XadesLevel.A,
                    TsaUrl = "http://mocktsa.com/tsa",
                    HttpClient = httpClient,
                    CheckRevocation = true,
                    AllowUntrustedRoot = true
                };
                
                await OdfSigner.SignAsync(package, signerCert, options);
                package.Save();
            }

            ms.Position = 0;
            using (var package = OdfPackage.Open(ms))
            {
                var options = new OdfSigningOptions { HttpClient = httpClient, CheckRevocation = true, AllowUntrustedRoot = true };
                var result = await OdfSigner.VerifySignaturesAsync(package, options);
                
                Assert.False(result.IsValid);
                Assert.False(result.Signatures[0].IsRevocationValid);
            }
        }

        [Fact]
        public async Task TestAcceptanceOfUntrustedTSACertificate()
        {
            using var signerCert = GenerateSelfSignedCertificate("XadesTTestSigner", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(5));
            using var tsaCert = GenerateSelfSignedCertificate("ExpiredOrUntrustedTSA", DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow.AddDays(-1));
            
            var mockHandler = new MockHttpMessageHandler(request =>
            {
                if (request.RequestUri?.AbsoluteUri == "http://mocktsa.com/tsa")
                {
                    byte[] reqBytes = request.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                    var root = ParseDer(reqBytes);
                    byte[] hash = root.Children[1].Children[1].Value;
                    byte[] tstInfoBytes = CreateMockTstInfoBytes(hash);
                    
                    var contentInfo = new ContentInfo(new Oid("1.2.840.113549.1.9.16.1.4"), tstInfoBytes);
                    var signedCms = new SignedCms(contentInfo, false);
                    signedCms.ComputeSignature(new CmsSigner(tsaCert));
                    
                    byte[] tsaResponse = CreateTsaResponse(signedCms.Encode());
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(tsaResponse) };
                }
                return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
            });

            using var httpClient = new HttpClient(mockHandler);
            using var ms = new MemoryStream();
            
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                package.SetMimeType("application/vnd.oasis.opendocument.text");
                package.WriteEntry("content.xml", Encoding.UTF8.GetBytes("<content/>"), "text/xml");
                
                var options = new OdfSigningOptions
                {
                    Level = XadesLevel.T,
                    TsaUrl = "http://mocktsa.com/tsa",
                    HttpClient = httpClient
                };
                
                await OdfSigner.SignAsync(package, signerCert, options);
                package.Save();
            }

            ms.Position = 0;
            using (var package = OdfPackage.Open(ms))
            {
                // Verify that it fails by default (AllowUntrustedTimestamp = false)
                var optionsDefault = new OdfSigningOptions { HttpClient = httpClient, AllowUntrustedRoot = true, AllowUntrustedTimestamp = false };
                var resultDefault = await OdfSigner.VerifySignaturesAsync(package, optionsDefault);
                Assert.False(resultDefault.IsValid);
                Assert.False(resultDefault.Signatures[0].IsTimestampValid);

                // Verify that it only passes when AllowUntrustedTimestamp = true
                var optionsAllowed = new OdfSigningOptions { HttpClient = httpClient, AllowUntrustedRoot = true, AllowUntrustedTimestamp = true };
                var resultAllowed = await OdfSigner.VerifySignaturesAsync(package, optionsAllowed);
                Assert.True(resultAllowed.IsValid, resultAllowed.Signatures.FirstOrDefault()?.ErrorMessage);
                Assert.True(resultAllowed.Signatures[0].IsTimestampValid);
            }
        }

        [Fact]
        public async Task TestExpiredCertificateRejection()
        {
            using var expiredCert = GenerateSelfSignedCertificate("ExpiredSigner", DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow.AddDays(-1));
            using var ms = new MemoryStream();
            
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                package.SetMimeType("application/vnd.oasis.opendocument.text");
                package.WriteEntry("content.xml", Encoding.UTF8.GetBytes("<content/>"), "text/xml");
                
                await OdfSigner.SignAsync(package, expiredCert, new OdfSigningOptions { Level = XadesLevel.None });
                package.Save();
            }

            ms.Position = 0;
            using (var package = OdfPackage.Open(ms))
            {
                var result = await OdfSigner.VerifySignaturesAsync(package);
                Assert.False(result.IsValid, "Verification should fail for expired certificate");
                Assert.False(result.Signatures[0].IsCertificateValid);
                Assert.Contains("expired", result.Signatures[0].ErrorMessage ?? "", StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public async Task TestCoSigningConcurrentLoad()
        {
            int concurrency = 10;
            var tasks = new Task[concurrency];
            
            for (int i = 0; i < concurrency; i++)
            {
                int localIndex = i;
                tasks[i] = Task.Run(async () =>
                {
                    using var cert = GenerateSelfSignedCertificate($"LoadSigner_{localIndex}", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(5));
                    using var ms = new MemoryStream();
                    
                    using (var package = OdfPackage.Create(ms, leaveOpen: true))
                    {
                        package.SetMimeType("application/vnd.oasis.opendocument.text");
                        package.WriteEntry("content.xml", Encoding.UTF8.GetBytes($"<content id='{localIndex}'/>"), "text/xml");
                        
                        await OdfSigner.SignAsync(package, cert, new OdfSigningOptions { Level = XadesLevel.BES });
                        package.Save();
                    }
                    
                    ms.Position = 0;
                    using (var package = OdfPackage.Open(ms))
                    {
                        var result = await OdfSigner.VerifySignaturesAsync(package, new OdfSigningOptions { AllowUntrustedRoot = true });
                        Assert.True(result.IsValid, $"Signature verification failed for index {localIndex}: {result.Signatures.FirstOrDefault()?.ErrorMessage}");
                    }
                }, TestContext.Current.CancellationToken);
            }
            
            await Task.WhenAll(tasks);
        }

        private static (X509Certificate2 Root, X509Certificate2 Leaf) GenerateCertificateChainWithSerial(string rootName, string leafName, byte[] serial, byte[]? cdpBytes = null)
        {
            using var rootRsa = RSA.Create(2048);
            var rootRequest = new CertificateRequest($"CN={rootName}", rootRsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 1, true));
            rootRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
            
            var rootCert = rootRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(10));
            
            using var leafRsa = RSA.Create(2048);
            var leafRequest = new CertificateRequest($"CN={leafName}", leafRsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            leafRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
            leafRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation, false));

            if (cdpBytes != null)
            {
                leafRequest.CertificateExtensions.Add(new X509Extension("2.5.29.31", cdpBytes, false));
            }
            
            var leafCert = leafRequest.Create(rootCert, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(5), serial);
            var leafWithKey = leafCert.CopyWithPrivateKey(leafRsa);
            
            var rootPfx = rootCert.Export(X509ContentType.Pfx);
            var rootImported = LoadCertificateFromPfx(rootPfx);
            
            var leafPfx = leafWithKey.Export(X509ContentType.Pfx);
            var leafImported = LoadCertificateFromPfx(leafPfx);

            return (rootImported, leafImported);
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

        #region Helpers for Generating Mock DER, CA/Leaf Chains, TSA and CRL structures

        private static X509Certificate2 GenerateSelfSignedCertificate(string subjectName, DateTimeOffset notBefore, DateTimeOffset notAfter, byte[]? cdpBytes = null)
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest($"CN={subjectName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation, false));

            if (cdpBytes != null)
            {
                request.CertificateExtensions.Add(new X509Extension("2.5.29.31", cdpBytes, false));
            }

            var cert = request.CreateSelfSigned(notBefore, notAfter);
            
            var pfx = cert.Export(X509ContentType.Pfx);
            return LoadCertificateFromPfx(pfx);
        }

        private static (X509Certificate2 Root, X509Certificate2 Leaf) GenerateCertificateChain(string rootName, string leafName, byte[]? cdpBytes = null)
        {
            using var rootRsa = RSA.Create(2048);
            var rootRequest = new CertificateRequest($"CN={rootName}", rootRsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 1, true));
            rootRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
            
            var rootCert = rootRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(10));
            
            using var leafRsa = RSA.Create(2048);
            var leafRequest = new CertificateRequest($"CN={leafName}", leafRsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            leafRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
            leafRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation, false));

            if (cdpBytes != null)
            {
                leafRequest.CertificateExtensions.Add(new X509Extension("2.5.29.31", cdpBytes, false));
            }

            byte[] serial = new byte[8];
            RandomNumberGenerator.Fill(serial);
            serial[0] &= 0x7F; // Ensure positive integer
            
            var leafCert = leafRequest.Create(rootCert, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(5), serial);
            var leafWithKey = leafCert.CopyWithPrivateKey(leafRsa);
            
            var rootPfx = rootCert.Export(X509ContentType.Pfx);
            var rootImported = LoadCertificateFromPfx(rootPfx);
            
            var leafPfx = leafWithKey.Export(X509ContentType.Pfx);
            var leafImported = LoadCertificateFromPfx(leafPfx);

            return (rootImported, leafImported);
        }

        private static byte[] CreateMockTstInfoBytes(byte[] hash)
        {
            byte[] tstInfo = new byte[82];
            tstInfo[0] = 0x30;
            tstInfo[1] = 80;
            // Version
            tstInfo[2] = 0x02;
            tstInfo[3] = 0x01;
            tstInfo[4] = 0x01;
            // Policy
            tstInfo[5] = 0x06;
            tstInfo[6] = 0x04;
            tstInfo[7] = 0x2a;
            tstInfo[8] = 0x03;
            tstInfo[9] = 0x04;
            tstInfo[10] = 0x05;
            // MessageImprint
            tstInfo[11] = 0x30;
            tstInfo[12] = 49;
            tstInfo[13] = 0x30;
            tstInfo[14] = 13;
            tstInfo[15] = 0x06;
            tstInfo[16] = 0x09;
            byte[] sha256Oid = { 0x60, 0x86, 0x48, 0x01, 0x65, 0x03, 0x04, 0x02, 0x01 };
            Buffer.BlockCopy(sha256Oid, 0, tstInfo, 17, 9);
            tstInfo[26] = 0x05;
            tstInfo[27] = 0x00;
            tstInfo[28] = 0x04;
            tstInfo[29] = 32;
            Buffer.BlockCopy(hash, 0, tstInfo, 30, 32);
            // SerialNumber
            tstInfo[62] = 0x02;
            tstInfo[63] = 0x01;
            tstInfo[64] = 42;
            // GenTime
            tstInfo[65] = 0x18;
            tstInfo[66] = 15;
            byte[] timeBytes = Encoding.ASCII.GetBytes("20260611004412Z");
            Buffer.BlockCopy(timeBytes, 0, tstInfo, 67, 15);

            return tstInfo;
        }

        private static byte[] CreateTsaResponse(byte[] timeStampTokenBytes)
        {
            byte[] lenBytes = EncodeDerLength(5 + timeStampTokenBytes.Length);
            byte[] response = new byte[1 + lenBytes.Length + 5 + timeStampTokenBytes.Length];
            response[0] = 0x30;
            Buffer.BlockCopy(lenBytes, 0, response, 1, lenBytes.Length);
            int offset = 1 + lenBytes.Length;
            
            response[offset++] = 0x30;
            response[offset++] = 0x03;
            response[offset++] = 0x02;
            response[offset++] = 0x01;
            response[offset++] = 0x00;
            
            Buffer.BlockCopy(timeStampTokenBytes, 0, response, offset, timeStampTokenBytes.Length);
            return response;
        }

        private static byte[] CreateMockCrlBytes(X509Certificate2 issuerCert, List<string> revokedSerials, bool useInvalidSignature = false)
        {
            byte[] sigAlg = { 0x30, 0x0d, 0x06, 0x09, 0x2a, 0x86, 0x48, 0x86, 0xf7, 0x0d, 0x01, 0x01, 0x0b, 0x05, 0x00 };
            byte[] issuerName = issuerCert.IssuerName.RawData;
            byte[] thisUpdate = { 0x17, 0x0d, (byte)'2', (byte)'6', (byte)'0', (byte)'6', (byte)'1', (byte)'1', (byte)'0', (byte)'0', (byte)'0', (byte)'0', (byte)'0', (byte)'0', (byte)'Z' };
            
            var revokedItemsList = new List<byte[]>();
            foreach (var serialHex in revokedSerials)
            {
                byte[] serialBytes = ParseHex(serialHex);
                byte[] integerBytes = BuildDerInteger(serialBytes);
                byte[] dateBytes = { 0x17, 0x0d, (byte)'2', (byte)'6', (byte)'0', (byte)'6', (byte)'1', (byte)'1', (byte)'0', (byte)'0', (byte)'0', (byte)'0', (byte)'0', (byte)'0', (byte)'Z' };
                
                byte[] itemInner = new byte[integerBytes.Length + dateBytes.Length];
                Buffer.BlockCopy(integerBytes, 0, itemInner, 0, integerBytes.Length);
                Buffer.BlockCopy(dateBytes, 0, itemInner, integerBytes.Length, dateBytes.Length);
                
                byte[] itemSeq = BuildDerSequence(itemInner);
                revokedItemsList.Add(itemSeq);
            }
            
            byte[] revokedSeq;
            if (revokedItemsList.Count > 0)
            {
                int totalRevokedLen = revokedItemsList.Sum(x => x.Length);
                byte[] revokedInner = new byte[totalRevokedLen];
                int offset = 0;
                foreach (var item in revokedItemsList)
                {
                    Buffer.BlockCopy(item, 0, revokedInner, offset, item.Length);
                    offset += item.Length;
                }
                revokedSeq = BuildDerSequence(revokedInner);
            }
            else
            {
                revokedSeq = Array.Empty<byte>();
            }
            
            int tbsLen = sigAlg.Length + issuerName.Length + thisUpdate.Length + revokedSeq.Length;
            byte[] tbsInner = new byte[tbsLen];
            int tbsOffset = 0;
            Buffer.BlockCopy(sigAlg, 0, tbsInner, tbsOffset, sigAlg.Length);
            tbsOffset += sigAlg.Length;
            Buffer.BlockCopy(issuerName, 0, tbsInner, tbsOffset, issuerName.Length);
            tbsOffset += issuerName.Length;
            Buffer.BlockCopy(thisUpdate, 0, tbsInner, tbsOffset, thisUpdate.Length);
            tbsOffset += thisUpdate.Length;
            if (revokedSeq.Length > 0)
            {
                Buffer.BlockCopy(revokedSeq, 0, tbsInner, tbsOffset, revokedSeq.Length);
            }
            
            byte[] tbsCertList = BuildDerSequence(tbsInner);
            
            byte[] sigValueBytes;
            if (useInvalidSignature)
            {
                sigValueBytes = new byte[] { 0x03, 0x03, 0x00, 0x01, 0x02 };
            }
            else
            {
                using var rsa = issuerCert.GetRSAPrivateKey();
                if (rsa == null)
                {
                    throw new InvalidOperationException("Issuer certificate does not have RSA private key.");
                }
                byte[] signature = rsa.SignData(tbsCertList, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                
                byte[] bitStringValue = new byte[signature.Length + 1];
                bitStringValue[0] = 0x00;
                Buffer.BlockCopy(signature, 0, bitStringValue, 1, signature.Length);
                
                byte[] lenBytes = EncodeDerLength(bitStringValue.Length);
                sigValueBytes = new byte[1 + lenBytes.Length + bitStringValue.Length];
                sigValueBytes[0] = 0x03;
                Buffer.BlockCopy(lenBytes, 0, sigValueBytes, 1, lenBytes.Length);
                Buffer.BlockCopy(bitStringValue, 0, sigValueBytes, 1 + lenBytes.Length, bitStringValue.Length);
            }
            
            int outerLen = tbsCertList.Length + sigAlg.Length + sigValueBytes.Length;
            byte[] outerInner = new byte[outerLen];
            int outerOffset = 0;
            Buffer.BlockCopy(tbsCertList, 0, outerInner, outerOffset, tbsCertList.Length);
            outerOffset += tbsCertList.Length;
            Buffer.BlockCopy(sigAlg, 0, outerInner, outerOffset, sigAlg.Length);
            outerOffset += sigAlg.Length;
            Buffer.BlockCopy(sigValueBytes, 0, outerInner, outerOffset, sigValueBytes.Length);
            
            return BuildDerSequence(outerInner);
        }

        private static byte[] BuildDerSequence(byte[] inner)
        {
            byte[] lenBytes = EncodeDerLength(inner.Length);
            byte[] seq = new byte[1 + lenBytes.Length + inner.Length];
            seq[0] = 0x30;
            Buffer.BlockCopy(lenBytes, 0, seq, 1, lenBytes.Length);
            Buffer.BlockCopy(inner, 0, seq, 1 + lenBytes.Length, inner.Length);
            return seq;
        }

        private static byte[] BuildDerInteger(byte[] val)
        {
            byte[] lenBytes = EncodeDerLength(val.Length);
            byte[] integer = new byte[1 + lenBytes.Length + val.Length];
            integer[0] = 0x02;
            Buffer.BlockCopy(lenBytes, 0, integer, 1, lenBytes.Length);
            Buffer.BlockCopy(val, 0, integer, 1 + lenBytes.Length, val.Length);
            return integer;
        }

        private static byte[] ParseHex(string hex)
        {
            hex = hex.Replace("-", "");
            if (hex.Length % 2 != 0) hex = "0" + hex;
            byte[] raw = new byte[hex.Length / 2];
            for (int i = 0; i < raw.Length; i++)
            {
                raw[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return raw;
        }

        private static byte[] EncodeDerLength(int len)
        {
            if (len < 128)
            {
                return new byte[] { (byte)len };
            }
            else if (len <= 255)
            {
                return new byte[] { 0x81, (byte)len };
            }
            else
            {
                return new byte[] { 0x82, (byte)(len >> 8), (byte)(len & 0xFF) };
            }
        }

        private static DerNode ParseDer(byte[] bytes)
        {
            int offset = 0;
            return ReadNode(bytes, ref offset);
        }

        private static DerNode ReadNode(byte[] bytes, ref int offset)
        {
            int start = offset;
            byte tag = bytes[offset++];
            int length = ReadLength(bytes, ref offset);

            byte[] val = new byte[length];
            Buffer.BlockCopy(bytes, offset, val, 0, length);
            
            var node = new DerNode(tag, val);
            offset += length;

            if ((tag & 0x20) != 0)
            {
                int childOffset = 0;
                try
                {
                    while (childOffset < val.Length)
                    {
                        node.Children.Add(ReadNode(val, ref childOffset));
                    }
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
            int len = 0;
            for (int i = 0; i < numBytes; i++)
            {
                len = (len << 8) | bytes[offset++];
            }
            return len;
        }

        #endregion

        [Fact]
        public async Task TestMultiSignatureCoSigningUnderLoad()
        {
            const int CoSignerCount = 50;
            var certs = new List<X509Certificate2>();
            for (int i = 0; i < CoSignerCount; i++)
            {
                certs.Add(GenerateSelfSignedCertificate($"CoSigner_{i}", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(5)));
            }

            using var ms = new MemoryStream();
            
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                package.SetMimeType("application/vnd.oasis.opendocument.text");
                package.WriteEntry("content.xml", Encoding.UTF8.GetBytes("<content/>"), "text/xml");
                package.WriteEntry("styles.xml", Encoding.UTF8.GetBytes("<styles/>"), "text/xml");
                package.Save();
            }

            for (int i = 0; i < CoSignerCount; i++)
            {
                ms.Position = 0;
                using var package = OdfPackage.Open(ms, leaveOpen: true);
                await OdfSigner.SignAsync(package, certs[i], new OdfSigningOptions { Level = XadesLevel.None });
                package.Save();
            }

            ms.Position = 0;
            using (var package = OdfPackage.Open(ms))
            {
                var result = await OdfSigner.VerifySignaturesAsync(package, new OdfSigningOptions { AllowUntrustedRoot = true });
                Assert.True(result.IsValid, result.Signatures.FirstOrDefault()?.ErrorMessage);
                Assert.Equal(CoSignerCount, result.Signatures.Count);
                for (int i = 0; i < CoSignerCount; i++)
                {
                    var sig = result.Signatures[i];
                    Assert.True(sig.IsSignatureValid);
                    Assert.True(sig.IsCertificateValid);
                    Assert.True(sig.IsChainValid);
                    Assert.NotNull(sig.Certificate);
                    Assert.Equal($"CN=CoSigner_{i}", sig.Certificate.Subject);
                }
            }

            foreach (var cert in certs)
            {
                cert.Dispose();
            }
        }

        [Fact]
        public async Task TestConcurrentMultiSignatureCoSigning()
        {
            const int CoSignerCount = 10;
            var certs = new List<X509Certificate2>();
            for (int i = 0; i < CoSignerCount; i++)
            {
                certs.Add(GenerateSelfSignedCertificate($"ConcurrentCoSigner_{i}", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(5)));
            }

            using var ms = new MemoryStream();
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                package.SetMimeType("application/vnd.oasis.opendocument.text");
                package.WriteEntry("content.xml", Encoding.UTF8.GetBytes("<content/>"), "text/xml");
                package.Save();
            }

            var tasks = certs.Select(async cert =>
            {
                OdfPackage pkg;
                lock (ms)
                {
                    ms.Position = 0;
                    pkg = OdfPackage.Open(ms, leaveOpen: true);
                }

                await OdfSigner.SignAsync(pkg, cert, new OdfSigningOptions { Level = XadesLevel.None });
                
                lock (ms)
                {
                    pkg.Save();
                    pkg.Dispose();
                }
            }).ToArray();

            await Task.WhenAll(tasks);

            ms.Position = 0;
            using (var package = OdfPackage.Open(ms))
            {
                var result = await OdfSigner.VerifySignaturesAsync(package, new OdfSigningOptions { AllowUntrustedRoot = true });
                Assert.True(result.IsValid, result.Signatures.FirstOrDefault()?.ErrorMessage);
                Assert.Equal(CoSignerCount, result.Signatures.Count);
                foreach (var sig in result.Signatures)
                {
                    Assert.True(sig.IsSignatureValid);
                }
            }

            foreach (var cert in certs)
            {
                cert.Dispose();
            }
        }

        [Fact]
        public async Task TestVerificationRejectsExpiredCertificate()
        {
            using var cert = GenerateSelfSignedCertificate("ExpiredSigner", DateTimeOffset.UtcNow.AddDays(-5), DateTimeOffset.UtcNow.AddDays(-1));
            using var ms = new MemoryStream();
            
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                package.SetMimeType("application/vnd.oasis.opendocument.text");
                package.WriteEntry("content.xml", Encoding.UTF8.GetBytes("<content/>"), "text/xml");
                await OdfSigner.SignAsync(package, cert, new OdfSigningOptions { Level = XadesLevel.None });
                package.Save();
            }

            ms.Position = 0;
            using (var package = OdfPackage.Open(ms))
            {
                var result = await OdfSigner.VerifySignaturesAsync(package);
                Assert.False(result.IsValid);
                Assert.Single(result.Signatures);
                
                var sig = result.Signatures[0];
                Assert.True(sig.IsSignatureValid);
                Assert.False(sig.IsCertificateValid);
                Assert.Contains("expired", sig.ErrorMessage ?? "", StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public async Task TestVerificationRejectsNotYetValidCertificate()
        {
            using var cert = GenerateSelfSignedCertificate("FutureSigner", DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(5));
            using var ms = new MemoryStream();
            
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                package.SetMimeType("application/vnd.oasis.opendocument.text");
                package.WriteEntry("content.xml", Encoding.UTF8.GetBytes("<content/>"), "text/xml");
                await OdfSigner.SignAsync(package, cert, new OdfSigningOptions { Level = XadesLevel.None });
                package.Save();
            }

            ms.Position = 0;
            using (var package = OdfPackage.Open(ms))
            {
                var result = await OdfSigner.VerifySignaturesAsync(package);
                Assert.False(result.IsValid);
                Assert.Single(result.Signatures);
                
                var sig = result.Signatures[0];
                Assert.True(sig.IsSignatureValid);
                Assert.False(sig.IsCertificateValid);
                Assert.Contains("not yet valid", sig.ErrorMessage ?? "", StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public async Task TestVerificationRejectsTamperedCertificateDigest()
        {
            using var cert = GenerateSelfSignedCertificate("XadesBesTamperedDigest", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(5));
            using var ms = new MemoryStream();
            
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                package.SetMimeType("application/vnd.oasis.opendocument.text");
                package.WriteEntry("content.xml", Encoding.UTF8.GetBytes("<content/>"), "text/xml");
                await OdfSigner.SignAsync(package, cert, new OdfSigningOptions { Level = XadesLevel.BES });
                package.Save();
            }

            ms.Position = 0;
            byte[] tamperedXmlBytes;
            using (var package = OdfPackage.Open(ms, leaveOpen: true))
            {
                using var sigStream = package.GetEntryStream("META-INF/documentsignatures.xml");
                var doc = new XmlDocument();
                doc.Load(sigStream);
                
                var ns = new XmlNamespaceManager(doc.NameTable);
                ns.AddNamespace("ds", OdfNamespaces.Ds);
                ns.AddNamespace("xades", "http://uri.etsi.org/01903/v1.3.2#");
                
                var digestValue = doc.SelectSingleNode("//xades:SigningCertificate/xades:Cert/xades:CertDigest/ds:DigestValue", ns);
                Assert.NotNull(digestValue);
                digestValue.InnerText = Convert.ToBase64String(new byte[32]);
                
                using var outMs = new MemoryStream();
                doc.Save(outMs);
                tamperedXmlBytes = outMs.ToArray();
            }

            using (var package = OdfPackage.Open(ms, leaveOpen: true))
            {
                package.WriteEntry("META-INF/documentsignatures.xml", tamperedXmlBytes, "text/xml");
                package.Save();
            }

            ms.Position = 0;
            using (var package = OdfPackage.Open(ms))
            {
                var result = await OdfSigner.VerifySignaturesAsync(package);
                Assert.False(result.IsValid);
                Assert.Single(result.Signatures);
                
                var sig = result.Signatures[0];
                Assert.False(sig.IsSignatureValid || result.IsValid);
                Assert.Contains("verification failed", sig.ErrorMessage ?? "", StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public async Task TestVerificationRejectsTamperedSignatureValue()
        {
            using var cert = GenerateSelfSignedCertificate("SignatureValueTampered", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(5));
            using var ms = new MemoryStream();
            
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                package.SetMimeType("application/vnd.oasis.opendocument.text");
                package.WriteEntry("content.xml", Encoding.UTF8.GetBytes("<content/>"), "text/xml");
                await OdfSigner.SignAsync(package, cert, new OdfSigningOptions { Level = XadesLevel.None });
                package.Save();
            }

            ms.Position = 0;
            byte[] tamperedXmlBytes;
            using (var package = OdfPackage.Open(ms, leaveOpen: true))
            {
                using var sigStream = package.GetEntryStream("META-INF/documentsignatures.xml");
                var doc = new XmlDocument();
                doc.Load(sigStream);
                
                var ns = new XmlNamespaceManager(doc.NameTable);
                ns.AddNamespace("ds", OdfNamespaces.Ds);
                
                var sigValue = doc.SelectSingleNode("//ds:SignatureValue", ns);
                Assert.NotNull(sigValue);
                
                string origB64 = sigValue.InnerText.Trim();
                char modifiedChar = origB64[0] == 'A' ? 'B' : 'A';
                sigValue.InnerText = modifiedChar + origB64.Substring(1);
                
                using var outMs = new MemoryStream();
                doc.Save(outMs);
                tamperedXmlBytes = outMs.ToArray();
            }

            using (var package = OdfPackage.Open(ms, leaveOpen: true))
            {
                package.WriteEntry("META-INF/documentsignatures.xml", tamperedXmlBytes, "text/xml");
                package.Save();
            }

            ms.Position = 0;
            using (var package = OdfPackage.Open(ms))
            {
                var result = await OdfSigner.VerifySignaturesAsync(package);
                Assert.False(result.IsValid);
                
                var sig = result.Signatures[0];
                Assert.False(sig.IsSignatureValid);
                Assert.Contains("invalid", sig.ErrorMessage ?? "", StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public async Task TestVerificationRejectsTamperedSignatureFile()
        {
            using var cert = GenerateSelfSignedCertificate("FileTampered", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(5));
            using var ms = new MemoryStream();
            
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                package.SetMimeType("application/vnd.oasis.opendocument.text");
                package.WriteEntry("content.xml", Encoding.UTF8.GetBytes("<content/>"), "text/xml");
                await OdfSigner.SignAsync(package, cert, new OdfSigningOptions { Level = XadesLevel.None });
                package.Save();
            }

            ms.Position = 0;
            using (var archive = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Update, leaveOpen: true))
            {
                var entry = archive.GetEntry("content.xml");
                entry?.Delete();
                var newEntry = archive.CreateEntry("content.xml");
                using var writer = new StreamWriter(newEntry.Open());
                writer.Write("<tampered-content/>");
            }

            ms.Position = 0;
            using (var package = OdfPackage.Open(ms))
            {
                var result = await OdfSigner.VerifySignaturesAsync(package);
                Assert.False(result.IsValid);
                
                var sig = result.Signatures[0];
                Assert.False(sig.IsSignatureValid);
                Assert.Contains("verification failed", sig.ErrorMessage ?? "", StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public async Task TestVerificationRejectsTamperedTimestampToken()
        {
            using var signerCert = GenerateSelfSignedCertificate("SignerCert", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(5));
            using var tsaCert = GenerateSelfSignedCertificate("MockTSA", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(5));
            
            var mockHandler = new MockHttpMessageHandler(request =>
            {
                if (request.RequestUri?.AbsoluteUri == "http://mocktsa.com/tsa")
                {
                    byte[] reqBytes = request.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                    var root = ParseDer(reqBytes);
                    byte[] hash = root.Children[1].Children[1].Value;
                    byte[] tstInfoBytes = CreateMockTstInfoBytes(hash);
                    
                    var contentInfo = new ContentInfo(new Oid("1.2.840.113549.1.9.16.1.4"), tstInfoBytes);
                    var signedCms = new SignedCms(contentInfo, false);
                    var signer = new CmsSigner(tsaCert);
                    signedCms.ComputeSignature(signer);
                    
                    byte[] tokenBytes = signedCms.Encode();
                    byte[] tsaResponse = CreateTsaResponse(tokenBytes);
                    
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(tsaResponse) };
                }
                return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
            });

            using var httpClient = new HttpClient(mockHandler);
            using var ms = new MemoryStream();
            
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                package.SetMimeType("application/vnd.oasis.opendocument.text");
                package.WriteEntry("content.xml", Encoding.UTF8.GetBytes("<content/>"), "text/xml");
                
                var options = new OdfSigningOptions
                {
                    Level = XadesLevel.T,
                    TsaUrl = "http://mocktsa.com/tsa",
                    HttpClient = httpClient
                };
                
                await OdfSigner.SignAsync(package, signerCert, options);
                package.Save();
            }

            ms.Position = 0;
            byte[] tamperedXmlBytes;
            using (var package = OdfPackage.Open(ms, leaveOpen: true))
            {
                using var sigStream = package.GetEntryStream("META-INF/documentsignatures.xml");
                var doc = new XmlDocument();
                doc.Load(sigStream);
                
                var ns = new XmlNamespaceManager(doc.NameTable);
                ns.AddNamespace("xades", "http://uri.etsi.org/01903/v1.3.2#");
                
                var encap = doc.SelectSingleNode("//xades:SignatureTimeStamp/xades:EncapsulatedTimeStamp", ns);
                Assert.NotNull(encap);
                
                string origB64 = encap.InnerText.Trim();
                char modifiedChar = origB64[0] == 'A' ? 'B' : 'A';
                encap.InnerText = modifiedChar + origB64.Substring(1);
                
                using var outMs = new MemoryStream();
                doc.Save(outMs);
                tamperedXmlBytes = outMs.ToArray();
            }

            using (var package = OdfPackage.Open(ms, leaveOpen: true))
            {
                package.WriteEntry("META-INF/documentsignatures.xml", tamperedXmlBytes, "text/xml");
                package.Save();
            }

            ms.Position = 0;
            using (var package = OdfPackage.Open(ms))
            {
                var options = new OdfSigningOptions { HttpClient = httpClient, AllowUntrustedRoot = true, AllowUntrustedTimestamp = true };
                var result = await OdfSigner.VerifySignaturesAsync(package, options);
                Assert.False(result.IsValid);
                
                var sig = result.Signatures[0];
                Assert.False(sig.IsTimestampValid);
                Assert.Contains("Timestamp", sig.ErrorMessage ?? "", StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public async Task TestVerificationRejectsMismatchedTimestampImprint()
        {
            using var signerCert = GenerateSelfSignedCertificate("SignerCert", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(5));
            using var tsaCert = GenerateSelfSignedCertificate("MockTSA", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(5));
            
            var mockHandler = new MockHttpMessageHandler(request =>
            {
                if (request.RequestUri?.AbsoluteUri == "http://mocktsa.com/tsa")
                {
                    byte[] reqBytes = request.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                    var root = ParseDer(reqBytes);
                    byte[] hash = root.Children[1].Children[1].Value;
                    byte[] tstInfoBytes = CreateMockTstInfoBytes(hash);
                    
                    var contentInfo = new ContentInfo(new Oid("1.2.840.113549.1.9.16.1.4"), tstInfoBytes);
                    var signedCms = new SignedCms(contentInfo, false);
                    var signer = new CmsSigner(tsaCert);
                    signedCms.ComputeSignature(signer);
                    
                    byte[] tokenBytes = signedCms.Encode();
                    byte[] tsaResponse = CreateTsaResponse(tokenBytes);
                    
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(tsaResponse) };
                }
                return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
            });

            using var httpClient = new HttpClient(mockHandler);
            using var msA = new MemoryStream();
            using var msB = new MemoryStream();
            
            using (var packageA = OdfPackage.Create(msA, leaveOpen: true))
            {
                packageA.SetMimeType("application/vnd.oasis.opendocument.text");
                packageA.WriteEntry("content.xml", Encoding.UTF8.GetBytes("<contentA/>"), "text/xml");
                
                var options = new OdfSigningOptions { Level = XadesLevel.T, TsaUrl = "http://mocktsa.com/tsa", HttpClient = httpClient };
                await OdfSigner.SignAsync(packageA, signerCert, options);
                packageA.Save();
            }

            using (var packageB = OdfPackage.Create(msB, leaveOpen: true))
            {
                packageB.SetMimeType("application/vnd.oasis.opendocument.text");
                packageB.WriteEntry("content.xml", Encoding.UTF8.GetBytes("<contentB/>"), "text/xml");
                
                var options = new OdfSigningOptions { Level = XadesLevel.T, TsaUrl = "http://mocktsa.com/tsa", HttpClient = httpClient };
                await OdfSigner.SignAsync(packageB, signerCert, options);
                packageB.Save();
            }

            string tsTokenB = "";
            msB.Position = 0;
            using (var packageB = OdfPackage.Open(msB))
            {
                using var sigStream = packageB.GetEntryStream("META-INF/documentsignatures.xml");
                var doc = new XmlDocument();
                doc.Load(sigStream);
                var ns = new XmlNamespaceManager(doc.NameTable);
                ns.AddNamespace("xades", "http://uri.etsi.org/01903/v1.3.2#");
                var encap = doc.SelectSingleNode("//xades:SignatureTimeStamp/xades:EncapsulatedTimeStamp", ns);
                Assert.NotNull(encap);
                tsTokenB = encap.InnerText.Trim();
            }

            msA.Position = 0;
            byte[] tamperedXmlBytes;
            using (var packageA = OdfPackage.Open(msA, leaveOpen: true))
            {
                using var sigStream = packageA.GetEntryStream("META-INF/documentsignatures.xml");
                var doc = new XmlDocument();
                doc.Load(sigStream);
                var ns = new XmlNamespaceManager(doc.NameTable);
                ns.AddNamespace("xades", "http://uri.etsi.org/01903/v1.3.2#");
                var encap = doc.SelectSingleNode("//xades:SignatureTimeStamp/xades:EncapsulatedTimeStamp", ns);
                Assert.NotNull(encap);
                encap.InnerText = tsTokenB;
                
                using var outMs = new MemoryStream();
                doc.Save(outMs);
                tamperedXmlBytes = outMs.ToArray();
            }

            using (var packageA = OdfPackage.Open(msA, leaveOpen: true))
            {
                packageA.WriteEntry("META-INF/documentsignatures.xml", tamperedXmlBytes, "text/xml");
                packageA.Save();
            }

            msA.Position = 0;
            using (var packageA = OdfPackage.Open(msA))
            {
                using var sigStream = packageA.GetEntryStream("META-INF/documentsignatures.xml");
                var doc = new XmlDocument();
                doc.Load(sigStream);
                var ns = new XmlNamespaceManager(doc.NameTable);
                ns.AddNamespace("ds", OdfNamespaces.Ds);
                ns.AddNamespace("xades", "http://uri.etsi.org/01903/v1.3.2#");
                var sigVal = doc.SelectSingleNode("//ds:SignatureValue", ns)?.InnerText;
                var encap = doc.SelectSingleNode("//xades:SignatureTimeStamp/xades:EncapsulatedTimeStamp", ns)?.InnerText;
                
                // Let's also check packageB
                using var msB2 = new MemoryStream(msB.ToArray());
                using var packageB = OdfPackage.Open(msB2);
                using var sigStreamB = packageB.GetEntryStream("META-INF/documentsignatures.xml");
                var docB = new XmlDocument();
                docB.Load(sigStreamB);
                var sigValB = docB.SelectSingleNode("//ds:SignatureValue", ns)?.InnerText;
                var encapB = docB.SelectSingleNode("//xades:SignatureTimeStamp/xades:EncapsulatedTimeStamp", ns)?.InnerText;

                var options = new OdfSigningOptions { HttpClient = httpClient, AllowUntrustedRoot = true, AllowUntrustedTimestamp = true };
                var result = await OdfSigner.VerifySignaturesAsync(packageA, options);

                var sigValElem = doc.SelectSingleNode("//ds:SignatureValue", ns) as XmlElement;
                var cleanDoc = new XmlDocument();
                var imported = (XmlElement)cleanDoc.ImportNode(sigValElem!, true);
                cleanDoc.AppendChild(imported);
                var transform = new System.Security.Cryptography.Xml.XmlDsigExcC14NTransform();
                transform.LoadInput(imported.SelectNodes("descendant-or-self::node()")!);
                using var tsStream = (Stream)transform.GetOutput(typeof(Stream));
                using var tsMs = new MemoryStream();
                tsStream.CopyTo(tsMs);
                byte[] sigBytes = tsMs.ToArray();
                using var sha256 = SHA256.Create();
                byte[] calculatedHash = sha256.ComputeHash(sigBytes);

                byte[] tsBytes = Convert.FromBase64String(encap!);
                var signedCms = new SignedCms();
                signedCms.Decode(tsBytes);
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

                // Verify that the spoofed timestamp is rejected (IsTimestampValid = false, result.IsValid = false)
                Assert.False(result.IsValid);
                Assert.False(result.Signatures.FirstOrDefault()?.IsTimestampValid);
                Assert.NotEqual(calculatedHash, embeddedHash);
            }
        }

        [Fact]
        public void TestCrlAcceptsSpoofedSignature()
        {
            var (rootCert, leafCert) = GenerateCertificateChain("XadesARootCA", "XadesATestSigner");
            
            // Create a CRL containing the leafCert's serial, signed with a completely fake/dummy signature
            byte[] revokedCrlBytes = CreateMockCrlBytes(rootCert, new List<string> { leafCert.SerialNumber }, useInvalidSignature: true);

            var signerType = typeof(OdfSigner);
            var getTbsNodeMethod = signerType.GetMethod("GetTbsNode", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(getTbsNodeMethod);
            var tbsNode = getTbsNodeMethod.Invoke(null, new object?[] { revokedCrlBytes });
            Assert.NotNull(tbsNode);

            var getCrlIssuerDerMethod = signerType.GetMethod("GetCrlIssuerDer", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(getCrlIssuerDerMethod);
            var crlIssuer = (byte[]?)getCrlIssuerDerMethod.Invoke(null, new object?[] { tbsNode });
            Assert.NotNull(crlIssuer);
            
            var structuralEqualMethod = signerType.GetMethod("StructuralEqual", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(structuralEqualMethod);
            var isIssuerEqualVal = structuralEqualMethod.Invoke(null, new object?[] { crlIssuer, leafCert.IssuerName.RawData });
            Assert.NotNull(isIssuerEqualVal);
            bool isIssuerEqual = (bool)isIssuerEqualVal;
            Assert.True(isIssuerEqual);

            // Verify the revoked serial is successfully extracted, despite the CRL having a fake signature
            var getRevokedSerialNumbersMethod = signerType.GetMethod("GetRevokedSerialNumbers", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(getRevokedSerialNumbersMethod);
            var revoked = (HashSet<string>?)getRevokedSerialNumbersMethod.Invoke(null, new object?[] { revokedCrlBytes });
            Assert.NotNull(revoked);
            
            var normalizeHexSerialMethod = signerType.GetMethod("NormalizeHexSerial", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(normalizeHexSerialMethod);
            var normalizedSerial = (string?)normalizeHexSerialMethod.Invoke(null, new object?[] { leafCert.SerialNumber });
            Assert.NotNull(normalizedSerial);
                
            Assert.Contains(normalizedSerial!, revoked);
        }

        [Fact]
        public async Task TestSignatureValueCanonicalizationIsEmptyElement()
        {
            using var signerCert = GenerateSelfSignedCertificate("SignerCert", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(5));
            using var tsaCert = GenerateSelfSignedCertificate("MockTSA", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(5));
            
            var mockHandler = new MockHttpMessageHandler(request =>
            {
                byte[] reqBytes = request.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                var root = ParseDer(reqBytes);
                byte[] hash = root.Children[1].Children[1].Value;
                byte[] tstInfoBytes = CreateMockTstInfoBytes(hash);
                
                var contentInfo = new ContentInfo(new Oid("1.2.840.113549.1.9.16.1.4"), tstInfoBytes);
                var signedCms = new SignedCms(contentInfo, false);
                signedCms.ComputeSignature(new CmsSigner(tsaCert));
                
                byte[] tsaResponse = CreateTsaResponse(signedCms.Encode());
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(tsaResponse) };
            });

            using var httpClient = new HttpClient(mockHandler);
            using var ms = new MemoryStream();
            
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                package.SetMimeType("application/vnd.oasis.opendocument.text");
                package.WriteEntry("content.xml", Encoding.UTF8.GetBytes("<content/>"), "text/xml");
                
                var options = new OdfSigningOptions { Level = XadesLevel.T, TsaUrl = "http://mocktsa.com/tsa", HttpClient = httpClient };
                await OdfSigner.SignAsync(package, signerCert, options);
                package.Save();
            }

            ms.Position = 0;
            using (var package = OdfPackage.Open(ms))
            {
                using var sigStream = package.GetEntryStream("META-INF/documentsignatures.xml");
                var doc = new XmlDocument();
                doc.Load(sigStream);
                
                var nsManager = new XmlNamespaceManager(doc.NameTable);
                nsManager.AddNamespace("ds", OdfNamespaces.Ds);
                nsManager.AddNamespace("xades", "http://uri.etsi.org/01903/v1.3.2#");
                
                var sigValElem = doc.SelectSingleNode("//ds:SignatureValue", nsManager) as XmlElement;
                Assert.NotNull(sigValElem);

                var cleanDoc = new XmlDocument();
                var imported = (XmlElement)cleanDoc.ImportNode(sigValElem, true);
                cleanDoc.AppendChild(imported);

                var transform = new System.Security.Cryptography.Xml.XmlDsigExcC14NTransform();
                transform.LoadInput(imported.SelectNodes("descendant-or-self::node()")!);
                using var tsStream = (Stream)transform.GetOutput(typeof(Stream));
                using var tsMs = new MemoryStream();
                tsStream.CopyTo(tsMs);
                byte[] sigValueBytes = tsMs.ToArray();

                using var sha256 = SHA256.Create();
                byte[] sigHash = sha256.ComputeHash(sigValueBytes);

                string canonString = Encoding.UTF8.GetString(sigValueBytes);
                string hashB64 = Convert.ToBase64String(sigHash);

                // Assert that the canonicalized element is NOT empty and has correct hash (since we fixed the bug where SelectNodes(".") excluded the text child node)
                Assert.NotEqual("<SignatureValue xmlns=\"http://www.w3.org/2000/09/xmldsig#\"></SignatureValue>", canonString);
                Assert.NotEqual("ieYpe7QxZ6H5gVXFXFLmt7frmNdP/a4Wtta3PFJhniY=", hashB64);
                Assert.Contains(sigValElem.InnerText.Trim(), canonString);
            }
        }

        [Fact]
        public async Task TestSignatureVerificationFailsWithFakeSignedCrl()
        {
            var cdpBytes = new byte[] {
                0x30, 0x29, 0x30, 0x27, 0xa0, 0x25, 0xa0, 0x23, 0x86, 0x21,
                (byte)'h', (byte)'t', (byte)'t', (byte)'p', (byte)':', (byte)'/', (byte)'/', 
                (byte)'m', (byte)'o', (byte)'c', (byte)'k', (byte)'c', (byte)'r', (byte)'l', (byte)'.', (byte)'c', (byte)'o', (byte)'m', 
                (byte)'/', (byte)'r', (byte)'e', (byte)'v', (byte)'o', (byte)'c', (byte)'a', (byte)'t', (byte)'i', (byte)'o', (byte)'n', 
                (byte)'.', (byte)'c', (byte)'r', (byte)'l'
            };

            var (rootCert, leafCert) = GenerateCertificateChain("XadesARootCA", "XadesATestSigner", cdpBytes);
            using var signerCA = rootCert;
            using var signerCert = leafCert;
            using var tsaCert = GenerateSelfSignedCertificate("MockTSA", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(5));

            // Generate mock CRL but with completely invalid signature (fake)
            byte[] fakeCrlBytes = CreateMockCrlBytes(signerCA, new List<string>(), useInvalidSignature: true);

            var mockHandler = new MockHttpMessageHandler(request =>
            {
                string url = request.RequestUri?.AbsoluteUri ?? "";
                if (url == "http://mockcrl.com/revocation.crl")
                {
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(fakeCrlBytes) };
                }
                else if (url == "http://mocktsa.com/tsa")
                {
                    byte[] reqBytes = request.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                    var root = ParseDer(reqBytes);
                    byte[] hash = root.Children[1].Children[1].Value;
                    byte[] tstInfoBytes = CreateMockTstInfoBytes(hash);
                    
                    var contentInfo = new ContentInfo(new Oid("1.2.840.113549.1.9.16.1.4"), tstInfoBytes);
                    var signedCms = new SignedCms(contentInfo, false);
                    signedCms.ComputeSignature(new CmsSigner(tsaCert));
                    
                    byte[] tsaResponse = CreateTsaResponse(signedCms.Encode());
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(tsaResponse) };
                }
                return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
            });

            using var httpClient = new HttpClient(mockHandler);
            using var ms = new MemoryStream();
            
            // Sign the package first (bypass revocation checking during signing)
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                package.SetMimeType("application/vnd.oasis.opendocument.text");
                package.WriteEntry("content.xml", Encoding.UTF8.GetBytes("<content/>"), "text/xml");
                
                var options = new OdfSigningOptions
                {
                    Level = XadesLevel.A,
                    TsaUrl = "http://mocktsa.com/tsa",
                    HttpClient = httpClient,
                    CheckRevocation = false,
                    AllowUntrustedRoot = true
                };
                options.ExtraCertificates.Add(signerCA);

                await OdfSigner.SignAsync(package, signerCert, options);
                package.Save();
            }

            // Verify package validation fails when CheckRevocation is true and the CRL has a fake signature
            ms.Position = 0;
            using (var package = OdfPackage.Open(ms))
            {
                var options = new OdfSigningOptions
                {
                    HttpClient = httpClient,
                    CheckRevocation = true,
                    AllowUntrustedRoot = true
                };
                options.ExtraCertificates.Add(signerCA);

                var result = await OdfSigner.VerifySignaturesAsync(package, options);
                Assert.False(result.IsValid);
                Assert.False(result.Signatures[0].IsRevocationValid);
                Assert.Equal("CRL_SIGNATURE_INVALID", result.Signatures[0].ErrorCode);
            }
        }

        [Fact]
        public async Task TestCoSigningWithTimestamps()
        {
            using var certA = GenerateSelfSignedCertificate("SignerA", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(5));
            using var certB = GenerateSelfSignedCertificate("SignerB", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(5));
            using var tsaCert = GenerateSelfSignedCertificate("MockTSA", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(5));

            var mockHandler = new MockHttpMessageHandler(request =>
            {
                if (request.RequestUri?.AbsoluteUri == "http://mocktsa.com/tsa")
                {
                    byte[] reqBytes = request.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                    var root = ParseDer(reqBytes);
                    byte[] hash = root.Children[1].Children[1].Value;
                    byte[] tstInfoBytes = CreateMockTstInfoBytes(hash);
                    
                    var contentInfo = new ContentInfo(new Oid("1.2.840.113549.1.9.16.1.4"), tstInfoBytes);
                    var signedCms = new SignedCms(contentInfo, false);
                    signedCms.ComputeSignature(new CmsSigner(tsaCert));
                    
                    byte[] tsaResponse = CreateTsaResponse(signedCms.Encode());
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(tsaResponse) };
                }
                return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
            });

            using var httpClient = new HttpClient(mockHandler);
            using var ms = new MemoryStream();

            // 1. Create a package and sign with Cert A + timestamp
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                package.SetMimeType("application/vnd.oasis.opendocument.text");
                package.WriteEntry("content.xml", Encoding.UTF8.GetBytes("<content/>"), "text/xml");
                
                var options = new OdfSigningOptions
                {
                    Level = XadesLevel.T,
                    TsaUrl = "http://mocktsa.com/tsa",
                    HttpClient = httpClient
                };
                await OdfSigner.SignAsync(package, certA, options);
                package.Save();
            }

            // 2. Open package and co-sign with Cert B + timestamp
            ms.Position = 0;
            using (var package = OdfPackage.Open(ms, leaveOpen: true))
            {
                var options = new OdfSigningOptions
                {
                    Level = XadesLevel.T,
                    TsaUrl = "http://mocktsa.com/tsa",
                    HttpClient = httpClient
                };
                await OdfSigner.SignAsync(package, certB, options);
                package.Save();
            }

            // 3. Open package and verify both signatures and timestamps are valid
            ms.Position = 0;
            using (var package = OdfPackage.Open(ms))
            {
                var options = new OdfSigningOptions
                {
                    HttpClient = httpClient,
                    AllowUntrustedRoot = true,
                    AllowUntrustedTimestamp = true
                };
                var result = await OdfSigner.VerifySignaturesAsync(package, options);
                
                Assert.True(result.IsValid, result.Signatures.FirstOrDefault(s => !s.IsSignatureValid)?.ErrorMessage);
                Assert.Equal(2, result.Signatures.Count);
                
                Assert.True(result.Signatures[0].IsSignatureValid);
                Assert.True(result.Signatures[0].IsTimestampValid);
                
                Assert.True(result.Signatures[1].IsSignatureValid);
                Assert.True(result.Signatures[1].IsTimestampValid);
            }
        }
    }

    public class MockHttpMessageHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> HandlerFunc { get; set; }

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handlerFunc)
        {
            HandlerFunc = handlerFunc;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        {
            return Task.FromResult(HandlerFunc(request));
        }
    }

    internal class DerNode
    {
        public byte Tag { get; set; }
        public byte[] Value { get; set; }
        public List<DerNode> Children { get; } = new List<DerNode>();

        public DerNode(byte tag, byte[] value)
        {
            Tag = tag;
            Value = value;
        }
    }
}
