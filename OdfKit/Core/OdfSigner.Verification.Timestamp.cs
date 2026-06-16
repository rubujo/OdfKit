using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.Xml;
using System.Xml;

namespace OdfKit.Core;

public static partial class OdfSigner
{
    #region Verification - Timestamp & References

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
            return false;
        }

        return true;
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

    internal static byte[] CanonicalizeSignatureValue(XmlElement signatureValueElem)
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

    #endregion
}
