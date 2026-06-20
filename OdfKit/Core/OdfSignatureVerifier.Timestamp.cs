using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.Xml;
using System.Xml;
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
            byte[] tsBytes = Convert.FromBase64String(timestampNode.InnerText.Trim());
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
            singleResult.IsTimestampValid = false;
            singleResult.ErrorCode = "TIMESTAMP_SIGNATURE_INVALID";
            singleResult.ErrorMessage = $"Timestamp signature verification failed: {ex.Message}";
            return false;
        }

        var signatureValueElem = signatureNode.SelectSingleNode("ds:SignatureValue", nsManager) as XmlElement;
        if (signatureValueElem == null)
        {
            singleResult.IsTimestampValid = false;
            singleResult.ErrorCode = "TIMESTAMP_IMPRINT_MISMATCH";
            singleResult.ErrorMessage = "Missing SignatureValue for timestamp verification.";
            return false;
        }

        byte[] sigBytes = OdfSignatureTsaClient.CanonicalizeSignatureValue(signatureValueElem);

        using var sha256 = SHA256.Create();
        byte[] calculatedHash = sha256.ComputeHash(sigBytes);

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
            singleResult.IsTimestampValid = false;
            singleResult.ErrorCode = "TIMESTAMP_IMPRINT_MISMATCH";
            singleResult.ErrorMessage = "Timestamp message imprint does not match the signature value.";
            return false;
        }

        return true;
    }
}
