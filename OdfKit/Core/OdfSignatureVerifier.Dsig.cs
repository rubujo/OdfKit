using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace OdfKit.Core;

internal static partial class OdfSignatureVerifier
{
    private static bool TryCollectEmbeddedCrls(
        XmlNode signatureNode,
        XmlNamespaceManager nsManager,
        OdfSingleSignatureValidationResult singleResult,
        out List<byte[]> embeddedCrls)
    {
        embeddedCrls = [];
        var crlValueNodes = signatureNode.SelectNodes(".//xades:EncapsulatedCRLValue", nsManager);
        if (crlValueNodes == null)
            return true;

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
                return false;
            }
        }

        return true;
    }

    private static X509Certificate2? TryExtractSigningCertificate(XadesSignedXml signedXml)
    {
        if (signedXml.KeyInfo == null)
            return null;

        foreach (KeyInfoClause clause in signedXml.KeyInfo)
        {
            if (clause is KeyInfoX509Data x509Data && x509Data.Certificates != null)
            {
                foreach (var certObj in x509Data.Certificates)
                {
                    if (certObj is X509Certificate2 x509Cert)
                        return x509Cert;
                }
            }
        }

        return null;
    }

    private static bool VerifyCryptographicXmlSignature(
        XadesSignedXml signedXml,
        X509Certificate2 cert,
        OdfPackage package,
        OdfSingleSignatureValidationResult singleResult)
    {
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
                            OdfSignatureX509Utilities.InjectReferenceStream(reference, stream);
                        }
                    }
                }
            }

            isSignatureValid = signedXml.CheckSignature(cert, true);
        }
        finally
        {
            foreach (var stream in openStreams)
                stream.Dispose();
        }

        singleResult.IsSignatureValid = isSignatureValid;
        if (!isSignatureValid)
        {
            singleResult.ErrorCode = "CRYPTOGRAPHIC_SIGNATURE_INVALID";
            singleResult.ErrorMessage = "XML signature verification failed (cryptographically invalid).";
            return false;
        }

        return true;
    }

    private static bool VerifyCertificateValidityPeriod(X509Certificate2 cert, OdfSingleSignatureValidationResult singleResult)
    {
        singleResult.ValidationSteps.Add("2. Verifying certificate validity period...");
        var now = DateTime.UtcNow;
        var notBeforeUtc = cert.NotBefore.ToUniversalTime();
        var notAfterUtc = cert.NotAfter.ToUniversalTime();
        singleResult.IsCertificateValid = now >= notBeforeUtc && now <= notAfterUtc;
        if (!singleResult.IsCertificateValid)
        {
            singleResult.ErrorCode = "CERTIFICATE_EXPIRED";
            singleResult.ErrorMessage = "Signing certificate is expired or not yet valid.";
            return false;
        }

        return true;
    }

    private static bool TryBuildCertificateChain(
        XmlNode signatureNode,
        X509Certificate2 cert,
        OdfSigningOptions options,
        XmlNamespaceManager nsManager,
        OdfSingleSignatureValidationResult singleResult,
        out List<X509Certificate2> chainCerts)
    {
        chainCerts = [];
        singleResult.ValidationSteps.Add("4. Verifying certificate trust chain...");
        var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        foreach (var embeddedCertificate in OdfSignatureX509Utilities.GetEmbeddedCertificates(signatureNode, nsManager))
        {
            if (!OdfEncryption.ByteArrayEquals(embeddedCertificate.RawData, cert.RawData))
                chain.ChainPolicy.ExtraStore.Add(embeddedCertificate);
        }

        foreach (var extraCertificate in options.ExtraCertificates)
        {
            if (!OdfEncryption.ByteArrayEquals(extraCertificate.RawData, cert.RawData))
                chain.ChainPolicy.ExtraStore.Add(extraCertificate);
        }

        bool isChainValid = chain.Build(cert);
        if (!isChainValid && options.AllowUntrustedRoot)
        {
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
            return false;
        }

        foreach (var el in chain.ChainElements)
            chainCerts.Add(el.Certificate);

        return true;
    }

    private static async Task<bool> VerifySingleSignatureAsync(
        XmlNode signatureNode,
        XmlDocument doc,
        OdfPackage package,
        OdfSigningOptions options,
        XmlNamespaceManager nsManager,
        OdfSingleSignatureValidationResult singleResult,
        CancellationToken cancellationToken = default)
    {
        var signedXml = new XadesSignedXml(doc)
        {
            Resolver = new OdfPackageXmlResolver(package)
        };
        signedXml.LoadXml((XmlElement)signatureNode);

        if (!TryCollectEmbeddedCrls(signatureNode, nsManager, singleResult, out List<byte[]> embeddedCrls))
            return false;

        try
        {
            X509Certificate2? cert = TryExtractSigningCertificate(signedXml);
            if (cert == null)
            {
                singleResult.ErrorCode = "CERTIFICATE_MISSING";
                singleResult.ErrorMessage = "Signature key info does not contain a valid X509 certificate.";
                return false;
            }

            singleResult.Certificate = cert;

            if (!VerifyCryptographicXmlSignature(signedXml, cert, package, singleResult))
                return false;

            if (!VerifyCertificateValidityPeriod(cert, singleResult))
                return false;

            singleResult.ValidationSteps.Add("3. Verifying signing certificate digest...");
            if (!OdfSignatureX509Utilities.VerifySigningCertificateDigest((XmlElement)signatureNode, cert, out string? digestError))
            {
                singleResult.ErrorCode = "CERTIFICATE_DIGEST_MISMATCH";
                singleResult.ErrorMessage = digestError;
                return false;
            }

            if (!TryBuildCertificateChain(signatureNode, cert, options, nsManager, singleResult, out List<X509Certificate2> chainCerts))
                return false;

            if (!await VerifyRevocationStatusAsync(chainCerts, embeddedCrls, options, singleResult, cancellationToken)
                .ConfigureAwait(false))
                return false;

            if (!VerifySignatureTimestamp(signatureNode, nsManager, options, singleResult))
                return false;

            CollectCheckedReferences(signedXml, singleResult);
            return true;
        }
        catch (Exception ex)
        {
            singleResult.ErrorMessage = $"Verification error: {ex.Message}";
            if (string.IsNullOrEmpty(singleResult.ErrorCode))
                singleResult.ErrorCode = "VERIFICATION_ERROR";
            return false;
        }
    }

    private static void CollectCheckedReferences(XadesSignedXml signedXml, OdfSingleSignatureValidationResult singleResult)
    {
        if (signedXml.SignedInfo == null)
            return;

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
