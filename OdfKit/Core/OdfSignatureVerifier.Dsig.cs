using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

using OdfKit.Compliance;

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
                embeddedCrls.Add(DecodeBase64WithLimit(
                    crlNode.InnerText,
                    MaxEmbeddedCrlBytes,
                    "Err_OdfSignatureVerifier_EmbeddedCrlSizeLimitExceeded"));
            }
            catch (FormatException ex)
            {
                return FailWithWarning(
                    () => singleResult.IsRevocationValid = false,
                    singleResult,
                    "CRL_INVALID_FORMAT",
                    "Err_OdfSignatureVerifier_EmbeddedCrlNotBase64",
                    ex.Message);
            }
            catch (SecurityException ex)
            {
                return FailWithWarning(
                    () => singleResult.IsRevocationValid = false,
                    singleResult,
                    "CRL_SIZE_LIMIT_EXCEEDED",
                    "Err_OdfSignatureVerifier_EmbeddedCrlSizeLimitExceededNoLimit",
                    ex.Message);
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
                        string entryName = OdfPackageEntryNameSanitizer.NormalizeReferenceUri(uri);
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
            return Fail(
                () => { },
                singleResult,
                "CRYPTOGRAPHIC_SIGNATURE_INVALID",
                "Err_OdfSignatureVerifier_CryptographicSignatureInvalid");
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
            return Fail(
                () => { },
                singleResult,
                "CERTIFICATE_EXPIRED",
                "Err_OdfSignatureVerifier_CertificateNotYetValid");
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
            return Fail(
                () => { },
                singleResult,
                "CERTIFICATE_CHAIN_INVALID",
                "Err_OdfSignatureVerifier_CertificateChainInvalid");
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
                return Fail(
                    () => { },
                    singleResult,
                    "CERTIFICATE_MISSING",
                    "Err_OdfSignatureVerifier_SigningCertificateMissing");
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
            if (!VerifyPackageEntryCoverage(package, singleResult))
                return false;
            return true;
        }
        catch (Exception ex)
        {
            singleResult.ErrorMessage = OdfLocalizer.GetMessage("Err_OdfSignatureVerifier_VerificationError");
            singleResult.Warnings.Add(ex.Message);
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
                string entryName = OdfPackageEntryNameSanitizer.NormalizeReferenceUri(uri);
                singleResult.CheckedReferences.Add(entryName);
            }
        }
    }

    private static bool VerifyPackageEntryCoverage(
        OdfPackage package,
        OdfSingleSignatureValidationResult singleResult)
    {
        HashSet<string> covered = new(singleResult.CheckedReferences, StringComparer.Ordinal);
        foreach (string entryName in package.Entries.Keys)
        {
            string normalized = entryName.Replace('\\', '/').TrimStart('/');
            if (!ShouldRequireSignatureCoverage(normalized))
                continue;

            if (covered.Contains(normalized))
                continue;

            return Fail(
                () => singleResult.IsSignatureValid = false,
                singleResult,
                "UNSIGNED_PACKAGE_ENTRY",
                "Err_OdfSignatureVerifier_UnsignedPackageEntry",
                normalized);
        }

        return true;
    }

    private static bool ShouldRequireSignatureCoverage(string entryName)
    {
        return OdfSignerConstants.IsCoverableEntry(entryName);
    }
}
