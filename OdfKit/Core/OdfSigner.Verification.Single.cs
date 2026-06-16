using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Xml;

namespace OdfKit.Core;

public static partial class OdfSigner
{
    #region Verification - Single Signature

    private static async Task<bool> VerifySingleSignatureAsync(
        XmlNode signatureNode,
        XmlDocument doc,
        OdfPackage package,
        OdfSigningOptions options,
        XmlNamespaceManager nsManager,
        OdfSingleSignatureValidationResult singleResult)
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
            if (!VerifySigningCertificateDigest((XmlElement)signatureNode, cert, out string? digestError))
            {
                singleResult.ErrorCode = "CERTIFICATE_DIGEST_MISMATCH";
                singleResult.ErrorMessage = digestError;
                return false;
            }

            if (!TryBuildCertificateChain(signatureNode, cert, options, nsManager, singleResult, out List<X509Certificate2> chainCerts))
                return false;

            if (!await VerifyRevocationStatusAsync(chainCerts, embeddedCrls, options, singleResult))
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

    #endregion
}
