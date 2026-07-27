using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.Xml;
using System.Xml;

using OdfKit.Compliance;

using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Tsp;

namespace OdfKit.Core;

internal static partial class OdfSignatureVerifier
{
    private static bool VerifySignatureTimestamp(
        XmlNode signatureNode,
        XmlNamespaceManager nsManager,
        OdfSigningOptions options,
        OdfSingleSignatureValidationResult singleResult)
    {
        singleResult.ValidationSteps.Add("6. Verifying signature timestamp...");
        var timestampNode = signatureNode.SelectSingleNode(".//xades:SignatureTimeStamp/xades:EncapsulatedTimeStamp", nsManager);
        if (timestampNode == null)
            return true;

        var signedCms = new SignedCms();
        try
        {
            byte[] tsBytes = DecodeBase64WithLimit(
                timestampNode.InnerText,
                MaxEmbeddedTimestampBytes,
                "Err_OdfSignatureVerifier_EmbeddedTimestampSizeLimitExceeded");
            signedCms.Decode(tsBytes);

            try
            {
                signedCms.CheckSignature(false);
            }
            catch (CryptographicException)
            {
                if (options.AllowUntrustedTimestamp)
                    signedCms.CheckSignature(true);
                else
                    throw;
            }
        }
        catch (Exception ex)
        {
            return FailWithWarning(
                () => singleResult.IsTimestampValid = false,
                singleResult,
                "TIMESTAMP_SIGNATURE_INVALID",
                "Err_OdfSignatureVerifier_TimestampSignatureVerificationFailed",
                ex.Message);
        }

        var signatureValueElem = signatureNode.SelectSingleNode("ds:SignatureValue", nsManager) as XmlElement;
        if (signatureValueElem == null)
        {
            return Fail(
                () => singleResult.IsTimestampValid = false,
                singleResult,
                "TIMESTAMP_IMPRINT_MISMATCH",
                "Err_OdfSignatureVerifier_MissingSignatureValueForTimestamp");
        }

        byte[] sigBytes = OdfSignatureTsaClient.CanonicalizeSignatureValue(signatureValueElem);

        byte[] calculatedHash = global::OdfKit.Internal.OdfHashHelper.Sha256(sigBytes);

        byte[]? embeddedHash = null;
        try
        {
            var tstInfo = TstInfo.GetInstance(Asn1Object.FromByteArray(signedCms.ContentInfo.Content));
            embeddedHash = tstInfo.MessageImprint.GetHashedMessage();
        }
        catch (Exception ex)
        {
            OdfKitDiagnostics.Warn($"TSTInfo parsing exception: {ex.Message}");
        }

        if (embeddedHash == null || !OdfEncryption.ByteArrayEquals(calculatedHash, embeddedHash))
        {
            return Fail(
                () => singleResult.IsTimestampValid = false,
                singleResult,
                "TIMESTAMP_IMPRINT_MISMATCH",
                "Err_OdfSignatureVerifier_TimestampMessageImprintMismatch");
        }

        return true;
    }
}
